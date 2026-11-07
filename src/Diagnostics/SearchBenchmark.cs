using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SadeSes
{
    static class PerformanceTest
    {
        static void Assert(bool condition, string name) { if (!condition) throw new Exception("FAILED: " + name); }
        public static async Task Check(string path)
        {
            var lines = new List<string>();
            await CheckFixture(lines);
            File.WriteAllLines(path, lines, new UTF8Encoding(false));
        }
        static async Task CheckFixture(List<string> lines)
        {
            string fixture = Path.Combine(AppPaths.Fixtures, "youtube-search.html");
            var parsed = YouTubeSearch.ParsePage(File.ReadAllText(fixture));
            Assert(parsed.Count == 2 && parsed[0].Title == "Gece {Canlı}" && parsed[0].Duration == 183, "page parser");
            Assert(parsed[1].Channel == "Deneme Sanatçısı" && parsed.All(t => t.ArtworkUrl.StartsWith("https://i.ytimg.com/")), "result metadata");
            Assert(YouTubeSearch.Decode(YouTubeSearch.Encode(parsed)).Select(t => t.Id).SequenceEqual(parsed.Select(t => t.Id)), "cache roundtrip");
            lines.Add("Page parser, thumbnail metadata and cache roundtrip: PASS");
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel(); bool cancelled = false; try { await Tools.Search("cancelled query", cancel.Token); } catch (OperationCanceledException) { cancelled = true; }
                Assert(cancelled, "cancellation"); lines.Add("Search cancellation: PASS");
            }
        }
        public static async Task Run(string path)
        {
            var lines = new List<string>();
            await CheckFixture(lines);
            string query = "Daft Punk Instant Crush";
            var timer = Stopwatch.StartNew(); var original = await Tools.SearchLegacy(query, CancellationToken.None); double oldTime = timer.Elapsed.TotalSeconds;
            timer.Restart(); var tracks = await Tools.Search(query, CancellationToken.None); double newTime = timer.Elapsed.TotalSeconds;
            Assert(tracks.Count > 0, "live search");
            timer.Restart(); var cached = await Tools.Search(query, CancellationToken.None); double cachedTime = timer.Elapsed.TotalSeconds;
            Assert(cached.Select(t => t.Id).SequenceEqual(tracks.Select(t => t.Id)), "cached IDs");
            lines.Add("Existing search: " + oldTime.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " s");
            lines.Add("New search: " + newTime.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " s (" + tracks.Count + " results)");
            lines.Add("Repeated search: " + cachedTime.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " s");
            await Task.WhenAll(tracks.Take(8).Select(t => Thumbnails.Get(t, CancellationToken.None)));
            lines.Add("Eight real YouTube thumbnails downloaded and decoded: PASS");
            var direct = await Tools.Search(tracks[0].Url, CancellationToken.None); Assert(direct.Count == 1 && direct[0].Id == tracks[0].Id, "direct YouTube URL");
            lines.Add("Direct YouTube video URL: PASS");
            File.WriteAllLines(path, lines, new UTF8Encoding(false));
        }
    }
}
