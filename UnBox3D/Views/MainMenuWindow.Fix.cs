using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Windows;
using UnBox3D.Rendering.OpenGL;
using UnBox3D.Utils;
using Application = System.Windows.Application;

namespace UnBox3D.Views
{
    public partial class MainMenuWindow : Window
    {
        public void Initialize(object glHost, object logger, object blenderInstaller) { }

        public void OpenFromPath(string path)
        {
            try
            {
                var main = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault()
                           ?? App.Services.GetRequiredService<MainWindow>();

                var gl = App.Services.GetService<IGLControlHost>();
                var log = App.Services.GetService<ILogger>();
                var blender = App.Services.GetService<IBlenderInstaller>();

                if (gl != null && log != null && blender != null)
                    main.Initialize(gl, log, blender);

                if (!main.IsVisible) main.Show();
                main.Activate();
                main.OpenFromPath(path);
                Close();
            }
            catch { }
        }
    }
}