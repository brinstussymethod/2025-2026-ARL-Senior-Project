using System.Diagnostics;
using System.Windows;
using UnBox3D.Rendering.OpenGL;
using UnBox3D.Utils;
using System.Windows.Input;
using TextBox = System.Windows.Controls.TextBox;
using UnBox3D.Rendering;
using UnBox3D.ViewModels;
using UnBox3D.Models;

namespace UnBox3D.Views
{
    public partial class MainWindow : Window
    {
        #region Fields
        private IBlenderInstaller _blenderInstaller;
        private IGLControlHost? _controlHost;
        private ILogger? _logger;
        #endregion

        #region Constructor
        public MainWindow()
        {
            InitializeComponent();

            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
        }
        #endregion

        #region Event Handlers
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger?.Info("MainWindow loaded. Initializing OpenGL...");

                // Ensure Blender is installed
                var loadingWindow = new LoadingWindow
                {
                    StatusHint = "Installing Blender...",
                    Owner = System.Windows.Application.Current.MainWindow,
                    IsProgressIndeterminate = false
                };
                loadingWindow.Show();

                var progress = new Progress<double>(value =>
                {
                    loadingWindow.UpdateProgress(value * 100);
                    loadingWindow.UpdateStatus($"Installing Blender... {Math.Round(value * 100)}%");
                });

                await _blenderInstaller.CheckAndInstallBlender(progress);

                loadingWindow.Close();

                // Attach GLControlHost to WindowsFormsHost
                openGLHost.Child = (Control)_controlHost;

                _logger?.Info("GLControlHost successfully attached to WindowsFormsHost.");
                StartUpdateLoop();
            }
            catch (Exception ex)
            {
                _logger?.Error($"Error initializing OpenGL: {ex.Message}");
                System.Windows.MessageBox.Show($"Error initializing OpenGL: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            try
            {
                _logger?.Info("MainWindow is closing. Performing cleanup...");
                _controlHost?.Cleanup();
                (_controlHost as IDisposable)?.Dispose();
                _logger?.Info("Cleanup completed successfully.");
            }
            catch (Exception ex)
            {
                _logger?.Error($"Error during cleanup: {ex.Message}");
            }
        }


        private void Settings_Click(object? sender, EventArgs e)
        {
            // click handler for Settings menu item: opens the settings window!
            // NOTE: maybe refactor the SettingsWindow opening code to store a reusable instance of it in an App instance?
            var settingsWindow = App.Current.Windows
                .OfType<SettingsWindow>()
                .FirstOrDefault();

            if (settingsWindow != null)
            {
                settingsWindow.Show();
                this.Hide();
            }
            else
            {
                _logger?.Warn("Failed to open settings window, found null instance instead.");
            }

        }
        #endregion

        #region Initialization
        // Inject dependencies
        public void Initialize(IGLControlHost controlHost, ILogger logger, IBlenderInstaller blenderInstaller)
        {
            _controlHost = controlHost ?? throw new ArgumentNullException(nameof(controlHost));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _blenderInstaller = blenderInstaller ?? throw new ArgumentNullException(nameof(blenderInstaller));
        }
        #endregion

        #region Update Loop
        private async void StartUpdateLoop()
        {
            bool isRunning = true;
            Stopwatch sw = new Stopwatch();

            while (isRunning)
            {
                _controlHost.Render();
                await Task.Delay(Math.Max(0, 16 - (int)sw.ElapsedMilliseconds));
            }
        }
        #endregion

        private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;

            // Only allow digits and one decimal
            if (!char.IsDigit(e.Text[0]) && e.Text != ".")
            {
                e.Handled = true;
                return;
            }

            if (e.Text == "." && textBox.Text.Contains("."))
            {
                e.Handled = true;
                return;
            }
        }

        private void NumericTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Allow navigation, deletion and control keys
            if (e.Key == Key.Back || e.Key == Key.Delete || e.Key == Key.Left ||
                e.Key == Key.Right || e.Key == Key.Tab)
            {
                return;
            }

            // Handle clipboard operations
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.V)
            {
                var textBox = sender as TextBox;

                if (System.Windows.Clipboard.ContainsText())
                {
                    string clipboardText = System.Windows.Clipboard.GetText();

                    if (!IsValidDecimalInput(clipboardText))
                    {
                        e.Handled = true;
                        return;
                    }

                    string resultText = textBox.Text.Substring(0, textBox.SelectionStart) +
                                        clipboardText +
                                        textBox.Text.Substring(textBox.SelectionStart + textBox.SelectionLength);

                    if (resultText.Count(c => c == '.') > 1)
                    {
                        e.Handled = true;
                        return;
                    }
                }
            }
        }

        private bool IsValidDecimalInput(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            bool hasDecimal = false;

            foreach (char c in input)
            {
                if (c == '.')
                {
                    if (hasDecimal)
                        return false;
                    hasDecimal = true;
                }
                else if (!char.IsDigit(c))
                {
                    return false;
                }
            }

            return true;
        }

        private void NumericTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            int cursorPosition = textBox.SelectionStart;
            string originalText = textBox.Text;

            if (string.IsNullOrEmpty(textBox.Text) || textBox.Text == ".")
                return;

            if (float.TryParse(textBox.Text, out float value) &&
                textBox.DataContext is ViewModels.MainViewModel viewModel)
            {
                if (textBox.Name.Contains("Width"))
                    viewModel.PageWidth = value;
                else if (textBox.Name.Contains("Height"))
                    viewModel.PageHeight = value;
            }

            if (textBox.Text != originalText)
            {
                int charsAdded = textBox.Text.Length - originalText.Length;
                cursorPosition += charsAdded > 0 ? charsAdded : 0;
            }

            textBox.SelectionStart = cursorPosition;
        }

        private void NumericTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;

            if (string.IsNullOrWhiteSpace(textBox.Text) || textBox.Text == ".")
            {
                textBox.Text = "0";

                if (textBox.DataContext is ViewModels.MainViewModel viewModel)
                {
                    if (textBox.Name.Contains("Width"))
                        viewModel.PageWidth = 0;
                    else if (textBox.Name.Contains("Height"))
                        viewModel.PageHeight = 0;
                }
            }
        }

        private void MeshThreshold_ValueChanged(object sender, EventArgs e)
        { 
            if (sender is System.Windows.Controls.Slider slider)
            {
                Debug.WriteLine(slider.Value);
                if (DataContext is MainViewModel vm)
                {
                    vm.SmallMeshThreshold = (float)slider.Value;
                    vm.ApplyMeshThreshold();
                }
            }
        }

        // Runs whenever the user left-clicks a different item in the TreeView.
        // e.NewValue is a MeshSummary, not the raw mesh object, so we pull the underlying SourceMesh.
        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is not MainViewModel vm) return;


            // The TreeView is bound to MeshSummary items. Select the *real* mesh:
            if (e.NewValue is MeshSummary summary && summary.SourceMesh != null)
            {
                vm.SelectedMesh = summary.SourceMesh;   // -> triggers OnSelectedMeshChanged in the VM (/ViewModels/MainViewModel.cs)
            }
            else if (e.NewValue is IAppMesh mesh)       // (handles the case you ever bind meshes directly)
            {
                vm.SelectedMesh = mesh;
            }
            else
            {
                vm.SelectedMesh = null;                 // clicked a non-mesh node
            }
        }

    }
}
