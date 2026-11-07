using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SadeSes
{
    public class AudioPlayer : IDisposable
    {
        Process process;
        NamedPipeClientStream pipe;
        StreamWriter writer;
        readonly object gate = new object();
        readonly SemaphoreSlim startGate = new SemaphoreSlim(1, 1);
        volatile bool disposed;
        int loadRequest;
        readonly bool silentOutput;
        public event Action<Dictionary<string, object>> Message;
        public AudioPlayer(bool silentOutput = false) { this.silentOutput = silentOutput; }
        public async Task Start()
        {
            await startGate.WaitAsync();
            try { await StartCore(); } finally { startGate.Release(); }
        }
        async Task StartCore()
        {
            if (disposed) throw new ObjectDisposedException("AudioPlayer");
            if (process != null && !process.HasExited && pipe != null && pipe.IsConnected) return;
            CloseEngine();
            string name = "SadeSes-" + Guid.NewGuid().ToString("N");
            var args = new List<string> { "--no-config", "--load-scripts=no", "--idle=yes", "--keep-open=no", "--vid=no", "--audio-display=no", "--force-window=no", "--terminal=no", "--input-terminal=no", "--ytdl=no", "--volume=70", "--network-timeout=20", "--input-ipc-server=\\\\.\\pipe\\" + name };
            if (silentOutput) args.Add("--ao=null");
            string diagnosticLog = Environment.GetEnvironmentVariable("SADESES_DIAGNOSTIC_LOG"); if (!string.IsNullOrEmpty(diagnosticLog)) args.Add("--log-file=" + diagnosticLog);
            process = Process.Start(new ProcessStartInfo(Tools.Bin("mpv.exe"), string.Join(" ", args.Select(Tools.Quote))) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = AppPaths.Root });
            pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
            var localPipe = pipe;
            try { await Task.Run(() => localPipe.Connect(10000)); }
            catch { CloseEngine(); throw; }
            if (disposed) throw new ObjectDisposedException("AudioPlayer");
            writer = new StreamWriter(localPipe, new UTF8Encoding(false), 4096, true) { AutoFlush = true };
            Task ignored = Task.Run(async () => {
                long entry = 0, startedEntry = 0;
                int version = 0, startedVersion = 0;
                try
                {
                    using (var reader = new StreamReader(localPipe, Encoding.UTF8, false, 4096, true))
                    {
                        string line;
                        while (!disposed && (line = await reader.ReadLineAsync()) != null)
                        {
                            try
                            {
                                var message = Json.Read(line);
                                var handler = Message;
                                int request = (int)Json.Num(message, "request_id");
                                string nameOfEvent = Json.Str(message, "event");
                                if (request > 0)
                                {
                                    entry = (long)Json.Num(Json.Get(message, "data") as Dictionary<string, object>, "playlist_entry_id");
                                    version = request;
                                    if (entry == 0)
                                    {
                                        message["event"] = "load-error"; message["play_version"] = request;
                                        if (handler != null) handler(message);
                                    }
                                    continue;
                                }
                                if (nameOfEvent == "start-file")
                                {
                                    startedEntry = (long)Json.Num(message, "playlist_entry_id");
                                    startedVersion = startedEntry == entry ? version : 0;
                                }
                                message["play_version"] = nameOfEvent == "end-file" && Json.Num(message, "playlist_entry_id") != startedEntry ? 0 : startedVersion;
                                if (handler != null) handler(message);
                            }
                            catch { }
                        }
                    }
                }
                catch { }
                if (!disposed && ReferenceEquals(localPipe, pipe))
                {
                    var handler = Message;
                    if (handler != null) handler(new Dictionary<string, object> { { "event", "engine-disconnected" }, { "play_version", version } });
                }
            });
            int index = 0;
            foreach (string property in new[] { "time-pos", "duration", "pause", "paused-for-cache", "volume" }) Send("observe_property", ++index, property);
        }
        public void Send(params object[] command)
        {
            lock (gate)
            {
                if (disposed || writer == null) return;
                writer.WriteLine(Json.Write(new { command = command }));
            }
        }
        public void Load(Dictionary<string, object> data, int volume, int version = 0, double position = 0)
        {
            lock (gate)
            {
                if (disposed || writer == null) throw new IOException("Ses motoruyla bağlantı kesildi.");
                Send("set_property", "force-media-title", Json.Str(data, "title"));
                Send("set_property", "volume", volume);
                Send("set_property", "pause", false);
                var headers = Json.Get(data, "http_headers") as Dictionary<string, object>;
                var fields = headers == null ? new string[0] : headers
                    .Where(h => !h.Key.Equals("Range", StringComparison.OrdinalIgnoreCase) && !Convert.ToString(h.Value).Contains("\r") && !Convert.ToString(h.Value).Contains("\n"))
                    .Select(h => h.Key + ": " + Convert.ToString(h.Value)).ToArray();
                Send("set_property", "http-header-fields", fields);
                var command = new object[] { "loadfile", Json.Str(data, "url"), "replace", -1, "start=" + position.ToString("0.###", CultureInfo.InvariantCulture) };
                writer.WriteLine(Json.Write(new { command = command, request_id = version > 0 ? version : ++loadRequest }));
            }
        }
        void CloseEngine()
        {
            var oldPipe = pipe; pipe = null; writer = null;
            if (oldPipe != null) oldPipe.Dispose();
            if (process == null) return;
            try { if (!process.HasExited && !process.WaitForExit(1000)) process.Kill(); } catch { }
            process.Dispose(); process = null;
        }
        public void Dispose()
        {
            if (disposed) return;
            try { Send("quit"); } catch { }
            disposed = true;
            CloseEngine();
        }
    }
}
