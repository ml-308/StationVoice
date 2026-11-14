using System;
using System.Drawing;
using System.Windows.Forms;
using StationVoice.Ui;

namespace StationVoice
{
    internal static class Program
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int processId);

        private const int AttachParentProcess = -1;

        [STAThread]
        private static int Main(string[] args)
        {
            // 「发现新版本」检查：命令行开关 + 无人值守 probe（先于 CLI 判定，避免被当成非法参数）
            UpdateKit.UpdateCheck.Configure(args);
            int? probeExit = UpdateKit.UpdateCheck.TryProbe(args);
            if (probeExit.HasValue) return probeExit.Value;

            if (Cli.CliRunner.LooksLikeCli(args))
            {
                try
                {
                    AttachConsole(AttachParentProcess);
                    Console.OutputEncoding = System.Text.Encoding.UTF8;
                }
                catch (Exception)
                {
                }

                try
                {
                    return Cli.CliRunner.Run(args);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("未处理的错误：" + ex);
                    return 1;
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            MainForm form = new MainForm();
            UpdateKit.UpdatePrompt.ScheduleAutoCheck(form);
            Application.Run(form);
            return 0;
        }
    }
}
