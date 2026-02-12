using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnBox3D.Utils;

namespace UnBox3D.Utils
{
    public class BlenderScriptResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class BlenderIntegration
    {
        private readonly ILogger _logger;
        private readonly IBlenderInstaller _blenderInstaller;

        public BlenderIntegration(ILogger logger, IBlenderInstaller blenderInstaller)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _blenderInstaller = blenderInstaller ?? throw new ArgumentNullException(nameof(blenderInstaller));
        }

        public bool RunBlenderScript(string inputModelPath, string outputModelPath, string scriptPath, 
            string filename, double doc_width, double doc_height, string ext, out string errorMessage,
            double inverseScale = 1.0)
        {
            // Synchronous wrapper for backward compatibility
            var result = RunBlenderScriptAsync(inputModelPath, outputModelPath, scriptPath, filename, doc_width, doc_height, ext, inverseScale).Result;
            errorMessage = result.ErrorMessage;
            return result.Success;
        }

        public async Task<BlenderScriptResult> RunBlenderScriptAsync(string inputModelPath, string outputModelPath, string scriptPath, 
            string filename, double doc_width, double doc_height, string ext, double inverseScale = 1.0)
        {
            var result = new BlenderScriptResult();
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            Debug.WriteLine("baseDirectory: " + baseDirectory);

            // Get Blender path from BlenderInstaller (which checks Program Files)
            string blenderExePath = _blenderInstaller.ExecutablePath;
            Debug.WriteLine("blenderExePath: " + blenderExePath);

            _logger.Info($"Base Directory: {baseDirectory}");
            _logger.Info($"Blender Path from BlenderInstaller: {blenderExePath}");

            if (string.IsNullOrEmpty(blenderExePath) || !File.Exists(blenderExePath))
            {
                result.ErrorMessage = $"Blender executable not found. Expected path: {blenderExePath ?? "(null)"}. Please ensure Blender is installed.";
                _logger.Error(result.ErrorMessage);
                Debug.WriteLine(result.ErrorMessage);
                return result;
            }

            if (!File.Exists(scriptPath))
            {
                result.ErrorMessage = $"Script file not found: {scriptPath}";
                _logger.Error(result.ErrorMessage);
                Debug.WriteLine(result.ErrorMessage);
                return result;
            }

            if (!File.Exists(inputModelPath))
            {
                result.ErrorMessage = $"Input model file not found: {inputModelPath}";
                _logger.Error(result.ErrorMessage);
                Debug.WriteLine(result.ErrorMessage);
                return result;
            }

            string arguments = $"-b -P \"{scriptPath}\"" +
                                $" -- --input_model \"{inputModelPath}\"" +
                                $" --output_model \"{outputModelPath}\"" +
                                $" --fn \"{filename}\"" +
                                $" --dw \"{doc_width}\"" +
                                $" --dh \"{doc_height}\"" +
                                $" --ext \"{ext}\"" +
                                (inverseScale != 1.0 ? $" --scale \"{inverseScale}\"" : "");

            _logger.Info($"Blender Command: {blenderExePath} {arguments}");
            Debug.WriteLine($"Full Blender Command: {blenderExePath} {arguments}");

            Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = blenderExePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = baseDirectory
                }
            };

            try
            {
                _logger.Info("Starting Blender process...");
                Debug.WriteLine("Starting Blender process...");

                bool processStarted = process.Start();
                if (!processStarted)
                {
                    result.ErrorMessage = "Failed to start Blender process.";
                    _logger.Error(result.ErrorMessage);
                    Debug.WriteLine(result.ErrorMessage);
                    return result;
                }

                _logger.Info($"Blender process started. PID: {process.Id}");
                Debug.WriteLine($"Blender process started. PID: {process.Id}");

                // Read output streams asynchronously to prevent deadlock
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                // Set a timeout for waiting for Blender to finish
                if (!process.WaitForExit(120000))
                {
                    result.ErrorMessage = "Process took too long to respond (2 minute timeout). Terminating...";
                    _logger.Warn(result.ErrorMessage);
                    Debug.WriteLine(result.ErrorMessage);
                    ForceTerminateBlender();
                    return result;
                }

                // Wait for output streams to complete
                string output = await outputTask;
                string error = await errorTask;

                int exitCode = process.ExitCode;
                _logger.Info($"Blender process exited with code: {exitCode}");
                Debug.WriteLine($"Blender process exited with code: {exitCode}");

                // Log all output for debugging
                if (!string.IsNullOrWhiteSpace(output))
                {
                    _logger.Info("=== BLENDER STDOUT START ===");
                    _logger.Info(output);
                    _logger.Info("=== BLENDER STDOUT END ===");
                    Debug.WriteLine("=== BLENDER STDOUT START ===");
                    Debug.WriteLine(output);
                    Debug.WriteLine("=== BLENDER STDOUT END ===");
                }
                else
                {
                    _logger.Warn("Blender produced NO standard output.");
                    Debug.WriteLine("Blender produced NO standard output.");
                }

                if (!string.IsNullOrWhiteSpace(error))
                {
                    _logger.Warn("=== BLENDER STDERR START ===");
                    _logger.Warn(error);
                    _logger.Warn("=== BLENDER STDERR END ===");
                    Debug.WriteLine("=== BLENDER STDERR START ===");
                    Debug.WriteLine(error);
                    Debug.WriteLine("=== BLENDER STDERR END ===");
                }
                else
                {
                    _logger.Info("No errors reported by Blender.");
                    Debug.WriteLine("No errors reported by Blender.");
                }

                // Check exit code first
                if (exitCode != 0)
                {
                    result.ErrorMessage = $"Blender exited with error code {exitCode}. Check logs for details.";
                    _logger.Error(result.ErrorMessage);
                    Debug.WriteLine(result.ErrorMessage);
                    return result;
                }

                // Extract runtime error message if exists
                string runtimeErrorMessage = ExtractRuntimeError(error);

                if (string.IsNullOrEmpty(runtimeErrorMessage))
                {
                    _logger.Info("Blender script executed successfully.");
                    Debug.WriteLine("Blender script executed successfully.");
                    result.Success = true;
                    return result;
                }
                else
                {
                    result.ErrorMessage = runtimeErrorMessage ?? "An unknown error occurred during processing.";
                    _logger.Error($"Blender script failed with runtime error: {result.ErrorMessage}");
                    Debug.WriteLine($"Blender script failed with runtime error: {result.ErrorMessage}");
                    ForceTerminateBlender();
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Exception while running Blender: {ex.GetType().Name} - {ex.Message}";
                _logger.Error(result.ErrorMessage);
                _logger.Error($"Stack trace: {ex.StackTrace}");
                Debug.WriteLine(result.ErrorMessage);
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                ForceTerminateBlender();
                return result;
            }
        }

        private void ForceTerminateBlender()
        {
            try
            {
                var startInfo = new ProcessStartInfo("taskkill", "/F /IM blender.exe")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var taskKillProcess = Process.Start(startInfo))
                {
                    string output = taskKillProcess.StandardOutput.ReadToEnd();
                    string error = taskKillProcess.StandardError.ReadToEnd();
                    taskKillProcess.WaitForExit();

                    _logger.Info("Taskkill Output: " + output);
                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        _logger.Warn("Taskkill Errors: " + error);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to terminate Blender process: " + ex.Message);
            }
        }

        private string ExtractRuntimeError(string error)
        {
            if (error.Contains("ZeroDivisionError: float division by zero") ||
                error.Contains("RuntimeError: Invalid Input Error: An island is too big to fit onto page"))
            {
                return "continue";
            }

            if (error.Contains("'>' not supported between instances of 'NoneType' and 'int'") ||
                error.Contains("balance = sum"))
            {
                return "The model's geometry caused an error in the Paper Model addon. Try simplifying the mesh before unfolding.";
            }

            string pattern = @"RuntimeError:\s*(.+?)(?:\n|$)";
            Match match = Regex.Match(error, pattern);

            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }

            return null;
        }
    }
}
