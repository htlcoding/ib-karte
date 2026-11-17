# IB-Karte

Interaktive Karte zum Netzwerk der Identitären Bewegung. Grundlage ist die Recherche im privaten Repo
`htlcoding/recherche` (`IdentitäreBewegung/netzwerk.json`).

| Teil | Technik | Ordner |
|---|---|---|
| Frontend | Vue 3, TypeScript, Vite, MapLibre GL | `frontend/` |
| Backend | ASP.NET Core (.NET 10), EF Core | `backend/` |
| Datenbank | PostgreSQL 16 | über Docker Compose |
| Startdaten | Orte, Zuordnungen, Regeln (JSON) | `seed/` |

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

### Personen auf der Karte

Personen stehen **nie an einer Wohn- oder Meldeadresse**, sondern symbolisch am nächstliegenden Ort, der sich
aus einer belegten Funktion ergibt:

1. Mandat laut Datensatz: Parlament, Wiener Rathaus (Gemeinderat, Landtag, Stadtregierung), Bezirksvertretung,
   Bundestag, EU-Parlament, Thüringer Landtag.
2. Belegte Rolle bei einer Organisation mit Ort (z. B. Obmann eines Vereins).
3. Sonst der Eintrag, mit dem die meisten Verbindungen bestehen.
4. Landet eine Person so nur auf Landesebene, nennt ihre Funktion aber eine Stadt („FJ-Chef Wien“), steht sie in
   dieser Stadt.

Der Grund steht in der Datenbank (`eintraege.ort_grund`) und über `GET /api/eintraege/{id}`.

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
| GET | `/api/karte` | Alles für die Karte (Einträge, Orte, Verbindungen, Farben, Arten) |
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
