using System.IO.Compression;
using IbKarte.Api.Daten;
using IbKarte.Api.Import;
using IbKarte.Api.Karte;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<KarteDb>(o => o.UseNpgsql(
    builder.Configuration.GetConnectionString("Karte") ?? throw new InvalidOperationException("ConnectionStrings:Karte fehlt"),
    npg => npg.EnableRetryOnFailure(5)));
builder.Services.Configure<ImportOptionen>(builder.Configuration.GetSection("Import"));
builder.Services.AddScoped<Importer>();
builder.Services.AddSingleton<Kartendaten>();
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
    o.MimeTypes = ["application/json"];
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.AddHealthChecks().AddDbContextCheck<KarteDb>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// ---------- Kommandozeile: dotnet IbKarte.Api.dll import [datei] ----------
if (args.Length > 0 && args[0] == "import")
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<KarteDb>();
    await db.Database.MigrateAsync();
    var opt = scope.ServiceProvider.GetRequiredService<IOptions<ImportOptionen>>().Value;
    var datei = args.Length > 1 ? args[1] : opt.Datei;
    var bericht = await scope.ServiceProvider.GetRequiredService<Importer>().AusfuehrenAsync(datei, opt.SeedVerzeichnis);
    Console.WriteLine($"Stand {bericht.Stand}: {bericht.Eintraege} Einträge, {bericht.Verbindungen} Verbindungen, " +
                      $"{bericht.Orte} Orte, {bericht.Quellen} Quellen, {bericht.PersonenVerortet} Personen verortet, {bericht.OhneOrt} ohne Ort");
    foreach (var w in bericht.Warnungen) Console.WriteLine("  Hinweis: " + w);
    return;
}

// ---------- Kommandozeile: dotnet IbKarte.Api.dll export <ordner> [--nur-belegt] ----------
// Schreibt die Kartendaten als statische Dateien (für eine Vorzeige-Version ohne Server).
if (args.Length > 0 && args[0] == "export")
{
    var ordner = args.Length > 1 && !args[1].StartsWith("--") ? args[1] : "../../../frontend/public/statisch";
    var nurBelegt = args.Contains("--nur-belegt");
    Directory.CreateDirectory(ordner);
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<KarteDb>();
    var opt = scope.ServiceProvider.GetRequiredService<IOptions<ImportOptionen>>().Value;
    var daten = await Kartendaten.BauenAsync(db, opt.SeedVerzeichnis, CancellationToken.None);
    if (nurBelegt) daten = daten with { Verbindungen = daten.Verbindungen.Where(v => v.Status == "belegt").ToList() };
    var json = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
    await File.WriteAllBytesAsync(Path.Combine(ordner, "karte.json"), System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(daten, json));
    var texte = await db.Eintraege.AsNoTracking().ToDictionaryAsync(e => e.Id, e => e.Info);
    await File.WriteAllBytesAsync(Path.Combine(ordner, "texte.json"), System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(texte, json));
    Console.WriteLine($"Export nach {Path.GetFullPath(ordner)}: {daten.Eintraege.Count} Einträge, {daten.Verbindungen.Count} Verbindungen" + (nurBelegt ? " (nur belegte)" : ""));
    return;
}

if (app.Configuration.GetValue("Datenbank:AutoMigrate", true))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<KarteDb>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseResponseCompression();

var api = app.MapGroup("/api");

// Alles, was die Karte braucht, in einer Antwort; per ETag zwischengespeichert.
api.MapGet("/karte", async (HttpContext ctx, Kartendaten karte, CancellationToken ct) =>
{
    var p = await karte.HolenAsync(ct);
    ctx.Response.Headers.ETag = p.ETag;
    ctx.Response.Headers.CacheControl = "public, max-age=60, must-revalidate";
    if (ctx.Request.Headers.IfNoneMatch == p.ETag) return Results.StatusCode(StatusCodes.Status304NotModified);
    return Results.Bytes(p.Json, "application/json");
});

// Einzelner Eintrag mit vollem Text, Ort und Belegen (für spätere Detail- oder Quellenseiten).
api.MapGet("/eintraege/{id}", async (string id, KarteDb db, CancellationToken ct) =>
{
    var e = await db.Eintraege.AsNoTracking().Include(x => x.Ort).Include(x => x.Quellen).ThenInclude(q => q.Quelle)
        .FirstOrDefaultAsync(x => x.Id == id, ct);
    if (e is null) return Results.NotFound();
    var verbindungen = await db.Verbindungen.AsNoTracking().Where(v => v.VonId == id || v.NachId == id)
        .Include(v => v.Quellen).ThenInclude(q => q.Quelle)
        .Select(v => new
        {
            v.VonId, v.NachId, v.Text, v.Status, v.Art,
            Quellen = v.Quellen.Select(q => new { q.Quelle!.Id, q.Quelle.Titel, q.Quelle.Url })
        }).ToListAsync(ct);
    return Results.Ok(new
    {
        e.Id, e.Name, e.Typ, e.Umfeld, e.Form, e.Info, e.OrtGrund, e.BildUrl, e.BildQuelle,
        Ort = e.Ort is null ? null : new { e.Ort.Key, e.Ort.Name, e.Ort.Adresse, e.Ort.Genauigkeit, e.Ort.Quelle, e.Ort.QuelleUrl, e.Ort.Land },
        Quellen = e.Quellen.Select(q => new { q.Quelle!.Id, q.Quelle.Titel, q.Quelle.Url }),
        Verbindungen = verbindungen
    });
});

// Volltextsuche über Namen und Orte.
api.MapGet("/suche", async (string? q, KarteDb db, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2) return Results.Ok(Array.Empty<object>());
    var muster = "%" + q.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
    var treffer = await db.Eintraege.AsNoTracking()
        .Where(e => EF.Functions.ILike(e.Name, muster) || (e.Ort != null && EF.Functions.ILike(e.Ort.Name, muster)))
        .OrderBy(e => e.Typ == "ereignis").ThenBy(e => e.Name).Take(20)
        .Select(e => new { e.Id, e.Name, e.Typ, e.Umfeld, Ort = e.Ort != null ? e.Ort.Name : null })
        .ToListAsync(ct);
    return Results.Ok(treffer);
});

// Neu importieren ohne Neustart; nur mit gesetztem Schlüssel aktiv.
api.MapPost("/admin/import", async (HttpContext ctx, Importer importer, Kartendaten karte, IOptions<ImportOptionen> opt, CancellationToken ct) =>
{
    var schluessel = opt.Value.Schluessel;
    if (string.IsNullOrEmpty(schluessel)) return Results.NotFound();
    if (!ctx.Request.Headers.TryGetValue("X-Import-Schluessel", out var s) ||
        !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(s.ToString()), System.Text.Encoding.UTF8.GetBytes(schluessel)))
        return Results.Unauthorized();
    var bericht = await importer.AusfuehrenAsync(opt.Value.Datei, opt.Value.SeedVerzeichnis, ct);
    karte.Verwerfen();
    return Results.Ok(bericht);
});

app.MapHealthChecks("/health");

app.Run();

public partial class Program;
