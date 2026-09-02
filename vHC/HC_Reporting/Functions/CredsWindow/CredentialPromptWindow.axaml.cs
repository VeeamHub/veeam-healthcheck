// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using VeeamHealthCheck.Functions.UserInteraction;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.CredsWindow
{
    public partial class CredentialPromptWindow : Window
    {
        public string Username => UsernameBox.Text;

        public string Password => PasswordBox.Text;

        public CredentialPromptWindow() => InitializeComponent();

        public CredentialPromptWindow(string host) : this()
        {
            // Belt-and-suspenders: silent (unattended) mode must never show a
            // credential dialog. Any caller that bypasses CredsHandler and
            // constructs this window directly while CGlobals.Silent is true
            // is a bug and should fail fast rather than hang the process.
            if (CGlobals.Silent)
            {
                throw new InvalidOperationException(
                    "CredentialPromptWindow must not be invoked in silent mode.");
            }

            this.Title = $"Authentication Required - {host}";
            ServerText.Text = $"Please enter credentials to connect to {host}";
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            UsernameBox.Focus();
        }

        private async void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(this.Username) && !string.IsNullOrWhiteSpace(this.Password))
            {
                Close(true);
            }
            else
            {
                await new AvaloniaUiNotifier().ShowErrorAsync("Please enter both username and password.", "Missing Information");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close(false);
    }
}
