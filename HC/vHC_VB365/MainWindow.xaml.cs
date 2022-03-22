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
using System.Windows.Navigation;
using System.Windows.Shapes;
using HC_Reporting;

namespace vHC_VB365
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MWindow : Window
    {
        public MWindow()
        {
            InitializeComponent();

            this.Visibility = Visibility.Hidden;
            //this.Close();

        Window w = new HC_Reporting.MainWindow();
            //Page p = new vHC_Common.BaseUI();
            //p.Visibility = Visibility.Visible;

            w.Title = "VB365 Health Check";
            w.Show();

            w.Closing += W_Closing;

        }

        private void W_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.Close();
        }
    }
  
}
