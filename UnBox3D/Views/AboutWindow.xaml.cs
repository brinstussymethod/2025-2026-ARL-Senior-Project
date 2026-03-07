using System.Windows;

namespace UnBox3D.Views
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
        }

        private void OK_Click(object sender, RoutedEventArgs e) => Close();
    }
}
