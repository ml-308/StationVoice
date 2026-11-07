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
            Application.Run(new MainForm());
            return 0;
        }
    }
}
