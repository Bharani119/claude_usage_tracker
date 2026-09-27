using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ClaudeUsageTray
{
    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main()
        {
            bool createdNew;
            using (var mutex = new Mutex(true, "Local\\ClaudeUsageTray", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("Claude Usage Tray is already running (check the tray overflow area).",
                        "Claude Usage Tray", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Crisp text on high-DPI screens instead of bitmap-stretched UI.
                try { SetProcessDPIAware(); } catch (EntryPointNotFoundException) { }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApp());
            }
        }
    }
}
