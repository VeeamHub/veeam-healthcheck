// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace VeeamHealthCheck.Functions.UserInteraction
{
    public partial class NotifierDialog : Window
    {
        public NotifierDialog() => InitializeComponent();

        public NotifierDialog(string message, string title, bool isConfirm) : this()
        {
            Title = title;
            MessageText.Text = message;
            if (isConfirm)
            {
                NoButton.IsVisible = true;
                YesOkButton.Content = "Yes";
            }
        }

        private void YesOk_Click(object sender, RoutedEventArgs e) => Close(true);

        private void No_Click(object sender, RoutedEventArgs e) => Close(false);
    }
}
