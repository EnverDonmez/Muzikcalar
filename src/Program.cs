using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace SadeSes
{
    static class Program
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [STAThread]
        static int Main(string[] args)
        {
            SetProcessDPIAware(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                if (args.Length >= 2 && args[0] == "--preview") { using (var f = new MainForm(true)) f.RenderPreview(args[1]); return 0; }
                if (args.Length >= 2 && args[0] == "--benchmark") { PerformanceTest.Run(args[1]).GetAwaiter().GetResult(); return 0; }
                if (args.Length >= 2 && args[0] == "--search-check") { PerformanceTest.Check(args[1]).GetAwaiter().GetResult(); return 0; }
                if (args.Length >= 2 && args[0] == "--ui-check") { using (var f = new MainForm(true)) f.CheckInterface(args[1]); return 0; }
                if (args.Length >= 2 && args[0] == "--visual-check") { VisualChecks.Run(args[1]); return 0; }
                if (args.Length >= 2 && args[0] == "--library-check") { LibraryTests.Run(args[1]); return 0; }
                if (args.Length >= 2 && args[0] == "--local-playback-check") { LocalPlaybackCheck.Run(args[1]).GetAwaiter().GetResult(); return 0; }
                if (args.Length >= 2 && args[0] == "--self-test") { SelfTest.Run(args[1]).GetAwaiter().GetResult(); return 0; }
                bool created;
                using (var mutex = new Mutex(true, "Local\\SadeSes-" + Math.Abs(AppPaths.Root.GetHashCode()), out created))
                {
                    if (!created) { MessageBox.Show("Müzikçalar zaten açık. Görev çubuğundaki penceresine geçebilirsin.", "Müzikçalar"); return 0; }
                    if (!File.Exists(Tools.Bin("yt-dlp.exe")) || !File.Exists(Tools.Bin("mpv.exe"))) { MessageBox.Show("bin klasörü eksik. Muzikcalar.exe dosyasını bin klasörüyle birlikte tut.", "Müzikçalar"); return 1; }
                    Application.Run(new MainForm(false));
                }
                return 0;
            }
            catch (Exception ex) { if (args.Length >= 2 && args[0].StartsWith("--")) { File.WriteAllText(args[1] + ".failed.txt", ex.ToString()); return 1; } File.WriteAllText(Path.Combine(AppPaths.Root, "hata.txt"), ex.ToString()); MessageBox.Show("Program açılamadı. Ayrıntılar hata.txt dosyasına yazıldı.", "Müzikçalar"); return 1; }
        }
    }
}
