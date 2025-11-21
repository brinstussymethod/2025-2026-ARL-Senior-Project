
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace UnBox3D.Views
{
    public partial class HelpWindow : Window
    {
        private readonly IServiceProvider? _services;

        public HelpWindow(IServiceProvider? services)
        {
            InitializeComponent();
            _services = services;
            Loaded += HelpWindow_Loaded;
        }

        private void HelpWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var isDark = (this.Resources["AppTheme"] as string)?.Equals("Dark", StringComparison.OrdinalIgnoreCase) == true;
            if (isDark) ApplyDarkTheme();
        }

        private void ApplyDarkTheme()
        {
            foreach (var panel in FindVisualChildren<System.Windows.Controls.Panel>(this))
                panel.Background = System.Windows.Media.Brushes.Black;

            foreach (var tb in FindVisualChildren<System.Windows.Controls.TextBlock>(this))
                tb.Foreground = System.Windows.Media.Brushes.White;

            foreach (var ctrl in FindVisualChildren<System.Windows.Controls.Control>(this))
            {
                if (ctrl is System.Windows.Controls.Button b)
                {
                    b.Background = System.Windows.Media.Brushes.Black;
                    b.Foreground = System.Windows.Media.Brushes.White;
                    b.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(96,96,96));
                }
                else
                {
                    ctrl.Foreground = System.Windows.Media.Brushes.White;
                }
            }
            this.Background = System.Windows.Media.Brushes.Black;
        }

        private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T typedChild)
                    yield return typedChild;

                foreach (var childOfChild in FindVisualChildren<T>(child))
                    yield return childOfChild;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => this.Close();

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            var main = new MainMenuWindow(_services);
            main.Show();
            this.Close();
        }

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                var main = new MainMenuWindow(_services);
                main.Show();
                this.Close();
            }
        }
    }
}
