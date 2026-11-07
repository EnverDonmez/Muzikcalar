using System;
using System.IO;
using System.Text;

namespace SadeSes
{
    static class LibraryTests
    {
        static void Assert(bool condition, string message) { if (!condition) throw new Exception("Library test failed: " + message); }
        public static void Run(string report)
        {
            string folder = Path.Combine(Path.GetDirectoryName(report), "library-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "library.json"); var store = new LibraryStore(path); var library = store.Load();
            var track = new Track { Id = "n61ULEU7CO0", Title = "Türkçe Şarkı — İıĞğŞşÇçÖöÜü", Channel = "Sanatçı", Duration = 123 };
            var playlist = library.Create("  Gece müzikleri  "); string id = playlist.Id;
            Assert(library.Add(id, track), "first add"); Assert(!library.Add(id, track), "duplicate prevention"); library.ToggleLike(track); store.Save(library);
            var reloaded = new LibraryStore(path).Load(); Assert(reloaded.Find(id).Name == "Gece müzikleri" && reloaded.Find(id).Tracks[0].Title == track.Title && reloaded.Liked(track.Id), "restart roundtrip");
            reloaded.Rename(id, "Yol şarkıları"); store.Save(reloaded); Assert(File.Exists(path + ".bak"), "atomic backup");
            bool duplicate = false; try { reloaded.Create("YOL ŞARKILARI"); } catch { duplicate = true; } Assert(duplicate, "duplicate playlist name");
            File.WriteAllText(path, "broken-json"); var recovery = new LibraryStore(path); var recovered = recovery.Load(); Assert(recovery.Recovered && recovered.Find(id) != null && recovered.Liked(track.Id), "backup recovery");
            recovered.ToggleLike(track); recovered.Find(id).Tracks.RemoveAll(t => t.Id == track.Id); store.Save(recovered);
            var last = new LibraryStore(path).Load(); Assert(last.Favorites.Count == 0 && last.Find(id).Tracks.Count == 0, "removals persisted");
            last.Playlists.RemoveAll(p => p.Id == id); store.Save(last); Assert(new LibraryStore(path).Load().Playlists.Count == 0, "playlist deletion");
            var other = MusicLibrary.Decode("{\"version\":1,\"favorites\":[{\"id\":\"n61ULEU7CO0\",\"title\":\"Şarkı\",\"artist\":\"Sanatçı\",\"seconds\":42,\"cover\":\"https://i.ytimg.com/example.jpg\"}],\"playlists\":[]}");
            Assert(other.Favorites[0].Channel == "Sanatçı" && other.Favorites[0].Duration == 42, "legacy Android metadata");
            var local = Track.FromFile(Path.Combine(folder, "demo.wav"));
            other.Favorites.Add(local);
            Assert(other.Clone().Liked(local.Id), "local metadata");
            Assert(!MusicLibrary.Decode(other.Encode(true)).Liked(local.Id), "portable export excludes local paths");
            last.Merge(other); last.Merge(other);
            Assert(last.Favorites.Count == 1 && last.Liked(track.Id), "idempotent portable merge");
            File.WriteAllLines(report, new[] { "Create, rename and remove playlists: PASS", "Duplicate names and duplicate songs: PASS", "Favorites, Turkish metadata and playlist order survive reload: PASS", "Atomic save, backup recovery and persistent removals: PASS", "Android metadata, portable export and idempotent merge: PASS" }, new UTF8Encoding(false));
        }
    }
}
