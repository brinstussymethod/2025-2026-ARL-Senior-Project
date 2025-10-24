using Microsoft.Extensions.DependencyInjection;
using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using UnBox3D.Rendering.OpenGL;
using UnBox3D.Utils;
using UnBox3D.ViewModels;
using Application = System.Windows.Application;

namespace UnBox3D.Views
{
    public partial class MainWindow : Window
    {
        private MainViewModel VM => DataContext as MainViewModel;

        public MainWindow()
        {
            InitializeComponent();

            // Ensure DataContext from DI so ICommand bindings resolve.
            try
            {
                DataContext ??= App.Services.GetRequiredService<MainViewModel>();
            }
            catch
            {
                Loaded += (_, __) =>
                {
                    if (DataContext == null)
                        DataContext = App.Services.GetRequiredService<MainViewModel>();
                };
            }
        }

        // Called from MainMenuWindow after construction
        public void Initialize(IGLControlHost glHost, ILogger logger, IBlenderInstaller blender)
        {
            if (VM == null) return;
            try
            {
                var m = VM.GetType().GetMethod("Initialize");
                if (m != null) m.Invoke(VM, new object[] { glHost, logger, blender });
            }
            catch { }
        }

        // File menu fallbacks
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
            try
            {
                var settingsWindowType = Type.GetType("UnBox3D.Views.SettingsWindow");
                if (settingsWindowType is not null)
                {
                    var settings = App.Services.GetService(settingsWindowType) as Window
                                 ?? Activator.CreateInstance(settingsWindowType) as Window;
                    settings?.Show();
                }
            }
            catch { }
        }

        private static readonly Regex _numericRegex =
            new Regex(@"^[0-9\-\.]+$", RegexOptions.Compiled);

        private void NumericTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !_numericRegex.IsMatch(e.Text);
        }

        private void NumericTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Back ||
                e.Key == System.Windows.Input.Key.Delete ||
                e.Key == System.Windows.Input.Key.Tab ||
                e.Key == System.Windows.Input.Key.Left ||
                e.Key == System.Windows.Input.Key.Right ||
                e.Key == System.Windows.Input.Key.Enter ||
                e.Key == System.Windows.Input.Key.OemMinus ||
                e.Key == System.Windows.Input.Key.Subtract ||
                e.Key == System.Windows.Input.Key.OemPeriod ||
                e.Key == System.Windows.Input.Key.Decimal)
            {
                e.Handled = false;
                return;
            }
        }

        private void NumericTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // Optional validation hook
        }

        private void NumericTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox tb && !double.TryParse(tb.Text, out _))
                tb.Text = string.Empty;
        }

        private void MeshThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (VM != null)
            {
                var prop = VM.GetType().GetProperty("MeshThreshold");
                if (prop != null && prop.CanWrite) prop.SetValue(VM, e.NewValue);
            }
        }
    }
}
