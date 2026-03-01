using System;
using System.IO;
using System.Windows;
using System.Linq;
using IOPath = System.IO.Path;          // avoid clash with Shapes.Path
// no using System.Windows.Forms;

namespace UnBox3D.Views
{
    public partial class OpenExistingWindow : Window
    {
        // nullable because no value until user picks something
        public string? ImportedFilePath { get; private set; }
        private string? _pickedPath;

        public OpenExistingWindow()
        {
            InitializeComponent();
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select project or model",
                DefaultExt = "3D Models",
                Filter =
                    "3D Models (*.obj;*.fbx;*.stl;*.glb;*.gltf)|*.obj;*.fbx;*.stl;*.glb;*.gltf|" +
                    "All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            var defaultDir = IOPath.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "UnBox3D", "Projects");
            if (Directory.Exists(defaultDir))
                dlg.InitialDirectory = defaultDir;

            if (dlg.ShowDialog(this) == true)
            {
                _pickedPath = dlg.FileName;
                PathBox.Text = _pickedPath;
            }
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_pickedPath) || !File.Exists(_pickedPath))
            {
                System.Windows.MessageBox.Show(this, "Please choose a valid file first.",
                                "UnBox3D", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ImportedFilePath = _pickedPath;

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
