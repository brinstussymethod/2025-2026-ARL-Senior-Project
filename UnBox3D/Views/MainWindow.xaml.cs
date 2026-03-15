using Microsoft.Extensions.DependencyInjection;
using OpenTK.Mathematics;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using UnBox3D.Controls;
using UnBox3D.Models;
using UnBox3D.Rendering;
using UnBox3D.Rendering.OpenGL;
using UnBox3D.Utils;
using UnBox3D.ViewModels;
using Application = System.Windows.Application;
using Control = System.Windows.Forms.Control;
using TextBox = System.Windows.Controls.TextBox;
namespace UnBox3D.Views
{
    public partial class MainWindow : Window
    {
        private IBlenderInstaller _blenderInstaller;
        private IGLControlHost? _controlHost;
        private ILogger? _logger;
        private string? _pendingOpenPath;
        private Window? _rulerOverlayWindow;
        private Canvas? _rulerOverlayCanvas;

        // ── Overlay HWND passthrough ────────────────────────────────────────
        // WS_EX_TRANSPARENT makes Win32 skip this window for mouse hit-testing so
        // scroll-to-zoom and clicks reach the GL viewport even over label pixels.
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int nIndex);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int nIndex, int dwNewLong);
        private const int GWL_EXSTYLE      = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;

        private void SetOverlayPassthrough(bool passthrough)
        {
            if (_rulerOverlayWindow == null) return;
            var hwnd  = new WindowInteropHelper(_rulerOverlayWindow).Handle;
            if (hwnd == IntPtr.Zero) return;
            int style = GetWindowLong(hwnd, GWL_EXSTYLE);
            style = passthrough ? (style |  WS_EX_TRANSPARENT)
                                : (style & ~WS_EX_TRANSPARENT);
            SetWindowLong(hwnd, GWL_EXSTYLE, style);
        }
        // Set to true by BackToMainMenu_Click so MainWindow_Closed knows
        // NOT to dispose the singleton GLControlHost (it will be reused).
        private bool _returningToMenu = false;

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
            DataContextChanged += OnDataContextChanged;

            // Subscribe to toast notifications
            ToastService.ToastRequested += ShowToast;

            // Keyboard shortcuts for undo / redo
            var undoGesture = new KeyBinding(
                new RelayCommandAdapter(() => VM?.UndoActionCommand?.Execute(null)),
                Key.Z, ModifierKeys.Control);
            var redoGesture = new KeyBinding(
                new RelayCommandAdapter(() => VM?.RedoActionCommand?.Execute(null)),
                Key.Y, ModifierKeys.Control);
            InputBindings.Add(undoGesture);
            InputBindings.Add(redoGesture);
            var deleteRulerGesture = new KeyBinding(
                new RelayCommandAdapter(() => VM?.RulerDeleteKey()),
                Key.Delete, ModifierKeys.None);
            InputBindings.Add(deleteRulerGesture);
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

                // Ensure Blender is installed
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

                PlayEntranceAnimations();

                if (_controlHost is not null)
                {
                    openGLHost.Child = (Control)_controlHost;
                    var gl = openGLHost.Child;
                    gl.MouseDown += OpenGL_MouseDown;
                    _logger?.Info("GLControlHost successfully attached to WindowsFormsHost.");

                    // Create a transparent overlay window for ruler labels.
                    // Owner = this keeps it above the main window (and its WindowsFormsHost child)
                    // without WS_EX_TOPMOST, so it doesn't float above other apps.
                    _rulerOverlayCanvas = new Canvas { Background = System.Windows.Media.Brushes.Transparent };
                    _rulerOverlayWindow = new Window
                    {
                        AllowsTransparency = true,
                        WindowStyle        = WindowStyle.None,
                        Background         = System.Windows.Media.Brushes.Transparent,
                        ShowInTaskbar      = false,
                        Topmost            = false,
                        IsHitTestVisible   = true,
                        Owner              = this,
                        Content            = _rulerOverlayCanvas,
                    };

                    var rulerOverlay = App.Services.GetRequiredService<UnBox3D.Rendering.Rulers.RulerOverlayManager>();
                    rulerOverlay.Attach(
                        _rulerOverlayCanvas,
                        App.Services.GetRequiredService<UnBox3D.Rendering.ICamera>(),
                        _controlHost,
                        App.Services.GetRequiredService<UnBox3D.Utils.IScaleSettings>(),
                        App.Services.GetRequiredService<UnBox3D.Rendering.Rulers.IRulerManager>(),
                        App.Services.GetRequiredService<UnBox3D.Models.ICommandHistory>());

                    // When the ruler tool is inactive, make the overlay HWND fully transparent
                    // to mouse messages so scroll-to-zoom and clicks pass through label pixels
                    // to the GL viewport. WS_EX_TRANSPARENT does this at the Win32 level.
                    // The window HWND is available after Show(), so we set initial state there.
                    rulerOverlay.InRulerModeChanged += active => SetOverlayPassthrough(!active);

                    _rulerOverlayCanvas.Width  = ViewportGrid.ActualWidth;
                    _rulerOverlayCanvas.Height = ViewportGrid.ActualHeight;
                    _rulerOverlayWindow.Show();
                    // Apply initial passthrough state (app starts in Select mode, not Ruler mode).
                    SetOverlayPassthrough(!rulerOverlay.InRulerMode);
                    UpdateRulerOverlayPosition();

                    ViewportGrid.SizeChanged += (_, e) =>
                    {
                        _rulerOverlayCanvas.Width  = e.NewSize.Width;
                        _rulerOverlayCanvas.Height = e.NewSize.Height;
                        UpdateRulerOverlayPosition();
                    };
                    this.LocationChanged += (_, _) => UpdateRulerOverlayPosition();
                    this.SizeChanged     += (_, _) => UpdateRulerOverlayPosition();

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

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is MainViewModel oldVm)
                oldVm.PropertyChanged -= OnVmPropertyChanged;
            if (e.NewValue is MainViewModel newVm)
                newVm.PropertyChanged += OnVmPropertyChanged;
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(MainViewModel.HierarchyVisible)) return;
            if (sender is not MainViewModel vm) return;

            Dispatcher.Invoke(() =>
            {
                bool show = vm.HierarchyVisible;

                // Stop any running animation so the local value is writable again,
                // then snap directly - instant and reliable.
                HierarchyColumn.BeginAnimation(ColumnDefinition.WidthProperty, null);
                HierarchyColumn.Width = new GridLength(show ? 260 : 0);

                // Also hide the splitter so the 4px gap doesn't linger.
                HierarchySplitter.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        private System.Windows.Threading.DispatcherTimer? _toastTimer;
        private System.Windows.Threading.DispatcherTimer? _notifyTimer;

        private void ShowToast(string message, bool isError)
        {
            Dispatcher.Invoke(() => ShowNotification(message, isError));
        }

        private void ShowNotification(string message, bool isError)
        {
            var accentBrush = isError
                ? new System.Windows.Media.SolidColorBrush(
                      System.Windows.Media.Color.FromRgb(0xC0, 0x39, 0x2B))
                : new System.Windows.Media.SolidColorBrush(
                      System.Windows.Media.Color.FromRgb(0x00, 0xB3, 0x94));

            var bgBrush = isError
                ? new System.Windows.Media.SolidColorBrush(
                      System.Windows.Media.Color.FromArgb(0xF5, 0x28, 0x0C, 0x0C))
                : new System.Windows.Media.SolidColorBrush(
                      System.Windows.Media.Color.FromArgb(0xF5, 0x08, 0x18, 0x14));

            NotifyBanner.Background  = bgBrush;
            NotifyBanner.BorderBrush = accentBrush;
            NotifyIcon.Text          = isError ? "⚠" : "✓";
            NotifyIcon.Foreground    = accentBrush;
            NotifyText.Text          = message;
            NotifyText.Foreground    = System.Windows.Media.Brushes.White;

            NotifyBanner.Opacity = 0;
            NotifyBanner.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            double bannerW = Math.Max(NotifyBanner.DesiredSize.Width, 200);

            NotifyPopup.PlacementTarget   = this;
            NotifyPopup.Placement         = System.Windows.Controls.Primitives.PlacementMode.Relative;
            NotifyPopup.HorizontalOffset  = (this.ActualWidth  - bannerW) / 2.0;
            NotifyPopup.VerticalOffset    = 90;
            NotifyPopup.IsOpen            = true;

            NotifyBanner.RenderTransform = new System.Windows.Media.TranslateTransform(0, -8);
            NotifyBanner.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            ((System.Windows.Media.TranslateTransform)NotifyBanner.RenderTransform)
                .BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
                    new DoubleAnimation(-8, 0, TimeSpan.FromMilliseconds(260))
                    { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });

            _notifyTimer?.Stop();
            _notifyTimer = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromSeconds(isError ? 4.0 : 2.8) };
            _notifyTimer.Tick += (_, __) =>
            {
                _notifyTimer!.Stop();
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(400))
                    { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
                fadeOut.Completed += (__, ___) => NotifyPopup.IsOpen = false;
                NotifyBanner.BeginAnimation(OpacityProperty, fadeOut);
            };
            _notifyTimer.Start();
        }

        private void PlayEntranceAnimations()
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            const int dur = 420;

            ToolbarBorder.Opacity = 0;
            ToolbarBorder.RenderTransform = new System.Windows.Media.TranslateTransform(0, -32);
            ToolbarBorder.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(dur)) { EasingFunction = ease });
            ((System.Windows.Media.TranslateTransform)ToolbarBorder.RenderTransform)
                .BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
                    new DoubleAnimation(-32, 0, TimeSpan.FromMilliseconds(dur)) { EasingFunction = ease });

            HierarchyPanel.Opacity = 0;
            HierarchyPanel.RenderTransform = new System.Windows.Media.TranslateTransform(-40, 0);
            HierarchyPanel.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(dur))
                { EasingFunction = ease, BeginTime = TimeSpan.FromMilliseconds(80) });
            ((System.Windows.Media.TranslateTransform)HierarchyPanel.RenderTransform)
                .BeginAnimation(System.Windows.Media.TranslateTransform.XProperty,
                    new DoubleAnimation(-40, 0, TimeSpan.FromMilliseconds(dur))
                    { EasingFunction = ease, BeginTime = TimeSpan.FromMilliseconds(80) });

            ToolsPanel.Opacity = 0;
            ToolsPanel.RenderTransform = new System.Windows.Media.TranslateTransform(40, 0);
            ToolsPanel.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(dur))
                { EasingFunction = ease, BeginTime = TimeSpan.FromMilliseconds(160) });
            ((System.Windows.Media.TranslateTransform)ToolsPanel.RenderTransform)
                .BeginAnimation(System.Windows.Media.TranslateTransform.XProperty,
                    new DoubleAnimation(40, 0, TimeSpan.FromMilliseconds(dur))
                    { EasingFunction = ease, BeginTime = TimeSpan.FromMilliseconds(160) });
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            ToastService.ToastRequested -= ShowToast;
            if (DataContext is MainViewModel vm) vm.PropertyChanged -= OnVmPropertyChanged;

            // Always unsubscribe the mouse handler from the shared WinForms control
            // so stale handlers from old window instances don't keep firing.
            try { if (openGLHost?.Child != null) openGLHost.Child.MouseDown -= OpenGL_MouseDown; }
            catch { }

            // Close the ruler overlay window (it's owned by this window, but close explicitly
            // to avoid stale references when returning to main menu and reopening).
            _rulerOverlayWindow?.Close();
            _rulerOverlayWindow = null;

            if (_returningToMenu)
            {
                // The GLControlHost is a DI singleton — a new MainWindow will reuse it.
                // Do NOT dispose or clean it up here, only detach it from this host.
                _logger?.Info("MainWindow closing to return to main menu. GL host preserved.");
                return;
            }

            // Full exit path: safe to tear everything down.
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
                // Reposition ruler labels every frame. During drag, RulerState.OnMouseMove
                // also calls UpdateAll so the label tracks the ruler without lag.
                VM?.RulerOverlayManager?.UpdateAll(
                    App.Services.GetRequiredService<UnBox3D.Rendering.Rulers.IRulerManager>().GetRulers());
                await Task.Delay(16);
            }
        }

        /// <summary>
        /// Repositions the ruler overlay window to exactly cover the GL viewport.
        /// Uses PointToScreen + DPI transform for correct multi-monitor support.
        /// </summary>
        private void UpdateRulerOverlayPosition()
        {
            if (_rulerOverlayWindow == null || !_rulerOverlayWindow.IsVisible || !IsLoaded) return;
            var pt = ViewportGrid.PointToScreen(new System.Windows.Point(0, 0));

            // PointToScreen returns device pixels; WPF Window.Left/Top expect DIPs.
            var source = PresentationSource.FromVisual(this);
            double dpiX = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
            double dpiY = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

            _rulerOverlayWindow.Left   = pt.X * dpiX;
            _rulerOverlayWindow.Top    = pt.Y * dpiY;
            _rulerOverlayWindow.Width  = ViewportGrid.ActualWidth;
            _rulerOverlayWindow.Height = ViewportGrid.ActualHeight;
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

        private void BackToMainMenu_Click(object sender, RoutedEventArgs e)
        {
            // Tell Closed handler to preserve the singleton GL host.
            _returningToMenu = true;
            // Use DI so the transient MainMenuWindow is properly constructed.
            var menu = App.Services.GetRequiredService<MainMenuWindow>();
            Application.Current.MainWindow = menu;
            menu.Show();
            Close();
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

        private void Help_Click(object sender, RoutedEventArgs e)
        {
            var help = new HelpWindow(App.Services) { Owner = this };
            help.ShowDialog();
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            var about = new AboutWindow { Owner = this };
            about.ShowDialog();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var settings = ActivatorUtilities.CreateInstance<SettingsWindow>(App.Services);
            
            var settingsManager = App.Services.GetRequiredService<ISettingsManager>();
            settings.Initialize(_logger, settingsManager);

            settings.Owner = this;
            settings.ShowDialog();
        }

        private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is not TextBox textBox) return;

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
            if (sender is not TextBox textBox) return;

            // Allow navigation, deletion and control keys
            if (e.Key == Key.Back || e.Key == Key.Delete || e.Key == Key.Left ||
                e.Key == Key.Right || e.Key == Key.Tab)
            {
                return;
            }

            // Handle clipboard operations
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.V)
            {
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
            if (sender is not TextBox textBox) return;

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
            if (sender is not TextBox textBox) return;

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

            if (e.NewValue is MeshSummary summary)
            {
                vm.SelectedMeshSummary = summary;
                ScrollSelectedIntoView(summary);
            }
            else
            {
                vm.SelectedMeshSummary = null;
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

        private void OpenGL_MouseDown(object? sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button != System.Windows.Forms.MouseButtons.Left) return;
            if (DataContext is not MainViewModel vm) return;

            // While a transform gizmo is active the state machine owns all mesh interaction.
            // Letting this handler update SelectedMesh would bypass the gizmo guards and
            // reassign _selectedMesh in the active state to whatever the ray happens to hit
            // behind the gizmo arrows or rings.
            if (vm.IsTransformModeActive) return;

            // 1) Build ray in world space
            RayCaster rayCaster = new RayCaster(_controlHost, vm.Camera);

            // 2) Find hit mesh
            Vector3 rayOrigin = vm.Camera.Position;
            Vector3 rayDirection = rayCaster.GetRay();
            var hit = rayCaster.GetClickedMesh(vm.SceneMeshes, rayOrigin, rayDirection);

            // 3) Update selection
            var hitSummary = vm.Meshes.FirstOrDefault(ms => ReferenceEquals(ms.SourceMesh, hit));
            if (hitSummary == null)
            {
                _logger?.Warn("Pick hit a mesh, but no MeshSummary matched it (hitSummary == null).");
                return;
            }
            vm.SelectedMeshSummary = hitSummary;
            ScrollSelectedIntoView(hitSummary);
        }

        private void ScrollSelectedIntoView(MeshSummary? summary)
        {
            if (summary == null) return;

            MeshesTreeView.Dispatcher.BeginInvoke(new Action(() =>
            {
                MeshesTreeView.UpdateLayout();

                if (MeshesTreeView.ItemContainerGenerator.ContainerFromItem(summary) is TreeViewItem item)
                {
                    item.BringIntoView();
                    item.Focus(); // optional, if you want keyboard selection/focus
                }
                else
                {
                    // Fallback: select-trigger + layout usually creates it next pass
                    MeshesTreeView.UpdateLayout();
                    (MeshesTreeView.ItemContainerGenerator.ContainerFromItem(summary) as TreeViewItem)?.BringIntoView();
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

    }

    /// <summary>
    /// Minimal ICommand shim so a lambda can be used with WPF KeyBinding.
    /// </summary>
    internal sealed class RelayCommandAdapter : System.Windows.Input.ICommand
    {
        private readonly Action _execute;
        public RelayCommandAdapter(Action execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}

