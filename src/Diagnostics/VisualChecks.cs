using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace SadeSes
{
    static class VisualChecks
    {
        class ButtonProbe : SoftButton
        {
            public bool HoverState, PressedState, FocusState;
            protected override bool DrawHover { get { return HoverState; } }
            protected override bool DrawPressed { get { return PressedState; } }
            protected override bool DrawKeyboardFocus { get { return FocusState; } }
        }

        public static void Run(string reportPath)
        {
            string folder = Path.GetDirectoryName(reportPath); Directory.CreateDirectory(folder);
            var lines = new List<string>(); float originalScale = Theme.Scale;
            int cases = 0;
            using (var parent = new Panel { BackColor = Theme.Sidebar, Size = new Size(400, 400) })
            using (var probe = new ButtonProbe { Quiet = true, Glyph = "previous", BackColor = Theme.Accent })
            {
                parent.Controls.Add(probe); parent.CreateControl(); probe.CreateControl();
                foreach (float scale in new[] { 1f, 1.25f, 1.5f, 2f })
                {
                    Theme.Scale = scale;
                    foreach (int side in new[] { 36, 44, 52 })
                    {
                        probe.Size = new Size(Theme.P(side), Theme.P(side)); probe.Font = Theme.Font(13, true);
                        foreach (string state in new[] { "idle", "hover", "pressed", "focus", "active", "disabled" })
                        {
                            SetState(probe, state);
                            foreach (bool primary in new[] { false, true })
                            {
                                probe.Primary = primary;
                                using (var bitmap = new Bitmap(probe.Width, probe.Height))
                                {
                                    using (var graphics = Graphics.FromImage(bitmap)) graphics.Clear(Color.Magenta);
                                    probe.DrawToBitmap(bitmap, new Rectangle(Point.Empty, probe.Size));
                                    foreach (var point in new[] { new Point(0, 0), new Point(bitmap.Width - 1, 0), new Point(0, bitmap.Height - 1), new Point(bitmap.Width - 1, bitmap.Height - 1) })
                                        if (bitmap.GetPixel(point.X, point.Y).ToArgb() != parent.BackColor.ToArgb()) throw new Exception("Button corner leaked in " + state + " at scale " + scale + " and size " + side);
                                }
                                cases++;
                            }
                        }
                    }
                }
            }
            Theme.Scale = originalScale;
            lines.Add("Rounded button background/state/resize regression: PASS (" + cases + " bitmap cases, 100/125/150/200% scale)");
            if (Contrast(Theme.Text, Theme.Panel) < 4.5 || Contrast(Theme.Muted, Theme.Panel) < 4.5 || Contrast(Theme.Bg, Theme.Accent) < 4.5 || Contrast(Theme.Track, Theme.Sidebar) < 3 || Contrast(Theme.Focus, Theme.Raised) < 3) throw new Exception("Dark theme contrast regression");
            lines.Add("Dark theme text, primary action, slider track and focus contrast: PASS");
            RenderStates(Path.Combine(folder, "button-states.png"));
            foreach (float scale in new[] { 1f, 1.5f, 2f })
            {
                using (var form = new MainForm(true, scale)) form.CheckInterface(Path.Combine(folder, "layout-" + (int)(scale * 100) + ".txt"));
                using (var form = new MainForm(true, scale)) form.RenderPreview(Path.Combine(folder, "window-" + (int)(scale * 100) + ".png"));
                lines.Add("Full interface, wheel routing and control layout: PASS at " + (int)(scale * 100) + "%");
            }
            LibraryTests.Run(Path.Combine(folder, "library-storage.txt"));
            lines.Add("Playlist/favorites storage, atomic backup and reload: PASS");
            foreach (string page in new[] { "playlists", "playlist", "favorites" }) using (var form = new MainForm(true, 1.5f)) form.RenderLibraryPreview(Path.Combine(folder, page + ".png"), page);
            Theme.Scale = originalScale;
            File.WriteAllLines(reportPath, lines, new System.Text.UTF8Encoding(false));
        }
        static double Contrast(Color a, Color b) { double first = Luminance(a), second = Luminance(b); return (Math.Max(first, second) + .05) / (Math.Min(first, second) + .05); }
        static double Luminance(Color c)
        {
            Func<byte, double> linear = value => { double v = value / 255.0; return v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4); };
            return .2126 * linear(c.R) + .7152 * linear(c.G) + .0722 * linear(c.B);
        }

        static void SetState(ButtonProbe probe, string state)
        {
            probe.Enabled = state != "disabled"; probe.Active = state == "active";
            probe.HoverState = state == "hover"; probe.PressedState = state == "pressed"; probe.FocusState = state == "focus";
        }

        static void RenderStates(string path)
        {
            using (var panel = new Panel { BackColor = Theme.Sidebar, Size = new Size(Theme.P(650), Theme.P(460)) })
            using (var image = new Bitmap(panel.Width, panel.Height))
            using (var graphics = Graphics.FromImage(image))
            using (var labelFont = Theme.Font(12, false))
            {
                panel.CreateControl(); graphics.Clear(Theme.Sidebar);
                string[] states = { "idle", "hover", "pressed", "focus", "active", "disabled" };
                for (int row = 0; row < states.Length; row++)
                {
                    int y = Theme.P(18 + row * 70);
                    TextRenderer.DrawText(graphics, states[row], labelFont, new Rectangle(Theme.P(16), y, Theme.P(110), Theme.P(52)), Theme.Muted, TextFormatFlags.VerticalCenter);
                    string[] icons = { "previous", "play", "next", "repeat" };
                    for (int col = 0; col < icons.Length; col++)
                    {
                        using (var button = new ButtonProbe { Quiet = true, Glyph = icons[col], Primary = col == 1, Size = new Size(Theme.P(52), Theme.P(52)), BackColor = Theme.Accent })
                        {
                            panel.Controls.Add(button); button.CreateControl(); SetState(button, states[row]);
                            using (var bitmap = new Bitmap(button.Width, button.Height)) { button.DrawToBitmap(bitmap, new Rectangle(Point.Empty, button.Size)); graphics.DrawImageUnscaled(bitmap, Theme.P(140 + col * 78), y); }
                        }
                    }
                    using (var button = new ButtonProbe { Text = "Şimdi çal", Primary = true, Size = new Size(Theme.P(144), Theme.P(44)), BackColor = Theme.Accent })
                    {
                        panel.Controls.Add(button); button.CreateControl(); SetState(button, states[row]);
                        using (var bitmap = new Bitmap(button.Width, button.Height)) { button.DrawToBitmap(bitmap, new Rectangle(Point.Empty, button.Size)); graphics.DrawImageUnscaled(bitmap, Theme.P(480), y + Theme.P(4)); }
                    }
                }
                image.Save(path, ImageFormat.Png);
            }
        }
    }
}
