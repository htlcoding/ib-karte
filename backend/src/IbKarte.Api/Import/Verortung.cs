namespace IbKarte.Api.Import;

/// <summary>
/// Bestimmt für jeden Eintrag einen möglichst bestimmten, sinnvollen Ort.
/// <list type="number">
/// <item>Organisationen, Medien, Ereignisse: Ort aus den Startdaten (offizieller Sitz, Veranstaltungsort).</item>
/// <item>Liegt der nur auf Landes- oder Regionsebene (oder fehlt), kommt der Eintrag zum Ort eines verbundenen
///       Eintrags – aber nur, wenn dieser Ort selbst belegt ist (keine Ketten), im selben Land bzw. Bundesland liegt
///       und die Verbindung tragfähig ist (Organisation, Funktion, Geld vor Veranstaltung, Medien, Zusammenarbeit;
///       „Indirekt / Umfeld“ zählt nie).</item>
/// <item>Personen: symbolisch nach <see cref="PersonenPosition"/> mit Vorrang für ihr Heimatland, danach dieselbe
///       Verbindungsregel. Nie eine Wohnadresse.</item>
/// </list>
/// </summary>
public static class Verortung
{
    public record Ergebnis(Dictionary<string, string> Ort, Dictionary<string, string> Grund);
    public record Kante(string Von, string Nach, string Text, string Art);

    static readonly string[] Flaeche = ["land", "region", "aussen"];
    static readonly HashSet<string> AT = ["Wien", "Niederösterreich", "Oberösterreich", "Salzburg", "Tirol", "Vorarlberg", "Steiermark", "Kärnten", "Burgenland", "Österreich"];
    static readonly HashSet<string> DE = ["Baden-Württemberg", "Bayern", "Berlin", "Brandenburg", "Bremen", "Hamburg", "Hessen", "Mecklenburg-Vorpommern",
        "Niedersachsen", "Nordrhein-Westfalen", "Rheinland-Pfalz", "Saarland", "Sachsen", "Sachsen-Anhalt", "Schleswig-Holstein", "Thüringen", "Deutschland"];

    /// <summary>Staat zu einem Land/Bundesland-Eintrag der Ortsdaten.</summary>
    public static string? Staat(string? land) => land is null ? null : AT.Contains(land) ? "Österreich" : DE.Contains(land) ? "Deutschland" : land;

    /// <summary>Heimatland einer Person aus ihrer Gruppe in der Recherche.</summary>
    public static string? Heimatland(string gruppe) => gruppe switch
    {
        "de" => "Deutschland",
        "fpoe" or "kern" or "zentrum" => "Österreich",
        _ => null,
    };

    static int Genauigkeit(string g) => g switch
    {
        "adresse" or "gebaeude" or "strasse" or "ortszentrum" => 3,
        "bezirk" => 2,
        "gemeinde" => 1,
        _ => 0,
    };

    /// <summary>Wie tragfähig eine Verbindung als Ortsanker ist (0 = gar nicht).</summary>
    static double Tragfaehig(string art, string typ) => (art, typ) switch
    {
        ("struktur", "person") or ("funktion", "person") or ("geld", "person") => 2,
        ("struktur", _) => 3,                       // Teil von, Nachfolger, Träger, Adresse
        ("geld", _) => 2,
        ("funktion", _) or ("veranstaltung", _) or ("medien", _) or ("kontakt", _) => 1,
        ("indirekt", "ereignis") => 0.5,            // ein Ereignis darf zu seinem Gegenstand
        _ => 0,
    };

    public static Ergebnis Berechne(
        IReadOnlyDictionary<string, PersonenPosition.Knoten> knoten,
        IReadOnlyList<Kante> belegt,
        IReadOnlyDictionary<string, string> zuordnung,
        IReadOnlyDictionary<string, (string Genauigkeit, string? Land)> orte,
        PersonenPosition position,
        IReadOnlyDictionary<string, string>? hauptorte = null)
    {
        var adj = knoten.Keys.ToDictionary(k => k, _ => new List<(string Partner, string Text, string Art)>());
        foreach (var e in belegt)
        {
            adj[e.Von].Add((e.Nach, e.Text, e.Art));
            adj[e.Nach].Add((e.Von, e.Text, e.Art));
        }
        var grad = adj.ToDictionary(x => x.Key, x => x.Value.Count);
        var genauigkeit = orte.ToDictionary(x => x.Key, x => x.Value.Genauigkeit);
        bool IstFlaeche(string ort) => Flaeche.Contains(orte[ort].Genauigkeit);
        string? StaatVon(string ort) => Staat(orte[ort].Land);

        var ort = new Dictionary<string, string>();
        var grund = new Dictionary<string, string>();
        foreach (var k in knoten.Values.Where(k => k.Typ != "person"))
            if (zuordnung.TryGetValue(k.Id, out var o) && orte.ContainsKey(o)) ort[k.Id] = o;

        // Anker: nur selbst belegte, genaue Orte (keine Weitergabe über mehrere Ecken).
        var anker = ort.Where(x => !IstFlaeche(x.Value)).ToDictionary(x => x.Key, x => x.Value);

        bool Anbinden(string id, bool auchPersonen, string? staat, string? bundesland, bool nurStruktur = false)
        {
            (double, int, int, int, string, string)? beste = null;
            foreach (var (partner, text, art) in adj[id])
            {
                var t = Tragfaehig(art, knoten[id].Typ);
                if (t == 0 || nurStruktur && art is not ("struktur" or "geld")) continue;
                var p = knoten[partner];
                if (p.Typ == "person" && !auchPersonen) continue;
                if (!anker.TryGetValue(partner, out var po)) continue;
                if (bundesland is not null && orte[po].Land != bundesland) continue;
                if (staat is not null && StaatVon(po) != staat) continue;
                var gewicht = p.Typ is "org" or "medium" ? 2 : p.Typ == "ereignis" ? 1 : 0;
                var w = (t, Genauigkeit(genauigkeit[po]), gewicht, grad[partner], partner, text);
                if (beste is null || (w.Item1, w.Item2, w.Item3, w.Item4).CompareTo((beste.Value.Item1, beste.Value.Item2, beste.Value.Item3, beste.Value.Item4)) > 0) beste = w;
            }
            if (beste is not { } b) return false;
            ort[id] = anker[b.Item5];
            grund[id] = (knoten[id].Typ == "person" ? "keine Funktion mit genauerem Ort belegt" : "kein eigener Sitz belegt")
                        + $" – symbolisch beim verbundenen Eintrag „{knoten[b.Item5].Name}“ ({b.Item6})";
            return true;
        }

        // 1. Organisationen, Medien, Ereignisse ohne genauen Ort: im selben Bundesland bzw. Land
        foreach (var k in knoten.Values.Where(k => k.Typ != "person"))
        {
            if (anker.ContainsKey(k.Id)) continue;
            if (ort.TryGetValue(k.Id, out var o))
            {
                var bundesland = orte[o].Genauigkeit == "region" && (AT.Contains(orte[o].Land ?? "") || DE.Contains(orte[o].Land ?? "")) ? orte[o].Land : null;
                Anbinden(k.Id, false, StaatVon(o), bundesland);
            }
            else Anbinden(k.Id, false, null, null);
        }

        // 1b. Zweite Runde nur über organisatorische Verbindungen (z. B. Fonds → Verein): neu verortete Einträge werden Anker.
        foreach (var x in ort.Where(x => !IstFlaeche(x.Value) && !anker.ContainsKey(x.Key)).ToList()) anker[x.Key] = x.Value;
        foreach (var k in knoten.Values.Where(k => k.Typ != "person" && (!ort.TryGetValue(k.Id, out var o) || IstFlaeche(o))))
        {
            var o = ort.TryGetValue(k.Id, out var oo) ? oo : null;
            Anbinden(k.Id, false, o is null ? null : StaatVon(o), null, nurStruktur: true);
        }

        // 2. Personen: Funktion/Rolle, sonst Ersatz mit Vorrang fürs Heimatland
        var ortVon = new Dictionary<string, string>(ort);
        var kantenAlt = adj.ToDictionary(x => x.Key, x => (IReadOnlyList<PersonenPosition.Kante>)x.Value.Select(v => new PersonenPosition.Kante(v.Partner, v.Text)).ToList());
        foreach (var p in knoten.Values.Where(k => k.Typ == "person"))
        {
            var heimat = Heimatland(p.Gruppe);
            var r = position.Bestimme(p, kantenAlt[p.Id], knoten, ortVon, genauigkeit, grad,
                heimat is null ? null : o => StaatVon(o) == heimat);
            if (r is null) continue;
            ort[p.Id] = r.Value.Ort;
            grund[p.Id] = r.Value.Grund;
            // Liegt der Ort im Ausland, nennt der Infotext aber eine Stadt im Heimatland, gewinnt diese.
            if (heimat is not null && StaatVon(r.Value.Ort) != heimat && position.StadtIn(p.Info, o => StaatVon(o) == heimat) is { } stadt)
            {
                ort[p.Id] = stadt.Ort;
                grund[p.Id] = $"keine Funktion mit Ort im Heimatland belegt – {stadt.Name} laut Infotext";
            }
            if (!IstFlaeche(r.Value.Ort) && !r.Value.Grund.StartsWith("keine Funktion")) anker[p.Id] = r.Value.Ort;
        }
        foreach (var p in knoten.Values.Where(k => k.Typ == "person"))
        {
            if (ort.TryGetValue(p.Id, out var o) && !IstFlaeche(o)) continue;
            var staat = ort.TryGetValue(p.Id, out var o2) ? StaatVon(o2) : Heimatland(p.Gruppe);
            if (!Anbinden(p.Id, true, staat, null)) Anbinden(p.Id, true, null, null);
        }
        // 3. Letzte Stufe: Stadt aus dem eigenen Infotext (im selben Land), sonst Hauptstadt der Region (symbolisch).
        foreach (var k in knoten.Values)
        {
            if (!ort.TryGetValue(k.Id, out var o) || !IstFlaeche(o)) continue;
            var staat = StaatVon(o);
            var bundesland = orte[o].Genauigkeit == "region" && (AT.Contains(orte[o].Land ?? "") || DE.Contains(orte[o].Land ?? "")) ? orte[o].Land : null;
            if (staat is not null && position.StadtIn(k.Info, x => StaatVon(x) == staat && (bundesland is null || orte[x].Land == bundesland)) is { } stadt)
            {
                ort[k.Id] = stadt.Ort;
                grund[k.Id] = $"kein genauerer Ort belegt – {stadt.Name} laut Infotext";
            }
            else if (hauptorte is not null && hauptorte.TryGetValue(o, out var h) && orte.ContainsKey(h))
            {
                ort[k.Id] = h;
                grund[k.Id] = $"kein genauerer Ort belegt – symbolisch in der Hauptstadt ({orte[o].Land ?? o})";
            }
        }
        return new Ergebnis(ort, grund);
    }
}
