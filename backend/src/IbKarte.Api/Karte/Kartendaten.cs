using System.Security.Cryptography;
using System.Text.Json;
using IbKarte.Api.Daten;
using IbKarte.Api.Import;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IbKarte.Api.Karte;

// Kompakte Darstellung für das Frontend. Bewusst ohne Quellen-Links (Gestaltungsentscheidung);
// die Belege bleiben in der Datenbank und über /api/eintraege/{id} abrufbar.
public record KarteOrt(string Key, string Name, double Lat, double Lon, string Genauigkeit, string? Symbol);
public record Ausschnitt(double X, double Y, double Zoom);
public record KarteEintrag(string Id, string Name, string Typ, string Umfeld, string Form, string Kurz, string? Ort,
    string? OrtName, string? Bild, string? BildQuelle, Ausschnitt? Ausschnitt);
public record KarteVerbindung(string Von, string Nach, string Text, string Status, string Art);
public record KarteDaten(string Stand, List<ArtDef> Arten, List<UmfeldDef> Umfelder, List<KarteOrt> Orte,
    List<KarteEintrag> Eintraege, List<KarteVerbindung> Verbindungen);

/// <summary>
/// Baut den Kartendatenstand einmal aus der Datenbank und hält ihn als fertiges, komprimierbares JSON im Speicher.
/// Nach einem Import wird er verworfen und beim nächsten Abruf neu gebaut.
/// </summary>
public class Kartendaten(IServiceScopeFactory scopes, IOptions<ImportOptionen> opt)
{
    public record Paket(byte[] Json, string ETag, DateTimeOffset ImportiertAm);

    readonly SemaphoreSlim _sperre = new(1, 1);
    volatile Paket? _paket;
    long _naechstePruefung;

    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    static readonly TimeSpan PruefAbstand = TimeSpan.FromSeconds(15);

    public void Verwerfen() => _paket = null;

    /// <summary>
    /// Liefert den fertigen Datenstand. Höchstens alle 15 Sekunden wird geprüft, ob inzwischen neu importiert wurde
    /// (auch durch einen anderen Prozess, z. B. „dotnet IbKarte.Api.dll import“).
    /// </summary>
    public async Task<Paket> HolenAsync(CancellationToken ct)
    {
        var p = _paket;
        if (p is not null && Environment.TickCount64 < Interlocked.Read(ref _naechstePruefung)) return p;
        await _sperre.WaitAsync(ct);
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KarteDb>();
            var importiert = await db.Datenstand.AsNoTracking().Select(d => (DateTimeOffset?)d.ImportiertAm).FirstOrDefaultAsync(ct) ?? DateTimeOffset.MinValue;
            Interlocked.Exchange(ref _naechstePruefung, Environment.TickCount64 + (long)PruefAbstand.TotalMilliseconds);
            if (_paket is { } q && q.ImportiertAm == importiert) return q;
            var daten = await BauenAsync(db, opt.Value.SeedVerzeichnis, ct);
            var json = JsonSerializer.SerializeToUtf8Bytes(daten, Json);
            var etag = "\"" + Convert.ToHexString(SHA256.HashData(json))[..20] + "\"";
            return _paket = new Paket(json, etag, importiert);
        }
        finally { _sperre.Release(); }
    }

    public static async Task<KarteDaten> BauenAsync(KarteDb db, string seedVerzeichnis, CancellationToken ct)
    {
        var seed = Startdaten.Laden(seedVerzeichnis);
        var stand = await db.Datenstand.AsNoTracking().Select(d => d.Stand).FirstOrDefaultAsync(ct) ?? "";
        var orte = await db.Orte.AsNoTracking().Where(o => o.Lat != null && o.Lon != null).ToListAsync(ct);
        var ortName = orte.ToDictionary(o => o.Key, o => o.Name);
        var eintraege = await db.Eintraege.AsNoTracking().OrderBy(e => e.Name).ToListAsync(ct);
        var verbindungen = await db.Verbindungen.AsNoTracking().OrderBy(v => v.Id).ToListAsync(ct);
        var genutzt = eintraege.Select(e => e.OrtKey).Where(k => k != null).ToHashSet();

        return new KarteDaten(
            stand,
            seed.Arten.Arten,
            seed.Regeln.Umfelder,
            orte.Where(o => genutzt.Contains(o.Key) || o.Symbol != null)
                .Select(o => new KarteOrt(o.Key, o.Name, o.Lat!.Value, o.Lon!.Value, o.Genauigkeit, o.Symbol)).ToList(),
            eintraege.Select(e => new KarteEintrag(e.Id, e.Name, e.Typ, e.Umfeld, e.Form, Text.ErsterSatz(e.Info),
                e.OrtKey is { } k && ortName.ContainsKey(k) ? k : null,
                e.OrtKey is { } k2 ? ortName.GetValueOrDefault(k2) : null,
                e.BildUrl, e.BildQuelle,
                e.BildX is { } x && e.BildY is { } y ? new Ausschnitt(x, y, e.BildZoom ?? 1) : null)).ToList(),
            verbindungen.Select(v => new KarteVerbindung(v.VonId, v.NachId, v.Text, v.Status, v.Art)).ToList());
    }
}
