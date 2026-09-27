using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClaudeUsageTray
{
    // Borderless popup shown above the tray icon; hides itself when it loses focus.
    class FlyoutForm : Form
    {
        static readonly Color Bg = Color.FromArgb(32, 32, 32);
        static readonly Color Fg = Color.FromArgb(240, 240, 240);
        static readonly Color Dim = Color.FromArgb(155, 155, 155);
        static readonly Color Border = Color.FromArgb(70, 70, 70);
        static readonly Color TimeColor = Color.FromArgb(120, 150, 200);
        static readonly Color OverColor = IconRenderer.Over;

        readonly TableLayoutPanel table;
        readonly Font headerFont, sectionFont;
        readonly float scale;
        Point anchor;

        public event EventHandler RefreshRequested;
        public DateTime LastHidden { get; private set; }

        public FlyoutForm()
        {
            using (var g = CreateGraphics()) scale = g.DpiX / 96f;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Bg;
            ForeColor = Fg;
            Font = new Font("Segoe UI", 9f);
            headerFont = new Font("Segoe UI Semibold", 11f);
            sectionFont = new Font("Segoe UI Semibold", 9f);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(S(16), S(12), S(16), S(12));
            KeyPreview = true;

            table = new TableLayoutPanel();
            table.ColumnCount = 2;
            table.AutoSize = true;
            table.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            table.Margin = Padding.Empty;
            table.MinimumSize = new Size(S(270), 0);
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            // Form.Padding only affects docked children, so offset the table by it explicitly.
            table.Location = new Point(Padding.Left, Padding.Top);
            Controls.Add(table);
        }

        int S(int px) { return (int)Math.Round(px * scale); }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x20000;   // CS_DROPSHADOW
                cp.ExStyle |= 0x80;         // WS_EX_TOOLWINDOW: keep out of Alt+Tab
                return cp;
            }
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            Hide();
            LastHidden = DateTime.Now;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Hide(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Border))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        public void ShowNear(Point cursor)
        {
            anchor = cursor;
            if (!IsHandleCreated) CreateHandle();
            PerformLayout();
            Reposition();
            Show();
            Activate();
        }

        void Reposition()
        {
            Screen screen = Screen.FromPoint(anchor);
            Rectangle wa = screen.WorkingArea, b = screen.Bounds;
            int gap = S(10);
            int x = Math.Max(wa.Left + gap, Math.Min(anchor.X - Width / 2, wa.Right - Width - gap));
            int y = wa.Top > b.Top ? wa.Top + gap : wa.Bottom - Height - gap; // taskbar on top vs bottom
            Location = new Point(x, y);
        }

        // plan is the last successful fetch (may be stale); error is the latest problem, if any.
        public void Populate(PlanUsage plan, string error)
        {
            table.SuspendLayout();
            while (table.Controls.Count > 0) table.Controls[0].Dispose();
            table.RowStyles.Clear();
            table.RowCount = 0;

            AddSpanning(MakeLabel("Claude usage", headerFont, Fg, 0));
            DateTime now = DateTime.Now;
            string status;

            if (plan == null)
            {
                Section("Plan limits");
                AddSpanning(MakeLabel(error ?? "Loading…", Font, Fg, 0));
                status = "";
            }
            else
            {
                Limit session = plan.Session;
                Section("Current session");
                LimitRows(session, now);
                if (session.IsRunning(now))
                {
                    DateTime resets = session.ResetsAt.Value;
                    Row("Resets", resets.ToString("t") + "  (in " + Fmt.Duration(resets - now) + ")");
                    double elapsed = session.ElapsedPercent(now, PlanUsage.SessionLength);
                    Row("Time elapsed", Fmt.Duration(now - (resets - PlanUsage.SessionLength)) + " of 5h  (" + Math.Floor(elapsed) + "%)");
                    Bar(elapsed / 100, TimeColor);
                }
                else
                {
                    Row("Resets", "starts with your next message");
                }

                if (plan.Weekly != null)
                {
                    Section("Weekly");
                    LimitRows(plan.Weekly, now);
                    if (plan.Weekly.ResetsAt.HasValue) Row("Resets", Fmt.Day(plan.Weekly.ResetsAt.Value));
                }

                status = "As of " + plan.Fetched.ToString("t");
                if (error != null) AddSpanning(MakeLabel("Couldn't refresh: " + error, Font, OverColor, S(8)));
            }

            var footer = MakeLabel(status, Font, Dim, S(12));
            var refresh = new LinkLabel();
            refresh.Text = "Refresh";
            refresh.AutoSize = true;
            refresh.LinkColor = IconRenderer.Accent;
            refresh.ActiveLinkColor = Fg;
            refresh.Anchor = AnchorStyles.Right;
            refresh.Margin = new Padding(0, S(12), 0, 0);
            refresh.LinkClicked += (o, e) => { if (RefreshRequested != null) RefreshRequested(this, EventArgs.Empty); };
            AddRow(footer, refresh);

            table.ResumeLayout();
            if (Visible) Reposition();
            Invalidate();
        }

        void Section(string title)
        {
            AddSpanning(MakeLabel(title, sectionFont, IconRenderer.Accent, S(12)));
        }

        void Row(string name, string value)
        {
            var v = MakeLabel(value, Font, Fg, 0);
            v.Anchor = AnchorStyles.Right;
            AddRow(MakeLabel(name, Font, Dim, 0), v);
        }

        void LimitRows(Limit limit, DateTime now)
        {
            double pct = limit.PercentAt(now);
            Row("Used", Fmt.Percent(pct) + "%");
            Bar(pct / 100, pct >= 90 ? OverColor : IconRenderer.Accent);
        }

        void Bar(double value, Color fill)
        {
            var m = new Meter();
            m.Value = value;
            m.Fill = fill;
            m.Height = S(6);
            m.Width = S(100);   // stretched across both columns by the anchor
            m.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            m.Margin = new Padding(0, S(2), 0, S(6));
            AddSpanning(m);
        }

        Label MakeLabel(string text, Font font, Color color, int topMargin)
        {
            var l = new Label();
            l.Text = text;
            l.Font = font;
            l.ForeColor = color;
            l.AutoSize = true;
            l.MaximumSize = new Size(S(360), 0);
            l.Margin = new Padding(0, topMargin + S(2), S(18), S(2));
            return l;
        }

        void AddRow(Control left, Control right)
        {
            int row = table.RowCount++;
            table.Controls.Add(left, 0, row);
            table.Controls.Add(right, 1, row);
        }

        void AddSpanning(Control c)
        {
            int row = table.RowCount++;
            table.Controls.Add(c, 0, row);
            table.SetColumnSpan(c, 2);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                headerFont.Dispose();
                sectionFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
