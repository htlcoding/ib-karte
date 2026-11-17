@echo off
rem IB-Karte starten: Backend und Frontend in eigenen Fenstern, dann die Karte im Browser oeffnen.
cd /d "%~dp0"
rem Altes Backend beenden, sonst ist die Programmdatei gesperrt
taskkill /im IbKarte.Api.exe /f >nul 2>&1
start "IB-Karte Backend" cmd /k "cd /d "%~dp0backend\src\IbKarte.Api" && set "ASPNETCORE_URLS=http://localhost:5207"&& dotnet run --no-launch-profile"
start "IB-Karte Frontend" cmd /k "cd /d "%~dp0frontend" && npm run dev -- --port 5173"
echo Die Karte startet ... (die beiden Fenster bitte offen lassen, zum Beenden schliessen)
timeout /t 15 /nobreak >nul
start "" http://localhost:5173
