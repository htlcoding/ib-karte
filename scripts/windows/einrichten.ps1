# IB-Karte einrichten (Windows, ohne Docker)
# Start: Rechtsklick auf diese Datei -> "Mit PowerShell ausführen".
# Installiert fehlende Programme (Git, .NET 10, Node.js, PostgreSQL), holt die Repos,
# legt die Datenbank an, importiert die Daten und erstellt eine Desktop-Verknüpfung "IB-Karte starten".
# Mehrfach ausführbar: Bereits Vorhandenes wird übersprungen bzw. aktualisiert.

$ErrorActionPreference = 'Stop'
$Ziel = Join-Path $HOME 'IB-Karte'
$Karte = Join-Path $Ziel 'ib-karte'
$Recherche = Join-Path $Ziel 'recherche'
$Notizen = Join-Path $Ziel 'zugangsdaten.txt'

function Schritt($t) { Write-Host ''; Write-Host "==> $t" -ForegroundColor Cyan }
function Ok($t) { Write-Host "    $t" -ForegroundColor Green }
function Pruefe($was) { if ($LASTEXITCODE -ne 0) { throw "$was ist fehlgeschlagen (Code $LASTEXITCODE)." } }
function PfadNeu {
  $env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')
}
function Hat($befehl) { [bool](Get-Command $befehl -ErrorAction SilentlyContinue) }
function Installiere($id, $name, $extra = @()) {
  Write-Host "    $name wird installiert (Bestätigung der Windows-Abfrage nötig) ..."
  winget install --id $id -e --silent --accept-package-agreements --accept-source-agreements @extra
  if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne -1978335189) { throw "$name konnte nicht installiert werden (Code $LASTEXITCODE)." }
  PfadNeu
}

try {
  Write-Host 'IB-Karte – Einrichtung' -ForegroundColor White
  New-Item -ItemType Directory -Force -Path $Ziel | Out-Null

  # ---------- 1. Programme ----------
  Schritt 'Programme prüfen'
  if (-not (Hat 'winget')) { throw 'winget fehlt. Bitte "App-Installer" aus dem Microsoft Store installieren und das Skript erneut starten.' }
  PfadNeu

  if (Hat 'git') { Ok 'Git vorhanden' } else { Installiere 'Git.Git' 'Git' }

  $sdk = if (Hat 'dotnet') { (dotnet --list-sdks) -match '^10\.' } else { $null }
  if ($sdk) { Ok '.NET SDK 10 vorhanden' } else { Installiere 'Microsoft.DotNet.SDK.10' '.NET SDK 10' }

  $nodeOk = $false
  if (Hat 'node') { $nodeOk = [int]((node --version).TrimStart('v').Split('.')[0]) -ge 20 }
  if ($nodeOk) { Ok 'Node.js vorhanden' } else { Installiere 'OpenJS.NodeJS.LTS' 'Node.js' }

  $psql = Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
  $pgPasswort = $null
  if ($psql) {
    Ok "PostgreSQL vorhanden ($($psql.Directory.Parent.Name))"
  } else {
    $pgPasswort = -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 20 | ForEach-Object { [char]$_ })
    Installiere 'PostgreSQL.PostgreSQL.16' 'PostgreSQL 16' @('--override', "--mode unattended --unattendedmodeui minimal --superpassword $pgPasswort --serverport 5432")
    "PostgreSQL-Administrator (Benutzer postgres): $pgPasswort" | Set-Content -Encoding UTF8 $Notizen
    Ok "Passwort des PostgreSQL-Administrators gespeichert in $Notizen"
    $psql = Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' | Sort-Object FullName -Descending | Select-Object -First 1
  }
  foreach ($b in 'git', 'dotnet', 'node', 'npm') { if (-not (Hat $b)) { throw "$b ist nach der Installation nicht auffindbar. Bitte PowerShell schließen und das Skript erneut starten." } }

  # ---------- 2. Repos ----------
  Schritt 'Repos holen (beim ersten Mal öffnet sich ein GitHub-Login im Browser)'
  foreach ($r in @(@{ Name = 'ib-karte'; Pfad = $Karte }, @{ Name = 'recherche'; Pfad = $Recherche })) {
    if (Test-Path (Join-Path $r.Pfad '.git')) {
      git -C $r.Pfad pull --ff-only; Pruefe "git pull ($($r.Name))"; Ok "$($r.Name) aktualisiert"
    } else {
      git clone "https://github.com/htlcoding/$($r.Name)" $r.Pfad; Pruefe "git clone ($($r.Name))"; Ok "$($r.Name) geholt"
    }
  }
  $quelle = Get-ChildItem -Path $Recherche -Recurse -Filter 'netzwerk.json' | Where-Object { $_.Directory.Name -like 'Identit*reBewegung' } | Select-Object -First 1
  if (-not $quelle) { throw 'netzwerk.json wurde im Recherche-Repo nicht gefunden.' }
  New-Item -ItemType Directory -Force -Path (Join-Path $Karte 'daten') | Out-Null
  Copy-Item $quelle.FullName (Join-Path $Karte 'daten\netzwerk.json') -Force
  Ok 'netzwerk.json kopiert'

  # ---------- 3. Datenbank ----------
  Schritt 'Datenbank anlegen'
  # Schon eingerichtet? Dann ist kein Administrator-Passwort nötig.
  $env:PGPASSWORD = 'ibkarte'
  $ErrorActionPreference = 'Continue'
  $schonDa = & $psql.FullName -h localhost -U ibkarte -d ibkarte -tAc 'SELECT 1' 2>$null
  $ErrorActionPreference = 'Stop'
  Remove-Item Env:PGPASSWORD
  if ($LASTEXITCODE -eq 0 -and "$schonDa".Trim() -eq '1') {
    Ok 'Datenbank ibkarte bereits vorhanden'
  } else {
  if (-not $pgPasswort -and (Test-Path $Notizen)) {
    $zeile = Get-Content $Notizen | Where-Object { $_ -match 'postgres\): (.+)$' } | Select-Object -First 1
    if ($zeile -match 'postgres\): (.+)$') { $pgPasswort = $Matches[1].Trim() }
  }
  if (-not $pgPasswort) {
    $sicher = Read-Host 'Passwort des PostgreSQL-Benutzers "postgres" (bei der PostgreSQL-Installation festgelegt)' -AsSecureString
    $pgPasswort = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($sicher))
  }
  $env:PGPASSWORD = $pgPasswort
  $rolle = & $psql.FullName -h localhost -U postgres -d postgres -tAc "SELECT 1 FROM pg_roles WHERE rolname='ibkarte'"; Pruefe 'Anmeldung bei PostgreSQL'
  if ($rolle -ne '1') { & $psql.FullName -h localhost -U postgres -d postgres -c "CREATE ROLE ibkarte LOGIN PASSWORD 'ibkarte'"; Pruefe 'Benutzer anlegen' }
  $db = & $psql.FullName -h localhost -U postgres -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='ibkarte'"
  if ($db -ne '1') { & $psql.FullName -h localhost -U postgres -d postgres -c "CREATE DATABASE ibkarte OWNER ibkarte"; Pruefe 'Datenbank anlegen' }
  Remove-Item Env:PGPASSWORD
  Ok 'Datenbank ibkarte bereit'
  }

  # ---------- 4. Import ----------
  Schritt 'Daten importieren (beim ersten Mal lädt .NET Pakete, das dauert etwas)'
  # Ein noch laufendes Backend sperrt die Programmdatei und verhindert den Neubau.
  $alt = Get-Process -Name 'IbKarte.Api' -ErrorAction SilentlyContinue
  if ($alt) { $alt | Stop-Process -Force; Start-Sleep -Seconds 1; Ok 'Laufendes Backend beendet' }
  Push-Location (Join-Path $Karte 'backend\src\IbKarte.Api')
  dotnet run --no-launch-profile -- import; $code = $LASTEXITCODE
  Pop-Location
  if ($code -ne 0) { throw "Import fehlgeschlagen (Code $code)." }

  # ---------- 5. Frontend ----------
  Schritt 'Frontend vorbereiten'
  Push-Location (Join-Path $Karte 'frontend')
  npm install --no-fund --no-audit; $code = $LASTEXITCODE
  Pop-Location
  if ($code -ne 0) { throw "npm install fehlgeschlagen (Code $code)." }

  # ---------- 6. Verknüpfung ----------
  Schritt 'Desktop-Verknüpfung anlegen'
  $lnk = (New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path ([Environment]::GetFolderPath('Desktop')) 'IB-Karte starten.lnk'))
  $lnk.TargetPath = Join-Path $Karte 'starten.bat'
  $lnk.WorkingDirectory = $Karte
  $lnk.Save()
  Ok 'Verknüpfung "IB-Karte starten" liegt auf dem Desktop'

  Schritt 'Fertig – die Karte wird gestartet'
  Start-Process (Join-Path $Karte 'starten.bat') -WorkingDirectory $Karte
}
catch {
  Write-Host ''
  Write-Host "FEHLER: $($_.Exception.Message)" -ForegroundColor Red
  Write-Host 'Das Skript kann nach dem Beheben einfach erneut gestartet werden.'
}
Write-Host ''
Read-Host 'Enter drücken zum Schließen'
