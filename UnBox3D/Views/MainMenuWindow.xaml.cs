using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;
using System.Windows.Input;
using Application = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;
using UnBox3D.Rendering.OpenGL;
using UnBox3D.Utils;
using UnBox3D.Theming;

namespace UnBox3D.Views
{
    public partial class MainMenuWindow : Window
    {
        private readonly IServiceProvider _services;

        public static readonly RoutedUICommand StartCommand    = new("Start",    nameof(StartCommand),    typeof(MainMenuWindow),
            new InputGestureCollection { new KeyGesture(Key.Enter) });
        public static readonly RoutedUICommand OpenCommand     = new("Open",     nameof(OpenCommand),     typeof(MainMenuWindow),
            new InputGestureCollection { new KeyGesture(Key.O, ModifierKeys.Alt) });
        public static readonly RoutedUICommand SettingsCommand = new("Settings", nameof(SettingsCommand), typeof(MainMenuWindow),
            new InputGestureCollection { new KeyGesture(Key.S, ModifierKeys.Alt) });
        public static readonly RoutedUICommand HelpCommand     = new("Help",     nameof(HelpCommand),     typeof(MainMenuWindow));
        public static readonly RoutedUICommand ExitCommand     = new("Exit",     nameof(ExitCommand),     typeof(MainMenuWindow));
        // NOTE: Removed ToggleTheme Alt+D binding per request

        public MainMenuWindow(IServiceProvider services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            InitializeComponent();

            Loaded += MainMenuWindow_Loaded;
            CommandBindings.Add(new CommandBinding(StartCommand, Start_Click));
            CommandBindings.Add(new CommandBinding(OpenCommand, Open_Click));
            CommandBindings.Add(new CommandBinding(SettingsCommand, Settings_Click));
            CommandBindings.Add(new CommandBinding(HelpCommand, Help_Click));
            CommandBindings.Add(new CommandBinding(ExitCommand, Exit_Click));
        }

        private void MainMenuWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var theme = _services.GetRequiredService<IThemeManager>();
            DarkModeToggle.IsChecked = theme.Current == AppTheme.Dark;

            // Ensure Enter triggers Start
            BtnStart.Focus();
        }

        private void ToggleTheme_Click(object? sender, RoutedEventArgs e)
        {
            var theme = _services.GetRequiredService<IThemeManager>();
            theme.Toggle(true);
            DarkModeToggle.IsChecked = theme.Current == AppTheme.Dark;
        }

        private void Start_Click(object? sender, RoutedEventArgs e)
        {
            var main = _services.GetRequiredService<MainWindow>();

            var glHost = _services.GetService<IGLControlHost>();
            var logger = _services.GetService<ILogger>();
            var blender = _services.GetService<IBlenderInstaller>();
            if (glHost != null && logger != null && blender != null)
            {
                main.Initialize(glHost, logger, blender);
            }

            Application.Current.MainWindow = main;
            main.Show();
            Close();
        }

        private void Settings_Click(object? sender, RoutedEventArgs e)
        {
            var settings = _services.GetRequiredService<SettingsWindow>();
            // Ensure the window has its dependencies before Loaded fires
            var logger = _services.GetRequiredService<ILogger>();
            var settingsManager = _services.GetRequiredService<ISettingsManager>();
            settings.Initialize(logger, settingsManager);

            settings.Owner = this;
            settings.ShowDialog();
        }

        private void Open_Click(object? sender, RoutedEventArgs e)
        {
            WpfMessageBox.Show("Open existing Project… (wire to your import/open flow).", "UnBox3D");
        }

        private void Help_Click(object? sender, RoutedEventArgs e)
        {
            //WpfMessageBox.Show("Help (coming soon).", "UnBox3D");
            var helpWindow = new HelpWindow(_services);
            helpWindow.Show();
            this.Close();
        }
 
        private void Exit_Click(object? sender, RoutedEventArgs e) => Application.Current.Shutdown();
    }
}
