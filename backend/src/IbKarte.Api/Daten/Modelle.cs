namespace IbKarte.Api.Daten;

/// <summary>Ein Ort auf der Karte: Adresse, Gebäude oder nur Gemeinde/Region.</summary>
public class Ort
{
    public required string Key { get; set; }
    public required string Name { get; set; }
    public string? Adresse { get; set; }
    public double? Lat { get; set; }
    public double? Lon { get; set; }
    /// <summary>adresse, gebaeude, strasse, ortszentrum, bezirk, gemeinde, region, land, aussen</summary>
    public required string Genauigkeit { get; set; }
    public string? Quelle { get; set; }
    public string? QuelleUrl { get; set; }
    public string? Land { get; set; }
    /// <summary>Symbol für wichtige Orte (parlament, rathaus, keller, parteizentrale, gericht, haus).</summary>
    public string? Symbol { get; set; }
}

/// <summary>Person, Organisation, Medium oder Ereignis.</summary>
public class Eintrag
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    /// <summary>person, org, medium, ereignis</summary>
    public required string Typ { get; set; }
    /// <summary>Gruppe laut Recherche (ib, fpoe, de, medien, intl, ereignis).</summary>
    public required string Gruppe { get; set; }
    /// <summary>Farbgruppe auf der Karte (ib, fpoe, medien, firmen, de, intl, ereignis).</summary>
    public required string Umfeld { get; set; }
    public required string Form { get; set; }
    public required string Info { get; set; }
    public string? Ordner { get; set; }
    public string? OrtKey { get; set; }
    public Ort? Ort { get; set; }
    /// <summary>Bei Personen: warum sie an diesem (symbolischen) Ort stehen.</summary>
    public string? OrtGrund { get; set; }
    public string? BildUrl { get; set; }
    public string? BildQuelle { get; set; }
    /// <summary>Gesichtsausschnitt: Mittelpunkt in Prozent der Bildbreite/-höhe und Vergrößerung (1 = ganzes Bild).</summary>
    public double? BildX { get; set; }
    public double? BildY { get; set; }
    public double? BildZoom { get; set; }
    public List<EintragQuelle> Quellen { get; set; } = [];
}

public class Verbindung
{
    public int Id { get; set; }
    public required string VonId { get; set; }
    public Eintrag? Von { get; set; }
    public required string NachId { get; set; }
    public Eintrag? Nach { get; set; }
    public required string Text { get; set; }
    /// <summary>belegt oder vorwurf</summary>
    public required string Status { get; set; }
    /// <summary>Art der Verbindung (funktion, struktur, geld, medien, veranstaltung, kontakt, indirekt).</summary>
    public required string Art { get; set; }
    public List<VerbindungQuelle> Quellen { get; set; } = [];
}

public class Quelle
{
    public required string Id { get; set; }
    public required string Titel { get; set; }
    public string? Url { get; set; }
}

public class EintragQuelle
{
    public required string EintragId { get; set; }
    public required string QuelleId { get; set; }
    public Quelle? Quelle { get; set; }
}

public class VerbindungQuelle
{
    public int VerbindungId { get; set; }
    public required string QuelleId { get; set; }
    public Quelle? Quelle { get; set; }
}

public class OffeneFrage
{
    public int Id { get; set; }
    public required string Text { get; set; }
}

/// <summary>Genau eine Zeile: Stand der Recherche und Zeitpunkt des letzten Imports.</summary>
public class Datenstand
{
    public int Id { get; set; } = 1;
    public required string Stand { get; set; }
    public DateTimeOffset ImportiertAm { get; set; }
    public required string Pruefsumme { get; set; }
}
