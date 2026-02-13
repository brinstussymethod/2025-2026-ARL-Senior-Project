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

        // Possible addon module names (Blender 4.2 extension system)
        private static readonly string[] AddonNames =
        [
            "bl_ext.user_default.export_paper_model",
            "bl_ext.blender_org.export_paper_model",
            "export_paper_model"
        ];

        public BlenderAddonSetup(ILogger logger, IBlenderInstaller blenderInstaller, IFileSystem fileSystem)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _blenderInstaller = blenderInstaller ?? throw new ArgumentNullException(nameof(blenderInstaller));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        /// <summary>
        /// Fully automated setup: installs the addon if missing, enables it, and patches the flat-geometry bug.
        /// No user input required.
        /// </summary>
        public async Task<(bool success, string message)> EnsureAddonIsEnabled()
        {
            string blenderExePath = _blenderInstaller.ExecutablePath;

            if (string.IsNullOrEmpty(blenderExePath) || !File.Exists(blenderExePath))
            {
                return (false, "Blender executable not found.");
            }

            _logger.Info("=== Automatic Addon Setup Started ===");

            // Step 1: Check if addon is already installed
            bool isInstalled = IsAddonInstalled();

            if (!isInstalled)
            {
                // Step 2: Install the addon via Blender CLI
                _logger.Info("Addon not found. Installing automatically...");
                var installResult = await InstallAddonViaCli(blenderExePath);
                if (!installResult.success)
                {
                    return installResult;
                }
            }
            else
            {
                _logger.Info("Export Paper Model addon is already installed.");
            }

            // Step 3: Enable the addon
            var enableResult = await EnableAddonViaCli(blenderExePath);
            if (!enableResult.success)
            {
                _logger.Warn($"Enable step reported: {enableResult.message}");
                // Non-fatal — addon may already be enabled
            }

            // Step 4: Patch the flat-geometry bug
            PatchAddonBug();

            _logger.Info("=== Automatic Addon Setup Complete ===");
            return (true, "Addon installed, enabled, and patched automatically.");
        }

        /// <summary>
        /// Checks if the addon folder exists on disk.
        /// </summary>
        private bool IsAddonInstalled()
        {
            string addonPath = GetAddonPath();
            bool exists = !string.IsNullOrEmpty(addonPath) && Directory.Exists(addonPath);
            _logger.Info($"Addon path check: {addonPath ?? "(not found)"} — exists: {exists}");
            return exists;
        }

        /// <summary>
        /// Returns the addon folder path if found, otherwise null.
        /// </summary>
        private string? GetAddonPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            // Check common Blender addon locations (4.2 extension system)
            string[] possiblePaths =
            [
                Path.Combine(appData, "Blender Foundation", "Blender", "4.2", "extensions", "user_default", "export_paper_model"),
                Path.Combine(appData, "Blender Foundation", "Blender", "4.2", "extensions", "blender_org", "export_paper_model"),
                Path.Combine(appData, "Blender Foundation", "Blender", "4.2", "scripts", "addons", "export_paper_model"),
            ];

            foreach (var path in possiblePaths)
            {
                if (Directory.Exists(path))
                    return path;
            }

            return possiblePaths[0]; // Default path for installation
        }

        /// <summary>
        /// Installs the Export Paper Model addon using Blender's CLI extension installer.
        /// </summary>
        private async Task<(bool success, string message)> InstallAddonViaCli(string blenderExePath)
        {
            // Use Blender's built-in extension install command
            string pythonScript =
                "import bpy, sys\n" +
                "try:\n" +
                "    bpy.ops.extensions.repo_sync(repo_index=0)\n" +
                "    bpy.ops.extensions.package_install(repo_index=0, pkg_id='export_paper_model')\n" +
                "    bpy.ops.wm.save_userpref()\n" +
                "    print('ADDON_INSTALL_SUCCESS')\n" +
                "except Exception as e:\n" +
                "    print(f'ADDON_INSTALL_FAILED: {e}')\n" +
                "    sys.exit(1)\n";

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string tempScript = Path.Combine(baseDir, "Scripts", "_temp_install_addon.py");

            try
            {
                await File.WriteAllTextAsync(tempScript, pythonScript);

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = blenderExePath,
                        Arguments = $"-b --python \"{tempScript}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = baseDir
                    }
                };

                process.Start();
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                if (!process.WaitForExit(60000)) // 60 second timeout for download
                {
                    process.Kill();
                    return (false, "Addon installation timed out (60s).");
                }

                string output = await outputTask;
                string error = await errorTask;

                _logger.Info($"Addon install output: {output}");
                if (!string.IsNullOrWhiteSpace(error))
                    _logger.Warn($"Addon install stderr: {error}");

                if (output.Contains("ADDON_INSTALL_SUCCESS"))
                {
                    _logger.Info("Addon installed successfully via CLI.");
                    return (true, "Addon installed.");
                }
                else
                {
                    _logger.Warn("CLI install may have failed. Will check addon folder...");
                    // Check if it actually installed despite the output
                    if (IsAddonInstalled())
                        return (true, "Addon appears to be installed.");

                    return (false, "Failed to install addon automatically. " + output);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Exception installing addon: {ex.Message}");
                return (false, $"Exception: {ex.Message}");
            }
            finally
            {
                try { File.Delete(tempScript); } catch { }
            }
        }

        /// <summary>
        /// Enables the addon using Blender's CLI.
        /// </summary>
        private async Task<(bool success, string message)> EnableAddonViaCli(string blenderExePath)
        {
            // Try each possible addon name
            string addonNamesList = string.Join("', '", AddonNames);
            string pythonScript =
                "import bpy\n" +
                $"names = ['{addonNamesList}']\n" +
                "enabled = False\n" +
                "for name in names:\n" +
                "    try:\n" +
                "        bpy.ops.preferences.addon_enable(module=name)\n" +
                "        bpy.ops.wm.save_userpref()\n" +
                "        print(f'ADDON_ENABLED: {name}')\n" +
                "        enabled = True\n" +
                "        break\n" +
                "    except Exception:\n" +
                "        pass\n" +
                "if not enabled:\n" +
                "    print('ADDON_ENABLE_FAILED')\n";

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string tempScript = Path.Combine(baseDir, "Scripts", "_temp_enable_addon.py");

            try
            {
                await File.WriteAllTextAsync(tempScript, pythonScript);

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = blenderExePath,
                        Arguments = $"-b --python \"{tempScript}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = baseDir
                    }
                };

                process.Start();
                var outputTask = process.StandardOutput.ReadToEndAsync();

                if (!process.WaitForExit(15000))
                {
                    process.Kill();
                    return (false, "Enable timed out.");
                }

                string output = await outputTask;
                _logger.Info($"Addon enable output: {output}");

                return output.Contains("ADDON_ENABLED")
                    ? (true, "Addon enabled.")
                    : (false, "Could not enable addon.");
            }
            catch (Exception ex)
            {
                return (false, $"Exception: {ex.Message}");
            }
            finally
            {
                try { File.Delete(tempScript); } catch { }
            }
        }

        /// <summary>
        /// Patches the Export Paper Model addon's files to fix the flat-geometry crash.
        /// The addon crashes with "TypeError: '>' not supported between instances of 'NoneType' and 'int'"
        /// when edge.angle is None on flat/coplanar faces.
        /// Patches ALL addon locations (user_default has unfolder.py, blender_org has __init__.py).
        /// </summary>
        private void PatchAddonBug()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            // All possible addon locations and the files that contain the buggy line
            var filesToPatch = new[]
            {
                Path.Combine(appData, "Blender Foundation", "Blender", "4.2", "extensions", "user_default", "export_paper_model", "unfolder.py"),
                Path.Combine(appData, "Blender Foundation", "Blender", "4.2", "extensions", "blender_org", "export_paper_model", "__init__.py"),
                Path.Combine(appData, "Blender Foundation", "Blender", "4.2", "extensions", "blender_org", "export_paper_model", "unfolder.py"),
                Path.Combine(appData, "Blender Foundation", "Blender", "4.2", "scripts", "addons", "export_paper_model", "__init__.py"),
            };

            // The blender_org version has a slightly different line (includes "if not edge.is_cut")
            string[] buggyPatterns =
            [
                "balance = sum((+1 if edge.angle > 0 else -1) for edge in island_edges)",
                "balance = sum((+1 if edge.angle > 0 else -1) for edge in island_edges if not edge.is_cut(uvedge.uvface.face))",
            ];

            string[] fixedPatterns =
            [
                "balance = sum((+1 if (edge.angle or 0) > 0 else -1) for edge in island_edges)",
                "balance = sum((+1 if (edge.angle or 0) > 0 else -1) for edge in island_edges if not edge.is_cut(uvedge.uvface.face))",
            ];

            foreach (var filePath in filesToPatch)
            {
                if (!File.Exists(filePath)) continue;

                try
                {
                    string content = File.ReadAllText(filePath);
                    bool patched = false;

                    for (int i = 0; i < buggyPatterns.Length; i++)
                    {
                        if (content.Contains(buggyPatterns[i]))
                        {
                            content = content.Replace(buggyPatterns[i], fixedPatterns[i]);
                            patched = true;
                        }
                    }

                    if (patched)
                    {
                        File.WriteAllText(filePath, content);
                        _logger.Info($"Patched: {filePath}");
                    }
                    else
                    {
                        _logger.Info($"Already patched or different version: {filePath}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to patch {filePath}: {ex.Message}");
                }
            }
        }
    }
}
