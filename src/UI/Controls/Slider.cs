using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SadeSes
{
    public class Slider : Control
    {
        public double Maximum = 100;
        public double Value;
        public Color Accent = Theme.Accent;
        public event Action<double> Changed;
        bool dragging;
        public Slider() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable | ControlStyles.ResizeRedraw, true); Height = 24; Cursor = Cursors.Hand; TabStop = true; AccessibleRole = AccessibleRole.Slider; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int y = Height / 2; float width = Math.Max(1, Width - 14); float filled = (float)(Math.Max(0, Math.Min(Value / Math.Max(1, Maximum), 1)) * width);
            using (var b = new SolidBrush(Theme.Track)) e.Graphics.FillRectangle(b, 7, y - 2, width, 4);
            using (var b = new SolidBrush(Accent)) { e.Graphics.FillRectangle(b, 7, y - 2, filled, 4); e.Graphics.FillEllipse(b, 2 + filled, y - 5, 10, 10); }
            if (Focused) using (var pen = new Pen(Theme.Focus, Math.Max(1, Theme.Scale))) e.Graphics.DrawRectangle(pen, 1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
        }
        void SetAt(int x) { Value = Math.Max(0, Math.Min(Maximum, (x - 7) / (double)Math.Max(1, Width - 14) * Maximum)); Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button != MouseButtons.Left) return; Focus(); dragging = true; Capture = true; SetAt(e.X); }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (dragging) SetAt(e.X); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); if (dragging) { dragging = false; Capture = false; SetAt(e.X); if (Changed != null) Changed(Value); } }
        protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right) { Value = Math.Max(0, Math.Min(Maximum, Value + (e.KeyCode == Keys.Left ? -1 : 1) * Math.Max(1, Maximum / 100))); Invalidate(); if (Changed != null) Changed(Value); e.Handled = true; } }
        public void UpdateValue(double v) { if (!dragging) { Value = v; Invalidate(); } }
    }
}
