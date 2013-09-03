using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using tvToolbox;

namespace GoPcBackup
{
    /// <summary>
    /// Interaction logic for ScrollingText.xaml
    /// </summary>
    public partial class ScrollingText : SavedWindow
    {
        private double      miOriginalScreenHeight;
        private double      miOriginalScreenWidth;
        private double      miAdjustedWindowHeight;
        private double      miAdjustedWindowWidth;
        private int         miTopLeftOffset = 50;


        private ScrollingText() {}


        /// <summary>
        /// This constructor expects message text and a message caption to be provided.
        /// </summary>
        /// <param name="asMessageText">
        /// The scrolling text message to be displayed.
        /// </param>
        /// <param name="asMessageCaption">
        /// The summary caption for the message text.
        /// </param>
        public ScrollingText(string asMessageText, string asMessageCaption)
                : this(asMessageText, asMessageCaption, Brushes.Tomato, false)
        {}


        /// <summary>
        /// This constructor expects message text and a message caption to be provided
        /// as well as a background color and switch to for preparing block text into
        /// wrappable text.
        /// </summary>
        /// <param name="asMessageText">
        /// The scrolling text message to be displayed.
        /// </param>
        /// <param name="asMessageCaption">
        /// The summary caption for the message text.
        /// </param>
        /// </param>
        /// <param name="aoBackground">
        /// The brush color object to use for the background.
        /// </param>
        /// <param name="abPrepareTextForWrap">
        /// The boolean for preparing block text for text wrapping.
        /// </param>
        public ScrollingText(
                  string asMessageText
                , string asMessageCaption
                , Brush aoBackground
                , bool abPrepareTextForWrap
                )
        {
            InitializeComponent();

            // This loads window ScrollingText defaults from the given profile.
            base.Init();

            this.txtMessageText.Text = !abPrepareTextForWrap ? asMessageText : this.sPrepareTextForWrap(asMessageText);
            this.txtMessageCaption.Text = asMessageCaption;
            this.Title = asMessageCaption;
            this.InnerBorder.Background = aoBackground;
        }


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.AdjustWindowSize();
            miOriginalScreenHeight = SystemParameters.PrimaryScreenHeight;
            miOriginalScreenWidth = SystemParameters.PrimaryScreenWidth;
            this.ShowMe();
        }

        private void mnuExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if ( MouseButton.Left == e.ChangedButton )
                this.DragMove();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch ( e.Key )
            {
                case Key.Escape:
                    this.Close();
                    break;
            }
        }

        private void mnuMaximize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Maximized;
            this.Height = SystemParameters.MaximizedPrimaryScreenHeight;
            this.Width = SystemParameters.MaximizedPrimaryScreenWidth;
        }

        private void mnuRestore_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Normal;
            this.Height = miAdjustedWindowHeight;
            this.Width = miAdjustedWindowWidth;
        }

        private void mnuMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            string lsControlClass = e.OriginalSource.ToString();

            // Disable maximizing while clicking various controls.
            if (       !lsControlClass.Contains("Text")
                    && !lsControlClass.Contains("Bullet")
                    && !lsControlClass.Contains("ClassicBorderDecorator")
                    )
            {
                if ( WindowState.Normal == this.WindowState )
                    this.mnuMaximize_Click(null, null);
                else
                    this.mnuRestore_Click(null, null);
            }
        }

        private void AdjustWindowSize()
        {
            if ( WindowState.Maximized != this.WindowState
                    && (SystemParameters.PrimaryScreenHeight != miOriginalScreenHeight
                    || SystemParameters.MaximizedPrimaryScreenWidth != miOriginalScreenWidth)
                    )
            {
                // Adjust window size to optimize the display depending on screen size.
                // This is done here rather in "Window_Loaded()" in case the screen size
                // changes post startup (eg. via RDP).
                if ( SystemParameters.PrimaryScreenHeight <= 768 )
                {
                    this.mnuMaximize_Click(null, null);
                }
                else if ( SystemParameters.PrimaryScreenHeight <= 864 )
                {
                    this.Height = .90 * SystemParameters.MaximizedPrimaryScreenHeight;
                    this.Width = .90 * SystemParameters.MaximizedPrimaryScreenWidth;
                }
                else if ( SystemParameters.PrimaryScreenHeight <= 885 )
                {
                    this.Height = .80 * SystemParameters.MaximizedPrimaryScreenHeight;
                    this.Width = .80 * SystemParameters.MaximizedPrimaryScreenWidth;
                }
                else
                {
                    this.Height = 675;
                    this.Width = 900;
                }

                miAdjustedWindowHeight = this.Height;
                miAdjustedWindowWidth = this.Width;

                // "this.WindowStartupLocation = WindowStartupLocation.CenterScreen" fails. Who knew?
                // An offset is added to both dimensions so the parent window can be seen underneath.
                this.Top = (SystemParameters.MaximizedPrimaryScreenHeight - this.Height) / 2 + miTopLeftOffset;
                this.Left = (SystemParameters.MaximizedPrimaryScreenWidth - this.Width) / 2  + miTopLeftOffset;
            }
        }

        // This "HideMe() / ShowMe()" kludge is necessary
        // to avoid annoying flicker on some platforms.
        private void HideMe()
        {
            this.MainCanvas.Visibility = Visibility.Hidden;
            this.Hide();
        }

        private void ShowMe()
        {
            this.AdjustWindowSize();
            this.MainCanvas.Visibility = Visibility.Visible;
            System.Windows.Forms.Application.DoEvents();
            this.Show();
            this.scrMessageText.Focus();
            System.Windows.Forms.Application.DoEvents();
        }

        private string sPrepareTextForWrap(string asSourceText)
        {
            StringBuilder   lsbUnwrapText = new StringBuilder(asSourceText);

            lsbUnwrapText.Replace("\r\n\r\n\r\n\r\n", "\u0004");
            lsbUnwrapText.Replace("\r\n\r\n\r\n", "\u0003");
            lsbUnwrapText.Replace("\r\n\r\n", "\u0002");

            lsbUnwrapText.Replace("\r\n    ", "\u0015");
            lsbUnwrapText.Replace("    ", "\u0014");
            lsbUnwrapText.Replace("   ", "\u0013");
            lsbUnwrapText.Replace("  ", "\u0012");

            lsbUnwrapText.Replace("\r\n", " ");
            lsbUnwrapText.Replace("  ", " ");

            lsbUnwrapText.Replace("\u0012", "  ");
            lsbUnwrapText.Replace("\u0013", "   ");
            lsbUnwrapText.Replace("\u0014", "    ");
            lsbUnwrapText.Replace("\u0015", "\r\n    ");

            lsbUnwrapText.Replace("\u0002", "\r\n\r\n");
            lsbUnwrapText.Replace("\u0003", "\r\n\r\n\r\n");
            lsbUnwrapText.Replace("\u0004", "\r\n\r\n\r\n\r\n");

            return lsbUnwrapText.ToString();
        }
    }
}
