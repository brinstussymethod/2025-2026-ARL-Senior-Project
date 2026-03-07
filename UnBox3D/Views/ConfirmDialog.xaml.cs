using System.Windows;

namespace UnBox3D.Views
{
    public partial class ConfirmDialog : Window
    {
        public bool Confirmed { get; private set; } = false;

        public ConfirmDialog(
            string title,
            string message,
            string confirmLabel = "Yes",
            string cancelLabel  = "No")
        {
            InitializeComponent();
            TitleText.Text    = title;
            MessageText.Text  = message;
            ConfirmLabel.Text = confirmLabel;
            CancelLabel.Text  = cancelLabel;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            Close();
        }
    }
}
