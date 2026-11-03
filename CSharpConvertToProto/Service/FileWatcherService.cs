using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Timers;

namespace CSharpConvertToProto.Service
{
    // Simple file watcher service that watches a folder for *.cs changes and
    // regenerates a single proto file into the configured output folder.
    public class FileWatcherService : IDisposable
    {
        private readonly string _sourceFolder;
        private readonly string _outputFolder;
        private readonly string _nameSpace;
        private readonly string _replaceClassTextFrom;
        private readonly string _replaceClassTextTo;
        private const string OutputFileName = "GeneratedProtoModels.proto";
        private const string StampFileName = ".proto.sync.stamp";
        private FileSystemWatcher _watcher;
        private readonly System.Timers.Timer _debounceTimer;
        private readonly object _lock = new object();
        private int _isGenerating;
        private bool _disposed = false;

        public FileWatcherService(string sourceFolder, string outputFolder, string nameSpace, string replaceClassTextFrom, string replaceClassTextTo)
        {
            _sourceFolder = sourceFolder ?? throw new ArgumentNullException(nameof(sourceFolder));
            _outputFolder = outputFolder ?? throw new ArgumentNullException(nameof(outputFolder));
            _nameSpace = string.IsNullOrWhiteSpace(nameSpace) ? "Generated" : nameSpace;
            _replaceClassTextFrom = string.IsNullOrWhiteSpace(replaceClassTextFrom) ? "infra" : replaceClassTextFrom;
            _replaceClassTextTo = string.IsNullOrWhiteSpace(replaceClassTextTo) ? "grpc" : replaceClassTextTo;

            _debounceTimer = new System.Timers.Timer(1000);
            _debounceTimer.AutoReset = false;
            _debounceTimer.Elapsed += DebounceTimer_Elapsed;
        }

        public void Start()
        {
            if (!Directory.Exists(_sourceFolder))
            {
                try
                {
                    Directory.CreateDirectory(_sourceFolder);
                }
                catch
                {
                    return;
                }
            }

            _watcher = new FileSystemWatcher(_sourceFolder, "*.cs")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size
            };

            _watcher.Changed += OnFileEvent;
            _watcher.Created += OnFileEvent;
            _watcher.Renamed += OnFileEvent;
            _watcher.Deleted += OnFileEvent;
            _watcher.Error += OnWatcherError;

            _watcher.EnableRaisingEvents = true;

            // Initial sync at startup: generate only if source state changed
            TriggerGenerationIfNeeded();
        }

        private void OnFileEvent(object sender, FileSystemEventArgs e)
        {
            if (e == null || !IsFileIncluded(e.FullPath))
            {
                return;
            }

            // Debounce rapid events
            lock (_lock)
            {
                _debounceTimer.Stop();
                _debounceTimer.Start();
            }
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            try
            {
                if (_watcher == null) return;

                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
                Start();
            }
            catch
            {
                // swallow
            }
        }

        private void DebounceTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            TriggerGenerationIfNeeded();
        }

        private void TriggerGenerationIfNeeded()
        {
            if (Interlocked.Exchange(ref _isGenerating, 1) == 1)
            {
                return;
            }

            try
            {
                var currentFingerprint = BuildSourceFingerprint();
                if (!ShouldRegenerate(currentFingerprint))
                {
                    return;
                }

                var parser = new CSharpClassParser();
                var classes = parser.ParseFolder(_sourceFolder);

                var generator = new ProtoGenerator();
                var proto = generator.GenerateProto(classes.Values, _nameSpace, _replaceClassTextFrom, _replaceClassTextTo);

                if (!Directory.Exists(_outputFolder))
                {
                    Directory.CreateDirectory(_outputFolder);
                }

                var outPath = Path.Combine(_outputFolder, OutputFileName);
                File.WriteAllText(outPath, proto);

                var stampPath = Path.Combine(_outputFolder, StampFileName);
                File.WriteAllText(stampPath, currentFingerprint);
            }
            catch (Exception)
            {
                // swallow exceptions: file IO or parsing should not crash the watcher
            }
            finally
            {
                Interlocked.Exchange(ref _isGenerating, 0);
            }
        }

        private bool ShouldRegenerate(string currentFingerprint)
        {
            var outPath = Path.Combine(_outputFolder, OutputFileName);
            if (!File.Exists(outPath))
            {
                return true;
            }

            var stampPath = Path.Combine(_outputFolder, StampFileName);
            if (!File.Exists(stampPath))
            {
                return true;
            }

            try
            {
                var storedFingerprint = File.ReadAllText(stampPath);
                return !string.Equals(storedFingerprint, currentFingerprint, StringComparison.Ordinal);
            }
            catch
            {
                return true;
            }
        }

        private string BuildSourceFingerprint()
        {
            if (!Directory.Exists(_sourceFolder))
            {
                return string.Empty;
            }

            var entries = new List<string>();
            entries.Add(_nameSpace ?? string.Empty);
            entries.Add(_replaceClassTextFrom ?? string.Empty);
            entries.Add(_replaceClassTextTo ?? string.Empty);

            foreach (var file in Directory.GetFiles(_sourceFolder, "*.cs", SearchOption.AllDirectories)
                .Where(IsFileIncluded)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                var relative = file.Substring(_sourceFolder.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                entries.Add(relative + "|" + ComputeFileSignature(file));
            }

            var payload = string.Join("\n", entries);
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(payload);
                var hash = sha.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        private string ComputeFileSignature(string filePath)
        {
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var sha = SHA256.Create())
                {
                    var hash = sha.ComputeHash(stream);
                    return Convert.ToBase64String(hash);
                }
            }
            catch
            {
                try
                {
                    var info = new FileInfo(filePath);
                    return info.Length + "|" + info.LastWriteTimeUtc.Ticks;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        private bool IsFileIncluded(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }

            var normalized = filePath.Replace('/', '\\');
            return normalized.IndexOf("\\bin\\", StringComparison.OrdinalIgnoreCase) < 0
                   && normalized.IndexOf("\\obj\\", StringComparison.OrdinalIgnoreCase) < 0
                   && normalized.IndexOf("\\.vs\\", StringComparison.OrdinalIgnoreCase) < 0
                   && normalized.IndexOf("\\packages\\", StringComparison.OrdinalIgnoreCase) < 0;
        }

        public void Stop()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Changed -= OnFileEvent;
                _watcher.Created -= OnFileEvent;
                _watcher.Renamed -= OnFileEvent;
                _watcher.Deleted -= OnFileEvent;
                _watcher.Error -= OnWatcherError;
                _watcher.Dispose();
                _watcher = null;
            }

            _debounceTimer?.Stop();
        }

        public void Dispose()
        {
            if (_disposed) return;
            Stop();
            _debounceTimer?.Dispose();
            _disposed = true;
        }
    }
}
