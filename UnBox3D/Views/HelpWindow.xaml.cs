using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace UnBox3D.Views
{
    // interaction logic for HelpWindow.xaml
    public partial class HelpWindow : Window
    {
        private readonly IServiceProvider _services;

        // constructor: initialize UI components from xaml and assigns services to local field
        public HelpWindow(IServiceProvider services)
        {
            InitializeComponent();
            _services = services;
        }

        // this function is the logic for close button
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // this function is the logic for the back button which creates a main menu window and displays it
        // then close the pop up help window
        private void Back_Click(object sender, RoutedEventArgs e)
        {
            var mainMenu = new MainMenuWindow(_services);
            mainMenu.Show();
            this.Close();
        }

        // this function overrides the windows default key handling in order to add the 'esc' feature
        // when user presses 'esc' key, it opens the main menu window and closes the help window
        // similar to the back_click function above
        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                var mainMenu = new MainMenuWindow(_services);
                mainMenu.Show();
                this.Close();
            }
        }

    }
}
