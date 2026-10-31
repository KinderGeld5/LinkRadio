# LinkRadio (BepInEx-Mod für Nomad Drive)

> **Hinweis für Claude Code:** Dieses Projekt wurde bereits gemeinsam mit dem Besitzer (Florian, deutschsprachig) in einer
> langen Claude-Chat-Session gebaut, getestet und ausgeliefert. **Nicht neu aufbauen.** Quellcode lesen, gezielt ändern,
> bauen. Antworten und Kommentare für den Nutzer auf Deutsch. Der Stand unten ist der Ausgangspunkt für weitere Arbeit.

## Was es ist
BepInEx-5-Plugin (kompatibel mit BepInExPack_NomadDrive 5.4.23.2). Spielt YouTube-, YouTube-Music-Links, YouTube-Playlists und
einzelne Spotify-Tracks über Autoradio/Boombox (3D) oder global ab – **im Multiplayer synchron** (Host + Mitspieler, Mirror).
Version **1.1.0** (1.0.0 war die erste öffentliche Version; 1.1.0 ergänzt nur Beschreibung und `cover.png` für ModMenu), GUID `linkradio.nomaddrive`, Namespace `LinkRadio`. Soft-Dependency auf den Boombox-Mod `ramses.nomaddrive.boombox`.

## Stand
- Getestet im Spiel (Host + 1 Kollege): YouTube-Links, Wunsch/Annehmen/Warteschlange, gemeinsamer Start (ab 1.2.0 funktioniert es auch beim Kollegen).
- Neues Fenster (1.4.0): öffnet und funktioniert laut Florian im Spiel. Multiplayer mit 1.4.0 noch nicht geprüft.
- Stand 1.5.0 (Song-Anzeige, Verschieben, DE/EN): laut Florian im Spiel und im Multiplayer getestet.
- **Nicht im Spiel getestet:** Playlists (1.3.0), Spotify-Änderungen (1.3.1: `spotify.link`, Fallbacks), Hotkey-Umbelegung im Fenster.
- Bis 1.3.1 wurde mit `mcs` (Mono) gebaut. 1.4.0 ist unter Windows mit `build.bat` gebaut; `build.sh` ist seit den neuen Dateien nicht erneut gelaufen.
- `Managed\UnityEngine.IMGUIModule.dll` ist gestrippt (kein `GUIStyle.border` usw.). Für den Build die Unity-Module aus `unstripped_libs\` nehmen, siehe `libs/README.txt`.

## Projektaufbau
| Pfad | Inhalt |
|---|---|
| `src/LinkRadio.cs` | Plugin: Config, `LinkRadioMsg`, `LinkRadioRunner` (Netzwerk, Queue, Wiedergabe, Tools) |
| `src/Ui.cs` | F7-Fenster mit den Reitern Player, Queue, Settings (zweiter Teil von `LinkRadioRunner`) |
| `src/Theme.cs` | Aussehen im Autoradio-Stil: Farben, `GUIStyle`s, zur Laufzeit erzeugte Texturen |
| `src/Util.cs` | Reine Hilfsfunktionen (URL-Prüfung, YouTube-/Playlist-ID, Spotify-Parsing, Formatierung) – unit-getestet |
| `test/Test.cs`, `test/build_test.sh` | Tests für `Util.cs` (`./test/build_test.sh` → „ALL OK“) |
| `build.sh` / `build.bat` / `LinkRadio.csproj` | Build (siehe unten) |
| `package/` + `package.sh` | Spieler-ZIP: `BepInEx/plugins/LinkRadio/LinkRadio.dll` + `README.md` aus dem Hauptordner (Deutsch und Englisch) |
| `libs/` | Hier die Referenz-DLLs ablegen (nicht enthalten, siehe `libs/README.txt`) |

## Bauen
1. DLLs nach `libs/` kopieren (Liste in `libs/README.txt`) **oder** `MANAGED` / `BEPINEX_CORE` setzen.
2. `./build.sh` → `bin/LinkRadio.dll` (benötigt `mono-mcs`). Windows: `build.bat` (dotnet SDK, csproj ungetestet).
3. `./test/build_test.sh` für die Tests, `./package.sh` für das Spieler-ZIP.
Nach Änderungen: `Version` in `LinkRadio.cs` erhöhen und `README.md` (Spieler-Doku) anpassen.

## Architektur
- **Host-autoritativ.** Host/Server hält Queue und aktuellen Track; Clients bekommen nur Nachrichten. Offline/Singleplayer = Host-Pfad lokal.
- **Jeder lädt selbst:** yt-dlp + ffmpeg → mp3 im Cache (`BepInEx\cache\LinkRadio`, Key = YouTube-ID) → `UnityWebRequestMultimedia` → `AudioClip`. Kein Audio über das Netz.
- **Nachricht:** eine Mirror-`NetworkMessage` `LinkRadioMsg` mit Feld `Op` (Writer/Reader von Hand registriert, kein Weaver). Mirror trennt Clients, die unbekannte Nachrichten bekommen →
  Client schickt beim Beitritt `Op 10` (Hallo, Config `AnnounceToHost`), Host sendet nur an „modded“ Verbindungen.
- **Ops:** 1 Play · 2 Stop · 3 Pause · 4 Resume · 5 Go (Startzeitpunkt nach Barriere) · 10 Hallo · 11 Play now · 12 Stop · 13 Pause · 14 Resume · 15 Statustext · 16 Wunsch/Enqueue · 17 Skip · 18 Queue-Info · 19 Client „bereit“ · 21 Prefetch-Hinweis. Op 11–14/17 von Clients nur mit `ClientsCanControl`.
- **Startbarriere (1.2.0):** `Op 1` kommt mit `Start = 1e9` (= „noch nicht“). Jeder Client meldet nach dem Laden `Op 19`; wenn alle bereit sind (oder `WaitForEveryoneSeconds`, Standard 180, überschritten / Knopf „Start now“), sendet der Host `Op 5` mit `Start = NetworkTime.time + 1.5`. Fehler beim Client zählen auch als „fertig“.
- **Sync:** Position = `NetworkTime.time - Start`; Drift > 0,4 s wird alle 3 s korrigiert. Queue-Fortschritt in `QueueTick` (`srvTrackSeq` wird nur bei `Op 1` gesetzt, nicht bei Pause/Resume).
- **Song-Anzeige (1.5.0):** `DrawHud` in `Ui.cs`, eigenes `GUI.Window`. Bei offenem F7-Fenster per Maus verschiebbar, Position in `[Overlay]`. Dauer des nächsten Songs: Host holt sie mit yt-dlp (`CoFetchInfo`, Playlist über `%(duration)s`) und sendet sie im Feld `Pos` von `Op 18`; ältere Clients ignorieren das.
- **Sprache (1.5.0):** `L(en, de)` in `Ui.cs`, Config `General.Language`. Meldungen an Clients kommen in der Sprache des Hosts; Fehlertexte von yt-dlp bleiben englisch.
- **Warteschlange/Wünsche:** Client → `Op 16` → Host zeigt Hinweis (F9 annehmen / F10 ablehnen, einstellbar im Fenster), `AutoAcceptRequests` optional.
- **Playlists (1.3.0):** nur reine `youtube.com/playlist?list=…`-Links (auch music.youtube.com). Host liest mit `yt-dlp --flat-playlist --print "%(playlist_title)s|||%(id)s|||%(title)s"` (max. `MaxPlaylistSongs`=50) und legt Einzel-Items in die Queue. **Prefetch:** Host sendet `Op 21` mit den nächsten `PrefetchAhead` (3) URLs, jeder Client lädt sie im Hintergrund (`CoPrefetchLoop`).
- **Spotify:** Seite wird **vom Host** gelesen (`CoResolveSpotify`): `<title>`/og-Meta → Interpret+Titel, Fallback oEmbed (nur Titel), `spotify.link` wird geöffnet; dann `ytsearch1:` per yt-dlp → YouTube-ID. Kein Spotify-Stream (DRM), nur die YouTube-Entsprechung. Keine Spotify-Playlists/Alben.
- **Tools** (`EnsureTools`): lädt beim ersten Start yt-dlp.exe, ffmpeg (ZIP, ~200 MB) und deno.exe (~45 MB, JS-Runtime für yt-dlp) nach `…\LinkRadio\tools`. Windows-only (curl.exe/PowerShell/`Expand-Archive`).
- **yt-dlp-Härtung:** Cookies über `CookiesFromBrowser` / `CookiesFile` (bei Cookie-Fehler automatisch ohne Cookies weiter), Zweitversuch mit `player_client=android_vr,tv,web_safari` bei Bot-Check / „Requested format is not available“, `--js-runtimes deno:<pfad>`.
- **Spiel-Reflection:** `NomadDrive.Features.Vehicle.Parts.Radio.VehicleRadio` (Feld `audioSource` → Mixer-Gruppe), `BoomboxMod.BoomboxPlayer` (Lautsprecher-Auswahl, Radius `SpeakerPickDistance`), `NomadDrive.Features.Inputs.ActionMapsManager` (`SuspendGameplayMaps`/`ResumeGameplayMaps`, solange das Fenster offen ist).
- **Sicherheit:** Links nur von Hosts aus `AllowedHosts`, Sonderzeichen-Prüfung (`Util.TryParseHttpUrl`), Playlist-/Video-IDs per Regex, Argumente in Anführungszeichen; Länge max. `MaxMinutes`=15, keine Livestreams.

## Config (`BepInEx\config\linkradio.nomaddrive.cfg`)
`[General]` OpenKey=F7, AcceptKey=F9, RejectKey=F10, Language, Volume, UseGameVolume, DefaultRange, SpeakerPickDistance ·
`[Overlay]` ShowOverlay, PositionX, PositionY ·
`[Multiplayer]` ClientsCanControl=false, AllowClientRequests, AutoAcceptRequests, MaxQueue=20, DisplayName, AnnounceToHost, WaitForEveryoneSeconds=180, StartDelaySeconds (nur offline) ·
`[Playlist]` MaxPlaylistSongs=50, PrefetchAhead=3 · `[Download]` MaxMinutes, CacheLimitMB=600, AutoDownloadTools, YtDlpPath, FfmpegPath, CookiesFromBrowser, CookiesFile · `[Security]` AllowedHosts.
Hinweis: Bestehende Config-Dateien behalten ihre alten Werte, wenn sich im Code ein Default ändert.

## Wichtige Entscheidungen
- Kein Spotify-Stream (DRM) → YouTube-Entsprechung; kein Audio über Mirror → jeder lädt selbst.
- Host-autoritativ + Barriere statt „jeder startet, wenn er fertig ist“ (Wunsch des Nutzers: gleichzeitig hören).
- Client darf nur wünschen; Steuerung nur mit `ClientsCanControl`.
- Playlists nur als reine Playlist-Links; `watch?v=…&list=…` bleibt ein einzelner Song.
- Nur Hilfsfunktionen sind unit-testbar (`Util.cs`); alles Spiel-/Unity-nahe lässt sich nur im Spiel testen.

## Bekannte Probleme / Risiken
- **YouTube-Bot-Check** („Sign in to confirm you're not a bot“) hängt an der IP des jeweiligen Spielers; Gegenmittel (Cookies, deno, Zweitversuch) sind ohne Garantie. Chromium-Browser (Brave/Chrome/Edge) lassen sich unter Windows oft nicht auslesen → Firefox oder `CookiesFile`.
- Client-Namen für `player_client` und die yt-dlp-/deno-Optionen sind nicht dauerhaft verifiziert (YouTube ändert sich); yt-dlp/ffmpeg/deno werden unversioniert „latest“ geladen.
- Ganze Datei wird als `AudioClip` dekodiert (`streamAudio=false`): bei 15 min Stereo sehr viel RAM.
- Alle Spieler brauchen dieselbe Mod-Version; ältere Clients melden nie „bereit“ → Host wartet bis zum Timeout.
- Hotkey-Umbelegung nutzt IMGUI-Key-Events – im Spiel nicht verifiziert.
- Windows-only Tool-Installation.
- Das Fenster braucht zur Laufzeit die ungestrippten Unity-Module. BepInExPack_NomadDrive liefert sie über `dll_search_path_override = unstripped_libs`.
- Gesperrte/private Playlist-Videos werden beim Abspielen übersprungen (Fehlermeldung).

## Gelernt beim Modden von Nomad Drive
- Solange das Spiel läuft, ist die Mod-DLL gesperrt: vor dem Kopieren prüfen, ob der Prozess `Nomad Drive` läuft, und die alte DLL sichern.
- Referenzen: Mirror und Input System aus `Nomad Drive_Data\Managed`, Unity-Module aus `unstripped_libs` (siehe `libs/README.txt`).
- IMGUI: Nie im selben Event die Struktur ändern. Reiterwechsel erst im Layout-Event übernehmen, Listen-Änderungen nach dem Zeichnen ausführen.
- Eigene `GUIStyle`s mit zur Laufzeit erzeugten Texturen brauchen `HideFlags.HideAndDontSave` und eine Neuerzeugung, falls sie verschwinden (`Theme.Ensure`).
- Neue Daten für Clients ohne Protokollbruch: freie Felder bestehender Nachrichten nutzen (Beispiel `Pos` in `Op 18`). Unbekannte Mirror-Nachrichten trennen Clients.
- Git: Der App-Ordner für Sessions hat einen zu langen Pfad (`.git/hooks: Filename too long`). Repos in einem kurzen Pfad wie `E:\Claude` anlegen.

## Offene Punkte / Ideen
- Spotify-Playlists/Alben (z. B. über Embed-Seite) – ungetestet möglich.
- Streaming statt komplettes Dekodieren; Cache-/Speicher-Anzeige im Fenster.
- Queue-Verwaltung im Fenster (einzelne Songs entfernen/umsortieren).
- Versionsabgleich Host/Client (statt Warten bis Timeout).
- Pin/Update-Strategie für yt-dlp/deno.
