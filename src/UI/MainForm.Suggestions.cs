using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SadeSes
{
    public partial class MainForm
    {
        readonly Panel suggestionCard = new Panel();
        readonly Label suggestionHeading = new Label();
        readonly SmoothList suggestions = new SmoothList();
        CancellationTokenSource suggestionRequest;

        void BuildSearchSuggestions()
        {
            suggestionCard.Visible = false;
            suggestionCard.BackColor = Theme.Panel;
            StyleSearchSurface(suggestionCard, 14);
            LabelStyle(suggestionHeading, "ARAMA ÖNERİLERİ", 11, Theme.Muted, true);
            suggestionCard.Controls.Add(suggestionHeading);

            suggestions.TabStop = false;
            suggestions.AccessibleName = "Arama önerileri";
            suggestions.BackColor = Theme.Panel;
            suggestions.ForeColor = Theme.Text;
            suggestions.Font = Theme.Font(14, false);
            suggestions.BorderStyle = BorderStyle.None;
            suggestions.Cursor = Cursors.Hand;
            suggestions.DrawMode = DrawMode.OwnerDrawFixed;
            suggestions.IntegralHeight = false;
            suggestions.ItemHeight = Theme.P(36);
            suggestions.DrawItem += DrawSuggestion;
            suggestionCard.Controls.Add(suggestions);
            content.Controls.Add(suggestionCard);

            search.TextChanged += async (s, e) => await RefreshSuggestions();
            search.PreviewKeyDown += (s, e) =>
            {
                if (suggestions.Visible && (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down || e.KeyCode == Keys.Escape)) e.IsInputKey = true;
            };
            search.KeyDown += SearchKeyDown;
            suggestions.KeyDown += SearchKeyDown;
            search.Leave += CheckSuggestionFocus;
            suggestions.Leave += CheckSuggestionFocus;
            suggestions.MouseClick += async (s, e) =>
            {
                int index = suggestions.IndexFromPoint(e.Location);
                if (e.Button == MouseButtons.Left && index >= 0) await SearchSuggestion((string)suggestions.Items[index]);
            };
            Deactivate += (s, e) => HideSuggestions();
            FormClosing += (s, e) => HideSuggestions();
        }

        static void StyleSearchSurface(Panel surface, int radius)
        {
            surface.SizeChanged += (s, e) =>
            {
                if (surface.Width < 2 || surface.Height < 2) return;
                using (var shape = Theme.Round(surface.ClientRectangle, Theme.P(radius))) surface.Region = new Region(shape);
                surface.Invalidate();
            };
            surface.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var outline = Theme.Round(new RectangleF(.5f, .5f, surface.Width - 1, surface.Height - 1), Theme.P(radius)))
                using (var pen = new Pen(Theme.Border, Theme.Scale)) e.Graphics.DrawPath(pen, outline);
            };
        }

        void LayoutSearchSuggestions()
        {
            int width = Math.Min(searchBox.Width, Theme.P(520)), inset = Theme.P(6), rowsTop = Theme.P(28);
            int rowsHeight = suggestions.Items.Count * suggestions.ItemHeight;
            Bounds(suggestionCard, searchBox.Left, searchBox.Bottom + Theme.P(7), width, rowsTop + rowsHeight + inset);
            Bounds(suggestionHeading, Theme.P(16), Theme.P(4), width - Theme.P(32), Theme.P(22));
            Bounds(suggestions, inset, rowsTop, width - 2 * inset, rowsHeight);
            if (suggestionCard.Visible) suggestionCard.BringToFront();
        }

        void DrawSuggestion(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= suggestions.Items.Count) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            bool hovered = e.Index == suggestions.HoverIndex;
            var row = Rectangle.Inflate(e.Bounds, 0, -Theme.P(1));
            if (selected || hovered)
            {
                using (var shape = Theme.Round(row, Theme.P(8)))
                using (var fill = new SolidBrush(selected ? Theme.AccentDim : Theme.Raised)) g.FillPath(fill, shape);
            }
            if (selected || (hovered && suggestions.SelectedIndex < 0))
            {
                int arrow = Theme.P(14);
                Theme.Icon(g, "enter", new Rectangle(row.Right - Theme.P(14) - arrow, row.Y + (row.Height - arrow) / 2, arrow, arrow), selected ? Theme.Accent : Theme.Muted);
            }
            if (selected)
            {
                using (var rail = Theme.Round(new Rectangle(row.X + Theme.P(3), row.Y + Theme.P(8), Theme.P(3), row.Height - Theme.P(16)), Theme.P(2)))
                using (var fill = new SolidBrush(Theme.Accent)) g.FillPath(fill, rail);
            }

            int icon = Theme.P(16);
            Theme.Icon(g, "search", new Rectangle(row.X + Theme.P(12), row.Y + (row.Height - icon) / 2, icon, icon), selected ? Theme.Accent : Theme.Muted);
            var textBounds = new Rectangle(row.X + Theme.P(42), row.Y, row.Width - Theme.P(82), row.Height);
            const TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            string text = (string)suggestions.Items[e.Index];
            string query = search.Text.Trim();
            if (query.Length > 0 && query.Length < text.Length && text.StartsWith(query, true, CultureInfo.GetCultureInfo("tr-TR")))
            {
                string prefix = text.Substring(0, query.Length);
                int prefixWidth = TextRenderer.MeasureText(g, prefix, e.Font, Size.Empty, flags).Width;
                if (prefixWidth < textBounds.Width)
                {
                    TextRenderer.DrawText(g, prefix, e.Font, textBounds, selected ? Theme.Text : Theme.Muted, flags);
                    textBounds.X += prefixWidth;
                    textBounds.Width -= prefixWidth;
                    text = text.Substring(query.Length);
                }
            }
            TextRenderer.DrawText(g, text, e.Font, textBounds, selected ? Theme.Accent : Theme.Text, flags | TextFormatFlags.EndEllipsis);
        }

        async Task RefreshSuggestions()
        {
            HideSuggestions();
            string query = search.Text.Trim();
            if (preview || closing || updating || !search.Focused || !YouTubeSearch.CanSuggest(query)) return;

            var request = new CancellationTokenSource();
            suggestionRequest = request;
            try
            {
                await Task.Delay(250, request.Token);
                var found = await YouTubeSearch.Suggest(query, request.Token);
                if (suggestionRequest != request || !search.Focused || found.Count == 0) return;
                ShowSuggestions(found.ToArray());
            }
            catch (OperationCanceledException) { }
            catch (System.Net.Http.HttpRequestException) { }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            finally
            {
                if (suggestionRequest == request) suggestionRequest = null;
                request.Dispose();
            }
        }

        void ShowSuggestions(string[] found)
        {
            suggestions.Items.Clear();
            suggestions.Items.AddRange(found);
            LayoutSearchSuggestions();
            suggestionCard.BringToFront();
            suggestionCard.Show();
        }

        void CancelSuggestions()
        {
            var request = suggestionRequest;
            suggestionRequest = null;
            if (request != null) request.Cancel();
        }

        void HideSuggestions()
        {
            CancelSuggestions();
            suggestionCard.Hide();
            suggestions.HoverIndex = -1;
        }

        void CheckSuggestionFocus(object sender, EventArgs e)
        {
            if (closing || !IsHandleCreated) return;
            BeginInvoke((Action)(() =>
            {
                if (!closing && !search.Focused && !suggestions.Focused) HideSuggestions();
            }));
        }

        async void SearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                if (e.Modifiers == Keys.None && suggestions.Visible && suggestions.SelectedItem is string) await SearchSuggestion((string)suggestions.SelectedItem);
                else await Search();
                return;
            }
            if (e.Modifiers != Keys.None) return;
            if (e.KeyCode == Keys.Escape && (suggestions.Visible || suggestionRequest != null))
            {
                e.SuppressKeyPress = true;
                search.Focus();
                HideSuggestions();
            }
            else if (sender == search && suggestions.Visible && (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down))
            {
                e.SuppressKeyPress = true;
                int index = suggestions.SelectedIndex;
                if (e.KeyCode == Keys.Down) suggestions.SelectedIndex = Math.Min(index + 1, suggestions.Items.Count - 1);
                else suggestions.SelectedIndex = index < 0 ? suggestions.Items.Count - 1 : index - 1;
            }
        }

        async Task SearchSuggestion(string query)
        {
            search.Focus();
            search.Text = query;
            search.SelectionStart = query.Length;
            await Search();
        }
    }
}
