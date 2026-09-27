using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ClaudeUsageTray
{
    // Draws the tray icon (a rounded badge with a short number) at runtime.
    static class IconRenderer
    {
        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr handle);

        public static readonly Color Accent = Color.FromArgb(217, 119, 87);
        public static readonly Color Idle = Color.FromArgb(110, 110, 110);
        public static readonly Color Over = Color.FromArgb(226, 84, 84);

        // Returns an icon that owns a native handle; release it with Free().
        public static Icon Render(string text, Color background)
        {
            int size = Math.Max(16, SystemInformation.SmallIconSize.Width);
            using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    g.Clear(Color.Transparent);

                    using (var path = RoundedRect(new RectangleF(0, 0, size - 1, size - 1), size / 4f))
                    using (var brush = new SolidBrush(background))
                        g.FillPath(brush, path);

                    using (var fmt = new StringFormat(StringFormat.GenericTypographic))
                    {
                        fmt.Alignment = StringAlignment.Center;
                        fmt.LineAlignment = StringAlignment.Center;
                        fmt.FormatFlags |= StringFormatFlags.NoWrap;

                        // Shrink the font until the text fits the badge.
                        float em = size * 0.72f;
                        Font font = null;
                        while (true)
                        {
                            font = new Font("Segoe UI", em, FontStyle.Bold, GraphicsUnit.Pixel);
                            if (g.MeasureString(text, font, PointF.Empty, fmt).Width <= size - 1 || em <= 6) break;
                            font.Dispose();
                            em -= 0.5f;
                        }
                        using (font)
                            g.DrawString(text, font, Brushes.White, new RectangleF(0, 0.5f, size, size), fmt);
                    }
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        public static void Free(Icon icon)
        {
            if (icon == null) return;
            IntPtr handle = icon.Handle;
            icon.Dispose();
            DestroyIcon(handle);
        }

        internal static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            var path = new GraphicsPath();
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
