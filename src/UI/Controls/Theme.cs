using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace SadeSes
{
    static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(14, 17, 23), Sidebar = Color.FromArgb(10, 13, 18), Panel = Color.FromArgb(23, 28, 36), Raised = Color.FromArgb(31, 37, 47), Border = Color.FromArgb(37, 44, 55), Text = Color.FromArgb(240, 243, 247), Muted = Color.FromArgb(143, 155, 174), Accent = Color.FromArgb(199, 241, 113), AccentDim = Color.FromArgb(41, 52, 35), Pressed = Color.FromArgb(46, 55, 66), AccentPressed = Color.FromArgb(174, 211, 94), DisabledText = Color.FromArgb(117, 129, 146), Focus = Color.FromArgb(225, 250, 184), Track = Color.FromArgb(85, 99, 117);
        public static readonly Color InputBorder = Color.FromArgb(104, 116, 132), Error = Color.FromArgb(255, 165, 152);
        public static float Scale = 1;
        static readonly PrivateFontCollection regularFonts = new PrivateFontCollection(), extraBoldFonts = new PrivateFontCollection();
        static FontFamily regularFamily, extraBoldFamily;
        static string fontRegistration = "";
        [System.Runtime.InteropServices.DllImport("gdi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)] static extern int AddFontResourceEx(string file, uint flags, IntPtr reserved);
        static Theme()
        {
            try
            {
                string folder = Path.Combine(AppPaths.Assets, "fonts");
                string normal = Path.Combine(folder, "Montserrat-Regular.ttf"), heavy = Path.Combine(folder, "Montserrat-ExtraBold.ttf");
                if (File.Exists(normal)) { int count = AddFontResourceEx(normal, 0x10, IntPtr.Zero); fontRegistration += " normal=" + count + "/" + System.Runtime.InteropServices.Marshal.GetLastWin32Error(); regularFonts.AddFontFile(normal); regularFamily = regularFonts.Families[0]; }
                if (File.Exists(heavy)) { int count = AddFontResourceEx(heavy, 0x10, IntPtr.Zero); fontRegistration += " heavy=" + count + "/" + System.Runtime.InteropServices.Marshal.GetLastWin32Error(); extraBoldFonts.AddFontFile(heavy); extraBoldFamily = extraBoldFonts.Families[0]; }
            }
            catch { }
        }
        public static int P(float value) { return (int)Math.Round(value * Scale); }
        public static Font Font(float pixels, bool bold)
        {
            FontFamily family = bold ? extraBoldFamily : regularFamily;
            if (family == null) return new Font(bold ? "Segoe UI Semibold" : "Segoe UI", pixels * Scale, FontStyle.Regular, GraphicsUnit.Pixel);
            FontStyle style = family.IsStyleAvailable(FontStyle.Regular) ? FontStyle.Regular : FontStyle.Bold;
            return new Font(family, pixels * Scale, style, GraphicsUnit.Pixel);
        }
        [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr item);
        [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr item);
        [System.Runtime.InteropServices.DllImport("gdi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)] static extern int GetTextFace(IntPtr dc, int count, StringBuilder name);
        public static string RenderedFontFace(Font font)
        {
            using (var graphics = Graphics.FromHwnd(IntPtr.Zero))
            {
                IntPtr dc = graphics.GetHdc(), handle = font.ToHfont(), previous = SelectObject(dc, handle);
                try { var name = new StringBuilder(128); GetTextFace(dc, name.Capacity, name); return name.ToString(); }
                finally { SelectObject(dc, previous); DeleteObject(handle); graphics.ReleaseHdc(dc); }
            }
        }
        public static string FontRegistration { get { return fontRegistration; } }
        public static GraphicsPath Round(RectangleF rect, float radius)
        {
            radius = Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2); float d = Math.Max(1, radius * 2);
            var path = new GraphicsPath(); path.AddArc(rect.X, rect.Y, d, d, 180, 90); path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90); path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90); path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
        }
        public static void Icon(Graphics g, string kind, Rectangle rect, Color color)
        {
            float x = rect.X, y = rect.Y, w = rect.Width, h = rect.Height;
            using (var pen = new Pen(color, Math.Max(1.6f, w / 11)))
            using (var fill = new SolidBrush(color))
            {
                pen.StartCap = pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;
                if (kind == "play") g.FillPolygon(fill, new[] { new PointF(x + w * .32f, y + h * .18f), new PointF(x + w * .32f, y + h * .82f), new PointF(x + w * .82f, y + h * .5f) });
                else if (kind == "pause") { g.FillRectangle(fill, x + w * .25f, y + h * .2f, w * .16f, h * .6f); g.FillRectangle(fill, x + w * .59f, y + h * .2f, w * .16f, h * .6f); }
                else if (kind == "next" || kind == "previous")
                {
                    bool next = kind == "next"; float a = next ? .2f : .8f, b = next ? .7f : .3f;
                    g.FillPolygon(fill, new[] { new PointF(x + w * a, y + h * .22f), new PointF(x + w * a, y + h * .78f), new PointF(x + w * b, y + h * .5f) });
                    float line = next ? .78f : .22f; g.DrawLine(pen, x + w * line, y + h * .23f, x + w * line, y + h * .77f);
                }
                else if (kind == "search") { g.DrawEllipse(pen, x + w * .12f, y + h * .1f, w * .55f, h * .55f); g.DrawLine(pen, x + w * .61f, y + h * .61f, x + w * .88f, y + h * .88f); }
                else if (kind == "enter")
                {
                    g.DrawLines(pen, new[] { new PointF(x + w * .83f, y + h * .24f), new PointF(x + w * .83f, y + h * .62f), new PointF(x + w * .18f, y + h * .62f) });
                    g.DrawLines(pen, new[] { new PointF(x + w * .41f, y + h * .39f), new PointF(x + w * .18f, y + h * .62f), new PointF(x + w * .41f, y + h * .85f) });
                }
                else if (kind == "plus") { g.DrawLine(pen, x + w * .5f, y + h * .2f, x + w * .5f, y + h * .8f); g.DrawLine(pen, x + w * .2f, y + h * .5f, x + w * .8f, y + h * .5f); }
                else if (kind == "check") g.DrawLines(pen, new[] { new PointF(x + w * .17f, y + h * .50f), new PointF(x + w * .41f, y + h * .73f), new PointF(x + w * .83f, y + h * .27f) });
                else if (kind == "stop") g.FillRectangle(fill, x + w * .26f, y + h * .26f, w * .48f, h * .48f);
                else if (kind == "repeat") { g.DrawArc(pen, x + w * .15f, y + h * .2f, w * .7f, h * .6f, 195, 160); g.DrawArc(pen, x + w * .15f, y + h * .2f, w * .7f, h * .6f, 15, 160); g.DrawLine(pen, x + w * .82f, y + h * .2f, x + w * .84f, y + h * .45f); g.DrawLine(pen, x + w * .16f, y + h * .55f, x + w * .18f, y + h * .8f); }
                else if (kind == "queue") { for (int i = 0; i < 3; i++) { float lineY = y + h * (.25f + .25f * i); g.DrawLine(pen, x + w * .16f, lineY, x + w * .83f, lineY); } }
                else if (kind == "heart" || kind == "heart-filled")
                {
                    using (var heart = new GraphicsPath())
                    {
                        heart.AddBezier(x + w * .5f, y + h * .86f, x - w * .04f, y + h * .5f, x + w * .04f, y + h * .06f, x + w * .33f, y + h * .18f);
                        heart.AddBezier(x + w * .33f, y + h * .18f, x + w * .43f, y + h * .2f, x + w * .5f, y + h * .3f, x + w * .5f, y + h * .3f);
                        heart.AddBezier(x + w * .5f, y + h * .3f, x + w * .75f, y - h * .08f, x + w * 1.14f, y + h * .3f, x + w * .5f, y + h * .86f); heart.CloseFigure();
                        if (kind == "heart-filled") g.FillPath(fill, heart); else g.DrawPath(pen, heart);
                    }
                }
                else if (kind == "library") { g.DrawRectangle(pen, x + w * .15f, y + h * .18f, w * .6f, h * .64f); g.DrawLine(pen, x + w * .34f, y + h * .18f, x + w * .34f, y + h * .82f); g.DrawLine(pen, x + w * .85f, y + h * .26f, x + w * .85f, y + h * .82f); }
                else if (kind == "volume") { g.DrawPolygon(pen, new[] { new PointF(x + w * .15f, y + h * .36f), new PointF(x + w * .35f, y + h * .36f), new PointF(x + w * .55f, y + h * .18f), new PointF(x + w * .55f, y + h * .82f), new PointF(x + w * .35f, y + h * .64f), new PointF(x + w * .15f, y + h * .64f) }); g.DrawArc(pen, x + w * .45f, y + h * .25f, w * .4f, h * .5f, -70, 140); }
                else if (kind == "refresh") { g.DrawArc(pen, x + w * .2f, y + h * .2f, w * .6f, h * .6f, 50, 285); g.DrawLine(pen, x + w * .8f, y + h * .18f, x + w * .8f, y + h * .43f); }
                else { for (int i = 0; i < 5; i++) { float len = new[] { .25f, .52f, .85f, .6f, .3f }[i]; float xx = x + w * (.16f + i * .17f); g.DrawLine(pen, xx, y + h * (.5f - len / 2), xx, y + h * (.5f + len / 2)); } }
            }
        }
    }
}
