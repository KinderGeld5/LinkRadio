// LinkRadio: the F7 window and the song overlay. Look and textures are in Theme.cs.
using System;
using System.IO;
using BepInEx.Configuration;
using Mirror;
using UnityEngine;

namespace LinkRadio
{
    internal partial class LinkRadioRunner
    {
        const int TabPlayer = 0, TabQueue = 1, TabSettings = 2;
        int tab, nextTab;
        float winHeight = 520f;
        Rect hud;
        Action afterDraw;   // list changes wait until the window is drawn, IMGUI needs the same layout during one event

        string L(string en, string de) { return P.CfgLanguage.Value == "de" ? de : en; }

        string KeyHint() { return " (" + P.CfgAcceptKey.Value + L(" accept / ", " annehmen / ") + P.CfgRejectKey.Value + L(" reject)", " ablehnen)"); }

        void SetStatus(string s, bool bad) { status = s; statusBad = bad; }

        string Role()
        {
            if (NetworkServer.active) return NetworkClient.active ? L("You are the HOST - everyone with the mod hears your music.", "Du bist der HOST - alle mit dem Mod hören deine Musik.") : "Server";
            if (NetworkClient.isConnected) return P.CfgClientControl.Value ? L("Client (host decides if you may control)", "Client (der Host entscheidet, ob du steuern darfst)") : L("Client - you hear the host's music. Controls need host permission.", "Client - du hörst die Musik des Hosts. Steuern geht nur mit Erlaubnis des Hosts.");
            return L("Offline - local playback only.", "Offline - nur lokale Wiedergabe.");
        }

        string RoleShort()
        {
            if (NetworkServer.active) return NetworkClient.active ? "Host" : "Server";
            return NetworkClient.isConnected ? "Client" : "Offline";
        }

        void OnGUI()
        {
            CaptureRebind();
            Theme.Ensure();
            float s = Mathf.Clamp(Screen.height / 1080f, 1f, 2.5f);
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            GUI.depth = -1000;
            DrawHud(s);
            if (!open)
            {
                if (pending.Count > 0 && IsHostLike())
                {
                    QItem it = pending[0];
                    string txt = L("Song request from ", "Songwunsch von ") + it.Who + ":\n" + (!string.IsNullOrEmpty(it.Title) ? it.Title : Util.Short(it.Url, 50)) +
                                 "\n[" + P.CfgAcceptKey.Value + "] " + L("accept", "annehmen") + "    [" + P.CfgRejectKey.Value + "] " + L("reject", "ablehnen") + (pending.Count > 1 ? "    (" + pending.Count + L(" waiting)", " offen)") : "");
                    float w = 380f;
                    GUI.Box(new Rect(Screen.width / s / 2f - w / 2f, 20f, w, 74f), txt, Theme.Overlay);
                }
                GUI.matrix = old;
                return;
            }
            win.width = Theme.Width; win.height = winHeight;
            win = GUI.Window(0x4C52, win, DrawWindow, GUIContent.none, Theme.Window);
            win.x = Mathf.Clamp(win.x, 0, Screen.width / s - 80); win.y = Mathf.Clamp(win.y, 0, Screen.height / s - 40);
            GUI.matrix = old;
        }

        // ----- overlay: current song and next song, stays on screen while you play -----
        void DrawHud(float s)
        {
            if (!P.CfgHudShow.Value) return;
            Track t = cur;
            if ((t == null || t.Failed) && !open) return;
            double pos, len;
            TrackTime(t, out pos, out len);
            if (!open && len > 0 && !t.Loop && pos > len + 1) return;   // song is over, nothing follows
            string next = NextTitle();
            hud.width = Theme.HudWidth;
            hud.height = Theme.HudNowHeight + (next != null || open ? Theme.HudGap + Theme.HudNextHeight : 0f);
            hud = GUI.Window(0x4C53, hud, DrawHudWindow, GUIContent.none, Theme.HudWindow);
            hud.x = Mathf.Clamp(hud.x, 0, Mathf.Max(0f, Screen.width / s - hud.width));
            hud.y = Mathf.Clamp(hud.y, 0, Mathf.Max(0f, Screen.height / s - hud.height));
            if (open && Event.current.rawType == EventType.MouseUp && (hud.x != P.CfgHudX.Value || hud.y != P.CfgHudY.Value)) { P.CfgHudX.Value = hud.x; P.CfgHudY.Value = hud.y; }
        }

        void DrawHudWindow(int id)
        {
            Track t = cur;
            bool playing = t != null && t.Clip != null && !t.Paused;
            float w = Theme.HudWidth, pad = 12f;
            GUI.Box(new Rect(0, 0, w, Theme.HudNowHeight), GUIContent.none, Theme.Lcd);
            GUI.Label(new Rect(pad, 7, 200, 14), TrackState(t), Theme.LcdSmall);
            Rect eq = new Rect(w - pad - 32f, 6, 32, 14);
            if (Event.current.type == EventType.Repaint) DrawLevels(eq, playing);
            GUI.Label(new Rect(pad, 22, w - pad * 2f, 22), Util.Short(TrackName(t), 40), Theme.LcdTitle);
            double pos, len;
            TrackTime(t, out pos, out len);
            if (Event.current.type == EventType.Repaint) DrawProgress(new Rect(pad, 47, w - pad * 2f, 6f), pos, len);
            GUI.Label(new Rect(pad, 56, 150, 14), TimeText(t, pos, len), Theme.LcdSmall);
            if (len > 0 && pos >= 0) GUI.Label(new Rect(w - pad - 150f, 56, 150, 14), L(Util.FormatTime(Math.Max(0, len - pos)) + " left", "noch " + Util.FormatTime(Math.Max(0, len - pos))), Theme.LcdSmallRight);

            if (hud.height > Theme.HudNowHeight)
            {
                Rect r = new Rect(0, Theme.HudNowHeight + Theme.HudGap, w, Theme.HudNextHeight);
                string next = NextTitle();
                GUI.Box(r, GUIContent.none, Theme.HudNext);
                GUI.Label(new Rect(r.x + pad, r.y, w - pad * 2f - 50f, r.height), L("Next: ", "Danach: ") + (next != null ? Util.Short(next, 40) : L("nothing queued", "nichts in der Warteschlange")), Theme.HudNextText);
                if (next != null && nextSeconds > 0f) GUI.Label(new Rect(w - pad - 50f, r.y, 50f, r.height), Util.FormatTime(nextSeconds), Theme.HudNextTime);
            }
            if (open) GUI.DragWindow(new Rect(0, 0, 10000, 10000));
        }

        string NextTitle()
        {
            if (string.IsNullOrEmpty(queueInfo)) return null;
            string num, title, who;
            return Util.ParseQueueLine(queueInfo.Split('\n')[0], out num, out title, out who) ? title : null;
        }

        string TrackState(Track t)
        {
            if (t == null) return "Standby";
            if (t.Paused) return L("Paused", "Pausiert");
            return t.Clip != null ? L("Now playing", "Läuft gerade") : L("Loading ...", "Lädt ...");
        }

        string TrackName(Track t)
        {
            if (t == null) return L("No song", "Kein Song");
            return !string.IsNullOrEmpty(t.Title) ? t.Title : t.Key;
        }

        void TrackTime(Track t, out double pos, out double len)
        {
            pos = 0; len = 0;
            if (t == null || t.Clip == null) return;
            len = t.Clip.length;
            pos = t.Paused ? t.PausePos : NetworkTime.time - t.Start;
            if (t.Loop) pos = pos % len;
        }

        string TimeText(Track t, double pos, double len)
        {
            if (t == null || t.Clip == null) return "-:-- / -:--";
            return pos < 0 ? L("Starting ...", "Startet ...") : Util.FormatTime(pos) + " / " + Util.FormatTime(len);
        }

        void DrawProgress(Rect r, double pos, double len)
        {
            GUI.DrawTexture(r, Theme.BarBack);
            float fill = len > 0 ? Mathf.Clamp01((float)(pos / len)) * r.width : 0f;
            if (fill >= 6f) GUI.DrawTexture(new Rect(r.x, r.y, fill, r.height), Theme.BarFill);
        }

        // ----- window -----
        void DrawWindow(int id)
        {
            if (Event.current.type == EventType.Layout) tab = nextTab;
            bool host = IsHostLike();

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal(Theme.Header, GUILayout.Height(40));
            GUILayout.Label("LinkRadio", Theme.TitleText, GUILayout.Height(40));
            GUILayout.Label(RoleShort(), Theme.Pill);
            GUILayout.Label(P.CfgKey.Value + L(" to close", " zum Schließen"), Theme.HintRight, GUILayout.Height(40));
            GUILayout.EndHorizontal();
            DrawTabs();
            GUILayout.BeginVertical(Theme.Body);
            if (tab == TabQueue) DrawQueueTab(host);
            else if (tab == TabSettings) DrawSettingsTab(host);
            else DrawPlayerTab(host);
            GUILayout.EndVertical();
            GUILayout.EndVertical();
            if (Event.current.type == EventType.Repaint) winHeight = GUILayoutUtility.GetLastRect().yMax + 2f;

            GUI.DragWindow(new Rect(0, 0, 10000, 40));
            if (afterDraw != null) { Action a = afterDraw; afterDraw = null; a(); }
        }

        void DrawTabs()
        {
            string[] names = { "Player", L("Queue", "Warteschlange"), L("Settings", "Einstellungen") };
            GUILayout.BeginHorizontal(Theme.TabBar, GUILayout.Height(34));
            for (int i = 0; i < names.Length; i++)
            {
                int queued = Util.CountQueue(queueInfo);
                string name = i == TabQueue && queued > 0 ? names[i] + " (" + queued + ")" : names[i];
                if (GUILayout.Button(name, i == tab ? Theme.TabOn : Theme.Tab, GUILayout.Height(34))) nextTab = i;
                if (i == tab && Event.current.type == EventType.Repaint)
                {
                    Rect r = GUILayoutUtility.GetLastRect();
                    Paint(new Rect(r.x, r.yMax - 2f, r.width, 2f), Theme.Amber);
                }
            }
            GUILayout.EndHorizontal();
        }

        // ----- player -----
        void DrawPlayerTab(bool host)
        {
            DrawDisplay();
            GUILayout.Label(status, statusBad ? Theme.StatusBad : Theme.StatusOk);

            if (host && pending.Count > 0)
            {
                GUILayout.Space(8);
                GUILayout.BeginVertical(Theme.Request);
                GUILayout.Label(L("Requests", "Wünsche") + KeyHint(), Theme.ReqTitle);
                int shown = Math.Min(pending.Count, 4), more = pending.Count - shown;
                for (int i = 0; i < shown; i++)
                {
                    QItem it = pending[i];
                    int index = i;
                    GUILayout.Space(4);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(it.Who + ": " + Util.Short(!string.IsNullOrEmpty(it.Title) ? it.Title : it.Url, 30), Theme.Label, GUILayout.Height(30));
                    if (GUILayout.Button(L("Accept", "Annehmen"), Theme.Button, GUILayout.Width(80))) afterDraw = () => AcceptPending(index);
                    GUILayout.Space(6);
                    if (GUILayout.Button(L("Reject", "Ablehnen"), Theme.Button, GUILayout.Width(80))) afterDraw = () => RejectPending(index);
                    GUILayout.EndHorizontal();
                }
                if (more > 0) GUILayout.Label("+ " + more + L(" more", " weitere"), Theme.Hint);
                GUILayout.EndVertical();
            }

            GUILayout.Space(10);
            GUILayout.Label(L("YouTube, YouTube Music, or Spotify link", "YouTube-, YouTube-Music- oder Spotify-Link"), Theme.Hint);
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUI.SetNextControlName("lr_url");
            url = GUILayout.TextField(url, Theme.TextField, GUILayout.Height(34), GUILayout.Width(Theme.BodyWidth - 120f)); if (url.Length > 400) url = url.Substring(0, 400);
            GUILayout.Space(8);
            if (GUILayout.Button(L("Paste", "Einfügen"), Theme.Button, GUILayout.Width(76), GUILayout.Height(34))) url = (GUIUtility.systemCopyBuffer ?? "").Trim();
            GUILayout.Space(8);
            if (GUILayout.Button("X", Theme.Button, GUILayout.Width(28), GUILayout.Height(34))) url = "";
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            bool playNow = false, enqueue = false;
            float half = (Theme.BodyWidth - 8f) / 2f;
            if (host)
            {
                playNow = GUILayout.Button(L("Play now", "Jetzt spielen"), Theme.ButtonMain, GUILayout.Width(half), GUILayout.Height(34));
                GUILayout.Space(8);
                enqueue = GUILayout.Button(L("Add to queue", "Zur Warteschlange"), Theme.Button, GUILayout.Width(half), GUILayout.Height(34));
            }
            else enqueue = GUILayout.Button(L("Request song (host must accept)", "Song wünschen (Host muss annehmen)"), Theme.ButtonMain, GUILayout.Width(Theme.BodyWidth), GUILayout.Height(34));
            GUILayout.EndHorizontal();
            if (playNow || enqueue)
            {
                string sn; uint sp = PickSpeaker(out sn); lastSpeaker = sn;
                if (string.IsNullOrEmpty(url.Trim())) SetStatus(L("Paste a link first.", "Füge zuerst einen Link ein."), true);
                else
                {
                    SetStatus(L("Sending ...", "Wird gesendet ..."), false);
                    if (playNow) Dispatch(new LinkRadioMsg { Op = 11, Url = url.Trim(), Speaker = sp, Loop = loop, Range = range, Title = "" });
                    else Dispatch(new LinkRadioMsg { Op = 16, Url = url.Trim(), Speaker = sp, Range = range, Title = MyName() });
                    url = "";
                }
            }

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            bool paused = host ? srvPaused : (cur != null && cur.Paused);
            float third = (Theme.BodyWidth - 16f) / 3f;
            if (GUILayout.Button(paused ? L("Resume", "Weiter") : L("Pause", "Pause"), Theme.Button, GUILayout.Width(third), GUILayout.Height(30))) Dispatch(new LinkRadioMsg { Op = paused ? (byte)14 : (byte)13, Url = "", Title = "" });
            GUILayout.Space(8);
            if (GUILayout.Button(L("Skip", "Überspringen"), Theme.Button, GUILayout.Width(third), GUILayout.Height(30))) Dispatch(new LinkRadioMsg { Op = 17, Url = "", Title = "" });
            GUILayout.Space(8);
            if (GUILayout.Button("Stop", Theme.Button, GUILayout.Width(third), GUILayout.Height(30))) Dispatch(new LinkRadioMsg { Op = 12, Url = "", Title = "" });
            GUILayout.EndHorizontal();
            if (host && srvGate != null)
            {
                GUILayout.Space(8);
                if (GUILayout.Button(L("Start now (do not wait for the others)", "Jetzt starten (nicht auf die anderen warten)"), Theme.Button, GUILayout.Height(30))) GateRelease("host forced");
            }
            if (!host)
            {
                GUILayout.Space(4);
                GUILayout.Label(L("Pause, Skip, and Stop only work if the host allowed it (ClientsCanControl).", "Pause, Überspringen und Stop gehen nur, wenn der Host es erlaubt (ClientsCanControl)."), Theme.Hint);
            }

            GUILayout.Space(12);
            GUILayout.BeginHorizontal();
            GUILayout.Label(L("Volume", "Lautstärke"), Theme.Label, GUILayout.Width(76), GUILayout.Height(18));
            P.CfgVolume.Value = DrawSlider(P.CfgVolume.Value, 0f, 1f);
            GUILayout.Label((int)(P.CfgVolume.Value * 100) + "%", Theme.HintRight, GUILayout.Width(44), GUILayout.Height(18));
            GUILayout.Space(12);
            muted = DrawCheck(muted, L("Mute", "Stumm"), 72f);
            GUILayout.EndHorizontal();

            if (!ToolsOk())
            {
                GUILayout.Space(8);
                GUILayout.Label(toolsBusy ? L("Installing tools ...", "Tools werden installiert ...") : L("Tools missing. Install them on the Settings tab.", "Tools fehlen. Installiere sie unter Einstellungen."), Theme.StatusBad);
            }
        }

        void DrawDisplay()
        {
            Track t = cur;
            bool playing = t != null && t.Clip != null && !t.Paused;
            GUILayout.BeginVertical(Theme.Lcd);

            GUILayout.BeginHorizontal();
            GUILayout.Label(TrackState(t), Theme.LcdSmall, GUILayout.Height(16));
            Rect eq = GUILayoutUtility.GetRect(32f, 16f, GUILayout.Width(32));
            if (Event.current.type == EventType.Repaint) DrawLevels(eq, playing);
            GUILayout.EndHorizontal();

            GUILayout.Label(Util.Short(TrackName(t), 46), Theme.LcdTitle, GUILayout.Height(24));

            double pos, len;
            TrackTime(t, out pos, out len);
            Rect bar = GUILayoutUtility.GetRect(0f, 6f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint) DrawProgress(new Rect(bar.x, bar.y + 1f, bar.width, 6f), pos, len);
            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            GUILayout.Label(TimeText(t, pos, len), Theme.LcdSmall, GUILayout.Height(16));
            string where = t == null ? "" : t.Speaker == 0 ? "Global" : L("Speaker - ", "Lautsprecher - ") + t.Range.ToString("0") + " m";
            GUILayout.Label(where, Theme.LcdSmallRight, GUILayout.Height(16));
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        // Moving bars while a song plays. Only decoration, not the real sound level.
        void DrawLevels(Rect r, bool playing)
        {
            for (int i = 0; i < 6; i++)
            {
                float k = playing ? Mathf.PerlinNoise(Time.unscaledTime * 5f, i * 3.7f) : 0f;
                float h = Mathf.Max(2f, r.height * Mathf.Clamp01(k * 1.3f));
                Paint(new Rect(r.x + i * 5.5f, r.yMax - h, 3f, h), Theme.Amber);
            }
        }

        // ----- queue -----
        void DrawQueueTab(bool host)
        {
            if (string.IsNullOrEmpty(queueInfo))
            {
                GUILayout.Label(L("The queue is empty. Add a song on the Player tab.", "Die Warteschlange ist leer. Füge im Player einen Song hinzu."), Theme.Hint);
                return;
            }
            string[] lines = queueInfo.Split('\n');
            foreach (string line in lines)
            {
                string num, title, who;
                if (!Util.ParseQueueLine(line, out num, out title, out who))
                {
                    GUILayout.Space(6);
                    GUILayout.Label(line.Trim(), Theme.Hint);
                    continue;
                }
                GUILayout.BeginHorizontal();
                GUILayout.Label(num, Theme.Number, GUILayout.Width(24), GUILayout.Height(30));
                GUILayout.Label(Util.Short(title, 40), Theme.Label, GUILayout.Height(30));
                GUILayout.Label(Util.Short(who, 14), Theme.Who, GUILayout.Width(90), GUILayout.Height(30));
                GUILayout.EndHorizontal();
                DrawLine();
            }
            if (host)
            {
                GUILayout.Space(10);
                GUILayout.BeginHorizontal();
                GUILayout.Label("", Theme.Hint);
                if (GUILayout.Button(L("Clear queue", "Leeren"), Theme.Button, GUILayout.Width(110), GUILayout.Height(30))) afterDraw = () => { queue.Clear(); SendQueueInfo(); };
                GUILayout.EndHorizontal();
            }
        }

        // ----- settings -----
        void DrawSettingsTab(bool host)
        {
            GUILayout.Label(Role(), Theme.Hint);

            GUILayout.Space(10);
            float half = (Theme.BodyWidth - 84f - 8f) / 2f;
            GUILayout.BeginHorizontal();
            GUILayout.Label(L("Language", "Sprache"), Theme.Label, GUILayout.Width(84), GUILayout.Height(30));
            bool de = P.CfgLanguage.Value == "de";
            if (GUILayout.Button("English", de ? Theme.Button : Theme.ButtonMain, GUILayout.Width(half), GUILayout.Height(30))) P.CfgLanguage.Value = "en";
            GUILayout.Space(8);
            if (GUILayout.Button("Deutsch", de ? Theme.ButtonMain : Theme.Button, GUILayout.Width(half), GUILayout.Height(30))) P.CfgLanguage.Value = "de";
            GUILayout.EndHorizontal();

            GUILayout.Space(10); DrawLine(); GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            GUILayout.Label(L("Range", "Reichweite"), Theme.Label, GUILayout.Width(84), GUILayout.Height(18));
            range = DrawSlider(range, 8f, 120f);
            GUILayout.Label(range.ToString("0") + " m", Theme.HintRight, GUILayout.Width(48), GUILayout.Height(18));
            GUILayout.EndHorizontal();
            if (host)
            {
                GUILayout.Space(10);
                loop = DrawCheck(loop, L("Loop track (only for Play now)", "Song wiederholen (nur bei Jetzt spielen)"), Theme.BodyWidth);
            }
            GUILayout.Space(8);
            GUILayout.Label(L("Speaker: ", "Lautsprecher: ") + lastSpeaker, Theme.Label);
            GUILayout.Label(L("Speaker = nearest car radio or boombox within ", "Lautsprecher = nächstes Autoradio oder Boombox innerhalb von ") + P.CfgMaxPick.Value.ToString("0") + L(" m when you press the button, otherwise global.", " m beim Klick, sonst global."), Theme.Hint);

            GUILayout.Space(10); DrawLine(); GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            P.CfgHudShow.Value = DrawCheck(P.CfgHudShow.Value, L("Show song overlay", "Song-Anzeige einblenden"), Theme.BodyWidth - 130f);
            if (GUILayout.Button(L("Reset position", "Position zurücksetzen"), Theme.Button, GUILayout.Width(130), GUILayout.Height(30)))
            {
                hud.x = Theme.HudDefaultPos; hud.y = Theme.HudDefaultPos;
                P.CfgHudX.Value = hud.x; P.CfgHudY.Value = hud.y;
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(L("While this window is open, drag the overlay with the mouse.", "Solange dieses Fenster offen ist, kannst du die Anzeige mit der Maus verschieben."), Theme.Hint);

            GUILayout.Space(10); DrawLine(); GUILayout.Space(10);
            GUILayout.Label("Hotkeys", Theme.Hint);
            GUILayout.Space(4);
            DrawKeyRow(L("Open / close window", "Fenster öffnen / schließen"), P.CfgKey);
            DrawKeyRow(L("Accept request (host)", "Wunsch annehmen (Host)"), P.CfgAcceptKey);
            DrawKeyRow(L("Reject request (host)", "Wunsch ablehnen (Host)"), P.CfgRejectKey);
            if (keyMsg.Length > 0) GUILayout.Label(keyMsg, Theme.Hint);

            GUILayout.Space(6); DrawLine(); GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            string okText = "OK", missing = L("missing", "fehlt");
            GUILayout.Label("yt-dlp: " + (File.Exists(YtDlpPath()) ? okText : missing) + "    ffmpeg: " + (File.Exists(FfmpegPath()) ? okText : missing) + (toolsBusy ? L("    (installing ...)", "    (installiert ...)") : ""), Theme.Hint, GUILayout.Height(30));
            if (!toolsBusy && GUILayout.Button(ToolsOk() ? L("Update yt-dlp", "yt-dlp aktualisieren") : L("Install tools", "Tools installieren"), Theme.Button, GUILayout.Width(140), GUILayout.Height(30)))
            {
                forceToolUpdate = ToolsOk(); StartCoroutine(EnsureTools());
            }
            GUILayout.EndHorizontal();
        }

        void DrawKeyRow(string label, ConfigEntry<KeyCode> entry)
        {
            GUILayout.BeginHorizontal();
            bool waiting = rebind == entry;
            GUILayout.Label(label, Theme.Label, GUILayout.Height(30));
            if (!waiting) GUILayout.Label(entry.Value.ToString(), Theme.HintRight, GUILayout.Width(70), GUILayout.Height(30));
            GUILayout.Space(8);
            if (GUILayout.Button(waiting ? L("Press a key (Esc = cancel)", "Taste drücken (Esc = Abbruch)") : L("Change", "Ändern"), Theme.Button, GUILayout.Width(waiting ? 200 : 80), GUILayout.Height(30)))
            {
                rebind = waiting ? null : entry; rebindName = label; keyMsg = "";
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(4);
        }

        // ----- small controls -----
        float DrawSlider(float value, float min, float max)
        {
            Rect r = GUILayoutUtility.GetRect(0f, 18f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                Rect track = new Rect(r.x, r.y + 6f, r.width, 6f);
                GUI.DrawTexture(track, Theme.SliderBack);
                float fill = (value - min) / (max - min) * r.width;
                if (fill >= 6f) GUI.DrawTexture(new Rect(track.x, track.y, fill, 6f), Theme.BarFill);
            }
            return GUI.HorizontalSlider(r, value, min, max, Theme.Slider, Theme.SliderThumb);
        }

        bool DrawCheck(bool value, string text, float width)
        {
            Rect r = GUILayoutUtility.GetRect(width, 18f, GUILayout.Width(width));
            bool v = GUI.Toggle(r, value, text, Theme.Check);
            if (Event.current.type == EventType.Repaint) GUI.DrawTexture(new Rect(r.x, r.y + 1f, 16f, 16f), v ? Theme.CheckOn : Theme.CheckOff);
            return v;
        }

        void DrawLine()
        {
            Rect r = GUILayoutUtility.GetRect(0f, 1f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint) Paint(r, new Color32(0x2a, 0x2e, 0x35, 0xff));
        }

        void Paint(Rect r, Color32 c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Theme.White);
            GUI.color = old;
        }

        // Called from OnGUI: grabs the next key press while a hotkey is being re-assigned.
        void CaptureRebind()
        {
            Event e = Event.current;
            if (rebind == null || e == null || e.type != EventType.KeyDown || e.keyCode == KeyCode.None) return;
            KeyCode k = e.keyCode;
            e.Use();
            if (k == KeyCode.Escape) { rebind = null; keyMsg = L("Cancelled.", "Abgebrochen."); return; }
            string ks = k.ToString();
            bool typing = (k >= KeyCode.A && k <= KeyCode.Z) || (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9) ||
                          k == KeyCode.Space || k == KeyCode.Return || k == KeyCode.KeypadEnter || k == KeyCode.Backspace || ks.StartsWith("Mouse");
            if (typing) { keyMsg = "'" + ks + L("' would trigger while you type a link. Pick an F-key, Insert, Home, End, PageUp/Down, ...", "' würde beim Tippen eines Links auslösen. Nimm eine F-Taste, Einfg, Pos1, Ende, Bild hoch/runter, ..."); return; }
            ConfigEntry<KeyCode>[] all = { P.CfgKey, P.CfgAcceptKey, P.CfgRejectKey };
            foreach (ConfigEntry<KeyCode> o in all) if (o != rebind && o.Value == k) { keyMsg = ks + L(" is already used by another LinkRadio hotkey.", " ist schon mit einem anderen LinkRadio-Hotkey belegt."); return; }
            rebind.Value = k;
            keyMsg = rebindName + L(" is now ", " ist jetzt ") + k + L(" (saved).", " (gespeichert).");
            rebind = null;
        }
    }
}
