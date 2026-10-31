# LinkRadio für Nomad Drive

Hör gemeinsam Musik im Auto: LinkRadio spielt YouTube-, YouTube-Music- und Spotify-Links über Autoradio oder Boombox ab, synchron für alle Spieler mit dem Mod.

*English version below.*

## Funktionen

- YouTube- und YouTube-Music-Links, YouTube-Playlists und einzelne Spotify-Songs
- Musik kommt aus Autoradio oder Boombox (3D mit Reichweite) oder global
- Im Multiplayer startet ein Song bei allen gleichzeitig
- Warteschlange und Songwünsche: Mitspieler wünschen, der Host nimmt an oder lehnt ab
- Fenster im Autoradio-Stil mit den Bereichen Player, Warteschlange und Einstellungen
- Song-Anzeige auf dem Bildschirm mit Restzeit und nächstem Song, frei verschiebbar
- Menü auf Deutsch und Englisch
- Hotkeys im Spiel änderbar

## Installation

Voraussetzung ist BepInExPack_NomadDrive. **Jeder Spieler braucht den Mod, auch der Host.**

1. Den Ordner `BepInEx` aus der ZIP in den Spielordner kopieren (neben `Nomad Drive.exe`) und Dateien zusammenführen.
2. Danach liegt der Mod hier: `BepInEx\plugins\LinkRadio\LinkRadio.dll`

Beim ersten Start lädt der Mod einmalig yt-dlp, ffmpeg und deno in den Ordner `tools` (zusammen ca. 250 MB). Das kann ein paar Minuten dauern.

Die Boombox ist optional. LinkRadio funktioniert mit dem Boombox-Mod zusammen, braucht ihn aber nicht.

## Benutzung

1. Im Spiel **F7** drücken.
2. Link einfügen.
3. **Jetzt spielen** oder **Zur Warteschlange** klicken.

Steht beim Klick ein Autoradio oder eine Boombox in der Nähe (Standard 12 m), kommt die Musik von dort. Sonst läuft sie global.

**Song-Anzeige verschieben:** F7 öffnen und die Anzeige mit der Maus ziehen. Ausblenden und Zurücksetzen gehen in den Einstellungen.

### Links

| Link | Funktioniert |
|---|---|
| YouTube / YouTube Music | Ja, bis 15 Minuten, keine Livestreams |
| YouTube-Playlist (`youtube.com/playlist?list=...`) | Ja, bis 50 Songs |
| Spotify-Song (`open.spotify.com/track/...`, `spotify.link/...`) | Ja, gespielt wird die passende YouTube-Version |
| Spotify-Playlists und -Alben | Nein |

Ein Link der Form `watch?v=...&list=...` zählt als einzelner Song. Für die ganze Playlist den Link der Playlist-Seite nehmen.

## Multiplayer

- Der Host steuert die Musik, Mitspieler hören mit.
- Mitspieler können Songs wünschen. Der Host nimmt mit **F9** an und lehnt mit **F10** ab.
- Jeder Spieler lädt den Ton selbst herunter, es wird kein Audio übers Netzwerk geschickt.
- Der Host wartet, bis alle fertig geladen haben, und startet den Song dann für alle gleichzeitig. Mit **Jetzt starten** geht es sofort los.

**Wichtig:** Hat der Host den Mod nicht, wirst du beim Beitreten getrennt. Für Server ohne Mod in der Config unter `[Multiplayer]` den Wert `AnnounceToHost = false` setzen.

## Einstellungen

Die Datei liegt unter `BepInEx\config\linkradio.nomaddrive.cfg`.

| Einstellung | Standard | Bedeutung |
|---|---|---|
| `AllowClientRequests` | an | Mitspieler dürfen Songs wünschen |
| `AutoAcceptRequests` | aus | Wünsche ohne Nachfrage annehmen |
| `ClientsCanControl` | aus | Mitspieler dürfen Pause, Überspringen und Stop nutzen |
| `MaxQueue` | 20 | Maximale Länge der Warteschlange |
| `PrefetchAhead` | 3 | So viele nächste Songs laden alle im Hintergrund |
| `CacheLimitMB` | 600 | Größe des Song-Speichers |
| `CookiesFromBrowser`, `CookiesFile` | leer | Hilfe bei der YouTube-Bot-Prüfung, siehe unten |

## Probleme lösen

Fehlerdetails stehen in `BepInEx\LogOutput.log`.

**„Sign in to confirm you're not a bot“:** YouTube prüft die IP dieses PCs, der Mod selbst ist nicht kaputt.

1. VPN oder Proxy ausschalten.
2. Im Mod unter Einstellungen **yt-dlp aktualisieren** klicken.
3. Cookies nutzen: In Firefox bei YouTube einloggen (am besten mit einem Nebenkonto), Firefox schließen und in der Config `CookiesFromBrowser = firefox` eintragen. Danach das Spiel neu starten.

Mit Brave, Chrome oder Edge klappt das oft nicht. Exportiere dann die Cookies als `cookies.txt` und trage den Pfad unter `CookiesFile` ein. Gib Cookie-Dateien niemals weiter, sie sind wie ein Passwort.

**„Requested format is not available“:** Warte nach dem Spielstart 1 bis 2 Minuten, bis deno geladen ist. Klicke dann auf **yt-dlp aktualisieren** und spiele den Song noch einmal.

**Ein Song läuft bei einem Spieler nicht:** Meist schlägt der Download auf dem PC dieses Spielers fehl. Der Host wartet dann nicht weiter auf ihn.

## Sicherheit und Hinweise

- Links werden nur von erlaubten Seiten geladen (`AllowedHosts`) und auf Sonderzeichen geprüft, weil dein PC dafür yt-dlp startet.
- Der Mod lädt yt-dlp, ffmpeg und deno von GitHub. Du kannst auch eigene Kopien in den Ordner `tools` legen und `AutoDownloadTools` ausschalten.
- Spotify wird nicht gestreamt. Der Mod liest nur Titel und Interpret und sucht die passende YouTube-Version, die Aufnahme kann also abweichen.
- Das Herunterladen von YouTube-Audio kann gegen die Nutzungsbedingungen von YouTube verstoßen. Nutzung auf eigene Verantwortung und nur privat.

---

# LinkRadio for Nomad Drive (English)

Listen to music together on the road: LinkRadio plays YouTube, YouTube Music, and Spotify links through the car radio or a boombox, in sync for everyone with the mod.

## Features

- YouTube and YouTube Music links, YouTube playlists, and single Spotify tracks
- Sound comes from the car radio or boombox (3D with range) or plays globally
- In multiplayer, a song starts for everyone at the same moment
- Queue and song requests: players request, the host accepts or rejects
- Car radio style window with Player, Queue, and Settings tabs
- On-screen overlay with time left and the next song, which you can move
- Menu in English and German
- Hotkeys can be changed in game

## Installation

You need BepInExPack_NomadDrive. **Every player needs the mod, including the host.**

1. Copy the `BepInEx` folder from the ZIP into the game folder (next to `Nomad Drive.exe`) and merge the files.
2. The mod should end up here: `BepInEx\plugins\LinkRadio\LinkRadio.dll`

On first start, the mod downloads yt-dlp, ffmpeg, and deno into its `tools` folder once (about 250 MB in total). This can take a few minutes.

## How to use

1. Press **F7** in game.
2. Paste a link.
3. Click **Play now** or **Add to queue**.

If a car radio or boombox is nearby when you click (12 m by default), the music comes from there. Otherwise it plays globally.

To move the overlay, open the F7 window and drag it with the mouse. You can hide or reset it in Settings.

Spotify playlists and albums aren't supported. Videos are limited to 15 minutes, and live streams don't work.

## Multiplayer

- The host controls the music. Players can request songs, and the host accepts with **F9** or rejects with **F10**.
- Each player downloads the audio themselves. The host waits until everyone is ready, then starts the song for all.

**Important:** If the host doesn't have the mod, you get disconnected when joining. For servers without the mod, set `AnnounceToHost = false` under `[Multiplayer]` in `BepInEx\config\linkradio.nomaddrive.cfg`.

## Troubleshooting

Details are in `BepInEx\LogOutput.log`.

**"Sign in to confirm you're not a bot":** YouTube is checking this PC's IP address. Turn off any VPN or proxy, then click **Update yt-dlp** in Settings. If that doesn't help, sign in to YouTube in Firefox with a spare account, close Firefox, set `CookiesFromBrowser = firefox` in the config, and restart the game. Never share cookie files, they work like a password.

**"Requested format is not available":** Wait 1 to 2 minutes after starting the game so deno can finish downloading. Then click **Update yt-dlp** and try again.

## Notes

- Links are only loaded from allowed hosts (`AllowedHosts`), because your PC runs yt-dlp on them.
- Spotify isn't streamed. The mod reads the title and artist and plays the best YouTube match, so the recording can differ.
- Downloading audio from YouTube may violate YouTube's Terms of Service. Use at your own risk and for private use only.
