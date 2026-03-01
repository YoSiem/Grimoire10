using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Grimoire.CLI
{
    internal static class ConsoleHost
    {
        private const int ATTACH_PARENT_PROCESS = -1;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        public static void EnsureConsole()
        {
            if (!AttachConsole(ATTACH_PARENT_PROCESS))
                AllocConsole();

            ResetConsoleStreams();
        }

        private static void ResetConsoleStreams()
        {
            var utf8NoBom = new UTF8Encoding(false);

            var standardOut = Console.OpenStandardOutput();
            var writer = new StreamWriter(standardOut, utf8NoBom) { AutoFlush = true };
            Console.SetOut(writer);

            var standardError = Console.OpenStandardError();
            var errorWriter = new StreamWriter(standardError, utf8NoBom) { AutoFlush = true };
            Console.SetError(errorWriter);
        }
    }
}
