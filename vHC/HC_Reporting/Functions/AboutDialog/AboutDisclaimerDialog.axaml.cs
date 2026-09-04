// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using Avalonia.Controls;
using Avalonia.Interactivity;
using VeeamHealthCheck.Resources.Localization;

namespace VeeamHealthCheck.Functions.AboutDialog
{
    public partial class AboutDisclaimerDialog : Window
    {
        public AboutDisclaimerDialog()
        {
            InitializeComponent();

            this.InsHeader.Text = VbrLocalizationHelper.GuiInstHeader;
            this.line1.Text = VbrLocalizationHelper.GuiInstLine1;
            this.line2.Text = VbrLocalizationHelper.GuiInstLine2;
            this.line3.Text = VbrLocalizationHelper.GuiInstLine3;
            this.line4.Text = VbrLocalizationHelper.GuiInstLine4;
            this.line5.Text = VbrLocalizationHelper.GuiInstLine5;
            this.line6.Text = VbrLocalizationHelper.GuiInstLine6;
            this.Cav1Part1.Text = VbrLocalizationHelper.GuiInstCaveat1;
            this.Cav2.Text = VbrLocalizationHelper.GuiInstCaveat2;
            this.Cav3.Text = "*** This tool is community supported and not an officially supported Veeam product.\r\n";
            this.Cav4.Text = "**** The tool does not automatically phone home, or reach out to any network infrastructure beyond the Veeam Backup and Replication components or the Veeam Backup for 365 components if appropriate.";
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
