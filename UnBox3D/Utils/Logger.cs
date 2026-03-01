using System;
using System.IO;

namespace UnBox3D.Utils
{
    // Logger interface for logging functionality
    public interface ILogger
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message);
    }

    public class Logger : ILogger
    {
        #region Fields

        private readonly IFileSystem _fileSystem;
        private readonly object _lock = new object();
        private readonly string _logDirectory;
        private readonly string _logFilePath;
        private readonly long _maxFileSizeInBytes;
        private readonly string _logFileName;
        private readonly int _maxArchiveFiles;

        #endregion

        #region Constructors

        public Logger(
            IFileSystem fileSystem,
            string? logDirectory = null,
            string logFileName = "UnBox3D.log",
            long maxFileSizeInBytes = 5 * 1024 * 1024,
            int maxArchiveFiles = 5)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logDirectory = logDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "UnBox3D", "Log");
            _logFileName = logFileName;
            _maxFileSizeInBytes = maxFileSizeInBytes;
            _maxArchiveFiles = maxArchiveFiles;
            _logFilePath = Path.Combine(_logDirectory, _logFileName);

            if (!_fileSystem.DoesDirectoryExists(_logDirectory))
                _fileSystem.CreateDirectory(_logDirectory);
        }

        #endregion

        #region Enums

        public enum LogLevel
        {
            Info,
            Debug,
            Warn,
            Error,
            Fatal
        }

        #endregion

        #region Public Logging Methods

        /// <summary>
        /// Logs a message with the specified log level.
        /// </summary>
        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            lock (_lock)
            {
                if (_fileSystem.DoesFileExists(_logFilePath) &&
                    _fileSystem.GetFileSize(_logFilePath) > _maxFileSizeInBytes)
                {
                    RotateLogs();
                }

                string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} " +
                                  $"[{level.ToString().ToUpper()}] {message}";
                _fileSystem.WriteToFile(_logFilePath, logEntry);
            }
        }

        public void Info(string message) => Log(message, LogLevel.Info);
        public void Debug(string message) => Log(message, LogLevel.Debug);
        public void Warn(string message) => Log(message, LogLevel.Warn);
        public void Error(string message) => Log(message, LogLevel.Error);
        public void Fatal(string message) => Log(message, LogLevel.Fatal);

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Rotates the log files when the current log file exceeds the maximum size.
        /// </summary>
        private void RotateLogs()
        {
            for (int i = _maxArchiveFiles - 1; i >= 0; i--)
            {
                string oldLogFilePath = Path.Combine(_logDirectory, $"{_logFileName}.{i}");
                string newLogFilePath = Path.Combine(_logDirectory, $"{_logFileName}.{i + 1}");

                if (_fileSystem.DoesFileExists(newLogFilePath))
                    _fileSystem.DeleteFile(newLogFilePath);

                if (_fileSystem.DoesFileExists(oldLogFilePath))
                    _fileSystem.MoveFile(oldLogFilePath, newLogFilePath);
            }

            _fileSystem.MoveFile(_logFilePath, Path.Combine(_logDirectory, $"{_logFileName}.0"));
        }

        #endregion
    }
}
