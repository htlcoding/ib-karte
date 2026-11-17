# IB-Karte

Interaktive Karte zum Netzwerk der Identitären Bewegung. Grundlage ist die Recherche im privaten Repo
`htlcoding/recherche` (`IdentitäreBewegung/netzwerk.json`).

| Teil | Technik | Ordner |
|---|---|---|
| Frontend | Vue 3, TypeScript, Vite, MapLibre GL | `frontend/` |
| Backend | ASP.NET Core (.NET 10), EF Core | `backend/` |
| Datenbank | PostgreSQL 16 | über Docker Compose |
| Startdaten | Orte, Zuordnungen, Regeln (JSON) | `seed/` |

## Schnellstart unter Windows (ohne Docker)

1. `scripts/windows/einrichten.ps1` herunterladen.
2. Rechtsklick → **Mit PowerShell ausführen**. Windows-Abfragen (Installation, Administratorrechte) bestätigen;
   beim ersten Mal öffnet sich ein GitHub-Login im Browser.
3. Das Skript installiert fehlende Programme (Git, .NET 10, Node.js, PostgreSQL 16), holt beide Repos nach
   `%USERPROFILE%\IB-Karte`, legt die Datenbank an, importiert die Daten und startet die Karte.
4. Später reicht ein Doppelklick auf **IB-Karte starten** auf dem Desktop (startet `starten.bat`).
   Neue Recherchedaten: das Einrichtungsskript erneut ausführen.

## Schnellstart (lokal mit Docker)

```bash
cp .env.example .env                      # Passwort setzen
cp ../recherche/IdentitäreBewegung/netzwerk.json daten/
docker compose up -d --build
docker compose run --rm api import        # Daten in die Datenbank laden
```

Die Karte läuft dann unter <http://localhost:8080>. Ein neuer Import ist jederzeit möglich; die laufende
API bemerkt ihn innerhalb von 15 Sekunden.

## Entwicklung ohne Docker

Voraussetzungen: .NET SDK 10, Node 22, eine lokale PostgreSQL-Datenbank `ibkarte` (Benutzer/Passwort `ibkarte`).

```bash
# Backend (http://localhost:5207)
cd backend/src/IbKarte.Api
dotnet run -- import          # Migration + Import aus ../../../daten/netzwerk.json
dotnet run

# Frontend (http://localhost:5173, leitet /api an das Backend weiter)
cd frontend
npm install
npm run dev

# Tests
cd backend && dotnet test
```

## Wie die Daten fließen

1. `netzwerk.json` enthält Einträge, Verbindungen und Quellen.
2. Der Import (`IbKarte.Api import`) ergänzt sie mit den Startdaten aus `seed/` und ersetzt den gesamten
   Datenbestand in einer Transaktion:
   - **Orte** (`seed/orte.json`): Adressen und Gebiete mit Genauigkeit und Ortsquelle; wichtige Orte tragen ein
     `symbol` (parlament, rathaus, keller, parteizentrale, gericht, haus).
   - **Zuordnung** (`seed/zuordnung.json`): welche Organisation, welches Medium, welches Ereignis an welchem Ort liegt.
   - **Verbindungsarten** (`seed/verbindungsarten.json`): Stichwortregeln, die jede Verbindung einer Art zuordnen
     (Funktion, Organisation, Geld, Medien, Veranstaltung, Zusammenarbeit, Indirekt). Die erste passende Regel gewinnt.
   - **Regeln** (`seed/regeln.json`): Farbgruppen, Organisationsform und die symbolische Position von Personen.
3. Die API baut daraus einen fertigen Kartendatenstand (`GET /api/karte`), hält ihn im Speicher und liefert ihn
   komprimiert mit ETag aus.

### Wo ein Eintrag auf der Karte steht

Jeder Eintrag bekommt einen bestimmten Ort, nie nur „Österreich“ oder „Deutschland“:

1. **Organisationen, Medien, Ereignisse:** offizieller Sitz bzw. Veranstaltungsort aus `seed/orte.json` und
   `seed/zuordnung.json` (mit Quelle, z. B. Impressum, Satzung, Register).
2. **Kein Sitz belegt:** Ort eines verbundenen Eintrags – nur wenn dieser Ort selbst belegt ist, im selben Land
   bzw. Bundesland liegt und die Verbindung tragfähig ist (Organisation/Adresse vor Geld vor Funktion,
   Veranstaltung, Medien, Zusammenarbeit; „Indirekt / Umfeld“ zählt nur bei Ereignissen).
3. **Letzte Stufe:** eine Stadt aus dem eigenen Infotext (im selben Land/Bundesland), sonst symbolisch die
   Hauptstadt der Region (`hauptorte` in `seed/regeln.json`).

Der Grund steht in `eintraege.ort_grund` und im Info-Kästchen („Standort auf der Karte: …“).

**Personen** stehen **nie an einer Wohn- oder Meldeadresse**, sondern symbolisch:

1. Mandat laut Datensatz: Parlament, Wiener Rathaus, Bezirksvertretung, Bundestag, Bayerischer/Thüringer Landtag,
   EU-Parlament.
2. Belegte Rolle bei einer Organisation mit Ort.
3. Sonst der Eintrag mit den meisten Verbindungen – mit Vorrang für das Heimatland der Person
   („Neue Rechte Deutschland“ → Deutschland, FPÖ/IBÖ → Österreich); nennt der Infotext eine Stadt im Heimatland,
   gewinnt diese.

### Bilder und Logos

Die Karte zeigt Fotos (Personen) und Logos (Medien, Organisationen), wenn ein Eintrag in `netzwerk.json` die Felder
`bild` (Bildadresse) und `bild_quelle` (Urheber und Lizenz) hat. Bei Personen legt `bild_ausschnitt` fest, welcher
Teil des Fotos gezeigt wird – Mittelpunkt des Gesichts in Prozent der Bildbreite und -höhe sowie die Vergrößerung:

```json
{ "id": "beispiel", "bild": "https://…/foto.jpg", "bild_quelle": "Name, CC BY 4.0",
  "bild_ausschnitt": { "x": 52, "y": 30, "zoom": 2.5 } }
```

Ohne Ausschnitt wird die obere Bildmitte genommen. Nur Bilder mit geklärten Rechten eintragen; der
Server, der das Bild ausliefert, muss CORS erlauben. Ohne Bild zeigt die Karte ein neutrales Profilbild bzw. ein
Symbol.

## API

| Methode | Pfad | Zweck |
|---|---|---|
| GET | `/api/karte` | Alles für die Karte (Einträge, Orte mit Adresse und Symbol, Verbindungen, Farben, Arten) |
| GET | `/api/eintraege/{id}` | Ein Eintrag mit vollem Text, Ort und Belegen |
| GET | `/api/suche?q=` | Suche über Namen und Orte |
| POST | `/api/admin/import` | Neu importieren; nur aktiv, wenn `IMPORT_SCHLUESSEL` gesetzt ist (Header `X-Import-Schluessel`) |
| GET | `/health` | Zustand von API und Datenbank |

## Datenbank

Tabellen: `orte`, `eintraege`, `verbindungen`, `quellen`, `eintrag_quellen`, `verbindung_quellen`,
`offene_fragen`, `datenstand`. Das Schema wird über EF-Core-Migrationen
(`backend/src/IbKarte.Api/Daten/Migrationen`) beim Start automatisch angelegt.

Neue Migration nach einer Modelländerung:

```bash
cd backend/src/IbKarte.Api
dotnet tool install --global dotnet-ef
dotnet ef migrations add <Name> -o Daten/Migrationen
```

## Grundkarte

Kartenkacheln kommen von [OpenFreeMap](https://openfreemap.org) (frei, ohne Schlüssel, Daten © OpenStreetMap).
Länder ohne Einträge werden abgedeckt; die Ländergrenzen dafür stammen aus Natural Earth
(`frontend/public/laender.geojson`). Die Karte rechnet im Browser der Besucher (WebGL); der Server braucht keine
Grafikkarte.
