using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using UnBox3D.Utils;

namespace UnBox3D.Utils
{
    public class BlenderIntegration
    {
        private readonly ILogger _logger;
        private readonly IBlenderInstaller _installer;

        public BlenderIntegration(ILogger logger, IBlenderInstaller installer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _installer = installer ?? throw new ArgumentNullException(nameof(installer));
        }

        public bool RunBlenderScript(string inputModelPath, string outputModelPath, string scriptPath, 
            string filename, double doc_width, double doc_height, string ext, out string errorMessage)
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            Debug.WriteLine("baseDirectory: " + baseDirectory);

            // Fix Blender path construction for publishing
            string blenderExePath = _installer.ExecutablePath;

            if (!File.Exists(blenderExePath))
            {
                errorMessage = $"Blender executable not found. Expected at: {blenderExePath}";
                _logger.Error(errorMessage);
                return false;
            }

            if (!File.Exists(scriptPath))
            {
                _logger.Error($"Script file not found: {scriptPath}");
                errorMessage = "The script file was not found.";
                return false;
            }

            Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = blenderExePath,
                    Arguments = $"-b -P \"{scriptPath}\"" +
                                $" -- --input_model \"{inputModelPath}\"" +
                                $" --output_model \"{outputModelPath}\"" +
                                $" --fn \"{filename}\"" +
                                $" --dw \"{doc_width}\"" +
                                $" --dh \"{doc_height}\"" +
                                $" --ext \"{ext}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = baseDirectory
                }
            };

            errorMessage = string.Empty;

            try
            {
                process.Start();

                // Set a timeout for waiting for Blender to finish
                if (!process.WaitForExit(30000))
                {
                    errorMessage = "Process took too long to respond. Terminating...";
                    _logger.Warn(errorMessage);
                    ForceTerminateBlender();
                    return false;
                }

                // Read Blender's output and error streams
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                _logger.Info("Blender Output: " + output);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    _logger.Warn("Blender Errors: " + error);
                }

                // Extract runtime error message if exists
                string? runtimeErrorMessage = ExtractRuntimeError(error);

                if (string.IsNullOrEmpty(runtimeErrorMessage))
                {
                    _logger.Info("Blender script executed successfully.");
                    return true;
                }
                else
                {
                    errorMessage = runtimeErrorMessage ?? "An unknown error occurred during processing.";
                    _logger.Error($"Blender script failed. Exit code: {process.ExitCode}, Error: {errorMessage}");
                    ForceTerminateBlender();
                    return false;
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                _logger.Error("Error occurred while running Blender: " + errorMessage);
                ForceTerminateBlender();
                return false;
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
                    if (taskKillProcess == null)
                    {
                        _logger.Error("Failed to start taskkill process.");
                        return;
                    }
                    
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

        private string? ExtractRuntimeError(string error)
        {
            if (error.Contains("ZeroDivisionError: float division by zero") ||
                error.Contains("RuntimeError: Invalid Input Error: An island is too big to fit onto page"))
            {
                return "continue";
            }

            if (error.Contains("RuntimeError: Error: Python: Traceback (most recent call last)"))
            {
                return "Model was too complex to perform unfolding.";
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
