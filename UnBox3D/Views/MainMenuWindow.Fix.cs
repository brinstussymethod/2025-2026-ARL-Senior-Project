
using System;
using System.Reflection;
using System.Windows;

namespace UnBox3D.Views
{
    // Drop-in partial: provides stubs XAML expects on MainMenuWindow.
    public partial class MainMenuWindow : Window
    {
        // If your XAML wires these names, keep signatures so compilation succeeds.
        public void Initialize(object glHost, object logger, object blenderInstaller)
        {
            // Forward to DataContext.Initialize(...) if present
            var vm = DataContext;
            var mi = vm?.GetType().GetMethod("Initialize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi != null)
            {
                mi.Invoke(vm, new object[] { glHost, logger, blenderInstaller });
            }
        }

        public void OpenFromPath(string path)
        {
            // Open main window and forward the path
            try
            {
                var main = new MainWindow();
                main.Show();
                // If MainWindow exposes OpenFromPath, call it
                var mi = typeof(MainWindow).GetMethod("OpenFromPath", BindingFlags.Instance | BindingFlags.Public);
                mi?.Invoke(main, new object[] { path });
                this.Close();
            }
            catch
            {
                // No-op fallback
            }
        }
    }
}
