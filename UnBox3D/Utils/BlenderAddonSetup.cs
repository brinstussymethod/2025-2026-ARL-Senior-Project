using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace UnBox3D.Utils
{
    public class BlenderAddonSetup
    {
        private readonly ILogger _logger;
        private readonly IBlenderInstaller _blenderInstaller;
        private readonly IFileSystem _fileSystem;

        public BlenderAddonSetup(ILogger logger, IBlenderInstaller blenderInstaller, IFileSystem fileSystem)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _blenderInstaller = blenderInstaller ?? throw new ArgumentNullException(nameof(blenderInstaller));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        /// <summary>
        /// Checks if the Export Paper Model addon is installed and enabled.
        /// If not, attempts to enable it automatically.
        /// </summary>
        /// <returns>True if addon is ready to use, false otherwise</returns>
        public async Task<(bool success, string message)> EnsureAddonIsEnabled()
        {
            string blenderExePath = _blenderInstaller.ExecutablePath;
            
            if (string.IsNullOrEmpty(blenderExePath) || !File.Exists(blenderExePath))
            {
                return (false, "Blender executable not found.");
            }

            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string setupScriptPath = _fileSystem.CombinePaths(baseDirectory, "Scripts", "setup_blender_addon.py");

            if (!File.Exists(setupScriptPath))
            {
                return (false, $"Setup script not found: {setupScriptPath}");
            }

            _logger.Info("Checking Export Paper Model addon status...");

            Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = blenderExePath,
                    Arguments = $"-b -P \"{setupScriptPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = baseDirectory
                }
            };

            try
            {
                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                if (!process.WaitForExit(10000)) // 10 second timeout
                {
                    process.Kill();
                    return (false, "Addon setup timed out.");
                }

                string output = await outputTask;
                string error = await errorTask;

                _logger.Info("Addon Setup Output:");
                _logger.Info(output);

                if (!string.IsNullOrWhiteSpace(error))
                {
                    _logger.Warn("Addon Setup Errors:");
                    _logger.Warn(error);
                }

                int exitCode = process.ExitCode;

                if (exitCode == 0 && output.Contains("SUCCESS"))
                {
                    _logger.Info("Export Paper Model addon is ready!");
                    return (true, "Addon enabled successfully.");
                }
                else if (output.Contains("already enabled"))
                {
                    _logger.Info("Export Paper Model addon is already enabled.");
                    return (true, "Addon already enabled.");
                }
                else
                {
                    string errorMsg = "Failed to enable addon. Manual installation required.\n\n" +
                                     "Please follow these steps:\n" +
                                     "1. Open Blender from Start Menu\n" +
                                     "2. Edit → Preferences → Add-ons\n" +
                                     "3. Search for 'paper'\n" +
                                     "4. Enable 'Export Paper Model'\n" +
                                     "5. Restart this application";
                    
                    _logger.Error(errorMsg);
                    return (false, errorMsg);
                }
            }
            catch (Exception ex)
            {
                string errorMsg = $"Exception during addon setup: {ex.Message}";
                _logger.Error(errorMsg);
                return (false, errorMsg);
            }
        }
    }
}
