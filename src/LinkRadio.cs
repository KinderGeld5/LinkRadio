// LinkRadio for Nomad Drive (BepInEx 5 / BepInExPack_NomadDrive)
// Play YouTube / YouTube Music / Spotify-track links through a radio or boombox, synchronised over Mirror.
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Mirror;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Networking;

namespace LinkRadio
{
    public struct LinkRadioMsg : NetworkMessage
    {
        public byte Op;        // 1 play, 2 stop, 3 pause, 4 resume, 10 hello, 11 play now, 12 stop, 13 pause, 14 resume, 15 status text, 16 enqueue request, 17 skip, 18 queue info, 21 prefetch hint, 5 go (start moment after everybody is ready), 19 client ready report
        public int Seq;
        public uint Speaker;   // netId of radio/boombox, 0 = global
        public double Start;   // NetworkTime when position 0 is/was reached
        public float Pos;
        public float Range;
        public bool Loop;
        public string Url;
        public string Title;
    }

    [BepInPlugin(Guid, "LinkRadio", Version)]
    [BepInDependency("ramses.nomaddrive.boombox", BepInDependency.DependencyFlags.SoftDependency)]
    public class LinkRadioPlugin : BaseUnityPlugin
    {
        public const string Guid = "linkradio.nomaddrive";
        public const string Version = "1.0.0";

        internal static LinkRadioPlugin I;
        internal static ManualLogSource L;

        internal ConfigEntry<KeyCode> CfgKey, CfgAcceptKey, CfgRejectKey;
        internal ConfigEntry<bool> CfgAllowRequests, CfgAutoAccept;
        internal ConfigEntry<int> CfgMaxQueue;
        internal ConfigEntry<string> CfgName;
        internal ConfigEntry<bool> CfgClientControl, CfgSendHello, CfgUseMixer, CfgAutoTools;
        internal ConfigEntry<float> CfgVolume, CfgDelay, CfgMaxPick, CfgDefaultRange, CfgReadyTimeout;
        internal ConfigEntry<int> CfgMaxMinutes, CfgCacheMB, CfgMaxPlaylist, CfgPrefetch;
        internal ConfigEntry<string> CfgYtDlp, CfgFfmpeg, CfgAllowedHosts, CfgCookiesBrowser, CfgCookiesFile;
        internal ConfigEntry<string> CfgLanguage;
        internal ConfigEntry<bool> CfgHudShow;
        internal ConfigEntry<float> CfgHudX, CfgHudY;

        internal string PluginDir, ToolsDir, CacheDir;

        void Awake()
        {
            I = this; L = Logger;
            CfgKey = Config.Bind("General", "OpenKey", KeyCode.F7, "Key that opens the LinkRadio window.");
            CfgLanguage = Config.Bind("General", "Language", Application.systemLanguage == SystemLanguage.German ? "de" : "en", new ConfigDescription("Menu language.", new AcceptableValueList<string>("en", "de")));
            CfgHudShow = Config.Bind("Overlay", "ShowOverlay", true, "Show the current and the next song on screen.");
            CfgHudX = Config.Bind("Overlay", "PositionX", Theme.HudDefaultPos, "Overlay position. Drag it with the mouse while the LinkRadio window is open.");
            CfgHudY = Config.Bind("Overlay", "PositionY", Theme.HudDefaultPos, "Overlay position. Drag it with the mouse while the LinkRadio window is open.");
            CfgVolume = Config.Bind("General", "Volume", 0.8f, new ConfigDescription("Your personal volume (0..1).", new AcceptableValueRange<float>(0f, 1f)));
            CfgUseMixer = Config.Bind("General", "UseGameVolume", true, "Route the sound through the game's radio mixer group so the game's volume sliders apply.");
            CfgDefaultRange = Config.Bind("General", "DefaultRange", 40f, "Default audible distance (metres) when the music comes from a radio / boombox.");
            CfgMaxPick = Config.Bind("General", "SpeakerPickDistance", 12f, "A radio / boombox closer than this (metres) to you becomes the speaker. Otherwise the music is global (2D).");
            CfgClientControl = Config.Bind("Multiplayer", "ClientsCanControl", false, "HOST SETTING: allow other players (who have this mod) to start / stop music. Default: only the host controls the radio.");
            CfgAllowRequests = Config.Bind("Multiplayer", "AllowClientRequests", true, "HOST SETTING: clients can request songs. They land in a list that you accept or reject.");
            CfgAutoAccept = Config.Bind("Multiplayer", "AutoAcceptRequests", false, "HOST SETTING: put requested songs into the queue without asking.");
            CfgMaxQueue = Config.Bind("Multiplayer", "MaxQueue", 20, "HOST SETTING: maximum songs in queue + waiting requests.");
            CfgAcceptKey = Config.Bind("General", "AcceptKey", KeyCode.F9, "Host: accept the first waiting song request.");
            CfgRejectKey = Config.Bind("General", "RejectKey", KeyCode.F10, "Host: reject the first waiting song request.");
            CfgName = Config.Bind("Multiplayer", "DisplayName", "", "Name shown to the host on your requests (empty = Steam name).");
            CfgCookiesBrowser = Config.Bind("Download", "CookiesFromBrowser", "", "Fixes 'Sign in to confirm you're not a bot'. Browser yt-dlp reads YouTube cookies from: firefox (recommended), edge, chrome, brave, opera ... Optional profile: 'firefox:ProfileName'. Empty = off.");
            CfgCookiesFile = Config.Bind("Download", "CookiesFile", "", "Alternative: full path to an exported cookies.txt (Netscape format). Empty = off.");
            CfgSendHello = Config.Bind("Multiplayer", "AnnounceToHost", true, "Send a small hello message to the host so you receive the music. Set to false if you join servers WITHOUT this mod (Mirror kicks clients on unknown messages).");
            CfgReadyTimeout = Config.Bind("Multiplayer", "WaitForEveryoneSeconds", 180f, "Host: a new track starts for everybody at the same moment once ALL players have downloaded it. Maximum seconds to wait for slow / failed players before starting anyway.");
            CfgDelay = Config.Bind("Multiplayer", "StartDelaySeconds", 4f, "Only used offline. In multiplayer the host waits until everybody is ready (see WaitForEveryoneSeconds).");
            CfgAllowedHosts = Config.Bind("Security", "AllowedHosts", "youtube.com,youtu.be,youtube-nocookie.com", "Comma separated list of hosts that links may point to. Applies to everything received from the host, because your PC runs yt-dlp on it.");
            CfgMaxPlaylist = Config.Bind("Playlist", "MaxPlaylistSongs", 50, "Maximum number of songs taken from one YouTube playlist link.");
            CfgPrefetch = Config.Bind("Playlist", "PrefetchAhead", 3, "Every player downloads the next N queued songs in the background while the current one plays, so playlist songs follow without waiting. 0 = off. Use a big number (e.g. 50) to download a whole playlist up front (needs disk space, see CacheLimitMB).");
            CfgMaxMinutes = Config.Bind("Download", "MaxMinutes", 15, "Longer videos / live streams are refused.");
            CfgCacheMB = Config.Bind("Download", "CacheLimitMB", 600, "Downloaded tracks are cached; oldest are deleted above this size.");
            CfgAutoTools = Config.Bind("Download", "AutoDownloadTools", true, "Download yt-dlp and ffmpeg automatically (GitHub) into the plugin's tools folder on first use.");
            CfgYtDlp = Config.Bind("Download", "YtDlpPath", "", "Optional full path to yt-dlp.exe (empty = tools folder, then PATH).");
            CfgFfmpeg = Config.Bind("Download", "FfmpegPath", "", "Optional full path to ffmpeg.exe (empty = tools folder, then PATH).");

            PluginDir = Path.GetDirectoryName(Info.Location);
            ToolsDir = Path.Combine(PluginDir, "tools");
            CacheDir = Path.Combine(Paths.CachePath, "LinkRadio");
            try { Directory.CreateDirectory(ToolsDir); Directory.CreateDirectory(CacheDir); } catch (Exception e) { L.LogWarning("Could not create folders: " + e.Message); }

            // Mirror custom message serializers (no weaver needed).
            Writer<LinkRadioMsg>.write = (w, m) =>
            {
                w.WriteByte(m.Op); w.WriteInt(m.Seq); w.WriteUInt(m.Speaker); w.WriteDouble(m.Start);
                w.WriteFloat(m.Pos); w.WriteFloat(m.Range); w.WriteBool(m.Loop); w.WriteString(m.Url); w.WriteString(m.Title);
            };
            Reader<LinkRadioMsg>.read = r =>
            {
                LinkRadioMsg m = new LinkRadioMsg();
                m.Op = r.ReadByte(); m.Seq = r.ReadInt(); m.Speaker = r.ReadUInt(); m.Start = r.ReadDouble();
                m.Pos = r.ReadFloat(); m.Range = r.ReadFloat(); m.Loop = r.ReadBool(); m.Url = r.ReadString(); m.Title = r.ReadString();
                return m;
            };

            ThreadPool.QueueUserWorkItem(delegate { CleanCache(); });

            GameObject go = new GameObject("LinkRadioRunner");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            go.AddComponent<LinkRadioRunner>();
            L.LogInfo("LinkRadio " + Version + " loaded. Press " + CfgKey.Value + " in game.");
        }

        void CleanCache()
        {
            try
            {
                DirectoryInfo di = new DirectoryInfo(CacheDir);
                if (!di.Exists) return;
                List<FileInfo> files = new List<FileInfo>(di.GetFiles());
                foreach (FileInfo f in files.ToArray())
                {
                    if (f.Extension == ".part" || (DateTime.UtcNow - f.LastWriteTimeUtc).TotalDays > 14) { try { f.Delete(); files.Remove(f); } catch { } }
                }
                files.Sort((a, b) => a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));
                long total = 0; foreach (FileInfo f in files) total += f.Length;
                long limit = (long)CfgCacheMB.Value * 1024 * 1024;
                int i = 0;
                while (total > limit && i < files.Count) { total -= files[i].Length; try { files[i].Delete(); } catch { } i++; }
            }
            catch (Exception e) { L.LogWarning("Cache cleanup: " + e.Message); }
        }
    }

    internal class ProcResult { public int Code = -1; public string Out = ""; public string Err = ""; public bool TimedOut; }
    internal class Job { public volatile bool Done; public ProcResult Res; public string Extra; }

    internal class Track
    {
        public int Seq; public string Url, Key, Title; public double Start; public bool Loop; public uint Speaker; public float Range;
        public AudioClip Clip; public bool Paused; public float PausePos; public bool Failed; public bool Gated, Reported;
    }

    internal partial class LinkRadioRunner : MonoBehaviour
    {
        const string YtDlpUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
        const string DenoZipUrl = "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip";
        const string FfmpegZipUrl = "https://github.com/yt-dlp/FFmpeg-Builds/releases/latest/download/ffmpeg-master-latest-win64-gpl.zip";

        LinkRadioPlugin P { get { return LinkRadioPlugin.I; } }
        readonly ConcurrentQueue<Action> mainQueue = new ConcurrentQueue<Action>();

        // ---- UI state ----
        ConfigEntry<KeyCode> rebind; string rebindName = ""; string keyMsg = "";
        bool open; Rect win = new Rect(60, 80, Theme.Width, 520);
        string url = ""; bool loop; float range = 0f; bool muted;
        string status = "Ready."; bool statusBad;
        string lastSpeaker = "-";
        CursorLockMode prevLock; bool prevVis;
        object ams; bool weSuspended;

        // ---- tools ----
        const double FarFuture = 1e9;
        class Gate { public int Seq; public HashSet<int> Expected = new HashSet<int>(), Done = new HashSet<int>(); public float Deadline, Since; }
        Gate srvGate;
        bool denoTried; bool jsRtBad;
        bool toolsBusy; string toolsMsg = ""; bool forceToolUpdate;

        // ---- network ----
        bool srvReg, cliReg, helloSent; float helloAt;
        readonly HashSet<int> modded = new HashSet<int>();
        LinkRadioMsg srvPlay; bool srvHasPlay, srvPaused; float srvPausePos; int srvSeq;
        int lastClientSeq = -1;
        // ---- queue / requests ----
        class QItem { public string Url, Who, Title; public uint Speaker; public float Range, Seconds; public bool Loop; public int ConnId = -1; public int Id; public List<string[]> Subs; }
        readonly List<QItem> pending = new List<QItem>(), queue = new List<QItem>();
        readonly Dictionary<int, float> lastReq = new Dictionary<int, float>();
        int nextQId, srvTrackSeq; string queueInfo = ""; float nextSeconds; bool queueHold; float nextQueueCheck, srvTrackWatch; string myName;

        // ---- playback ----
        Track cur; GameObject srcGo; AudioSource src; Transform anchor; float lastCorr, nextAnchorCheck, nextMixerCheck;
        AudioMixerGroup mixer;

        // =====================================================================
        void Start()
        {
            range = P.CfgDefaultRange.Value; status = L("Ready.", "Bereit.");
            hud = new Rect(P.CfgHudX.Value, P.CfgHudY.Value, Theme.HudWidth, Theme.HudNowHeight);
            StartCoroutine(CoPrefetchLoop());
        }

        void Update()
        {
            Action a; int n = 0;
            while (n++ < 50 && mainQueue.TryDequeue(out a)) { try { a(); } catch (Exception e) { LinkRadioPlugin.L.LogError(e); } }

            if (rebind == null && KeyDown(P.CfgKey.Value)) SetOpen(!open);
            NetTick();
            if (IsHostLike() && pending.Count > 0 && rebind == null)
            {
                if (KeyDown(P.CfgAcceptKey.Value)) AcceptPending(0);
                else if (KeyDown(P.CfgRejectKey.Value)) RejectPending(0);
            }
            QueueTick();
            TickPlayback();
            if (P.CfgUseMixer.Value && mixer == null && Time.unscaledTime > nextMixerCheck) { nextMixerCheck = Time.unscaledTime + 5f; FindMixer(); }
        }

        void LateUpdate()
        {
            if (open) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }

        void OnDestroy() { if (open) SetOpen(false); }

        // =====================================================================
        // Input helpers
        static bool KeyDown(KeyCode k)
        {
            try
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null)
                {
                    UnityEngine.InputSystem.Key key;
                    if (Enum.TryParse(k.ToString(), out key)) return kb[key].wasPressedThisFrame;
                }
            }
            catch { }
            try { return Input.GetKeyDown(k); } catch { return false; }
        }

        void SetOpen(bool v)
        {
            if (v == open) return;
            open = v;
            if (v)
            {
                prevLock = Cursor.lockState; prevVis = Cursor.visible;
                SuspendInput(true);
            }
            else
            {
                rebind = null;
                SuspendInput(false);
                Cursor.lockState = prevLock; Cursor.visible = prevVis;
                GUIUtility.keyboardControl = 0;
            }
        }

        void SuspendInput(bool on)
        {
            try
            {
                if (on)
                {
                    Type t = Util.FindType("NomadDrive.Features.Inputs.ActionMapsManager");
                    if (t == null || !typeof(UnityEngine.Object).IsAssignableFrom(t)) return;
                    UnityEngine.Object o = UnityEngine.Object.FindObjectOfType(t);
                    if (o == null) return;
                    PropertyInfo prop = t.GetProperty("IsGameplayMapsSuspended");
                    if (prop != null && (bool)prop.GetValue(o, null)) return;
                    t.GetMethod("SuspendGameplayMaps").Invoke(o, null);
                    ams = o; weSuspended = true;
                }
                else if (weSuspended)
                {
                    weSuspended = false;
                    if (ams != null) ams.GetType().GetMethod("ResumeGameplayMaps").Invoke(ams, null);
                    ams = null;
                }
            }
            catch (Exception e) { LinkRadioPlugin.L.LogWarning("Input suspend: " + e.Message); }
        }

        // =====================================================================
        // Networking
        void NetTick()
        {
            bool srv = NetworkServer.active;
            if (srv && !srvReg)
            {
                srvReg = true; modded.Clear();
                NetworkServer.RegisterHandler<LinkRadioMsg>(OnServerMsg, true);
                if (NetworkClient.active && NetworkServer.localConnection != null) modded.Add(NetworkServer.localConnection.connectionId);
            }
            else if (!srv && srvReg) { srvReg = false; modded.Clear(); srvHasPlay = false; srvPaused = false; pending.Clear(); queue.Clear(); queueInfo = ""; lastReq.Clear(); }
            if (srv && srvReg && NetworkClient.active && NetworkServer.localConnection != null) modded.Add(NetworkServer.localConnection.connectionId);

            bool cli = NetworkClient.active;
            if (cli && !cliReg)
            {
                cliReg = true; helloSent = false; helloAt = Time.unscaledTime + 3f; lastClientSeq = -1;
                NetworkClient.RegisterHandler<LinkRadioMsg>(OnClientMsg, false);
            }
            else if (!cli && cliReg) { cliReg = false; StopLocal("Disconnected."); }

            if (cliReg && !helloSent && !NetworkServer.active && P.CfgSendHello.Value && NetworkClient.isConnected && Time.unscaledTime > helloAt)
            {
                if (NetworkClient.connection != null && NetworkClient.connection.isAuthenticated)
                {
                    helloSent = true;
                    NetworkClient.Send(new LinkRadioMsg { Op = 10, Url = "", Title = "" });
                }
            }
        }

        void OnServerMsg(NetworkConnectionToClient conn, LinkRadioMsg m)
        {
            if (conn == null) return;
            if (m.Op == 10)
            {
                modded.Add(conn.connectionId);
                if (srvHasPlay)
                {
                    conn.Send(srvPlay);
                    if (srvPaused) conn.Send(new LinkRadioMsg { Op = 3, Seq = srvPlay.Seq, Pos = srvPausePos, Url = "", Title = "" });
                }
                conn.Send(new LinkRadioMsg { Op = 18, Url = "", Title = queueInfo ?? "", Pos = nextSeconds });
                return;
            }
            if (!modded.Contains(conn.connectionId)) return;
            if (m.Op == 19) { GateReport(conn.connectionId, m.Seq); return; }
            if (m.Op == 16) { HandleEnqueue(m, conn); return; }
            if ((m.Op >= 11 && m.Op <= 14) || m.Op == 17)
            {
                if (!P.CfgClientControl.Value)
                {
                    conn.Send(new LinkRadioMsg { Op = 15, Url = "", Title = L("Only the host can control the radio. Use 'Request song' instead.", "Nur der Host kann das Radio steuern. Nutze stattdessen 'Song wünschen'.") });
                    return;
                }
                HandleRequest(m, conn);
            }
        }

        void OnClientMsg(LinkRadioMsg m)
        {
            if (m.Op == 15) { SetStatus(Util.Short(m.Title, 140), true); return; }
            if (m.Op == 18) { queueInfo = m.Title ?? ""; nextSeconds = m.Pos; return; }
            if (m.Op == 21) { SetPrefetch(m.Url); return; }
            if (m.Op >= 10) return;
            if (m.Seq < lastClientSeq && m.Op != 3) return;
            lastClientSeq = Math.Max(lastClientSeq, m.Seq);
            ApplyClient(m);
        }

        bool IsHostLike() { return NetworkServer.active || !NetworkClient.isConnected; }

        // Entry for UI actions (any role)
        void Dispatch(LinkRadioMsg m)
        {
            if (IsHostLike()) HandleRequest(m, null);
            else NetworkClient.Send(m);
        }

        // Runs on the server / host (or offline). conn == null -> local player.
        void HandleRequest(LinkRadioMsg m, NetworkConnectionToClient conn)
        {
            switch (m.Op)
            {
                case 11: StartCoroutine(CoStartPlay(m, conn)); break;
                case 16: HandleEnqueue(m, conn); break;
                case 12:
                    srvGate = null; queueHold = true; srvHasPlay = false; srvPaused = false;
                    Broadcast(new LinkRadioMsg { Op = 2, Url = "", Title = "" });
                    break;
                case 17:
                    srvGate = null; queueHold = false;
                    if (queue.Count > 0) PlayNext();
                    else { srvHasPlay = false; srvPaused = false; Broadcast(new LinkRadioMsg { Op = 2, Url = "", Title = "" }); }
                    break;
                case 13:
                    if (srvHasPlay && !srvPaused && srvGate == null)
                    {
                        srvPausePos = (float)Math.Max(0, NetworkTime.time - srvPlay.Start); srvPaused = true;
                        Broadcast(new LinkRadioMsg { Op = 3, Pos = srvPausePos, Url = "", Title = "" });
                    }
                    break;
                case 14:
                    if (srvHasPlay && srvPaused)
                    {
                        srvPaused = false; srvPlay.Start = NetworkTime.time - srvPausePos;
                        Broadcast(new LinkRadioMsg { Op = 4, Start = srvPlay.Start, Url = "", Title = "" });
                    }
                    break;
            }
        }

        void Broadcast(LinkRadioMsg m)
        {
            m.Seq = ++srvSeq;
            if (m.Url == null) m.Url = ""; if (m.Title == null) m.Title = "";
            if (m.Op == 1) { srvPlay = m; srvTrackSeq = m.Seq; srvHasPlay = true; srvPaused = false; }
            else if (m.Op == 4 || m.Op == 3) { srvPlay.Seq = m.Seq; }
            else if (m.Op == 5) { srvPlay.Seq = m.Seq; srvPlay.Start = m.Start; }
            if (NetworkServer.active)
            {
                List<int> dead = null;
                foreach (int id in modded)
                {
                    NetworkConnectionToClient c;
                    if (NetworkServer.connections.TryGetValue(id, out c) && c != null) c.Send(m);
                    else { if (dead == null) dead = new List<int>(); dead.Add(id); }
                }
                if (dead != null) foreach (int id in dead) modded.Remove(id);
            }
            else ApplyClient(m); // offline: local only
        }

        void ReportTo(NetworkConnectionToClient conn, string text, bool bad)
        {
            if (conn != null) conn.Send(new LinkRadioMsg { Op = 15, Url = "", Title = text });
            else SetStatus(text, bad);
        }

        void NotifyConn(int connId, string text)
        {
            NetworkConnectionToClient c;
            if (connId >= 0 && NetworkServer.active && NetworkServer.connections.TryGetValue(connId, out c) && c != null)
                c.Send(new LinkRadioMsg { Op = 15, Url = "", Title = text });
        }

        // ---------------- queue / requests (server side) ----------------
        string MyName()
        {
            string c = P.CfgName.Value;
            if (!string.IsNullOrEmpty(c)) return Util.Short(Util.CleanQuery(c), 24);
            if (myName == null)
            {
                myName = "Player";
                try
                {
                    Type t = Util.FindType("Steamworks.SteamClient");
                    if (t != null)
                    {
                        PropertyInfo p = t.GetProperty("Name", BindingFlags.Public | BindingFlags.Static);
                        string n = p != null ? p.GetValue(null, null) as string : null;
                        if (!string.IsNullOrEmpty(n)) myName = Util.Short(Util.CleanQuery(n), 24);
                    }
                }
                catch { }
            }
            return myName;
        }

        void HandleEnqueue(LinkRadioMsg m, NetworkConnectionToClient conn)
        {
            string who;
            if (conn == null) who = MyName();
            else
            {
                if (!P.CfgAllowRequests.Value) { ReportTo(conn, L("The host does not accept song requests.", "Der Host nimmt keine Songwünsche an."), true); return; }
                who = string.IsNullOrEmpty(m.Title) ? "Player " + conn.connectionId : Util.Short(Util.CleanQuery(m.Title), 24);
                float last;
                if (lastReq.TryGetValue(conn.connectionId, out last) && Time.unscaledTime - last < 3f) { ReportTo(conn, L("Slow down a bit.", "Nicht so schnell."), true); return; }
                lastReq[conn.connectionId] = Time.unscaledTime;
                int mine = 0; foreach (QItem p in pending) if (p.ConnId == conn.connectionId) mine++;
                if (mine >= 3) { ReportTo(conn, L("You already have 3 requests waiting for the host.", "Du hast schon 3 offene Wünsche beim Host."), true); return; }
            }
            if (queue.Count + pending.Count >= Math.Max(1, P.CfgMaxQueue.Value)) { ReportTo(conn, L("The queue is full.", "Die Warteschlange ist voll."), true); return; }
            StartCoroutine(CoEnqueue(m, conn, who));
        }

        IEnumerator CoEnqueue(LinkRadioMsg m, NetworkConnectionToClient conn, string who)
        {
            string plId;
            if (TryPlaylist(m.Url, out plId)) { yield return CoEnqueuePlaylist(m, conn, who, plId); yield break; }
            string u = null, t = null, e = null;
            yield return CoResolveAny(m.Url, conn, (a, b, c) => { u = a; t = b; e = c; });
            if (e != null) { ReportTo(conn, e, true); yield break; }
            QItem it = new QItem
            {
                Url = u, Title = t, Who = who, Speaker = m.Speaker, Loop = false,
                Range = Mathf.Clamp(m.Range <= 0 ? P.CfgDefaultRange.Value : m.Range, 8f, 120f),
                ConnId = conn == null ? -1 : conn.connectionId, Id = ++nextQId
            };
            bool accept = conn == null || P.CfgAutoAccept.Value;
            if (accept) AddToQueue(it);
            else
            {
                pending.Add(it);
                NotifyConn(it.ConnId, L("Request sent. Waiting for the host to accept ...", "Wunsch gesendet. Warte, bis der Host annimmt ..."));
                SetStatus(L("Song request from ", "Songwunsch von ") + who + KeyHint(), false);
            }
            StartCoroutine(CoFetchInfo(it));
        }

        // Title (if still unknown) and length of a queued song.
        IEnumerator CoFetchInfo(QItem it)
        {
            yield return EnsureTools();
            if (!File.Exists(YtDlpPath())) yield break;
            Job job = new Job(); string exe = YtDlpPath(); string url = it.Url;
            const string print = "--print \"%(duration)s|||%(title)s\" -- \"";
            string aCk = "--no-warnings --no-colors --ignore-config --no-playlist --socket-timeout 15 " + ExtraYtArgs(false) + print + url + "\"";
            ThreadPool.QueueUserWorkItem(delegate
            {
                job.Res = Run(exe, aCk, 45000);
                if (IsCookieErr(job.Res.Err))
                {
                    cookieProblem = LastErrorLine(job.Res.Err);
                    job.Res = Run(exe, "--no-warnings --no-colors --ignore-config --no-playlist --socket-timeout 15 " + print + url + "\"", 45000);
                }
                job.Done = true;
            });
            while (!job.Done) yield return null;
            foreach (string line in (job.Res.Out ?? "").Split('\n'))
            {
                string[] parts = line.Trim().Split(new string[] { "|||" }, 2, StringSplitOptions.None);
                if (parts.Length < 2) continue;
                it.Seconds = Util.ParseSeconds(parts[0]);
                if (string.IsNullOrEmpty(it.Title) && parts[1].Length > 0) it.Title = Util.Short(parts[1], 80);
                SendQueueInfo();
                break;
            }
        }

        // ---- playlists ----
        bool TryPlaylist(string raw, out string plId)
        {
            plId = null; Uri u;
            if (!Util.TryParseHttpUrl((raw ?? "").Trim(), out u) || !Util.HostAllowed(u, P.CfgAllowedHosts.Value)) return false;
            plId = Util.YouTubePlaylistId(u);
            return plId != null;
        }

        QItem MakeSubItem(string[] e, string who, uint speaker, float range)
        {
            return new QItem { Url = "https://www.youtube.com/watch?v=" + e[0], Title = e[1], Seconds = Util.ParseSeconds(e[2]), Who = who, Speaker = speaker, Range = range, Loop = false, ConnId = -1, Id = ++nextQId };
        }

        // done(playlistTitle, entries{id,title,seconds}, error)
        IEnumerator CoReadPlaylist(string plId, Action<string, List<string[]>, string> done)
        {
            yield return EnsureTools();
            if (!File.Exists(YtDlpPath())) { done(null, null, "yt-dlp is missing: " + toolsMsg); yield break; }
            int max = Mathf.Clamp(P.CfgMaxPlaylist.Value, 1, 200);
            string pl = "https://www.youtube.com/playlist?list=" + plId;
            string baseArgs = "--flat-playlist --no-warnings --no-colors --ignore-config --socket-timeout 30 --playlist-end " + max + " ";
            string tail = "--print \"%(playlist_title)s|||%(id)s|||%(duration)s|||%(title)s\" -- \"" + pl + "\"";
            string aCk = baseArgs + ExtraYtArgs(false) + tail, aNo = baseArgs + tail;
            Job job = new Job(); string exe = YtDlpPath();
            ThreadPool.QueueUserWorkItem(delegate
            {
                job.Res = Run(exe, aCk, 90000);
                if (IsCookieErr(job.Res.Err)) { cookieProblem = LastErrorLine(job.Res.Err); job.Res = Run(exe, aNo, 90000); }
                job.Done = true;
            });
            while (!job.Done) yield return null;
            string name = null; List<string[]> list = new List<string[]>();
            System.Text.RegularExpressions.Regex idRx = new System.Text.RegularExpressions.Regex("^[A-Za-z0-9_-]{11}$");
            foreach (string line in (job.Res.Out ?? "").Split('\n'))
            {
                string[] parts = line.Trim().Split(new string[] { "|||" }, 4, StringSplitOptions.None);
                if (parts.Length < 3 || !idRx.IsMatch(parts[1])) continue;
                if (name == null) name = Util.Short(Util.CleanQuery(parts[0]), 40);
                string title = parts.Length > 3 ? Util.Short(Util.CleanQuery(parts[3]), 80) : "";
                if (title == "NA" || title.Length == 0) title = null;
                list.Add(new string[] { parts[1], title, parts[2] });
            }
            if (list.Count == 0)
            {
                string why = IsBotBlock(job.Res.Err) ? "YouTube blocked this PC (bot check) - see README (cookies)." : LastErrorLine(job.Res.Err);
                done(null, null, L("Could not read the playlist (private, empty or blocked?) ", "Playlist nicht lesbar (privat, leer oder gesperrt?) ") + Util.Short(why, 100)); yield break;
            }
            if (string.IsNullOrEmpty(name) || name == "NA") name = "Playlist";
            done(name, list, null);
        }

        IEnumerator CoEnqueuePlaylist(LinkRadioMsg m, NetworkConnectionToClient conn, string who, string plId)
        {
            ReportTo(conn, L("Reading playlist ...", "Playlist wird gelesen ..."), false);
            string name = null, err = null; List<string[]> list = null;
            yield return CoReadPlaylist(plId, (a, b, c) => { name = a; list = b; err = c; });
            if (err != null) { ReportTo(conn, err, true); yield break; }
            QItem it = new QItem
            {
                Url = "https://www.youtube.com/playlist?list=" + plId, Title = "[Playlist] " + name + " (" + list.Count + " songs)", Subs = list,
                Who = who, Speaker = m.Speaker, Loop = false,
                Range = Mathf.Clamp(m.Range <= 0 ? P.CfgDefaultRange.Value : m.Range, 8f, 120f),
                ConnId = conn == null ? -1 : conn.connectionId, Id = ++nextQId
            };
            bool accept = conn == null || P.CfgAutoAccept.Value;
            if (accept) AddToQueue(it);
            else
            {
                pending.Add(it);
                NotifyConn(it.ConnId, L("Playlist request sent (", "Playlist-Wunsch gesendet (") + list.Count + L(" songs). Waiting for the host to accept ...", " Songs). Warte, bis der Host annimmt ..."));
                SetStatus(L("Playlist request from ", "Playlist-Wunsch von ") + who + KeyHint(), false);
            }
        }

        void AddToQueue(QItem it)
        {
            if (it.Subs != null)
            {
                foreach (string[] e in it.Subs) queue.Add(MakeSubItem(e, it.Who, it.Speaker, it.Range));
                queueHold = false;
                SendQueueInfo();
                NotifyConn(it.ConnId, L("Your playlist was added to the queue (", "Deine Playlist ist in der Warteschlange (") + it.Subs.Count + L(" songs).", " Songs)."));
                SetStatus(L("Playlist added: ", "Playlist hinzugefügt: ") + it.Subs.Count + L(" songs.", " Songs."), false);
                if (!srvHasPlay) { nextQueueCheck = 0f; }
                return;
            }
            queue.Add(it); queueHold = false;
            SendQueueInfo();
            NotifyConn(it.ConnId, L("Your song was added to the queue (position ", "Dein Song ist in der Warteschlange (Platz ") + queue.Count + ").");
            if (!srvHasPlay) { nextQueueCheck = 0f; }
        }

        void AcceptPending(int idx)
        {
            if (idx < 0 || idx >= pending.Count) return;
            QItem it = pending[idx]; pending.RemoveAt(idx);
            AddToQueue(it);
            SetStatus(L("Accepted: ", "Angenommen: ") + (it.Title ?? Util.Short(it.Url, 40)) + " (" + it.Who + ")", false);
        }

        void RejectPending(int idx)
        {
            if (idx < 0 || idx >= pending.Count) return;
            QItem it = pending[idx]; pending.RemoveAt(idx);
            NotifyConn(it.ConnId, L("The host declined your request.", "Der Host hat deinen Wunsch abgelehnt."));
            SetStatus(L("Declined request from ", "Wunsch abgelehnt von ") + it.Who, false);
        }

        void PlayNext()
        {
            if (queue.Count == 0) return;
            QItem it = queue[0]; queue.RemoveAt(0);
            StartItem(it);
            NotifyConn(it.ConnId, L("Your song is playing now: ", "Dein Song läuft jetzt: ") + (it.Title ?? Util.Short(it.Url, 40)));
            SendQueueInfo();
        }

        void StartItem(QItem it)
        {
            uint sp = it.Speaker;
            if (sp != 0 && NetworkServer.active && !NetworkServer.spawned.ContainsKey(sp)) sp = 0;
            queueHold = false; srvTrackWatch = Time.unscaledTime;
            bool gated = NetworkServer.active;
            srvGate = null;
            Broadcast(new LinkRadioMsg
            {
                Op = 1, Url = it.Url, Title = it.Title ?? "", Speaker = sp, Loop = it.Loop,
                Range = Mathf.Clamp(it.Range <= 0 ? P.CfgDefaultRange.Value : it.Range, 8f, 120f),
                Start = gated ? FarFuture : NetworkTime.time + P.CfgDelay.Value
            });
            if (gated)
            {
                Gate g = new Gate { Seq = srvTrackSeq, Since = Time.unscaledTime, Deadline = Time.unscaledTime + Mathf.Max(15f, P.CfgReadyTimeout.Value) };
                foreach (int id in modded) g.Expected.Add(id);
                srvGate = g;
                SetStatus(L("Waiting until everybody has downloaded the song ...", "Warte, bis alle den Song geladen haben ..."), false);
            }
        }

        void SendQueueInfo()
        {
            StringBuilder sb = new StringBuilder();
            int n = Math.Min(queue.Count, 8);
            for (int i = 0; i < n; i++)
            {
                QItem it = queue[i];
                sb.Append(i + 1).Append(". ").Append(!string.IsNullOrEmpty(it.Title) ? it.Title : Util.Short(it.Url, 40)).Append("  [").Append(it.Who).Append("]\n");
            }
            if (queue.Count > n) sb.Append("+ ").Append(queue.Count - n).Append(" more");
            queueInfo = sb.ToString().TrimEnd();
            nextSeconds = queue.Count > 0 ? queue[0].Seconds : 0f;
            if (IsHostLike())
            {
                Broadcast(new LinkRadioMsg { Op = 18, Url = "", Title = queueInfo, Pos = nextSeconds });
                int pf = Mathf.Clamp(P.CfgPrefetch.Value, 0, 20);
                StringBuilder ps = new StringBuilder();
                for (int i = 0; i < queue.Count && i < pf; i++) { if (queue[i].Subs != null) continue; if (ps.Length > 0) ps.Append(' '); ps.Append(queue[i].Url); }
                Broadcast(new LinkRadioMsg { Op = 21, Url = ps.ToString(), Title = "" });
            }
        }

        // ---- prefetch: download the next queued songs in the background ----
        readonly List<string> prefetchUrls = new List<string>();
        readonly HashSet<string> prefetchBad = new HashSet<string>();
        string prefetchingKey;

        void SetPrefetch(string joined)
        {
            prefetchUrls.Clear();
            if (string.IsNullOrEmpty(joined)) return;
            foreach (string part in joined.Split(' '))
            {
                Uri u;
                if (part.Length == 0 || !Util.TryParseHttpUrl(part, out u) || !Util.HostAllowed(u, P.CfgAllowedHosts.Value)) continue;
                prefetchUrls.Add(Util.Normalize(u));
                if (prefetchUrls.Count >= 20) break;
            }
        }

        IEnumerator CoPrefetchLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(1.5f);
                if (P.CfgPrefetch.Value <= 0 || prefetchUrls.Count == 0 || toolsBusy || !ToolsOk()) continue;
                Track c = cur;
                if (c != null && c.Clip == null && !c.Failed) continue; // the current song is still loading - it has priority
                string url = null, key = null;
                foreach (string pu in prefetchUrls.ToArray())
                {
                    Uri uu; if (!Util.TryParseHttpUrl(pu, out uu)) continue;
                    string k = Util.KeyFor(uu);
                    if (File.Exists(Path.Combine(P.CacheDir, k + ".mp3")) || prefetchBad.Contains(k)) continue;
                    url = Util.Normalize(uu); key = k; break;
                }
                if (url == null) continue;
                prefetchingKey = key;
                Job job = new Job(); string exe = YtDlpPath();
                string a1 = BuildDownloadArgs(url, key, false);
                ThreadPool.QueueUserWorkItem(delegate
                {
                    job.Res = Run(exe, a1, 240000);
                    if (IsCookieErr(job.Res.Err)) { cookieProblem = LastErrorLine(job.Res.Err); job.Res = Run(exe, BuildDownloadArgs(url, key, false), 240000); }
                    job.Done = true;
                });
                while (!job.Done) yield return null;
                prefetchingKey = null;
                if (!File.Exists(Path.Combine(P.CacheDir, key + ".mp3")))
                {
                    prefetchBad.Add(key);
                    LinkRadioPlugin.L.LogWarning("Prefetch failed for " + url + ": " + LastErrorLine(job.Res.Err));
                }
            }
        }

        // ---- start barrier (host side): everybody downloads first, then the track starts for all at once
        void GateReport(int connId, int seq)
        {
            Gate g = srvGate;
            if (g == null || g.Seq != seq) return;
            g.Done.Add(connId);
            GateTick();
        }

        void GateTick()
        {
            Gate g = srvGate;
            if (g == null) return;
            if (!srvHasPlay || srvTrackSeq != g.Seq) { srvGate = null; return; }
            int waiting = 0;
            foreach (int id in g.Expected)
            {
                if (g.Done.Contains(id)) continue;
                NetworkConnectionToClient c;
                bool alive = NetworkServer.connections.TryGetValue(id, out c) && c != null;
                if (NetworkServer.localConnection != null && id == NetworkServer.localConnection.connectionId) alive = true;
                if (alive) waiting++;
            }
            if (waiting == 0) { GateRelease("ready"); return; }
            if (Time.unscaledTime > g.Deadline) { GateRelease("timeout"); return; }
            int total = g.Expected.Count;
            SetStatus(L("Waiting for everybody: ", "Warte auf alle: ") + (total - waiting) + "/" + total + L(" ready (", " bereit (") + (int)(Time.unscaledTime - g.Since) + L("s) - starts automatically", "s) - startet automatisch"), false);
        }

        void GateRelease(string why)
        {
            Gate g = srvGate; if (g == null) return;
            srvGate = null;
            LinkRadioPlugin.L.LogInfo("Start barrier released (" + why + "), " + g.Done.Count + "/" + g.Expected.Count + " ready.");
            Broadcast(new LinkRadioMsg { Op = 5, Start = NetworkTime.time + 1.5, Url = "", Title = "" });
        }

        // Client side: tell the host this track is downloaded (ok) or that it failed (either way the host stops waiting for us).
        void ReportReady(Track t)
        {
            if (t == null || !t.Gated || t.Reported) return;
            t.Reported = true;
            if (NetworkServer.active)
            {
                if (NetworkServer.localConnection != null) GateReport(NetworkServer.localConnection.connectionId, t.Seq);
            }
            else if (NetworkClient.isConnected) NetworkClient.Send(new LinkRadioMsg { Op = 19, Seq = t.Seq, Url = "", Title = "" });
        }

        // Host side: advance the queue when the current track has ended.
        void QueueTick()
        {
            if (!IsHostLike()) return;
            if (Time.unscaledTime < nextQueueCheck) return;
            nextQueueCheck = Time.unscaledTime + 0.5f;
            GateTick();
            if (srvHasPlay)
            {
                if (srvPaused) return;
                Track t = cur;
                if (t != null && t.Seq != srvTrackSeq) return; // our own start message has not arrived yet
                bool finished = false;
                if (t == null) { if (Time.unscaledTime - srvTrackWatch > 25f) finished = true; }
                else if (t.Failed) finished = true;
                else if (t.Clip != null) { if (!t.Loop && NetworkTime.time - t.Start >= t.Clip.length + 0.3) finished = true; }
                else if (Time.unscaledTime - srvTrackWatch > 300f) finished = true;
                if (finished)
                {
                    srvHasPlay = false;
                    if (queue.Count > 0) PlayNext();
                }
            }
            else if (!queueHold && queue.Count > 0) PlayNext();
        }

        // Resolves a user supplied link to a canonical YouTube URL (+ optional title).
        IEnumerator CoResolveAny(string raw, NetworkConnectionToClient conn, Action<string, string, string> done)
        {
            raw = raw == null ? "" : raw.Trim();
            string spot; bool spUnsupported;
            if (Util.TryParseSpotifyTrack(raw, out spot, out spUnsupported))
            {
                ReportTo(conn, L("Resolving Spotify track ...", "Spotify-Song wird gesucht ..."), false);
                string u = null, t = null, e = null;
                yield return CoResolveSpotify(spot, (a, b, c) => { u = a; t = b; e = c; });
                if (e == null) ReportTo(conn, "Spotify -> YouTube: " + t, false);
                else LinkRadioPlugin.L.LogWarning("Spotify resolve failed: " + e);
                done(u, t, e);
                yield break;
            }
            if (spUnsupported) { done(null, null, L("Only Spotify TRACK links are supported (no playlists / albums).", "Von Spotify gehen nur einzelne Songs (keine Playlists / Alben).")); yield break; }
            Uri uri;
            if (!Util.TryParseHttpUrl(raw, out uri)) { done(null, null, L("That is not a valid link.", "Das ist kein gültiger Link.")); yield break; }
            if (!Util.HostAllowed(uri, P.CfgAllowedHosts.Value)) { done(null, null, "Host '" + uri.Host + "' is not in AllowedHosts (config)."); yield break; }
            done(Util.Normalize(uri), null, null);
        }

        IEnumerator CoStartPlay(LinkRadioMsg m, NetworkConnectionToClient conn)
        {
            string plId;
            if (TryPlaylist(m.Url, out plId))
            {
                ReportTo(conn, L("Reading playlist ...", "Playlist wird gelesen ..."), false);
                string pn = null, perr = null; List<string[]> pl = null;
                yield return CoReadPlaylist(plId, (a, b, c) => { pn = a; pl = b; perr = c; });
                if (perr != null) { ReportTo(conn, perr, true); yield break; }
                string pwho = conn == null ? MyName() : "Player";
                float prange = Mathf.Clamp(m.Range <= 0 ? P.CfgDefaultRange.Value : m.Range, 8f, 120f);
                List<QItem> rest = new List<QItem>();
                for (int i = 1; i < pl.Count; i++) rest.Add(MakeSubItem(pl[i], pwho, m.Speaker, prange));
                queue.InsertRange(0, rest);
                StartItem(MakeSubItem(pl[0], pwho, m.Speaker, prange));
                SendQueueInfo();
                SetStatus("Playlist '" + pn + "': " + pl.Count + L(" songs.", " Songs."), false);
                yield break;
            }
            string u = null, t = null, e = null;
            yield return CoResolveAny(m.Url, conn, (a, b, c) => { u = a; t = b; e = c; });
            if (e != null) { ReportTo(conn, e, true); yield break; }
            StartItem(new QItem { Url = u, Title = t, Speaker = m.Speaker, Loop = m.Loop, Range = m.Range, Who = conn == null ? MyName() : "Player" });
        }

        // Simple GET; done(text, error)
        IEnumerator CoHttpGet(string url, Action<string, string> done)
        {
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36");
                req.SetRequestHeader("Accept-Language", "en-US,en;q=0.9");
                req.redirectLimit = 8;
                req.timeout = 20;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success) done(req.downloadHandler.text, null);
                else done(null, req.error + (req.responseCode > 0 ? " (HTTP " + req.responseCode + ")" : ""));
            }
        }

        IEnumerator CoResolveSpotify(string trackUrl, Action<string, string, string> done)
        {
            string html = null, err = null;
            // phone share links (spotify.link/...) first have to be opened to find the real track
            if (trackUrl.StartsWith("https://spotify.link/"))
            {
                string shortHtml = null;
                yield return CoHttpGet(trackUrl, (a, b) => { shortHtml = a; err = b; });
                string sid = Util.FindSpotifyTrackId(shortHtml);
                if (sid == null) { done(null, null, "Could not open the Spotify share link" + (err != null ? " (" + Util.Short(err, 60) + ")" : "") + ". Paste the open.spotify.com/track/... link instead."); yield break; }
                trackUrl = "https://open.spotify.com/track/" + sid;
                err = null;
            }
            yield return CoHttpGet(trackUrl, (a, b) => { html = a; err = b; });
            string title = null, artist = null;
            if (html != null && Util.ParseSpotifyHtml(html, out title, out artist)) { }
            else
            {
                string ojson = null, oerr = null;
                yield return CoHttpGet("https://open.spotify.com/oembed?url=" + Uri.EscapeDataString(trackUrl), (a, b) => { ojson = a; oerr = b; });
                string ot = Util.ParseOEmbedTitle(ojson);
                if (ot == null)
                {
                    LinkRadioPlugin.L.LogWarning("Spotify page unreadable: " + (err ?? "no title found in page") + " / oEmbed: " + (oerr ?? "no title"));
                    done(null, null, err != null ? "Could not read the Spotify page: " + Util.Short(err, 80) : "Could not read track info from Spotify (page layout changed?).");
                    yield break;
                }
                title = ot; artist = "";
            }

            yield return EnsureTools();
            if (!ToolsOk()) { done(null, null, "yt-dlp is missing: " + toolsMsg); yield break; }

            string query = Util.CleanQuery((string.IsNullOrEmpty(artist) ? "" : artist + " - ") + title + " audio");
            Job job = new Job();
            string exe = YtDlpPath();
            string sCk = "--no-warnings --no-colors --ignore-config --flat-playlist --print id --socket-timeout 20 " + ExtraYtArgs(false) + "\"ytsearch1:" + query + "\"";
            string sNo = "--no-warnings --no-colors --ignore-config --flat-playlist --print id --socket-timeout 20 \"ytsearch1:" + query + "\"";
            ThreadPool.QueueUserWorkItem(delegate
            {
                job.Res = Run(exe, sCk, 60000);
                if (IsCookieErr(job.Res.Err))
                {
                    cookieProblem = LastErrorLine(job.Res.Err);
                    job.Res = Run(exe, sNo, 60000);
                }
                job.Done = true;
            });
            while (!job.Done) yield return null;
            string id = null;
            foreach (string line in (job.Res.Out ?? "").Split('\n'))
            {
                string l = line.Trim();
                if (System.Text.RegularExpressions.Regex.IsMatch(l, "^[A-Za-z0-9_-]{11}$")) { id = l; break; }
            }
            string shown = Util.Short((string.IsNullOrEmpty(artist) ? "" : artist + " - ") + title, 80);
            if (id == null) { done(null, null, "No YouTube match for '" + Util.Short(shown, 60) + "'. " + Util.Short(job.Res.Err, 100)); yield break; }
            done("https://www.youtube.com/watch?v=" + id, shown, null);
        }

        // =====================================================================
        // Client playback
        void ApplyClient(LinkRadioMsg m)
        {
            switch (m.Op)
            {
                case 1:
                    {
                        StopLocal(null);
                        Uri u;
                        Track t = new Track { Seq = m.Seq, Gated = m.Start >= 1e8, Url = m.Url, Start = m.Start, Loop = m.Loop, Speaker = m.Speaker, Range = Mathf.Clamp(m.Range, 8f, 120f), Title = string.IsNullOrEmpty(m.Title) ? null : m.Title };
                        if (!Util.TryParseHttpUrl(m.Url, out u) || !Util.HostAllowed(u, P.CfgAllowedHosts.Value))
                        {
                            SetStatus(L("Blocked a link from the host (host not in your AllowedHosts).", "Link vom Host blockiert (nicht in deinen AllowedHosts)."), true); return;
                        }
                        t.Key = Util.KeyFor(u);
                        cur = t;
                        StartCoroutine(CoPrepare(t, u));
                        break;
                    }
                case 21: SetPrefetch(m.Url); break;
                case 2: StopLocal("Stopped."); break;
                case 5:
                    if (cur != null) { cur.Start = m.Start; cur.Gated = false; if (cur.Clip != null) SetStatus(L("Playing: ", "Läuft: ") + (cur.Title ?? cur.Key), false); }
                    break;
                case 18: queueInfo = m.Title ?? ""; nextSeconds = m.Pos; break;
                case 3: if (cur != null) { cur.Paused = true; cur.PausePos = m.Pos; } break;
                case 4: if (cur != null) { cur.Paused = false; cur.Start = m.Start; } break;
            }
        }

        void StopLocal(string msg)
        {
            Track old = cur; cur = null;
            if (src != null) { try { src.Stop(); src.clip = null; } catch { } }
            if (old != null && old.Clip != null) UnityEngine.Object.Destroy(old.Clip);
            if (msg != null) SetStatus(msg, false);
        }

        IEnumerator CoPrepare(Track t, Uri u)
        {
            SetStatus(L("Preparing ...", "Wird vorbereitet ..."), false);
            yield return EnsureTools();
            if (cur != t) yield break;
            if (!ToolsOk()) { Fail(t, "yt-dlp / ffmpeg missing: " + toolsMsg); yield break; }

            string file = Path.Combine(P.CacheDir, t.Key + ".mp3");
            string titleFile = Path.Combine(P.CacheDir, t.Key + ".title");
            while (prefetchingKey == t.Key)
            {
                SetStatus(L("Finishing background download ...", "Hintergrund-Download wird beendet ..."), false);
                yield return null;
                if (cur != t) yield break;
            }
            if (!File.Exists(file))
            {
                ProcResult last = null, first = null; float t0 = Time.unscaledTime;
                bool cookieRetried = false;
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    Job job = new Job();
                    string args = BuildDownloadArgs(Util.Normalize(u), t.Key, attempt == 1);
                    string exe = YtDlpPath();
                    ThreadPool.QueueUserWorkItem(delegate { job.Res = Run(exe, args, 240000); job.Done = true; });
                    while (!job.Done)
                    {
                        if (cur != t) yield break;
                        SetStatus((attempt == 1 ? L("Retrying (other YouTube client) ... ", "Neuer Versuch (anderer YouTube-Client) ... ") : L("Downloading audio ... ", "Audio wird geladen ... ")) + (int)(Time.unscaledTime - t0) + "s", false);
                        yield return null;
                    }
                    last = job.Res;
                    if (attempt == 0 && first == null) first = last;
                    if (File.Exists(file)) break;
                    if (!jsRtBad && (last.Err ?? "").IndexOf("--js-runtimes", StringComparison.OrdinalIgnoreCase) >= 0 && (last.Err ?? "").IndexOf("no such option", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        jsRtBad = true; attempt--; continue;
                    }
                    if (IsCookieErr(last.Err) && !cookieRetried)
                    {
                        cookieProblem = LastErrorLine(last.Err); cookieRetried = true;
                        LinkRadioPlugin.L.LogWarning("Cookies could not be read, continuing without: " + last.Err);
                        attempt--; continue;
                    }
                    if (!IsBotBlock(last.Err) && !IsFormatErr(last.Err)) break;
                }
                if (!File.Exists(file))
                {
                    string why;
                    if (first != null && IsBotBlock(first.Err)) last = first; // the first error is the real cause, not the retry's
                    if (last != null && IsBotBlock(last.Err))
                        why = cookieProblem != null
                            ? "YouTube blocked this PC (bot check) and the browser cookies could not be read (" + Util.Short(cookieProblem, 110) + "). Export a cookies.txt and set [Download] CookiesFile instead (see README)."
                            : "YouTube blocked this PC (bot check). Turn off VPN / try another network, or set [Download] CookiesFromBrowser (e.g. firefox) or CookiesFile in the LinkRadio config.";
                    else if (last != null && IsFormatErr(last.Err))
                        why = "YouTube returned no audio formats. yt-dlp probably needs its JavaScript runtime (deno) - restart the game once so it can install it, then click 'Update yt-dlp'. If it persists, see the log.";
                    else why = last != null && last.TimedOut ? "timed out" : LastErrorLine(last == null ? "" : last.Err);
                    LinkRadioPlugin.L.LogWarning("yt-dlp error: " + (last == null ? "" : last.Err));
                    Fail(t, L("Download failed: ", "Download fehlgeschlagen: ") + why); yield break;
                }
            }
            try { if (File.Exists(titleFile)) t.Title = Util.Short(File.ReadAllText(titleFile, Encoding.UTF8), 100); } catch { }

            AudioClip clip = null; string loadErr = null;
            using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(new Uri(file).AbsoluteUri, AudioType.MPEG))
            {
                DownloadHandlerAudioClip dh = (DownloadHandlerAudioClip)req.downloadHandler;
                dh.streamAudio = false;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) loadErr = req.error;
                else clip = DownloadHandlerAudioClip.GetContent(req);
            }
            if (cur != t) { if (clip != null) UnityEngine.Object.Destroy(clip); yield break; }
            if (clip == null || clip.length < 0.5f) { Fail(t, L("Could not decode audio", "Audio konnte nicht gelesen werden") + (loadErr != null ? ": " + loadErr : ".")); yield break; }
            t.Clip = clip;
            if (t.Gated)
            {
                SetStatus(L("Ready: ", "Bereit: ") + (t.Title ?? t.Key) + L(" - waiting for the others ...", " - warte auf die anderen ..."), false);
                ReportReady(t);
            }
            else SetStatus(L("Ready: ", "Bereit: ") + (t.Title ?? t.Key), false);
        }

        static readonly System.Text.RegularExpressions.Regex BrowserRx = new System.Text.RegularExpressions.Regex("^[A-Za-z0-9_:+ .\\\\/-]{1,200}$");

        // Extra yt-dlp arguments: cookies (user configured) and, on a retry, alternative YouTube clients.
        string cookieProblem = null; // set when reading cookies failed once -> cookies are skipped afterwards

        static bool IsCookieErr(string err)
        {
            if (string.IsNullOrEmpty(err) || IsBotBlock(err)) return false;
            return err.IndexOf("cookie", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        string ExtraYtArgs(bool altClients)
        {
            StringBuilder sb = new StringBuilder();
            string b = (P.CfgCookiesBrowser.Value ?? "").Trim();
            string f = (P.CfgCookiesFile.Value ?? "").Trim();
            if (cookieProblem != null) { }
            else if (b.Length > 0 && BrowserRx.IsMatch(b) && !b.StartsWith("-")) sb.Append("--cookies-from-browser \"" + b + "\" ");
            else if (f.Length > 0 && File.Exists(f) && f.IndexOf('"') < 0) sb.Append("--cookies \"" + f + "\" ");
            string dn = DenoPath();
            if (!jsRtBad && dn != null && dn.IndexOf('"') < 0) sb.Append("--js-runtimes \"deno:" + dn + "\" ");
            if (altClients) sb.Append("--extractor-args \"youtube:player_client=android_vr,tv,web_safari\" ");
            return sb.ToString();
        }

        static bool IsFormatErr(string err)
        {
            return !string.IsNullOrEmpty(err) && err.IndexOf("Requested format is not available", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool IsBotBlock(string err)
        {
            if (string.IsNullOrEmpty(err)) return false;
            return err.IndexOf("not a bot", StringComparison.OrdinalIgnoreCase) >= 0 || err.IndexOf("Sign in to confirm", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        string BuildDownloadArgs(string ytUrl, string key, bool alt)
        {
            string ffDir = Path.GetDirectoryName(FfmpegPath());
            int maxSec = Math.Max(1, P.CfgMaxMinutes.Value) * 60;
            StringBuilder sb = new StringBuilder();
            sb.Append("--no-playlist --no-progress --no-colors --ignore-config --socket-timeout 20 --retries 2 ");
            sb.Append(ExtraYtArgs(alt));
            sb.Append("-f \"bestaudio/best\" -x --audio-format mp3 --audio-quality 5 ");
            sb.Append("--ffmpeg-location \"" + ffDir + "\" ");
            sb.Append("--match-filter \"!is_live & duration<=" + maxSec + "\" ");
            sb.Append("-o \"" + Path.Combine(P.CacheDir, key) + ".%(ext)s\" ");
            sb.Append("--print-to-file \"%(title)s\" \"" + Path.Combine(P.CacheDir, key) + ".title\" ");
            sb.Append("-- \"" + ytUrl + "\"");
            return sb.ToString();
        }

        static string LastErrorLine(string err)
        {
            if (string.IsNullOrEmpty(err)) return "unknown error";
            string best = null;
            foreach (string l in err.Split('\n')) { string s = l.Trim(); if (s.Length > 0 && (s.StartsWith("ERROR") || best == null)) best = s; }
            return Util.Short(best, 140);
        }

        void Fail(Track t, string msg) { t.Failed = true; ReportReady(t); SetStatus(msg, true); LinkRadioPlugin.L.LogWarning(msg); }

        void EnsureSource()
        {
            if (srcGo != null && src != null) return;
            srcGo = new GameObject("LinkRadioSpeaker");
            srcGo.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(srcGo);
            src = srcGo.AddComponent<AudioSource>();
            src.playOnAwake = false; src.dopplerLevel = 0f; src.spread = 60f; src.rolloffMode = AudioRolloffMode.Linear;
            src.spatialBlend = 0f; src.minDistance = 3f; src.maxDistance = 40f; src.priority = 64;
            anchor = null;
        }

        void TickPlayback()
        {
            Track t = cur;
            if (t == null || t.Clip == null) return;
            EnsureSource();
            if (Time.unscaledTime > nextAnchorCheck) { nextAnchorCheck = Time.unscaledTime + 1f; UpdateAnchor(t); }
            if (mixer != null && src.outputAudioMixerGroup != mixer) src.outputAudioMixerGroup = mixer;
            src.volume = muted ? 0f : P.CfgVolume.Value;

            float len = t.Clip.length;
            if (t.Paused)
            {
                if (src.isPlaying) src.Pause();
                return;
            }
            double expected = NetworkTime.time - t.Start;
            if (expected < 0) { if (src.isPlaying) src.Stop(); return; }
            if (t.Loop) expected = expected % len;
            else if (expected >= len - 0.05)
            {
                if (src.isPlaying) src.Stop();
                return;
            }
            if (!src.isPlaying)
            {
                src.clip = t.Clip; src.loop = t.Loop; src.time = (float)expected; src.Play(); lastCorr = Time.unscaledTime;
            }
            else if (Time.unscaledTime - lastCorr > 3f)
            {
                lastCorr = Time.unscaledTime;
                float diff = Mathf.Abs(src.time - (float)expected);
                if (t.Loop) diff = Mathf.Min(diff, len - diff);
                if (diff > 0.4f) src.time = (float)expected;
            }
        }

        void UpdateAnchor(Track t)
        {
            Transform want = null;
            if (t.Speaker != 0)
            {
                NetworkIdentity id;
                if (NetworkClient.spawned.TryGetValue(t.Speaker, out id) && id != null) want = id.transform;
            }
            src.minDistance = 3f; src.maxDistance = t.Range;
            if (want != anchor || (want == null) != (src.spatialBlend < 0.5f))
            {
                anchor = want;
                if (want != null)
                {
                    srcGo.transform.SetParent(want, false);
                    srcGo.transform.localPosition = new Vector3(0f, 0.3f, 0f);
                    src.spatialBlend = 1f;
                }
                else
                {
                    srcGo.transform.SetParent(null, false);
                    src.spatialBlend = 0f;
                }
            }
        }

        void FindMixer()
        {
            try
            {
                Type t = Util.FindType("NomadDrive.Features.Vehicle.Parts.Radio.VehicleRadio");
                if (t == null) return;
                FieldInfo f = t.GetField("audioSource", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f == null) return;
                foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsOfType(t))
                {
                    AudioSource a = f.GetValue(o) as AudioSource;
                    if (a != null && a.outputAudioMixerGroup != null) { mixer = a.outputAudioMixerGroup; return; }
                }
            }
            catch (Exception e) { LinkRadioPlugin.L.LogWarning("Mixer lookup: " + e.Message); }
        }

        // Picks the nearest car radio / boombox as the speaker.
        uint PickSpeaker(out string name)
        {
            name = "global (no radio / boombox near you)";
            try
            {
                Camera cam = Camera.main; if (cam == null) return 0;
                Vector3 p = cam.transform.position;
                float best = P.CfgMaxPick.Value; uint bestId = 0;
                string[] types = { "NomadDrive.Features.Vehicle.Parts.Radio.VehicleRadio", "BoomboxMod.BoomboxPlayer" };
                foreach (string tn in types)
                {
                    Type t = Util.FindType(tn); if (t == null) continue;
                    foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsOfType(t))
                    {
                        Component c = o as Component; if (c == null) continue;
                        float d = Vector3.Distance(p, c.transform.position);
                        if (d >= best) continue;
                        NetworkIdentity id = c.GetComponentInParent<NetworkIdentity>();
                        if (id == null || id.netId == 0) continue;
                        best = d; bestId = id.netId;
                        name = (tn.EndsWith("BoomboxPlayer") ? "boombox" : "car radio") + " (" + d.ToString("0.0") + " m)";
                    }
                }
                return bestId;
            }
            catch (Exception e) { LinkRadioPlugin.L.LogWarning("PickSpeaker: " + e.Message); return 0; }
        }

        // =====================================================================
        // Tools (yt-dlp / ffmpeg)
        string FindOnPath(string exe)
        {
            try
            {
                string path = Environment.GetEnvironmentVariable("PATH") ?? "";
                foreach (string d in path.Split(Path.PathSeparator))
                {
                    if (string.IsNullOrEmpty(d)) continue;
                    string f = Path.Combine(d.Trim('"'), exe);
                    if (File.Exists(f)) return f;
                }
            }
            catch { }
            return null;
        }

        string YtDlpPath()
        {
            string c = P.CfgYtDlp.Value; if (!string.IsNullOrEmpty(c) && File.Exists(c)) return c;
            string f = Path.Combine(P.ToolsDir, "yt-dlp.exe"); if (File.Exists(f)) return f;
            return FindOnPath("yt-dlp.exe") ?? FindOnPath("yt-dlp") ?? f;
        }

        string FfmpegPath()
        {
            string c = P.CfgFfmpeg.Value; if (!string.IsNullOrEmpty(c) && File.Exists(c)) return c;
            string f = Path.Combine(P.ToolsDir, "ffmpeg.exe"); if (File.Exists(f)) return f;
            return FindOnPath("ffmpeg.exe") ?? FindOnPath("ffmpeg") ?? f;
        }

        string DenoPath()
        {
            string f = Path.Combine(P.ToolsDir, "deno.exe"); if (File.Exists(f)) return f;
            return FindOnPath("deno.exe") ?? FindOnPath("deno");
        }

        bool ToolsOk() { return File.Exists(YtDlpPath()) && File.Exists(FfmpegPath()); }

        IEnumerator EnsureTools()
        {
            while (toolsBusy) yield return null;
            if (ToolsOk() && !forceToolUpdate && (denoTried || DenoPath() != null || !P.CfgAutoTools.Value)) yield break;
            if (!P.CfgAutoTools.Value) { toolsMsg = "not found (put yt-dlp.exe + ffmpeg.exe into " + P.ToolsDir + ")"; yield break; }
            toolsBusy = true;
            toolsMsg = "";
            string ytPath = Path.Combine(P.ToolsDir, "yt-dlp.exe");
            if (!File.Exists(YtDlpPath()) || forceToolUpdate)
            {
                yield return DownloadFile(YtDlpUrl, ytPath, L("Downloading yt-dlp", "yt-dlp wird geladen"));
            }
            forceToolUpdate = false;
            if (!File.Exists(FfmpegPath()))
            {
                string zip = Path.Combine(P.ToolsDir, "ffmpeg.zip");
                yield return DownloadFile(FfmpegZipUrl, zip, L("Downloading ffmpeg (one time, ~200 MB)", "ffmpeg wird geladen (einmalig, ca. 200 MB)"));
                if (File.Exists(zip))
                {
                    SetStatus(L("Extracting ffmpeg ...", "ffmpeg wird entpackt ..."), false);
                    Job job = new Job();
                    string tmp = Path.Combine(P.ToolsDir, "ff_tmp"); string dest = Path.Combine(P.ToolsDir, "ffmpeg.exe");
                    ThreadPool.QueueUserWorkItem(delegate { job.Extra = ExtractFfmpeg(zip, tmp, dest); job.Done = true; });
                    while (!job.Done) yield return null;
                    if (!string.IsNullOrEmpty(job.Extra)) toolsMsg = job.Extra;
                    try { File.Delete(zip); } catch { }
                }
            }
            if (!denoTried && DenoPath() == null)
            {
                denoTried = true; // one try per session; deno is optional (needed by yt-dlp for some YouTube formats)
                string dzip = Path.Combine(P.ToolsDir, "deno.zip");
                string keepMsg = toolsMsg;
                yield return DownloadFile(DenoZipUrl, dzip, L("Downloading JS runtime (deno, one time, ~45 MB)", "JS-Runtime wird geladen (deno, einmalig, ca. 45 MB)"));
                if (File.Exists(dzip))
                {
                    SetStatus(L("Extracting deno ...", "deno wird entpackt ..."), false);
                    Job dj = new Job();
                    string dtmp = Path.Combine(P.ToolsDir, "deno_tmp"); string ddest = Path.Combine(P.ToolsDir, "deno.exe");
                    ThreadPool.QueueUserWorkItem(delegate { dj.Extra = ExtractExe(dzip, dtmp, ddest, "deno.exe"); dj.Done = true; });
                    while (!dj.Done) yield return null;
                    try { File.Delete(dzip); } catch { }
                }
                toolsMsg = keepMsg;
            }
            if (!ToolsOk() && toolsMsg.Length == 0) toolsMsg = "download failed";
            toolsBusy = false;
            SetStatus(ToolsOk() ? L("Tools ready.", "Tools bereit.") : L("Tools missing: ", "Tools fehlen: ") + toolsMsg, !ToolsOk());
        }

        IEnumerator DownloadFile(string srcUrl, string dest, string label)
        {
            string tmp = dest + ".part";
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            Job job = new Job();
            ThreadPool.QueueUserWorkItem(delegate
            {
                // curl.exe ships with Windows 10+; fall back to PowerShell if it is missing.
                ProcResult r = Run("curl.exe", "-L --fail --silent --show-error --retry 2 --connect-timeout 20 -o \"" + tmp + "\" \"" + srcUrl + "\"", 900000);
                if (r.Code != 0 || !File.Exists(tmp))
                {
                    ProcResult r2 = Run("powershell", "-NoProfile -NonInteractive -Command \"$ProgressPreference='SilentlyContinue'; Invoke-WebRequest -UseBasicParsing -Uri '" + srcUrl + "' -OutFile '" + tmp + "'\"", 900000);
                    if (r2.Code != 0) r = r2;
                    else r = r2;
                }
                job.Res = r; job.Done = true;
            });
            float t0 = Time.unscaledTime;
            while (!job.Done)
            {
                long size = 0; try { FileInfo fi = new FileInfo(tmp); if (fi.Exists) size = fi.Length; } catch { }
                SetStatus(label + " ... " + (size / (1024 * 1024)) + " MB (" + (int)(Time.unscaledTime - t0) + "s)", false);
                yield return null;
            }
            if (job.Res.Code != 0 || !File.Exists(tmp))
            {
                toolsMsg = label + " failed: " + LastErrorLine(job.Res.Err);
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                yield break;
            }
            try { if (File.Exists(dest)) File.Delete(dest); File.Move(tmp, dest); }
            catch (Exception e) { toolsMsg = "could not save file: " + e.Message; }
        }

        static string ExtractFfmpeg(string zip, string tmp, string dest) { return ExtractExe(zip, tmp, dest, "ffmpeg.exe"); }

        static string ExtractExe(string zip, string tmp, string dest, string exeName)
        {
            try
            {
                if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
                Directory.CreateDirectory(tmp);
                ProcResult r = Run("powershell", "-NoProfile -NonInteractive -Command \"Expand-Archive -LiteralPath '" + zip + "' -DestinationPath '" + tmp + "' -Force\"", 300000);
                string found = FindFile(tmp, exeName);
                if (found == null)
                {
                    r = Run("tar", "-xf \"" + zip + "\" -C \"" + tmp + "\"", 300000);
                    found = FindFile(tmp, exeName);
                }
                if (found == null) return "could not extract " + exeName + " (extract it manually into the tools folder)";
                if (File.Exists(dest)) File.Delete(dest);
                File.Copy(found, dest);
                Directory.Delete(tmp, true);
                return "";
            }
            catch (Exception e) { return exeName + " extract error: " + e.Message; }
        }

        static string FindFile(string dir, string name)
        {
            try { foreach (string f in Directory.GetFiles(dir, name, SearchOption.AllDirectories)) return f; } catch { }
            return null;
        }

        static ProcResult Run(string exe, string args, int timeoutMs)
        {
            ProcResult res = new ProcResult();
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(exe, args);
                psi.UseShellExecute = false; psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8; psi.StandardErrorEncoding = Encoding.UTF8;
                StringBuilder so = new StringBuilder(), se = new StringBuilder();
                using (Process p = Process.Start(psi))
                {
                    p.OutputDataReceived += (s, e) => { if (e.Data != null) lock (so) so.AppendLine(e.Data); };
                    p.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (se) se.AppendLine(e.Data); };
                    p.BeginOutputReadLine(); p.BeginErrorReadLine();
                    if (!p.WaitForExit(timeoutMs)) { res.TimedOut = true; try { p.Kill(); } catch { } }
                    else { p.WaitForExit(); res.Code = p.ExitCode; }
                }
                lock (so) res.Out = so.ToString(); lock (se) res.Err = se.ToString();
            }
            catch (Exception e) { res.Err = "Could not start " + Path.GetFileName(exe) + ": " + e.Message; }
            return res;
        }
    }
}
