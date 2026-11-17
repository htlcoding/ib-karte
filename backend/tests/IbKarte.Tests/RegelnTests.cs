using System.Text.Json;
using IbKarte.Api.Import;

namespace IbKarte.Tests;

public class RegelnTests
{
    static readonly string Wurzel = FindeWurzel();
    static string FindeWurzel()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !Directory.Exists(Path.Combine(d.FullName, "seed"))) d = d.Parent;
        return d?.FullName ?? throw new DirectoryNotFoundException("seed-Verzeichnis nicht gefunden");
    }
    static Startdaten Seed => Startdaten.Laden(Path.Combine(Wurzel, "seed"));

    [Theory]
    [InlineData("Klubobmann (2019)", "funktion")]
    [InlineData("erstes Interview als NR-Präsident (für AUF1, laut Standard)", "medien")]
    [InlineData("Gesellschafter", "geld")]
    [InlineData("Teilnehmer 2026", "veranstaltung")]
    [InlineData("Regionalgruppe", "struktur")]
    [InlineData("Teil des deutschnationalen Korporationsmilieus", "indirekt")]
    [InlineData("Kooperation", "kontakt")]
    [InlineData("etwas ganz anderes", "kontakt")]
    public void Verbindungsart(string text, string erwartet) =>
        Assert.Equal(erwartet, new VerbindungsKlassifikator(Seed.Arten).Art(text));

    [Theory]
    [InlineData("2019 FPÖ-Klubobmann; rechtfertigte laut ORF die Vermietung. Zweiter Satz.", "2019 FPÖ-Klubobmann; rechtfertigte laut ORF die Vermietung.")]
    [InlineData("Ein Eintrag mit genug Text vor dem Strichpunkt am Ende; und dann mehr", "Ein Eintrag mit genug Text vor dem Strichpunkt am Ende.")]
    [InlineData("Kurz.", "Kurz.")]
    public void ErsterSatz(string ein, string aus) => Assert.Equal(aus, Text.ErsterSatz(ein));

    [Fact]
    public void LangerTextWirdGekuerzt()
    {
        var s = Text.ErsterSatz(string.Join(' ', Enumerable.Repeat("Wort", 100)));
        Assert.True(s.Length <= 242);
        Assert.EndsWith(" …", s);
    }

    [Fact]
    public void FarbgruppeUndForm()
    {
        var r = new EintragsRegeln(Seed.Regeln);
        Assert.Equal("zentrum", r.Gruppe("ibo", "org", "Identitäre Bewegung Österreich (IBÖ)", "ib"));
        Assert.Equal("ib", EintragsRegeln.Umfeld("zentrum"));
        Assert.Equal("firmen", r.Gruppe("x", "org", "Info-DIREKT Verlags GmbH", "medien_nicht"));
        Assert.Equal("fpoe", r.Gruppe("fpo", "org", "FPÖ", "fpoe"));
        Assert.Equal("Partei / Parteijugend", r.Form("org", "FPÖ"));
        Assert.Equal("Person", r.Form("person", "Egal"));
    }

    // Abgleich mit der bisherigen Python-Auswertung, sofern netzwerk.json vorliegt.
    [Fact]
    public void GleichesErgebnisWieBisherigeKarte()
    {
        var datei = Environment.GetEnvironmentVariable("NETZWERK_JSON") ?? Path.Combine(Wurzel, "daten", "netzwerk.json");
        if (!File.Exists(datei)) return;
        var netz = Importer.LiesNetzwerk(datei);
        var seed = Seed;
        var refJ = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Testdaten", "referenz.json"))).RootElement;

        var klass = new VerbindungsKlassifikator(seed.Arten);
        foreach (var p in refJ.GetProperty("arten").EnumerateObject())
            Assert.True(p.Value.GetString() == klass.Art(p.Name), $"Art für „{p.Name}“: erwartet {p.Value}, war {klass.Art(p.Name)}");

        var regeln = new EintragsRegeln(seed.Regeln);
        var knoten = netz.Nodes.ToDictionary(n => n.Id, n => new PersonenPosition.Knoten(n.Id, n.Name, n.Typ, regeln.Gruppe(n.Id, n.Typ, n.Name, n.Gruppe), n.Info));
        var adj = knoten.Keys.ToDictionary(k => k, _ => new List<PersonenPosition.Kante>());
        foreach (var e in netz.Edges.Where(e => e.Status == "belegt" && knoten.ContainsKey(e.Von) && knoten.ContainsKey(e.Nach)))
        { adj[e.Von].Add(new(e.Nach, e.Label)); adj[e.Nach].Add(new(e.Von, e.Label)); }
        var grad = adj.ToDictionary(x => x.Key, x => x.Value.Count);
        var ortVon = seed.Zuordnung.Where(z => knoten.ContainsKey(z.Key)).ToDictionary(z => z.Key, z => z.Value);
        var gen = seed.Orte.ToDictionary(o => o.Key, o => o.Genauigkeit);
        var pos = new PersonenPosition(seed.Regeln);
        var abw = new List<string>();
        foreach (var p in refJ.GetProperty("personen").EnumerateObject())
        {
            var r = pos.Bestimme(knoten[p.Name], adj[p.Name], knoten, ortVon, gen, grad);
            if (r?.Ort != p.Value.GetString()) abw.Add($"{p.Name}: erwartet {p.Value}, war {r?.Ort}");
        }
        Assert.True(abw.Count == 0, string.Join("\n", abw));

        // Nächster zuordenbarer Ort: Bezirksmandat schlägt Landesebene.
        if (knoten.ContainsKey("greiner"))
            Assert.Equal("amtshaus-brigittenau", pos.Bestimme(knoten["greiner"], adj["greiner"], knoten, ortVon, gen, grad)?.Ort);
    }
}
