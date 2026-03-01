using System;
using DataCore;
using Grimoire.Configuration;

namespace Grimoire.Utilities
{
    public static class DataPerformanceConfig
    {
        public static DataBuildOptions GetBuildOptions(ConfigManager config) =>
            new DataBuildOptions
            {
                MaxParallelDataFiles = GetMaxParallelDataFiles(config),
                MaxFileWorkers = GetMaxFileWorkers(config),
                IoBufferSizeKb = GetIoBufferSizeKb(config),
                ProgressIntervalMs = GetProgressIntervalMs(config),
                UseLegacyPipeline = GetUseLegacyPipeline(config),
                SaveIndex = true
            };

        public static DataExportOptions GetExportOptions(ConfigManager config) =>
            new DataExportOptions
            {
                MaxParallelDataFiles = GetMaxParallelDataFiles(config),
                MaxFileWorkers = GetMaxFileWorkers(config),
                IoBufferSizeKb = GetIoBufferSizeKb(config),
                ProgressIntervalMs = GetProgressIntervalMs(config),
                UseLegacyPipeline = GetUseLegacyPipeline(config)
            };

        public static DataRebuildOptions GetRebuildOptions(ConfigManager config) =>
            new DataRebuildOptions
            {
                MaxParallelDataFiles = GetMaxParallelDataFiles(config),
                MaxFileWorkers = GetMaxFileWorkers(config),
                IoBufferSizeKb = GetIoBufferSizeKb(config),
                ProgressIntervalMs = GetProgressIntervalMs(config),
                UseLegacyPipeline = GetUseLegacyPipeline(config),
                ReplaceOriginalFiles = true,
                SaveIndex = true
            };

        static int GetMaxParallelDataFiles(ConfigManager config) =>
            Math.Clamp(config.Get<int>("MaxParallelDataFiles", "DataPerformance", Math.Min(8, Environment.ProcessorCount)), 1, 8);

        static int GetMaxFileWorkers(ConfigManager config) =>
            Math.Clamp(config.Get<int>("MaxFileWorkers", "DataPerformance", Environment.ProcessorCount), 1, Math.Max(1, Environment.ProcessorCount * 4));

        static int GetIoBufferSizeKb(ConfigManager config) =>
            Math.Clamp(config.Get<int>("IoBufferSizeKb", "DataPerformance", 1024), 64, 16 * 1024);

        static int GetProgressIntervalMs(ConfigManager config) =>
            Math.Clamp(config.Get<int>("ProgressIntervalMs", "DataPerformance", 100), 10, 5000);

        static bool GetUseLegacyPipeline(ConfigManager config) =>
            config.Get<bool>("UseLegacyPipeline", "DataPerformance", false);
    }
}
