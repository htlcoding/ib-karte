using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using IbKarte.Api.Daten;
using Microsoft.EntityFrameworkCore;

namespace IbKarte.Api.Import;

// ---------- Format von netzwerk.json ----------
public record NetzKnoten(string Id, string Name, string Typ, string Gruppe, string? Ordner, string Info, List<string>? Quellen,
    string? Bild, [property: JsonPropertyName("bild_quelle")] string? BildQuelle,
    [property: JsonPropertyName("bild_ausschnitt")] BildAusschnitt? BildAusschnitt);
/// <summary>Gesichtsausschnitt, z. B. {"x": 52, "y": 30, "zoom": 2.5}</summary>
public record BildAusschnitt(double X, double Y, double Zoom);
public record NetzKante(string Von, string Nach, string Label, string Status, List<string>? Quellen);
public record NetzQuelle(string Titel, string? Url);
public record Netzwerk(string Stand, List<NetzKnoten> Nodes, List<NetzKante> Edges, Dictionary<string, NetzQuelle> Quellen, List<string>? Luecken);

public record ImportBericht(string Stand, int Orte, int Eintraege, int Verbindungen, int Quellen, int PersonenVerortet,
    int OhneOrt, List<string> Warnungen);

public class ImportOptionen
{
    /// <summary>Pfad zu netzwerk.json</summary>
    public string Datei { get; set; } = "/daten/netzwerk.json";
    /// <summary>Verzeichnis mit orte.json, zuordnung.json, verbindungsarten.json, regeln.json</summary>
    public string SeedVerzeichnis { get; set; } = "/seed";
    /// <summary>Schlüssel für POST /api/admin/import. Leer = Endpunkt aus.</summary>
    public string? Schluessel { get; set; }
}

/// <summary>
/// Liest netzwerk.json und die Startdaten und ersetzt den gesamten Datenbestand in einer Transaktion.
/// Mehrfach ausführbar; das Ergebnis hängt nur von den Eingabedateien ab.
/// </summary>
public class Importer(KarteDb db, ILogger<Importer> log)
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static Netzwerk LiesNetzwerk(string datei) =>
        JsonSerializer.Deserialize<Netzwerk>(File.ReadAllText(datei), Json) ?? throw new InvalidDataException("netzwerk.json ist leer");

    public async Task<ImportBericht> AusfuehrenAsync(string datei, string seedVerzeichnis, CancellationToken ct = default)
    {
        var roh = await File.ReadAllBytesAsync(datei, ct);
        var netz = JsonSerializer.Deserialize<Netzwerk>(roh, Json) ?? throw new InvalidDataException("netzwerk.json ist leer");
        var seed = Startdaten.Laden(seedVerzeichnis);
        var warn = new List<string>();

        var arten = new VerbindungsKlassifikator(seed.Arten);
        var regeln = new EintragsRegeln(seed.Regeln);
        var position = new PersonenPosition(seed.Regeln);

        // Orte
        var orte = seed.Orte.ToDictionary(o => o.Key);
        // Einträge
        var knoten = new Dictionary<string, PersonenPosition.Knoten>();
        foreach (var n in netz.Nodes)
        {
            if (knoten.ContainsKey(n.Id)) { warn.Add($"Eintrag doppelt: {n.Id}"); continue; }
            knoten[n.Id] = new(n.Id, n.Name, n.Typ, regeln.Gruppe(n.Id, n.Typ, n.Name, n.Gruppe), n.Info ?? "");
        }
        // Verbindungen (nur zwischen bekannten Einträgen)
        var kanten = new List<NetzKante>();
        foreach (var e in netz.Edges)
        {
            if (!knoten.ContainsKey(e.Von) || !knoten.ContainsKey(e.Nach)) { warn.Add($"Verbindung mit unbekanntem Eintrag: {e.Von} → {e.Nach}"); continue; }
            if (e.Status is not ("belegt" or "vorwurf")) { warn.Add($"Unbekannter Status „{e.Status}“: {e.Von} → {e.Nach}"); continue; }
            kanten.Add(e);
        }
        // Orte bestimmen (Sitz laut Startdaten, sonst über Verbindungen; Personen symbolisch)
        foreach (var z in seed.Zuordnung.Where(z => knoten.ContainsKey(z.Key) && !orte.ContainsKey(z.Value)))
            warn.Add($"Zuordnung auf unbekannten Ort: {z.Key} → {z.Value}");
        var belegt = kanten.Where(e => e.Status == "belegt").Select(e => new Verortung.Kante(e.Von, e.Nach, e.Label, arten.Art(e.Label))).ToList();
        var verortung = Verortung.Berechne(knoten, belegt, seed.Zuordnung, orte.ToDictionary(x => x.Key, x => (x.Value.Genauigkeit, x.Value.Land)), position, seed.Regeln.Hauptorte);
        var ortAlle = verortung.Ort;
        var grund = verortung.Grund;
        var grad = knoten.Keys.ToDictionary(k => k, k => belegt.Count(e => e.Von == k || e.Nach == k));
        foreach (var n in netz.Nodes.Where(n => !ortAlle.ContainsKey(n.Id) && grad[n.Id] > 0))
            warn.Add($"Ohne Ort: {n.Name}");

        // ---------- Schreiben ----------
        var strategie = db.Database.CreateExecutionStrategy();
        await strategie.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlRawAsync(
                "TRUNCATE verbindung_quellen, eintrag_quellen, verbindungen, eintraege, quellen, orte, offene_fragen, datenstand RESTART IDENTITY CASCADE", ct);
            db.ChangeTracker.Clear();

            db.Orte.AddRange(seed.Orte.Select(o => new Ort
            {
                Key = o.Key, Name = o.Name, Adresse = o.Adresse, Lat = o.Lat, Lon = o.Lon, Genauigkeit = o.Genauigkeit,
                Quelle = o.Quelle, QuelleUrl = o.QuelleUrl, Land = o.Land, Symbol = o.Symbol
            }));
            db.Quellen.AddRange(netz.Quellen.Select(q => new Quelle { Id = q.Key, Titel = q.Value.Titel, Url = q.Value.Url }));
            var quellIds = netz.Quellen.Keys.ToHashSet();

            foreach (var n in netz.Nodes.DistinctBy(n => n.Id))
            {
                var fein = knoten[n.Id].Gruppe;
                db.Eintraege.Add(new Eintrag
                {
                    Id = n.Id, Name = n.Name, Typ = n.Typ, Gruppe = n.Gruppe, Umfeld = EintragsRegeln.Umfeld(fein),
                    Form = regeln.Form(n.Typ, n.Name), Info = n.Info ?? "", Ordner = n.Ordner,
                    OrtKey = ortAlle.GetValueOrDefault(n.Id), OrtGrund = grund.GetValueOrDefault(n.Id),
                    BildUrl = n.Bild, BildQuelle = n.BildQuelle,
                    BildX = n.BildAusschnitt is { } a ? Math.Clamp(a.X, 0, 100) : null,
                    BildY = n.BildAusschnitt is { } b ? Math.Clamp(b.Y, 0, 100) : null,
                    BildZoom = n.BildAusschnitt is { } c ? Math.Clamp(c.Zoom, 1, 10) : null,
                    Quellen = (n.Quellen ?? []).Distinct().Where(quellIds.Contains).Select(q => new EintragQuelle { EintragId = n.Id, QuelleId = q }).ToList()
                });
            }
            foreach (var e in kanten)
            {
                db.Verbindungen.Add(new Verbindung
                {
                    VonId = e.Von, NachId = e.Nach, Text = e.Label, Status = e.Status, Art = arten.Art(e.Label),
                    Quellen = (e.Quellen ?? []).Distinct().Where(quellIds.Contains).Select(q => new VerbindungQuelle { QuelleId = q }).ToList()
                });
            }
            db.OffeneFragen.AddRange((netz.Luecken ?? []).Select(t => new OffeneFrage { Text = t }));
            db.Datenstand.Add(new Datenstand { Stand = netz.Stand, ImportiertAm = DateTimeOffset.UtcNow, Pruefsumme = Convert.ToHexString(SHA256.HashData(roh))[..16] });

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });

        var bericht = new ImportBericht(netz.Stand, seed.Orte.Count, knoten.Count, kanten.Count, netz.Quellen.Count,
            ortAlle.Keys.Count(k => knoten[k].Typ == "person"), netz.Nodes.Count(n => !ortAlle.ContainsKey(n.Id)), warn);
        log.LogInformation("Import fertig: {Eintraege} Einträge, {Verbindungen} Verbindungen, {Warnungen} Warnungen",
            bericht.Eintraege, bericht.Verbindungen, warn.Count);
        return bericht;
    }
}
