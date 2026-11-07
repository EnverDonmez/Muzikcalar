using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SadeSes
{
    static class Network
    {
        public static readonly HttpClient Client = Create();
        static HttpClient Create()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            ServicePointManager.DefaultConnectionLimit = 12;
            var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate, UseCookies = false };
            var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(12) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("tr-TR,tr;q=0.9,en;q=0.6");
            return client;
        }
        public static async Task<string> Text(string url, CancellationToken token, int seconds)
        {
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                deadline.CancelAfter(TimeSpan.FromSeconds(seconds));
                using (var response = await Client.GetAsync(url, HttpCompletionOption.ResponseContentRead, deadline.Token).ConfigureAwait(false))
                { response.EnsureSuccessStatusCode(); return await response.Content.ReadAsStringAsync().ConfigureAwait(false); }
            }
        }
    }

    public static class YouTubeSearch
    {
        public const int ResultLimit = 15;
        public static async Task<List<Track>> Search(string input, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string target = Tools.Target(input);
            string key;
            string cacheKey = target.StartsWith("ytsearch", StringComparison.Ordinal) ? target.ToLowerInvariant() : target;
            using (var sha = SHA256.Create()) key = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(cacheKey))).Replace("-", "").ToLowerInvariant();
            string path = Path.Combine(AppPaths.Cache, "search", key + ".json");
            try
            {
                if (File.Exists(path) && DateTime.UtcNow - File.GetLastWriteTimeUtc(path) < TimeSpan.FromMinutes(10))
                {
                    var cached = Decode(File.ReadAllText(path, Encoding.UTF8));
                    token.ThrowIfCancellationRequested();
                    if (cached.Count > 0) return cached;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { }
            List<Track> found = null;
            try
            {
                if (target.StartsWith("ytsearch", StringComparison.Ordinal))
                {
                    string url = "https://www.youtube.com/results?search_query=" + Uri.EscapeDataString(input.Trim()) + "&hl=tr&sp=EgIQAQ%253D%253D";
                    var html = await Network.Text(url, token, 7).ConfigureAwait(false);
                    found = ParsePage(html);
                    if (found.Count == 0) throw new InvalidDataException("Search layout unavailable");
                }
                else
                {
                    string json = await Network.Text("https://www.youtube.com/oembed?url=" + Uri.EscapeDataString(target) + "&format=json", token, 5).ConfigureAwait(false);
                    var d = Json.Read(json);
                    string id = new Uri(target).Query.Substring(3);
                    found = new List<Track> { new Track { Id = id, Title = Json.Str(d, "title"), Channel = Json.Str(d, "author_name"), Thumbnail = "https://i.ytimg.com/vi/" + id + "/mqdefault.jpg" } };
                }
            }
            catch (Exception)
            {
                token.ThrowIfCancellationRequested();
            }
            if (found == null) found = await Tools.SearchLegacy(input, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            try { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, Encode(found), new UTF8Encoding(false)); } catch { }
            return found;
        }

        public static string Encode(IEnumerable<Track> tracks)
        { return Json.Write(new { entries = tracks.Select(t => new { id = t.Id, title = t.Title, channel = t.Channel, duration = t.Duration, thumbnail = t.ArtworkUrl }).ToArray() }); }
        public static List<Track> Decode(string json)
        {
            var entries = Json.Get(Json.Read(json), "entries") as IEnumerable;
            var result = new List<Track>();
            if (entries != null) foreach (var item in entries) { var track = Track.From(item as Dictionary<string, object>); if (track != null) result.Add(track); }
            return result;
        }
        public static List<Track> ParsePage(string html)
        {
            var match = Regex.Match(html, @"(?:var\s+ytInitialData\s*=|window\[""ytInitialData""\]\s*=|ytInitialData\s*=)\s*\{");
            if (!match.Success) throw new InvalidDataException("YouTube initial data missing");
            int start = match.Index + match.Length - 1, depth = 0, end = -1;
            bool quoted = false, escaped = false;
            for (int i = start; i < html.Length; i++)
            {
                char c = html[i];
                if (quoted) { if (escaped) escaped = false; else if (c == '\\') escaped = true; else if (c == '"') quoted = false; continue; }
                if (c == '"') quoted = true;
                else if (c == '{') depth++;
                else if (c == '}' && --depth == 0) { end = i + 1; break; }
            }
            if (end < 0) throw new InvalidDataException("Incomplete search data");
            var initial = Json.Read(html.Substring(start, end - start));
            var result = new List<Track>(); var seen = new HashSet<string>();
            Walk(Json.Get(initial, "contents"), result, seen);
            return result.Take(ResultLimit).ToList();
        }
        static void Walk(object node, List<Track> result, HashSet<string> seen)
        {
            if (result.Count >= ResultLimit) return;
            var d = node as Dictionary<string, object>;
            if (d != null)
            {
                var video = Json.Get(d, "videoRenderer") as Dictionary<string, object>;
                if (video != null)
                {
                    string id = Json.Str(video, "videoId");
                    if (!Regex.IsMatch(id, "^[a-zA-Z0-9_-]{11}$") || !seen.Add(id)) return;
                    var t = new Track { Id = id, Title = Text(Json.Get(video, "title")), Channel = Text(Json.Get(video, "ownerText")), Duration = Duration(Text(Json.Get(video, "lengthText"))), Thumbnail = "https://i.ytimg.com/vi/" + id + "/mqdefault.jpg" };
                    if (t.Channel.Length == 0) t.Channel = Text(Json.Get(video, "shortBylineText"));
                    if (t.Title.Length > 0) result.Add(t);
                    return;
                }
                foreach (var pair in d)
                {
                    if (pair.Key.IndexOf("adSlot", StringComparison.OrdinalIgnoreCase) >= 0 || pair.Key.StartsWith("promoted", StringComparison.OrdinalIgnoreCase)) continue;
                    Walk(pair.Value, result, seen);
                }
            }
            else if (node is IEnumerable && !(node is string)) foreach (var value in (IEnumerable)node) Walk(value, result, seen);
        }
        static string Text(object node)
        {
            var d = node as Dictionary<string, object>; if (d == null) return "";
            string simple = Json.Str(d, "simpleText"); if (simple.Length > 0) return simple;
            var runs = Json.Get(d, "runs") as IEnumerable;
            if (runs == null) return "";
            var result = new StringBuilder(); foreach (var run in runs) result.Append(Json.Str(run as Dictionary<string, object>, "text")); return result.ToString();
        }
        static double Duration(string text)
        {
            double total = 0;
            foreach (string part in text.Split(':')) { double number; if (!double.TryParse(part, out number)) return 0; total = total * 60 + number; }
            return total;
        }
    }
}
