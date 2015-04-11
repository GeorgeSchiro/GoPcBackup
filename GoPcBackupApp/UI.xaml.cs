using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Input;
using System.Windows.Threading;
using tvToolbox;

namespace GoPcBackup
{
    public partial class UI : SavedWindow
    {
        private tvProfile       moProfile;
        private DoGoPcBackup    moDoGoPcBackup;

        private const string mcsBackingUpText       = "Backup Running";
        private const string mcsNoPreviousText      = "none";
        private const string mcsStoppedText         = "stopped";
        private const string mcsWaitingText         = "Waiting";
        private const string mcsNotifyIconIdleText  = "GoPC Backup";
        private const string mcsNotifyIconProcText  = "GoPC Backup - busy";

        private double  miOriginalScreenHeight;
        private double  miOriginalScreenWidth;
        private double  miAdjustedWindowHeight;
        private double  miAdjustedWindowWidth;
        private int     miPreviousConfigWizardSelectedIndex  = -1;
        private int     miPreviousConfigDetailsSelectedIndex = -1;
        private int     miProcessedFilesCount;
        private int     miProcessedFilesMaximum;
        private bool    mbBackupRan;
        private bool    mbGetSetDefaultsDone;
        private bool    mbIgnoreCheck;                                  // This is needed to avoid double hits in checkbox events.
        private bool    mbInShowMissingBackupDevices;                   // This prevents recursive calls into "ShowMissingBackupDevices()".
        private bool    mbShowBackupOutputAfterSysTray;                 // Determines if the text output console is displayed after a systray click.
        private bool    mbStartupDone;                                  // Indicates all activities prior to logo animation are completed.
        private bool    mbUpdateSelectedBackupDevices;                  // Indicates when the selected backup devices list can be updated.
                                                                        // (which differs from the usual "GetSet()" "always update" behavior)
        private string  msGetSetConfigurationDefaultsError = null;      // This allows configuration errors to be display asynchronously.

        private Button              moStartStopButtonState = new Button();
        private DateTime            mdtNextStart = DateTime.MinValue;
        private DispatcherTimer     moMainLoopTimer;
        private ExtendedNotifyIcon  moNotifyIcon;
        private tvMessageBox        moNotifyWaitMessage;
        private WindowState         mePreviousWindowState;


        private UI() { }


        /// <summary>
        /// This constructor expects a file backup 
        /// application object to be provided.
        /// </summary>
        /// <param name="aoDoGoPcBackup">
        /// The given file backup object creates dated archives
        /// of file collections from the local file system.
        /// </param>
        public UI(DoGoPcBackup aoDoGoPcBackup)
        {
            InitializeComponent();

            // This loads window UI defaults from the given profile.
            base.Init();

            moProfile = aoDoGoPcBackup.oProfile;
            moDoGoPcBackup = aoDoGoPcBackup;
        }


        // This lets us handle windows messages.
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            HwndSource  loSource = PresentationSource.FromVisual(this) as HwndSource;
                        loSource.AddHook(WndProc);
        }

        [DllImport("user32")]
        public static extern int    RegisterWindowMessage(string message);
        public static readonly int  WM_SHOWME = RegisterWindowMessage("WM_SHOWME_GoPcBackup");
        [DllImport("user32")]
        public static extern bool   PostMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);
        public static readonly int  HWND_BROADCAST = 0xffff;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // This handles the "WM_SHOWME" message so another
            // instance can display this one before exiting.
            if( WM_SHOWME == msg )
            {
                this.ShowMe();

                if ( null != moNotifyWaitMessage )
                {
                    // Wait for the UI to redisplay.
                    while ( Visibility.Visible != this.Visibility )
                    {
                        System.Windows.Forms.Application.DoEvents();
                        System.Threading.Thread.Sleep(moProfile.iValue("-MainLoopSleepMS", 200));
                    }

                    moNotifyWaitMessage.Close();
                    moNotifyWaitMessage = null;
                }
            }

            return IntPtr.Zero;
        }


        /// <summary>
        /// This is used with "HideMe() / ShowMe()"
        /// to track the visible state of this window.
        /// </summary>
        public bool bVisible
        {
            get
            {
                return mbVisible;
            }
            set
            {
                mbVisible = value;
            }
        }
        private bool mbVisible = false;

        /// <summary>
        /// This will stop the main timer loop as well
        /// as whatever process is currently running.
        /// </summary>
        public bool bMainLoopStopped
        {
            get
            {
                return mbMainLoopStopped;
            }
            set
            {
                mbMainLoopStopped = value;

                moDoGoPcBackup.bMainLoopStopped = mbMainLoopStopped;

                if ( mbMainLoopStopped )
                {
                    if ( null != moMainLoopTimer )
                        moMainLoopTimer.Stop();

                    this.PopulateTimerDisplay(mcsStoppedText);
                }
            }
        }
        private bool mbMainLoopStopped = true;

        /// <summary>
        /// This will restart the main timer loop
        /// (eg. after configuration changes) so 
        /// that the timer panel is refreshed.
        /// </summary>
        public bool bMainLoopRestart
        {
            get
            {
                return mbMainLoopRestart;
            }
            set
            {
                mbMainLoopRestart = value;
            }
        }
        private bool mbMainLoopRestart = true;

        /// <summary>
        /// This indicates when the cleanup
        /// / backup process is running.
        /// </summary>
        public bool bBackupRunning
        {
            get
            {
                return mbBackupRunning;
            }
            set
            {
                mbBackupRunning = value;

                if ( !mbBackupRunning )
                {
                    if ( (bool)this.chkUseTimer.IsChecked )
                        this.PopulateTimerDisplay(mcsWaitingText);
                    else
                        this.PopulateTimerDisplay(mcsStoppedText);

                    this.CreateSysTrayIcon();
                    moNotifyIcon.targetNotifyIcon.Text = mcsNotifyIconIdleText;
                    this.EnableButtons();
                }
                else
                {
                    moDoGoPcBackup.bMainLoopStopped = false;
                    mbShowBackupOutputAfterSysTray = true;

                    this.DisableButtons();
                    this.HideMiddlePanels();
                    this.MiddlePanelOutputText.Visibility = Visibility.Visible;

                    this.PopulateTimerDisplay(mcsBackingUpText);
                    this.CreateSysTrayIcon();
                    moNotifyIcon.targetNotifyIcon.Text = mcsNotifyIconProcText;

                    // Indicate that the backup has run at least once.
                    mbBackupRan = true;
                }
            }
        }
        private bool mbBackupRunning;

        /// <summary>
        /// This contains all child windows
        /// opened by the main parent window
        /// (ie. by this window).
        /// </summary>
        public List<ScrollingText> oOtherWindows
        {
            get
            {
                return moOtherWindows;
            }
            set
            {
                moOtherWindows = value;
            }
        }
        private List<ScrollingText> moOtherWindows = new List<ScrollingText>();


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.AdjustWindowSize();
            miOriginalScreenHeight = SystemParameters.PrimaryScreenHeight;
            miOriginalScreenWidth = SystemParameters.PrimaryScreenWidth;

            // This window is hidden by default. Only make it initially
            // visible if all setup steps have not yet been completed.
            // Otherwise it is displayed via its system tray icon.
            if ( !moProfile.bValue("-AllConfigWizardStepsCompleted", false) )
                this.ShowMe();

            // Only animate until the initial setup has been completed.
            int liDurationSecs = moProfile.bValue("-AllConfigWizardStepsCompleted", false) ? 0 : 1;

            DoubleAnimation loAnimation = null;
            loAnimation = new DoubleAnimation(0, this.TopPanel.Height, new Duration(TimeSpan.FromSeconds(liDurationSecs)));
            this.LogoImage.BeginAnimation(Image.HeightProperty, loAnimation);
            loAnimation = new DoubleAnimation(0, this.TopPanel.Width, new Duration(TimeSpan.FromSeconds(liDurationSecs)));
            this.LogoImage.BeginAnimation(Image.WidthProperty, loAnimation);
            loAnimation = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(liDurationSecs)));
            Storyboard.SetTargetProperty(loAnimation, new PropertyPath(Image.OpacityProperty));
            Storyboard loStoryboard = new Storyboard();
            loStoryboard.Children.Add(loAnimation);
            loStoryboard.Completed += new EventHandler(LogoImageAnimation_Completed);
            loStoryboard.Begin(this.LogoImage);

            // "this.chkUseTimer_SetChecked() is intentionally not used here.
            // We want the stored check value displayed with no side effects.
            this.chkUseTimer.IsChecked = moProfile.bValue("-AutoStart", true);

            // Turns off the "loading" message.
            moProfile.bAppFullyLoaded = true;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // Save any setup changes.
            this.GetSetConfigurationDefaults();

            // Do a full exit if shutting down.
            if ( ShutdownMode.OnExplicitShutdown == Application.Current.ShutdownMode )
            {
                this.bMainLoopStopped = true;

                // Always turn the timer back on during shutdowns. That
                // way backups should automatically retart after reboot.
                this.chkUseTimer_SetChecked(true);
            }
            else
            {
                if ( !(bool)chkCloseAndExit.IsChecked
                        && (bool)this.chkUseTimer.IsChecked
                        && moProfile.bValue("-AllConfigWizardStepsCompleted", false)
                        )
                {
                    // Minimize to the system tray if "full exit" is not
                    // checked, the loop timer is on and the setup is done.
                    this.HideMe();
                    e.Cancel = true;
                }
                else
                    if ( !(bool)this.chkUseTimer.IsChecked
                            && tvMessageBoxResults.No == tvMessageBox.Show(
                                      this
                                    , "Are you sure you want to exit with the timer stopped? (ie. with the backup not running in the background)"
                                    , "Timer Stopped"
                                    , tvMessageBoxButtons.YesNo
                                    , tvMessageBoxIcons.Question
                                    , tvMessageBoxCheckBoxTypes.DontAsk
                                    , moProfile
                                    , "-TimerStopped"
                                    , tvMessageBoxResults.Yes
                                    )
                                )
                        e.Cancel = true;
                // If the timer is off, ask about it before exiting.
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            this.bMainLoopStopped = true;

            if ( null != moNotifyIcon )
                moNotifyIcon.Dispose();

            if ( 0 != moOtherWindows.Count )
                foreach (ScrollingText loWindow in moOtherWindows)
                    loWindow.Close();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            // This kludge is needed since attempting to restore a window that is busy
            // processing (after a system minimize) will typically not be responsive.
            if ( this.bBackupRunning && WindowState.Minimized == this.WindowState )
                this.HideMe();
        }

        // This method is called before anything else (after init & load events).
        // Most of the UI initialization code goes here.

        private void LogoImageAnimation_Completed(object sender, EventArgs e)
        {
            mbStartupDone = true;

            if ( !moProfile.bValue("-AllConfigWizardStepsCompleted", false) )
            {
                if ( moProfile.bValue("-LicenseAccepted", false) )
                {
                    this.ShowWizard();
                }
                else
                {
                    const string lsLicenseCaption = "MIT License";
                    const string lsLicensePathFile = "MIT License.txt";

                    // Fetch license.
                    tvFetchResource.ToDisk(Application.ResourceAssembly.GetName().Name
                            , lsLicensePathFile, null);

                    tvMessageBox.ShowBriefly(this, string.Format("The \"{0}\" will now be displayed."
                                    + "\r\n\r\nPlease accept it if you would like to use this software."
                            , lsLicenseCaption), lsLicenseCaption, tvMessageBoxIcons.Information, 3);

                    ScrollingText   loLicense = new ScrollingText(moDoGoPcBackup.sFileAsStream(
                                                          moProfile.sRelativeToProfilePathFile(lsLicensePathFile))
                                                        , lsLicenseCaption, true);
                                    loLicense.TextBackground = Brushes.LightYellow;
                                    loLicense.OkButtonText = "Accept";
                                    loLicense.bDefaultButtonDisabled = true;
                                    loLicense.ShowDialog();

                    if ( loLicense.bOkButtonClicked )
                    {
                        moProfile["-LicenseAccepted"] = true;
                        moProfile.Save();

                        this.ShowWizard();
                    }
                }
            }            
            else
            {
                // Display all of the main application elements needed
                // after the configuration wizard has been completed.
                this.HideMiddlePanels();
                this.MainButtonPanel.IsEnabled = true;
                this.GetSetOutputTextPanelErrorCache();
                this.PopulateTimerDisplay(mcsStoppedText);

                bool lbPreviousBackupError = this.ShowPreviousBackupStatus();

                // No timer checked or a previous backup error means show the window
                // immediately. Otherwise, it will be accessible via the system tray.
                if ( !(bool)this.chkUseTimer.IsChecked || lbPreviousBackupError )
                {
                    this.ShowMe();
                }
                else
                {
                    this.HideMe();
                    this.ShowMissingBackupDevices();
                }

                this.CreateSysTrayIcon();

                // Since error output is cached (see "this.GetSetOutputTextPanelErrorCache()"),
                // the cached error output text should be displayed right away.
                if ( moProfile.ContainsKey("-PreviousBackupOk") && !moProfile.bValue("-PreviousBackupOk", false) )
                    this.ShowOutputText();

                // If the timer is checked, start the main loop.
                if ( (bool)this.chkUseTimer.IsChecked )
                {
                    // Don't bother displaying the timer if there was a previous backup error.
                    // FYI, "ContainsKey" is used since existence checking is done on this key
                    // elsewhere (yeah I know, so much for black boxes).
                    if ( !moProfile.ContainsKey("-PreviousBackupOk") || moProfile.bValue("-PreviousBackupOk", false) )
                        this.MiddlePanelTimer.Visibility = Visibility.Visible;

                    this.MainLoop();
                }
            }
        }

        // Buttons that don't launch external processes are toggles.

        private void btnDoBackupNow_Click(object sender, RoutedEventArgs e)
        {
            if ( this.bBackupRunning )
            {
                // Stop the backup but leave the timer running.
                moDoGoPcBackup.bMainLoopStopped = true;
                this.bBackupRunning = false;
            }
            else
            {
                this.HideMiddlePanels();
                this.GetSetConfigurationDefaults();

                if ( this.bValidateConfiguration() )
                    this.DoBackup();
                else
                    this.btnSetup_Click(null, null);
            }
        }

        private void btnSetup_Click(object sender, RoutedEventArgs e)
        {
            if ( Visibility.Visible == this.MiddlePanelConfigWizard.Visibility )
            {
                this.HideMiddlePanels();
                this.ShowBackupRunning();
            }
            else
            {
                this.HideMiddlePanels();
                this.ShowWizard();

                // Reset setup backup device checkboxes.
                gridBackupDevices.Children.Clear();
                this.ConfigWizardTabs.SelectedIndex = 0;
            }
        }

        private void btnNextSetupStep_Click(object sender, RoutedEventArgs e)
        {
            this.ConfigWizardTabs.SelectedIndex++;
        }

        private void btnConfigureDetails_Click(object sender, RoutedEventArgs e)
        {
            if ( Visibility.Visible == this.MiddlePanelConfigDetails.Visibility )
            {
                this.HideMiddlePanels();
                this.ShowBackupRunning();
            }
            else
            {
                this.HideMiddlePanels();
                this.MiddlePanelConfigDetails.Visibility = Visibility.Visible;
            }
        }

        private void btnShowTimer_Click(object sender, RoutedEventArgs e)
        {
            if ( Visibility.Visible == this.MiddlePanelTimer.Visibility )
            {
                this.HideMiddlePanels();
                this.ShowBackupRunning(true);
            }
            else
            {
                this.GetSetConfigurationDefaults();
                this.HideMiddlePanels();
                this.MiddlePanelTimer.Visibility = Visibility.Visible;

                // Clicking the "show timer" button when the backup
                // is no longer running implies that the timer panel
                // is also what we want to see after a systray click.
                // This is true provided the previous backup was "OK".

                if ( !this.bBackupRunning && !moProfile.ContainsKey("-PreviousBackupOk") )
                    mbShowBackupOutputAfterSysTray = false;
                else
                if ( !this.bBackupRunning && moProfile.bValue("-PreviousBackupOk", false) )
                    mbShowBackupOutputAfterSysTray = false;
            }
        }

        private void btnShowArchive_Click(object sender, RoutedEventArgs e)
        {
            this.ShowBackupRunning();

            Process.Start(moProfile.sValue("-WindowsExplorer"
                    , "explorer.exe"), moDoGoPcBackup.sArchivePath());
        }

        private void btnShowBackupLogs_Click(object sender, RoutedEventArgs e)
        {
            this.ShowBackupRunning();

            Process.Start(moProfile.sValue("-WindowsExplorer"
                    , "explorer.exe"), Path.GetDirectoryName(moProfile.sLoadedPathFile));
        }

        private void btnShowHelp_Click(object sender, RoutedEventArgs e)
        {
            this.ShowHelp();
        }

        private void chkUseTimer_Click(object sender, RoutedEventArgs e)
        {
            this.chkUseTimer_SetChecked((bool)this.chkUseTimer.IsChecked);
        }

        // This kludge is necessary becuase use of the "Checked" and "Unchecked"
        // event handlers oddly disable rendering of the check in the box.
        private void chkUseTimer_SetChecked(bool abValue)
        {
            this.chkUseTimer.IsChecked = abValue;

            moProfile["-AutoStart"] = this.chkUseTimer.IsChecked;
            moProfile.Save();

            if ( !(bool)this.chkUseTimer.IsChecked )
            {
                this.bMainLoopStopped = true;
                this.MiddlePanelTimer.Visibility = Visibility.Collapsed;
            }
            else
            {
                this.GetSetConfigurationDefaults();
                this.HideMiddlePanels();
                this.MiddlePanelTimer.Visibility = Visibility.Visible;
                this.MainLoop();
            }
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
                case Key.F1:
                    this.ShowHelp();
                    break;
            }
        }

        private void mnuMaximize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Maximized;
            this.Height = SystemParameters.MaximizedPrimaryScreenHeight;
            this.Width = SystemParameters.MaximizedPrimaryScreenWidth;

            mePreviousWindowState = WindowState.Maximized;
        }

        private void mnuRestore_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Normal;
            this.Height = miAdjustedWindowHeight;
            this.Width = miAdjustedWindowWidth;

            mePreviousWindowState = WindowState.Normal;
        }

        private void mnuMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            string lsControlClass = e.OriginalSource.ToString();

            // Disable maximizing while clicking various controls.
            if (       !lsControlClass.Contains("Bullet")
                    && !lsControlClass.Contains("Button")
                    && !lsControlClass.Contains("ClassicBorderDecorator")
                    && !lsControlClass.Contains("Run")
                    && !lsControlClass.Contains("Scroll")
                    && !lsControlClass.Contains("Text")
                    )
            {
                if ( WindowState.Normal == this.WindowState )
                    this.mnuMaximize_Click(null, null);
                else
                    this.mnuRestore_Click(null, null);
            }
        }

        public void AppendOutputTextLine(string asTextLine)
        {
            this.BackupProcessOutput.Inlines.Add(asTextLine + Environment.NewLine);
            if ( this.BackupProcessOutput.Inlines.Count > 50 )
                this.BackupProcessOutput.Inlines.Remove(this.BackupProcessOutput.Inlines.FirstInline);
            this.scrBackupProcessOutput.ScrollToEnd();

            this.IncrementProgressBar(false);
        }

        public void InitProgressBar(int aiProcessedFilesMaximum)
        {
            miProcessedFilesCount = 0;
            miProcessedFilesMaximum = aiProcessedFilesMaximum;
            this.prbBackupProgress.Value = 0;
            this.prbBackupProgress.Minimum = 0;
            this.prbBackupProgress.Maximum = 100;
        }

        public void IncrementProgressBar(bool abFill)
        {
            if ( !abFill )
                this.prbBackupProgress.Value = 0 == miProcessedFilesMaximum ? 0
                        : this.prbBackupProgress.Maximum
                                * ((double)++miProcessedFilesCount / miProcessedFilesMaximum);
            else
                if ( 0 == miProcessedFilesMaximum )
                {
                    this.prbBackupProgress.Value = 0;
                }
                else
                    // This kludge is necessary since simply setting
                    // "this.prbBackupProgress.Value = this.prbBackupProgress.Maximum" fails.
                    do
	                {
                	    this.prbBackupProgress.Value = this.prbBackupProgress.Maximum
                                * ((double)++miProcessedFilesCount / miProcessedFilesMaximum);
	                }
                    while ( miProcessedFilesCount < miProcessedFilesMaximum );
        }

        private void btnSetupStep1_Click(object sender, RoutedEventArgs e)
        {
            // Currently the setup UI only handles 1 backup set (ie. the primary backup).
            tvProfile loBackupSet1Profile = new tvProfile(moProfile.sValue("-BackupSet", "(not set)"));
            if ( loBackupSet1Profile.oOneKeyProfile("-FolderToBackup").Count > 1 )
            {
                tvMessageBox.ShowWarning(this, string.Format("The profile file (\"{0}\") has been edited manually to contain more than one folder to backup. Please remove the excess or continue to edit the profile by hand."
                        , Path.GetFileName(moProfile.sLoadedPathFile)), "Can't Change Folder to Backup");
                return;
            }

            System.Windows.Forms.FolderBrowserDialog loOpenDialog = new System.Windows.Forms.FolderBrowserDialog();
            loOpenDialog.RootFolder = Environment.SpecialFolder.Desktop;
            loOpenDialog.SelectedPath = this.FolderToBackup.Text;

            System.Windows.Forms.DialogResult leDialogResult = loOpenDialog.ShowDialog();

            if ( System.Windows.Forms.DialogResult.OK == leDialogResult )
                this.FolderToBackup.Text = loOpenDialog.SelectedPath;
        }

        private void btnSetupStep2_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog loOpenDialog = new System.Windows.Forms.FolderBrowserDialog();
            loOpenDialog.RootFolder = Environment.SpecialFolder.Desktop;
            loOpenDialog.SelectedPath = this.ArchivePath.Text;

            System.Windows.Forms.DialogResult leDialogResult = loOpenDialog.ShowDialog();

            if ( System.Windows.Forms.DialogResult.OK == leDialogResult )
                this.ArchivePath.Text = loOpenDialog.SelectedPath;
        }

        private void UseVirtualMachineHostArchive_Checked(object sender, RoutedEventArgs e)
        {
            if ( mbIgnoreCheck )
            {
                mbIgnoreCheck = false;
                return;
            }

            if ( mbStartupDone && !this.bShowInitScriptsWarning() )
            {
                mbIgnoreCheck = true;
                (e.OriginalSource as CheckBox).IsChecked = false;
                return;
            }

            this.VirtualMachineHostGrid.Visibility = Visibility.Visible;
            this.UseConnectVirtualMachineHost.Visibility = Visibility.Visible;
        }

        private void UseVirtualMachineHostArchive_Unchecked(object sender, RoutedEventArgs e)
        {
            if ( mbIgnoreCheck )
            {
                mbIgnoreCheck = false;
                return;
            }

            if ( mbStartupDone && !this.bShowInitScriptsWarning() )
            {
                mbIgnoreCheck = true;
                (e.OriginalSource as CheckBox).IsChecked = true;
                return;
            }

            this.VirtualMachineHostGrid.Visibility = Visibility.Hidden;
            this.UseConnectVirtualMachineHost.IsChecked = false;
            this.UseConnectVirtualMachineHost.Visibility = Visibility.Hidden;
        }

        private void UseConnectVirtualMachineHost_Checked(object sender, RoutedEventArgs e)
        {
            if ( mbIgnoreCheck )
            {
                mbIgnoreCheck = false;
                return;
            }

            if ( mbStartupDone && !this.bShowInitBeginScriptWarning() )
            {
                mbIgnoreCheck = true;
                (e.OriginalSource as CheckBox).IsChecked = false;
                return;
            }

            this.ConnectVirtualMachineHostGrid.Visibility = Visibility.Visible;
        }

        private void UseConnectVirtualMachineHost_Unchecked(object sender, RoutedEventArgs e)
        {
            if ( mbIgnoreCheck )
            {
                mbIgnoreCheck = false;
                return;
            }

            if ( mbStartupDone && !this.bShowInitBeginScriptWarning() )
            {
                mbIgnoreCheck = true;
                (e.OriginalSource as CheckBox).IsChecked = true;
                return;
            }

            this.ConnectVirtualMachineHostGrid.Visibility = Visibility.Hidden;
        }

        private void btnSetupStep3_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog loOpenDialog = new System.Windows.Forms.FolderBrowserDialog();
            loOpenDialog.RootFolder = Environment.SpecialFolder.Desktop;
            loOpenDialog.SelectedPath = this.VirtualMachineHostArchivePath.Text;

            System.Windows.Forms.DialogResult leDialogResult = loOpenDialog.ShowDialog();

            if ( System.Windows.Forms.DialogResult.OK == leDialogResult )
                this.VirtualMachineHostArchivePath.Text = loOpenDialog.SelectedPath;
        }

        private void moNotifyIcon_OnHideWindow()
        {
        }

        private void moNotifyIcon_OnShowWindow()
        {
            if ( this.bVisible )
            {
                this.HideMe();
            }
            else
            {
                if ( this.bBackupRunning || mbShowBackupOutputAfterSysTray )
                {
                    if (Visibility.Visible != this.MiddlePanelOutputText.Visibility)
                    {
                        this.HideMiddlePanels();
                        this.ShowOutputText();
                    }
                }
                else
                {
                    if ( Visibility.Visible != this.MiddlePanelTimer.Visibility )
                    {
                        this.HideMiddlePanels();
                        this.MiddlePanelTimer.Visibility = Visibility.Visible;
                    }
                }

                if ( !this.bBackupRunning )
                {
                    this.ShowMe();
                }
                else
                {
                    if ( null == moNotifyWaitMessage )
                        moNotifyWaitMessage = new tvMessageBox();

                    moNotifyWaitMessage.ShowWait(this, mcsNotifyIconProcText + " - please wait ...", 350);

                    // This kludge is necessary to overcome sometimes severe
                    // delays processing click events to redisplay the main 
                    // window during heavy backup or cleanup processing.
                    PostMessage((IntPtr)HWND_BROADCAST, WM_SHOWME, IntPtr.Zero, IntPtr.Zero);
                }
            }
        }

        // This "HideMe() / ShowMe()" kludge is necessary
        // to avoid annoying flicker on some platforms.
        private void HideMe()
        {
            this.MainCanvas.Visibility = Visibility.Hidden;
            this.WindowState = mePreviousWindowState;
            this.Hide();
            moNotifyIcon_OnHideWindow();
            this.bVisible = false;
        }

        private void ShowMe()
        {
            bool lTopmost = this.Topmost;

            this.AdjustWindowSize();
            this.MainCanvas.Visibility = Visibility.Visible;
            this.WindowState = mePreviousWindowState;
            this.Topmost = true;
            System.Windows.Forms.Application.DoEvents();
            this.Show();
            this.bVisible = true;
            System.Windows.Forms.Application.DoEvents();
            this.Topmost = lTopmost;
            System.Windows.Forms.Application.DoEvents();

            this.ShowMissingBackupDevices();
        }

        private bool bShowInitBeginScriptWarning()
        {
            bool lbShowInitBeginScriptWarning = true;

            // Don't bother asking if the "backup begin" script
            // doesn't exist or the "init" switch has been set already.
            if ( moProfile.ContainsKey("-BackupBeginScriptPathFile")
                    && !moProfile.bValue("-BackupBeginScriptInit", false) )
            {
                lbShowInitBeginScriptWarning = tvMessageBoxResults.OK == tvMessageBox.Show(this
                        , "Are you sure you want reinitialize the \"backup begin\" script to its default state?"
                        , "Reinitialize Begin Script", tvMessageBoxButtons.OKCancel, tvMessageBoxIcons.Exclamation
                        , tvMessageBoxCheckBoxTypes.DontAsk, moProfile, "-InitBeginScript", tvMessageBoxResults.OK
                        );

                if ( lbShowInitBeginScriptWarning )
                {
                    moProfile["-BackupBeginScriptInit"] = true;
                    moProfile.Save();
                }
            }

            return lbShowInitBeginScriptWarning;
        }

        private bool bShowInitScriptsWarning()
        {
            bool lbShowInitScriptsWarning = true;

            // Don't bother asking if neither script exists
            // or both "init" switches have been set already.
            if ( (moProfile.ContainsKey("-BackupBeginScriptPathFile")
                    && !moProfile.bValue("-BackupBeginScriptInit", false))
                    || (moProfile.ContainsKey("-BackupDoneScriptPathFile")
                        && !moProfile.bValue("-BackupDoneScriptInit", false))
                    )
            {
                lbShowInitScriptsWarning = tvMessageBoxResults.OK == tvMessageBox.Show(this
                        , "Are you sure you want reinitialize both backup scripts to their default states?"
                        , "Reinitialize Both Scripts", tvMessageBoxButtons.OKCancel, tvMessageBoxIcons.Exclamation
                        , tvMessageBoxCheckBoxTypes.DontAsk, moProfile, "-InitBothScripts", tvMessageBoxResults.OK
                        );

                if ( lbShowInitScriptsWarning )
                {
                    moProfile["-BackupBeginScriptInit"] = true;
                    moProfile["-BackupDoneScriptInit"] = true;
                    moProfile.Save();
                }
            }

            return lbShowInitScriptsWarning;
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
                this.Top = (SystemParameters.MaximizedPrimaryScreenHeight - this.Height) / 2;
                this.Left = (SystemParameters.MaximizedPrimaryScreenWidth - this.Width) / 2;
            }
        }

        private void sldBackupTime_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.sldBackupTime.Maximum = 1440 / moProfile.iValue("-BackupTimeMinsPerTick", 15) - 1;

            int lciAmPmTicks = ((int)this.sldBackupTime.Maximum + 1) / 2;

            bool lbIsAM = this.sldBackupTime.Value / lciAmPmTicks < 1;
            double ldTimeValue = moProfile.iValue("-BackupTimeMinsPerTick", 15)
                                        * (this.sldBackupTime.Value - lciAmPmTicks * (lbIsAM ? 0 : 1));

            int liHour = (int)(ldTimeValue / 60);
            int liMin = (int)Math.Round(60 * (ldTimeValue / 60 - liHour));

            string lsHour = 0 == liHour ? "12" : liHour.ToString();
            string lsMin = liMin.ToString("00");

            this.BackupTime.Text = lsHour + ":" + lsMin + " " + (lbIsAM ? "AM" : "PM");
        }

        private void sldBackupTime_ValueFromString(string asTimeOnly)
        {
            TimeSpan loTimeSpan = DateTime.Parse(asTimeOnly) - DateTime.Today;
            this.sldBackupTime.Value = loTimeSpan.TotalMinutes / moProfile.iValue("-BackupTimeMinsPerTick", 15);
        }

        private void GetSetConfigurationDefaults()
        {
            tvProfile loBackupSet1Profile = new tvProfile(moProfile.sValue("-BackupSet", "(not set)"));

            if ( !mbGetSetDefaultsDone )
            {
                try
                {
                    // General
                    this.CleanupFiles.IsChecked = moProfile.bValue("-CleanupFiles", true);
                    this.BackupFiles.IsChecked = moProfile.bValue("-BackupFiles", true);
                    this.BackupBeginScriptEnabled.IsChecked = moProfile.bValue("-BackupBeginScriptEnabled", true);
                    this.BackupDoneScriptEnabled.IsChecked = moProfile.bValue("-BackupDoneScriptEnabled", true);


                    // Step 1
                    this.FolderToBackup.Text = loBackupSet1Profile.sValue("-FolderToBackup",
                            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));


                    // Step 2
                    this.BackupOutputFilename.Text = loBackupSet1Profile.sValue("-OutputFilename",
                            string.Format("{0}Files", Environment.GetEnvironmentVariable("USERNAME")));

                    this.ArchivePath.Text = moDoGoPcBackup.sArchivePath();

                    this.UseVirtualMachineHostArchive.IsChecked = moProfile.bValue("-UseVirtualMachineHostArchive", false);
                    this.VirtualMachineHostArchivePath.Text = moProfile.sValue("-VirtualMachineHostArchivePath", "");
                    this.UseConnectVirtualMachineHost.IsChecked = moProfile.bValue("-UseConnectVirtualMachineHost", false);
                    this.VirtualMachineHostUsername.Text = moProfile.sValue("-VirtualMachineHostUsername", "");
                    this.VirtualMachineHostPassword.Text = moProfile.sValue("-VirtualMachineHostPassword", "");


                    // Step 4
                    this.BackupTime.Text = moProfile.sValue("-BackupTime", "12:00 AM");
                    this.sldBackupTime_ValueFromString(this.BackupTime.Text);
                }
                catch (Exception ex)
                {
                    msGetSetConfigurationDefaultsError = ex.Message;
                }

                mbGetSetDefaultsDone = true;
            }


            // Step 3

            // If the user merely looks at the backup devices tab, update the profile.
            if ( this.ConfigWizardTabs.SelectedIndex
                    == ItemsControl.ItemsControlFromItemContainer(
                    this.tabStep3).ItemContainerGenerator.IndexFromContainer(this.tabStep3)
                    )
                mbUpdateSelectedBackupDevices = true;

            // The removal or insertion of external devices will be
            // detected whenever the "Setup Wizard" button is clicked.

            if ( 0 == gridBackupDevices.Children.Count )
            {   
                string  lsFirstDriveLetter = moDoGoPcBackup.cPossibleDriveLetterBegin.ToString();
                int     liRow = 0;
                int     liColumn = 0;

                // Add each drive (starting with lsFirstDriveLetter) to the list of checkboxes.
                foreach (DriveInfo loDrive in DriveInfo.GetDrives())
                {
                    try
                    {
                        if ( String.Compare(lsFirstDriveLetter, loDrive.Name) < 0 )
                        {
                            CheckBox    loCheckBox = new CheckBox();
                                        loCheckBox.Width = 200;
                                        loCheckBox.Tag = loDrive;

                            // If the drive has a valid volume label, display it alongside the drive name.
                            try
                            {
                                loCheckBox.Content = "(" + loDrive.Name.Substring(0, 2) + ") " + loDrive.VolumeLabel;
                            }
                            // Otherwise, display the drive name by itself.
                            catch
                            {
                                loCheckBox.Content = "(" + loDrive.Name.Substring(0, 2) + ") ";
                            }

                            // Add a CheckBox to the tab to represent the drive.
                            gridBackupDevices.Children.Add(loCheckBox);
                            Grid.SetRow(loCheckBox, liRow);
                            Grid.SetColumn(loCheckBox, liColumn);

                            // Arrange the CheckBoxes such that a new column is formed for every 8 CheckBoxes.
                            if ( liRow < 7 )
                            {
                                ++liRow;
                            }
                            else
                            {
                                liRow = 0;
                                ++liColumn;
                            }
                        
                            string lsTokenPathFile = Path.Combine((loCheckBox.Tag as DriveInfo).Name, moDoGoPcBackup.sBackupDriveToken);

                            try
                            {
                                File.Create(lsTokenPathFile + ".test").Close();
                                File.Delete(lsTokenPathFile + ".test");
                                loCheckBox.Foreground = Brushes.DarkGreen;
                            }
                            catch
                            {
                                loCheckBox.Foreground = Brushes.Red;
                                loCheckBox.IsEnabled = false;
                            }

                            // If the BackupDriveToken is already on a drive, set the drive's CheckBox to 'checked.'
                            if ( File.Exists(lsTokenPathFile) )
                                loCheckBox.IsChecked = true;

                            // Create or delete the BackupDriveToken from the drive whenever it is checked or unchecked.
                            loCheckBox.Checked += new RoutedEventHandler(BackupDeviceCheckboxStateChanged);
                            loCheckBox.Unchecked += new RoutedEventHandler(BackupDeviceCheckboxStateChanged);
                        }
                    }
                    catch {}
                }
            }


            // Finish
            this.ReviewFolderToBackup.Text = this.FolderToBackup.Text;
            this.ReviewOutputFilename.Text = this.BackupOutputFilename.Text;
            this.ReviewArchivePath.Text = this.ArchivePath.Text;
            this.ReviewBackupTime.Text = this.BackupTime.Text;

            tvProfile loSelectedBackupDevices = new tvProfile();
            string lsSelectedDrives = "";

            // Generate a string with the content of each CheckBox that the user checked in Step 4.
            foreach (CheckBox loCheckBox in gridBackupDevices.Children)
            {
                if ((bool)loCheckBox.IsChecked)
                {
                    loSelectedBackupDevices.Add("-Device", loSelectedBackupDevices.sSwapHyphens(loCheckBox.Content.ToString()));
                    lsSelectedDrives += loCheckBox.Content.ToString().Substring(0, 5);
                }
            }

            this.ReviewAdditionalDevices.Text = lsSelectedDrives;

            loBackupSet1Profile["-FolderToBackup"] = this.ReviewFolderToBackup.Text;
            loBackupSet1Profile["-OutputFilename"] = this.ReviewOutputFilename.Text;

            if ( null == msGetSetConfigurationDefaultsError )
            {
                moProfile["-BackupSet"] = loBackupSet1Profile.sCommandBlock();
                moProfile["-ArchivePath"] = this.ReviewArchivePath.Text;
                moProfile["-BackupTime"] = this.ReviewBackupTime.Text;

                moProfile["-UseVirtualMachineHostArchive"] = this.UseVirtualMachineHostArchive.IsChecked;
                moProfile["-VirtualMachineHostArchivePath"] = this.VirtualMachineHostArchivePath.Text;
                moProfile["-UseConnectVirtualMachineHost"] = this.UseConnectVirtualMachineHost.IsChecked;
                moProfile["-VirtualMachineHostUsername"] = this.VirtualMachineHostUsername.Text;
                moProfile["-VirtualMachineHostPassword"] = this.VirtualMachineHostPassword.Text;

                // Only update the selected backup devices list (and bit field) if the backup devices
                // tab has been viewed or one of the backup device checkboxes has been clicked.
                if ( mbUpdateSelectedBackupDevices )
                {
                    // Make the list of selected backup devices a multi-line block by inserting newlines before hyphens.
                    moProfile["-SelectedBackupDevices"] = loSelectedBackupDevices.sCommandBlock();
                    moProfile["-SelectedBackupDevicesBitField"] = Convert.ToString(this.iSelectedBackupDevicesBitField(), 2);

                    mbUpdateSelectedBackupDevices = false;
                }

                moProfile["-CleanupFiles"] = this.CleanupFiles.IsChecked;
                moProfile["-BackupFiles"] = this.BackupFiles.IsChecked;
                moProfile["-BackupBeginScriptEnabled"] = this.BackupBeginScriptEnabled.IsChecked;
                moProfile["-BackupDoneScriptEnabled"] = this.BackupDoneScriptEnabled.IsChecked;

                moProfile.Save();
            }

            if ( !this.bMainLoopStopped )
                this.bMainLoopRestart = true;
        }

        private bool bValidateConfiguration()
        {
            return ( this.bValidateConfigWizardValues(true) && this.bValidateConfigDetailsValues(true) );
        }

        private bool bValidateConfigWizardValues(bool abVerifyAllTabs)
        {
            string lsCaption = "Please Fix Before You Finish";
            string lsMessage = null;
            bool lbHaveMovedForward = this.ConfigWizardTabs.SelectedIndex >= miPreviousConfigWizardSelectedIndex;

            // Step 1 (backup folder)
            if ( abVerifyAllTabs || lbHaveMovedForward && miPreviousConfigWizardSelectedIndex
                    == ItemsControl.ItemsControlFromItemContainer(
                    this.tabStep1).ItemContainerGenerator.IndexFromContainer(this.tabStep1)
                    )
            {
                string lsSystemFolderPrefix = Environment.GetFolderPath(Environment.SpecialFolder.System);
                lsSystemFolderPrefix = Path.Combine(Path.GetPathRoot(lsSystemFolderPrefix),
                                                    lsSystemFolderPrefix.Split(Path.DirectorySeparatorChar)[1]);
                string lsProgramsFolderPrefix = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                lsProgramsFolderPrefix = Path.Combine(Path.GetPathRoot(lsProgramsFolderPrefix),
                                                    lsProgramsFolderPrefix.Split(Path.DirectorySeparatorChar)[1]);

                if ( !Directory.Exists(this.FolderToBackup.Text) )
                    lsMessage += (null == lsMessage ? "" : Environment.NewLine + Environment.NewLine)
                            + "Step 1. Select a backup folder that exists."
                            ;
                if (this.FolderToBackup.Text == Path.GetPathRoot(lsSystemFolderPrefix)
                        || this.FolderToBackup.Text.StartsWith(lsSystemFolderPrefix)
                        || this.FolderToBackup.Text.StartsWith(lsProgramsFolderPrefix)
                        )
                    lsMessage += (null == lsMessage ? "" : Environment.NewLine + Environment.NewLine)
                            + "Step 1. Select a backup folder that is not the system root,"
                            + " not in the system folder and not in the program files folder."
                            ;
            }

            // Step 2 (backup filename and archive folder).
            if ( abVerifyAllTabs || lbHaveMovedForward && miPreviousConfigWizardSelectedIndex
                    == ItemsControl.ItemsControlFromItemContainer(
                    this.tabStep2).ItemContainerGenerator.IndexFromContainer(this.tabStep2)
                    )
            {
                try
                {   // Don't use "Path.Combine()" here since we want
                    // to test for path separators in the filename.
                    string lsPathfile = Path.GetDirectoryName(moProfile.sLoadedPathFile)
                                            + Path.DirectorySeparatorChar + BackupOutputFilename.Text;
                    FileStream loFileStream = File.Create(lsPathfile);
                    loFileStream.Close();
                    File.Delete(lsPathfile);
                }
                catch
                {
                    lsMessage += (null == lsMessage ? "" : Environment.NewLine + Environment.NewLine)
                            + "Step 2a. Type a backup filename that is valid for output files.";
                }

                try
                {
                    if ( !Directory.Exists(ArchivePath.Text) )
                    {
                        Directory.CreateDirectory(ArchivePath.Text);
                        Directory.Delete(ArchivePath.Text);
                    }
                }
                catch
                {
                    lsMessage += (null == lsMessage ? "" : Environment.NewLine + Environment.NewLine)
                            + "Step 2b. Select or type a valid archive folder name.";
                }

                if ( (bool)this.UseVirtualMachineHostArchive.IsChecked && !this.VirtualMachineHostArchivePath.Text.StartsWith("\\\\") )
                {
                    lsMessage += (null == lsMessage ? "" : Environment.NewLine + Environment.NewLine)
                            + "Step 2c. Select or type a valid VM host archive share name.";
                }

                if ( (bool)this.UseConnectVirtualMachineHost.IsChecked 
                        && ("" == this.VirtualMachineHostUsername.Text || "" == this.VirtualMachineHostPassword.Text ) )
                {
                    lsMessage += (null == lsMessage ? "" : Environment.NewLine + Environment.NewLine)
                            + "Step 2d. Select or type a valid VM host archive share username and password.";
                }

                if ( null != lsMessage )
                    lsMessage += Environment.NewLine + Environment.NewLine
                                + "Also, make sure you have adminstrator privileges to do all this.";
            }

            // Step 4 (backup time)
            if ( abVerifyAllTabs || lbHaveMovedForward && miPreviousConfigWizardSelectedIndex
                    == ItemsControl.ItemsControlFromItemContainer(
                    this.tabStep4).ItemContainerGenerator.IndexFromContainer(this.tabStep4)
                    )
            {
                DateTime ldtBackupTime;

                if ( !DateTime.TryParse(BackupTime.Text, out ldtBackupTime) )
                    lsMessage += (null == lsMessage ? "" : Environment.NewLine + Environment.NewLine)
                            + "Step 4. Select a valid backup time."
                            ;
            }

            if ( null != lsMessage )
            {
                tvMessageBox.ShowWarning(this, lsMessage, lsCaption);
            }
            else
            {
                // Adjust "-BackupDoneArgs" for a proper potential rerun of the "backup done" script.
                // The only one that can reasonably be changed without potentially impacting several
                // other values is the "-VirtualMachineHostArchivePath".
                tvProfile loBackupDoneArgs = new tvProfile(moProfile.sValue("-BackupDoneArgs", ""));

                loBackupDoneArgs["-VirtualMachineHostArchivePath"] = moProfile.sValue("-VirtualMachineHostArchivePath", "");

                moProfile["-BackupDoneArgs"] = loBackupDoneArgs.sCommandBlock();
                moProfile.Save();
            }

            return null == lsMessage;
        }

        private bool bValidateConfigDetailsValues(bool abVerifyAllTabs)
        {
            string lsCaption = "Please Fix Before You Finish";
            string lsMessage = null;
            bool lbHaveMovedForward = this.ConfigDetailsTabs.SelectedIndex >= miPreviousConfigDetailsSelectedIndex;

            // General
            if ( abVerifyAllTabs || lbHaveMovedForward && miPreviousConfigDetailsSelectedIndex
                    == ItemsControl.ItemsControlFromItemContainer(
                    this.tabSetupGeneral).ItemContainerGenerator.IndexFromContainer(this.tabSetupGeneral)
                    )
            {
            }

            if ( null != lsMessage )
                tvMessageBox.ShowWarning(this, lsMessage, lsCaption);

            return null == lsMessage;
        }

        private void ConfigWizardTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.GetSetConfigurationDefaults();
            this.bValidateConfigWizardValues(false);

            miPreviousConfigWizardSelectedIndex = this.ConfigWizardTabs.SelectedIndex;
        }

        private void ConfigDetailsTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.GetSetConfigurationDefaults();
            this.bValidateConfigDetailsValues(false);

            miPreviousConfigDetailsSelectedIndex = this.ConfigDetailsTabs.SelectedIndex;
        }

        private void BackupDeviceCheckboxStateChanged(object sender, RoutedEventArgs e)
        {
            CheckBox    loCheckBox = (CheckBox)sender;
            string      lsTokenPathFile = Path.Combine((loCheckBox.Tag as DriveInfo).Name, moDoGoPcBackup.sBackupDriveToken);

            if ( (bool)loCheckBox.IsChecked )
            {
                if ( !File.Exists(lsTokenPathFile) )
                    File.Create(lsTokenPathFile).Close();
            }
            else
            {
                if ( File.Exists(lsTokenPathFile) )
                    File.Delete(lsTokenPathFile);
            }

            // If the user clicks a backup devices checkbox, update the profile.
            mbUpdateSelectedBackupDevices = true;
        }
        
        private void btnSetupDone_Click(object sender, RoutedEventArgs e)
        {
            if ( this.bValidateConfiguration()
                    && tvMessageBoxResults.Yes == tvMessageBox.Show(this, string.Format(@"
Are you sure you want to run the backup now?

You can continue this later wherever you left off. "
+ @" You can also edit the profile file directly (""{0}"") for"
+ @" much more detailed configuration (see ""Help"").
", Path.GetFileName(moProfile.sLoadedPathFile))
                    , "Run Backup", tvMessageBoxButtons.YesNo, tvMessageBoxIcons.Question)
                    )
            {
                moProfile["-AllConfigWizardStepsCompleted"] = true;
                moProfile.Save();                

                this.HideMiddlePanels();
                this.MainButtonPanel.IsEnabled = true;

                this.DoBackup();
                this.CreateSysTrayIcon();
                this.MainLoop();
            }
        }

        public int iCurrentBackupDevicesBitField()
        {
            // The leftmost bit is always 1 to preserve leading zeros.
            int  liCurrentBackupDevicesBitField = 1;
            char lcPossibleDriveLetter = moDoGoPcBackup.cPossibleDriveLetterBegin;

            foreach ( DriveInfo loDrive in DriveInfo.GetDrives() )
            {
                string lsDeviceDriveLetter = loDrive.Name.Substring(0, 1);

                // Skip devices with letters starting before the possible drive letters.
                if ( String.Compare(lsDeviceDriveLetter, lcPossibleDriveLetter.ToString()) >= 0 )
                {
                    // Fill in zeros for all drives prior to or between each drive selected.
                    while ( String.Compare(lsDeviceDriveLetter, lcPossibleDriveLetter.ToString()) > 0 )
                    {
                        liCurrentBackupDevicesBitField = liCurrentBackupDevicesBitField << 1;
                        ++lcPossibleDriveLetter;
                    }

                    liCurrentBackupDevicesBitField = liCurrentBackupDevicesBitField << 1;
                    liCurrentBackupDevicesBitField += (bool)File.Exists(Path.Combine(loDrive.Name, moDoGoPcBackup.sBackupDriveToken)) ? 1 : 0;
                    ++lcPossibleDriveLetter;
                }
            }

            // Fill in zeros for all the drives after the last drive found.
            for ( char c = lcPossibleDriveLetter; c <= moDoGoPcBackup.cPossibleDriveLetterEnd; ++c )
            {
                liCurrentBackupDevicesBitField = liCurrentBackupDevicesBitField << 1;
            }

            return liCurrentBackupDevicesBitField;
        }

        // Return a bit field of user's preferences for additional backup devices.
        private int iSelectedBackupDevicesBitField()
        {
            // The leftmost bit is always 1 to preserve leading zeros.
            int  liSelectedBackupDevicesBitField = 1;
            char lcPossibleDriveLetter = moDoGoPcBackup.cPossibleDriveLetterBegin;

            foreach (CheckBox loCheckBox in gridBackupDevices.Children)
            {
                string lsCheckBoxDriveLetter = (loCheckBox.Tag as DriveInfo).Name.Substring(0, 1);

                // Skip devices with letters starting before the possible drive letters.
                if ( String.Compare(lsCheckBoxDriveLetter, lcPossibleDriveLetter.ToString()) >= 0 )
                {
                    // Fill in zeros for all drives prior to or between each drive selected.
                    while ( String.Compare(lsCheckBoxDriveLetter, lcPossibleDriveLetter.ToString()) > 0 )
                    {
                        liSelectedBackupDevicesBitField = liSelectedBackupDevicesBitField << 1;
                        ++lcPossibleDriveLetter;
                    }

                    liSelectedBackupDevicesBitField = liSelectedBackupDevicesBitField << 1;
                    liSelectedBackupDevicesBitField += (bool)loCheckBox.IsChecked ? 1 : 0;
                    ++lcPossibleDriveLetter;
                }
            }

            // Fill in zeros for all the drives after the last drive found.
            for ( char c = lcPossibleDriveLetter; c <= moDoGoPcBackup.cPossibleDriveLetterEnd; ++c )
            {
                liSelectedBackupDevicesBitField = liSelectedBackupDevicesBitField << 1;
            }

            return liSelectedBackupDevicesBitField;
        }

        // Show any missing selected backup devices in a pop-up message.
        private bool ShowMissingBackupDevices()
        {
            bool lbDeviceReattached = false;

            if ( mbInShowMissingBackupDevices )
                return lbDeviceReattached;

            mbInShowMissingBackupDevices = true;

            // Additional backup devices are only used by the "backup done" script.
            if ( moProfile.bValue("-BackupDoneScriptEnabled", true) )
            {
                tvMessageBoxResults leShowMissingBackupDevices = tvMessageBoxResults.No;

                while ( tvMessageBoxResults.No == leShowMissingBackupDevices )
                {
                    switch ( leShowMissingBackupDevices = this.eShowMissingBackupDevices() )
                    {
                        case tvMessageBoxResults.Yes:
                            // Allow updates to the selected devices
                            // (ie. simulate a device checkbox click).
                            mbUpdateSelectedBackupDevices = true;
                            // Change the setup to match the missing device(s).
                            this.GetSetConfigurationDefaults();
                            break;
                        case tvMessageBoxResults.No:
                            // Try again.
                            lbDeviceReattached = true;
                            break;
                        case tvMessageBoxResults.Cancel:
                            // Quit trying.
                            break;
                    }
                }

                // "None" (the default from "this.eShowMissingBackupDevices()")
                // means a device was reattached or no devices were missing.
                lbDeviceReattached = lbDeviceReattached && tvMessageBoxResults.None == leShowMissingBackupDevices;

                // Reset setup of backup device checkboxes.
                if ( tvMessageBoxResults.Yes == leShowMissingBackupDevices )
                    gridBackupDevices.Children.Clear();

                if ( lbDeviceReattached
                        && tvMessageBoxResults.Yes == tvMessageBox.Show(
                                  this
                                , "Reattached devices should be updated.\r\n\r\nShall we rerun the last \"backup done\" script?"
                                , "Device Reattached"
                                , tvMessageBoxButtons.YesNo
                                , tvMessageBoxIcons.Question
                                , tvMessageBoxCheckBoxTypes.DontAsk
                                , moProfile
                                , "-DeviceReattached"
                                )
                            )
                    this.RerunBackupDoneScript();
            }

            mbInShowMissingBackupDevices = false;

            return lbDeviceReattached;
        }

        // Show any missing selected backup devices in a pop-up message and return the user's response.
        private tvMessageBoxResults eShowMissingBackupDevices()
        {
            tvMessageBoxResults leTvMessageBoxResults = tvMessageBoxResults.None;

            List<char> loMissingBackupDevices = moDoGoPcBackup.oMissingBackupDevices(
                                                    this.iCurrentBackupDevicesBitField());

            if ( 0 != loMissingBackupDevices.Count )
            {
                string lsMessageCaption = String.Format("Missing Device{0}", 1 == loMissingBackupDevices.Count ? "" : "s");

                tvProfile loSelectedBackupDevices = moProfile.oProfile("-SelectedBackupDevices");

                string lsMessage = String.Format("Change setup? Selected backup device{0}:\r\n\r\n"
                                                    , 1 == loSelectedBackupDevices.Count ? "" : "s");

                foreach (DictionaryEntry loEntry in loSelectedBackupDevices)
                {
                    lsMessage += loEntry.Value + "\r\n";
                }

                lsMessage += String.Format("\r\nMissing device{0}: "
                                            , 1 == loMissingBackupDevices.Count ? "" : "s");

                foreach (char drive in loMissingBackupDevices)
                {
                    lsMessage += "(" + drive + ":) ";
                }

                lsMessage += String.Format(@"

[Yes]  Make device{0} found the selected device{1} (unselect missing device{2}).

[No]  Reattach the missing backup device{3} and try again.

[Cancel]  Change nothing now (throw an error later when the backup runs).
                        "
                        , 1 == loSelectedBackupDevices.Count - loMissingBackupDevices.Count ? "" : "s"
                        , 1 == loSelectedBackupDevices.Count - loMissingBackupDevices.Count ? "" : "s"
                        , 1 == loMissingBackupDevices.Count ? "" : "s"
                        , 1 == loMissingBackupDevices.Count ? "" : "s"
                        );

                leTvMessageBoxResults = tvMessageBox.Show(this, lsMessage, lsMessageCaption, tvMessageBoxButtons.YesNoCancel, tvMessageBoxIcons.Alert);
            }

            return leTvMessageBoxResults;
        }

        private void btnRerunBackupDoneScript_Click(object sender, RoutedEventArgs e)
        {
            if ( tvMessageBoxResults.Yes == tvMessageBox.Show(
                      this
                    , "Are you sure you want to rerun the last \"backup done\" script?"
                    , "Rerun Script"
                    , tvMessageBoxButtons.YesNo
                    , tvMessageBoxIcons.Question
                    ) )
                this.RerunBackupDoneScript();
        }

        private void btnSetupGeneralResetAllPrompts_Click(object sender, RoutedEventArgs e)
        {
            tvMessageBox.ResetAllPrompts(
                  this
                , "Are you sure you want to reset all prompts?"
                , "Reset All Prompts"
                , moProfile
                );
        }

        private void ShowWizard()
        {
            this.MiddlePanelConfigWizard.Visibility = Visibility.Visible;

            if ( null != msGetSetConfigurationDefaultsError )
            {
                // This kludge (the "Replace()") is needed to correct for bad MS grammar.
                tvMessageBox.ShowError(this, msGetSetConfigurationDefaultsError.Replace("a unknown", "an unknown"), "Error Loading Configuration");
                msGetSetConfigurationDefaultsError = null;
            }
        }

        private void ShowOutputText()
        {
            if ( "" != this.BackupProcessOutput.Text.Trim() )
                this.MiddlePanelOutputText.Visibility = Visibility.Visible;
            else
                this.MiddlePanelTimer.Visibility = Visibility.Visible;
        }

        private void HideMiddlePanels()
        {
            this.MiddlePanelTimer.Visibility = Visibility.Collapsed;
            this.MiddlePanelConfigWizard.Visibility = Visibility.Collapsed;
            this.MiddlePanelConfigDetails.Visibility = Visibility.Collapsed;
            this.MiddlePanelOutputText.Visibility = Visibility.Collapsed;
        }

        private void DisableButtons()
        {
            moStartStopButtonState.Content = this.btnDoBackupNow.Content;
            moStartStopButtonState.ToolTip = this.btnDoBackupNow.ToolTip;
            moStartStopButtonState.Background = this.btnDoBackupNow.Background;
            moStartStopButtonState.FontSize = this.btnDoBackupNow.FontSize;

            this.btnDoBackupNow.Content = "STOP";
            this.btnDoBackupNow.ToolTip = "Stop the Process Now";
            this.btnDoBackupNow.Background = Brushes.Red;
            this.btnDoBackupNow.FontSize = 22;

            this.btnSetup.IsEnabled = false;
            this.btnConfigureDetails.IsEnabled = false;
        }

        private void EnableButtons()
        {
            this.btnDoBackupNow.Content = moStartStopButtonState.Content;
            this.btnDoBackupNow.ToolTip = moStartStopButtonState.ToolTip;
            this.btnDoBackupNow.Background = moStartStopButtonState.Background;
            this.btnDoBackupNow.FontSize = moStartStopButtonState.FontSize;

            this.MainButtonPanel.IsEnabled = true;
            this.btnDoBackupNow.IsEnabled = true;
            this.btnSetup.IsEnabled = true;
            this.btnConfigureDetails.IsEnabled = true;
            this.btnShowArchive.IsEnabled = true;
            this.btnShowTimer.IsEnabled = true;
        }

        public void GetSetOutputTextPanelErrorCache()
        {
            if ( !moProfile.ContainsKey("-PreviousBackupOk")
                    || moProfile.bValue("-PreviousBackupOk", false) )
            {
                moProfile.Remove("-PreviousBackupErrorOutputText");
            }
            else
            {
                if ( "" == this.BackupProcessOutput.Text )
                {
                    this.BackupProcessOutput.Text = moProfile.sValue("-PreviousBackupErrorOutputText", "");
                    this.scrBackupProcessOutput.ScrollToEnd();

                    // Indicate the backup has run at least once.
                    // This is necessary so the output text panel will
                    // be displayed when the timer button is toggled.
                    mbBackupRan = true;
                }
                else
                {
                    // This makes sure the last line of text appears before we save.
                    System.Windows.Forms.Application.DoEvents();

                    moProfile["-PreviousBackupErrorOutputText"] = this.BackupProcessOutput.Text;
                    moProfile.Save();
                }
            }            
        }

        private void PopulateTimerDisplay(string asCommonValue)
        {
            lblNextBackupTime.Content = asCommonValue;
            lblNextBackupTimeLeft.Content = asCommonValue;

            if ( moProfile.ContainsKey("-PreviousBackupTime") )
                lblPrevBackupTime.Content = moProfile["-PreviousBackupTime"];
            else
                lblPrevBackupTime.Content = mcsNoPreviousText;

            imgPrevBackupResultNull.Visibility = Visibility.Collapsed;
            imgPrevBackupResultPass.Visibility = Visibility.Collapsed;
            imgPrevBackupResultFail.Visibility = Visibility.Collapsed;

            if ( !moProfile.ContainsKey("-PreviousBackupOk") )
            {
                lblPrevBackupResult.Content = "unfinished";
                imgPrevBackupResultNull.Visibility = Visibility.Visible;
            }
            else
            {
                if ( moProfile.bValue("-PreviousBackupOk", false) )
                {
                    lblPrevBackupResult.Content = "Ok";
                    imgPrevBackupResultPass.Visibility = Visibility.Visible;
                }
                else
                {
                    lblPrevBackupResult.Content = "Failed";
                    imgPrevBackupResultFail.Visibility = Visibility.Visible;
                }
            }

            lblTimerStatus.Content = asCommonValue;
        }

        private void ShowBackupRunning()
        {
            this.ShowBackupRunning(false);
        }

        private void ShowBackupRunning(bool abShowBackupRan)
        {
            if ( this.bBackupRunning || (abShowBackupRan && mbBackupRan) )
                this.MiddlePanelOutputText.Visibility = Visibility.Visible;
        }

        private void ShowHelp()
        {
            // If a help window is already open, close it.
            foreach (ScrollingText loWindow in moOtherWindows)
            {
                if ( loWindow.Title.Contains("Help") )
                {
                    loWindow.Close();
                    moOtherWindows.Remove(loWindow);
                    break;
                }
            }

            ScrollingText   loHelp = new ScrollingText(moProfile["-Help"].ToString(), "Backup Help", true);
                            loHelp.TextBackground = Brushes.Khaki;
                            loHelp.Show();

                            moOtherWindows.Add(loHelp);
        }

        private bool ShowPreviousBackupStatus()
        {
            bool lbPreviousBackupError = false;

            // "ContainsKey" is used here to prevent "DateTime.MinValue"
            // being written as the default value for -PreviousBackupTime.

            // If the backup just finished (ie. less than 1 minute ago), don't bother.
            if ( !moProfile.ContainsKey("-PreviousBackupTime") || (DateTime.Now - moProfile.dtValue(
                    "-PreviousBackupTime", DateTime.MinValue)).Minutes > 1 )
            {
                if ( !moProfile.ContainsKey("-PreviousBackupOk") )
                {
                    lbPreviousBackupError = true;
                    mbShowBackupOutputAfterSysTray = true;

                    tvMessageBox.ShowError(this, "The previous backup did not complete. Check the log."
                            + moDoGoPcBackup.sSysTrayMsg, "Backup Failed");
                }
                else
                {
                    if ( !moProfile.bValue("-PreviousBackupOk", false) )
                    {
                        lbPreviousBackupError = true;
                        mbShowBackupOutputAfterSysTray = true;

                        tvMessageBox.ShowError(this, "The previous backup failed. Check the log for errors."
                                + moDoGoPcBackup.sSysTrayMsg, "Backup Failed");
                    }
                    else
                    {
                        int liPreviousBackupDays = (DateTime.Now - moProfile.dtValue("-PreviousBackupTime"
                                                        , DateTime.MinValue)).Days;

                        tvMessageBox.Show(this, string.Format(
                                "The previous backup finished successfully ({0} {1} day{2} ago)."
                                        , liPreviousBackupDays < 1 ? "less than" : "about"
                                        , liPreviousBackupDays < 1 ? 1 : liPreviousBackupDays
                                        , liPreviousBackupDays <= 1 ? "" : "s"
                                        )
                                + moDoGoPcBackup.sSysTrayMsg
                                , "Backup Finished"
                                , tvMessageBoxButtons.OK, tvMessageBoxIcons.Done
                                , tvMessageBoxCheckBoxTypes.SkipThis
                                , moProfile
                                , "-PreviousBackupFinished"
                                );
                    }
                }
            }

            this.GetSetOutputTextPanelErrorCache();

            return lbPreviousBackupError;
        }

        private void CreateSysTrayIcon()
        {
            if ( null == moNotifyIcon )
            {
                moNotifyIcon = new ExtendedNotifyIcon();
                moNotifyIcon.MouseClick += new ExtendedNotifyIcon.MouseClickHandler(moNotifyIcon_OnShowWindow);
                moNotifyIcon.StopMouseMoveEventFromFiring();
                moNotifyIcon.StopMouseLeaveEventFromFiring();
                moNotifyIcon.targetNotifyIcon.Icon = new System.Drawing.Icon(
                        Application.GetResourceStream(new Uri(
                        "pack://application:,,,/Resources/images/GoPC.ico")).Stream);
                moNotifyIcon.targetNotifyIcon.Text = mcsNotifyIconIdleText;
            }

            // Also make sure the timer / close checkboxes are visible
            // since we can't fully exit the software without them.
            this.CloseCheckboxes.Visibility = Visibility.Visible;
        }

        private void RerunBackupDoneScript()
        {
            this.GetSetConfigurationDefaults();

            // Only do the rerun if the current configuration is valid.
            bool lbDoRerun = this.bValidateConfiguration();
            if ( lbDoRerun )
            {
                // Verify that critical last run values match the current configuration.
                bool lbLastRunArgsUnstable = false;
                    try
                    {
                        tvProfile loBackupDoneArgs = new tvProfile(moProfile.sValue("-BackupDoneArgs", ""));
                        if ( !lbLastRunArgsUnstable ) lbLastRunArgsUnstable = loBackupDoneArgs["-LocalArchivePath"].ToString() != moDoGoPcBackup.sArchivePath();
                        if ( !lbLastRunArgsUnstable ) lbLastRunArgsUnstable = Path.GetDirectoryName(loBackupDoneArgs["-LogPathFile"].ToString()) != Path.GetDirectoryName(moProfile.sRelativeToProfilePathFile(moDoGoPcBackup.sLogPathFile));
                    }
                    catch {}

                if ( lbLastRunArgsUnstable )
                    lbDoRerun = tvMessageBoxResults.OK == tvMessageBox.Show(
                                      this
                                    , "Changes have been made to your backup configuration which may cause a rerun to fail.\r\n\r\nAre you sure you want to rerun the last \"backup done\" script?"
                                    , "Backup Configuration Changed"
                                    , tvMessageBoxButtons.OKCancel
                                    , tvMessageBoxIcons.Question
                                    );
            }
            if ( lbDoRerun )
            {
                this.ShowMe();
                this.bBackupRunning = true;

                // Append a blank line to the error output before proceeding.
                moDoGoPcBackup.LogIt("");

                // Run the "backup done" script and return the failed file count with bit field.
                // The exit code is defined in the script as a combination of two integers-
                // a bit field of found backup devices and a count of copy failures (99 max).
                // The integer part of the composite number is the bit field.
                double ldCompositeResult = moDoGoPcBackup.iBackupDoneScriptCopyFailuresWithBitField(true) / 100.0;

                // The fractional part (x 100) is the actual number of copy failures.
                int liCopyFailures = (int)Math.Round(100 * (ldCompositeResult - (int)ldCompositeResult));

                if ( 0 != liCopyFailures )
                {
                    moProfile["-PreviousBackupOk"] = false;
                    moProfile.Save();

                    moDoGoPcBackup.ShowError(
                              string.Format("The \"backup done\" script failed with {0} copy failure{1}. Check the log for errors."
                                    + moDoGoPcBackup.sSysTrayMsg, liCopyFailures, 1 == liCopyFailures ? "" : "s")
                            , "Backup Failed");
                }
                else
                {
                    bool lbPreviousBackupOk = moProfile.ContainsKey("-PreviousBackupOk") && moProfile.bValue("-PreviousBackupOk", false);

                    if ( !lbPreviousBackupOk && tvMessageBoxResults.Yes == tvMessageBox.Show(
                                      this
                                    , "The backup status is \"Backup Failed\".\r\n\r\nShall we change it to \"Backup OK?\""
                                    , "Adjust Backup Status"
                                    , tvMessageBoxButtons.YesNo
                                    , tvMessageBoxIcons.Question
                                    , tvMessageBoxCheckBoxTypes.DontAsk
                                    , moProfile
                                    , "-BackupStatusChanged"
                                    )
                                )
                    {
                        moProfile["-PreviousBackupOk"] = true;
                        moProfile["-PreviousBackupTime"] = DateTime.Now;
                        moProfile.Save();

                        this.GetSetConfigurationDefaults();
                    }
                }

                this.bBackupRunning = false;
            }
        }

        private void DoBackup()
        {
            this.bBackupRunning = true;

            if ( moProfile.bValue("-CleanupFiles", true) && moProfile.bValue("-BackupFiles", true)
                    && Visibility.Hidden != this.Visibility )
                tvMessageBox.ShowBriefly(this, "The file cleanup process is now running ..."
                        , "Backup Started", tvMessageBoxIcons.Information, 2);

            this.InitProgressBar(moDoGoPcBackup.iCleanupFilesCount());

            if ( !moDoGoPcBackup.CleanupFiles() )
            {
                this.InitProgressBar(0);
            }
            else
            {
                this.IncrementProgressBar(true);

                if ( !moProfile.bValue("-BackupFiles", true) )
                {
                    moDoGoPcBackup.LogIt("Backup files is disabled.");
                }
                else
                {
                    this.InitProgressBar(moDoGoPcBackup.iBackupFilesCount());

                    if ( moDoGoPcBackup.BackupFiles() )
                    {
                        this.IncrementProgressBar(true);
                    }
                    else
                    {
                        this.InitProgressBar(0);
                    }

                    if ( this.bVisible )
                        this.ShowMissingBackupDevices();
                }
            }

            this.bBackupRunning = false;
        }

        private DateTime dtBackupTime()
        {
            DateTime ldtBackupTime = DateTime.MinValue;

            try
            {
                ldtBackupTime = moProfile.dtValue("-BackupTime", DateTime.MinValue);

                if ( DateTime.MinValue == ldtBackupTime )
                {
                    ldtBackupTime = DateTime.Now;
                }
                else
                {
                    // Wait until the given date (if in the future).
                    // Otherwise wait until later today or tomorrow.

                    // This is done by parsing out the date string
                    // from the starting datetime string to get the
                    // given time for the given datetime.
                    if ( ldtBackupTime < DateTime.Now )
                        ldtBackupTime = DateTime.Parse(ldtBackupTime.ToString().Replace(
                                ldtBackupTime.ToShortDateString(), null));

                    // If the given time has passed, wait until the
                    // the same time tomorrow.
                    if (ldtBackupTime < DateTime.Now)
                        ldtBackupTime = ldtBackupTime.AddDays(1);
                }
            }
            catch (Exception ex)
            {
                this.bMainLoopStopped = true;

                // This kludge (the "Replace()") is needed to correct for bad MS grammar.
                tvMessageBox.ShowError(this, ex.Message.Replace("a unknown", "an unknown"), "Error Setting Backup Time");
            }

            return ldtBackupTime;
        }

        private void MainLoop()
        {
            if ( !this.bMainLoopStopped
                    || !(bool)this.chkUseTimer.IsChecked
                    || this.bBackupRunning
                    )
                return;

            if ( null == moMainLoopTimer )
            {
                moMainLoopTimer = new DispatcherTimer();
                moMainLoopTimer.Tick += new EventHandler(moMainLoopTimer_Tick);
                moMainLoopTimer.Interval = new TimeSpan(0, 0, 0, 0, moProfile.iValue("-MainLoopSleepMS", 200));
            }

            this.bMainLoopStopped = false;
            this.bMainLoopRestart = true;

            moMainLoopTimer.Start();
        }

        private void moMainLoopTimer_Tick(object sender, EventArgs e)
        {
            // This keeps the output window updated while the backup is running.
            System.Windows.Forms.Application.DoEvents();

            if ( this.bMainLoopStopped || this.bBackupRunning )
                return;

            if ( this.bMainLoopRestart )
            {
                mdtNextStart= this.dtBackupTime();

                this.bMainLoopRestart = false;
            }

            if ( DateTime.MinValue == mdtNextStart )
                return;

            try
            {
                string lsTimeLeft = ((TimeSpan)(mdtNextStart - DateTime.Now)).ToString();
                lblNextBackupTimeLeft.Content = lsTimeLeft.Substring(0, lsTimeLeft.Length - 8);

                // "ContainsKey" is used here to avoid storing "mcsNoPreviousText" as the 
                // default -PreviousBackupTime. Doing so would throw an error next time.
                if (moProfile.ContainsKey("-PreviousBackupTime"))
                    lblPrevBackupTime.Content = moProfile["-PreviousBackupTime"];
                else
                    lblPrevBackupTime.Content = mcsNoPreviousText;

                if ( DateTime.Now < mdtNextStart )
                {
                    if (!this.bBackupRunning)
                        lblTimerStatus.Content = mcsWaitingText;

                    lblNextBackupTime.Content = (DateTime.Today.Date == mdtNextStart.Date
                            ? ""
                            : mdtNextStart.DayOfWeek.ToString()) + " " + mdtNextStart.ToShortTimeString();
                }
                else
                {
                    mdtNextStart = DateTime.Now.AddMinutes(moProfile.iValue("-MainLoopMinutes", 1440));
                    lblNextBackupTime.Content = (DateTime.Today.Date == mdtNextStart.Date
                            ? ""
                            : mdtNextStart.DayOfWeek.ToString()) + " " + mdtNextStart.ToShortTimeString();

                    this.DoBackup();

                    // This kludge is needed to refresh "DateTime.Now" after a long pause.
                    System.Windows.Forms.Application.DoEvents();

                    // If it's time to run the backup again, the backup finished dialog must have
                    // been up for more than a day (ie. more than -MainLoopMinutes). Wait another day.
                    if ( DateTime.Now > mdtNextStart )
                        mdtNextStart = this.dtBackupTime();
                }
            }
            catch (Exception ex)
            {
                tvMessageBox.ShowError(this, ex);
            }
            finally
            {
                this.Cursor = null;
            }
        }
    }
}
