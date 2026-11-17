using System.Text.Json;
using System.Text.RegularExpressions;

namespace IbKarte.Api.Import;

// ---------- Startdaten (seed/*.json) ----------

public record OrtSeed(string Key, string Name, string? Adresse, double? Lat, double? Lon, string Genauigkeit,
    string? Quelle, string? QuelleUrl, string? Land, string? Symbol = null);

public record ArtDef(string Key, string Name, string Farbe, bool Gestrichelt);
public record ArtRegel(string Art, string Muster);
public record VerbindungsArtenSeed(List<ArtDef> Arten, string Standard, List<ArtRegel> Regeln);

public record UmfeldDef(string Key, string Name, string Farbe);
public record FormRegel(string Muster, string Form);
public record AmtRegel(string Muster, string Ort, string Grund);
public record StadtDef(string Name, string Ort);
public record RegelnSeed(string ZentrumId, List<UmfeldDef> Umfelder, string FirmaMuster, List<string> KeineFirma,
    List<FormRegel> Formen, List<AmtRegel> Amt, string RolleMuster, string AmtAusschlussVorher, int AmtMaxPosition,
    List<StadtDef>? Staedte = null);

/// <summary>Alle Startdaten aus dem seed-Verzeichnis.</summary>
public class Startdaten
{
    public required List<OrtSeed> Orte { get; init; }
    public required Dictionary<string, string> Zuordnung { get; init; }
    public required VerbindungsArtenSeed Arten { get; init; }
    public required RegelnSeed Regeln { get; init; }

    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static Startdaten Laden(string verzeichnis)
    {
        T Lies<T>(string datei) =>
            JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(verzeichnis, datei)), Json)
            ?? throw new InvalidDataException($"{datei} ist leer");
        return new Startdaten
        {
            Orte = Lies<List<OrtSeed>>("orte.json"),
            Zuordnung = Lies<Dictionary<string, string>>("zuordnung.json"),
            Arten = Lies<VerbindungsArtenSeed>("verbindungsarten.json"),
            Regeln = Lies<RegelnSeed>("regeln.json"),
        };
    }
}

// ---------- Regeln anwenden ----------

/// <summary>Ordnet eine Verbindung anhand ihrer Beschriftung einer Art zu. Erste passende Regel gewinnt.</summary>
public class VerbindungsKlassifikator(VerbindungsArtenSeed seed)
{
    readonly List<(string Art, Regex Rx)> _regeln = seed.Regeln
        .Select(r => (r.Art, new Regex(r.Muster, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)))
        .ToList();

    public string Art(string text)
    {
        foreach (var (art, rx) in _regeln)
            if (rx.IsMatch(text)) return art;
        return seed.Standard;
    }
}

/// <summary>Farbgruppe und Organisationsform eines Eintrags.</summary>
public class EintragsRegeln(RegelnSeed r)
{
    readonly Regex _firma = new(r.FirmaMuster);
    readonly HashSet<string> _keineFirma = [.. r.KeineFirma];
    readonly List<(Regex Rx, string Form)> _formen = r.Formen.Select(f => (new Regex(f.Muster), f.Form)).ToList();

    /// <summary>Feine Gruppe wie in der Recherche-Auswertung (zentrum, kern, firmen, fpoe, …).</summary>
    public string Gruppe(string id, string typ, string name, string gruppe)
    {
        if (id == r.ZentrumId) return "zentrum";
        if (gruppe is "ereignis" or "medien") return gruppe;
        if (typ == "org" && !_keineFirma.Contains(id) && _firma.IsMatch(name)) return "firmen";
        return gruppe == "ib" ? "kern" : gruppe;
    }

    /// <summary>Farbgruppe auf der Karte.</summary>
    public static string Umfeld(string feineGruppe) => feineGruppe is "zentrum" or "kern" ? "ib" : feineGruppe;

    public string Form(string typ, string name)
    {
        switch (typ)
        {
            case "ereignis": return "Ereignis / Verfahren";
            case "medium": return "Medium";
            case "person": return "Person";
        }
        foreach (var (rx, form) in _formen)
            if (rx.IsMatch(name)) return form;
        return "Gruppe / Bewegung";
    }
}

/// <summary>
/// Symbolische Position einer Person: nie ein Wohnort, sondern der Ort ihrer belegten Funktion
/// (Mandat, Rolle bei einer Organisation) oder ersatzweise der Eintrag mit den meisten Verbindungen.
/// </summary>
public class PersonenPosition(RegelnSeed r)
{
    readonly List<(Regex Rx, string Ort, string Grund)> _amt = r.Amt.Select(a => (new Regex(a.Muster), a.Ort, a.Grund)).ToList();
    readonly Regex _ausschluss = new(r.AmtAusschlussVorher);
    readonly Regex _rolle = new(r.RolleMuster, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    readonly List<(Regex Rx, string Name, string Ort)> _staedte = (r.Staedte ?? [])
        .Select(s => (new Regex(@"\b" + Regex.Escape(s.Name) + @"(?:er)?\b"), s.Name, s.Ort)).ToList();

    public record Knoten(string Id, string Name, string Typ, string Gruppe, string Info);
    public record Kante(string Partner, string Text);

    /// <param name="person">die Person</param>
    /// <param name="kanten">belegte Verbindungen der Person, in Datensatz-Reihenfolge</param>
    /// <param name="knoten">alle Einträge</param>
    /// <param name="ortVon">Ort-Key je Nicht-Person</param>
    /// <param name="genauigkeit">Genauigkeit je Ort-Key</param>
    /// <param name="grad">Anzahl belegter Verbindungen je Eintrag</param>
    public (string Ort, string Grund)? Bestimme(Knoten person, IReadOnlyList<Kante> kanten,
        IReadOnlyDictionary<string, Knoten> knoten, IReadOnlyDictionary<string, string> ortVon,
        IReadOnlyDictionary<string, string> genauigkeit, IReadOnlyDictionary<string, int> grad)
    {
        // 1. Mandat im Infotext (nur am Anfang, nicht bei Mitarbeitern, Interviews o. Ä.)
        foreach (var (rx, ort, grund) in _amt)
        {
            var m = rx.Match(person.Info);
            if (!m.Success) continue;
            var davor = person.Info[Math.Max(0, m.Index - 50)..m.Index];
            if (m.Index > r.AmtMaxPosition || _ausschluss.IsMatch(davor)) continue;
            return (ort, grund);
        }

        // 2. Belegte Rolle bei einer Organisation mit Ort
        (bool, bool, int)? besteWertung = null;
        string? besteOrg = null, besteRolle = null;
        foreach (var k in kanten)
        {
            if (!ortVon.TryGetValue(k.Partner, out var ok)) continue;
            var o = knoten[k.Partner];
            if (o.Typ is "person" or "ereignis" || genauigkeit[ok] == "bezirk") continue;
            if (!_rolle.IsMatch(k.Text)) continue;
            var gleich = o.Gruppe == person.Gruppe || (person.Gruppe == "kern" && o.Gruppe is "zentrum" or "kern" or "firmen");
            var flaeche = genauigkeit[ok] is "land" or "region" or "aussen";
            var wertung = (gleich, !flaeche, grad.GetValueOrDefault(k.Partner));
            if (besteWertung is null || Vergleiche(wertung, besteWertung.Value) > 0)
            {
                besteWertung = wertung; besteOrg = k.Partner; besteRolle = k.Text;
            }
        }
        if (besteOrg is not null)
            return Verfeinern(person, besteRolle!, (ortVon[besteOrg], $"über belegte Rolle bei „{knoten[besteOrg].Name}“ ({besteRolle})"), genauigkeit);

        // 3. Ersatz: Eintrag mit den meisten Verbindungen
        var punkte = new Dictionary<string, int>();
        var reihenfolge = new List<string>();
        foreach (var k in kanten)
        {
            if (!ortVon.ContainsKey(k.Partner)) continue;
            if (!punkte.ContainsKey(k.Partner)) { punkte[k.Partner] = 0; reihenfolge.Add(k.Partner); }
            punkte[k.Partner] += knoten[k.Partner].Typ is "org" or "medium" ? 3 : 1;
        }
        if (reihenfolge.Count == 0) return null;
        var beste = reihenfolge[0];
        foreach (var x in reihenfolge.Skip(1))
            if ((punkte[x], grad.GetValueOrDefault(x)).CompareTo((punkte[beste], grad.GetValueOrDefault(beste))) > 0) beste = x;
        return Verfeinern(person, "", (ortVon[beste], $"keine Funktion mit Ort belegt – symbolisch beim Eintrag „{knoten[beste].Name}“, mit dem die meisten Verbindungen bestehen"), genauigkeit);
    }

    /// <summary>
    /// Liegt der Ort nur auf Landes- oder Regionsebene, wird die Stadt genommen, die die Funktion
    /// oder der Anfang des Infotexts nennt (z. B. „FJ-Chef Wien“ → Wien).
    /// </summary>
    (string Ort, string Grund) Verfeinern(Knoten person, string rolle, (string Ort, string Grund) ergebnis, IReadOnlyDictionary<string, string> genauigkeit)
    {
        if (genauigkeit[ergebnis.Ort] is not ("land" or "region" or "aussen")) return ergebnis;
        var anfang = person.Info.Length > 120 ? person.Info[..120] : person.Info;
        foreach (var (quelle, text) in new[] { ("Funktion", rolle), ("Kurzbeschreibung", anfang) })
            foreach (var (rx, name, ort) in _staedte)
                if (rx.IsMatch(text)) return (ort, ergebnis.Grund + $"; genauer: {name} laut {quelle}");
        return ergebnis;
    }

    static int Vergleiche((bool, bool, int) a, (bool, bool, int) b)
    {
        var c = a.Item1.CompareTo(b.Item1);
        if (c != 0) return c;
        c = a.Item2.CompareTo(b.Item2);
        return c != 0 ? c : a.Item3.CompareTo(b.Item3);
    }
}

public static class Text
{
    static readonly Regex Satz = new(@"^.{40,240}?[.;:](\s|$)");

    /// <summary>Kurzbeschreibung: erster Satz, höchstens etwa 240 Zeichen.</summary>
    public static string ErsterSatz(string t)
    {
        t = (t ?? "").Trim();
        var m = Satz.Match(t);
        if (m.Success) t = m.Value.Trim();
        else if (t.Length > 240)
        {
            var cut = t.LastIndexOf(' ', 236);
            t = t[..(cut > 0 ? cut : 236)] + " …";
        }
        return t.EndsWith(';') || t.EndsWith(':') ? t[..^1] + "." : t;
    }
}
