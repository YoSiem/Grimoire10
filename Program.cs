using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

using Grimoire.CLI;
using Grimoire.GUI;
using Grimoire.Utilities;

namespace Grimoire
{
    static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);

            if (CliRunner.ShouldHandle(args))
            {
                ConsoleHost.EnsureConsole();
                return CliRunner.RunAsync(args).GetAwaiter().GetResult();
            }

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ModernTheme.Initialize();

            Application.Run(new Main());
            return 0;
        }
    }
}
