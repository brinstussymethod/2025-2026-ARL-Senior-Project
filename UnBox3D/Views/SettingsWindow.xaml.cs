using System.Windows;
using UnBox3D.Utils;
using TextBox = System.Windows.Controls.TextBox;
using CheckBox = System.Windows.Controls.CheckBox;
using System.Collections.Generic;


namespace UnBox3D.Views
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        #region Fields
        private ILogger _logger;
        private ISettingsManager _settingsManager;
        private Dictionary<string, Func<string, bool>> _inputValidators; // NOTE: only supports simple, run-on words in textual inputs. Invalid string formats will highlight the input reddish.
        private Dictionary<string, Tuple<string, string>> _inputSettingKeys; // NOTE: maps text-box name -> (parent-name, sub-setting-name)
        #endregion

        #region Miscellaneous Methods
        private bool isDigit(char symbol)
        {
            return symbol >= '0' && symbol <= '9';
        }
        #endregion

        public SettingsWindow()
        {
            InitializeComponent();

            /// add lifecycle handlers
            Loaded += SettingsWindow_Loaded;
            Closed += SettingsWindow_Closed;
        }

        #region Lifecycle Handlers
        private void SettingsWindow_Loaded(object sender, RoutedEventArgs args)
        {
            _logger?.Info("SettingsWindow loaded.");
        }

        private void SettingsWindow_Closed(object? sender, EventArgs args)
        {
            _logger?.Info("SettingsWindow closed.");
        }

        public void Initialize(ILogger logger, ISettingsManager settingsManager)
        {
            /// for mapping inputs to validator functions which trigger on edit
            _inputValidators = new Dictionary<string, Func<string, bool>>
            {
                { "splash_screen_opt", IsValidIntegerInput },
                { "background_color_opt", IsValidWordInput },
                { "mesh_color_opt", IsValidWordInput },
                { "mesh_highlight_color_opt", IsValidWordInput },
                { "render_mode_opt", IsValidWordInput },
                { "shading_model_opt", IsValidWordInput },
                { "tool_strip_position_opt", IsValidWordInput },
                { "camera_yaw_factor_opt", IsValidRealNumberInput },
                { "camera_pitch_factor_opt", IsValidRealNumberInput },
                { "camera_pan_factor_opt", IsValidRealNumberInput },
                { "mesh_rotate_factor_opt", IsValidRealNumberInput },
                { "mesh_move_factor_opt", IsValidRealNumberInput },
                { "zoom_factor_opt", IsValidRealNumberInput },
                { "default_unit_opt", IsValidWordInput },
                { "default_window_height_opt", IsValidIntegerInput },
                { "default_window_width_opt", IsValidIntegerInput }
            };

            /// for mapping inputs to appropriate setting keys
            _inputSettingKeys = new Dictionary<string, Tuple<string, string>>
            {
                { "splash_screen_opt", Tuple.Create("AppSettings", "SplashScreenDuration") },
                { "export_path_opt", Tuple.Create("AppSettings", "ExportDirectory") },
                { "triangulation_opt", Tuple.Create("AssimpSettings", "EnableTriangulation") },
                { "join_identical_vertices_opt", Tuple.Create("AssimpSettings", "JoinIdenticalVertices") },
                { "component_removal_opt", Tuple.Create("AssimpSettings", "RemoveComponents") },
                { "split_large_meshes_opt", Tuple.Create("AssimpSettings", "SplitLargeMeshes") },
                { "optimize_meshes_opt", Tuple.Create("AssimpSettings", "OptimizeMeshes") },
                { "find_degens_opt", Tuple.Create("AssimpSettings", "FindDegenerates") },
                { "find_invalid_data_opt", Tuple.Create("AssimpSettings", "FindInvalidData") },
                { "ignore_invalid_data_opt", Tuple.Create("AssimpSettings", "IgnoreInvalidData") },
                { "background_color_opt", Tuple.Create("RenderingSettings", "BackgroundColor") },
                { "mesh_color_opt", Tuple.Create("RenderingSettings", "DefaultMeshColor") },
                { "mesh_highlight_color_opt", Tuple.Create("RenderingSettings", "MeshHighlightColor") },
                { "render_mode_opt", Tuple.Create("RenderingSettings", "RenderMode") },
                { "shading_model_opt", Tuple.Create("RenderingSettings", "ShadingModel") },
                { "enable_lighting_opt", Tuple.Create("RenderingSettings", "LightingEnabled") },
                { "enable_shadows_opt", Tuple.Create("RenderingSettings", "ShadowsEnabled") },
                { "tool_strip_position_opt", Tuple.Create("UISettings", "ToolStripPosition") },
                { "camera_yaw_factor_opt", Tuple.Create("UISettings", "CameraYawSensitivity") },
                { "camera_pitch_factor_opt", Tuple.Create("UISettings", "CameraPitchSensitivity") },
                { "camera_pan_factor_opt", Tuple.Create("UISettings", "CameraPanSensitivity") },
                { "mesh_rotate_factor_opt", Tuple.Create("UISettings", "MeshRotationSensitivity") },
                { "mesh_move_factor_opt", Tuple.Create("UISettings", "MeshMoveSensitivity") },
                { "zoom_factor_opt", Tuple.Create("UISettings", "ZoomSensitivity") },
                { "default_unit_opt", Tuple.Create("UnitsSettings", "DefaultUnit") },
                { "enable_metric_system_opt", Tuple.Create("UnitsSettings", "UseMetricSystem") },
                { "fullscreen_opt", Tuple.Create("WindowSettings", "Fullscreen") },
                { "default_window_height_opt", Tuple.Create("WindowSettings", "Height") },
                { "default_window_width_opt", Tuple.Create("WindowSettings", "Width") },
            };

            /// injects dependencies to initialize logger and settings management components...
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        }
        #endregion

        #region Validation Methods
        private bool IsValidWordInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            foreach (var symbol in input)
            {
                if ((symbol < 'A' || symbol > 'Z') && (symbol < 'a' || symbol > 'z'))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsValidRealNumberInput(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            int preDigits = 0;
            int postDigits = 0;
            bool foundPoint = false;

            foreach (var symbol in input)
            {
                {
                    if (!isDigit(symbol) && symbol != '.')
                    {
                        return false;
                    }

                    if (symbol == '.')
                    {
                        foundPoint = true;
                        continue;
                    }

                    if (foundPoint)
                    {
                        postDigits++;
                    }
                    else
                    {
                        preDigits++;
                    }
                }

            }
            
            return preDigits > 0 && foundPoint && postDigits > 0;
        }

        private bool IsValidIntegerInput(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            foreach(var symbol in input)
            {
                if (!isDigit(symbol))
                {
                    return false;
                }
            }

            return true;
        }
        #endregion

        #region Interaction Methods
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            _logger.Info("Saving settings changes...");
            _settingsManager.SaveSettings();
            _logger.Info("Settings saved.");
            var mainWindow = App.Current.Windows
                .OfType<MainWindow>()
                .FirstOrDefault();

            mainWindow?.Show();
            this.Hide();
        }

        private void TextBox_InputChanged(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            var textBoxName = textBox.Name;
            var textInput = textBox.Text;

            if (_inputValidators == null)
            {
                return;
            }

            /// NOTE: this code will validate a text-box's value if applicable, and then submit
            if (_inputValidators.ContainsKey(textBoxName))
            {
                if (_inputValidators[textBoxName](textInput))
                {
                    textBox.BorderBrush = System.Windows.Media.Brushes.SpringGreen;

                    (string parentName, string subName) = _inputSettingKeys[textBoxName];

                    _settingsManager.SetSetting(parentName, subName, textInput);
                }
                else
                {
                    textBox.BorderBrush = System.Windows.Media.Brushes.OrangeRed;
                }
            }
        }

        private void TextBox_PathChanged(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            var textBoxName = textBox.Name;
            var textInput = textBox.Text;

            if (_inputSettingKeys == null)
            {
                return;
            }

            (string parentName, string subName) = _inputSettingKeys[textBoxName];

            _settingsManager.SetSetting(parentName, subName, textInput);
        }

        private void CheckBox_Toggled(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            var checkBoxName = checkBox.Name;
            var checkFlag = checkBox.IsChecked;

            if (_inputSettingKeys == null)
            {
                return;
            }

            (string parentName, string subName) = _inputSettingKeys[checkBoxName];

            _settingsManager.SetSetting(parentName, subName, checkFlag ?? false);

        }
        #endregion
    }
}

