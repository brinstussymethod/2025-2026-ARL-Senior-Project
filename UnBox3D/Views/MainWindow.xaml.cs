// MainWindow.xaml.cs — MERGED
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.IO;
using Control = System.Windows.Forms.Control;
using UnBox3D.Rendering.OpenGL;
using UnBox3D.Utils;
using UnBox3D.ViewModels;
using UnBox3D.Models; // added for MeshSummary/IAppMesh
using TextBox = System.Windows.Controls.TextBox;
using Application = System.Windows.Application;

using UnBox3D.Rendering;
using UnBox3D.Controls;
namespace UnBox3D.Views
{
    public partial class MainWindow : Window
    {
        private IBlenderInstaller _blenderInstaller;
        private IGLControlHost? _controlHost;
        private ILogger? _logger;
        private string? _pendingOpenPath;

        private MainViewModel VM => DataContext as MainViewModel;

        public MainWindow()
        {
            InitializeComponent();

            try
            {
                DataContext ??= App.Services.GetRequiredService<MainViewModel>();
            }
            catch
            {
                Loaded += (_, __) =>
                {
                    if (DataContext == null)
                    {
                        DataContext = App.Services.GetRequiredService<MainViewModel>();
                    }
                };
            }

            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
        }

        public void Initialize(IGLControlHost controlHost, ILogger logger, IBlenderInstaller blenderInstaller)
        {
            _controlHost = controlHost ?? throw new ArgumentNullException(nameof(controlHost));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _blenderInstaller = blenderInstaller ?? throw new ArgumentNullException(nameof(blenderInstaller));

            try
            {
                var m = VM?.GetType().GetMethod("Initialize");
                if (m != null && VM != null)
                    m.Invoke(VM, new object[] { controlHost, logger, blenderInstaller });
            }
            catch { }
        }

        private async void MainWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            try
            {
                _logger?.Info("MainWindow loaded. Initializing OpenGL...");

                var loadingWindow = new LoadingWindow
                {
                    StatusHint = "Installing Blender...",
                    Owner = this,
                    IsProgressIndeterminate = false
                };
                loadingWindow.Show();

                if (_blenderInstaller != null)
                {
                    var progress = new Progress<double>(value =>
                    {
                        loadingWindow.UpdateProgress(value * 100);
                        loadingWindow.UpdateStatus($"Installing Blender... {Math.Round(value * 100)}%");
                    });

                    await _blenderInstaller.CheckAndInstallBlender(progress);
                }
                else
                {
                    _logger?.Warn("Blender installer dependency was null; skipping installation check.");
                }

                loadingWindow.Close();

                if (_controlHost is not null)
                {
                    openGLHost.Child = (Control)_controlHost;
                    _logger?.Info("GLControlHost successfully attached to WindowsFormsHost.");
                    StartUpdateLoop();
                }
                else
                {
                    _logger?.Warn("GLControlHost not initialized; skipping rendering start.");
                }
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

        private async void StartUpdateLoop()
        {
            var sw = new Stopwatch();
            while (IsLoaded)
            {
                _controlHost?.Render();
                await Task.Delay(16);
            }
        }

        public void OpenFromPath(string path)
        {
            if (!IsLoaded)
            {
                _pendingOpenPath = path;
                Loaded -= MainWindow_Loaded_OpenPending;
                Loaded += MainWindow_Loaded_OpenPending;
                return;
            }
            DoOpen(path);
        }

        private void MainWindow_Loaded_OpenPending(object? sender, RoutedEventArgs e)
        {
            Loaded -= MainWindow_Loaded_OpenPending;
            if (!string.IsNullOrWhiteSpace(_pendingOpenPath))
            {
                DoOpen(_pendingOpenPath);
                _pendingOpenPath = null;
            }
        }

        private void DoOpen(string path)
        {
            try
            {
                var vm = VM;
                var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();

                if (vm != null)
                {
                    var importMethod = vm.GetType().GetMethod("ImportModelFromPath");
                    if (importMethod != null)
                    {
                        importMethod.Invoke(vm, new object[] { path });
                    }
                    else
                    {
                        _logger?.Warn("ImportModelFromPath not found on MainViewModel; cannot import without popping a dialog.");
                        System.Windows.MessageBox.Show(this,
                            "This build doesn’t support path-based import yet. Ask your team to add MainViewModel.ImportModelFromPath(string).",
                            "UnBox3D", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    vm.GetType().GetMethod("FrameScene")?.Invoke(vm, null);
                }

                this.Title = $"UnBox3D — {System.IO.Path.GetFileName(path)}";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this,
                    $"Failed to open '{System.IO.Path.GetFileName(path)}':\n{ex.Message}",
                    "UnBox3D", MessageBoxButton.OK, MessageBoxImage.Error);
                _logger?.Error($"Open failed: {ex}");
            }
        }

        // Back to Main Menu (XAML button may exist elsewhere)
        private void BackToMainMenu_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var mainMenu = new MainMenuWindow(App.Services);
                mainMenu.Show();
            }
            finally
            {
                this.Close();
            }
        }

        private void ImportModel_Click(object sender, RoutedEventArgs e)
        {
            if (VM?.ImportObjModelCommand is ICommand cmd && cmd.CanExecute(null))
                cmd.Execute(null);
        }

        private void ExportModel_Click(object sender, RoutedEventArgs e)
        {
            if (VM?.ExportModelCommand is ICommand cmd && cmd.CanExecute(null))
                cmd.Execute(null);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            var prop = VM?.GetType().GetProperty("ExitCommand");
            if (prop?.GetValue(VM) is ICommand cmd && cmd.CanExecute(null))
                cmd.Execute(null);
            else
                Application.Current.Shutdown();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var settings = ActivatorUtilities.CreateInstance<SettingsWindow>(App.Services);
            settings.Owner = this;
            settings.ShowDialog();
        }

        private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;

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
            if (e.Key == System.Windows.Input.Key.Back || e.Key == System.Windows.Input.Key.Delete || e.Key == System.Windows.Input.Key.Left ||
                e.Key == System.Windows.Input.Key.Right || e.Key == System.Windows.Input.Key.Tab)
            {
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == System.Windows.Input.Key.V)
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

        private static bool IsValidDecimalInput(string input)
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

        // Slider hook -> SmallMeshThreshold + reapply filter (fixes mismatch)
        private void MeshThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (VM != null)
            {
                var prop = VM.GetType().GetProperty("SmallMeshThreshold");
                if (prop != null && prop.CanWrite) prop.SetValue(VM, (float)e.NewValue);
                VM.GetType().GetMethod("ApplyMeshThreshold")?.Invoke(VM, null);
            }
        }

        // Extra signature for teammate XAML using EventArgs
        private void MeshThreshold_ValueChanged(object sender, EventArgs e)
        {
            if (sender is System.Windows.Controls.Slider slider && DataContext is MainViewModel vm)
            {
                vm.SmallMeshThreshold = (float)slider.Value;
                vm.ApplyMeshThreshold();
            }
        }

        // Tree selection -> VM.SelectedMesh (adds back original behavior)
        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is not MainViewModel vm) return;

            if (e.NewValue is MeshSummary summary && summary.SourceMesh != null)
            {
                vm.SelectedMesh = summary.SourceMesh;
            }
            else if (e.NewValue is IAppMesh mesh)
            {
                vm.SelectedMesh = mesh;
            }
            else
            {
                vm.SelectedMesh = null;
            }
        }

        private void EnsureTopLevelMainMenu()
        {
            try
            {
                var rootMenu = VisualChildrenFirstOrDefault<System.Windows.Controls.Menu>(this);
                if (rootMenu == null) return;

                bool exists = rootMenu.Items.OfType<System.Windows.Controls.MenuItem>()
                    .Any(mi => string.Equals((mi.Header as string)?.Replace("_", "").Trim() ?? mi.Header?.ToString() ?? "",
                                              "Main Menu", StringComparison.OrdinalIgnoreCase));

                if (!exists)
                {
                    var item = new System.Windows.Controls.MenuItem { Header = "_Main Menu" };
                    item.Click += BackToMainMenu_Click;
                    rootMenu.Items.Add(item);
                }
            }
            catch
            {
            }
        }

        private MainMenuWindow CreateMainMenuWindow()
        {
            try
            {
                var servicesProp = typeof(App).GetProperty("Services", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var services = servicesProp?.GetValue(null) as System.IServiceProvider;

                var ctor = typeof(MainMenuWindow).GetConstructor(new System.Type[] { typeof(System.IServiceProvider) });
                if (ctor != null && services != null)
                    return (MainMenuWindow)ctor.Invoke(new object[] { services });

                return new MainMenuWindow(App.Services);
            }
            catch
            {
                return new MainMenuWindow(App.Services);
            }
        }

        private static T? VisualChildrenFirstOrDefault<T>(System.Windows.DependencyObject? parent) where T : System.Windows.DependencyObject
        {
            if (parent == null) return default;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T tChild) return tChild;
                var hit = VisualChildrenFirstOrDefault<T>(child);
                if (hit != null) return hit;
            }
            return default;
        }
    }
}

