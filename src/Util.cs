// Pure helper functions (no Unity / Mirror dependency) so they can be unit-tested.
using System;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace LinkRadio
{
    internal static class Util
    {
        static readonly char[] BadUrlChars = { ' ', '"', '\'', '`', '<', '>', '|', '^', '\r', '\n', '\t', '\\', '{', '}' };

        public static bool TryParseHttpUrl(string raw, out Uri uri)
        {
            uri = null;
            if (string.IsNullOrEmpty(raw)) return false;
            raw = raw.Trim();
            if (raw.Length > 400) return false;
            if (raw.IndexOfAny(BadUrlChars) >= 0) return false;
            if (raw.StartsWith("-")) return false;
            Uri u;
            if (!Uri.TryCreate(raw, UriKind.Absolute, out u)) return false;
            if (u.Scheme != "https" && u.Scheme != "http") return false;
            uri = u;
            return true;
        }

        public static bool HostAllowed(Uri u, string allowedCsv)
        {
            if (u == null || string.IsNullOrEmpty(allowedCsv)) return false;
            string h = u.Host.ToLowerInvariant();
            foreach (string a0 in allowedCsv.Split(','))
            {
                string a = a0.Trim().ToLowerInvariant();
                if (a.Length == 0) continue;
                if (h == a || h.EndsWith("." + a)) return true;
            }
            return false;
        }

        static readonly Regex IdRx = new Regex(@"^[A-Za-z0-9_-]{11}$");

        public static string YouTubeId(Uri u)
        {
            if (u == null) return null;
            string h = u.Host.ToLowerInvariant();
            bool isYt = h == "youtube.com" || h.EndsWith(".youtube.com") || h == "youtube-nocookie.com" || h.EndsWith(".youtube-nocookie.com");
            if (h == "youtu.be" || h.EndsWith(".youtu.be"))
            {
                string seg = u.AbsolutePath.Trim('/');
                int slash = seg.IndexOf('/');
                if (slash > 0) seg = seg.Substring(0, slash);
                return IdRx.IsMatch(seg) ? seg : null;
            }
            if (!isYt) return null;
            Match m = Regex.Match(u.Query, @"[?&]v=([A-Za-z0-9_-]{11})(?:&|$)");
            if (m.Success) return m.Groups[1].Value;
            m = Regex.Match(u.AbsolutePath, @"/(?:shorts|embed|live|v)/([A-Za-z0-9_-]{11})(?:/|$)");
            if (m.Success) return m.Groups[1].Value;
            return null;
        }

        /// <summary>Playlist id of a pure YouTube / YouTube Music playlist link (youtube.com/playlist?list=...). Null for single videos.</summary>
        public static string YouTubePlaylistId(Uri u)
        {
            if (u == null || YouTubeId(u) != null) return null;
            string h = u.Host.ToLowerInvariant();
            if (!(h == "youtube.com" || h.EndsWith(".youtube.com"))) return null;
            if (u.AbsolutePath.TrimEnd('/').ToLowerInvariant() != "/playlist") return null;
            Match m = Regex.Match(u.Query, @"[?&]list=([A-Za-z0-9_-]{2,80})(?:&|$)");
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>Canonical URL handed to yt-dlp (strips playlist / tracking parameters for YouTube).</summary>
        public static string Normalize(Uri u)
        {
            string id = YouTubeId(u);
            if (id != null) return "https://www.youtube.com/watch?v=" + id;
            return u.AbsoluteUri;
        }

        public static string KeyFor(Uri u)
        {
            string id = YouTubeId(u);
            if (id != null) return "yt_" + id;
            return "u_" + Md5Hex(u.AbsoluteUri).Substring(0, 16);
        }

        public static string Md5Hex(string s)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] b = md5.ComputeHash(Encoding.UTF8.GetBytes(s));
                StringBuilder sb = new StringBuilder();
                foreach (byte x in b) sb.Append(x.ToString("x2"));
                return sb.ToString();
            }
        }

        // ---------- Spotify ----------
        public static bool TryParseSpotifyTrack(string raw, out string trackUrl, out bool isSpotifyButUnsupported)
        {
            trackUrl = null; isSpotifyButUnsupported = false;
            if (string.IsNullOrEmpty(raw)) return false;
            raw = raw.Trim();
            Match m = Regex.Match(raw, @"^spotify:track:([A-Za-z0-9]{22})$");
            if (m.Success) { trackUrl = "https://open.spotify.com/track/" + m.Groups[1].Value; return true; }
            if (Regex.IsMatch(raw, @"^spotify:", RegexOptions.IgnoreCase)) { isSpotifyButUnsupported = true; return false; }
            Uri u;
            if (!TryParseHttpUrl(raw, out u)) return false;
            string h = u.Host.ToLowerInvariant();
            if (h != "open.spotify.com" && h != "spotify.link" && !h.EndsWith(".spotify.com")) return false;
            m = Regex.Match(u.AbsolutePath, @"/track/([A-Za-z0-9]{22})");
            if (m.Success) { trackUrl = "https://open.spotify.com/track/" + m.Groups[1].Value; return true; }
            if (h == "spotify.link" && Regex.IsMatch(u.AbsolutePath, @"^/[A-Za-z0-9]{4,32}/?$")) { trackUrl = "https://spotify.link" + u.AbsolutePath.TrimEnd('/'); return true; } // phone share link, resolved later
            isSpotifyButUnsupported = true;
            return false;
        }

        /// <summary>Finds a track id inside any page text (used for spotify.link share links).</summary>
        public static string FindSpotifyTrackId(string html)
        {
            if (string.IsNullOrEmpty(html)) return null;
            Match m = Regex.Match(html, @"open\.spotify\.com(?:/[A-Za-z-]{2,12})?/track/([A-Za-z0-9]{22})");
            if (!m.Success) m = Regex.Match(html, @"spotify:track:([A-Za-z0-9]{22})");
            if (!m.Success) m = Regex.Match(html, @"open\.spotify\.com%2F(?:[A-Za-z-]{2,12}%2F)?track%2F([A-Za-z0-9]{22})");
            return m.Success ? m.Groups[1].Value : null;
        }

        static string Meta(string html, string prop)
        {
            foreach (Match tag in Regex.Matches(html, "<meta\\b[^>]*>", RegexOptions.IgnoreCase))
            {
                string t = tag.Value;
                Match p = Regex.Match(t, "(?:property|name)\\s*=\\s*[\"']" + Regex.Escape(prop) + "[\"']", RegexOptions.IgnoreCase);
                if (!p.Success) continue;
                Match c = Regex.Match(t, "content\\s*=\\s*\"([^\"]*)\"", RegexOptions.IgnoreCase);
                if (!c.Success) c = Regex.Match(t, "content\\s*=\\s*'([^']*)'", RegexOptions.IgnoreCase);
                if (c.Success) return WebUtility.HtmlDecode(c.Groups[1].Value).Trim();
            }
            return null;
        }

        public static bool ParseSpotifyHtml(string html, out string title, out string artist)
        {
            title = null; artist = null;
            if (string.IsNullOrEmpty(html)) return false;
            Match t = Regex.Match(html, "<title[^>]*>(.*?)</title>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (t.Success)
            {
                string tt = WebUtility.HtmlDecode(t.Groups[1].Value).Trim();
                Match mm = Regex.Match(tt,
                    @"^(?<title>.+) - (?<kind>[^|]*?) (?:by|von|de|par|di|por|door) (?<artist>[^|]+?)\s*\|\s*Spotify$",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (!mm.Success)
                    mm = Regex.Match(tt, @"^(?<title>.+?)\s+[\u00B7\-]\s+(?:song|lied|canci\u00F3n|chanson|brano)\s+(?:by|von|de|par|di|por|door)\s+(?<artist>[^|]+?)\s*\|\s*Spotify$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (mm.Success)
                {
                    title = mm.Groups["title"].Value.Trim();
                    artist = mm.Groups["artist"].Value.Trim();
                    if (title.Length > 0 && artist.Length > 0) return true;
                }
            }
            string ot = Meta(html, "og:title");
            string od = Meta(html, "og:description");
            if (!string.IsNullOrEmpty(ot) && !string.IsNullOrEmpty(od))
            {
                string a = od;
                int pre = a.IndexOf("Spotify. ", StringComparison.OrdinalIgnoreCase);   // "Listen to X on Spotify. Artist \u00B7 Song \u00B7 2020"
                if (pre >= 0 && pre < 120) a = a.Substring(pre + 9);
                int dot = a.IndexOf('\u00B7');
                if (dot > 0)
                {
                    title = ot.Trim();
                    artist = a.Substring(0, dot).Trim();
                    return title.Length > 0 && artist.Length > 0;
                }
            }
            return false;
        }

        /// <summary>Track name from Spotify's oEmbed JSON (no artist) - last-resort fallback.</summary>
        public static string ParseOEmbedTitle(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            Match m = Regex.Match(json, "\"title\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            if (!m.Success) return null;
            string v = Regex.Unescape(m.Groups[1].Value).Trim();
            return v.Length > 0 ? v : null;
        }

        public static string CleanQuery(string s)
        {
            if (s == null) return "";
            StringBuilder sb = new StringBuilder();
            foreach (char c in s)
            {
                if (c == '"' || c == '\\' || char.IsControl(c)) continue;
                sb.Append(c);
            }
            string r = sb.ToString().Trim();
            if (r.Length > 160) r = r.Substring(0, 160);
            return r;
        }

        public static string FormatTime(double s)
        {
            if (s < 0 || double.IsNaN(s)) s = 0;
            int t = (int)s;
            return (t / 60) + ":" + (t % 60).ToString("00");
        }

        public static string Short(string s, int max)
        {
            if (s == null) return "";
            s = s.Replace("\r", " ").Replace("\n", " ").Trim();
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }

        // Length as printed by yt-dlp ("242", "242.0", "NA"). 0 = unknown.
        public static float ParseSeconds(string s)
        {
            float v;
            if (s == null || !float.TryParse(s.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) return 0f;
            return v > 0f ? v : 0f;
        }

        // One line of the queue text sent by the host: "1. Title  [Who]"
        public static bool ParseQueueLine(string line, out string num, out string title, out string who)
        {
            num = title = who = "";
            if (line == null) return false;
            line = line.Trim();
            int dot = line.IndexOf(". ", StringComparison.Ordinal);
            int open = line.LastIndexOf("  [", StringComparison.Ordinal);
            int n;
            if (dot < 1 || open <= dot || !line.EndsWith("]") || !int.TryParse(line.Substring(0, dot), out n)) return false;
            num = line.Substring(0, dot);
            title = line.Substring(dot + 2, open - dot - 2).Trim();
            who = line.Substring(open + 3, line.Length - open - 4);
            return true;
        }

        // Number of songs in that text, including the "+ N more" line.
        public static int CountQueue(string info)
        {
            if (string.IsNullOrEmpty(info)) return 0;
            int count = 0;
            foreach (string line in info.Split('\n'))
            {
                string a, b, c, s = line.Trim();
                int more;
                if (ParseQueueLine(s, out a, out b, out c)) count++;
                else if (s.StartsWith("+ ") && s.EndsWith(" more") && int.TryParse(s.Substring(2, s.Length - 7), out more)) count += more;
            }
            return count;
        }

        public static Type FindType(string fullName)
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = a.GetType(fullName, false); } catch { }
                if (t != null) return t;
            }
            return null;
        }
    }
}
