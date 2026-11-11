using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SadeSes
{
    public partial class MainForm : Form
    {
        readonly bool preview;
        readonly Icon windowIcon;
        readonly Panel sidebar = new Panel(), content = new Panel(), searchBox = new Panel(), playerBar = new Panel(), inspector = new Panel();
        readonly PictureBox brandLogo = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, TabStop = false, AccessibleRole = AccessibleRole.Graphic, AccessibleName = "Müzikçalar logosu" };
        readonly TextBox search = new TextBox();
        readonly SmoothList list = new SmoothList();
        readonly DarkScroll scroll = new DarkScroll();
        readonly ResultsWheelFilter wheelFilter;
        readonly Label brand = new Label(), brandSub = new Label(), eyebrow = new Label(), heading = new Label(), subtitle = new Label(), section = new Label(), count = new Label(), empty = new Label(), status = new Label(), detailLabel = new Label(), detailChannel = new Label(), detailQueue = new Label(), nowTitle = new Label(), nowChannel = new Label(), elapsed = new Label(), total = new Label(), sidebarFoot = new Label();
        readonly WrappedText detailTitle = new WrappedText();
        readonly CoverView detailCover = new CoverView(), miniCover = new CoverView();
        readonly Slider seek = new Slider(), volume = new Slider();
        readonly SoftButton searchButton = new SoftButton(), updateButton = new SoftButton(), discoverButton = new SoftButton(), queueButton = new SoftButton(), playButton = new SoftButton(), addButton = new SoftButton(), removeButton = new SoftButton(), detailPlay = new SoftButton(), detailAdd = new SoftButton(), detailInfo = new SoftButton(), pauseButton = new SoftButton(), previousButton = new SoftButton(), nextButton = new SoftButton(), repeatButton = new SoftButton(), stopButton = new SoftButton();
        readonly List<Track> results = new List<Track>(), queue = new List<Track>();
        readonly Stack<Track> history = new Stack<Track>();
        readonly List<Track> recent = new List<Track>();
        readonly HashSet<Track> skipped = new HashSet<Track>();
        readonly System.Windows.Forms.Timer checkpoint = new System.Windows.Forms.Timer { Interval = 5000 };
        readonly Dictionary<string, Image> images = new Dictionary<string, Image>();
        readonly HashSet<string> loadingImages = new HashSet<string>();
        readonly AudioPlayer player = new AudioPlayer();
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        readonly ToolTip tips = new ToolTip();
        CancellationTokenSource searchCancel, playCancel;
        Track current, pendingQueueTrack;
        List<Track> pendingCollection;
        bool loading, searching, paused, closing, updating, repeating, playable, globalMediaKeys;
        string engineVersion;
        int playVersion, searchVersion, volumeLevel = 70, recoveryAttempts;
        double duration, position, savedPosition, attemptStart;
        string StatePath { get { return Path.Combine(AppPaths.Data, "state.json"); } }
        Track Selected { get { return view == LibraryView.Playlists ? null : list.SelectedItem as Track; } }
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr window, int id);
        static readonly uint[] MediaVirtualKeys = { 0xb0, 0xb1, 0xb2, 0xb3 };
        void SetGlobalMediaKeys(bool enable)
        {
            globalMediaKeys = false;
            for (int i = 0; i < MediaVirtualKeys.Length; i++) UnregisterHotKey(Handle, i + 11);
            if (!enable) { SetStatus("Arka plan medya tuşları kapalı.", false); return; }
            for (int i = 0; i < MediaVirtualKeys.Length; i++)
                if (!RegisterHotKey(Handle, i + 11, 0x4000, MediaVirtualKeys[i]))
                {
                    for (int j = 0; j < i; j++) UnregisterHotKey(Handle, j + 11);
                    SetStatus("Medya tuşları başka bir uygulama tarafından kullanılıyor.", true); return;
                }
            globalMediaKeys = true; SetStatus("Arka plan medya tuşları açık.", false);
        }
        protected override void WndProc(ref Message message)
        {
            int command = message.Msg == 0x0319 ? (int)((message.LParam.ToInt64() >> 16) & 0xfff) :
                message.Msg == 0x0312 ? message.WParam.ToInt32() : 0;
            if (!preview && !closing && (command == 11 || command == 12 || command == 13 || command == 14 || command == 46 || command == 47))
            {
                Ui(async () => {
                    switch (command)
                    {
                        case 11: await Next(); break;
                        case 12: await Previous(); break;
                        case 13: Stop(); break;
                        case 14: await TogglePlayback(); break;
                        case 46: if (!playable || paused) await TogglePlayback(); break;
                        case 47: if (playable && !paused) Send("set_property", "pause", true); break;
                    }
                });
                message.Result = (IntPtr)1; return;
            }
            base.WndProc(ref message);
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); try { int dark = 1; DwmSetWindowAttribute(Handle, 20, ref dark, 4); } catch { } }
        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_COMPOSITED = 0x02000000;
                var parameters = base.CreateParams;
                // Present the child panels together when the window repaints.
                parameters.ExStyle |= WS_EX_COMPOSITED;
                return parameters;
            }
        }

        public MainForm(bool previewMode, float? previewScale = null)
        {
            preview = previewMode;
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) Theme.Scale = Math.Max(1, Math.Min(2, g.DpiX / 96f));
            if (preview && previewScale.HasValue) Theme.Scale = Math.Max(1, Math.Min(2, previewScale.Value));
            AutoScaleMode = AutoScaleMode.None; Font = Theme.Font(13, false); Text = "Müzikçalar"; Name = "Muzikcalar"; AccessibleName = "Müzikçalar";
            BackColor = Theme.Bg; ForeColor = Theme.Text; StartPosition = FormStartPosition.CenterScreen; KeyPreview = true;
            var screen = Screen.PrimaryScreen.WorkingArea;
            ClientSize = new Size(Math.Min(Theme.P(1280), screen.Width - Theme.P(60)), Math.Min(Theme.P(810), screen.Height - Theme.P(70)));
            MinimumSize = new Size(Theme.P(1070), Theme.P(690));
            windowIcon = LoadBranding();
            if (windowIcon != null) Icon = windowIcon;
            Build(); LoadState(); LoadLibrary();
            wheelFilter = new ResultsWheelFilter(this, list); Application.AddMessageFilter(wheelFilter);
            player.Message += OnPlayerMessage;
            FormClosing += (s, e) => {
                if (globalMediaKeys) SetGlobalMediaKeys(false);
                closing = true; Application.RemoveMessageFilter(wheelFilter); playVersion++; lifetime.Cancel(); if (searchCancel != null) searchCancel.Cancel(); if (playCancel != null) playCancel.Cancel(); CancelStreamPreparation(); streamPreparationTimer.Dispose(); SaveState(); player.Dispose();
                foreach (var image in images.Values) image.Dispose(); images.Clear(); tips.Dispose();
            };
            checkpoint.Tick += (s, e) => { if (playable && Math.Abs(position - savedPosition) >= 1) SaveState(); };
            streamPreparationTimer.Tick += async (s, e) => await PrepareSelectedStream();
            Shown += (s, e) => {
                search.Focus(); LoadVisibleCovers();
                if (preview) return;
                checkpoint.Start();
                ScheduleStreamPreparation();
                Task engine = WarmAudioEngine();
                Task version = ShowEngineVersion();
            };
            KeyDown += (s, e) => { if (e.Control && e.KeyCode == Keys.K) { search.Focus(); search.SelectAll(); e.SuppressKeyPress = true; } };
            if (preview)
            {
                string fixture = Path.Combine(AppPaths.Fixtures, "preview-tracks.json");
                if (File.Exists(fixture)) results.AddRange(YouTubeSearch.Decode(File.ReadAllText(fixture)));
                search.Text = "Daft Punk Instant Crush"; RefreshList();
            }
            SetStatus(libraryProblem ?? "Bir şarkı seç. Geri kalanını müziğe bırak.", libraryProblem != null); LayoutUi();
        }
        Icon LoadBranding()
        {
            string logoPath = Path.Combine(AppPaths.Assets, "branding", "res", "drawable-nodpi", "brand_logo.png");
            if (!File.Exists(logoPath)) return null;

            using (var source = Image.FromFile(logoPath))
            using (var mark = ExtractBrandMark(source))
            {
                int brandSize = Theme.P(50);
                brandLogo.Image = RenderBrandMark(mark, source.Width, new Size(brandSize, brandSize));
                using (var bitmap = RenderBrandMark(mark, source.Width, SystemInformation.IconSize))
                {
                    IntPtr handle = bitmap.GetHicon();
                    try { using (var icon = Icon.FromHandle(handle)) return (Icon)icon.Clone(); }
                    finally { DestroyIcon(handle); }
                }
            }
        }
        static Bitmap ExtractBrandMark(Image source)
        {
            // Pixel coordinates in the original 1254 x 1254 branding asset.
            var crop = Rectangle.FromLTRB(100, 350, 1096, 993);
            var mark = new Bitmap(crop.Width, crop.Height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(mark))
            using (var attributes = new ImageAttributes())
            {
                // The lower tip of the green accent extends into the top-right of the crop.
                graphics.ExcludeClip(Rectangle.FromLTRB(970 - crop.Left, 0, crop.Width, 375 - crop.Top));
                attributes.SetColorKey(Color.Black, Color.FromArgb(32, 32, 48));
                graphics.DrawImage(source, new Rectangle(Point.Empty, mark.Size), crop.X, crop.Y, crop.Width, crop.Height, GraphicsUnit.Pixel, attributes);
            }
            return mark;
        }
        static Bitmap RenderBrandMark(Bitmap mark, int sourceWidth, Size size)
        {
            var bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
            float scale = Math.Min(size.Width, size.Height) / (float)sourceWidth;
            float width = mark.Width * scale, height = mark.Height * scale;
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(mark, new RectangleF((size.Width - width) / 2, (size.Height - height) / 2, width, height));
            }
            return bitmap;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { CancelSuggestions(); checkpoint.Dispose(); }
            if (disposing && brandLogo.Image != null)
            {
                Image image = brandLogo.Image;
                brandLogo.Image = null;
                image.Dispose();
            }
            base.Dispose(disposing);
            if (disposing && windowIcon != null) windowIcon.Dispose();
        }
        void LabelStyle(Label label, string text, float size, Color color, bool bold)
        { label.Text = text; label.Font = Theme.Font(size, bold); label.ForeColor = color; label.BackColor = Color.Transparent; label.AutoSize = false; label.TextAlign = ContentAlignment.MiddleLeft; label.AutoEllipsis = true; }
        void ButtonStyle(SoftButton button, string text, string glyph, bool primary)
        { button.Text = text; button.Glyph = glyph; button.Primary = primary; button.Font = Theme.Font(13, true); button.AccessibleName = text.Length > 0 ? text : glyph; }
        void Build()
        {
            SuspendLayout(); sidebar.BackColor = Theme.Sidebar; content.BackColor = Theme.Bg; playerBar.BackColor = Theme.Sidebar; inspector.BackColor = Theme.Panel; searchBox.BackColor = Theme.Panel;
            Controls.AddRange(new Control[] { sidebar, content, playerBar });
            sidebar.Controls.AddRange(new Control[] { brandLogo, brand, brandSub, discoverButton, queueButton, sidebarFoot });
            LabelStyle(brand, "Müzikçalar", 23, Theme.Text, true); LabelStyle(brandSub, "Kendi ritminde.", 12, Theme.Muted, false);
            LabelStyle(sidebarFoot, "Ctrl + K  ·  Hızlı arama", 12, Theme.Muted, false);
            ButtonStyle(discoverButton, "Keşfet", "search", false); discoverButton.Active = true; discoverButton.BackColor = Theme.Sidebar;
            ButtonStyle(queueButton, "Çalma sırası", "queue", false); queueButton.BackColor = Theme.Sidebar;
            discoverButton.Click += (s, e) => { queueView = false; RefreshList(); };
            queueButton.Click += (s, e) => { queueView = true; RefreshList(); };
            content.Controls.AddRange(new Control[] { searchBox, updateButton, eyebrow, heading, subtitle, section, count, list, scroll, empty, playButton, addButton, removeButton, status, inspector });
            scroll.Target = list; scroll.BringToFront();
            searchBox.Controls.AddRange(new Control[] { search, searchButton });
            StyleSearchSurface(searchBox, 12);
            searchBox.Paint += (s, e) => Theme.Icon(e.Graphics, "search", new Rectangle(Theme.P(16), Theme.P(14), Theme.P(20), Theme.P(20)), Theme.Muted);
            search.BorderStyle = BorderStyle.None; search.BackColor = Theme.Panel; search.ForeColor = Theme.Text; search.Font = Theme.Font(15, false); search.AccessibleName = "Şarkı, sanatçı veya YouTube bağlantısı";
            BuildSearchSuggestions();
            ButtonStyle(searchButton, "Ara", "", true); searchButton.Click += async (s, e) => await Search();
            ButtonStyle(updateButton, "Güncelle", "", false); updateButton.Click += async (s, e) => await UpdateTool();
            LabelStyle(eyebrow, "MÜZİĞİNİ BUL", 11, Theme.Accent, true); LabelStyle(heading, "Bugün ne dinleyelim?", 34, Theme.Text, true); LabelStyle(subtitle, "Sevdiğin sanatçıları ara, bir parçaya takıl, kendi sıranı oluştur.", 13, Theme.Muted, false);
            LabelStyle(section, "Arama sonuçları", 18, Theme.Text, true); LabelStyle(count, "", 12, Theme.Muted, false); count.TextAlign = ContentAlignment.MiddleRight;
            list.BackColor = Theme.Bg; list.ForeColor = Theme.Text; list.BorderStyle = BorderStyle.None; list.IntegralHeight = false; list.DrawMode = DrawMode.OwnerDrawFixed; list.ItemHeight = Theme.P(90); list.AccessibleName = "Kapak görselli şarkı listesi";
            list.DrawItem += DrawTrack; list.DoubleClick += async (s, e) => await PlaySelected();
            list.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await PlaySelected(); } else if (e.KeyCode == Keys.Delete) RemoveSelected(); };
            list.SelectedIndexChanged += (s, e) => { SelectionButtons(); ShowDetail(); LoadVisibleCovers(); };
            list.ViewChanged += () => { scroll.Invalidate(); LoadVisibleCovers(); };
            LabelStyle(empty, "Sıradaki favorini bul.\n\nYukarıya bir şarkı veya sanatçı adı yaz.\nYouTube bağlantısı da yapıştırabilirsin.", 16, Theme.Muted, false); empty.TextAlign = ContentAlignment.MiddleCenter;
            ButtonStyle(playButton, "Şimdi çal", "play", true); playButton.Click += async (s, e) => await PlayView();
            ButtonStyle(addButton, "Sıraya ekle", "plus", false); addButton.Click += (s, e) => AddSelected();
            ButtonStyle(removeButton, "Sıradan çıkar", "", false); removeButton.Click += (s, e) => RemoveSelected();
            LabelStyle(status, "", 11, Theme.Muted, false);

            inspector.Controls.AddRange(new Control[] { detailLabel, detailCover, detailTitle, detailChannel, detailPlay, detailAdd, detailInfo, detailQueue });
            LabelStyle(detailLabel, "SEÇTİĞİN PARÇA", 10, Theme.Muted, true); detailTitle.Text = "Müzikle biraz kal."; detailTitle.Font = Theme.Font(19, true); detailTitle.ForeColor = Theme.Text;
            LabelStyle(detailChannel, "Bir parça seçtiğinde burada göreceksin.", 12, Theme.Muted, false); detailChannel.TextAlign = ContentAlignment.TopLeft;
            LabelStyle(detailQueue, "Çalma sıran henüz boş.\nSevdiğin parçaları sıraya ekle.", 12, Theme.Muted, false); detailQueue.TextAlign = ContentAlignment.TopLeft;
            ButtonStyle(detailPlay, "Dinle", "play", true); detailPlay.Click += async (s, e) => await PlaySelected();
            ButtonStyle(detailAdd, "", "heart", false); detailAdd.AccessibleName = "Seçili parçayı beğen"; tips.SetToolTip(detailAdd, "Beğen"); detailAdd.Click += (s, e) => { if (view == LibraryView.Playlists) CreatePlaylistDialog(null); else ToggleFavorite(); };
            ButtonStyle(detailInfo, "Tam parça bilgisi", "", false); detailInfo.Quiet = true; detailInfo.BackColor = Theme.Panel; detailInfo.Font = Theme.Font(11, false); detailInfo.Click += (s, e) => ShowFullDetails();

            playerBar.Controls.AddRange(new Control[] { miniCover, nowTitle, nowChannel, previousButton, pauseButton, nextButton, repeatButton, stopButton, seek, elapsed, total, volume });
            playerBar.Paint += (s, e) => { using (var pen = new Pen(Theme.Border)) e.Graphics.DrawLine(pen, 0, 0, playerBar.Width, 0); Theme.Icon(e.Graphics, "volume", new Rectangle(playerBar.Width - Theme.P(184), Theme.P(45), Theme.P(19), Theme.P(19)), Theme.Muted); };
            LabelStyle(nowTitle, "Dinlemeye hazırsın", 14, Theme.Text, true); LabelStyle(nowChannel, "Bir parça seç, dinlemeye başla.", 12, Theme.Muted, false);
            LabelStyle(elapsed, "0:00", 10, Theme.Muted, false); elapsed.TextAlign = ContentAlignment.MiddleRight; LabelStyle(total, "0:00", 10, Theme.Muted, false);
            ButtonStyle(previousButton, "", "previous", false); previousButton.Quiet = true; previousButton.BackColor = Theme.Sidebar; previousButton.AccessibleName = "Önceki parça"; previousButton.Click += async (s, e) => await Previous();
            ButtonStyle(pauseButton, "", "play", true); pauseButton.AccessibleName = "Oynat veya duraklat"; pauseButton.Click += async (s, e) => await TogglePlayback();
            ButtonStyle(nextButton, "", "next", false); nextButton.Quiet = true; nextButton.BackColor = Theme.Sidebar; nextButton.AccessibleName = "Sonraki parça"; nextButton.Click += async (s, e) => await Next();
            tips.SetToolTip(previousButton, "Önceki parça"); tips.SetToolTip(nextButton, "Sonraki parça"); tips.SetToolTip(pauseButton, "Oynat / duraklat");
            ButtonStyle(repeatButton, "", "repeat", false); repeatButton.Quiet = true; repeatButton.BackColor = Theme.Sidebar; SetRepeat(false); repeatButton.Click += (s, e) => { SetRepeat(!repeating); SaveState(); }; tips.SetToolTip(repeatButton, "Aynı parçayı tekrarla");
            ButtonStyle(stopButton, "", "stop", false); stopButton.Quiet = true; stopButton.BackColor = Theme.Sidebar; stopButton.AccessibleName = "Durdur"; stopButton.Click += (s, e) => Stop(); tips.SetToolTip(stopButton, "Durdur");
            seek.BackColor = Theme.Sidebar; seek.AccessibleName = "Oynatma konumu"; seek.Changed += value => { if (current != null && !loading && duration > 0) { position = value; if (playable) Send("seek", value, "absolute"); SaveState(); } };
            volume.BackColor = Theme.Sidebar; volume.AccessibleName = "Ses seviyesi"; volume.Value = volumeLevel; volume.Changed += value => { volumeLevel = (int)value; Send("set_property", "volume", volumeLevel); };
            sidebar.TabIndex = 0; content.TabIndex = 1; playerBar.TabIndex = 2;
            searchBox.TabIndex = 0; search.TabIndex = 0; searchButton.TabIndex = 1; updateButton.TabIndex = 1; list.TabIndex = 2; playButton.TabIndex = 3; addButton.TabIndex = 4; removeButton.TabIndex = 5; inspector.TabIndex = 6;
            detailInfo.TabIndex = 0; detailPlay.TabIndex = 1; detailAdd.TabIndex = 2;
            previousButton.TabIndex = 0; pauseButton.TabIndex = 1; nextButton.TabIndex = 2; repeatButton.TabIndex = 3; seek.TabIndex = 4; stopButton.TabIndex = 5; volume.TabIndex = 6;
            BuildLibraryInterface(); ResumeLayout(false); Resize += (s, e) => LayoutUi(); RefreshList();
        }
        static new void Bounds(Control c, int x, int y, int w, int h) { c.SetBounds(x, y, Math.Max(1, w), Math.Max(1, h)); }
        void LayoutUi()
        {
            if (content == null || searchButton == null) return;
            int side = Theme.P(210), foot = Theme.P(116), gap = Theme.P(28), body = Math.Max(Theme.P(520), ClientSize.Height - foot);
            Bounds(sidebar, 0, 0, side, body); Bounds(content, side + gap, 0, ClientSize.Width - side - gap * 2, body); Bounds(playerBar, 0, body, ClientSize.Width, foot);
            Bounds(brandLogo, Theme.P(24), Theme.P(22), Theme.P(50), Theme.P(50));
            Bounds(brand, Theme.P(24), Theme.P(78), side - Theme.P(40), Theme.P(36)); Bounds(brandSub, Theme.P(26), Theme.P(116), side - Theme.P(40), Theme.P(22));
            Bounds(discoverButton, Theme.P(14), Theme.P(160), side - Theme.P(28), Theme.P(46)); Bounds(queueButton, Theme.P(14), Theme.P(214), side - Theme.P(28), Theme.P(46));
            Bounds(sidebarFoot, Theme.P(26), body - Theme.P(150), side - Theme.P(46), Theme.P(124));
            int width = content.Width, right = Theme.P(272), listWidth = Math.Max(Theme.P(400), width - right - Theme.P(24));
            Bounds(searchBox, 0, Theme.P(25), width - Theme.P(126), Theme.P(48)); Bounds(updateButton, width - Theme.P(112), Theme.P(25), Theme.P(112), Theme.P(48));
            Bounds(search, Theme.P(48), Theme.P(13), searchBox.Width - Theme.P(143), Theme.P(25)); Bounds(searchButton, searchBox.Width - Theme.P(80), Theme.P(6), Theme.P(72), Theme.P(36));
            Bounds(eyebrow, 0, Theme.P(100), width, Theme.P(21)); Bounds(heading, -Theme.P(2), Theme.P(124), width, Theme.P(53)); Bounds(subtitle, 0, Theme.P(180), width, Theme.P(24));
            Bounds(section, 0, Theme.P(231), listWidth - Theme.P(110), Theme.P(30)); Bounds(count, listWidth - Theme.P(108), Theme.P(233), Theme.P(108), Theme.P(27));
            int listTop = Theme.P(277), listHeight = Math.Max(Theme.P(130), body - listTop - Theme.P(91));
            Bounds(list, 0, listTop, listWidth, listHeight); Bounds(empty, 0, listTop, listWidth, listHeight);
            Bounds(scroll, list.Right - Theme.P(18), list.Top, Theme.P(18), list.Height); scroll.Visible = list.Items.Count > list.Height / Math.Max(1, list.ItemHeight); scroll.BringToFront();
            Bounds(playButton, 0, list.Bottom + Theme.P(11), Theme.P(135), Theme.P(44)); Bounds(addButton, Theme.P(145), list.Bottom + Theme.P(11), Theme.P(144), Theme.P(44)); Bounds(removeButton, Theme.P(299), list.Bottom + Theme.P(11), Theme.P(142), Theme.P(44));
            Bounds(status, 0, body - Theme.P(30), width, Theme.P(23));
            Bounds(inspector, width - right, Theme.P(233), right, body - Theme.P(280));
            int ip = Theme.P(17), iw = inspector.Width - ip * 2;
            Bounds(detailLabel, ip, Theme.P(14), iw, Theme.P(22)); Bounds(detailCover, ip, Theme.P(48), iw, (int)(iw * 9 / 16.0));
            bool compact = inspector.Height < Theme.P(411);
            Bounds(detailTitle, ip, detailCover.Bottom + Theme.P(16), iw, Theme.P(compact ? 54 : 88)); Bounds(detailChannel, ip, detailTitle.Bottom + Theme.P(3), iw, Theme.P(25));
            detailChannel.Visible = !compact;
            Bounds(detailInfo, ip, compact ? detailTitle.Bottom + Theme.P(8) : detailChannel.Bottom + Theme.P(2), iw, Theme.P(28));
            Bounds(detailPlay, ip, detailInfo.Bottom + Theme.P(12), iw - Theme.P(52), Theme.P(44)); Bounds(detailAdd, inspector.Width - ip - Theme.P(44), detailPlay.Top, Theme.P(44), Theme.P(44));
            detailPlay.Visible = detailAdd.Visible = !compact;
            detailInfo.Visible = inspector.Height >= detailInfo.Bottom + Theme.P(6);
            Bounds(detailQueue, ip, detailPlay.Bottom + Theme.P(25), iw, Math.Max(Theme.P(40), inspector.Height - detailPlay.Bottom - Theme.P(35))); detailQueue.Visible = inspector.Height > Theme.P(420);
            int cx = ClientSize.Width / 2;
            int trackTextLeft = Theme.P(98), elapsedLeft = cx - Theme.P(221);
            int trackTextWidth = Math.Min(Theme.P(245), elapsedLeft - trackTextLeft - Theme.P(12));
            Bounds(miniCover, Theme.P(24), Theme.P(28), Theme.P(60), Theme.P(60)); Bounds(nowTitle, trackTextLeft, Theme.P(34), trackTextWidth, Theme.P(26)); Bounds(nowChannel, trackTextLeft, Theme.P(61), trackTextWidth, Theme.P(23));
            Bounds(previousButton, cx - Theme.P(90), Theme.P(18), Theme.P(44), Theme.P(44)); Bounds(pauseButton, cx - Theme.P(24), Theme.P(14), Theme.P(52), Theme.P(52)); Bounds(nextButton, cx + Theme.P(50), Theme.P(18), Theme.P(44), Theme.P(44)); Bounds(repeatButton, cx + Theme.P(108), Theme.P(18), Theme.P(44), Theme.P(44));
            Bounds(seek, cx - Theme.P(170), Theme.P(76), Theme.P(340), Theme.P(22)); Bounds(elapsed, elapsedLeft, Theme.P(76), Theme.P(43), Theme.P(22)); Bounds(total, cx + Theme.P(180), Theme.P(76), Theme.P(48), Theme.P(22));
            Bounds(stopButton, ClientSize.Width - Theme.P(240), Theme.P(32), Theme.P(44), Theme.P(44)); Bounds(volume, ClientSize.Width - Theme.P(151), Theme.P(43), Theme.P(120), Theme.P(24));
            LayoutLibraryInterface(); LayoutSearchSuggestions(); playerBar.Invalidate();
        }
        void DrawTrack(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= list.Items.Count) return;
            Track track = (Track)list.Items[e.Index]; bool selected = (e.State & DrawItemState.Selected) != 0, hover = list.HoverIndex == e.Index;
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; using (var background = new SolidBrush(Theme.Bg)) g.FillRectangle(background, e.Bounds);
            Rectangle row = new Rectangle(e.Bounds.X, e.Bounds.Y + Theme.P(3), e.Bounds.Width - Theme.P(22), e.Bounds.Height - Theme.P(6));
            using (var path = Theme.Round(row, Theme.P(11))) using (var brush = new SolidBrush(selected ? Color.FromArgb(36, 45, 36) : hover ? Theme.Panel : Theme.Bg)) g.FillPath(brush, path);
            Rectangle cover = new Rectangle(row.X + Theme.P(10), row.Y + Theme.P(8), Theme.P(112), Theme.P(63));
            Image image; images.TryGetValue(track.Id, out image);
            using (var path = Theme.Round(cover, Theme.P(7)))
            {
                var state = g.Save(); g.SetClip(path);
                if (image != null) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(image, cover); }
                else { using (var brush = new SolidBrush(Theme.Raised)) g.FillRectangle(brush, cover); Theme.Icon(g, "wave", new Rectangle(cover.X + cover.Width / 2 - Theme.P(12), cover.Y + cover.Height / 2 - Theme.P(12), Theme.P(24), Theme.P(24)), Theme.Muted); }
                g.Restore(state);
            }
            int textX = cover.Right + Theme.P(17), textW = row.Right - textX - Theme.P(80);
            using (var titleFont = Theme.Font(14, true)) using (var channelFont = Theme.Font(12, false)) using (var timeFont = Theme.Font(11, false))
            {
                TextRenderer.DrawText(g, track.Title, titleFont, new Rectangle(textX, row.Y + Theme.P(17), textW, Theme.P(25)), selected ? Theme.Accent : Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(g, track.Channel, channelFont, new Rectangle(textX, row.Y + Theme.P(45), textW, Theme.P(21)), Theme.Muted, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                string length = track.Duration > 0 ? Tools.Time(track.Duration) : "—";
                TextRenderer.DrawText(g, length, timeFont, new Rectangle(row.Right - Theme.P(76), row.Y, Theme.P(63), row.Height), Theme.Muted, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }
            if (selected) using (var pen = new Pen(Color.FromArgb(74, 96, 59))) using (var path = Theme.Round(row, Theme.P(11))) g.DrawPath(pen, path);
        }
        void RefreshList()
        {
            string selectedId = Selected == null ? null : Selected.Id;
            list.BeginUpdate(); list.Items.Clear(); foreach (Track track in CurrentTracks()) list.Items.Add(track);
            if (list.Items.Count > 0) { int selected = 0; if (selectedId != null) for (int i = 0; i < list.Items.Count; i++) if (((Track)list.Items[i]).Id == selectedId) { selected = i; break; } list.SelectedIndex = selected; }
            list.EndUpdate();
            scroll.Visible = list.Items.Count > list.Height / Math.Max(1, list.ItemHeight); scroll.Invalidate();
            empty.Text = queueView ? "Sıran yeni hikâyeler bekliyor.\n\nArama sonuçlarından sevdiğin parçaları ekle." : "Sıradaki favorini bul.\n\nBir şarkı, sanatçı adı veya YouTube bağlantısı yaz.";
            section.Text = queueView ? "Çalma sıran" : "Arama sonuçları"; count.Text = list.Items.Count > 0 ? list.Items.Count + " parça" : "";
            heading.Text = queueView ? "Ritmin hiç bölünmesin." : "Bugün ne dinleyelim?";
            subtitle.Text = queueView ? "Sıraya eklediğin parçalar, tam istediğin sırayla." : "Sevdiğin sanatçıları ara, bir parçaya takıl, kendi sıranı oluştur.";
            discoverButton.Active = !queueView; queueButton.Active = queueView; queueButton.Text = "Çalma sırası"; queueButton.AccessibleName = "Çalma sırası, " + queue.Count + " parça"; tips.SetToolTip(queueButton, "Sırada " + queue.Count + " parça"); discoverButton.Invalidate(); queueButton.Invalidate();
            RefreshLibraryNavigation(); SelectionButtons(); ShowDetail(); LoadVisibleCovers();
        }
        void SelectionButtons()
        {
            bool selected = Selected != null;
            playButton.Enabled = detailPlay.Enabled = selected && !updating; addButton.Enabled = detailAdd.Enabled = detailInfo.Enabled = selected; removeButton.Visible = queueView; removeButton.Enabled = selected; UpdateLibraryActions();
            ScheduleStreamPreparation();
        }
        void ShowDetail()
        {
            if (ShowPlaylistDetail()) return;
            var track = Selected; Image image = null;
            if (track != null) images.TryGetValue(track.Id, out image);
            detailCover.Artwork = image; detailCover.Invalidate(); detailTitle.Text = track == null ? "Müzikle biraz kal." : track.Title; detailChannel.Text = track == null ? "Bir parça seçtiğinde burada göreceksin." : track.Channel;
            tips.SetToolTip(detailTitle, track == null ? "" : track.Title); tips.SetToolTip(detailChannel, track == null ? "" : track.Channel);
            detailLabel.Text = track != null && current != null && track.Id == current.Id ? "ŞİMDİ ÇALIYOR" : "SEÇTİĞİN PARÇA";
            detailQueue.Text = queue.Count == 0 ? "Çalma sıran henüz boş.\nSevdiğin parçaları sıraya ekle." : "SIRADA " + queue.Count + " PARÇA\n\n" + queue[0].Title;
            UpdateLikeButtons();
        }
        void ShowFullDetails()
        {
            var track = Selected; var playlist = SelectedPlaylist; if (track == null && playlist == null) return;
            string details = track != null ? track.Title + Environment.NewLine + Environment.NewLine + track.Channel : playlist.Name + Environment.NewLine + Environment.NewLine + playlist.Tracks.Count + " parça";
            using (var dialog = new Form { Text = "Parça bilgisi", BackColor = Theme.Panel, ForeColor = Theme.Text, StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(Theme.P(560), Theme.P(280)), MinimumSize = new Size(Theme.P(420), Theme.P(240)), MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false, Font = Theme.Font(14, false) })
            {
                var fullText = new TextBox { Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Theme.Panel, ForeColor = Theme.Text, Font = Theme.Font(15, false), ScrollBars = ScrollBars.Vertical, Text = details, AccessibleName = "Tam parça veya playlist bilgisi" };
                var close = new SoftButton { Text = "Kapat", Primary = true, DialogResult = DialogResult.OK, AccessibleName = "Parça bilgisini kapat" };
                dialog.Controls.Add(fullText); dialog.Controls.Add(close); dialog.AcceptButton = close; dialog.CancelButton = close;
                Action arrange = () => { fullText.SetBounds(Theme.P(24), Theme.P(24), dialog.ClientSize.Width - Theme.P(48), dialog.ClientSize.Height - Theme.P(100)); close.SetBounds(dialog.ClientSize.Width - Theme.P(124), dialog.ClientSize.Height - Theme.P(60), Theme.P(100), Theme.P(40)); };
                dialog.Resize += (s, e) => arrange(); arrange(); dialog.ShowDialog(this);
            }
        }
        void UpdateMiniCover()
        {
            Image image = null; if (current != null) images.TryGetValue(current.Id, out image); miniCover.Artwork = image; miniCover.Invalidate();
        }
        void LoadVisibleCovers()
        {
            if (view == LibraryView.Playlists) { LoadSelectedPlaylistCover(); return; }
            if (closing || !IsHandleCreated || list.Items.Count == 0) return;
            int start = Math.Max(0, list.TopIndex), length = Math.Min(list.Items.Count - start, list.Height / Math.Max(1, list.ItemHeight) + 3);
            for (int i = start; i < start + length; i++) { Task ignored = EnsureCover((Track)list.Items[i]); }
            if (Selected != null) { Task ignored = EnsureCover(Selected); }
            if (current != null) { Task ignored = EnsureCover(current); }
        }
        async Task EnsureCover(Track track)
        {
            if (preview || track == null || track.IsLocal || images.ContainsKey(track.Id) || !loadingImages.Add(track.Id)) return;
            try
            {
                string path = await Thumbnails.Get(track, lifetime.Token);
                if (closing) return;
                using (var stream = new MemoryStream(File.ReadAllBytes(path))) using (var source = Image.FromStream(stream)) images[track.Id] = new Bitmap(source);
                if (images.Count > 128)
                {
                    var protect = new HashSet<string>(); if (current != null) protect.Add(current.Id); if (Selected != null) protect.Add(Selected.Id);
                    var selectedPlaylist = SelectedPlaylist; if (selectedPlaylist != null && selectedPlaylist.Tracks.Count > 0) protect.Add(selectedPlaylist.Tracks[0].Id);
                    int top = Math.Max(0, list.TopIndex); for (int i = top; i < Math.Min(list.Items.Count, top + 20); i++) protect.Add(((Track)list.Items[i]).Id);
                    foreach (string key in images.Keys.Where(k => !protect.Contains(k)).Take(images.Count - 112).ToArray()) { images[key].Dispose(); images.Remove(key); }
                }
                ArtworkUpdated(track);
            }
            catch { }
            finally { loadingImages.Remove(track.Id); }
        }
        void SetStatus(string text, bool error) { status.Text = text; status.ForeColor = error ? Color.FromArgb(255, 165, 152) : Theme.Muted; }
        void Ui(Action action) { if (closing || IsDisposed || !IsHandleCreated) return; try { BeginInvoke((Action)(() => { if (!closing) action(); })); } catch { } }
        async Task Search()
        {
            HideSuggestions();
            if (updating || preview || string.IsNullOrWhiteSpace(search.Text)) return;
            if (searchCancel != null) searchCancel.Cancel(); searchCancel = new CancellationTokenSource(); var token = searchCancel.Token; int version = ++searchVersion;
            searching = true; searchButton.Text = "…"; updateButton.Enabled = false; SetStatus("Müziğin aranıyor…", false);
            var timer = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var found = await Tools.Search(search.Text, token); if (closing || version != searchVersion) return;
                results.Clear(); results.AddRange(found); queueView = false; RefreshList();
                SetStatus(found.Count == 0 ? "Sonuç bulunamadı. Başka bir arama dene." : found.Count + " parça · " + timer.Elapsed.TotalSeconds.ToString("0.0") + " sn  ·  Dinlemek için çift tıkla.", false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!closing && version == searchVersion) SetStatus(Tools.Friendly(ex.Message), true); }
            finally { if (!closing && version == searchVersion) { searching = false; searchButton.Text = "Ara"; updateButton.Enabled = !loading; } }
        }
        async Task UpdateTool()
        {
            if (updating || loading || preview || searching) return;
            updating = true; searchButton.Enabled = updateButton.Enabled = false; SelectionButtons(); SetStatus("YouTube bağlantısı güncelleniyor…", false);
            try
            {
                var result = await Tools.Run(Tools.Bin("yt-dlp.exe"), new[] { "--ignore-config", "--update" }, lifetime.Token, 180);
                if (result.Code != 0) throw new IOException("Güncelleme tamamlanamadı. İnternet bağlantını kontrol et.");
                Tools.ClearStreams();
                await ShowEngineVersion();
                var probe = current != null && !current.IsLocal ? current : new Track { Id = "n61ULEU7CO0" };
                await Tools.ProbeStream(probe, lifetime.Token);
                if (!closing) SetStatus("Ses motoru güncellendi ve ses bağlantısı doğrulandı.", false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!closing) SetStatus(ex.Message, true); }
            finally { if (!closing) { updating = false; searchButton.Enabled = updateButton.Enabled = true; SelectionButtons(); } }
        }
        async Task ShowEngineVersion()
        {
            try
            {
                var result = await Tools.Run(Tools.Bin("yt-dlp.exe"), new[] { "--ignore-config", "--version" }, lifetime.Token, 12);
                if (closing || result.Code != 0) return;
                engineVersion = result.Output.Trim();
                string label = "Ses motoru · yt-dlp " + engineVersion;
                updateButton.AccessibleName = "Güncelle · " + label;
                tips.SetToolTip(updateButton, label);
            }
            catch (Exception) { }
        }
    }
}
