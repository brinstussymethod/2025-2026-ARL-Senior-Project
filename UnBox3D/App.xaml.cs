using Microsoft.Extensions.DependencyInjection;
using OpenTK.Mathematics;
using System;
using System.IO;
using System.Windows;
using UnBox3D.Controls;
using UnBox3D.Controls.States;
using UnBox3D.Models;
using UnBox3D.Rendering;
using UnBox3D.Rendering.OpenGL;
using UnBox3D.Utils;
using UnBox3D.ViewModels;
using UnBox3D.Views;
using UnBox3D.Theming;
using UnBox3D.Rendering.Rulers;
using Application = System.Windows.Application;

namespace UnBox3D
{
    public partial class App : Application
    {
        private static ServiceProvider? _serviceProvider;
        public static ServiceProvider Services => _serviceProvider!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // ── Global exception traps ──────────────────────────────────────
            // Write every unhandled exception to %TEMP%\UnBox3D_crash.txt AND
            // show a MessageBox so the app never silently exits with code 0.
            DispatcherUnhandledException += (_, ex) =>
            {
                WriteCrashLog(ex.Exception);
                System.Windows.MessageBox.Show(
                $"Unhandled UI exception:\n\n{ex.Exception.GetType().Name}: {ex.Exception.Message}" +
                $"\n\n{ex.Exception.InnerException?.Message}" +
                $"\n\nDetails written to:\n{CrashLogPath}",
                "UnBox3D — Crash", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                ex.Handled = true;
                Shutdown(1);
            };
            AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            {
                if (ex.ExceptionObject is Exception e2) WriteCrashLog(e2);
            };
            // ───────────────────────────────────────────────────────────────

            // Configure the service provider (Dependency Injection container)
            _serviceProvider = ConfigureServices();

            var themeManager = _serviceProvider.GetRequiredService<IThemeManager>();
            themeManager.ApplySavedTheme(false);

            var splash = new SplashWindow();
            splash.Show();

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1700)
            };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                try
                {
                    var menu = _serviceProvider.GetRequiredService<MainMenuWindow>();
                    Current.MainWindow = menu;
                    menu.Show();      // open menu BEFORE closing splash
                    splash.Close();   // now safe — a window is already open
                }
                catch (Exception ex)
                {
                    // Keep splash alive so the app doesn't exit with no windows,
                    // then show the real error before shutting down cleanly.
                    System.Windows.MessageBox.Show(
                        $"Startup failed:\n\n{ex.GetType().Name}: {ex.Message}\n\n{ex.InnerException?.Message}",
                        "UnBox3D — Startup Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                    splash.Close();
                    Shutdown(1);
                }
            };
            timer.Start();
        }

        private ServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Core
            services.AddSingleton<IFileSystem, FileSystem>();
            services.AddSingleton<ILogger, Logger>(provider =>
            {
                var fs = provider.GetRequiredService<IFileSystem>();
                return new Logger(fs, logDirectory: @"C:\ProgramData\UnBox3D\Logs", logFileName: "UnBox3D.log");
            });
            services.AddSingleton<ICommandHistory, CommandHistory>();
            services.AddSingleton<ISettingsManager, SettingsManager>();
            services.AddSingleton<IThemeManager, ThemeManager>();

            // Rendering
            services.AddSingleton<ISceneManager, SceneManager>();
            services.AddSingleton<IRayCaster, RayCaster>();
            services.AddSingleton<ICamera, Camera>(provider =>
            {
                Vector3 defaultPos = new Vector3(0, 0, 0);
                float defaultAspectRatio = 16f / 9f;
                return new Camera(defaultPos, defaultAspectRatio);
            });
            services.AddSingleton<IRenderer, SceneRenderer>(provider =>             // IRenderer, SceneRenderer
            {
                var logger = provider.GetRequiredService<ILogger>();
                var settings = provider.GetRequiredService<ISettingsManager>();
                var scene = provider.GetRequiredService<ISceneManager>();
                return new SceneRenderer(logger, settings, scene);
            });

            // Ruler system
            services.AddSingleton<IRulerManager, RulerManager>();
            services.AddSingleton<RulerRenderer>();

            // GLControlHost is the WinForms GL surface bridge. It now receives the unified DI Camera.
            // IMPORTANT: host no longer owns/creates its own Camera/Mouse/Ray; that was the source of duplicates.
            services.AddSingleton<GLControlHost>(provider =>                        // GLControlHost
            {
                var scene         = provider.GetRequiredService<ISceneManager>();
                var renderer      = provider.GetRequiredService<IRenderer>();
                var settings      = provider.GetRequiredService<ISettingsManager>();
                var camera        = provider.GetRequiredService<ICamera>();
                var rulerManager  = provider.GetRequiredService<IRulerManager>();
                var rulerRenderer = provider.GetRequiredService<RulerRenderer>();
                return new GLControlHost(scene, renderer, settings, camera, rulerManager, rulerRenderer);
            });
            services.AddSingleton<IGLControlHost>(sp => sp.GetRequiredService<GLControlHost>());

            // Input/State
            services.AddSingleton<IState, DefaultState>(provider =>
            {
                var scene  = provider.GetRequiredService<ISceneManager>();
                var host   = provider.GetRequiredService<IGLControlHost>();
                var camera = provider.GetRequiredService<ICamera>();
                var ray    = provider.GetRequiredService<IRayCaster>();
                return new DefaultState(scene, host, camera, ray);
            });
            services.AddSingleton<MouseController>(provider =>
            {
                var settings = provider.GetRequiredService<ISettingsManager>();
                var camera   = provider.GetRequiredService<ICamera>();
                var state    = provider.GetRequiredService<IState>();
                var ray      = provider.GetRequiredService<IRayCaster>();
                var host     = provider.GetRequiredService<GLControlHost>();
                return new MouseController(settings, camera, state, ray, host);
            });

            // To make sure keyboard functionality is available for the main window
            services.AddSingleton<KeyboardController>(provider =>
            {
                var camera = provider.GetRequiredService<ICamera>();
                return new KeyboardController(camera);
            });

            // Windows & VM (windows transient to avoid reuse of closed instances)
            services.AddTransient<SettingsWindow>();
            services.AddTransient<MainWindow>();
            services.AddTransient<MainMenuWindow>(provider => new MainMenuWindow(provider));

            services.AddSingleton<IBlenderInstaller, BlenderInstaller>(provider =>
            {
                var fs = provider.GetRequiredService<IFileSystem>();
                return new BlenderInstaller(fs);
            });
            services.AddSingleton<ModelExporter>(provider =>
            {
                var settings = provider.GetRequiredService<ISettingsManager>();
                return new ModelExporter(settings);
            });
            services.AddSingleton<MainViewModel>(provider =>
            {
                var logger   = provider.GetRequiredService<ILogger>();
                var settings = provider.GetRequiredService<ISettingsManager>();
                var scene    = provider.GetRequiredService<ISceneManager>();
                var fs       = provider.GetRequiredService<IFileSystem>();
                var blender  = provider.GetRequiredService<BlenderIntegration>();
                var installer= provider.GetRequiredService<IBlenderInstaller>();
                var exporter = provider.GetRequiredService<ModelExporter>();
                var mouse    = provider.GetRequiredService<MouseController>();
                var camera   = provider.GetRequiredService<ICamera>();
                var host     = provider.GetRequiredService<IGLControlHost>();
                var history  = provider.GetRequiredService<ICommandHistory>();
                var renderer = provider.GetRequiredService<IRenderer>();
                var rulerMgr = provider.GetRequiredService<IRulerManager>();
                var rulerRdr = provider.GetRequiredService<RulerRenderer>();
                return new MainViewModel(logger, settings, scene, fs, blender, installer, exporter, mouse, host, camera, history, renderer, rulerMgr, rulerRdr);
            });

            services.AddSingleton<BlenderIntegration>();

            return services.BuildServiceProvider();
        }

        private static readonly string CrashLogPath =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "UnBox3D_crash.txt");

        private static void WriteCrashLog(Exception ex)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] UnBox3D crash");
                sb.AppendLine();
                var current = ex;
                int depth = 0;
                while (current != null && depth < 6)
                {
                    sb.AppendLine($"--- Exception (depth {depth}) ---");
                    sb.AppendLine($"Type   : {current.GetType().FullName}");
                    sb.AppendLine($"Message: {current.Message}");
                    sb.AppendLine($"Stack  :");
                    sb.AppendLine(current.StackTrace);
                    sb.AppendLine();
                    current = current.InnerException;
                    depth++;
                }
                System.IO.File.WriteAllText(CrashLogPath, sb.ToString());
            }
            catch { /* never let logging crash the crash handler */ }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Preserve export-folder cleanup behavior on exit
            var settingsManager = _serviceProvider?.GetRequiredService<ISettingsManager>();
            if (settingsManager != null)
            {
                bool cleanupOnExit = settingsManager.GetSetting<bool>(
                    new AppSettings().GetKey(),
                    AppSettings.CleanupExportOnExit
                );

                // 2. If the user wants cleanup, do it
                if (cleanupOnExit)
                {
                    // Also fetch the export directory from settings
                    string? exportDir = settingsManager.GetSetting<string>(
                        new AppSettings().GetKey(),
                        AppSettings.ExportDirectory
                    );

                    // Fallback if it doesn't exist
                    if (string.IsNullOrWhiteSpace(exportDir) || !Directory.Exists(exportDir))
                    {
                        exportDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Export");
                    }

                    try
                    {
                        foreach (var file in Directory.GetFiles(exportDir, "*.obj")) File.Delete(file);
                        foreach (var file in Directory.GetFiles(exportDir, "*.mtl")) File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to clean up export directory: {ex.Message}");
                    }
                }
            }
            // Clean up the service provider on exit
            _serviceProvider?.Dispose();
            base.OnExit(e);
        }
    }
}
