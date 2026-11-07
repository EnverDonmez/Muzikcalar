using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SadeSes
{
    public class Playlist
    {
        public string Id;
        public string Name;
        public readonly List<Track> Tracks = new List<Track>();
        public override string ToString() { return Name; }
    }

    public class MusicLibrary
    {
        public readonly List<Track> Favorites = new List<Track>();
        public readonly List<Playlist> Playlists = new List<Playlist>();
        static readonly StringComparer Names = StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), true);
        public Playlist Find(string id) { return Playlists.FirstOrDefault(p => p.Id == id); }
        public bool Liked(string id) { return Favorites.Any(t => t.Id == id); }
        public static string CleanName(string input)
        {
            string name = (input ?? "").Trim();
            if (name.Length == 0) throw new Exception("Playlist için bir ad yaz.");
            if (name.Length > 80 || name.Any(char.IsControl)) throw new Exception("Playlist adı en fazla 80 karakter olmalı ve tek satırda kalmalı.");
            return name;
        }
        public void CheckName(string input, string exceptId)
        {
            string name = CleanName(input);
            if (Playlists.Any(p => p.Id != exceptId && Names.Equals(p.Name, name))) throw new Exception("Bu adda bir playlist var. Başka bir ad seç.");
        }
        public Playlist Create(string input)
        {
            CheckName(input, null); var playlist = new Playlist { Id = Guid.NewGuid().ToString("N"), Name = CleanName(input) }; Playlists.Add(playlist); return playlist;
        }
        public void Rename(string id, string name) { CheckName(name, id); var playlist = Find(id); if (playlist == null) throw new Exception("Playlist bulunamadı."); playlist.Name = CleanName(name); }
        public bool Add(string id, Track track)
        {
            var playlist = Find(id); if (playlist == null) throw new Exception("Playlist bulunamadı.");
            if (playlist.Tracks.Any(t => t.Id == track.Id)) return false; playlist.Tracks.Add(track); return true;
        }
        public void ToggleLike(Track track) { if (Liked(track.Id)) Favorites.RemoveAll(t => t.Id == track.Id); else Favorites.Add(track); }
        public string Encode(bool portable = false)
        {
            return Json.Write(new { version = 1, favorites = Favorites.Where(t => !portable || !t.IsLocal).Select(t => t.Data()).ToArray(),
                playlists = Playlists.Select(p => new { id = p.Id, name = p.Name, tracks = p.Tracks.Where(t => !portable || !t.IsLocal).Select(t => t.Data()).ToArray() }).ToArray() });
        }
        public void Merge(MusicLibrary imported)
        {
            foreach (var track in imported.Favorites.Where(t => !t.IsLocal))
                if (!Liked(track.Id)) Favorites.Add(track);
            foreach (var source in imported.Playlists)
            {
                var target = Playlists.FirstOrDefault(p => Names.Equals(p.Name, source.Name)) ?? Create(source.Name);
                foreach (var track in source.Tracks.Where(t => !t.IsLocal)) Add(target.Id, track);
            }
        }
        public MusicLibrary Clone() { return Decode(Encode()); }
        public static MusicLibrary Decode(string text)
        {
            var data = Json.Read(text);
            if (Json.Num(data, "version") != 1) throw new InvalidDataException("Desteklenmeyen kitaplık sürümü.");
            var library = new MusicLibrary(); ReadTracks(Json.Get(data, "favorites"), library.Favorites);
            var playlists = Json.Get(data, "playlists") as IEnumerable;
            if (playlists != null) foreach (var entry in playlists)
            {
                var d = entry as Dictionary<string, object>; if (d == null) throw new InvalidDataException("Playlist kaydı okunamadı.");
                string id = Json.Str(d, "id"), name = CleanName(Json.Str(d, "name"));
                if (id.Length == 0 || library.Find(id) != null) throw new InvalidDataException("Playlist kimliği geçersiz.");
                var playlist = new Playlist { Id = id, Name = name }; ReadTracks(Json.Get(d, "tracks"), playlist.Tracks); library.Playlists.Add(playlist);
            }
            return library;
        }
        static void ReadTracks(object data, List<Track> target)
        {
            var items = data as IEnumerable; if (items == null) return;
            var ids = new HashSet<string>(); foreach (var item in items) { var track = Track.From(item as Dictionary<string, object>); if (track == null) throw new InvalidDataException("Şarkı kaydı okunamadı."); if (ids.Add(track.Id)) target.Add(track); }
        }
    }
}
