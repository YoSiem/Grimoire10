using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DataCore;
using Grimoire.Configuration;
using Grimoire.Utilities;
using Serilog;
using Serilog.Events;

namespace Grimoire.CLI
{
    internal static class CliRunner
    {
        private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;

        public static bool ShouldHandle(string[] args)
        {
            if (args == null || args.Length == 0)
                return false;

            string first = args[0]?.Trim() ?? string.Empty;

            if (IsHelp(first) || IsCommand(first))
                return true;

            if (!first.Equals("cli", StringComparison.OrdinalIgnoreCase))
                return false;

            if (args.Length == 1)
                return true;

            return IsHelp(args[1]) || IsCommand(args[1]);
        }

        public static async Task<int> RunAsync(string[] args)
        {
            configureLogger();

            try
            {
                string[] normalized = NormalizeArgs(args);
                if (normalized.Length == 0 || IsHelp(normalized[0]))
                {
                    PrintHelp();
                    return 0;
                }

                string command = normalized[0].ToLowerInvariant();
                Dictionary<string, string> options = ParseOptions(normalized.Skip(1).ToArray());

                using var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                    Console.WriteLine("Przerwanie... czekam na zatrzymanie zadania.");
                };

                return command switch
                {
                    "dump" => await runDumpAsync(options, cts.Token),
                    "build" => await runBuildAsync(options, cts.Token),
                    "help" => 0,
                    _ => fail($"Nieznana komenda: {command}")
                };
            }
            catch (OperationCanceledException)
            {
                return fail("Operacja anulowana.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "CLI execution failed");
                return fail(ex.Message);
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static async Task<int> runDumpAsync(IReadOnlyDictionary<string, string> options, CancellationToken ct)
        {
            string source = RequiredOption(options, "source", "s");
            string output = RequiredOption(options, "output", "o");
            string configPath = ResolveConfigPath(options);

            ConfigManager config = LoadConfig(configPath);
            DataExportOptions exportOptions = DataPerformanceConfig.GetExportOptions(config);
            ApplyPerformanceOverrides(options, exportOptions);

            Core core = CreateCore(config, options);
            using var progress = new ProgressPrinter(core);

            string dataIndexPath = ResolveDataIndexPath(source);
            Directory.CreateDirectory(output);

            Console.WriteLine($"[dump] source: {dataIndexPath}");
            Console.WriteLine($"[dump] output: {Path.GetFullPath(output)}");

            Stopwatch sw = Stopwatch.StartNew();

            ct.ThrowIfCancellationRequested();
            await Task.Run(() => core.Load(dataIndexPath), ct);
            Console.WriteLine($"[dump] loaded entries: {core.RowCount}");

            await core.ExportAllEntriesAsync(output, exportOptions, ct);

            sw.Stop();
            Console.WriteLine($"[dump] done in {StringExt.MilisecondsToString(sw.ElapsedMilliseconds)}");
            return 0;
        }

        private static async Task<int> runBuildAsync(IReadOnlyDictionary<string, string> options, CancellationToken ct)
        {
            string source = RequiredOption(options, "source", "s");
            string output = RequiredOption(options, "output", "o");
            string configPath = ResolveConfigPath(options);

            ConfigManager config = LoadConfig(configPath);
            DataBuildOptions buildOptions = DataPerformanceConfig.GetBuildOptions(config);
            ApplyPerformanceOverrides(options, buildOptions);

            Core core = CreateCore(config, options);
            using var progress = new ProgressPrinter(core);

            string dumpDirectory = ResolveDumpDirectory(source);
            Directory.CreateDirectory(output);

            Console.WriteLine($"[build] source: {dumpDirectory}");
            Console.WriteLine($"[build] output: {Path.GetFullPath(output)}");

            if (!await Paths.VerifyDump(dumpDirectory, interactive: false, autoOverwrite: true))
                return fail("Dump directory verification failed.");

            Stopwatch sw = Stopwatch.StartNew();
            await core.BuildDataFilesAsync(dumpDirectory, output, buildOptions, ct);
            sw.Stop();

            Console.WriteLine($"[build] done in {StringExt.MilisecondsToString(sw.ElapsedMilliseconds)}");
            return 0;
        }

        private static string ResolveConfigPath(IReadOnlyDictionary<string, string> options)
        {
            string path = GetOption(options, "config", "c") ?? Path.Combine(AppContext.BaseDirectory, "Config.json");
            path = Path.GetFullPath(path);

            if (!File.Exists(path))
                throw new FileNotFoundException($"Config file not found: {path}");

            return path;
        }

        private static ConfigManager LoadConfig(string configPath)
        {
            string configDirectory = Path.GetDirectoryName(configPath) ?? AppContext.BaseDirectory;
            string configFileName = Path.GetFileName(configPath);

            ConfigManager config = new ConfigManager(configDirectory, configFileName);
            if (config.Count == 0)
                throw new InvalidOperationException($"No configuration options loaded from: {configPath}");

            return config;
        }

        private static Core CreateCore(ConfigManager config, IReadOnlyDictionary<string, string> options)
        {
            int codePage = GetIntOption(options, "codepage") ?? config.Get<int>("Codepage", "Grim", 1252);
            bool backup = GetBoolOption(options, "backup") ?? config.Get<bool>("Backup", "Data", true);
            bool useModifiedXor = GetBoolOption(options, "use-modified-xor") ?? config.Get<bool>("UseModifiedXOR", "Data", false);

            Encoding encoding;
            try
            {
                encoding = Encoding.GetEncoding(codePage);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Invalid codepage: {codePage}", ex);
            }

            if (!useModifiedXor)
                return new Core(backup, encoding);

            byte[] key = config.GetByteArray("ModifiedXORKey");
            if (key == null || key.Length != 256)
                throw new InvalidOperationException("ModifiedXOR is enabled but key is missing or invalid length in Config.json.");

            return new Core(backup, encoding, key);
        }

        private static void ApplyPerformanceOverrides(IReadOnlyDictionary<string, string> options, DataBuildOptions buildOptions)
        {
            int? maxParallel = GetIntOption(options, "max-parallel-data-files");
            if (maxParallel.HasValue)
                buildOptions.MaxParallelDataFiles = maxParallel.Value;

            int? maxWorkers = GetIntOption(options, "max-file-workers");
            if (maxWorkers.HasValue)
                buildOptions.MaxFileWorkers = maxWorkers.Value;

            int? ioBufferKb = GetIntOption(options, "io-buffer-kb");
            if (ioBufferKb.HasValue)
                buildOptions.IoBufferSizeKb = ioBufferKb.Value;

            int? progressInterval = GetIntOption(options, "progress-interval-ms");
            if (progressInterval.HasValue)
                buildOptions.ProgressIntervalMs = progressInterval.Value;

            bool? useLegacy = GetBoolOption(options, "use-legacy-pipeline");
            if (useLegacy.HasValue)
                buildOptions.UseLegacyPipeline = useLegacy.Value;
        }

        private static void ApplyPerformanceOverrides(IReadOnlyDictionary<string, string> options, DataExportOptions exportOptions)
        {
            int? maxParallel = GetIntOption(options, "max-parallel-data-files");
            if (maxParallel.HasValue)
                exportOptions.MaxParallelDataFiles = maxParallel.Value;

            int? maxWorkers = GetIntOption(options, "max-file-workers");
            if (maxWorkers.HasValue)
                exportOptions.MaxFileWorkers = maxWorkers.Value;

            int? ioBufferKb = GetIntOption(options, "io-buffer-kb");
            if (ioBufferKb.HasValue)
                exportOptions.IoBufferSizeKb = ioBufferKb.Value;

            int? progressInterval = GetIntOption(options, "progress-interval-ms");
            if (progressInterval.HasValue)
                exportOptions.ProgressIntervalMs = progressInterval.Value;

            bool? useLegacy = GetBoolOption(options, "use-legacy-pipeline");
            if (useLegacy.HasValue)
                exportOptions.UseLegacyPipeline = useLegacy.Value;
        }

        private static string ResolveDataIndexPath(string source)
        {
            string fullSourcePath = Path.GetFullPath(source);

            if (Directory.Exists(fullSourcePath))
            {
                string indexPath = Path.Combine(fullSourcePath, "data.000");
                if (!File.Exists(indexPath))
                    throw new FileNotFoundException($"Cannot find data.000 in directory: {fullSourcePath}");

                return indexPath;
            }

            if (!File.Exists(fullSourcePath))
                throw new FileNotFoundException($"Source path not found: {fullSourcePath}");

            return fullSourcePath;
        }

        private static string ResolveDumpDirectory(string source)
        {
            string fullSourcePath = Path.GetFullPath(source);

            if (!Directory.Exists(fullSourcePath))
                throw new DirectoryNotFoundException($"Dump directory not found: {fullSourcePath}");

            return fullSourcePath;
        }

        private static string RequiredOption(IReadOnlyDictionary<string, string> options, params string[] aliases)
        {
            string value = GetOption(options, aliases);

            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"Missing required option: --{aliases[0]}");

            return value;
        }

        private static string GetOption(IReadOnlyDictionary<string, string> options, params string[] aliases)
        {
            foreach (string alias in aliases)
            {
                if (options.TryGetValue(alias, out string value))
                    return value;
            }

            return null;
        }

        private static int? GetIntOption(IReadOnlyDictionary<string, string> options, params string[] aliases)
        {
            string value = GetOption(options, aliases);
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (!int.TryParse(value, out int parsed))
                throw new ArgumentException($"Option --{aliases[0]} must be an integer. Got: {value}");

            return parsed;
        }

        private static bool? GetBoolOption(IReadOnlyDictionary<string, string> options, params string[] aliases)
        {
            string value = GetOption(options, aliases);
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (bool.TryParse(value, out bool parsedBool))
                return parsedBool;

            if (value == "1" || value.Equals("yes", StringComparison.OrdinalIgnoreCase))
                return true;

            if (value == "0" || value.Equals("no", StringComparison.OrdinalIgnoreCase))
                return false;

            throw new ArgumentException($"Option --{aliases[0]} must be true/false. Got: {value}");
        }

        private static string[] NormalizeArgs(string[] args)
        {
            if (args == null || args.Length == 0)
                return Array.Empty<string>();

            if (args[0].Equals("cli", StringComparison.OrdinalIgnoreCase))
                return args.Skip(1).ToArray();

            return args;
        }

        private static bool IsHelp(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            return token.Equals("help", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("-h", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCommand(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            return token.Equals("dump", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("build", StringComparison.OrdinalIgnoreCase) ||
                   token.Equals("help", StringComparison.OrdinalIgnoreCase);
        }

        private static Dictionary<string, string> ParseOptions(string[] args)
        {
            Dictionary<string, string> options = new(KeyComparer);

            for (int i = 0; i < args.Length; i++)
            {
                string token = args[i];

                if (!token.StartsWith("-", StringComparison.Ordinal))
                    throw new ArgumentException($"Invalid option token: {token}");

                string key = token.StartsWith("--", StringComparison.Ordinal)
                    ? token.Substring(2)
                    : token.Substring(1);

                if (string.IsNullOrWhiteSpace(key))
                    throw new ArgumentException($"Invalid option token: {token}");

                string value = "true";

                if (i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    value = args[i + 1];
                    i++;
                }

                options[key] = value;
            }

            return options;
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Grimoire CLI");
            Console.WriteLine();
            Console.WriteLine("Uzycie:");
            Console.WriteLine("  Grimoire.exe cli dump --source <client|data.000> --output <dump-folder>");
            Console.WriteLine("  Grimoire.exe cli build --source <dump-folder> --output <client-folder>");
            Console.WriteLine();
            Console.WriteLine("Opcje wspolne:");
            Console.WriteLine("  --config <path>                 Sciezka do Config.json (domyslnie: ./Config.json)");
            Console.WriteLine("  --codepage <int>                Nadpisuje Grim.Codepage");
            Console.WriteLine("  --backup <true|false>           Nadpisuje Data.Backup");
            Console.WriteLine("  --use-modified-xor <true|false> Nadpisuje Data.UseModifiedXOR");
            Console.WriteLine();
            Console.WriteLine("Opcje wydajnosci:");
            Console.WriteLine("  --max-parallel-data-files <int>");
            Console.WriteLine("  --max-file-workers <int>");
            Console.WriteLine("  --io-buffer-kb <int>");
            Console.WriteLine("  --progress-interval-ms <int>");
            Console.WriteLine("  --use-legacy-pipeline <true|false>");
            Console.WriteLine();
            Console.WriteLine("Przyklady (PowerShell):");
            Console.WriteLine("  .\\Grimoire.exe cli dump --source \"C:\\GameClient\" --output \"D:\\Dump\"");
            Console.WriteLine("  .\\Grimoire.exe cli build --source \"D:\\Dump\" --output \"E:\\NewClient\"");
        }

        private static int fail(string message)
        {
            Console.Error.WriteLine($"Error: {message}");
            Console.Error.WriteLine("Uzyj --help, aby zobaczyc skladnie.");
            return 1;
        }

        private static void configureLogger()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(LogEventLevel.Information)
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(".\\Logs\\Grimoire-CLI-.txt", rollingInterval: RollingInterval.Day, outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        }

        private sealed class ProgressPrinter : IDisposable
        {
            private readonly Core core;
            private readonly object sync = new();
            private long maximum = 1;
            private int lastPercent = -1;

            public ProgressPrinter(Core core)
            {
                this.core = core;

                core.CurrentMaxDetermined += Core_CurrentMaxDetermined;
                core.CurrentProgressChanged += Core_CurrentProgressChanged;
                core.CurrentProgressReset += Core_CurrentProgressReset;
            }

            private void Core_CurrentMaxDetermined(object sender, CurrentMaxArgs e)
            {
                Interlocked.Exchange(ref maximum, Math.Max(1, e.Maximum));
                Interlocked.Exchange(ref lastPercent, -1);
            }

            private void Core_CurrentProgressChanged(object sender, CurrentChangedArgs e)
            {
                long max = Math.Max(1, Interlocked.Read(ref maximum));
                int percent = (int)Math.Clamp(e.Value * 100 / max, 0, 100);

                int previous = Interlocked.Exchange(ref lastPercent, percent);
                if (previous == percent)
                    return;

                lock (sync)
                {
                    Console.Write($"\rPostep: {percent,3}%");
                }
            }

            private void Core_CurrentProgressReset(object sender, CurrentResetArgs e)
            {
                lock (sync)
                {
                    if (Interlocked.CompareExchange(ref lastPercent, 0, 0) >= 0)
                        Console.WriteLine("\rPostep: 100%");

                    Interlocked.Exchange(ref lastPercent, -1);
                }
            }

            public void Dispose()
            {
                core.CurrentMaxDetermined -= Core_CurrentMaxDetermined;
                core.CurrentProgressChanged -= Core_CurrentProgressChanged;
                core.CurrentProgressReset -= Core_CurrentProgressReset;
            }
        }
    }
}
