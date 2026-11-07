using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SadeSes
{
    class SoftButton : Button
    {
        public string Glyph = "";
        public bool Primary, Active, Quiet;
        bool hover, mousePressed, keyPressed;
        protected virtual bool DrawHover { get { return hover; } }
        protected virtual bool DrawPressed { get { return mousePressed || keyPressed; } }
        protected virtual bool DrawKeyboardFocus { get { return Focused && ShowFocusCues; } }
        public SoftButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Opaque, true);
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; UseVisualStyleBackColor = false; Cursor = Cursors.Hand;
            Font = Theme.Font(13, true); ForeColor = Theme.Text; BackColor = Theme.Bg; TabStop = true;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) mousePressed = true; base.OnMouseDown(e); Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { mousePressed = false; base.OnMouseUp(e); Invalidate(); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) keyPressed = true; base.OnKeyDown(e); Invalidate(); }
        protected override void OnKeyUp(KeyEventArgs e) { if (e.KeyCode == Keys.Space) keyPressed = false; base.OnKeyUp(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { mousePressed = keyPressed = false; base.OnLostFocus(e); Invalidate(); }
        protected override void OnMouseCaptureChanged(EventArgs e) { if (!Capture) mousePressed = false; base.OnMouseCaptureChanged(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { mousePressed = keyPressed = hover = false; base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(Parent == null ? Theme.Bg : Parent.BackColor); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            // Paint the entire client area on every state transition. Native button
            // background colors must never remain outside the rounded foreground.
            Color canvas = Parent == null ? Theme.Bg : Parent.BackColor;
            g.Clear(canvas); g.SmoothingMode = SmoothingMode.AntiAlias;
            bool pressed = Enabled && DrawPressed, hovered = Enabled && DrawHover;
            Color color = !Enabled ? Theme.DisabledText : Primary ? Theme.Bg : Active ? Theme.Accent : Theme.Text;
            Color bg = !Enabled ? (Quiet ? canvas : Theme.Panel) : Primary ? (pressed ? Theme.AccentPressed : hovered ? Color.FromArgb(216, 253, 153) : Theme.Accent) : pressed ? Theme.Pressed : Active ? Theme.AccentDim : hovered ? Theme.Raised : Quiet ? canvas : Theme.Panel;
            float radius = Text.Length == 0 && Primary ? Math.Min(Width, Height) / 2f - 1 : Theme.P(10);
            using (var shape = Theme.Round(new RectangleF(.5f, .5f, Width - 1, Height - 1), radius)) using (var brush = new SolidBrush(bg)) g.FillPath(brush, shape);
            int size = Theme.P(Text.Length == 0 ? 21 : 17), left = Theme.P(14);
            if (Glyph.Length > 0)
            {
                if (Text.Length == 0) left = (Width - size) / 2;
                Theme.Icon(g, Glyph, new Rectangle(left, (Height - size) / 2, size, size), color);
            }
            var textRect = new Rectangle(Glyph.Length > 0 ? left + size + Theme.P(9) : Theme.P(9), 0, Width - (Glyph.Length > 0 ? left + size + Theme.P(17) : Theme.P(18)), Height);
            if (Text.Length > 0) TextRenderer.DrawText(g, Text, Font, textRect, color, TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | (Glyph.Length == 0 ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left));
            if (Active && Text.Length == 0 && !Primary)
                using (var brush = new SolidBrush(Theme.Accent)) g.FillEllipse(brush, Width / 2f - Theme.P(2), Height - Theme.P(6), Theme.P(4), Theme.P(4));
            if (Active && Text.Length > 0 && !Primary)
                using (var pen = new Pen(Theme.Accent, Math.Max(2, Theme.P(2)))) { pen.StartCap = pen.EndCap = LineCap.Round; g.DrawLine(pen, Theme.P(3), Height / 2f - Theme.P(9), Theme.P(3), Height / 2f + Theme.P(9)); }
            if (Enabled && DrawKeyboardFocus)
                using (var pen = new Pen(Primary ? Theme.Bg : Theme.Focus, Math.Max(1.5f, Theme.Scale * 1.5f)))
                using (var shape = Theme.Round(new RectangleF(Theme.P(3), Theme.P(3), Width - Theme.P(6) - 1, Height - Theme.P(6) - 1), Math.Max(2, radius - Theme.P(3)))) g.DrawPath(pen, shape);
        }
    }

    class WrappedText : Control
    {
        public WrappedText() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true); BackColor = Color.Transparent; }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var brush = new SolidBrush(ForeColor))
            using (var format = new StringFormat { Trimming = StringTrimming.EllipsisWord, FormatFlags = StringFormatFlags.LineLimit })
                e.Graphics.DrawString(Text, Font, brush, new RectangleF(0, 0, Width, Height), format);
        }
    }

    class CoverView : Control
    {
        public Image Artwork;
        public CoverView() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); BackColor = Theme.Panel; }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Theme.Round(new RectangleF(0, 0, Width - 1, Height - 1), Theme.P(10)))
            {
                using (var fill = new SolidBrush(Theme.Raised)) g.FillPath(fill, path);
                if (Artwork != null)
                {
                    var state = g.Save(); g.SetClip(path); g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    float ratio = Math.Max(Width / (float)Artwork.Width, Height / (float)Artwork.Height);
                    float w = Artwork.Width * ratio, h = Artwork.Height * ratio;
                    g.DrawImage(Artwork, (Width - w) / 2, (Height - h) / 2, w, h); g.Restore(state);
                }
                else { int size = Math.Min(Width, Height) / 3; Theme.Icon(g, "wave", new Rectangle((Width - size) / 2, (Height - size) / 2, size, size), Color.FromArgb(95, 119, 85)); }
            }
        }
    }

    class SmoothList : ListBox
    {
        public int HoverIndex = -1;
        int wheelRemainder;
        public event Action ViewChanged;
        public SmoothList() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
        protected override CreateParams CreateParams { get { var parameters = base.CreateParams; parameters.Style &= ~0x200000; return parameters; } }
        public void InvalidateItem(int index) { if (index >= 0 && index < Items.Count) Invalidate(GetItemRectangle(index)); }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); int index = IndexFromPoint(e.Location); if (index != HoverIndex) { int old = HoverIndex; HoverIndex = index; InvalidateItem(old); InvalidateItem(index); } }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); int old = HoverIndex; HoverIndex = -1; InvalidateItem(old); }
        protected override void OnSelectedIndexChanged(EventArgs e) { base.OnSelectedIndexChanged(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            using (var brush = new SolidBrush(BackColor)) e.Graphics.FillRectangle(brush, e.ClipRectangle);
            int top = Math.Max(0, TopIndex), last = Math.Min(Items.Count, top + Height / Math.Max(1, ItemHeight) + 2);
            for (int index = top; index < last; index++)
            {
                var rect = GetItemRectangle(index); if (!e.ClipRectangle.IntersectsWith(rect)) continue;
                DrawItemState state = index == SelectedIndex ? DrawItemState.Selected : DrawItemState.None; if (Focused && index == SelectedIndex) state |= DrawItemState.Focus;
                OnDrawItem(new DrawItemEventArgs(e.Graphics, Font, rect, index, state, ForeColor, BackColor));
            }
            base.OnPaint(e);
        }
        public void ScrollWheel(int delta)
        {
            wheelRemainder += delta; int steps = wheelRemainder / 120; wheelRemainder %= 120;
            if (steps == 0 || Items.Count == 0) return;
            int visible = Math.Max(1, ClientSize.Height / Math.Max(1, ItemHeight));
            int lines = SystemInformation.MouseWheelScrollLines;
            if (lines == 0) return;
            if (lines < 0) lines = visible;
            int next = Math.Max(0, Math.Min(Math.Max(0, Items.Count - visible), TopIndex - steps * lines));
            if (next == TopIndex) return;
            TopIndex = next; Invalidate(); if (ViewChanged != null) ViewChanged();
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x14) { m.Result = new IntPtr(1); return; }
            if (m.Msg == 0x202B) { m.Result = new IntPtr(1); Invalidate(); return; }
            if (m.Msg == 0x20A) { ScrollWheel(unchecked((short)((m.WParam.ToInt64() >> 16) & 0xffff))); m.Result = IntPtr.Zero; return; }
            base.WndProc(ref m); if ((m.Msg == 0x115 || m.Msg == 0x101 || m.Msg == 0x197) && ViewChanged != null) ViewChanged();
        }
    }

    class ResultsWheelFilter : IMessageFilter
    {
        readonly Form owner;
        SmoothList target;
        public ResultsWheelFilter(Form form, SmoothList list) { owner = form; target = list; }
        public void SetTarget(SmoothList list) { target = list; }
        public bool PreFilterMessage(ref Message message)
        {
            if (message.Msg != 0x20A || owner.IsDisposed || !owner.Visible || !owner.Enabled || !target.Visible || !target.IsHandleCreated) return false;
            var recipient = Control.FromChildHandle(message.HWnd);
            if (recipient == null || recipient.FindForm() != owner) return false;
            long position = message.LParam.ToInt64();
            var point = new Point(unchecked((short)(position & 0xffff)), unchecked((short)((position >> 16) & 0xffff)));
            if (!target.RectangleToScreen(target.ClientRectangle).Contains(point)) return false;
            target.ScrollWheel(unchecked((short)((message.WParam.ToInt64() >> 16) & 0xffff)));
            return true;
        }
    }

    class DarkScroll : Control
    {
        public SmoothList Target;
        bool dragging;
        public DarkScroll() { BackColor = Theme.Bg; Cursor = Cursors.Hand; AccessibleRole = AccessibleRole.ScrollBar; AccessibleName = "Şarkı listesini kaydır"; SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); }
        Rectangle Thumb()
        {
            int visible = Math.Max(1, Target.Height / Math.Max(1, Target.ItemHeight));
            if (Target.Items.Count <= visible) return Rectangle.Empty;
            int height = Math.Max(Theme.P(32), Height * visible / Target.Items.Count);
            int y = (int)((Height - height) * Math.Min(1, Target.TopIndex / (double)Math.Max(1, Target.Items.Count - visible)));
            return new Rectangle((Width - Theme.P(5)) / 2, y, Theme.P(5), height);
        }
        protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); if (Target == null) return; Rectangle thumb = Thumb(); if (thumb.IsEmpty) return; e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using (var path = Theme.Round(thumb, Theme.P(3))) using (var brush = new SolidBrush(Theme.Border)) e.Graphics.FillPath(brush, path); }
        void MoveTo(int y) { if (Target == null || Target.Items.Count == 0) return; Rectangle thumb = Thumb(); int visible = Math.Max(1, Target.Height / Target.ItemHeight); int index = (int)Math.Round((y - thumb.Height / 2.0) / Math.Max(1, Height - thumb.Height) * Math.Max(0, Target.Items.Count - visible)); Target.TopIndex = Math.Max(0, Math.Min(Target.Items.Count - 1, index)); Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button != MouseButtons.Left) return; dragging = Capture = true; MoveTo(e.Y); }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (dragging) MoveTo(e.Y); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); dragging = Capture = false; }
        protected override void OnMouseWheel(MouseEventArgs e) { if (Target != null) { Target.ScrollWheel(e.Delta); Invalidate(); } }
    }
}
