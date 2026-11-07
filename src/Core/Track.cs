using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SadeSes
{
    public class Track
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Channel { get; set; }
        public double Duration { get; set; }
        public string Thumbnail { get; set; }
        public string LocalPath { get; set; }
        public bool IsLocal { get { return !string.IsNullOrEmpty(LocalPath); } }
        public string ArtworkUrl { get { return IsLocal ? "" : string.IsNullOrEmpty(Thumbnail) ? "https://i.ytimg.com/vi/" + Id + "/mqdefault.jpg" : Thumbnail; } }
        public string Url { get { return IsLocal ? LocalPath : "https://www.youtube.com/watch?v=" + Id; } }
        public Track Copy() { return (Track)MemberwiseClone(); }
        public object Data() { return new { id = Id, title = Title, channel = Channel, duration = Duration, thumbnail = ArtworkUrl, local = LocalPath ?? "" }; }
        public static Track FromFile(string path)
        {
            string full = Path.GetFullPath(path);
            string id;
            using (var hash = System.Security.Cryptography.SHA256.Create())
                id = "local-" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(full.ToUpperInvariant()))).Replace("-", "").ToLowerInvariant();
            return new Track { Id = id, Title = Path.GetFileNameWithoutExtension(full), Channel = "Yerel dosya", LocalPath = full };
        }
        public static Track From(Dictionary<string, object> d)
        {
            string id = Json.Str(d, "id");
            string local = Json.Str(d, "local");
            if (Regex.IsMatch(id, "^local-[a-f0-9]{64}$") && Regex.IsMatch(local, @"^[A-Za-z]:[\\/]") && FromFile(local).Id == id)
                return new Track { Id = id, Title = Json.Str(d, "title"), Channel = "Yerel dosya", Duration = Json.Num(d, "duration"), LocalPath = local };
            if (!Regex.IsMatch(id, "^[a-zA-Z0-9_-]{11}$")) return null;
            string title = Json.Str(d, "title");
            return new Track { Id = id, Title = string.IsNullOrEmpty(title) ? id : title,
                Channel = First(d, "channel", "artist", "uploader"), Duration = Json.Get(d, "duration") != null ? Json.Num(d, "duration") : Json.Num(d, "seconds"), Thumbnail = First(d, "thumbnail", "cover") };
        }
        static string First(Dictionary<string, object> d, params string[] keys) { return keys.Select(k => Json.Str(d, k)).FirstOrDefault(v => v.Length > 0) ?? ""; }
        public override string ToString() { return Title; }
    }
}
