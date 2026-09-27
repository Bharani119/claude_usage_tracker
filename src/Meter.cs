using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ClaudeUsageTray
{
    // Flat rounded progress bar. The stock ProgressBar ignores custom colours under visual styles.
    class Meter : Control
    {
        public double Value;   // 0..1, clamped when drawn
        public Color Fill = IconRenderer.Accent;
        public Color Track = Color.FromArgb(58, 58, 58);

        public Meter()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : BackColor);

            var track = new RectangleF(0, 0, Width - 1, Height - 1);
            float radius = track.Height / 2;
            using (var path = IconRenderer.RoundedRect(track, radius))
            using (var brush = new SolidBrush(Track))
                g.FillPath(brush, path);

            double v = Math.Max(0, Math.Min(1, Value));
            if (v <= 0) return;
            // Never narrower than the bar is tall, so small values still show a rounded pill.
            float w = Math.Max(track.Height, (float)(track.Width * v));
            using (var path = IconRenderer.RoundedRect(new RectangleF(0, 0, w, track.Height), radius))
            using (var brush = new SolidBrush(Fill))
                g.FillPath(brush, path);
        }
    }
}
