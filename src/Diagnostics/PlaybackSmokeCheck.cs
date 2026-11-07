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
    static class SelfTest
    {
        static void Assert(bool condition, string text) { if (!condition) throw new Exception("TEST FAILED: " + text); }
        public static async Task Run(string reportPath)
        {
            var checks = new List<string>();
            Assert(Tools.Target("lofi hip hop") == "ytsearch15:lofi hip hop", "search target");
            Assert(Tools.Target("https://youtu.be/n61ULEU7CO0?t=3") == "https://www.youtube.com/watch?v=n61ULEU7CO0", "URL normalization");
            bool rejected = false; try { Tools.Target("https://example.com/file"); } catch { rejected = true; } Assert(rejected, "unsupported host rejected"); checks.Add("Input validation: PASS");
            using (var cancelled = new CancellationTokenSource()) { cancelled.Cancel(); bool caught = false; try { await Tools.Search("lofi", cancelled.Token); } catch (OperationCanceledException) { caught = true; } Assert(caught, "cancelled search"); } checks.Add("Search cancellation: PASS");
            var tracks = await Tools.Search("lofi hip hop", CancellationToken.None); Assert(tracks.Count > 0, "live search"); checks.Add("Live YouTube search: PASS (" + tracks.Count + " results)");
            var track = new Track { Id = "n61ULEU7CO0" };
            using (var audio = new AudioPlayer())
            {
                int version = 1;
                var loaded = new TaskCompletionSource<bool>(); var progressed = new TaskCompletionSource<bool>(); var pause = new TaskCompletionSource<bool>(); var resumed = new TaskCompletionSource<bool>(); var seek = new TaskCompletionSource<bool>(); var volume = new TaskCompletionSource<bool>(); var stopped = new TaskCompletionSource<bool>();
                audio.Message += m => {
                    string ev = Json.Str(m, "event"), name = Json.Str(m, "name");
                    if (ev == "file-loaded" && Json.Num(m, "play_version") == version) loaded.TrySetResult(true);
                    if (ev == "end-file" && Json.Str(m, "reason") == "error" && Json.Num(m, "play_version") == version) loaded.TrySetResult(false);
                    if (ev == "end-file" && Json.Str(m, "reason") == "stop") stopped.TrySetResult(true);
                    if (ev == "property-change" && name == "time-pos" && Json.Num(m, "data") > 1) progressed.TrySetResult(true);
                    if (ev == "property-change" && name == "time-pos" && Json.Num(m, "data") >= 10) seek.TrySetResult(true);
                    if (ev == "property-change" && name == "pause" && Json.Str(m, "data").ToLowerInvariant() == "true") pause.TrySetResult(true);
                    if (ev == "property-change" && name == "pause" && pause.Task.IsCompleted && Json.Str(m, "data").ToLowerInvariant() == "false") resumed.TrySetResult(true);
                    if (ev == "property-change" && name == "volume" && Json.Get(m, "data") != null && Json.Num(m, "data") == 0) volume.TrySetResult(true);
                };
                await Task.WhenAll(audio.Start(), audio.Start()); checks.Add("Concurrent audio engine startup: PASS");
                var data = await Tools.Resolve(track, CancellationToken.None); Assert(Json.Str(data, "url").Length > 0, "resolve"); checks.Add("Live audio resolution: PASS (" + track.Id + ")");
                var cacheTimer = Stopwatch.StartNew(); var cachedData = await Tools.Resolve(track, CancellationToken.None); Assert(Json.Str(cachedData, "url") == Json.Str(data, "url"), "stream cache URL"); checks.Add("Repeated audio resolution: PASS (" + cacheTimer.ElapsedMilliseconds + " ms)");
                bool opened = false;
                for (int attempt = 0; attempt < 3 && !opened; attempt++)
                {
                    if (attempt > 0)
                    {
                        version++;
                        loaded = new TaskCompletionSource<bool>();
                        data = await Tools.Resolve(track, CancellationToken.None, attempt);
                    }
                    audio.Load(data, 0, version);
                    opened = await Task.WhenAny(loaded.Task, Task.Delay(20000)) == loaded.Task && await loaded.Task;
                }
                Assert(opened, "mpv audio stream could not be opened");
                checks.Add("mpv audio stream opened (muted): PASS (" + version + " attempt(s))");
                Assert(await Task.WhenAny(progressed.Task, Task.Delay(15000)) == progressed.Task, "playback progress"); checks.Add("Audio decoding and playback progress: PASS");
                Assert(await Task.WhenAny(volume.Task, Task.Delay(3000)) == volume.Task, "volume"); checks.Add("Volume control (mute): PASS");
                audio.Send("set_property", "pause", true); Assert(await Task.WhenAny(pause.Task, Task.Delay(3000)) == pause.Task, "pause"); checks.Add("Pause: PASS");
                audio.Send("seek", 10, "absolute"); Assert(await Task.WhenAny(seek.Task, Task.Delay(8000)) == seek.Task, "seek"); checks.Add("Seek: PASS");
                audio.Send("set_property", "pause", false); Assert(await Task.WhenAny(resumed.Task, Task.Delay(3000)) == resumed.Task, "resume"); checks.Add("Resume: PASS");
                audio.Send("stop"); Assert(await Task.WhenAny(stopped.Task, Task.Delay(3000)) == stopped.Task, "stop"); checks.Add("Stop: PASS");
            }
            File.WriteAllText(reportPath, string.Join(Environment.NewLine, checks) + Environment.NewLine, new UTF8Encoding(false));
        }
    }
}
