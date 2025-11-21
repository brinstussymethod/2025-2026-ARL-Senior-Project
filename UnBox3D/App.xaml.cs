// App.xaml.cs — MERGED
using Microsoft.Extensions.DependencyInjection;
using OpenTK.Mathematics;
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
            _serviceProvider = ConfigureServices();

            // Apply theme early and open the main menu first
            var themeManager = _serviceProvider.GetRequiredService<IThemeManager>();
            themeManager.ApplySavedTheme(false);

            var menu = _serviceProvider.GetRequiredService<MainMenuWindow>();
            Current.MainWindow = menu;
            menu.Show();
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
            services.AddSingleton<IRenderer, SceneRenderer>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger>();
                var settings = provider.GetRequiredService<ISettingsManager>();
                var scene = provider.GetRequiredService<ISceneManager>();
                return new SceneRenderer(logger, settings, scene);
            });
            services.AddSingleton<GLControlHost>(provider =>
            {
                var scene    = provider.GetRequiredService<ISceneManager>();
                var renderer = provider.GetRequiredService<IRenderer>();
                var settings = provider.GetRequiredService<ISettingsManager>();
                var camera   = provider.GetRequiredService<ICamera>();
                return new GLControlHost(scene, renderer, settings, camera); // adjusted to match 3‑arg constructornified pipeline
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
                return new MainViewModel(logger, settings, scene, fs, blender, installer, exporter, mouse, host, camera, history);
            });

            services.AddSingleton<BlenderIntegration>();

            return services.BuildServiceProvider();
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

                if (cleanupOnExit)
                {
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

            _serviceProvider?.Dispose();
            base.OnExit(e);
        }
    }
}
