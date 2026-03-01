using System;
using System.IO;
using System.Windows.Controls;
using gs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UnBox3D.Utils
{
    public interface ISettingsManager
    {
        void SaveSettings();
        T? GetSetting<T>(string mainSettingKey, string subSettingKey);
        T? GetSetting<T>(string parentSettingKey, string mainSettingKey, string subSettingKey);
        void SetSetting(string mainSettingKey, string subSettingKey, object newValue);
        void SetSetting(string parentSettingKey, string mainSettingKey, string subSettingKey, object newValue);
    }

    public class SettingsManager : ISettingsManager
    {
        #region Fields

        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;
        private readonly string _settingsFilePath;
        private readonly object _lock = new object();
        private JObject _settings;

        #endregion

        #region Constructor

        public SettingsManager(IFileSystem fileSystem, ILogger logger)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            string settingsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "UnBox3D",
                "Settings"
            );
            _settingsFilePath = Path.Combine(settingsDirectory, "settings.json");

            if (!_fileSystem.DoesDirectoryExists(settingsDirectory))
            {
                _logger.Info($"Settings directory not found. Creating directory: {settingsDirectory}");
                _fileSystem.CreateDirectory(settingsDirectory);
            }

            LoadSettings();
        }

        #endregion

        #region Settings Loading

        /// <summary>
        /// Loads settings from the file system, or initializes default settings if none exist.
        /// </summary>
        private void LoadSettings()
        {
            lock (_lock)
            {
                try
                {
                    _logger.Info("Attempting to load settings.");
                    if (_fileSystem.DoesFileExists(_settingsFilePath))
                    {
                        _logger.Info($"Settings file found. Loading settings from: {_settingsFilePath}");
                        // Load existing settings from file.
                        var json = _fileSystem.ReadFile(_settingsFilePath);
                        _settings = JsonConvert.DeserializeObject<JObject>(json) ?? GetDefaultSettings(); // fall back to defaults if JSON is invalid
                        _logger.Info("Settings loaded successfully.");
                    }
                    else
                    {
                        _logger.Warn("Settings file not found. Initializing with default settings.");
                        _settings = GetDefaultSettings();
                        SaveSettings();
                        _logger.Info("Default settings initialized and saved.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to load settings. Error: {ex.Message}");
                    throw;
                }
            }
        }

        #endregion

        #region Default Settings

        /// <summary>
        /// Provides default values for application settings.
        /// This is the main location for developers to set or update default values for settings used throughout the application.
        /// Each setting is organized into categories that match the structure of the settings.json file.
        /// To change a default value, simply modify the corresponding entry in this dictionary.
        /// </summary>
        private JObject GetDefaultSettings()
        {
            var appSettings = new AppSettings();
            var assimpSettings = new AssimpSettings();
            var renderingSettings = new RenderingSettings();
            var uiSettings = new UISettings();
            var unitsSettings = new UnitsSettings();
            var windowSettings = new WindowSettings();

            return new JObject
            {
                [appSettings.GetKey()] = new JObject
                {
                    [AppSettings.SplashScreenDuration] = appSettings.DefaultSplashScreenDuration,
                    [AppSettings.ExportDirectory] = appSettings.DefaultExportDirectory,
                    [AppSettings.CleanupExportOnExit] = appSettings.DefaultCleanupExportOnExit
                },

                [assimpSettings.GetKey()] = new JObject
                {
                    [AssimpSettings.Export] = new JObject(), // Empty object to match the structure
                    [AssimpSettings.Import] = new JObject
                    {
                        [AssimpSettings.EnableTriangulation] = assimpSettings.DefaultEnableTriangulation,
                        [AssimpSettings.JoinIdenticalVertices] = assimpSettings.DefaultJoinIdenticalVertices,
                        [AssimpSettings.RemoveComponents] = assimpSettings.DefaultRemoveComponents,
                        [AssimpSettings.SplitLargeMeshes] = assimpSettings.DefaultSplitLargeMeshes,
                        [AssimpSettings.OptimizeMeshes] = assimpSettings.DefaultOptimizeMeshes,
                        [AssimpSettings.FindDegenerates] = assimpSettings.DefaultFindDegenerates,
                        [AssimpSettings.FindInvalidData] = assimpSettings.DefaultFindInvalidData,
                        [AssimpSettings.IgnoreInvalidData] = assimpSettings.DefaultIgnoreInvalidData
                    }
                },

                [renderingSettings.GetKey()] = new JObject
                {
                    [RenderingSettings.BackgroundColor] = renderingSettings.DefaultBackgroundColor,
                    [RenderingSettings.MeshColor] = renderingSettings.DefaultMeshColor,
                    [RenderingSettings.MeshHighlightColor] = renderingSettings.DefaultMeshHighlightColor,
                    [RenderingSettings.RenderMode] = renderingSettings.DefaultRenderMode,
                    [RenderingSettings.ShadingModel] = renderingSettings.DefaultShadingModel,
                    [RenderingSettings.LightingEnabled] = renderingSettings.DefaultLightingEnabled,
                    [RenderingSettings.ShadowsEnabled] = renderingSettings.DefaultShadowsEnabled
                },

                [uiSettings.GetKey()] = new JObject
                {
                    [UISettings.ToolStripPosition] = uiSettings.DefaultToolStripPosition,
                    [UISettings.CameraYawSensitivity] = uiSettings.DefaultCameraYawSensitivity,
                    [UISettings.CameraPitchSensitivity] = uiSettings.DefaultCameraPitchSensitivity,
                    [UISettings.CameraPanSensitivity] = uiSettings.DefaultCameraPanSensitivity,
                    [UISettings.MeshRotationSensitivity] = uiSettings.DefaultMeshRotationSensitivity,
                    [UISettings.MeshMoveSensitivity] = uiSettings.DefaultMeshMoveSensitivity,
                    [UISettings.ZoomSensitivity] = uiSettings.DefaultZoomSensitivity
                },

                [unitsSettings.GetKey()] = new JObject
                {
                    [UnitsSettings.DefaultUnit] = unitsSettings.DefaultDefaultUnit,
                    [UnitsSettings.UseMetricSystem] = unitsSettings.DefaultUseMetricSystem
                },

                [windowSettings.GetKey()] = new JObject
                {
                    [WindowSettings.Fullscreen] = windowSettings.DefaultFullscreen,
                    [WindowSettings.Height] = windowSettings.DefaultHeight,
                    [WindowSettings.Width] = windowSettings.DefaultWidth
                }
            };
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Saves the current settings to the settings file.
        /// </summary>
        public void SaveSettings()
        {
            lock (_lock)
            {
                var json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
                _fileSystem.WriteToFile(_settingsFilePath, json);
            }
        }

        #region GetSetting Methods

        // Get a setting (two levels of nested dictionaries).
        public T? GetSetting<T>(string parentKey, string subSetting)
        {
            lock (_lock)
            {
                JObject parentDict;
                if (_settings.ContainsKey(parentKey))
                {
                    parentDict = (JObject)_settings[parentKey];

                    if (parentDict.ContainsKey(subSetting))
                    {
                        return parentDict[subSetting].ToObject<T>();
                    }
                    else
                    {
                        _logger.Warn($"Sub-setting '{subSetting}' not found under '{parentKey}'.");
                    }
                }
                else
                {
                    _logger.Warn($"Parent key {parentKey} not present in Settings Dictionary.");
                }

                return default;
            }
        }

        // Get a setting (three levels of nested dictionaries).
        public T? GetSetting<T>(string parentKey, string mainSetting, string subSetting)
        {
            lock (_lock)
            {
                JObject parentDict;
                JObject mainDict;
                if (_settings.ContainsKey(parentKey))
                {
                    parentDict = (JObject)_settings[parentKey];

                    if (parentDict.ContainsKey(mainSetting))
                    {
                        mainDict = (JObject)parentDict[mainSetting];

                        if (mainDict.ContainsKey(subSetting))
                        {
                            return mainDict[subSetting].ToObject<T>();
                        }
                        else
                        {
                            _logger.Warn($"Sub-setting '{subSetting}' not found under '{mainSetting}'.");
                        }
                    }
                    else
                    {
                        _logger.Warn($"Main-setting '{mainSetting}' not found under '{parentKey}'.");
                    }
                }
                else
                {
                    _logger.Warn($"Parent key {parentKey} not present in Settings Dictionary.");
                }

                return default;
            }
        }

        #endregion

        #region SetSetting Methods

        // Update a setting (two levels of nested dictionaries).
        public void SetSetting(string parentKey, string subSetting, object newValue)
        {
            lock (_lock)
            {
                LoadSettings();
                JObject parentDict;
                if (_settings.ContainsKey(parentKey))
                {
                    parentDict = (JObject)_settings[parentKey];

                    if (parentDict.ContainsKey(subSetting))
                    {
                        parentDict[subSetting] = JToken.FromObject(newValue);
                        _logger.Info($"Updated {parentKey} -> {subSetting}");
                    }
                    else
                    {
                        _logger.Warn($"Sub-setting '{subSetting}' not found under '{parentKey}'.");
                    }
                }
                else
                {
                    _logger.Warn($"Parent key {parentKey} not present in Settings Dictionary.");
                }

                SaveSettings();
            }
        }

        // Update a setting (three levels of nested dictionaries).
        public void SetSetting(string parentKey, string mainSetting, string subSetting, object newValue)
        {
            lock (_lock)
            {
                LoadSettings();

                JObject parentDict;
                JObject mainDict;
                if (_settings.ContainsKey(parentKey))
                {
                    parentDict = (JObject)_settings[parentKey];

                    if (parentDict.ContainsKey(mainSetting))
                    {
                        mainDict = (JObject)parentDict[mainSetting];

                        if (mainDict.ContainsKey(subSetting))
                        {
                            mainDict[subSetting] = JToken.FromObject(newValue);
                            _logger.Info($"Updated {parentKey} -> {mainSetting} -> {subSetting}");
                        }
                        else
                        {
                            _logger.Warn($"Sub-setting '{subSetting}' not found under '{mainSetting}'.");
                        }
                    }
                    else
                    {
                        _logger.Warn($"Main-setting '{mainSetting}' not found under '{parentKey}'.");
                    }
                }
                else
                {
                    _logger.Warn($"Parent key {parentKey} not present in Settings Dictionary.");
                }

                SaveSettings();
            }
        }

        #endregion

        #endregion
    }
}