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

                    using (var family = new FontFamily("Segoe UI"))
                    using (var fmt = new StringFormat(StringFormat.GenericTypographic))
                    {
                        fmt.FormatFlags |= StringFormatFlags.NoWrap;

                        // Shrink the font until the glyphs fit the badge, then centre
                        // them by their real ink bounds (the line box has uneven padding).
                        float em = size * 0.72f;
                        GraphicsPath textPath;
                        RectangleF b;
                        while (true)
                        {
                            textPath = new GraphicsPath();
                            textPath.AddString(text, family, (int)FontStyle.Bold, em, PointF.Empty, fmt);
                            b = textPath.GetBounds();
                            if (b.Width <= size - 2 || em <= 6) break;
                            textPath.Dispose();
                            em -= 0.5f;
                        }
                        using (textPath)
                        {
                            using (var m = new Matrix())
                            {
                                m.Translate((size - 1) / 2f - (b.Left + b.Width / 2f),
                                            (size - 1) / 2f - (b.Top + b.Height / 2f));
                                textPath.Transform(m);
                            }
                            g.FillPath(Brushes.White, textPath);
                        }
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
