using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using FormsTimer = System.Windows.Forms.Timer;

namespace ClaudeUsageTray
{
    class TrayApp : ApplicationContext
    {
        enum IconMode { SessionPercent, WeeklyPercent, SessionElapsed }

        readonly Control invoker;          // marshals poll results back to the UI thread
        readonly PlanUsageClient planClient;
        readonly NotifyIcon tray;
        readonly FlyoutForm flyout;
        readonly FormsTimer tick;
        readonly ToolStripMenuItem[] modeItems;
        readonly ToolStripMenuItem[] intervalItems;
        static readonly int[] IntervalMinutes = { 1, 2, 3, 5, 10, 15 };

        PlanUsage plan;
        string error;
        IconMode mode = IconMode.SessionPercent;
        Icon currentIcon;
        bool polling, pollPending, forcePending;

        public TrayApp()
        {
            invoker = new Control();
            IntPtr forceHandle = invoker.Handle;

            string configDir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
            if (string.IsNullOrEmpty(configDir))
                configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
            planClient = new PlanUsageClient(configDir);

            flyout = new FlyoutForm();
            flyout.RefreshRequested += (s, e) => Poll(true);

            var menu = new ContextMenuStrip();
            menu.Items.Add("Refresh now", null, (s, e) => Poll(true));
            var modeMenu = new ToolStripMenuItem("Tray icon shows");
            modeItems = new[]
            {
                ModeItem("Session limit used %", IconMode.SessionPercent),
                ModeItem("Weekly limit used %", IconMode.WeeklyPercent),
                ModeItem("Session time elapsed %", IconMode.SessionElapsed),
            };
            modeMenu.DropDownItems.AddRange(modeItems);
            menu.Items.Add(modeMenu);

            var intervalMenu = new ToolStripMenuItem("Refresh every");
            intervalItems = new ToolStripMenuItem[IntervalMinutes.Length];
            for (int i = 0; i < IntervalMinutes.Length; i++)
                intervalItems[i] = IntervalItem(IntervalMinutes[i]);
            intervalMenu.DropDownItems.AddRange(intervalItems);
            menu.Items.Add(intervalMenu);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (s, e) => Quit());
            SyncModeChecks();
            SyncIntervalChecks();

            tray = new NotifyIcon();
            tray.ContextMenuStrip = menu;
            tray.Text = "Claude usage: loading…";
            tray.MouseClick += OnTrayClick;
            SetIcon(IconRenderer.Render("…", IconRenderer.Idle));
            tray.Visible = true;

            // Every minute: redraw countdowns; the client only calls the API once its interval has passed.
            tick = new FormsTimer { Interval = 60000 };
            tick.Tick += (s, e) => Poll(false);
            tick.Start();

            Poll(true);
        }

        ToolStripMenuItem ModeItem(string text, IconMode m)
        {
            var item = new ToolStripMenuItem(text);
            item.Click += (s, e) => { mode = m; SyncModeChecks(); UpdateTray(); };
            item.Tag = m;
            return item;
        }

        void SyncModeChecks()
        {
            foreach (var item in modeItems) item.Checked = (IconMode)item.Tag == mode;
        }

        ToolStripMenuItem IntervalItem(int minutes)
        {
            var item = new ToolStripMenuItem(minutes == 1 ? "1 minute" : minutes + " minutes");
            item.Tag = minutes;
            item.Click += (s, e) =>
            {
                planClient.Interval = TimeSpan.FromMinutes(minutes);
                SyncIntervalChecks();
                Poll(false);   // a shorter interval may already be due
            };
            return item;
        }

        void SyncIntervalChecks()
        {
            foreach (var item in intervalItems)
                item.Checked = TimeSpan.FromMinutes((int)item.Tag) == planClient.Interval;
        }

        // force: call the API now instead of waiting for the refresh interval.
        void Poll(bool force)
        {
            if (polling) { pollPending = true; forcePending |= force; return; }
            polling = true;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                planClient.Poll(force);
                PlanUsage last = planClient.Last;
                string err = planClient.Error;

                invoker.BeginInvoke((MethodInvoker)delegate
                {
                    polling = false;
                    plan = last;
                    error = err;
                    UpdateTray();
                    if (flyout.Visible) flyout.Populate(plan, error);
                    if (pollPending)
                    {
                        bool f = forcePending;
                        pollPending = forcePending = false;
                        Poll(f);
                    }
                });
            });
        }

        void UpdateTray()
        {
            if (plan == null)
            {
                SetIcon(IconRenderer.Render(error == null ? "…" : "?", IconRenderer.Idle));
                SetTooltip("Claude: " + (error ?? "loading…"));
                return;
            }

            DateTime now = DateTime.Now;
            double sessionPct = plan.Session.PercentAt(now);
            double weeklyPct = plan.Weekly != null ? plan.Weekly.PercentAt(now) : -1;

            double value;
            switch (mode)
            {
                case IconMode.WeeklyPercent: value = weeklyPct; break;
                case IconMode.SessionElapsed: value = plan.Session.ElapsedPercent(now, PlanUsage.SessionLength); break;
                default: value = sessionPct; break;
            }
            Color badge = value < 0 ? IconRenderer.Idle
                : mode != IconMode.SessionElapsed && value >= 90 ? IconRenderer.Over
                : IconRenderer.Accent;
            string text = value < 0 ? "–"
                : Fmt.Percent(mode == IconMode.SessionElapsed ? Math.Floor(value) : value);
            SetIcon(IconRenderer.Render(text, badge));

            string tip = "Session " + Fmt.Percent(sessionPct) + "%";
            if (plan.Session.IsRunning(now)) tip += " · resets " + plan.Session.ResetsAt.Value.ToString("t");
            if (weeklyPct >= 0)
            {
                tip += "\nWeekly " + Fmt.Percent(weeklyPct) + "%";
                if (plan.Weekly.ResetsAt.HasValue) tip += " · resets " + Fmt.Day(plan.Weekly.ResetsAt.Value);
            }
            SetTooltip(tip);
        }

        void SetTooltip(string text)
        {
            // NotifyIcon.Text throws above 63 characters on .NET Framework.
            tray.Text = text.Length > 63 ? text.Substring(0, 62) + "…" : text;
        }

        void SetIcon(Icon icon)
        {
            var old = currentIcon;
            currentIcon = icon;
            tray.Icon = icon;
            IconRenderer.Free(old);
        }

        void OnTrayClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            // Clicking the icon while the flyout is open first deactivates (hides) it;
            // don't immediately reopen it on that same click.
            if (flyout.Visible || (DateTime.Now - flyout.LastHidden).TotalMilliseconds < 300)
            {
                flyout.Hide();
                return;
            }
            flyout.Populate(plan, error);
            flyout.ShowNear(Cursor.Position);
        }

        void Quit()
        {
            tick.Stop();
            tray.Visible = false;
            tray.Dispose();
            IconRenderer.Free(currentIcon);
            flyout.Dispose();
            ExitThread();
        }
    }
}
