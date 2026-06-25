using System;
using System.IO;
using System.Threading;
using System.Timers;
using CSharpConvertToProto.Models;

namespace CSharpConvertToProto.Service
{
    // Simple file watcher service that watches a folder for *.cs changes and
    // regenerates a single proto file into the configured output folder.
    public class FileWatcherService : IDisposable
    {
        private readonly string _sourceFolder;
        private readonly string _outputFolder;
        private readonly string _nameSpace;
        private FileSystemWatcher _watcher;
        private readonly System.Timers.Timer _debounceTimer;
        private readonly object _lock = new object();
        private bool _disposed = false;

        public FileWatcherService(string sourceFolder, string outputFolder, string nameSpace)
        {
            _sourceFolder = sourceFolder ?? throw new ArgumentNullException(nameof(sourceFolder));
            _outputFolder = outputFolder ?? throw new ArgumentNullException(nameof(outputFolder));
            _nameSpace = string.IsNullOrWhiteSpace(nameSpace) ? "Generated" : nameSpace;

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
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
            };

            _watcher.Changed += OnFileEvent;
            _watcher.Created += OnFileEvent;
            _watcher.Renamed += OnFileEvent;
            _watcher.Deleted += OnFileEvent;

            _watcher.EnableRaisingEvents = true;

            // Initial generation
            TriggerGeneration();
        }

        private void OnFileEvent(object sender, FileSystemEventArgs e)
        {
            // Debounce rapid events
            lock (_lock)
            {
                _debounceTimer.Stop();
                _debounceTimer.Start();
            }
        }

        private void DebounceTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            TriggerGeneration();
        }

        private void TriggerGeneration()
        {
            try
            {
                var parser = new CSharpClassParser();
                var classes = parser.ParseFolder(_sourceFolder);

                var generator = new ProtoGenerator();
                var proto = generator.GenerateProto(classes.Values, _nameSpace);

                if (!Directory.Exists(_outputFolder))
                {
                    Directory.CreateDirectory(_outputFolder);
                }

                var outPath = Path.Combine(_outputFolder, "GeneratedProtoModels.proto");
                File.WriteAllText(outPath, proto);
            }
            catch (Exception)
            {
                // swallow exceptions: file IO or parsing should not crash the watcher
            }
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
