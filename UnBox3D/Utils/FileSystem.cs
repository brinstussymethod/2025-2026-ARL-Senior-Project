using System.IO;

namespace UnBox3D.Utils
{
    public interface IFileSystem
    {
        // File operations
        bool DoesFileExists(string filePath);
        void WriteToFile(string filePath, string content);
        string ReadFile(string filePath);
        void AppendToFile(string filePath, string content);
        void DeleteFile(string filePath);
        long GetFileSize(string filePath);
        void MoveFile(string sourceFilePath, string destinationFilePath);
        Task WriteAllBytesAsync(string filePath, byte[] bytes);
        Stream CreateFile(string filePath);

        // Directory operations
        bool DoesDirectoryExists(string directoryPath);
        void CreateDirectory(string directoryPath);
        void DeleteDirectory(string directoryPath, bool recursive);
        IEnumerable<string> GetFilesInDirectory(string directoryPath);
        IEnumerable<string> GetDirectoriesInDirectory(string directoryPath);

        // Path operations
        string CombinePaths(params string[] paths);
    }

    public class FileSystem : IFileSystem
    {
        #region File Operations

        public bool DoesFileExists(string filePath) => File.Exists(filePath);

        public void WriteToFile(string filePath, string content)
        {
            File.WriteAllText(filePath, content);
        }

        public string ReadFile(string filePath)
        {
            if (!DoesFileExists(filePath))
                throw new FileNotFoundException("File not found", filePath);

            return File.ReadAllText(filePath);
        }

        public void AppendToFile(string filePath, string content)
        {
            File.AppendAllText(filePath, content);
        }

        public void DeleteFile(string filePath)
        {
            if (DoesFileExists(filePath))
                File.Delete(filePath);
        }

        public long GetFileSize(string filePath)
        {
            if (!DoesFileExists(filePath))
                throw new FileNotFoundException("File not found", filePath);

            return new FileInfo(filePath).Length;
        }

        public void MoveFile(string sourceFilePath, string destinationFilePath)
        {
            if (!DoesFileExists(sourceFilePath))
                throw new FileNotFoundException("Source file not found", sourceFilePath);

            string? destinationDirectory = Path.GetDirectoryName(destinationFilePath) ?? null;

            if (destinationDirectory == null || !DoesDirectoryExists(destinationDirectory))
                throw new DirectoryNotFoundException("Destination directory does not exist");

            File.Move(sourceFilePath, destinationFilePath);
        }

        public async Task WriteAllBytesAsync(string filePath, byte[] bytes)
        {
            await File.WriteAllBytesAsync(filePath, bytes);
        }

        public Stream CreateFile(string filePath)
        {
            return File.Create(filePath);
        }

        #endregion

        #region Directory Operations

        public bool DoesDirectoryExists(string directoryPath) => Directory.Exists(directoryPath);

        public void CreateDirectory(string directoryPath)
        {
            if (!DoesDirectoryExists(directoryPath))
                Directory.CreateDirectory(directoryPath);
        }

        public void DeleteDirectory(string directoryPath, bool recursive)
        {
            if (DoesDirectoryExists(directoryPath))
                Directory.Delete(directoryPath, recursive);
        }

        public IEnumerable<string> GetFilesInDirectory(string directoryPath)
        {
            if (!DoesDirectoryExists(directoryPath))
                throw new DirectoryNotFoundException("Directory not found");

            return Directory.GetFiles(directoryPath);
        }

        public IEnumerable<string> GetDirectoriesInDirectory(string directoryPath)
        {
            if (!DoesDirectoryExists(directoryPath))
                throw new DirectoryNotFoundException("Directory not found");

            return Directory.GetDirectories(directoryPath);
        }

        #endregion

        #region Path Operations

        public string CombinePaths(params string[] paths)
        {
            return Path.Combine(paths);
        }

        #endregion
    }
}
