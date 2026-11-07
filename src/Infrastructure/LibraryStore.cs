using System;
using System.IO;
using System.Text;

namespace SadeSes
{
    public class LibraryStore
    {
        readonly string path;
        public bool Recovered { get; private set; }
        public LibraryStore(string file) { path = file; }
        public MusicLibrary Load()
        {
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return new MusicLibrary();
            try { return MusicLibrary.Decode(File.ReadAllText(path, Encoding.UTF8)); }
            catch
            {
                if (!File.Exists(path + ".bak")) throw;
                var backup = MusicLibrary.Decode(File.ReadAllText(path + ".bak", Encoding.UTF8)); Recovered = true; return backup;
            }
        }
        public void Save(MusicLibrary library)
        {
            string json = library.Encode(); MusicLibrary.Decode(json);
            WriteAtomic(path, json, Recovered ? null : path + ".bak");
            Recovered = false;
        }
        public static void WriteAtomic(string path, string json, string backup = null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)); string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, json, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temp, path, backup); else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
