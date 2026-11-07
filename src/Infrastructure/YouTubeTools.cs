using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SadeSes
{
    public class ToolResult { public int Code; public string Output; public string Error; }
    public sealed class SourceException : IOException
    {
        public readonly bool Transient;
        public SourceException(string message, bool transient) : base(message) { Transient = transient; }
    }

    public static class Tools
    {
        class StreamEntry { public DateTime Created; public Dictionary<string, object> Data; }
        static readonly Dictionary<string, StreamEntry> streams = new Dictionary<string, StreamEntry>();
        static readonly object streamLock = new object();
        public static string Bin(string name) { return Path.Combine(AppPaths.Root, "bin", name); }
        public static string Quote(string value)
        {
            var b = new StringBuilder("\""); int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\0') continue;
                if (c == '\\') { slashes++; continue; }
                if (c == '"') { b.Append('\\', slashes * 2 + 1); b.Append(c); slashes = 0; continue; }
                b.Append('\\', slashes); slashes = 0; b.Append(c);
            }
            b.Append('\\', slashes * 2); b.Append('"'); return b.ToString();
        }
        public static string Node()
        {
            var candidates = new List<string> { Bin("node.exe"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe") };
            candidates.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "").Split(';').Where(p => p.Length > 0).Select(p => Path.Combine(p.Trim('"'), "node.exe")));
            return candidates.FirstOrDefault(File.Exists);
        }
        public static string Target(string input)
        {
            input = input.Trim();
            if (input.Length == 0 || input.Length > 500) throw new Exception("Bir şarkı adı veya YouTube video bağlantısı yaz.");
            if (input.Contains("://") || input.StartsWith("www.", StringComparison.OrdinalIgnoreCase) || input.StartsWith("youtu.be/", StringComparison.OrdinalIgnoreCase))
            {
                Uri u;
                if (!Uri.TryCreate(input.Contains("://") ? input : "https://" + input, UriKind.Absolute, out u)) throw new Exception("Geçerli bir YouTube video bağlantısı yaz.");
                string host = u.Host.ToLowerInvariant();
                if ((u.Scheme != "https" && u.Scheme != "http") || !(new[] { "youtube.com", "www.youtube.com", "m.youtube.com", "music.youtube.com", "youtu.be" }.Contains(host)))
                    throw new Exception("Yalnızca YouTube video bağlantıları destekleniyor.");
                string id = host == "youtu.be" ? u.AbsolutePath.Trim('/') : System.Web.HttpUtility.ParseQueryString(u.Query)["v"];
                if (string.IsNullOrEmpty(id)) { var m = Regex.Match(u.AbsolutePath, "^/(?:shorts|live|embed)/([a-zA-Z0-9_-]{11})(?:/|$)"); if (m.Success) id = m.Groups[1].Value; }
                if (id == null || !Regex.IsMatch(id, "^[a-zA-Z0-9_-]{11}$")) throw new Exception("Bir video bağlantısı kullan; oynatma listesi bağlantısı desteklenmiyor.");
                return "https://www.youtube.com/watch?v=" + id;
            }
            return "ytsearch15:" + input;
        }
        public static async Task<ToolResult> Run(string exe, IEnumerable<string> args, CancellationToken token, int timeoutSeconds)
        {
            token.ThrowIfCancellationRequested();
            using (var p = new Process())
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds)))
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeout.Token))
            {
                p.StartInfo = new ProcessStartInfo(exe, string.Join(" ", args.Select(Quote))) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, WorkingDirectory = AppPaths.Root };
                p.Start();
                using (linked.Token.Register(() => { try { if (!p.HasExited) p.Kill(); } catch { } }))
                {
                    var output = p.StandardOutput.ReadToEndAsync();
                    var error = p.StandardError.ReadToEndAsync();
                    await Task.Run(() => p.WaitForExit());
                    var result = new ToolResult { Code = p.ExitCode, Output = await output, Error = await error };
                    token.ThrowIfCancellationRequested();
                    if (timeout.IsCancellationRequested) throw new SourceException("Bağlantı zaman aşımına uğradı.", true);
                    return result;
                }
            }
        }
        static List<string> Common()
        {
            var args = new List<string> { "--ignore-config", "--no-warnings", "--no-playlist", "--socket-timeout", "15", "--retries", "1", "--encoding", "utf-8" };
            string node = Node();
            if (node != null) { args.Add("--js-runtimes"); args.Add("node:" + node); }
            return args;
        }
        public static Task<List<Track>> Search(string text, CancellationToken token) { return YouTubeSearch.Search(text, token); }
        public static async Task<List<Track>> SearchLegacy(string text, CancellationToken token)
        {
            var args = Common();
            string target = Target(text);
            args.AddRange(new[] { "--flat-playlist", "--skip-download", "--dump-single-json", "--", target });
            var result = await Run(Bin("yt-dlp.exe"), args, token, 75);
            if (result.Code != 0) throw new SourceException(Friendly(result.Error), IsTransient(result.Error));
            var d = Json.Read(result.Output);
            var entries = Json.Get(d, "entries") as IEnumerable;
            var tracks = new List<Track>();
            if (entries != null) { foreach (var entry in entries) { var t = Track.From(entry as Dictionary<string, object>); if (t != null) tracks.Add(t); } }
            else { var t = Track.From(d); if (t != null) tracks.Add(t); }
            return tracks;
        }
        public static async Task<Dictionary<string, object>> Resolve(Track track, CancellationToken token, int attempt = 0)
        {
            token.ThrowIfCancellationRequested();
            if (track.IsLocal)
            {
                if (!File.Exists(track.LocalPath)) throw new FileNotFoundException("Yerel dosya bulunamadı. Dosyayı yeniden seçebilirsin.");
                return new Dictionary<string, object> { { "title", track.Title }, { "url", track.LocalPath } };
            }
            lock (streamLock) { StreamEntry entry; if (attempt == 0 && streams.TryGetValue(track.Id, out entry) && DateTime.UtcNow - entry.Created < TimeSpan.FromMinutes(5)) return entry.Data; }
            var args = Common();
            if (attempt > 0) args.Add("--check-formats");
            args.AddRange(new[] { "--remote-components", "ejs:github", "--skip-download", "--dump-single-json",
                "-f", attempt > 1 ? "bestaudio[protocol^=m3u8]/bestaudio/best" : "bestaudio/best", "--", track.Url });
            var result = await Run(Bin("yt-dlp.exe"), args, token, 90);
            if (result.Code != 0) throw new SourceException(Friendly(result.Error), IsTransient(result.Error));
            var d = Json.Read(result.Output);
            Uri url;
            if (!Uri.TryCreate(Json.Str(d, "url"), UriKind.Absolute, out url) || (url.Scheme != "https" && url.Scheme != "http")) throw new Exception("Bu videonun ses bağlantısı alınamadı.");
            lock (streamLock) { if (streams.Count >= 16) streams.Remove(streams.OrderBy(pair => pair.Value.Created).First().Key); streams[track.Id] = new StreamEntry { Created = DateTime.UtcNow, Data = d }; }
            return d;
        }
        public static void ForgetStream(string id) { lock (streamLock) streams.Remove(id); }
        public static void ClearStreams() { lock (streamLock) streams.Clear(); }
        public static async Task ProbeStream(Track track, CancellationToken token)
        {
            var stream = await Resolve(track, token, 1);
            using (var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, Json.Str(stream, "url")))
            {
                var headers = Json.Get(stream, "http_headers") as Dictionary<string, object>;
                if (headers != null)
                    foreach (var header in headers) request.Headers.TryAddWithoutValidation(header.Key, Convert.ToString(header.Value));
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 1023);
                using (var response = await Network.Client.SendAsync(request, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, token))
                {
                    response.EnsureSuccessStatusCode();
                    using (var body = await response.Content.ReadAsStreamAsync())
                        if (await body.ReadAsync(new byte[1], 0, 1, token) == 0) throw new IOException("Ses akışı boş döndü.");
                }
            }
        }
        static bool IsTransient(string error)
        {
            return Regex.IsMatch(error, @"timeout|timed out|network|connection|temporary|http error (403|429|5\d\d)|signature|nsig|could not resolve|failed to resolve|name resolution|getaddrinfo", RegexOptions.IgnoreCase);
        }
        public static string Friendly(string error)
        {
            string s = error.ToLowerInvariant();
            if (s.Contains("sign in") || s.Contains("bot")) return "YouTube bu bağlantıda doğrulama istiyor. Bir süre sonra tekrar dene veya başka video seç.";
            if (s.Contains("age") || s.Contains("private") || s.Contains("unavailable") || s.Contains("not available")) return "Bu video kullanılamıyor veya giriş/yaş doğrulaması istiyor. Başka bir sonuç seç.";
            if (s.Contains("403") || s.Contains("signature") || s.Contains("nsig")) return "YouTube bağlantısı reddedildi. Üstteki Güncelle düğmesini deneyebilirsin.";
            if (s.Contains("timed out") || s.Contains("resolve") || s.Contains("network") || s.Contains("connection")) return "YouTube’a ulaşılamadı. İnternet bağlantını kontrol edip tekrar dene.";
            return "İşlem tamamlanamadı. Başka bir video deneyebilir veya Güncelle düğmesini kullanabilirsin.";
        }
        public static string Time(double seconds)
        {
            if (seconds < 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return "0:00";
            var t = TimeSpan.FromSeconds(seconds);
            return t.TotalHours >= 1 ? ((int)t.TotalHours) + ":" + t.Minutes.ToString("00") + ":" + t.Seconds.ToString("00") : ((int)t.TotalMinutes) + ":" + t.Seconds.ToString("00");
        }
    }
}
