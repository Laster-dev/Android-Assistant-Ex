using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace 搞机助手Ex
{
    /// <summary>
    /// Represents progress information for the initialization process
    /// </summary>
    public class InitializationProgress
    {
        public string StatusMessage { get; set; }
        public int PercentComplete { get; set; }
        public InitializationStage Stage { get; set; }
    }

    /// <summary>
    /// Defines the various stages of initialization
    /// </summary>
    public enum InitializationStage
    {
        Starting,
        KillingProcesses,
        PreparingResources,
        ExtractingTools,
        CleaningUp,
        Completed
    }

    /// <summary>
    /// Service responsible for handling all background initialization tasks
    /// </summary>
    public class InitializationService
    {
        private readonly string _toolsPath;
        private readonly string _zipPath;
        private const int BUFFER_SIZE = 81920; // 80KB buffer for file operations

        public InitializationService()
        {
            _toolsPath = Path.Combine(Directory.GetCurrentDirectory(), "Tools");
            _zipPath = Path.Combine(Directory.GetCurrentDirectory(), "Tools.zip");
        }

        /// <summary>
        /// Performs the complete initialization process asynchronously
        /// </summary>
        public async Task InitializeAsync(IProgress<InitializationProgress> progress, CancellationToken token)
        {
            // Report initial status
            ReportProgress(progress, "正在初始化...", 0, InitializationStage.Starting);

            // 1. Kill related processes
            await KillProcessesAsync(progress, token);

            // 2. Extract tools if needed
            if (!Directory.Exists(_toolsPath))
            {
                // 2.1 Prepare resources
                await PrepareResourcesAsync(progress, token);

                // 2.2 Extract tools
                await ExtractToolsAsync(progress, token);

                // 2.3 Clean up temporary files
                await CleanupAsync(progress, token);
            }

            // 3. Report completion
            ReportProgress(progress, "初始化完成", 100, InitializationStage.Completed);

            // Add a small delay for UI transition smoothness
            await Task.Delay(300, token);
        }

        /// <summary>
        /// Kills processes that might interfere with the application
        /// </summary>
        private async Task KillProcessesAsync(IProgress<InitializationProgress> progress, CancellationToken token)
        {
            ReportProgress(progress, "正在终止相关进程...", 10, InitializationStage.KillingProcesses);

            string[] processesToKill = { "adb", "fastboot" };

            await Task.Run(async () =>
            {
                foreach (var processName in processesToKill)
                {
                    token.ThrowIfCancellationRequested();

                    try
                    {
                        Process[] processes = Process.GetProcessesByName(processName);
                        foreach (var process in processes)
                        {
                            if (!process.HasExited)
                            {
                                process.Kill();
                                // Use shorter timeout and don't block
                                await Task.Run(() => process.WaitForExit(500));
                            }
                        }
                    }
                    catch
                    {
                        // Silent handling - just continue
                    }
                }
            }, token);
        }

        /// <summary>
        /// Prepares resources for extraction
        /// </summary>
        private async Task PrepareResourcesAsync(IProgress<InitializationProgress> progress, CancellationToken token)
        {
            ReportProgress(progress, "正在准备资源文件...", 20, InitializationStage.PreparingResources);

            await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();

                // Create temporary directory if it doesn't exist
                Directory.CreateDirectory(Path.GetDirectoryName(_zipPath));

                // Remove existing zip file if it exists
                if (File.Exists(_zipPath))
                {
                    File.Delete(_zipPath);
                }

                // Write resource to disk
                using (FileStream fs = new FileStream(_zipPath, FileMode.Create, FileAccess.Write, FileShare.None, BUFFER_SIZE, true))
                {
                    fs.Write(Resource1.Tools, 0, Resource1.Tools.Length);
                }

            }, token);
        }

        /// <summary>
        /// Extracts tools from the zip file
        /// </summary>
        private async Task ExtractToolsAsync(IProgress<InitializationProgress> progress, CancellationToken token)
        {
            ReportProgress(progress, "正在解压工具...", 30, InitializationStage.ExtractingTools);

            await Task.Run(async () =>
            {
                token.ThrowIfCancellationRequested();

                // Create target directory
                Directory.CreateDirectory(_toolsPath);

                // Use a memory-efficient extraction approach
                using (ZipArchive archive = ZipFile.OpenRead(_zipPath))
                {
                    int totalEntries = archive.Entries.Count;
                    int processedEntries = 0;

                    // Process entries in batches to improve performance
                    // while maintaining responsiveness
                    var entryBatches = archive.Entries
                        .Select((entry, index) => new { Entry = entry, Index = index })
                        .GroupBy(x => x.Index / 10)
                        .Select(g => g.Select(x => x.Entry).ToArray())
                        .ToArray();

                    foreach (var batch in entryBatches)
                    {
                        token.ThrowIfCancellationRequested();

                        // Process each batch in parallel for better performance
                        await Task.WhenAll(batch.Select(async entry =>
                        {
                            await ExtractEntryAsync(entry, token);

                            Interlocked.Increment(ref processedEntries);

                            // Calculate progress
                            int percentComplete = (int)(30 + (processedEntries / (double)totalEntries * 60));
                            ReportProgress(progress, $"正在解压工具... {processedEntries}/{totalEntries}",
                                percentComplete, InitializationStage.ExtractingTools);
                        }));

                        // Yield to allow UI thread to process messages
                        await Task.Delay(1, token);
                    }
                }
            });
        }

        /// <summary>
        /// Extracts a single ZIP entry
        /// </summary>
        private async Task ExtractEntryAsync(ZipArchiveEntry entry, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            // Skip directory entries
            if (string.IsNullOrEmpty(entry.Name))
                return;

            // Construct destination path
            string destinationPath = Path.Combine(_toolsPath, entry.FullName);
            string directory = Path.GetDirectoryName(destinationPath);

            // Create directory if it doesn't exist
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            try
            {
                // Extract the file using streams for better performance
                using (var source = entry.Open())
                using (var target = new FileStream(destinationPath, FileMode.Create, FileAccess.Write,
                                                 FileShare.None, BUFFER_SIZE, useAsync: true))
                {
                    await source.CopyToAsync(target, BUFFER_SIZE, token);
                }
            }
            catch
            {
                // Silently skip files that can't be extracted
            }
        }

        /// <summary>
        /// Cleans up temporary files
        /// </summary>
        private async Task CleanupAsync(IProgress<InitializationProgress> progress, CancellationToken token)
        {
            ReportProgress(progress, "正在清理缓存...", 95, InitializationStage.CleaningUp);

            await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();

                if (File.Exists(_zipPath))
                {
                    try
                    {
                        File.Delete(_zipPath);
                    }
                    catch
                    {
                        // Silently ignore cleanup errors
                    }
                }
            }, token);

            ReportProgress(progress, "清理完成", 98, InitializationStage.CleaningUp);
        }

        /// <summary>
        /// Helper method to report progress
        /// </summary>
        private void ReportProgress(IProgress<InitializationProgress> progress, string message,
                                   int percentComplete, InitializationStage stage)
        {
            if (progress != null)
            {
                progress.Report(new InitializationProgress
                {
                    StatusMessage = message,
                    PercentComplete = percentComplete,
                    Stage = stage
                });
            }
        }
    }
}