using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

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
        static void Main()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ModernTheme.Initialize();

            Application.Run(new Main());
        }
    }
}
