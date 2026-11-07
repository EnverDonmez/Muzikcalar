using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SadeSes
{
    enum LibraryView { Search, Queue, Favorites, Playlists, PlaylistTracks, Recent }

    public partial class MainForm
    {
        LibraryView view = LibraryView.Search;
        string playlistId, playlistFilter = "";
        MusicLibrary library = new MusicLibrary();
        readonly LibraryStore libraryStore = new LibraryStore(Path.Combine(AppPaths.Data, "library.json"));
        bool libraryWritable = true, refreshingPlaylists;
        string libraryProblem;
        readonly SmoothList playlistList = new SmoothList();
        readonly SoftButton favoritesButton = new SoftButton(), playlistsButton = new SoftButton(), saveButton = new SoftButton(), likeButton = new SoftButton(), createPlaylistButton = new SoftButton(), managePlaylistButton = new SoftButton();
        readonly SoftButton recentButton = new SoftButton(), toolsButton = new SoftButton();
        bool queueView { get { return view == LibraryView.Queue; } set { view = value ? LibraryView.Queue : LibraryView.Search; } }
        SmoothList ActiveList { get { return view == LibraryView.Playlists ? playlistList : list; } }
        Playlist SelectedPlaylist { get { var selected = playlistList.SelectedItem as Playlist; return view == LibraryView.Playlists ? (selected == null ? null : library.Find(selected.Id)) : view == LibraryView.PlaylistTracks ? library.Find(playlistId) : null; } }
        List<Track> CurrentTracks()
        {
            if (view == LibraryView.Queue) return queue;
            if (view == LibraryView.Recent) return recent;
            if (view == LibraryView.Favorites) return library.Favorites;
            if (view == LibraryView.PlaylistTracks)
            {
                var playlist = library.Find(playlistId);
                if (playlist == null) return new List<Track>();
                if (playlistFilter.Length == 0) return playlist.Tracks;
                var culture = CultureInfo.GetCultureInfo("tr-TR").CompareInfo;
                return playlist.Tracks.Where(t => culture.IndexOf(t.Title + " " + t.Channel, playlistFilter, CompareOptions.IgnoreCase) >= 0).ToList();
            }
            if (view == LibraryView.Playlists) return new List<Track>();
            return results;
        }

        void BuildLibraryInterface()
        {
            sidebar.Controls.AddRange(new Control[] { favoritesButton, playlistsButton, recentButton, toolsButton });
            content.Controls.AddRange(new Control[] { playlistList, saveButton, likeButton, createPlaylistButton, managePlaylistButton });
            ButtonStyle(favoritesButton, "Beğendiklerim", "heart", false); favoritesButton.BackColor = Theme.Sidebar; favoritesButton.TabIndex = 2;
            ButtonStyle(playlistsButton, "Playlistlerim", "library", false); playlistsButton.BackColor = Theme.Sidebar; playlistsButton.TabIndex = 3;
            ButtonStyle(recentButton, "Son dinlenenler", "", false); recentButton.BackColor = Theme.Sidebar; recentButton.TabIndex = 5;
            ButtonStyle(toolsButton, "Dosya ve kitaplık", "", false); toolsButton.BackColor = Theme.Sidebar; toolsButton.TabIndex = 6;
            favoritesButton.Click += (s, e) => { view = LibraryView.Favorites; RefreshList(); };
            playlistsButton.Click += (s, e) => { view = LibraryView.Playlists; RefreshList(); };
            recentButton.Click += (s, e) => { view = LibraryView.Recent; RefreshList(); };
            toolsButton.Click += (s, e) => ShowFileMenu();
            ButtonStyle(saveButton, "Playlist’e ekle", "plus", false); saveButton.Click += (s, e) => ShowSaveMenu(saveButton); saveButton.TabIndex = 5;
            ButtonStyle(likeButton, "", "heart", false); likeButton.Click += (s, e) => ToggleFavorite(); likeButton.TabIndex = 6;
            ButtonStyle(createPlaylistButton, "Yeni playlist", "plus", false); createPlaylistButton.Click += (s, e) => CreatePlaylistDialog(null);
            ButtonStyle(managePlaylistButton, "Düzenle", "", false); managePlaylistButton.Click += (s, e) => ShowManageMenu(managePlaylistButton);
            playlistList.BackColor = Theme.Bg; playlistList.ForeColor = Theme.Text; playlistList.BorderStyle = BorderStyle.None; playlistList.IntegralHeight = false; playlistList.DrawMode = DrawMode.OwnerDrawFixed; playlistList.ItemHeight = Theme.P(90); playlistList.AccessibleName = "Kayıtlı playlistler"; playlistList.TabIndex = 2;
            playlistList.DrawItem += DrawPlaylist; playlistList.DoubleClick += (s, e) => OpenSelectedPlaylist();
            playlistList.SelectedIndexChanged += (s, e) => { if (!refreshingPlaylists) { SelectionButtons(); ShowDetail(); LoadSelectedPlaylistCover(); } };
            playlistList.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; OpenSelectedPlaylist(); } else if (e.KeyCode == Keys.F2) RenamePlaylist(); };
            playlistList.ViewChanged += () => { scroll.Invalidate(); LoadSelectedPlaylistCover(); };
            var context = NewMenu();
            context.Opening += (s, e) => {
                context.Items.Clear(); var track = Selected; if (track == null) { e.Cancel = true; return; }
                context.Items.Add("Şimdi çal", null, async (a, b) => await PlaySelected());
                if (view == LibraryView.PlaylistTracks || view == LibraryView.Favorites)
                    context.Items.Add("Bu parçadan başlayarak listeyi çal", null, async (a, b) => await PlaySelected(true));
                context.Items.Add(library.Liked(track.Id) ? "Beğeniyi kaldır" : "Beğen", null, (a, b) => ToggleFavorite());
                context.Items.Add("Playlist’e ekle…", null, (a, b) => ShowSaveMenu(saveButton));
                context.Items.Add("Sırada sonraki çal", null, (a, b) => AddSelected(true));
                context.Items.Add("Sıranın sonuna ekle", null, (a, b) => AddSelected());
                if (view == LibraryView.PlaylistTracks || view == LibraryView.Queue || view == LibraryView.Favorites) context.Items.Add("Bu listeden çıkar", null, (a, b) => RemoveSelected());
                if (view == LibraryView.PlaylistTracks || view == LibraryView.Queue)
                {
                    var tracks = view == LibraryView.Queue ? queue : SelectedPlaylist.Tracks;
                    int index = tracks.IndexOf(track);
                    if (index > 0) context.Items.Add("Yukarı taşı", null, (a, b) => MoveSelected(-1));
                    if (index >= 0 && index + 1 < tracks.Count) context.Items.Add("Aşağı taşı", null, (a, b) => MoveSelected(1));
                }
                context.Items.Add("Tam parça bilgisi", null, (a, b) => ShowFullDetails());
            };
            list.ContextMenuStrip = context;
            list.MouseDown += (s, e) => { if (e.Button == MouseButtons.Right) { int index = list.IndexFromPoint(e.Location); if (index >= 0) list.SelectedIndex = index; } };
            var playlistContext = NewMenu(); playlistContext.Items.Add("Playlist’i aç", null, (s, e) => OpenSelectedPlaylist()); playlistContext.Items.Add("Yeniden adlandır", null, (s, e) => RenamePlaylist()); playlistContext.Items.Add("Playlist’i sil", null, (s, e) => DeletePlaylist()); playlistList.ContextMenuStrip = playlistContext;
            playlistList.MouseDown += (s, e) => { if (e.Button == MouseButtons.Right) { int index = playlistList.IndexFromPoint(e.Location); if (index >= 0) playlistList.SelectedIndex = index; } };
        }
        void LayoutLibraryInterface()
        {
            Bounds(favoritesButton, Theme.P(14), Theme.P(214), sidebar.Width - Theme.P(28), Theme.P(46));
            Bounds(playlistsButton, Theme.P(14), Theme.P(268), sidebar.Width - Theme.P(28), Theme.P(46));
            Bounds(queueButton, Theme.P(14), Theme.P(322), sidebar.Width - Theme.P(28), Theme.P(46));
            Bounds(recentButton, Theme.P(14), Theme.P(376), sidebar.Width - Theme.P(28), Theme.P(46));
            Bounds(toolsButton, Theme.P(14), Theme.P(430), sidebar.Width - Theme.P(28), Theme.P(46));
            Bounds(sidebarFoot, Theme.P(26), Theme.P(486), sidebar.Width - Theme.P(46), Theme.P(52));
            playlistList.Bounds = list.Bounds;
            int y = list.Bottom + Theme.P(11);
            Bounds(playButton, 0, y, Theme.P(126), Theme.P(44));
            Bounds(addButton, Theme.P(134), y, Theme.P(134), Theme.P(44)); removeButton.Bounds = addButton.Bounds;
            Bounds(saveButton, Theme.P(276), y, Theme.P(148), Theme.P(44)); Bounds(likeButton, Theme.P(432), y, Theme.P(44), Theme.P(44));
            if (view == LibraryView.Playlists) { Bounds(playButton, 0, y, Theme.P(156), Theme.P(44)); Bounds(addButton, Theme.P(164), y, Theme.P(144), Theme.P(44)); }
            bool libraryView = view == LibraryView.Playlists || view == LibraryView.PlaylistTracks;
            Bounds(createPlaylistButton, list.Width - Theme.P(260), section.Top - Theme.P(2), Theme.P(148), Theme.P(36)); Bounds(managePlaylistButton, list.Width - Theme.P(104), section.Top - Theme.P(2), Theme.P(104), Theme.P(36));
            createPlaylistButton.Visible = managePlaylistButton.Visible = libraryView;
            section.Width = libraryView ? list.Width - Theme.P(274) : list.Width - Theme.P(110); count.Visible = !libraryView;
            scroll.Target = ActiveList; scroll.Visible = ActiveList.Items.Count > ActiveList.Height / Math.Max(1, ActiveList.ItemHeight); scroll.BringToFront();
            if (wheelFilter != null) wheelFilter.SetTarget(ActiveList);
        }
        void RefreshLibraryNavigation()
        {
            bool overview = view == LibraryView.Playlists;
            detailInfo.Text = overview ? "Playlist bilgisi" : "Tam parça bilgisi";
            string selectedPlaylistId = (playlistList.SelectedItem as Playlist) == null ? null : ((Playlist)playlistList.SelectedItem).Id;
            if (overview)
            {
                refreshingPlaylists = true; playlistList.BeginUpdate(); playlistList.Items.Clear(); foreach (var playlist in library.Playlists) playlistList.Items.Add(playlist);
                if (playlistList.Items.Count > 0) { int selected = 0; for (int i = 0; i < playlistList.Items.Count; i++) if (((Playlist)playlistList.Items[i]).Id == selectedPlaylistId) selected = i; playlistList.SelectedIndex = selected; }
                playlistList.EndUpdate(); refreshingPlaylists = false;
                heading.Text = "Kendi ritmini oluştur."; subtitle.Text = "Sevdiğin parçalar, senin playlistlerin."; section.Text = "Playlistlerim";
                empty.Text = "İlk playlist’ini oluştur.\n\nYeni playlist düğmesiyle başlayabilirsin.";
            }
            else if (view == LibraryView.Favorites)
            {
                heading.Text = "Hep yeniden dinlediklerin."; subtitle.Text = "Beğendiğin parçalar burada, her zaman elinin altında."; section.Text = "Beğendiklerim";
                empty.Text = "Henüz beğendiğin bir parça yok.\n\nArama sonuçlarında kalp düğmesine bas.";
            }
            else if (view == LibraryView.PlaylistTracks)
            {
                var playlist = SelectedPlaylist; heading.Text = "Parçalar bir arada güzel."; subtitle.Text = "Playlist’ini çal, sıraya ekle veya yeni parçalarla büyüt."; section.Text = playlist == null ? "Playlist" : playlist.Name;
                empty.Text = "Bu playlist yeni parçalar bekliyor.\n\nBir şarkı ara, Playlist’e ekle ile buraya kaydet.";
                if (playlistFilter.Length > 0)
                {
                    subtitle.Text = "Playlist filtresi · " + playlistFilter;
                    empty.Text = "Bu filtreyle eşleşen parça yok.";
                }
            }
            else if (view == LibraryView.Recent)
            {
                heading.Text = "Son dinlenenler"; section.Text = heading.Text;
                subtitle.Text = "Yakın zamanda çaldığın parçalar.";
                empty.Text = "Bir parça çaldığında burada görünür.";
            }
            list.Visible = !overview && list.Items.Count > 0;
            playlistList.Visible = overview && playlistList.Items.Count > 0;
            empty.Visible = ActiveList.Items.Count == 0;
            discoverButton.Active = view == LibraryView.Search; favoritesButton.Active = view == LibraryView.Favorites; playlistsButton.Active = overview || view == LibraryView.PlaylistTracks; queueButton.Active = queueView;
            recentButton.Active = view == LibraryView.Recent;
            foreach (var button in new[] { discoverButton, favoritesButton, playlistsButton, queueButton, recentButton }) button.Invalidate();
            favoritesButton.AccessibleName = "Beğendiklerim, " + library.Favorites.Count + " parça"; playlistsButton.AccessibleName = "Playlistlerim, " + library.Playlists.Count + " playlist";
            LayoutLibraryInterface();
        }
        void UpdateLibraryActions()
        {
            bool overview = view == LibraryView.Playlists, selected = Selected != null; var playlist = SelectedPlaylist;
            playButton.Text = overview ? "Playlist’i aç" : (view == LibraryView.Favorites || view == LibraryView.PlaylistTracks) ? "Listeyi çal" : "Şimdi çal";
            playButton.Enabled = overview ? playlist != null : selected && !updating;
            addButton.Visible = !queueView; addButton.Text = overview ? "Yeni playlist" : "Sıraya ekle"; addButton.Enabled = overview ? libraryWritable : selected;
            saveButton.Visible = likeButton.Visible = !overview; saveButton.Enabled = likeButton.Enabled = selected && libraryWritable;
            removeButton.Visible = queueView; removeButton.Enabled = selected;
            managePlaylistButton.Enabled = playlist != null && libraryWritable; createPlaylistButton.Enabled = libraryWritable;
            detailPlay.Text = overview ? "Playlist’i aç" : "Dinle"; detailPlay.Enabled = overview ? playlist != null : selected && !updating;
            detailAdd.Enabled = overview ? libraryWritable : selected && libraryWritable; detailInfo.Enabled = overview ? playlist != null : selected;
            UpdateLikeButtons();
        }
        void UpdateLikeButtons()
        {
            var track = Selected; bool liked = track != null && library.Liked(track.Id);
            likeButton.Glyph = liked ? "heart-filled" : "heart"; likeButton.Active = liked; likeButton.AccessibleName = liked ? "Beğeniyi kaldır" : "Parçayı beğen"; tips.SetToolTip(likeButton, likeButton.AccessibleName); likeButton.Invalidate();
            if (view == LibraryView.Playlists) { detailAdd.Glyph = "plus"; detailAdd.Active = false; detailAdd.AccessibleName = "Yeni playlist oluştur"; }
            else { detailAdd.Glyph = likeButton.Glyph; detailAdd.Active = liked; detailAdd.AccessibleName = likeButton.AccessibleName; }
            tips.SetToolTip(detailAdd, detailAdd.AccessibleName); detailAdd.Invalidate();
        }
        bool ShowPlaylistDetail()
        {
            if (view != LibraryView.Playlists) return false;
            var playlist = SelectedPlaylist; Track first = playlist == null ? null : playlist.Tracks.FirstOrDefault(); Image image = null; if (first != null) images.TryGetValue(first.Id, out image);
            detailCover.Artwork = image; detailCover.Invalidate(); detailLabel.Text = "SEÇTİĞİN PLAYLIST"; detailTitle.Text = playlist == null ? "Kendi sıranı yarat." : playlist.Name;
            detailChannel.Text = playlist == null ? "Yeni playlist ile başlayabilirsin." : playlist.Tracks.Count + " parça"; detailQueue.Text = "Playlist’ini açarak parçalarını görebilir ve dinleyebilirsin."; detailInfo.Text = "Playlist bilgisi";
            UpdateLikeButtons(); return true;
        }
        void LoadSelectedPlaylistCover()
        {
            if (!IsHandleCreated || closing) return;
            if (view == LibraryView.Playlists)
            {
                int top = Math.Max(0, playlistList.TopIndex), last = Math.Min(playlistList.Items.Count, top + playlistList.Height / Math.Max(1, playlistList.ItemHeight) + 2);
                for (int i = top; i < last; i++) { var item = (Playlist)playlistList.Items[i]; if (item.Tracks.Count > 0) { Task ignored = EnsureCover(item.Tracks[0]); } }
            }
            var playlist = SelectedPlaylist; if (playlist != null && playlist.Tracks.Count > 0) { Task ignored = EnsureCover(playlist.Tracks[0]); }
        }
        void ArtworkUpdated(Track track)
        {
            int top = Math.Max(0, list.TopIndex), last = Math.Min(list.Items.Count, top + list.Height / Math.Max(1, list.ItemHeight) + 2);
            for (int i = top; i < last; i++) if (((Track)list.Items[i]).Id == track.Id) list.InvalidateItem(i);
            if (playlistList.Visible) for (int i = Math.Max(0, playlistList.TopIndex); i < Math.Min(playlistList.Items.Count, playlistList.TopIndex + 20); i++) { var item = (Playlist)playlistList.Items[i]; if (item.Tracks.Count > 0 && item.Tracks[0].Id == track.Id) playlistList.InvalidateItem(i); }
            if ((Selected != null && Selected.Id == track.Id) || (view == LibraryView.Playlists && SelectedPlaylist != null && SelectedPlaylist.Tracks.Any(t => t.Id == track.Id))) ShowDetail();
            if (current != null && current.Id == track.Id) UpdateMiniCover();
        }
        void DrawPlaylist(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= playlistList.Items.Count) return; var playlist = (Playlist)playlistList.Items[e.Index]; bool selected = (e.State & DrawItemState.Selected) != 0;
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; using (var brush = new SolidBrush(Theme.Bg)) g.FillRectangle(brush, e.Bounds);
            var row = new Rectangle(e.Bounds.X, e.Bounds.Y + Theme.P(3), e.Bounds.Width - Theme.P(22), e.Bounds.Height - Theme.P(6));
            using (var path = Theme.Round(row, Theme.P(11))) using (var brush = new SolidBrush(selected ? Theme.AccentDim : playlistList.HoverIndex == e.Index ? Theme.Panel : Theme.Bg)) g.FillPath(brush, path);
            var cover = new Rectangle(row.Left + Theme.P(12), row.Top + Theme.P(12), Theme.P(96), Theme.P(54));
            Image image = null; if (playlist.Tracks.Count > 0) images.TryGetValue(playlist.Tracks[0].Id, out image);
            using (var path = Theme.Round(cover, Theme.P(7)))
            {
                var state = g.Save(); g.SetClip(path); if (image != null) g.DrawImage(image, cover); else { using (var brush = new SolidBrush(Theme.Raised)) g.FillRectangle(brush, cover); Theme.Icon(g, "library", new Rectangle(cover.Left + Theme.P(35), cover.Top + Theme.P(14), Theme.P(26), Theme.P(26)), Theme.Accent); } g.Restore(state);
            }
            using (var titleFont = Theme.Font(15, true)) using (var smallFont = Theme.Font(12, false))
            {
                TextRenderer.DrawText(g, playlist.Name, titleFont, new Rectangle(cover.Right + Theme.P(18), row.Top + Theme.P(14), row.Width - Theme.P(154), Theme.P(27)), selected ? Theme.Accent : Theme.Text, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(g, playlist.Tracks.Count + " parça", smallFont, new Rectangle(cover.Right + Theme.P(18), row.Top + Theme.P(44), row.Width - Theme.P(154), Theme.P(22)), Theme.Muted, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            }
        }
        void LoadLibrary()
        {
            if (!preview)
            {
                try { library = libraryStore.Load(); if (libraryStore.Recovered) libraryProblem = "Kitaplığın yedekten geri yüklendi."; }
                catch { libraryWritable = false; libraryProblem = "Kitaplık okunamadı. Kayıtlar korunuyor; yeni kayıt yapılamıyor."; }
            }
            RefreshList();
        }
        bool CommitLibrary(Action<MusicLibrary> change)
        {
            if (!libraryWritable) { SetStatus("Kitaplık dosyası okunamadığı için kayıt yapılmadı.", true); return false; }
            try { var copy = library.Clone(); change(copy); if (!preview) libraryStore.Save(copy); library = copy; RefreshList(); return true; }
            catch (Exception ex) { SetStatus("Kaydedilemedi: " + ex.Message, true); return false; }
        }
        void ToggleFavorite()
        {
            var track = Selected; if (track == null) return; bool liked = library.Liked(track.Id);
            if (CommitLibrary(data => data.ToggleLike(track))) SetStatus(liked ? "Beğendiklerinden çıkarıldı: " + track.Title : "Beğendiklerine kaydedildi: " + track.Title, false);
        }
        void OpenSelectedPlaylist()
        {
            var playlist = SelectedPlaylist; if (playlist == null) return; playlistId = playlist.Id; playlistFilter = ""; view = LibraryView.PlaylistTracks; detailInfo.Text = "Tam parça bilgisi"; RefreshList();
        }
        void RemoveSavedTrack()
        {
            var track = Selected; if (track == null) return;
            if (view == LibraryView.Favorites) { ToggleFavorite(); return; }
            if (view == LibraryView.PlaylistTracks) { string id = playlistId; if (CommitLibrary(data => data.Find(id).Tracks.RemoveAll(t => t.Id == track.Id))) SetStatus("Parça playlist’ten çıkarıldı.", false); }
        }
        List<Track> CollectionAfter(int index)
        {
            return CurrentTracks().Skip(index + 1).Select(t => t.Copy()).ToList();
        }
        async Task PlayView()
        {
            if (view == LibraryView.Playlists) { OpenSelectedPlaylist(); return; }
            if ((view == LibraryView.Favorites || view == LibraryView.PlaylistTracks) && list.Items.Count > 0) list.SelectedIndex = 0;
            await PlaySelected(view == LibraryView.Favorites || view == LibraryView.PlaylistTracks);
        }
        void CreatePlaylistDialog(Track initialTrack)
        {
            if (!libraryWritable) return;
            string name = LibraryDialogs.Name(this, "Yeni playlist", "", value => library.CheckName(value, null)); if (name == null) return;
            string id = null;
            if (!CommitLibrary(data => { var created = data.Create(name); id = created.Id; if (initialTrack != null) data.Add(id, initialTrack); })) return;
            if (initialTrack == null) { playlistId = id; playlistFilter = ""; view = LibraryView.PlaylistTracks; RefreshList(); }
            SetStatus(initialTrack == null ? "Playlist oluşturuldu: " + name : "Parça yeni playlist’e kaydedildi: " + name, false);
        }
        void ShowSaveMenu(Control anchor)
        {
            var track = Selected; if (track == null || !libraryWritable) return;
            var menu = NewMenu();
            if (library.Playlists.Count == 0) menu.Items.Add(new ToolStripMenuItem("Henüz playlist yok") { Enabled = false });
            foreach (var playlist in library.Playlists)
            {
                string id = playlist.Id, name = playlist.Name; bool already = playlist.Tracks.Any(t => t.Id == track.Id);
                var item = new ToolStripMenuItem(name.Replace("&", "&&") + (already ? " · zaten ekli" : "")) { Enabled = !already, Checked = already };
                item.Click += (s, e) => { if (CommitLibrary(data => data.Add(id, track))) SetStatus("Playlist’e kaydedildi: " + name, false); }; menu.Items.Add(item);
            }
            menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("+ Yeni playlist oluştur…", null, (s, e) => CreatePlaylistDialog(track)); DisposeMenuAfterClose(menu); menu.Show(anchor, new Point(0, anchor.Height));
        }
        void ShowManageMenu(Control anchor)
        {
            if (SelectedPlaylist == null) return; var menu = NewMenu();
            if (view == LibraryView.PlaylistTracks)
            {
                menu.Items.Add("Playlist içinde ara…", null, (s, e) => {
                    var filter = LibraryDialogs.Name(this, "Playlist içinde ara", playlistFilter, value => { });
                    if (filter != null) { playlistFilter = filter.Trim(); RefreshList(); }
                });
                if (playlistFilter.Length > 0) menu.Items.Add("Filtreyi temizle", null, (s, e) => { playlistFilter = ""; RefreshList(); });
                menu.Items.Add("Parça adına göre sırala", null, (s, e) => SortPlaylist(false));
                menu.Items.Add("Sanatçıya göre sırala", null, (s, e) => SortPlaylist(true));
            }
            menu.Items.Add("Yeniden adlandır", null, (s, e) => RenamePlaylist()); menu.Items.Add("Playlist’i sil", null, (s, e) => DeletePlaylist());
            if (view == LibraryView.PlaylistTracks && Selected != null) menu.Items.Add("Seçili parçayı playlist’ten çıkar", null, (s, e) => RemoveSavedTrack());
            DisposeMenuAfterClose(menu); menu.Show(anchor, new Point(0, anchor.Height));
        }
        void MoveSelected(int direction)
        {
            var track = Selected; if (track == null) return;
            if (queueView)
            {
                int index = queue.IndexOf(track), to = index + direction;
                if (index < 0 || to < 0 || to >= queue.Count) return;
                queue.RemoveAt(index); queue.Insert(to, track); SaveState(); RefreshList(); list.SelectedIndex = to;
            }
            else if (view == LibraryView.PlaylistTracks)
            {
                string id = playlistId;
                CommitLibrary(data => {
                    var tracks = data.Find(id).Tracks;
                    int index = tracks.FindIndex(t => t.Id == track.Id), to = index + direction;
                    if (index < 0 || to < 0 || to >= tracks.Count) return;
                    var item = tracks[index]; tracks.RemoveAt(index); tracks.Insert(to, item);
                });
            }
        }
        void SortPlaylist(bool byArtist)
        {
            string id = playlistId;
            var comparer = StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), true);
            CommitLibrary(data => {
                var tracks = data.Find(id).Tracks;
                var sorted = tracks.OrderBy(t => byArtist ? t.Channel : t.Title, comparer).ThenBy(t => t.Title, comparer).ToList();
                tracks.Clear(); tracks.AddRange(sorted);
            });
        }
        void ShowFileMenu()
        {
            var menu = NewMenu();
            menu.Items.Add("Yerel müzik aç…", null, async (s, e) => await OpenLocalMusic());
            menu.Items.Add("Kitaplığı dışa aktar…", null, (s, e) => ExportLibrary()).Enabled = libraryWritable;
            menu.Items.Add("Kitaplığı içe aktar…", null, (s, e) => ImportLibrary()).Enabled = libraryWritable;
            menu.Items.Add("Ses motoru sürümü", null, async (s, e) => {
                if (string.IsNullOrEmpty(engineVersion)) await ShowEngineVersion();
                if (!closing) SetStatus(string.IsNullOrEmpty(engineVersion) ? "Ses motoru sürümü okunamadı." : "Ses motoru · yt-dlp " + engineVersion, string.IsNullOrEmpty(engineVersion));
            });
            menu.Items.Add(globalMediaKeys ? "Arka plan medya tuşlarını kapat" : "Arka plan medya tuşlarını aç", null, (s, e) => SetGlobalMediaKeys(!globalMediaKeys));
            DisposeMenuAfterClose(menu); menu.Show(toolsButton, new Point(0, toolsButton.Height));
        }
        async Task OpenLocalMusic()
        {
            using (var dialog = new OpenFileDialog { Title = "Yerel müzik aç", Multiselect = true, Filter = "Ses dosyaları|*.mp3;*.flac;*.wav;*.ogg;*.opus;*.m4a;*.aac;*.wma;*.aiff|Tüm dosyalar|*.*" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var tracks = dialog.FileNames.Where(path => System.Text.RegularExpressions.Regex.IsMatch(path, @"^[A-Za-z]:[\\/]")).Select(Track.FromFile).ToList();
                if (tracks.Count == 0) { SetStatus("Bilgisayarda kayıtlı bir ses dosyası seç.", true); return; }
                if (tracks.Count > 1) { queue.AddRange(tracks.Skip(1)); SaveState(); RefreshQueueInfo(); }
                await Play(tracks[0], true);
            }
        }
        void ExportLibrary()
        {
            using (var dialog = new SaveFileDialog { Title = "Kitaplığı dışa aktar", Filter = "Müzikçalar kitaplığı (*.json)|*.json", FileName = "muzikcalar-kitaplik.json" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try { LibraryStore.WriteAtomic(dialog.FileName, library.Encode(true)); SetStatus("Kitaplık aktarıldı. Cihazdaki ses dosyaları aktarım dosyasına eklenmedi.", false); }
                catch (Exception ex) { SetStatus("Kitaplık aktarılamadı. " + ex.Message, true); }
            }
        }
        void ImportLibrary()
        {
            using (var dialog = new OpenFileDialog { Title = "Kitaplığı içe aktar", Filter = "Müzikçalar kitaplığı (*.json)|*.json" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    if (new FileInfo(dialog.FileName).Length > 8 * 1024 * 1024) throw new InvalidDataException("Kitaplık dosyası çok büyük.");
                    var imported = MusicLibrary.Decode(File.ReadAllText(dialog.FileName, Encoding.UTF8));
                    if (CommitLibrary(data => data.Merge(imported))) SetStatus("Kitaplık mevcut kayıtlarla birleştirildi.", false);
                }
                catch (Exception ex) { SetStatus("Kitaplık okunamadı. " + ex.Message, true); }
            }
        }
        void RenamePlaylist()
        {
            var playlist = SelectedPlaylist; if (playlist == null) return; string id = playlist.Id;
            string name = LibraryDialogs.Name(this, "Playlist adını değiştir", playlist.Name, value => library.CheckName(value, id)); if (name == null) return;
            if (CommitLibrary(data => data.Rename(id, name))) SetStatus("Playlist adı güncellendi.", false);
        }
        void DeletePlaylist()
        {
            var playlist = SelectedPlaylist; if (playlist == null) return; string id = playlist.Id;
            if (!LibraryDialogs.Confirm(this, "Playlist’i sil", "“" + playlist.Name + "” playlist’i silinsin mi?\nBeğendiklerin korunur.")) return;
            if (CommitLibrary(data => data.Playlists.RemoveAll(p => p.Id == id))) { view = LibraryView.Playlists; playlistId = null; RefreshList(); SetStatus("Playlist silindi.", false); }
        }
        static ContextMenuStrip NewMenu() { return new ContextMenuStrip { BackColor = Theme.Panel, ForeColor = Theme.Text, Font = Theme.Font(13, false), Renderer = new ToolStripProfessionalRenderer(new LibraryMenuColors()), ShowImageMargin = false }; }
        void DisposeMenuAfterClose(ContextMenuStrip menu) { menu.Closed += (s, e) => { if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)(() => menu.Dispose())); else menu.Dispose(); }; }
    }

    class LibraryMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Theme.Panel; } }
        public override Color MenuItemSelected { get { return Theme.Raised; } }
        public override Color MenuItemBorder { get { return Theme.Border; } }
        public override Color MenuBorder { get { return Theme.Border; } }
        public override Color SeparatorDark { get { return Theme.Border; } }
        public override Color SeparatorLight { get { return Theme.Border; } }
    }

    static class LibraryDialogs
    {
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        static Form Window(string title)
        {
            var dialog = new Form { Text = title, StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(Theme.P(480), Theme.P(265)), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false, BackColor = Theme.Panel, ForeColor = Theme.Text, Font = Theme.Font(14, false), AutoScaleMode = AutoScaleMode.None };
            dialog.HandleCreated += (s, e) => { try { int dark = 1; DwmSetWindowAttribute(dialog.Handle, 20, ref dark, 4); } catch { } }; return dialog;
        }
        public static string Name(IWin32Window owner, string title, string initial, Action<string> validate)
        {
            using (var dialog = Window(title))
            {
                var heading = new Label { Text = title, Font = Theme.Font(21, true), ForeColor = Theme.Text, AutoEllipsis = true }; heading.SetBounds(Theme.P(24), Theme.P(22), Theme.P(432), Theme.P(36));
                var input = new TextBox { Text = initial, Font = Theme.Font(16, false), BackColor = Theme.Raised, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, MaxLength = 80, AccessibleName = "Playlist adı" }; input.SetBounds(Theme.P(24), Theme.P(84), Theme.P(432), Theme.P(36));
                var error = new Label { Font = Theme.Font(12, false), ForeColor = Color.FromArgb(255, 165, 152), AutoEllipsis = true }; error.SetBounds(Theme.P(24), Theme.P(130), Theme.P(432), Theme.P(42));
                var cancel = new SoftButton { Text = "Vazgeç", DialogResult = DialogResult.Cancel }; cancel.SetBounds(Theme.P(230), Theme.P(195), Theme.P(104), Theme.P(44));
                var save = new SoftButton { Text = "Kaydet", Primary = true }; save.SetBounds(Theme.P(346), Theme.P(195), Theme.P(110), Theme.P(44));
                save.Click += (s, e) => { try { validate(input.Text); dialog.DialogResult = DialogResult.OK; } catch (Exception ex) { error.Text = ex.Message; input.Focus(); } };
                dialog.Controls.AddRange(new Control[] { heading, input, error, cancel, save }); dialog.AcceptButton = save; dialog.CancelButton = cancel; dialog.Shown += (s, e) => { input.Focus(); input.SelectAll(); };
                return dialog.ShowDialog(owner) == DialogResult.OK ? MusicLibrary.CleanName(input.Text) : null;
            }
        }
        public static bool Confirm(IWin32Window owner, string title, string text)
        {
            using (var dialog = Window(title))
            {
                var message = new WrappedText { Text = text, Font = Theme.Font(16, false), ForeColor = Theme.Text }; message.SetBounds(Theme.P(24), Theme.P(30), Theme.P(432), Theme.P(128));
                var cancel = new SoftButton { Text = "Vazgeç", DialogResult = DialogResult.Cancel }; cancel.SetBounds(Theme.P(230), Theme.P(195), Theme.P(104), Theme.P(44));
                var remove = new SoftButton { Text = "Sil", Primary = true, DialogResult = DialogResult.OK }; remove.SetBounds(Theme.P(346), Theme.P(195), Theme.P(110), Theme.P(44));
                dialog.Controls.AddRange(new Control[] { message, cancel, remove }); dialog.CancelButton = cancel; dialog.AcceptButton = cancel;
                return dialog.ShowDialog(owner) == DialogResult.OK;
            }
        }
    }
}
