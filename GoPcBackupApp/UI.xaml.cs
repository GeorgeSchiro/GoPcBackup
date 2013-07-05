using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Input;
using Microsoft.Win32;
using tvToolbox;

namespace GoPcBackup
{
    /// <summary>
    /// Interaction logic for UI.xaml
    /// </summary>
    public partial class UI : SavedWindow
    {
        private tvProfile moProfile;
        private DoGoPcBackup moDoGoPcBackup;

        private const string mcsStoppedText = "stopped";
        private const string mcsNoPreviousText = "none";
        private const string mcsWaitingText = "Waiting";
        private const string mcsBackingUpText = "Backup Running";

        private ExtendedNotifyIcon moNotifyIcon;
        private double miOriginalScreenHeight;
        private double miOriginalScreenWidth;
        private double miAdjustedWindowHeight;
        private double miAdjustedWindowWidth;
        private int miPreviousConfigWizardSelectedIndex = -1;
        private int miProcessedFileCount;
        private bool mbShowBackupOutputAfterSysTray; // Determines if the text output console
        private bool mbPreviousBackupError;          // is displayed after a systray click.
        private bool mbBackupRan;
        private Help moHelpWindow;
        private Button moStartStopButtonState = new Button();

        private UI() { }


        /// <summary>
        /// This constructor expects a profile object 
        /// and a file backup object to be provided.
        /// </summary>
        /// <param name="aoProfile">
        /// The given profile object must either contain runtime options
        /// or it will be returned filled with default runtime options.
        /// </param>
        /// <param name="aoDoGoPcBackup">
        /// The given file backup object creates dated archives
        /// of file collections from the local file system.
        /// </param>
        public UI(tvProfile aoProfile, DoGoPcBackup aoDoGoPcBackup)
        {
            InitializeComponent();

            // This load window UI defaults fom the given profile.
            base.Init();

            moProfile = aoProfile;
            moDoGoPcBackup = aoDoGoPcBackup;
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

                if (!mbBackupRunning)
                {
                    if ((bool)this.chkUseTimer.IsChecked)
                        this.PopulateTimerDisplay(mcsWaitingText);
                    else
                        this.PopulateTimerDisplay(mcsStoppedText);

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

                    // Indicate that the backup has run at least once.
                    mbBackupRan = true;
                }
            }
        }
        private bool mbBackupRunning;


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.AdjustWindowSize();
            miOriginalScreenHeight = SystemParameters.PrimaryScreenHeight;
            miOriginalScreenWidth = SystemParameters.PrimaryScreenWidth;

            // This window is hidden by default. Only make it initially
            // visible if all setup steps have not yet been completed.
            // Otherwise it is displayed via its system tray icon.
            if (!moProfile.bValue("-AllConfigWizardStepsCompleted", false))
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
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // Save any setup changes.
            this.GetSetConfigurationWizardDefaults();

            // Do a full exit if shutting down.
            if (ShutdownMode.OnExplicitShutdown == Application.Current.ShutdownMode)
            {
                this.bMainLoopStopped = true;

                // Always turn the timer back on during shutdowns. That
                // way backups should automatically retart after reboot.
                this.chkUseTimer_SetChecked(true);
            }
            else
            {
                if (!(bool)chkCloseAndExit.IsChecked
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
                    if (!(bool)this.chkUseTimer.IsChecked
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

            if (null != moNotifyIcon)
                moNotifyIcon.Dispose();

            if (null != moHelpWindow)
                moHelpWindow.Close();
        }

        private void LogoImageAnimation_Completed(object sender, EventArgs e)
        {
            if (!moProfile.bValue("-AllConfigWizardStepsCompleted", false))
            {
                this.MiddlePanelConfigWizard.Visibility = Visibility.Visible;
            }
            else
            {
                this.HideMiddlePanels();
                this.MainButtonPanel.IsEnabled = true;
                this.PopulateTimerDisplay(mcsStoppedText);

                if ((bool)this.chkUseTimer.IsChecked && !mbPreviousBackupError)
                    this.HideMe();
                else
                    this.ShowMe();

                this.ShowPreviousBackupStatus();
                this.CreateSysTrayIcon();

                if ((bool)this.chkUseTimer.IsChecked)
                {
                    this.MiddlePanelTimer.Visibility = Visibility.Visible;
                    this.MainLoop();
                }
            }
        }

        // Buttons that don't launch external processes are toggles.

        private void btnDoBackupNow_Click(object sender, RoutedEventArgs e)
        {
            if (this.bBackupRunning)
            {
                // Stop the backup but leave the timer running.
                moDoGoPcBackup.bMainLoopStopped = true;
                this.bBackupRunning = false;
            }
            else
            {
                this.HideMiddlePanels();
                this.GetSetConfigurationWizardDefaults();
                this.DoBackup();
            }
        }

        private void btnSetup_Click(object sender, RoutedEventArgs e)
        {
            if (Visibility.Visible == this.MiddlePanelConfigWizard.Visibility)
            {
                this.HideMiddlePanels();
                this.ShowBackupRunning();
            }
            else
            {
                if (!(bool)this.chkUseTimer.IsChecked || this.StopTimerforUI())
                {
                    this.HideMiddlePanels();
                    this.MiddlePanelConfigWizard.Visibility = Visibility.Visible;
                }
            }
        }

        private void btnConfigureDetails_Click(object sender, RoutedEventArgs e)
        {
            if (Visibility.Visible == this.MiddlePanelConfigDetails.Visibility)
            {
                this.HideMiddlePanels();
                this.ShowBackupRunning();
            }
            else
            {
                if (!(bool)this.chkUseTimer.IsChecked || this.StopTimerforUI())
                {
                    this.HideMiddlePanels();
                    this.MiddlePanelConfigDetails.Visibility = Visibility.Visible;
                }
            }
        }

        private void btnShowTimer_Click(object sender, RoutedEventArgs e)
        {
            if (Visibility.Visible == this.MiddlePanelTimer.Visibility)
            {
                this.HideMiddlePanels();
                this.ShowBackupRunning(true);
            }
            else
            {
                this.GetSetConfigurationWizardDefaults();
                this.HideMiddlePanels();
                this.MiddlePanelTimer.Visibility = Visibility.Visible;

                // Clicking the "show timer" button when the backup
                // is no longer running implies that the timer panel
                // is also what we want to see after a systray click.
                if (!this.bBackupRunning)
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
        // event handlers oddly disables rendering of the check in the box.
        private void chkUseTimer_SetChecked(bool abValue)
        {
            this.chkUseTimer.IsChecked = abValue;

            moProfile["-AutoStart"] = this.chkUseTimer.IsChecked;
            moProfile.Save();

            if (!(bool)this.chkUseTimer.IsChecked)
            {
                this.bMainLoopStopped = true;
                this.MiddlePanelTimer.Visibility = Visibility.Collapsed;
            }
            else
            {
                this.GetSetConfigurationWizardDefaults();
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
            if (MouseButton.Left == e.ChangedButton)
                this.DragMove();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
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
            if (!lsControlClass.Contains("Text")
                    && !lsControlClass.Contains("ClassicBorderDecorator")
                    )
            {
                if (WindowState.Normal == this.WindowState)
                    this.mnuMaximize_Click(null, null);
                else
                    this.mnuRestore_Click(null, null);
            }
        }

        public void AppendOutputTextLine(string asTextLine)
        {
            txtBackupProcessOutput.Inlines.Add(asTextLine + Environment.NewLine);
            if (txtBackupProcessOutput.Inlines.Count > 50)
                txtBackupProcessOutput.Inlines.Remove(txtBackupProcessOutput.Inlines.FirstInline);
            scrBackupProcessOutput.ScrollToEnd();

            this.IncrementProgressBar();
        }

        public void InitProgressBar(int aiMaximum)
        {
            miProcessedFileCount = 0;
            this.prbBackupProgress.Value = 0;
            this.prbBackupProgress.Maximum = aiMaximum;
        }

        public void IncrementProgressBar()
        {
            this.prbBackupProgress.Value = ++miProcessedFileCount;
        }

        private void btnSetupStep1_Click(object sender, RoutedEventArgs e)
        {
            // Currently the setup UI only handles 1 backup set (ie. the primary backup).
            tvProfile loBackupSet1Profile = new tvProfile(moProfile.sValue("-BackupSet", "(not set)"));
            if (loBackupSet1Profile.oOneKeyProfile("-FolderToBackup").Count > 1)
            {
                tvMessageBox.ShowWarning(this, string.Format("The profile file (\"{0}\") has been edited manually to contain more than one folder to backup. Please remove the excess or continue to edit the profile by hand."
                        , Path.GetFileName(moProfile.sLoadedPathFile)), "Can't Change Folder to Backup");
                return;
            }

            System.Windows.Forms.FolderBrowserDialog loOpenDialog = new System.Windows.Forms.FolderBrowserDialog();
            loOpenDialog.RootFolder = Environment.SpecialFolder.Desktop;
            loOpenDialog.SelectedPath = this.txtBackupFolder.Text;

            System.Windows.Forms.DialogResult leDialogResult = loOpenDialog.ShowDialog();

            if (System.Windows.Forms.DialogResult.OK == leDialogResult)
                this.txtBackupFolder.Text = loOpenDialog.SelectedPath;
        }

        private void btnSetupStep3_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog loOpenDialog = new System.Windows.Forms.FolderBrowserDialog();
            loOpenDialog.RootFolder = Environment.SpecialFolder.Desktop;
            loOpenDialog.SelectedPath = this.txtArchivePath.Text;

            System.Windows.Forms.DialogResult leDialogResult = loOpenDialog.ShowDialog();

            if (System.Windows.Forms.DialogResult.OK == leDialogResult)
                this.txtArchivePath.Text = loOpenDialog.SelectedPath;
        }

        private void moNotifyIcon_OnHideWindow()
        {
        }

        private void moNotifyIcon_OnShowWindow()
        {
            if (this.bVisible)
            {
                this.HideMe();
            }
            else
            {
                if (this.bBackupRunning || mbShowBackupOutputAfterSysTray)
                {
                    if (Visibility.Visible != this.MiddlePanelOutputText.Visibility)
                    {
                        this.HideMiddlePanels();
                        this.MiddlePanelOutputText.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    if (Visibility.Visible != this.MiddlePanelTimer.Visibility)
                    {
                        this.HideMiddlePanels();
                        this.MiddlePanelTimer.Visibility = Visibility.Visible;
                    }
                }

                this.ShowMe();
            }
        }

        // This "HideMe() / ShowMe()" kludge is necessary
        // to avoid annoying flicker on some platforms.
        private void HideMe()
        {
            this.MainCanvas.Visibility = Visibility.Hidden;
            this.Hide();
            moNotifyIcon_OnHideWindow();
            this.bVisible = false;
        }

        private void ShowMe()
        {
            this.AdjustWindowSize();
            this.MainCanvas.Visibility = Visibility.Visible;
            System.Windows.Forms.Application.DoEvents();
            this.Show();
            System.Windows.Forms.Application.DoEvents();
            this.bVisible = true;
        }

        private void AdjustWindowSize()
        {
            if (WindowState.Maximized != this.WindowState
                    && (SystemParameters.PrimaryScreenHeight != miOriginalScreenHeight
                        || SystemParameters.MaximizedPrimaryScreenWidth != miOriginalScreenWidth)
                    )
            {
                // Adjust window size to optimize the display depending on screen size.
                // This is done here rather in "Window_Loaded()" in case the screen size
                // changes post startup (eg. via RDP).
                if (SystemParameters.PrimaryScreenHeight <= 768)
                {
                    this.mnuMaximize_Click(null, null);
                }
                else if (SystemParameters.PrimaryScreenHeight <= 864)
                {
                    this.Height = .90 * SystemParameters.MaximizedPrimaryScreenHeight;
                    this.Width = .90 * SystemParameters.MaximizedPrimaryScreenWidth;
                }
                else if (SystemParameters.PrimaryScreenHeight <= 885)
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

            this.txtBackupTime.Text = lsHour + ":" + lsMin + " " + (lbIsAM ? "AM" : "PM");
        }

        private void sldBackupTime_ValueFromString(string asTimeOnly)
        {
            TimeSpan loTimeSpan = DateTime.Parse(asTimeOnly) - DateTime.Today;
            this.sldBackupTime.Value = loTimeSpan.TotalMinutes / moProfile.iValue("-BackupTimeMinsPerTick", 15);
        }

        private bool StopTimerforUI()
        {
            bool lbStopTimerforUI = false;

            // The main loop must be stopped to make keyboard input responsive enough.
            if (moProfile.bValue("-AllConfigWizardStepsCompleted", false))
            {
                if (tvMessageBoxResults.OK == tvMessageBox.Show(
                              this
                            , "The timer will be stopped while you change the setup."
                            , "Timer Stopped"
                            , tvMessageBoxButtons.OKCancel
                            , tvMessageBoxIcons.Information
                            , tvMessageBoxCheckBoxTypes.SkipThis
                            , moProfile
                            , "-WhyTimerStopped"
                            , tvMessageBoxResults.OK
                            )
                        )
                {
                    this.chkUseTimer_SetChecked(false);
                    lbStopTimerforUI = true;
                }
            }

            return lbStopTimerforUI;
        }

        private void GetSetConfigurationWizardDefaults()
        {
            tvProfile loBackupSet1Profile = new tvProfile(moProfile.sValue("-BackupSet", "(not set)"));

            // Step 1
            if ("" == this.txtBackupFolder.Text)
                this.txtBackupFolder.Text = loBackupSet1Profile.sValue("-FolderToBackup",
                        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));

            // Step 2
            if ("" == this.txtBackupTime.Text)
            {
                this.txtBackupTime.Text = moProfile.sValue("-BackupTime", "12:00 AM");
                this.sldBackupTime_ValueFromString(this.txtBackupTime.Text);
            }

            // Step 3
            if ("" == this.txtBackupOutputFilename.Text)
                this.txtBackupOutputFilename.Text = loBackupSet1Profile.sValue("-OutputFilename",
                        string.Format("{0}Files", Environment.GetEnvironmentVariable("USERNAME")));

            if ("" == this.txtArchivePath.Text)
                this.txtArchivePath.Text = moDoGoPcBackup.sArchivePath();

            // Step 4
            DriveInfo[] loDrivesArray = DriveInfo.GetDrives();

            // This code could be improved by updating the CheckBoxes when the user 
            // removes or inserts external drives.
            if ( 0 == gridBackupDevices.Children.Count )
            {   
                int liRow = 0;
                int liColumn = 0;

                // Remove any checkboxes already there.
                gridBackupDevices.Children.Clear();

                // Add each drive (after B:) to the list of checkboxes.
                foreach (DriveInfo loDrive in loDrivesArray)
                {
                    try
                    {
                        if ( String.Compare(loDrive.Name, "B:\\") > 0 )
                        {
                            CheckBox    loCheckBox = new CheckBox();
                                        loCheckBox.Width = 200;
                            
                            // If the drive has a valid volume label, display it alongside the drive name.
                            try
                            {
                                loCheckBox.Content = "(" + loDrive.Name.Substring(0, 2) + ") " + loDrive.VolumeLabel;
                            }
                            // Otherwise, display the drive name alongside the word "DISK".
                            catch
                            {
                                loCheckBox.Content = "(" + loDrive.Name.Substring(0, 2) + ") " + "DISK";
                            }

                            // Add a CheckBox to the tab to represent the drive.
                            gridBackupDevices.Children.Add(loCheckBox);
                            Grid.SetRow(loCheckBox, liRow);
                            Grid.SetColumn(loCheckBox, liColumn);

                            // Arrange the CheckBoxes such that a new column is formed for every six CheckBoxes.
                            if (liRow < 5)
                            {
                                ++liRow;
                            }
                            else
                            {
                                liRow = 0;
                                ++liColumn;
                            }
                        
                            //Validate each drive by creating a temporary file.
                            string lsPathName = System.IO.Path.Combine(loCheckBox.Content.ToString().Substring(1, 2), 
                                                                        moDoGoPcBackup.sBackupDriveToken);

                            try
                            {
                                System.IO.File.Create(lsPathName).Close();
                                System.IO.File.Delete(lsPathName);
                                loCheckBox.Foreground = Brushes.DarkGreen;
                            }
                            catch
                            {
                                loCheckBox.Foreground = Brushes.Red;
                                loCheckBox.IsEnabled = false;
                            }
                        }
                    }
                    catch { }
                }
            }

            // Finish
            this.txtReviewBackupFolder.Text = this.txtBackupFolder.Text;
            this.txtReviewOutputFilename.Text = this.txtBackupOutputFilename.Text;
            this.txtReviewArchivePath.Text = this.txtArchivePath.Text;
            this.txtReviewBackupTime.Text = this.txtBackupTime.Text;

            string lsSelectedDrives = "";

            // Generate a string with the content of each CheckBox that the user checked in Step 4.
            foreach (CheckBox loCheckBox in gridBackupDevices.Children)
            {
                if ((bool)loCheckBox.IsChecked)
                {
                    lsSelectedDrives += loCheckBox.Content + " ";
                }
            }

            this.txtReviewAdditionalDrives.Text = lsSelectedDrives;

            loBackupSet1Profile["-FolderToBackup"] = this.txtReviewBackupFolder.Text;
            loBackupSet1Profile["-OutputFilename"] = this.txtReviewOutputFilename.Text;

            // Make the backup set a multi-line block by inserting newlines before hyphens.
            moProfile["-BackupSet"] = loBackupSet1Profile.sCommandLine().Replace("-", "\r\n    -") + "\r\n\r\n";

            moProfile["-ArchivePath"] = this.txtReviewArchivePath.Text;
            moProfile["-BackupTime"] = this.txtReviewBackupTime.Text;
            moProfile.Save();

            if ( !this.bMainLoopStopped )
                this.bMainLoopRestart = true;
        }

        private bool ValidateConfigurationWizardValues(bool abVerifyAllTabs)
        {
            string lsCaption = "Please Fix Before You Finish";
            string lsMessage = null;
            bool lbHaveMovedForward = this.ConfigWizardTabs.SelectedIndex >= miPreviousConfigWizardSelectedIndex;

            // Step 1 (backup folder)
            if (abVerifyAllTabs || lbHaveMovedForward && miPreviousConfigWizardSelectedIndex
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

                if (!Directory.Exists(txtBackupFolder.Text))
                    lsMessage += (null == lsMessage ? "" : Environment.NewLine + Environment.NewLine)
                            + "Step 1. Select a backup folder that exists."
                            ;
                if (txtBackupFolder.Text == Path.GetPathRoot(lsSystemFolderPrefix)
                        || txtBackupFolder.Text.StartsWith(lsSystemFolderPrefix)
                        || txtBackupFolder.Text.StartsWith(lsProgramsFolderPrefix)
                        )
                    lsMessage += (null == lsMessage ? "" : Environment.NewLine + Environment.NewLine)
                            + "Step 1. Select a backup folder that is not the system root,"
                            + " not in the system folder and not in the program files folder."
                            ;
            }

            // Step 2 (backup time)
            if (abVerifyAllTabs || lbHaveMovedForward && miPreviousConfigWizardSelectedIndex
                    == ItemsControl.ItemsControlFromItemContainer(
                    this.tabStep2).ItemContainerGenerator.IndexFromContainer(this.tabStep2)
                    )
            {
                DateTime ldtBackupTime;

                if (!DateTime.TryParse(txtBackupTime.Text, out ldtBackupTime))
                    lsMessage += (null == lsMessage ? "" : Environment.NewLine + Environment.NewLine)
                            + "Step 2. Select a valid backup time."
                            ;
            }

            // Step 3 (backup filename and archive folder).
            if (abVerifyAllTabs || lbHaveMovedForward && miPreviousConfigWizardSelectedIndex
                    == ItemsControl.ItemsControlFromItemContainer(
                    this.tabStep3).ItemContainerGenerator.IndexFromContainer(this.tabStep3)
                    )
            {
                try
                {   // Don't use "Path.Combine()" here since we want
                    // to test for path separators in the filename.
                    string lsPathfile = Path.GetDirectoryName(moProfile.sLoadedPathFile)
                                            + Path.DirectorySeparatorChar + txtBackupOutputFilename.Text;
                    FileStream loFileStream = File.Create(lsPathfile);
                    loFileStream.Close();
                    File.Delete(lsPathfile);
                }
                catch
                {
                    lsMessage += (null == lsMessage ? "" : Environment.NewLine + Environment.NewLine)
                            + "Step 3a. Select a backup filename that is valid for output files.";
                }

                try
                {
                    if (!Directory.Exists(txtArchivePath.Text))
                    {
                        Directory.CreateDirectory(txtArchivePath.Text);
                        Directory.Delete(txtArchivePath.Text);
                    }
                }
                catch
                {
                    lsMessage += (null == lsMessage ? "" : Environment.NewLine + Environment.NewLine)
                            + "Step 3b. Select a valid archive folder name.";
                }

                if (null != lsMessage)
                    lsMessage += Environment.NewLine + Environment.NewLine
                                + "Also, make sure you have adminstrator privileges to do this.";
            }

            if (null != lsMessage)
                tvMessageBox.ShowWarning(this, lsMessage, lsCaption);


            return null == lsMessage;
            
        }

        private void ConfigWizardTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.GetSetConfigurationWizardDefaults();
            this.ValidateConfigurationWizardValues(false);

            miPreviousConfigWizardSelectedIndex = this.ConfigWizardTabs.SelectedIndex;
        }

        private void loCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            (sender as CheckBox).Content = "test";
        }
        
        private void btnSetupDone_Click(object sender, RoutedEventArgs e)
        {
            if (this.ValidateConfigurationWizardValues(true)
                    && tvMessageBoxResults.Yes == tvMessageBox.Show(this, string.Format(@"
Are you sure you want to run the backup now?

You can continue this later wherever you left off. "
+ @" You can also edit the profile file directly (""{0}"") for"
+ @" much more detailed configuration (see ""Help"").
", Path.GetFileName(moProfile.sLoadedPathFile))
                    , "Run Backup", tvMessageBoxButtons.YesNo, tvMessageBoxIcons.Question)
                    )
            {
                //Create the token file in each additional drive that was selected from Step 4.
                foreach (CheckBox loCheckBox in gridBackupDevices.Children)
                {
                    string lsPathName = loCheckBox.Content.ToString().Substring(1, 2) + "\\" + moDoGoPcBackup.sBackupDriveToken;

                    if ( (bool)loCheckBox.IsChecked && !System.IO.File.Exists(lsPathName) )
                    {
                        System.IO.File.Create(lsPathName).Close();
                    }
                }

                moProfile["-AllConfigWizardStepsCompleted"] = true;
                moProfile.Save();

                this.HideMiddlePanels();
                this.MainButtonPanel.IsEnabled = true;

                this.DoBackup();
                this.CreateSysTrayIcon();
                this.MainLoop();
            }
        }

        private void ConfigDetailsTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
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

        private void HideMiddlePanels()
        {
            this.MiddlePanelConfigWizard.Visibility = Visibility.Collapsed;
            this.MiddlePanelConfigDetails.Visibility = Visibility.Collapsed;
            this.MiddlePanelTimer.Visibility = Visibility.Collapsed;
            this.MiddlePanelOutputText.Visibility = Visibility.Collapsed;
        }

        private void DisableButtons()
        {
            moStartStopButtonState.Content = this.btnDoBackupNow.Content;
            moStartStopButtonState.ToolTip = this.btnDoBackupNow.ToolTip;
            moStartStopButtonState.Background = this.btnDoBackupNow.Background;

            this.btnDoBackupNow.Content = "Stop";
            this.btnDoBackupNow.ToolTip = "Stop the Process Now";
            this.btnDoBackupNow.Background = Brushes.Red;

            this.btnSetup.IsEnabled = false;
            this.btnConfigureDetails.IsEnabled = false;
        }

        private void EnableButtons()
        {
            this.btnDoBackupNow.Content = moStartStopButtonState.Content;
            this.btnDoBackupNow.ToolTip = moStartStopButtonState.ToolTip;
            this.btnDoBackupNow.Background = moStartStopButtonState.Background;

            this.MainButtonPanel.IsEnabled = true;
            this.btnDoBackupNow.IsEnabled = true;
            this.btnSetup.IsEnabled = true;
            this.btnConfigureDetails.IsEnabled = true;
            this.btnShowArchive.IsEnabled = true;
            this.btnShowTimer.IsEnabled = true;
        }

        private void PopulateTimerDisplay(string asCommonValue)
        {
            lblNextBackupTime.Content = asCommonValue;
            lblNextBackupTimeLeft.Content = asCommonValue;

            if (moProfile.ContainsKey("-PreviousBackupTime"))
                lblPrevBackupTime.Content = moProfile["-PreviousBackupTime"];
            else
                lblPrevBackupTime.Content = mcsNoPreviousText;

            imgPrevBackupResultNull.Visibility = Visibility.Collapsed;
            imgPrevBackupResultPass.Visibility = Visibility.Collapsed;
            imgPrevBackupResultFail.Visibility = Visibility.Collapsed;

            if (!moProfile.ContainsKey("-PreviousBackupOk"))
            {
                lblPrevBackupResult.Content = "unfinished";
                imgPrevBackupResultNull.Visibility = Visibility.Visible;
            }
            else
            {
                if (moProfile.bValue("-PreviousBackupOk", false))
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
            if (this.bBackupRunning || (abShowBackupRan && mbBackupRan))
                this.MiddlePanelOutputText.Visibility = Visibility.Visible;
        }

        private void ShowHelp()
        {
            if (null != moHelpWindow)
                moHelpWindow.Close();

            moHelpWindow = new Help();
            moHelpWindow.WindowState = WindowState.Minimized;
            System.Windows.Forms.Application.DoEvents();
            moHelpWindow.Show();
            System.Windows.Forms.Application.DoEvents();
            moHelpWindow.WindowState = WindowState.Normal;
        }

        private void ShowPreviousBackupStatus()
        {
            mbPreviousBackupError = false;

            // "ContainsKey" is used here to prevent "DateTime.MinValue"
            // being written as the default value for -PreviousBackupTime.

            // If the backup just finished (ie. less than 1 minute ago), don't bother.
            if (!moProfile.ContainsKey("-PreviousBackupTime") || (DateTime.Now - moProfile.dtValue(
                    "-PreviousBackupTime", DateTime.MinValue)).Minutes > 1)
            {
                if (!moProfile.ContainsKey("-PreviousBackupOk"))
                {
                    mbPreviousBackupError = true;

                    tvMessageBox.ShowError(this, "The previous backup did not complete. Check the log."
                            + moDoGoPcBackup.sSysTrayMsg, "Backup Failed");
                }
                else
                {
                    if (!moProfile.bValue("-PreviousBackupOk", false))
                    {
                        mbPreviousBackupError = true;

                        tvMessageBox.ShowError(this, "The previous backup failed. Check the log for errors."
                                + moDoGoPcBackup.sSysTrayMsg, "Backup Failed");
                    }
                    else
                    {
                        int liPreviousBackupDays = (DateTime.Now - moProfile.dtValue("-PreviousBackupTime"
                                                        , DateTime.MinValue)).Days;

                        tvMessageBox.ShowModeless(this, string.Format(
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
        }

        private void CreateSysTrayIcon()
        {
            if (null == moNotifyIcon)
            {
                moNotifyIcon = new ExtendedNotifyIcon();
                moNotifyIcon.MouseClick += new ExtendedNotifyIcon.MouseClickHandler(moNotifyIcon_OnShowWindow);
                moNotifyIcon.StopMouseMoveEventFromFiring();
                moNotifyIcon.StopMouseLeaveEventFromFiring();
                moNotifyIcon.targetNotifyIcon.Icon = new System.Drawing.Icon(
                        Application.GetResourceStream(new Uri(
                        "pack://application:,,,/Resources/images/GoPC.ico")).Stream);
                moNotifyIcon.targetNotifyIcon.Text = "GoPC Backup";
            }
        }


        private void DoBackup()
        {
            this.bBackupRunning = true;

            if (moProfile.bValue("-CleanupFiles", true) && Visibility.Hidden != this.Visibility)
                tvMessageBox.ShowBriefly(this, "The file cleanup process is now running ..."
                        , "Backup Started", tvMessageBoxIcons.Information, 2);

            this.InitProgressBar(moDoGoPcBackup.iCleanupFilesCount());

            if (!moDoGoPcBackup.CleanupFiles())
            {
                this.InitProgressBar(0);
            }
            else
            {
                this.InitProgressBar(moDoGoPcBackup.iBackupFilesCount());

                if (!moDoGoPcBackup.BackupFiles())
                    this.InitProgressBar(0);
            }

            this.bBackupRunning = false;
        }

        private void MainLoop()
        {
            if (!this.bMainLoopStopped
                    || !(bool)this.chkUseTimer.IsChecked
                    || this.bBackupRunning
                    )
                return;

            do
            {
                // "bMainLoopRestart" is used only once (if at all) then immediately
                // reset. It's for breaking out of the inner loop as needed (see below).
                this.bMainLoopRestart = false;

                try
                {
                    DateTime ldtNextStart = DateTime.MinValue;

                    this.bMainLoopStopped = false;

                    try
                    {
                        ldtNextStart = moProfile.dtValue("-BackupTime", DateTime.MinValue);

                        if (DateTime.MinValue == ldtNextStart)
                        {
                            ldtNextStart = DateTime.Now;
                        }
                        else
                        {
                            // Wait until the given date (if in the future).
                            // Otherwise wait until later today or tomorrow.

                            // This is done by parsing out the date string
                            // from the starting datetime string to get the
                            // given time for the current date.
                            if (ldtNextStart < DateTime.Now)
                                ldtNextStart = DateTime.Parse(ldtNextStart.ToString().Replace(
                                        ldtNextStart.ToShortDateString(), null));

                            // If the given time has passed, wait until the
                            // the same time tomorrow.
                            if (ldtNextStart < DateTime.Now)
                                ldtNextStart = ldtNextStart.AddDays(1);
                        }
                    }
                    catch (Exception ex)
                    {
                        this.bMainLoopStopped = true;

                        tvMessageBox.ShowError(this, ex.Message, "Error Setting Timer");
                    }

                    do
                    {
                        if (!this.bMainLoopStopped)
                        {
                            System.Windows.Forms.Application.DoEvents();
                            System.Threading.Thread.Sleep(moProfile.iValue("-MainLoopSleepMS", 200));

                            string lsTimeLeft = ((TimeSpan)(ldtNextStart - DateTime.Now)).ToString();
                            lblNextBackupTimeLeft.Content = lsTimeLeft.Substring(0, lsTimeLeft.Length - 8);

                            // "ContainsKey" is used here to avoid storing "mcsNoPreviousText" as the 
                            // default -PreviousBackupTime. Doing so would blow an error next time.
                            if (moProfile.ContainsKey("-PreviousBackupTime"))
                                lblPrevBackupTime.Content = moProfile["-PreviousBackupTime"];
                            else
                                lblPrevBackupTime.Content = mcsNoPreviousText;

                            if (ldtNextStart > DateTime.Now)
                            {
                                if (!this.bBackupRunning)
                                    lblTimerStatus.Content = mcsWaitingText;

                                lblNextBackupTime.Content = (DateTime.Today.Date == ldtNextStart.Date ? ""
                                        : ldtNextStart.DayOfWeek.ToString())
                                        + " " + ldtNextStart.ToShortTimeString();
                            }
                            else
                            {
                                ldtNextStart = DateTime.Now.AddMinutes(moProfile.iValue("-MainLoopMinutes", 1440));
                                lblNextBackupTime.Content = (DateTime.Today.Date == ldtNextStart.Date ? ""
                                        : ldtNextStart.DayOfWeek.ToString())
                                        + " " + ldtNextStart.ToShortTimeString();

                                this.DoBackup();
                            }
                        }
                    }
                    while (!this.bMainLoopStopped && !this.bMainLoopRestart);
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
            while (!this.bMainLoopStopped);

            this.PopulateTimerDisplay(mcsStoppedText);
        }
    }
}
