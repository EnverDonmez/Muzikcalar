using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace SadeSes
{
    public partial class MainForm
    {
        public void CheckInterface(string path)
        {
            Show(); Application.DoEvents();
            var lines = new List<string>();
            if (results.Count == 0) throw new Exception("No preview fixture for interface check");
            int original = results.Count; list.SelectedIndex = Math.Min(1, original - 1); string id = Selected.Id;
            AddSelected(); if (queue.Count != 1 || queue[0].Id != id) throw new Exception("Queue add failed");
            queueView = true; RefreshList(); if (list.Items.Count != 1 || Selected.Id != id) throw new Exception("Queue navigation failed");
            RemoveSelected(); if (queue.Count != 0 || list.Items.Count != 0 || list.Visible || !empty.Visible) throw new Exception("Queue removal or empty state failed");
            queueView = false; RefreshList(); if (list.Items.Count != original) throw new Exception("Search results were lost");
            lines.Add("Selection, add to queue, queue navigation and removal: PASS");
            CheckSession(lines);
            CheckMouseWheel(lines);
            CheckHoverFrames(lines);
            CheckLibraryInterface(lines);
            if (Text != "Müzikçalar" || brand.Text != "Müzikçalar") throw new Exception("Application name mismatch");
            if (!brand.Font.Name.StartsWith("Montserrat", StringComparison.OrdinalIgnoreCase) || !subtitle.Font.Name.StartsWith("Montserrat", StringComparison.OrdinalIgnoreCase)) throw new Exception("Bundled Montserrat font not loaded");
            string titleFace = Theme.RenderedFontFace(brand.Font), bodyFace = Theme.RenderedFontFace(subtitle.Font);
            if (!titleFace.StartsWith("Montserrat", StringComparison.OrdinalIgnoreCase) || !bodyFace.StartsWith("Montserrat", StringComparison.OrdinalIgnoreCase)) throw new Exception("Windows substituted a font: " + titleFace + "; " + bodyFace + "; registration=" + Theme.FontRegistration);
            if (TextRenderer.MeasureText(brand.Text, brand.Font, Size.Empty, TextFormatFlags.NoPadding).Width > brand.Width) throw new Exception("Application name does not fit");
            lines.Add("Müzikçalar window title and bundled Montserrat typography: PASS (" + titleFace + "; " + bodyFace + ")");
            foreach (var size in new[] { new Size(Theme.P(1280), Theme.P(810)), new Size(Theme.P(1070), Theme.P(790)), new Size(Theme.P(1070), Theme.P(690)) })
            {
                ClientSize = size; LayoutUi(); Application.DoEvents(); CheckBounds(this);
                if (nowChannel.Bounds.IntersectsWith(elapsed.Bounds)) throw new Exception("Player text overlaps the elapsed time");
                lines.Add("Visible control bounds at " + size + ": PASS");
            }
            File.WriteAllLines(path, lines, new UTF8Encoding(false)); Close();
        }
        void CheckSession(List<string> lines)
        {
            string previousState = EncodeSession();
            Track track = results[0], other = results[1];
            DecodeSession(Json.Write(new { volume = 37, queue = new[] { track.Data(), track.Data(), other.Data() } }));
            if (queue.Count != 3 || current != null || volumeLevel != 37) throw new Exception("Legacy queue restore failed");
            current = pendingQueueTrack = queue[1]; position = 13; repeating = true;
            recent.Add(other);
            DecodeSession(EncodeSession());
            if (!paused || !repeating || position != 13 || !ReferenceEquals(pendingQueueTrack, queue[1]) ||
                recent.Count != 1 || recent[0].Id != other.Id || queue.Count != 3)
                throw new Exception("Paused session restore failed");
            var retained = queue[0];
            CommitLoadedQueue();
            if (queue.Count != 2 || !ReferenceEquals(queue[0], retained)) throw new Exception("Wrong duplicate removed from queue");
            DecodeSession(previousState);
            Stop();
            lines.Add("Legacy and paused session restore, recent tracks and exact queued entry commit: PASS");
        }
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
        void CheckMouseWheel(List<string> lines)
        {
            if (list.Items.Count < 8) throw new Exception("Not enough results for wheel regression test");
            string selection = Selected.Id; ActiveControl = search; search.Focus(); list.TopIndex = 0;
            Point inside = list.PointToScreen(new Point(list.Width / 2, list.Height / 2));
            IntPtr position = new IntPtr((inside.X & 0xffff) | ((inside.Y & 0xffff) << 16));
            SendMessage(list.Handle, 0x20A, new IntPtr((long)(-120 & 0xffff) << 16), position);
            if (list.TopIndex == 0) throw new Exception("Mouse wheel down did not scroll");
            SendMessage(list.Handle, 0x20A, new IntPtr((long)120 << 16), position);
            if (list.TopIndex != 0) throw new Exception("Mouse wheel up did not scroll back");
            SendMessage(list.Handle, 0x20A, new IntPtr((long)(-60 & 0xffff) << 16), position);
            if (list.TopIndex != 0) throw new Exception("Partial wheel delta should accumulate");
            SendMessage(list.Handle, 0x20A, new IntPtr((long)(-60 & 0xffff) << 16), position);
            if (list.TopIndex == 0) throw new Exception("High-resolution wheel deltas were lost");
            list.TopIndex = 0;
            PostMessage(search.Handle, 0x20A, new IntPtr((long)(-120 & 0xffff) << 16), position); Application.DoEvents();
            if (list.TopIndex == 0 || ActiveControl != search) throw new Exception("Hover scrolling with search input selected failed: top=" + list.TopIndex + ", focus=" + search.Focused + ", activeControl=" + (ActiveControl == null ? "null" : ActiveControl.GetType().Name));
            int previous = list.TopIndex;
            Point outside = search.PointToScreen(new Point(search.Width / 2, search.Height / 2));
            PostMessage(search.Handle, 0x20A, new IntPtr((long)(-120 & 0xffff) << 16), new IntPtr((outside.X & 0xffff) | ((outside.Y & 0xffff) << 16))); Application.DoEvents();
            if (list.TopIndex != previous) throw new Exception("Wheel outside results scrolled the list");
            for (int i = 0; i < 12; i++) SendMessage(list.Handle, 0x20A, new IntPtr((long)(-120 & 0xffff) << 16), position);
            if (list.TopIndex <= 0 || list.TopIndex > list.Items.Count - 1) throw new Exception("Bottom scroll bound failed");
            for (int i = 0; i < 12; i++) SendMessage(list.Handle, 0x20A, new IntPtr((long)120 << 16), position);
            if (list.TopIndex != 0 || Selected.Id != selection) throw new Exception("Scroll bounds or selection preservation failed");
            lines.Add("Windows mouse-wheel messages: down, up and high-resolution deltas: PASS");
            lines.Add("Hover scrolling with search focused; outside-list routing and bounds: PASS");
        }
        void CheckHoverFrames(List<string> lines)
        {
            list.TopIndex = 0; int selected = list.SelectedIndex;
            using (var bitmap = new Bitmap(list.Width, list.Height))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Magenta); IntPtr dc = graphics.GetHdc();
                try { if (SendMessage(list.Handle, 0x14, dc, IntPtr.Zero) == IntPtr.Zero) throw new Exception("Native list background erase was not suppressed"); }
                finally { graphics.ReleaseHdc(dc); }
                if (bitmap.GetPixel(4, 4).ToArgb() != Color.Magenta.ToArgb()) throw new Exception("Native background erase cleared an intermediate frame");
            }
            int count = Math.Min(3, list.Items.Count); var originals = new Dictionary<string, Image>(); var replacements = new List<Image>();
            try
            {
                for (int i = 0; i < count; i++)
                {
                    var track = (Track)list.Items[i]; Image original; images.TryGetValue(track.Id, out original); originals[track.Id] = original;
                    var image = new Bitmap(160, 90); using (var graphics = Graphics.FromImage(image)) graphics.Clear(Color.CornflowerBlue); replacements.Add(image); images[track.Id] = image;
                }
                for (int frame = 0; frame < 30; frame++)
                {
                    var rect = list.GetItemRectangle(frame % count); int x = Theme.P(40), y = rect.Top + Theme.P(35);
                    SendMessage(list.Handle, 0x200, IntPtr.Zero, new IntPtr((x & 0xffff) | ((y & 0xffff) << 16)));
                    using (var bitmap = new Bitmap(list.Width, list.Height))
                    {
                        list.DrawToBitmap(bitmap, new Rectangle(Point.Empty, list.Size));
                        for (int i = 0; i < count; i++) if (bitmap.GetPixel(Theme.P(25), list.GetItemRectangle(i).Top + Theme.P(30)).ToArgb() != Color.CornflowerBlue.ToArgb()) throw new Exception("Hover frame lost a cover image");
                    }
                    if (list.TopIndex != 0 || list.SelectedIndex != selected) throw new Exception("Hover moved the result list");
                }
            }
            finally
            {
                foreach (var pair in originals) { if (pair.Value == null) images.Remove(pair.Key); else images[pair.Key] = pair.Value; }
                foreach (var image in replacements) image.Dispose(); list.Invalidate();
            }
            lines.Add("Buffered hover rendering: 30 mouse-move frames retain covers, positions and selection; no native erase: PASS");
        }
        static void CheckBounds(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                if (!child.Visible) continue;
                if (child is SoftButton || child is Slider || child is TextBox || child is CoverView)
                    if (child.Left < 0 || child.Top < 0 || child.Right > parent.ClientSize.Width + 1 || child.Bottom > parent.ClientSize.Height + 1) throw new Exception("Clipped control: " + child.AccessibleName + " in " + parent.GetType().Name);
                CheckBounds(child);
            }
        }
        public void RenderPreview(string path)
        {
            Show(); Application.DoEvents();
            foreach (var track in results.Take(10))
            {
                string imagePath = Thumbnails.PathFor(track);
                if (File.Exists(imagePath) && !images.ContainsKey(track.Id)) using (var stream = new MemoryStream(File.ReadAllBytes(imagePath))) using (var image = Image.FromStream(stream)) images[track.Id] = new Bitmap(image);
            }
            ShowDetail(); list.Invalidate(); Application.DoEvents();
            using (var bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height)); bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png); }
            File.WriteAllText(path + ".layout.txt", "Scale=" + Theme.Scale + "\nClient=" + ClientSize + "\nList=" + list.Bounds + "\nInspector=" + inspector.Bounds + "\nSearch=" + search.Bounds + "\nThumbnails=" + images.Count + "\nTitle font=" + brand.Font.Name + " / " + brand.Font.Style + "\nBody font=" + subtitle.Font.Name); Close();
        }
        void CheckLibraryInterface(List<string> lines)
        {
            var previousLibrary = library; var previousView = view; var previousQueue = queue.ToList(); string previousId = playlistId;
            try
            {
                library = new MusicLibrary(); view = LibraryView.Search; RefreshList(); list.SelectedIndex = 0; Track track = Selected;
                ToggleFavorite(); if (!library.Liked(track.Id)) throw new Exception("Favorite action failed");
                view = LibraryView.Favorites; RefreshList(); if (list.Items.Count != 1 || Selected.Id != track.Id) throw new Exception("Favorites navigation failed");
                string id = null;
                if (!CommitLibrary(data => { var created = data.Create("Test playlist"); id = created.Id; data.Add(id, track); data.Add(id, results[1]); })) throw new Exception("Playlist commit failed");
                view = LibraryView.Playlists; RefreshList(); if (playlistList.Items.Count != 1 || !playlistList.Visible || list.Visible) throw new Exception("Playlist overview failed");
                OpenSelectedPlaylist(); if (view != LibraryView.PlaylistTracks || list.Items.Count != 2) throw new Exception("Playlist open failed");
                list.SelectedIndex = 0; pendingCollection = CollectionAfter(0); CommitLoadedQueue();
                if (queue.Count != 1 || queue[0].Id != results[1].Id) throw new Exception("Playlist playback order failed");
                RemoveSavedTrack(); if (library.Find(id).Tracks.Count != 1) throw new Exception("Playlist track removal failed");
                view = LibraryView.Favorites; RefreshList(); ToggleFavorite(); if (library.Favorites.Count != 0 || list.Items.Count != 0) throw new Exception("Favorite removal failed");
                lines.Add("Favorites, playlist overview/open, track removal and playlist queue order: PASS");
                var size = ClientSize;
                foreach (var page in new[] { LibraryView.Playlists, LibraryView.PlaylistTracks, LibraryView.Favorites })
                {
                    view = page; playlistId = id; RefreshList();
                    foreach (var window in new[] { new Size(Theme.P(1280), Theme.P(810)), new Size(Theme.P(1070), Theme.P(690)) }) { ClientSize = window; LayoutUi(); CheckBounds(this); }
                    lines.Add("Library page bounds " + page + " at normal/minimum window size: PASS");
                }
                ClientSize = size; LayoutUi();
            }
            finally { library = previousLibrary; view = previousView; playlistId = previousId; queue.Clear(); queue.AddRange(previousQueue); RefreshList(); }
        }
        public void RenderLibraryPreview(string path, string page)
        {
            if (!preview) throw new InvalidOperationException();
            library = new MusicLibrary();
            var first = library.Create("Gece modu"); var second = library.Create("Yol müzikleri");
            foreach (var track in results.Take(3)) { library.Add(first.Id, track); library.ToggleLike(track); }
            foreach (var track in results.Skip(2).Take(4)) library.Add(second.Id, track);
            view = page == "favorites" ? LibraryView.Favorites : page == "playlist" ? LibraryView.PlaylistTracks : LibraryView.Playlists; playlistId = first.Id; RefreshList(); RenderPreview(path);
        }
    }
}
