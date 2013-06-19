using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace GoPcBackup
{
    public partial class Help : SavedWindow
    {
        public Help()
        {
            //this.Closing += new CancelEventHandler(this.Window_Closing);

            InitializeComponent();
            base.Init();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.txtHelp.Text = this.oApp.oProfile.sValue("-Help", "");
            this.svText.Focus();
        }

        //private void Window_Closing(object sender, CancelEventArgs e)
        //{
        //    if ( Visibility.Visible == this.Visibility )
        //    {
        //        this.Hide();
        //        e.Cancel = true;
        //    }
        //}

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    this.Close();
                    break;
            }
        }
    }
}
