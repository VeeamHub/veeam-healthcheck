using System.Windows;

namespace VeeamHealthCheck.Functions.CredsWindow
{
    public partial class CredentialPromptWindow : Window
    {
        public string Username => UsernameBox.Text;
        public string Password => PasswordBox.Password;

        public CredentialPromptWindow(string host)
        {
            InitializeComponent();
            Title = $"Authentication Required - {host}";
            ServerText.Text = $"Please enter credentials to connect to {host}";
            UsernameBox.Focus();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password))
                DialogResult = true;
            else
                MessageBox.Show("Please enter both username and password.", "Missing Information", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}