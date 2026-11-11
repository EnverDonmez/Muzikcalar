using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SadeSes
{
    sealed class PlaylistMenu : ContextMenuStrip
    {
        readonly int menuWidth = Theme.P(304);
        readonly Font titleFont = Theme.Font(14, true);
        readonly Font detailFont = Theme.Font(12, false);

        public PlaylistMenu()
        {
            AccessibleName = "Playlist’e ekle";
            BackColor = Theme.Panel;
            ForeColor = Theme.Text;
            ShowCheckMargin = ShowImageMargin = false;
            ShowItemToolTips = true;
            MinimumSize = MaximumSize = new Size(menuWidth, 0);
            Renderer = new PlaylistMenuRenderer(titleFont, detailFont);
        }

        public void AddRow(string title, string detail, string icon, bool enabled, Action select = null)
        {
            var item = new PlaylistMenuItem(title, detail, icon) {
                AutoSize = false,
                Size = new Size(menuWidth, Theme.P(detail.Length == 0 ? 48 : 54)),
                Enabled = enabled
            };
            if (select != null) item.Click += (s, e) => select();
            Items.Add(item);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (Width < 2 || Height < 2) return;
            using (var shape = Theme.Round(ClientRectangle, Theme.P(14))) Region = new Region(shape);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) { titleFont.Dispose(); detailFont.Dispose(); }
        }

        sealed class PlaylistMenuItem : ToolStripMenuItem
        {
            public readonly string Title, Detail, Icon;

            public PlaylistMenuItem(string title, string detail, string icon) : base(title.Replace("&", "&&"))
            {
                Title = title; Detail = detail; Icon = icon;
                AccessibleName = title;
                AccessibleDescription = detail;
                ToolTipText = title;
            }
        }

        sealed class PlaylistMenuRenderer : ToolStripProfessionalRenderer
        {
            readonly Font titleFont, detailFont;
            const TextFormatFlags TextFlags = TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

            public PlaylistMenuRenderer(Font titleFont, Font detailFont) : base(new LibraryMenuColors())
            {
                this.titleFont = titleFont;
                this.detailFont = detailFont;
            }

            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                using (var fill = new SolidBrush(Theme.Panel)) e.Graphics.FillRectangle(fill, e.AffectedBounds);
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var shape = Theme.Round(new RectangleF(.5f, .5f, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1), Theme.P(14)))
                using (var pen = new Pen(Theme.Track, Theme.Scale)) e.Graphics.DrawPath(pen, shape);
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                var item = e.Item as PlaylistMenuItem;
                if (item == null) { base.OnRenderMenuItemBackground(e); return; }
                bool highlighted = item.Selected;
                bool create = item.Icon == "plus";
                var row = Rectangle.Inflate(new Rectangle(Point.Empty, item.Size), -Theme.P(8), -Theme.P(4));
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                if (create || highlighted)
                {
                    Color background = item.Pressed ? Theme.Pressed : highlighted ? Theme.Raised : Theme.AccentDim;
                    using (var shape = Theme.Round(row, Theme.P(9)))
                    using (var fill = new SolidBrush(background))
                    {
                        e.Graphics.FillPath(fill, shape);
                        if (highlighted)
                            using (var pen = new Pen(item.Enabled ? Theme.Focus : Theme.Muted, Theme.Scale)) e.Graphics.DrawPath(pen, shape);
                    }
                }
                if (item.Icon.Length == 0) return;

                int side = Theme.P(30), glyph = Theme.P(17);
                var tile = new Rectangle(Theme.P(16), (item.Height - side) / 2, side, side);
                using (var shape = Theme.Round(tile, Theme.P(8)))
                using (var fill = new SolidBrush(create ? Theme.Accent : Theme.Raised)) e.Graphics.FillPath(fill, shape);
                Color iconColor = create ? Theme.Bg : item.Enabled ? Theme.Muted : Theme.Accent;
                Theme.Icon(e.Graphics, item.Icon, new Rectangle(tile.X + (side - glyph) / 2, tile.Y + (side - glyph) / 2, glyph, glyph), iconColor);
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                var item = e.Item as PlaylistMenuItem;
                if (item == null) { base.OnRenderItemText(e); return; }

                int left = Theme.P(item.Icon.Length == 0 ? 16 : 56);
                var text = new Rectangle(left, 0, item.Width - left - Theme.P(16), item.Height);
                if (item.Detail.Length > 0)
                {
                    text.Y = Theme.P(7);
                    text.Height = Theme.P(21);
                    var detail = new Rectangle(text.X, text.Bottom, text.Width, Theme.P(17));
                    TextRenderer.DrawText(e.Graphics, item.Detail, detailFont, detail, Theme.Muted, TextFlags);
                }
                Color color = item.Icon == "plus" ? Theme.Accent : item.Enabled ? Theme.Text : Theme.Muted;
                var flags = item.Icon.Length == 0 ? TextFlags | TextFormatFlags.HorizontalCenter : TextFlags;
                TextRenderer.DrawText(e.Graphics, item.Title, item.Icon.Length == 0 ? detailFont : titleFont, text, color, flags);
            }
        }
    }
}
