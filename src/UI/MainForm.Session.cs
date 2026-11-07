using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SadeSes
{
    public partial class MainForm
    {
        void LoadState()
        {
            if (preview) return;
            try
            {
                if (!File.Exists(StatePath)) return;
                DecodeSession(File.ReadAllText(StatePath, Encoding.UTF8));
            }
            catch { SetStatus("Kaydedilen sıra okunamadı. Yeni bir sıra oluşturabilirsin.", true); }
        }
        void DecodeSession(string text)
        {
            var state = Json.Read(text);
            if (Json.Get(state, "volume") != null) volumeLevel = Math.Max(0, Math.Min(100, (int)Json.Num(state, "volume")));
            volume.UpdateValue(volumeLevel);
            queue.Clear(); recent.Clear(); skipped.Clear(); pendingQueueTrack = null; pendingCollection = null;
            ReadSessionTracks(Json.Get(state, "queue"), queue);
            ReadSessionTracks(Json.Get(state, "recent"), recent);
            SetRepeat(Json.Get(state, "repeat") is bool && (bool)Json.Get(state, "repeat"));
            current = Track.From(Json.Get(state, "current") as Dictionary<string, object>);
            if (current != null)
            {
                int pending = Json.Get(state, "pending") == null ? -1 : (int)Json.Num(state, "pending");
                if (pending >= 0 && pending < queue.Count && queue[pending].Id == current.Id) pendingQueueTrack = queue[pending];
                if (Json.Get(state, "collection") != null)
                {
                    pendingCollection = new List<Track>();
                    ReadSessionTracks(Json.Get(state, "collection"), pendingCollection);
                }
                paused = true; ShowCurrent(Math.Max(0, Json.Num(state, "position")));
            }
            RefreshList();
        }
        static void ReadSessionTracks(object value, List<Track> target)
        {
            var entries = value as IEnumerable;
            if (entries == null) return;
            foreach (var entry in entries)
            {
                var track = Track.From(entry as Dictionary<string, object>);
                if (track != null) target.Add(track);
            }
        }
        void SaveState()
        {
            if (preview) return;
            try
            {
                LibraryStore.WriteAtomic(StatePath, EncodeSession());
                savedPosition = position;
            }
            catch { if (!closing) SetStatus("Sıra kaydedilemedi. Klasörün yazılabilir olduğunu kontrol et.", true); }
        }
        string EncodeSession()
        {
            return Json.Write(new {
                volume = volumeLevel, queue = queue.Select(t => t.Data()).ToArray(),
                current = current == null ? null : current.Data(), position = position, repeat = repeating,
                pending = pendingQueueTrack == null ? -1 : queue.IndexOf(pendingQueueTrack),
                collection = pendingCollection == null ? null : pendingCollection.Select(t => t.Data()).ToArray(),
                recent = recent.Select(t => t.Data()).ToArray()
            });
        }
    }
}
