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
        var belegt = netz.Edges.Where(e => e.Status == "belegt" && knoten.ContainsKey(e.Von) && knoten.ContainsKey(e.Nach))
            .Select(e => new Verortung.Kante(e.Von, e.Nach, e.Label, klass.Art(e.Label))).ToList();
        var gen = seed.Orte.ToDictionary(o => o.Key, o => o.Genauigkeit);
        var v = Verortung.Berechne(knoten, belegt, seed.Zuordnung, seed.Orte.ToDictionary(o => o.Key, o => (o.Genauigkeit, o.Land)), new PersonenPosition(seed.Regeln), seed.Regeln.Hauptorte);

        // Nächster zuordenbarer Ort: Bezirksmandat schlägt Landesebene.
        if (knoten.ContainsKey("greiner")) Assert.Equal("amtshaus-brigittenau", v.Ort["greiner"]);

        // Sinnvolle Orte: kein deutscher Eintrag in Österreich und umgekehrt, wenn das Heimatland bekannt ist.
        var orteSeed = seed.Orte.ToDictionary(o => o.Key);
        var offen = new List<string>();
        string[] flaecheAlt = ["land", "region", "aussen"];
        foreach (var k in knoten.Values)
        {
            if (!v.Ort.TryGetValue(k.Id, out var o)) continue;
            var start = k.Typ == "person" ? Verortung.Heimatland(k.Gruppe) : seed.Zuordnung.TryGetValue(k.Id, out var z) ? Verortung.Staat(orteSeed[z].Land) : null;
            var jetzt = Verortung.Staat(orteSeed[o].Land);
            if (start is "Deutschland" && jetzt is "Österreich" || start is "Österreich" && jetzt is "Deutschland")
            {
                // nur ein Fehler, wenn es eine belegte Verbindung mit Ort im Heimatland gäbe
                var imHeimatland = belegt.Where(e => e.Von == k.Id || e.Nach == k.Id).Select(e => e.Von == k.Id ? e.Nach : e.Von)
                    .Any(p => v.Ort.TryGetValue(p, out var po) && Verortung.Staat(orteSeed[po].Land) == start && !flaecheAlt.Contains(gen[po]));
                if (imHeimatland && v.Grund.TryGetValue(k.Id, out var g) && g.StartsWith("kein")) offen.Add($"{k.Id}: {start} → {o}");
            }
        }
        Assert.True(offen.Count == 0, "Ort im falschen Land: " + string.Join(", ", offen));

        // Kein Eintrag bleibt auf Landes- oder Regionsebene (Ausnahme: Ereignisse auf See).
        string[] flaeche = ["land", "region", "aussen"];
        var flaechig = v.Ort.Where(x => flaeche.Contains(gen[x.Value]) && orteSeed[x.Value].Land?.StartsWith("Mittelmeer") != true).Select(x => x.Key).ToList();
        Assert.True(flaechig.Count == 0, "Noch ohne bestimmten Ort: " + string.Join(", ", flaechig));
        var ohne = knoten.Keys.Where(id => !v.Ort.ContainsKey(id)).ToList();
        Assert.True(ohne.Count == 0, "Ohne Ort (unsichtbar auf der Karte): " + string.Join(", ", ohne));
        Assert.Equal("afd-bundesgeschaeftsstelle", v.Ort["afd"]);
        Assert.Equal("rfj-bundesorganisation", v.Ort["fj"]);
        Assert.NotEqual("afd-bundesgeschaeftsstelle", v.Ort["ibd"]);
    }
}
