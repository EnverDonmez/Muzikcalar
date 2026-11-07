using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SadeSes
{
    static class LocalPlaybackCheck
    {
        public static async Task Run(string report)
        {
            string audio = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report)), Guid.NewGuid().ToString("N") + ".wav");
            string broken = audio + ".wav";
            try
            {
                WriteWave(audio);
                File.WriteAllText(broken, "unsupported audio");
                Track track = Track.FromFile(audio);
                var source = await Tools.Resolve(track, CancellationToken.None);
                var bad = await Tools.Resolve(Track.FromFile(broken), CancellationToken.None);
                var failed = new TaskCompletionSource<bool>();
                var loaded = new TaskCompletionSource<bool>();
                var resumed = new TaskCompletionSource<bool>();
                using (var player = new AudioPlayer(true))
                {
                    player.Message += message => {
                        var version = Json.Num(message, "play_version");
                        string kind = Json.Str(message, "event");
                        if (version == 21 && kind == "end-file" && Json.Str(message, "reason") == "error")
                            failed.TrySetResult(true);
                        if (version == 23 && kind == "file-loaded") loaded.TrySetResult(true);
                        if (version == 23 && kind == "property-change" && Json.Str(message, "name") == "time-pos")
                        {
                            object value = Json.Get(message, "data");
                            if (value != null && Convert.ToDouble(value) >= 4) resumed.TrySetResult(true);
                        }
                    };
                    await player.Start();
                    player.Load(bad, 0, 21);
                    await Check(failed.Task, "corrupt audio error");
                    player.Load(source, 0, 22);
                    player.Send("stop");
                    player.Load(source, 0, 23, 4);
                    await Check(loaded.Task, "rapid replace request identity");
                    await Check(resumed.Task, "resume from saved position");
                }
                File.WriteAllLines(report, new[] {
                    "Local WAV decode with no network dependency: PASS",
                    "Corrupt source error tied to requested entry: PASS",
                    "Rapid replacement and resume at 4 seconds: PASS"
                }, new UTF8Encoding(false));
            }
            finally { File.Delete(audio); File.Delete(broken); }
        }
        static async Task Check(Task task, string name)
        {
            if (await Task.WhenAny(task, Task.Delay(10000)) != task) throw new TimeoutException(name);
            await task;
        }
        static void WriteWave(string path)
        {
            const int rate = 16000, length = rate * 12 * 2;
            using (var stream = File.Create(path))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + length);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(rate);
                writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(length);
                writer.Write(new byte[length]);
            }
        }
    }
}
