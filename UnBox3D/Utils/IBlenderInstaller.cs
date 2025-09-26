using System.IO;
using System.Net.Http;
using System.IO.Compression;
using System.Diagnostics;
using System.Windows;

namespace UnBox3D.Utils
{
    public interface IBlenderInstaller
    {
        Task CheckAndInstallBlender();
        Task CheckAndInstallBlender(IProgress<double> progress);
        bool IsBlenderInstalled();
        string ExecutablePath { get; }
    }

    public class BlenderInstaller : IBlenderInstaller
    {
        private readonly IFileSystem _fileSystem;

        private string BlenderFolder;
        private string BlenderExecutable;
        private string ProgramFilesBlenderExecutable;
        private readonly string BlenderZipPath;
        public string ExecutablePath => BlenderExecutable;
        private static readonly string BlenderDownloadUrl = "https://download.blender.org/release/Blender4.2/blender-4.2.0-windows-x64.zip";
        // Quick test URL for invalid URL handling
        //private static string BlenderDownloadUrl = "https://invalid.url.fake/blender.zip";
        private Task? _blenderInstallTask;

        public BlenderInstaller(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            BlenderFolder = _fileSystem.CombinePaths(baseDir, "Blender");
            BlenderExecutable = _fileSystem.CombinePaths(BlenderFolder, "blender-4.2.0-windows-x64", "blender.exe");
            ProgramFilesBlenderExecutable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
                    , "Blender Foundation", "blender-4.2", "blender.exe"); // this will be updated if found
            BlenderZipPath = _fileSystem.CombinePaths(baseDir, "blender.zip");

            #region Standard Program Files path (version-independent)
            // 1. Try to find 'Blender Foundation' in Program Files
            string blenderFoundationPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Blender Foundation"
                );

            // 2. If it exists, look for versioned subfolders
            if (Directory.Exists(blenderFoundationPath))
            {
                DirectoryInfo blenderFoundationDir = new DirectoryInfo(blenderFoundationPath);
                DirectoryInfo[] blenderVersions = blenderFoundationDir.GetDirectories(); // Get all subfolders (Blender versions)

                if (blenderVersions.Length > 0)
                {
                    var latestVersion = blenderVersions // find last-modified version folder
                        .OrderByDescending(d => d.LastWriteTime)
                        .First();

                    // Construct the expected path to blender.exe    
                    ProgramFilesBlenderExecutable = Path.Combine(
                        latestVersion.FullName,
                        "blender.exe"
                        );
                }
            }

            // Optional: if ProgramFilesBlenderExecutable exists, set BlenderExecutable to it
            if (_fileSystem.DoesFileExists(ProgramFilesBlenderExecutable))
            {
                BlenderExecutable = ProgramFilesBlenderExecutable;
                BlenderFolder = Path.GetDirectoryName(Path.GetDirectoryName(BlenderExecutable)) ?? BlenderFolder;
                Debug.WriteLine($"Using Blender from Program Files: {ProgramFilesBlenderExecutable}");
            }
            
        }

            #endregion

        public Task CheckAndInstallBlender()
        {
            return CheckAndInstallBlenderInternal();
        }

        public Task CheckAndInstallBlender(IProgress<double> progress)
        {
            return CheckAndInstallBlenderInternal(progress);
        }

        public bool IsBlenderInstalled()
        {
            return _fileSystem.DoesFileExists(BlenderExecutable);
        }

        private async Task CheckAndInstallBlenderInternal()
        {
            if (!_fileSystem.DoesDirectoryExists(BlenderFolder) || !_fileSystem.DoesFileExists(BlenderExecutable))
            {
                Debug.WriteLine("Blender 4.2 is not installed. Downloading now...");
                await DownloadAndExtractBlender();
            }
            else
            {
                Debug.WriteLine("Blender 4.2 is already installed.");
            }
        }

        private async Task CheckAndInstallBlenderInternal(IProgress<double>? progress = null)
        {

            // If Blender exists in Program Files, just use it
            if (_fileSystem.DoesFileExists(ProgramFilesBlenderExecutable))
            {
                BlenderExecutable = ProgramFilesBlenderExecutable;
                BlenderFolder = Path.GetDirectoryName(Path.GetDirectoryName(BlenderExecutable)) ?? BlenderFolder;
                Debug.WriteLine("Blender found in Program Files.");
                progress?.Report(1.0);
                return;
            }
            
            if (!_fileSystem.DoesDirectoryExists(BlenderFolder) || !_fileSystem.DoesFileExists(BlenderExecutable))
            {
                Debug.WriteLine("Blender 4.2 is not installed. Downloading now...");

                try
                {
                    await DownloadAndExtractBlender(progress);
                }
                catch (HttpRequestException ex)
                {
                    Debug.WriteLine("Internet connection error: " + ex.Message);
                    System.Windows.MessageBox.Show("Blender could not be downloaded. Please check your internet connection.",
                                    "Download Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                Debug.WriteLine("Blender 4.2 is already installed.");
                progress?.Report(1.0);
            }
        }

        private async Task DownloadAndExtractBlender()
        {
            using HttpClient client = new HttpClient();
            var response = await client.GetAsync(BlenderDownloadUrl);
            response.EnsureSuccessStatusCode();

            byte[] data = await response.Content.ReadAsByteArrayAsync();
            await _fileSystem.WriteAllBytesAsync(BlenderZipPath, data);

            Debug.WriteLine("Download complete. Extracting Blender...");

            if (!_fileSystem.DoesDirectoryExists(BlenderFolder))
                _fileSystem.CreateDirectory(BlenderFolder);

            try
            {
                ZipFile.ExtractToDirectory(BlenderZipPath, BlenderFolder, overwriteFiles: true);
                _fileSystem.DeleteFile(BlenderZipPath);

                Debug.WriteLine("Blender installation completed.");
            }
            catch (IOException ioEx)
            {
                Debug.WriteLine($"IO error during extraction: {ioEx.Message}");
                System.Windows.MessageBox.Show("Blender archive is currently in use or locked by another process. Please close any applications using it and try again.",
                                "Extraction Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (InvalidDataException dataEx)
            {
                Debug.WriteLine($"Extraction failed: Invalid or corrupted zip file. {dataEx.Message}");
                System.Windows.MessageBox.Show("The downloaded Blender archive appears to be corrupted. Please try downloading again.",
                                "Corrupt Archive", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (UnauthorizedAccessException accessEx)
            {
                Debug.WriteLine($"Permission issue during extraction: {accessEx.Message}");
                System.Windows.MessageBox.Show("Access denied while extracting Blender files. Please run the application as administrator.",
                                "Permission Denied", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unexpected error during extraction: {ex.Message}");
                System.Windows.MessageBox.Show("An unexpected error occurred while extracting Blender. Please try again or contact support.",
                                "Extraction Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DownloadAndExtractBlender(IProgress<double>? progress = null)
        {
            using HttpClient client = new HttpClient();
            var response = await client.GetAsync(BlenderDownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var downloadedBytes = 0L;
            var buffer = new byte[8192];

            // Step 1: Download and write zip file
            await using (var contentStream = await response.Content.ReadAsStreamAsync())
            await using (var fileStream = _fileSystem.CreateFile(BlenderZipPath))
            {
                int bytesRead;
                while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    downloadedBytes += bytesRead;

                    if (totalBytes > 0)
                        progress?.Report((double)downloadedBytes / totalBytes * 0.8);
                }
            }

            // Now, fileStream is fully disposed, and the file is closed.
            Debug.WriteLine("Download complete. Extracting Blender...");

            if (!_fileSystem.DoesDirectoryExists(BlenderFolder))
                _fileSystem.CreateDirectory(BlenderFolder);

            progress?.Report(0.85);
            try
            {
                ZipFile.ExtractToDirectory(BlenderZipPath, BlenderFolder, overwriteFiles: true);
                _fileSystem.DeleteFile(BlenderZipPath);
                progress?.Report(1.0);
                Debug.WriteLine("Blender installation completed.");
            }
            catch (IOException ioEx)
            {
                Debug.WriteLine($"IO error during extraction: {ioEx.Message}");
                System.Windows.MessageBox.Show("Blender archive is currently in use or locked by another process. Please close any applications using it and try again.",
                                "Extraction Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (InvalidDataException dataEx)
            {
                Debug.WriteLine($"Extraction failed: Invalid or corrupted zip file. {dataEx.Message}");
                System.Windows.MessageBox.Show("The downloaded Blender archive appears to be corrupted. Please try downloading again.",
                                "Corrupt Archive", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (UnauthorizedAccessException accessEx)
            {
                Debug.WriteLine($"Permission issue during extraction: {accessEx.Message}");
                System.Windows.MessageBox.Show("Access denied while extracting Blender files. Please run the application as administrator.",
                                "Permission Denied", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unexpected error during extraction: {ex.Message}");
                System.Windows.MessageBox.Show("An unexpected error occurred while extracting Blender. Please try again or contact support.",
                                "Extraction Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
