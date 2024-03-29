using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using tvToolbox;


namespace GoPcBackup
{
    /// <summary>
    /// GoPcBackup.exe
    ///
    /// Run this program. It will prompt to create a folder with profile file:
    ///
    ///     GoPcBackup.exe.txt
    ///
    /// The profile will contain help (see -Help) as well as default options.
    ///
    /// Note: This software creates its own support files (including DLLs or
    ///       other EXEs) in the folder that contains it. It will prompt you
    ///       to create its own desktop folder when you run it the first time.
    /// </summary>


    /// <summary>
    /// File timestamp types.
    /// </summary>
    public enum FileDateTimeTypes
    {
          CreationTime
        , LastAccessTime
        , LastWriteTime
    }


    /// <summary>
    /// Backup new files after cleaning up old files.
    /// </summary>
    public class DoGoPcBackup : Application
    {
        private const string        mcsBackupBeginScriptPathFileDefault = "GoPcBackupBegin.cmd";
        private const string        mcsBackupDoneScriptPathFileDefault  = "GoPcBackupDone.cmd";
        private const string        mcsBackupFailedScriptPathFileDefault= "GoPcBackupFailed.cmd";
        private const string        mcsStartupScript                    = "Startup.cmd";
        private string[]            msZipToolExeFilenamesArray          = {"7za.exe", "7za.chm"};

        private bool                mbAddTasksStartupDelayDone;
        private bool                mbHasNoDeletionGroups;
        private int                 miAddTasksNextTotalMinutes          = int.MinValue;
        private int                 miBackupSets;
        private int                 miBackupSetsGood;
        private int                 miBackupSetsRun;
        private string              msCurrentBackupOutputPathFile;

        private Thread              moBackgroundThread;
        private Timer               moBackgroundLoopTimer;
        private tvProfile           moCurrentBackupSet;
        private tvProfile           moAddTasksProfile;
        private Process[]           moAddTasksProcessArray;


        private DoGoPcBackup()
        {
            tvMessageBox.ShowError(null, "Don't use this constructor!");
        }


        /// <summary>
        /// This constructor expects a profile object to be provided.
        /// </summary>
        /// <param name="aoProfile">
        /// The given profile object must either contain runtime options
        /// or it will be returned filled with default runtime options.
        /// </param>
        public DoGoPcBackup(tvProfile aoProfile)
        {
            moProfile = aoProfile;

            moBackgroundThread = new Thread(new ThreadStart(moBackgroundThread_Start));
            moBackgroundThread.Start();

            this.SessionEnding += new SessionEndingCancelEventHandler(DoGoPcBackup_SessionEnding);
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            DoGoPcBackup    loMain  = null;
            tvProfile       loProfile = null;

            try
            {
                bool        lbFirstInstance;
                            Mutex loMutex = new Mutex(false, "Global\\" + Application.ResourceAssembly.GetName().Name, out lbFirstInstance);
                            if ( !lbFirstInstance )
                                DoGoPcBackup.ActivateAlreadyRunningInstance(args);
                            else
                                loProfile = new tvProfile(args);

                if ( lbFirstInstance && !loProfile.bExit )
                {
                    loProfile.GetAdd("-Help",
                            @"
Introduction


This utility performs file backups and file cleanups in the background.

It also acts as its own scheduler. First, it checks for files to be removed on
a given schedule. Then it runs a backup of your files automatically.

There is no need to use a job scheduler unless this software is running on a 
server computer that has no regular user activity (see -NoPrompts and -RunOnce
below).

You provide various file specifications (ie. locations of files to backup and
to cleanup) as well as file age limits for the files to cleanup. The rest is
automatic.

This utility will run in the background unless its timer is turned off. Its
simple user interface (UI) is usually minimized to the system tray.


Command-Line Usage


Open this utility's profile file to see additional options available. It is
usually located in the same folder as ""{EXE}"" and has the same name
with "".txt"" added (see ""{INI}"").

Profile file options can be overridden with command-line arguments. The
keys for any ""-key=value"" pairs passed on the command-line must match
those that appear in the profile (with the exception of the ""-ini"" key).

For example, the following invokes the use of an alternative profile file:

    {EXE} -ini=NewProfile.txt

This tells the software to run in automatic mode:

    {EXE} -AutoStart


Author:  George Schiro (GeoCode@Schiro.name)

Date:    7/3/2013




Options and Features


The main options for this utility are listed below with their default values.
A brief description of each feature follows.

-AddTasks= NO DEFAULT VALUE

    Each added task has its own profile:

    -Task= NO DEFAULT VALUE

        -CommandEXE= NO DEFAULT VALUE

            This is the path\file specification of the task executable to be run.

        -CommandArgs=""""

            This is the list of arguments passed to the task executable.

        -CommandWindowTitle=""""

            This is the main window title for this instance of the task executable.
            This will be used to determine, during startup, if the task is already
            running (when multiple instances of the same executable are found).

        -CreateNoWindow=False

            Set this switch True and nothing will be displayed when the task runs.

            Note: This switch applies only to console applications.
                  It is ignored for other app types (ie. windows apps).

        -DelaySecs=0

            Set this value to greater than zero to delay this task by that number of
            seconds - during startup only (ie. -OnStartup is true). If -OnStartup is
            true (see below) this delay will be in addition to the general startup
            tasks delay (if any, see -StartupTasksDelaySecs below).

            Note: Task delays are cumulative. Each task delayed causes subsequent
                  tasks to be delayed as well (again, during startup only).

        -OnStartup=False

            Set this switch True and the task will start each time ""{EXE}""
            starts. If the task EXE is already running, it will not be started again.

        -RerunMins=0

            Set this value to greater than zero to have this task run again every
            this many minutes, indefinitely.

        -RerunLimit=0

            Set this value to greater than zero to have this task run again this
            many times (once every -RerunMins, see above). This value is ignored
            if -RerunMins is zero.

        -StartTime= NO DEFAULT VALUE

            Set this to the time of day to run the task (eg. 3:00am, 9:30pm, etc).

        -StartDays=""""

            Set this to days of the week to run the task (eg. Monday, Friday, etc).
            This value may include a comma-separated list of days as well as ranges
            of days. Leave this blank and the task will run every day at -StartTime.

        -TaskDisabled=False

            Set this switch True to skip this task indefinitely (ie. until this
            switch is removed or set False again).

        -TimeoutMinutes=0

            Set this to a number greater than zero to have the task run to completion
            and have all output properly logged before proceeding to the next task
            (ie. -CreateNoWindow will be set and IO redirection will be handled).

        -UnloadOnExit=False

            Set this switch True and the task executable will be removed from memory
            (if it's still running) when ""{EXE}"" exits.

        -WindowStyle=Normal

            Alternatively, you can set this to ""Minimized"" or ""Maximized"".


        Here's an example:

        -AddTasks=[

            -Task= -OnStartup -CommandEXE=http://xkcd.com
            -Task= -StartTime=6:00am -CommandEXE=shutdown.exe -CommandArgs=/r /t 60
            -Task= -StartTime=7:00am -StartDays=""Mon-Wed,Friday,Saturday"" -CommandEXE=""C:\Program Files\Calibre2\calibre.exe""  -Note=Fetch NY Times after 6:30am
            -Task= -StartTime=8:00am -StartDays=""Sunday"" -CommandEXE=""C:\Program Files\Calibre2\calibre.exe""  -Note=Fetch NY Times after 7:30am Sundays

        -AddTasks=]

-ArchivePath=C:\Archive

    This is the destination folder of the backup output files unless
    overridden in -BackupSet (see below).

-AutoStart=True

    This tells the software to run in automatic mode. Set this switch False
    and the main loop in the UI will only start manually. The software will
    also vacate memory after the UI window closes. This is the timer switch.

-BackupBeginScriptEnabled=True

    Set this switch False to skip running the ""backup begin"" script.

-BackupBeginScriptHelp= SEE PROFILE FOR DEFAULT VALUE

    This is the default content of the DOS script that is initially written to
    -BackupBeginScriptPathFile and run before each backup starts. It contains
    a description of the command-line arguments passed to the script at runtime.

-BackupBeginScriptInit=False

    Set this switch True and the ""backup begin"" script will be automatically
    overwritten from the content of -BackupBeginScriptHelp. Once used this switch
    will be reset to False.

    Note: The content of -BackupBeginScriptHelp will also be overwritten
          from the default value embedded in the executable file.

-BackupBeginScriptPathFile=GoPcBackupBegin.cmd

    This DOS shell script is run before each backup starts. Edit the contents
    of the file or point this parameter to another file. If you delete the file,
    it will be recreated from the content found in -BackupBeginScriptHelp (see 
    above).

-BackupBeginScriptTimeoutMins=5

    This determines how many minutes the ""backup begin"" script will be
    permitted to run before throwing a timeout error.

-BackupDoneScriptEnabled=True

    Set this switch False to skip running the ""backup done"" script.

-BackupDoneScriptHelp= SEE PROFILE FOR DEFAULT VALUE

    This is the default content of the DOS script that is initially written to
    -BackupDoneScriptPathFile and run after each successful backup. It contains
    a description of the command-line arguments passed to the script at runtime.

-BackupDoneScriptInit=False

    Set this switch True and the ""backup done"" script will be automatically
    overwritten from the content of -BackupDoneScriptHelp. Once used this switch
    will be reset to False.

    Note: The content of -BackupDoneScriptHelp will also be overwritten
          from the default value embedded in the executable file.

-BackupDoneScriptPathFile=GoPcBackupDone.cmd

    This DOS shell script is run after each successful backup completes. You 
    can edit the contents of the file or point this parameter to another file.
    If you delete the file, it will be recreated from the content found in 
    -BackupDoneScriptHelp (see above).

-BackupDoneScriptTimeoutMins=180

    This determines how many minutes the ""backup done"" script will be
    permitted to run before throwing a timeout error.

-BackupFailedScriptEnabled=True

    Set this switch False to skip running the ""backup failed"" script.

-BackupFailedScriptHelp= SEE PROFILE FOR DEFAULT VALUE

    This is the default content of the DOS script that is initially written to
    -BackupFailedScriptPathFile and run after each failed backup. It contains
    a description of the command-line arguments passed to the script at runtime.

-BackupFailedScriptInit=False

    Set this switch True and the ""backup failed"" script will be automatically
    overwritten from the content of -BackupFailedScriptHelp. Once used this switch
    will be reset to False.

    Note: The content of -BackupFailedScriptHelp will also be overwritten
          from the default value embedded in the executable file.

-BackupFailedScriptPathFile=GoPcBackupFailed.cmd

    This DOS shell script is run after a backup fails to complete. You can
    edit the contents of the file or point this parameter to another file.
    If you delete the file, it will be recreated from the content found in 
    -BackupFailedScriptHelp (see above).

-BackupFailedScriptTimeoutMins=1

    This determines how many minutes the ""backup failed"" script will be
    permitted to run before throwing a timeout error.

-BackupDriveToken=(This is my GoPC backup drive.)

    This is the filename looked for at the root of every storage device attached
    to the computer. If found, a copy of the backup will be written there.

-BackupFileSpec=*

    This wildcard is appended to folders to backup (see -FolderToBackup).

-BackupFiles=True

    Set this switch False to disable backups (ie. do file cleanups only).

-BackupOutputExtension=.zip

    This is appended to all backup path\filenames (see -OutputFilename below).

-BackupOutputFilenameDateFormat=-yyyy-MM-dd

    This format string is used to form the variable part of each backup
    output filename. It is inserted between the filename and the extension
    (see -OutputFilename below and -BackupOutputExtension above).

-BackupSet=""One of many file sets to backup goes here.""

    Each file backup set has its own profile:

        -ArchivePath= INHERITED

            This is the destination folder of the backup output files. If
            provided, this value will override the parent -ArchivePath (see
            above).

        -BackupFileSpec= INHERITED

            This wildcard is appended to each folder to backup. If provided,
            it will override the parent -BackupFileSpec (see above).

        -FolderToBackup=""One of many folders to backup goes here.""

            This is the full path\file specification of a folder to backup.
            This parameter can appear multiple times in each backup set.

            Instead of an entire folder you can use a path\file pattern like
            ""C:\Folder\File?.*"" to backup a subset of files or a single file.

        -OutputFilename=Files

            This is the backup output filename with no path and no extension.
            This parameter will be combined with -BackupOutputExtension, 
            -ArchivePath and -BackupOutputFilenameDateFormat to produce a
            full backup output path\file specification.

-BackupTime=12:00 AM

    This is the time each day the backup starts.

-BackupTimeMinsPerTick=15

    This determines how many minutes the backup time changes with each tick
    of the backup time selection slider in the UI.

-BackupTimeoutMins=60

    This determines how many minutes the backup will be permitted to run
    before throwing a timeout error.

-CleanupFiles=True

    Set this switch False to disable cleanups (ie. do file backups only).

-CleanupLoopSleepMS=1

    This is the number of milliseconds of process thread sleep time between
    file deletions. The default of 1 ms should result in rapid deletions. You 
    can increase this value if you are concerned the UI is not responsive
    enough or the process is using too much CPU while deleting.

-CleanupSet=""One of many file sets to cleanup goes here.""

    Each file cleanup set has its own profile:

        -AgeDays=365000

            This is the maximum file age in days. It is 1000 years by default.
            Only files older than this will be considered for deletion.

        -ApplyDeletionLimit=True

            Set this switch False and the cleanup process won't limit deletions
            to files regularly replaced by newer files. Without such a limit
            a large collection of very old files may be wiped out in one run.
            With the limit in place an old file will be removed only if a newer
            file exists to replace it.

            In other words, with this switch set, there should always be as many
            files retained as there are days in ""-AgeDays"" multiplied by the
            frequency of backups (1440 divided by -MainLoopMinutes, see below).

        -CleanupHidden=False

            Set this switch True and the file cleanup process will include
            hidden files as well. If -Recurse is also True (see below), hidden
            folders will also be removed.

        -CleanupReadOnly=False

            Set this switch True and the file cleanup process will include
            read-only files as well. If -Recurse is also True (see below), 
            read-only folders will also be removed.

        -DeletedFileListDateTimeType=LastWriteTime

            Each file has 3 timestamps: CreationTime, LastAccessTime
            and LastWriteTime.

            The default (""LastWriteTime"") is the file modification date.

        -FilesToDelete=""One of many path\file specifications goes here.""

            These are the files evaluated for deletion based on their age
            (see -AgeDays above). Wildcards are expected but not required
            (you can reference a single file if you like).

            Note: The -ApplyDeletionLimit switch (see above) applies to
                  each occurrence of this value separately. In other words,
                  each -FilesToDelete file specification is evaluated for
                  a deletion limit count as a separate collection of files
                  independent of all other -FilesToDelete collections.

        -Recurse=False

            Set this switch True and the file cleanup process will recurse 
            through all subdirectories starting from the path of the given
            -FilesToDelete (see above) looking for files to remove with the
            same file specification found in the -FilesToDelete parameter.

        -RecurseFolder=""""

            This identifies subfolders found within the folder defined by
            -FilesToDelete (see above). These subfolders can occur at any
            level of the directory hierarchy above the path provided in
            -FilesToDelete. If -RecurseFolder is non-empty, only files found
            within the -FilesToDelete path and also within any subfolder
            (from the given path upward) with the given subfolder name will
            be evaluated for removal.

            This parameter has the effect of greatly limiting the recursion.

            BE CAREFUL! If you don't provide this parameter or you specify
            an empty value, all files matching the file specification in the
            given -FilesToDelete at every level of the directory structure
            (starting at the path found in -FilesToDelete) will be evaluated
            for removal.

            IMPORTANT! Every empty folder found within the given recursive
            subfolder (at any level above the starting path) will also be
            removed.

        -RemoveRecurseFolder=False

            Set this switch True and the file cleanup process will remove 
            empty recurse folders as well.


        Here's a single line example:

        -CleanupSet= -AgeDays=14 -ApplyDeletionLimit=False -FilesToDelete=C:\WINDOWS\TEMP\*.*


        Here's a multi-line example:

        -CleanupSet=[

            -AgeDays=60
            -FilesToDelete=C:\WINDOWS\system32\*.log
            -FilesToDelete=C:\WINDOWS\system32\LogFiles\W3SVC1\*.log

        -CleanupSet=]
        -CleanupSet=[

            -AgeDays=14
            -ApplyDeletionLimit=False
            -FilesToDelete=C:\Documents and Settings\*.*
            -CleanupHidden
            -CleanupReadOnly
            -Recurse
            -RecurseFolder=Local Settings\Temp

        -CleanupSet=]
        -CleanupSet=[

            -AgeDays=90
            -FilesToDelete=C:\Archive\*.*

        -CleanupSet=]

-DeletedFileListDateFormat=-yyyy-MM-dd

    This format string is used to form the variable part of each deleted file
    list output filename (see -DeletedFileListOutputPathFile below). It is 
    inserted between the filename and the extension.

-DeletedFileListOutputColumnFormat={0, -22:MM-dd-yyyy hh:mm:ss tt}  {1, 13:#,#}  {2}

    This format string specifies the layout of the three file attribute output 
    columns (last modified date, file size and path\file name, resp.) for each
    file in the deleted file list (see -DeletedFileListOutputPathFile below).

-DeletedFileListOutputColumnHeaderArray=Deleted File Time,File Size,Former File Location

    This array of names specifies the column headers of the deleted file list
    (see -DeletedFileListOutputPathFile below).

-DeletedFileListOutputHeader={0, -10:MM-dd-yyyy} File Cleanup List

    This format string specifies the layout of the deleted file list output file
    header (see -DeletedFileListOutputPathFile below).

-DeletedFileListOutputPathFile=Logs\DeletedFileList.txt

    This is the output path\file that will contain the list of deleted files.
    The profile file name will be prepended to the default and the current date
    (see -DeletedFileListDateFormat) will be inserted between the filename and 
    the extension.

-FetchSource=False

    Set this switch True to fetch the source code for this utility from the EXE.
    Look in the containing folder for a ZIP file with the full project sources.

-Help= SEE PROFILE FOR DEFAULT VALUE

    This help text.

-KillProcessOrderlyWaitSecs=30

    This is the maximum number of seconds given to a process after a ""close""
    command is given before the process is forcibly terminated.

-KillProcessForcedWaitMS=1000

    This is the maximum milliseconds to wait while force killing a process.

-LogEntryDateTimeFormatPrefix""yyyy-MM-dd hh:mm:ss:fff tt  ""

    This format string is used to prepend a timestamp prefix to each log entry in
    the process log file (see -LogPathFile below).    

-LogFileDateFormat=-yyyy-MM-dd

    This format string is used to form the variable part of each backup / cleanup
    log file output filename (see -LogPathFile below). It is inserted between the
    filename and the extension.

-LogPathFile=Logs\Log.txt

    This is the output path\file that will contain the backup / cleanup process
    log. The profile file name will be prepended to the default and the current
    date (see -LogFileDateFormat above) will be inserted between the filename 
    and the extension.

-MainLoopMinutes=1440

    This is the number of minutes until the next run. One day is the default.

-MainLoopSleepMS=100

    This is the number of milliseconds of process thread sleep wait time between
    loops. The default of 100 ms should be a happy medium between a responsive
    overall UI and a responsive process timer UI. You can increase this value
    if you are concerned the timer UI is using too much CPU while waiting.

-NoPrompts=False

    Set this switch True and all pop-up prompts will be suppressed. Messages
    are written to the log instead (see -LogPathFile above). You must use this
    switch whenever the software is run via a server computer batch job or job
    scheduler (ie. where no user interaction is permitted).

-PreviousBackupDevicesMissing=False

    This is the True or False ""Devices Missing"" status of the previous backup 
    run. If True, at least one external device was missing when the backup ran.

-PreviousBackupOk= NO DEFAULT VALUE

    This is the True or False ""Ok"" status of the previous backup / cleanup run.

-PreviousBackupTime= NO DEFAULT VALUE

    This is the completion timestamp of the previous backup / cleanup run.

-RunOnce=False

    Set this switch True to run this utility one time only (with no UI) then
    shutdown automatically thereafter. This switch is useful if the utility
    is run in a batch process or if it is run by a server job scheduler.

-RunOncePrompts=True

    Set this switch False to not display a backup results dialog after -RunOnce
    is used. This switch is overridden by the -NoPrompts switch (see above).

-SaveProfile=True

    Set this switch False to prevent saving to the profile file by the backup
    software itself. This is not recommended since backup status information is 
    written to the profile after each backup runs.

-SaveSansCmdLine=True

    Set this switch False to allow merged command-lines to be written to
    the profile file (ie. ""{INI}""). When True, everything
    but command-line keys will be saved.

-SelectedBackupDevices= NO DEFAULT VALUE

    This is the list of selected backup devices as human readable text.

-SelectedBackupDevicesBitField=0 (0 means not yet set)

    This is the list of selected backup devices as a bit field. All bit fields
    have a leading 1 bit to preserve leading zeros. The second bit starts the
    device list (ie. drive letter list). Drive C: is not available as a device
    for additional (external) backups. So the second bit identifies drive D:.

-ShowBackupBeginScriptErrors=True

    Set this switch False to suppress the pop-up display of ""backup begin"" script
    errors (see -BackupBeginScriptPathFile above).

-ShowBackupDoneScriptErrors=True

    Set this switch False to suppress the pop-up display of ""backup done"" script
    errors (see -BackupDoneScriptPathFile above).

-ShowDeletedFileList=False

    Set this switch True and the list of deleted files will be displayed in a 
    pop-up window.

-ShowLogAfterReattachment=False

    Set this switch True to display the full process log file (see -LogPathFile
    above) whenever a backup device is reattached. This can be helpful to decide
    if the ""backup done"" script should be rerun (see -BackupDoneScriptPathFile
    above) after a missing backup device has been reattached to this PC.

-ShowProfile=False

    Set this switch True to immediately display the entire contents of the profile
    file at startup in command-line format. This may be helpful as a diagnostic.

-StartupTasksDelaySecs=0

    Set this value to greater than zero to delay all task scheduler defined startup
    tasks by that number of seconds (see -AddTasks above).

-StartupTasksDisabled=False

    Set this switch True to skip any and all startup tasks that may be defined by
    the task scheduler subsystem (see -AddTasks above).

-UpgradeKeysToCopy= SEE PROFILE FOR DEFAULT VALUE

    This list of profile keys is used during an upgrade to a new ""{EXE}"".
    The new software is typically tested on the desktop. The ""Upgrade"" button
    in the setup wizard is used to import this list of keys into the new profile
    prior to moving the new software to the application folder (to overwrite the
    old version via ""Setup Application Folder.exe"").

-UseConnectVirtualMachineHost=False

    Set this switch True to force a connection to the virtual machine share archive
    before each backup starts (ie. during the ""backup begin"" script).

-UseVirtualMachineHostArchive=False

    Set this switch True and code will be added to the ""backup done"" script
    (see -BackupDoneScriptPathFile above) to copy backups to your virtual
    machine host computer (assuming you have one). Alternatively, any network
    share can be referenced instead (see -VirtualMachineHostArchivePath below).

-VirtualMachineHostArchivePath= NO DEFAULT VALUE

    This value is used within the ""backup done"" script to copy backups to the
    virtual machine host share (see -UseVirtualMachineHostArchive above).

    You may want to reference your VM host by IP address rather than by name.
    Doing so is often more reliable than using NetBIOS names on your local
    area network.

-VirtualMachineHostPassword= NO DEFAULT VALUE

    This value is the password used within the ""backup begin"" script to log
    into the virtual machine host share (see -UseConnectVirtualMachineHost
    above).

-VirtualMachineHostUsername= NO DEFAULT VALUE

    This value is the username used within the ""backup begin"" script to log
    into the virtual machine host share (see -UseConnectVirtualMachineHost
    above).

-XML_Profile=False

    Set this switch True to change the profile file from command-line format
    to XML format.

-ZipToolEXE=7za.exe

    This is the ZIP tool executable that performs the backup compression.

-ZipToolEXEargs=a -ssw -bb ""{{BackupOutputPathFile}}"" @""{{BackupPathFiles}}"" -w""{{BackupOutputPath}}""

    These are command-line arguments passed to the ZIP compression tool (see
    -ZipToolEXE above). The tokens (in curly brackets) are self-evident. They
    are replaced at runtime.

-ZipToolEXEargsMore= NO DEFAULT VALUE

    These are additional command line arguments for the ZIP tool. Using
    this parameter makes it easier to add functionality without changing
    the existing command line. A typical example would be to supply an 
    encryption password on the command line to ""{EXE}"" itself.

-ZipToolFileListFileDateFormat=-yyyy-MM-dd

    This format string is used to form the variable part of each file list
    output filename (see -ZipToolFileListPathFile below). It is inserted
    between the filename and the extension.

-ZipToolFileListPathFile=FileLists\ZipFileList.txt

    This is the file used to store the list of filenames to be compressed.
    The profile file name will be prepended to the default and the current
    date (see -ZipToolFileListFileDateFormat above) will be inserted (with
    a GUID) between the filename and the extension.

-ZipToolInit=False

    Set this switch True and the zip compression tool will be automatically
    overwritten from the content embedded in the executable file. Once used
    this switch will be reset to False.


Notes:

    There may be various other settings that can be adjusted also (user
    interface settings, etc). See the profile file (""{INI}"")
    for all available options.

    To see the options related to any particular behavior, you must run that
    part of the software first. Configuration options are added ""on the fly""
    (in order of execution) to ""{INI}"" as the software runs.

"
                            .Replace("{EXE}", Path.GetFileName(Application.ResourceAssembly.Location))
                            .Replace("{INI}", Path.GetFileName(loProfile.sActualPathFile))
                            .Replace("{{", "{")
                            .Replace("}}", "}")
                            );

                    DoGoPcBackup.LogIt(loProfile, "");
                    DoGoPcBackup.LogIt(loProfile, String.Format("{0} started.", Path.GetFileName(Application.ResourceAssembly.Location)));

                    if ( loProfile.bSaveEnabled )
                    {
                        // Fetch simple setup.
                        tvFetchResource.ToDisk(Application.ResourceAssembly.GetName().Name
                                , "Setup Application Folder.exe", null);

                        // Fetch startup script.
                        tvFetchResource.ToDisk(Application.ResourceAssembly.GetName().Name
                                , mcsStartupScript, null);

                        // Fetch startup MMC script.
                        tvFetchResource.ToDisk(Application.ResourceAssembly.GetName().Name
                                , "Startup.msc", null);
                    }

                        string  lsStartupScriptPathFile = loProfile.sRelativeToExePathFile(mcsStartupScript);
                        string  lsCurrentInstallationFolder = Path.GetDirectoryName(lsStartupScriptPathFile);
                        string  lsPreviousInstallationFolder = loProfile.sValue("-InstallationFolder", "{InstallationFolder}");
                                if ( File.Exists(lsStartupScriptPathFile) && lsCurrentInstallationFolder != lsPreviousInstallationFolder )
                                {
                                    try
                                    {
                                        if ( !String.IsNullOrEmpty(lsCurrentInstallationFolder) )
                                            File.WriteAllText(lsStartupScriptPathFile, File.ReadAllText(lsStartupScriptPathFile)
                                                    .Replace(lsPreviousInstallationFolder, lsCurrentInstallationFolder));
                                    }
                                    catch (ArgumentException ex)
                                    {
                                        // Ignore an empty lsPreviousInstallationFolder.
                                        if ( !String.IsNullOrEmpty(lsPreviousInstallationFolder) )
                                            throw ex;
                                    }
                                    catch (IOException ex)
                                    {
                                        // Ignore IO conflicts since mcsStartupScript is likely running.
                                        if ( !ex.Message.Contains("being used by another process") )
                                            throw ex;
                                    }

                                    if ( !String.IsNullOrEmpty(lsCurrentInstallationFolder) )
                                    {
                                        loProfile["-InstallationFolder"] = lsCurrentInstallationFolder;
                                        loProfile.Save();
                                    }
                                }

                    // Fetch source code.
                    if ( loProfile.bValue("-FetchSource", false) )
                        tvFetchResource.ToDisk(Application.ResourceAssembly.GetName().Name, "GoPcBackup.zip", null);

                    // Updates start here.
                    if ( loProfile.bFileJustCreated )
                    {
                        loProfile["-Update2020-12-19"] = true;
                        loProfile.Save();
                    }
                    else
                    {
                        if ( !loProfile.bValue("-Update2020-12-19", false) )
                        {
                            // Force use of latest version of 7-Zip.
                            loProfile["-ZipToolInit"] = true;

                            loProfile["-Update2020-12-19"] = true;
                            loProfile.Save();
                        }
                    }
                    // Updates end here.

                    if ( !loProfile.bExit )
                    {
                        if ( loProfile.bValue("-RunOnce", false) )
                        {
                            // Run in batch mode.

                            bool            lbBackupResult = false;
                            DoGoPcBackup    loDoDa = new DoGoPcBackup(loProfile);
                                            loDoDa.CleanupFiles();
                                            lbBackupResult = loDoDa.BackupFiles();

                            if ( loProfile.bValue("-RunOncePrompts", true) && !loProfile.bValue("-NoPrompts", false) )
                            {
                                if ( lbBackupResult )
                                {
                                    tvMessageBox.Show(null, "Backup finished successfully.");
                                }
                                else
                                {
                                    Environment.ExitCode = 1;
                                    tvMessageBox.ShowError(null, "Backup failed. Check log for errors.");
                                }
                            }
                        }
                        else
                        {
                            // Run in interactive mode.

                            try
                            {
                                loMain = new DoGoPcBackup(loProfile);

                                // Load the UI.
                                UI  loUI = new UI(loMain);
                                    loMain.oUI = loUI;
                                    loMain.MainWindow = loUI;
                                    loMain.ShutdownMode = System.Windows.ShutdownMode.OnMainWindowClose;

                                loMain.Run(loUI);
                            }
                            catch (ObjectDisposedException) {}

                            GC.KeepAlive(loMutex);
                        }
                    }
                }
            }
            catch (SecurityException ex)
            {
                Environment.ExitCode = 1;
                DoGoPcBackup.LogIt(loProfile, DoGoPcBackup.sExceptionMessage(ex));

                if ( null == loProfile || !loProfile.bValue("-NoPrompts", false) )
                    tvFetchResource.NetworkSecurityStartupErrorMessage();
            }
            catch (Exception ex)
            {
                Environment.ExitCode = 1;
                DoGoPcBackup.LogIt(loProfile, DoGoPcBackup.sExceptionMessage(ex));

                if ( null == loProfile || !loProfile.bValue("-NoPrompts", false) )
                    tvFetchResource.ErrorMessage(null, ex.Message);
            }
            finally
            {
                if ( null != loMain && null != loMain.oUI )
                    loMain.oUI.Close();
            }
        }

        [DllImport("user32")]
        public static extern int RegisterWindowMessage(string message);
        public static readonly int WM_SHOWME = RegisterWindowMessage("WM_SHOWME_GoPcBackup");
        [DllImport("user32")]
        public static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);
        public static readonly int HWND_BROADCAST = 0xffff;
     
        private static void ActivateAlreadyRunningInstance(string[] args)
        {
            // Should we activate the already running instance or start a new "run once" instance?
            bool lbDoRunOnce = 0 != args.Length;
            if ( lbDoRunOnce )
            {
                tvProfile   loProfile = null;
                tvProfile   loCmdLine = new tvProfile(args, tvProfileDefaultFileActions.NoDefaultFile, tvProfileFileCreateActions.NoFileCreate);
                            if ( loCmdLine.bDefaultFileReplaced )
                            {
                                if ( loCmdLine.Count > args.Length )
                                {
                                    // The replacement profile has already been populated.
                                    loProfile = loCmdLine;
                                }
                                else
                                {
                                    // Use the given (empty) replacement profile and populate it.
                                    loProfile = new tvProfile(loCmdLine.sCommandLineArray(), tvProfileDefaultFileActions.NoDefaultFile, tvProfileFileCreateActions.NoFileCreate);
                                    loProfile.sActualPathFile = loCmdLine.sActualPathFile;
                                    loProfile.sLoadedPathFile = loCmdLine.sActualPathFile;
                                }

                                // Start a new "-RunOnce" instance with no limitations.
                                loProfile.LoadFromCommandLine("-ActivateAlreadyRunningInstance", tvProfileLoadActions.Merge);
                            }
                            else
                            if (       !loCmdLine.ContainsKey("-BackupFiles")
                                    && !loCmdLine.ContainsKey("-BackupSet")
                                    && !loCmdLine.ContainsKey("-CleanupFiles")
                                    && !loCmdLine.ContainsKey("-CleanupSet")
                                    && !loCmdLine.ContainsKey("-RunOnce")
                                    && !loCmdLine.ContainsKey("-RunOncePrompts")
                                    )
                            {
                                // Whatever was passed on the command-line is apparently unusable (ie. any given -ini file must already exist - if app is already running).
                                lbDoRunOnce = false;
                            }
                            else
                            {
                                // Get the default profile and use it to load the backup profile.
                                loProfile = new tvProfile(args);

                                // No INI? Use the backup profile instead as a full default profile.
                                string      lsPath = Path.GetDirectoryName(loProfile.sDefaultPathFile);
                                string      lsFilename = Path.GetFileName(loProfile.sDefaultPathFile);
                                string      lsExt = Path.GetExtension(loProfile.sDefaultPathFile);
                                string      lsBackupPathFile = Path.Combine(lsPath, lsFilename) + ".backup" + lsExt;

                                loProfile = new tvProfile(lsBackupPathFile, tvProfileFileCreateActions.NoFileCreate);
                                loProfile.LoadFromCommandLineArray(args, tvProfileLoadActions.Merge);

                                // Start a new "-RunOnce" instance, with sensible limitations.
                                loProfile.LoadFromCommandLine("-ActivateAlreadyRunningInstance -SaveProfile=False -StartupTasksDisabled -BackupBeginScriptEnabled=False -BackupDoneScriptEnabled=False -BackupFailedScriptEnabled=False"
                                                                , tvProfileLoadActions.Merge);
                            }

                if ( lbDoRunOnce )
                {
                    bool            lbBackupResult = false;
                    bool            lbCleanupResult = false;
                    DoGoPcBackup    loDoDa = new DoGoPcBackup(loProfile);
                                    lbCleanupResult = loDoDa.CleanupFiles();
                                    if ( lbCleanupResult )
                                        lbBackupResult = loDoDa.BackupFiles();

                    if ( !loCmdLine.bExit && loProfile.bValue("-RunOncePrompts", true) && !loProfile.bValue("-NoPrompts", false) )
                    {
                        string lsTitle = Path.GetFileNameWithoutExtension(loProfile.sExePathFile);

                        if ( lbBackupResult && lbCleanupResult )
                            tvMessageBox.Show(null, "Backup/Cleanup finished successfully.", lsTitle);
                        else
                            tvMessageBox.ShowError(null, "Backup/Cleanup failed. Check log for errors.", lsTitle);
                    }
                }
            }

            if ( !lbDoRunOnce )
            {
                // This activates the previous instance before exiting.
                PostMessage((IntPtr)HWND_BROADCAST, WM_SHOWME, IntPtr.Zero, IntPtr.Zero);
            }
        }

        /// <summary>
        /// Write the given asMessageText to a text file as well as
        /// to the output console of the UI window (if it exists).
        /// </summary>
        /// <param name="asMessageText">The text message string to log.</param>
        public static void LogIt(tvProfile aoProfile, string asMessageText)
        {
            StreamWriter loStreamWriter = null;

            try
            {
                if ( null == aoProfile )
                    aoProfile = new tvProfile();

                string  lsLogPathFile = DoGoPcBackup.sLogPathFile(aoProfile);
                string  lsPath = Path.GetDirectoryName(lsLogPathFile);
                        if ( !Directory.Exists(lsPath) )
                            Directory.CreateDirectory(lsPath);

                loStreamWriter = new StreamWriter(lsLogPathFile, true);
                loStreamWriter.WriteLine(DateTime.Now.ToString(aoProfile.sValueNoTrim(
                        "-LogEntryDateTimeFormatPrefix", "yyyy-MM-dd hh:mm:ss:fff tt  "))
                        + asMessageText);
            }
            catch { /* Can't log a log failure. */ }
            finally
            {
                if ( null != loStreamWriter )
                    loStreamWriter.Close();
            }
        }

        /// <summary>
        /// Returns the current "LogPathFile" name.
        /// </summary>
        public static string sLogPathFile(tvProfile aoProfile)
        {
            return DoGoPcBackup.sUniqueIntOutputPathFile(
                      aoProfile.sRelativeToProfilePathFile(DoGoPcBackup.sLogPathFileBase(aoProfile))
                    , aoProfile.sValue("-LogFileDateFormat", "-yyyy-MM-dd")
                    , true
                    );
        }

        /// <summary>
        /// Returns the full message string from the given exception object.
        /// </summary>
        private static string sExceptionMessage(Exception ex)
        {
            return ex.Message + (null == ex.InnerException ? "": "; " + ex.InnerException.Message) + "\r\n" + ex.StackTrace;
        }

        /// <summary>
        /// Returns the current "LogPathFile" base name.
        /// </summary>
        private static string sLogPathFileBase(tvProfile aoProfile)
        {
            return aoProfile.sValue("-LogPathFile", Path.Combine("Logs", Path.GetFileNameWithoutExtension(aoProfile.sActualPathFile) + "Log.txt"));
        }

        /// <summary>
        /// Returns a unique output pathfile based on an integer.
        /// </summary>
        /// <param name="asBasePathFile">The base pathfile string.
        /// It is segmented to form the output pathfile</param>
        /// <param name="asDateFormat">The format of the date inserted into the output filename.</param>
        /// <param name="abAppendOutput">If true, this boolean indicates that an existing file will be appended to. </param>
        /// <returns></returns>
        private static string sUniqueIntOutputPathFile(
                  string asBasePathFile
                , string asDateFormat
                , bool abAppendOutput
                )
        {
            // Get the path from the given asBasePathFile.
            string  lsOutputPath = Path.GetDirectoryName(asBasePathFile);
            // Make a filename from the given asBasePathFile and the current date.
            string  lsBaseFilename = Path.GetFileNameWithoutExtension(asBasePathFile)
                    + DateTime.Today.ToString(asDateFormat);
            // Get the filename extention from the given asBasePathFile.
            string  lsBaseFileExt = Path.GetExtension(asBasePathFile);

            string  lsOutputFilename = lsBaseFilename + lsBaseFileExt;
            int     liUniqueFilenameSuffix = 1;
            string  lsOutputPathFile = null;
            bool    lbDone = false;

            do
            {
                // If we are appending, we're done. Otherwise,
                // check for existence of the requested dated pathfile.
                lsOutputPathFile = Path.Combine(lsOutputPath, lsOutputFilename);
                lbDone = abAppendOutput || !File.Exists(lsOutputPathFile);

                // If the given pathfile already exists, create a variation on the dated
                // filename by appending an integer (see liUniqueFilenameSuffix above).
                // Keep trying until a unique dated pathfile is identified.
                if ( !lbDone )
                    lsOutputFilename = lsBaseFilename + "." + (++liUniqueFilenameSuffix).ToString() + lsBaseFileExt;
            }
            while ( !lbDone );

            return lsOutputPathFile;
        }

        /// <summary>
        /// Returns a unique output pathfile based on a GUID.
        /// </summary>
        /// <param name="asBasePathFile">The base pathfile string.
        /// It is segmented to form the output pathfile</param>
        /// <param name="asDateFormat">The format of the date inserted into the output filename.</param>
        /// <returns></returns>
        private static string sUniqueGuidOutputPathFile(
                  string asBasePathFile
                , string asDateFormat
                )
        {
            // Get the path from the given asBasePathFile.
            string  lsOutputPath = Path.GetDirectoryName(asBasePathFile);
            // Make a filename from the given asBasePathFile and the current date.
            string  lsBaseFilename = Path.GetFileNameWithoutExtension(asBasePathFile)
                    + DateTime.Today.ToString(asDateFormat);
            // Get the filename extention from the given asBasePathFile.
            string  lsBaseFileExt = Path.GetExtension(asBasePathFile);

            return Path.Combine(lsOutputPath, lsBaseFilename + "." + Guid.NewGuid().ToString() + lsBaseFileExt);
        }



        /// <summary>
        /// This is used within the main loop of the UI (see "this.oUI")
        /// to stop any loops running within this object as well.
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

                if (mbMainLoopStopped)
                {
                    // Stop the background task timer.
                    if ( null != moBackgroundLoopTimer )
                        moBackgroundLoopTimer.Change(Timeout.Infinite, Timeout.Infinite);
                }
                else
                {
                    // Start the background task timer.
                    if ( null != moBackgroundLoopTimer )
                        moBackgroundLoopTimer.Change(0, moProfile.iValue("-BackgroundLoopSleepMS", 200));
                }
            }
        }
        private bool mbMainLoopStopped;

        /// <summary>
        /// This is used to suppress prompts when
        /// the software is run via a job scheduler.
        /// </summary>
        public bool bNoPrompts
        {
            get
            {
                return moProfile.bValue("-NoPrompts", false);
            }
        }

        /// <summary>
        /// Returns the first possible backup device drive letter.
        /// </summary>
        public char cPossibleDriveLetterBegin
        {
            get
            {
                return 'D';
            }
        }

        /// <summary>
        /// Returns the last possible backup device drive letter.
        /// </summary>
        public char cPossibleDriveLetterEnd
        {
            get
            {
                return 'Z';
            }
        }

        /// <summary>
        /// This is the main application profile object.
        /// </summary>
        public tvProfile oProfile
        {
            get
            {
                return moProfile;
            }
        }
        private tvProfile moProfile;

        /// <summary>
        /// This is the main application user interface (UI) object.
        /// </summary>
        public UI oUI
        {
            get;set;
        }

        /// <summary>
        /// Returns the current "ZipToolFileListPathFile" name.
        /// </summary>
        public string ZipToolFileListPathFile
        {
            get
            {
                return DoGoPcBackup.sUniqueGuidOutputPathFile(
                          this.sZipToolFileListPathFileBase
                        , moProfile.sValue("-ZipToolFileListFileDateFormat", "-yyyy-MM-dd")
                        );
            }
        }

        /// <summary>
        /// Returns the current "ZipToolFileListPathFile" base name.
        /// </summary>
        public string sZipToolFileListPathFileBase
        {
            get
            {
                string  lsZipToolFileListPathFileBase = moProfile.sValue("-ZipToolFileListPathFile"
                                , Path.Combine("FileLists", Path.GetFileNameWithoutExtension(moProfile.sActualPathFile)
                                + "ZipFileList.txt"));
                string  lsPath = Path.GetDirectoryName(moProfile.sRelativeToExePathFile(lsZipToolFileListPathFileBase));
                        if ( !Directory.Exists(lsPath) )
                            Directory.CreateDirectory(lsPath);

                return lsZipToolFileListPathFileBase;
            }
        }

        public string sBackupDriveToken
        {
            get
            {
                return moProfile.sValue("-BackupDriveToken", "(This is my GoPC backup drive.)");
            }
            set
            {
                moProfile["-BackupDriveToken"] = value;
            }
        }
        /// <summary>
        /// Returns the current "DeletedFileListOutputPathFile" name.
        /// </summary>
        public string sDeletedFileListOutputPathFile
        {
            get
            {
                return DoGoPcBackup.sUniqueIntOutputPathFile(
                          this.sDeletedFileListOutputPathFileBase
                        , moProfile.sValue("-DeletedFileListDateFormat", "-yyyy-MM-dd")
                        , true
                        );
            }
        }

        /// <summary>
        /// Returns the current "DeletedFileListOutputPathFile" base name.
        /// </summary>
        public string sDeletedFileListOutputPathFileBase
        {
            get
            {
                string  lsDeletedFileListOutputPathFileBase = moProfile.sValue("-DeletedFileListOutputPathFile"
                                , Path.Combine("Logs", Path.GetFileNameWithoutExtension(moProfile.sActualPathFile)
                                + "DeletedFileList.txt"));
                string  lsPath = Path.GetDirectoryName(this.oProfile.sRelativeToProfilePathFile(lsDeletedFileListOutputPathFileBase));
                        if ( !Directory.Exists(lsPath) )
                            Directory.CreateDirectory(lsPath);

                return lsDeletedFileListOutputPathFileBase;
            }
        }

        /// <summary>
        /// Returns the message informing the user to
        /// use the system tray to reopen the UI.
        /// </summary>
        public string sSysTrayMsg
        {
            get
            {
                return "\r\n\r\nClick the \"GoPC\" icon in the system tray to toggle the backup window.\r\n";
            }
        }


        /// <summary>
        /// Returns the "ArchivePath" from the current backup set profile.
        /// </summary>
        public string sArchivePath()
        {
            return this.sArchivePath(moCurrentBackupSet);
        }

        /// <summary>
        /// Returns the "ArchivePath" from the given backup set profile.
        /// </summary>
        public string sArchivePath(tvProfile aoBackupSetProfile)
        {
            string lsArchivePath = moProfile.sValue("-ArchivePath", 
                    Path.Combine(Path.GetPathRoot(Environment.GetFolderPath(
                            Environment.SpecialFolder.System)), "Archive"));

            // Don't create a default "-ArchivePath" in the backup set (that's
            // what "ContainsKey" prevents). That way the global "-ArchivePath" 
            // will stay in force unless specifically overridden.
            if ( null != aoBackupSetProfile && aoBackupSetProfile.ContainsKey("-ArchivePath") )
                lsArchivePath = aoBackupSetProfile["-ArchivePath"].ToString();

            return lsArchivePath;
        }

        /// <summary>
        /// Returns the "BackupFileSpec" from the current backup set profile.
        /// </summary>
        public string sBackupFileSpec()
        {
            return this.sBackupFileSpec(moCurrentBackupSet);
        }

        /// <summary>
        /// Returns the "BackupFileSpec" from the given backup set profile.
        /// </summary>
        public string sBackupFileSpec(tvProfile aoBackupSetProfile)
        {
            string lsBackupFileSpec = moProfile.sValue("-BackupFileSpec", "*");

            // Don't create a default "-BackupFileSpec" in the backup set (that's
            // what "ContainsKey" prevents). That way the global "-BackupFileSpec"
            // will stay in force unless specifically overridden.
            if ( null != aoBackupSetProfile && aoBackupSetProfile.ContainsKey("-BackupFileSpec") )
                lsBackupFileSpec = aoBackupSetProfile["-BackupFileSpec"].ToString();

            return lsBackupFileSpec;
        }

        /// <summary>
        /// Returns the "BackupOutputPathFile" name from the current backup set profile.
        /// </summary>
        private string sBackupOutputPathFile()
        {
            return this.sBackupOutputPathFile(moCurrentBackupSet);
        }

        /// <summary>
        /// Returns the "BackupOutputPathFile" name from the given backup set profile.
        /// </summary>
        private string sBackupOutputPathFile(tvProfile aoBackupSetProfile)
        {
            return DoGoPcBackup.sUniqueIntOutputPathFile(
                      this.sBackupOutputPathFileBase(aoBackupSetProfile)
                    , moProfile.sValue("-BackupOutputFilenameDateFormat", "-yyyy-MM-dd")
                    , false
                    );
        }

        /// <summary>
        /// Returns the "BackupOutputPathFile" base name from the current backup set profile.
        /// </summary>
        private string sBackupOutputPathFileBase()
        {
            return this.sBackupOutputPathFileBase(moCurrentBackupSet);
        }

        /// <summary>
        /// Returns the "BackupOutputPathFile" base name from the given backup set profile.
        /// This is includes everything in the path\file specification except the embedded date.
        /// </summary>
        private string sBackupOutputPathFileBase(tvProfile aoBackupSetProfile)
        {
            string lsBackupOutputPathFileBase = "";

            if ( null != aoBackupSetProfile )
                lsBackupOutputPathFileBase = Path.Combine(this.sArchivePath(aoBackupSetProfile)
                        , aoBackupSetProfile.sValue("-OutputFilename", "Files"))
                        + moProfile.sValue("-BackupOutputExtension", ".zip");

            return lsBackupOutputPathFileBase;
        }

        /// <summary>
        /// Returns the total number of files to be evaluated for cleanup.
        /// </summary>
        /// <returns></returns>
        public int iCleanupFilesCount()
        {
            int liCleanupFilesCount = 0;

            // Get all cleanup sets.
            tvProfile loCleanupSetsProfile = moProfile.oOneKeyProfile("-CleanupSet");

            try
            {
                foreach (DictionaryEntry loEntry in loCleanupSetsProfile)
                {
                    // Create a profile object from the current cleanup set.
                    tvProfile loCleanupSetProfile = new tvProfile(loEntry.Value.ToString());

                    // Get all specifications of the files to delete in the current set.
                    tvProfile loFilesToCleanupProfile = loCleanupSetProfile.oOneKeyProfile("-FilesToDelete");

                    foreach (DictionaryEntry loFileToCleanup in loFilesToCleanupProfile)
                    {
                        try
                        {
                            // Count all files represented by the current file
                            // specifcation and add that number to the total.
                            liCleanupFilesCount += Directory.GetFiles(
                                      Path.GetDirectoryName(loFileToCleanup.Value.ToString())
                                    , Path.GetFileName(loFileToCleanup.Value.ToString())
                                    , SearchOption.AllDirectories).Length;
                        }
                        catch {}
                    }
                }
            }
            catch (Exception ex)
            {
                Environment.ExitCode = 1;
                this.ShowError(ex.Message, "Cleanup Failed");
            }

            return liCleanupFilesCount;
        }

        public int iCurrentBackupDevicesBitField()
        {
            // The leftmost bit is always 1 to preserve leading zeros.
            int  liCurrentBackupDevicesBitField = 1;
            char lcPossibleDriveLetter = this.cPossibleDriveLetterBegin;

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
                    liCurrentBackupDevicesBitField += (bool)File.Exists(Path.Combine(loDrive.Name, this.sBackupDriveToken)) ? 1 : 0;
                    ++lcPossibleDriveLetter;
                }
            }

            // Fill in zeros for all the drives after the last drive found.
            for ( char c = lcPossibleDriveLetter; c <= this.cPossibleDriveLetterEnd; ++c )
            {
                liCurrentBackupDevicesBitField = liCurrentBackupDevicesBitField << 1;
            }

            return liCurrentBackupDevicesBitField;
        }

        /// <summary>
        /// Returns the total number of files to be backed up.
        /// </summary>
        /// <returns></returns>
        public int iBackupFilesCount()
        {
            int liBackupFilesCount = 0;

            // Get all backup sets.
            tvProfile loBackupSetsProfile = moProfile.oOneKeyProfile("-BackupSet");

            try
            {
                foreach (DictionaryEntry loEntry in loBackupSetsProfile)
                {
                    // Create a profile object from the current backup set.
                    tvProfile loBackupSetProfile = new tvProfile(loEntry.Value.ToString());

                    // Get all specifications of the files to backup in the current set.
                    tvProfile loFolderToBackupProfile = loBackupSetProfile.oOneKeyProfile("-FolderToBackup");

                    foreach (DictionaryEntry loFolderToBackup in loFolderToBackupProfile)
                    {
                        string  lsFolderToBackup = loFolderToBackup.Value.ToString().Trim();
                        string  lsBackupFiles = null;
                                if ( Directory.Exists(lsFolderToBackup) )
                                {
                                    lsBackupFiles = this.sBackupFileSpec(loBackupSetProfile);
                                }
                                else
                                {
                                    lsFolderToBackup = Path.GetDirectoryName(lsFolderToBackup);
                                    lsBackupFiles = Path.GetFileName(lsFolderToBackup);
                                    // If the given "lsFolderToBackup" is not actually a folder,
                                    // assume it's a single file (or a file mask).
                                }

                        // Count all files represented by the current file
                        // specifcation and add that number to the total.
                        liBackupFilesCount += Directory.GetFiles(
                                  lsFolderToBackup
                                , lsBackupFiles
                                , SearchOption.AllDirectories).Length;
                    }
                }
            }
            catch (Exception ex)
            {
                Environment.ExitCode = 1;
                this.ShowError(ex.Message, "Backup Failed");
            }

            return liBackupFilesCount;
        }

        /// <summary>
        /// Returns the maximum number of files to be cleaned up
        /// given the file count and the age limit. This prevents
        /// wiping out old files when no new files are being created.
        /// </summary>
        /// <returns></returns>
        public int iFileDeletionLimit(int aiFileCount, DateTime adtDeleteOlderThan)
        {
            int liFileDeletionLimit = 
                      1440 / moProfile.iValue("-MainLoopMinutes", 1440)
                    * ((DateTime.Today - adtDeleteOlderThan).Days + 1);
                    // This represents the minimum number of files to retain
                    // (frequency of backups times number of age days).
                    // Backup frequency is 1440 minutes per day divided
                    // by the number of minutes in the main loop.

            // If the file count is less than or equal to the
            // file deletion limit, nothing will be deleted.
            return aiFileCount - liFileDeletionLimit;
        }


        // This collection of UI "Show" methods within this non-interactive class allows
        // for pop-up messages to be displayed by using dispatcher calls into the UI object,
        // if it exists. Whether the UI object exists or not, each pop-up message is also
        // written to the log file.


        private tvMessageBoxResults Show(
                  string asMessageText
                , string asMessageCaption
                , tvMessageBoxButtons aetvMessageBoxButtons
                , tvMessageBoxIcons aetvMessageBoxIcon
                , string asProfilePromptKey
                )
        {
            this.LogIt(asMessageText);

            tvMessageBoxResults ltvMessageBoxResults = tvMessageBoxResults.None;

            if ( null != this.oUI && !this.bNoPrompts )
            this.oUI.Dispatcher.BeginInvoke((Action)(() =>
            {
                ltvMessageBoxResults = tvMessageBox.Show(
                          this.oUI
                        , asMessageText
                        , asMessageCaption
                        , aetvMessageBoxButtons
                        , aetvMessageBoxIcon
                        , tvMessageBoxCheckBoxTypes.SkipThis
                        , moProfile
                        , asProfilePromptKey
                        );
            }
            ));
            System.Windows.Forms.Application.DoEvents();

            return ltvMessageBoxResults;
        }

        private tvMessageBoxResults ShowModeless(
                  string asMessageText
                , string asMessageCaption
                , tvMessageBoxButtons aetvMessageBoxButtons
                , tvMessageBoxIcons aetvMessageBoxIcon
                , string asProfilePromptKey
                )
        {
            this.LogIt(asMessageText);

            tvMessageBoxResults ltvMessageBoxResults = tvMessageBoxResults.None;

            if ( null != this.oUI && !this.bNoPrompts )
            this.oUI.Dispatcher.BeginInvoke((Action)(() =>
            {
                ltvMessageBoxResults = tvMessageBox.ShowModeless(
                          this.oUI
                        , asMessageText
                        , asMessageCaption
                        , aetvMessageBoxButtons
                        , aetvMessageBoxIcon
                        , tvMessageBoxCheckBoxTypes.SkipThis
                        , moProfile
                        , asProfilePromptKey
                        );
            }
            ));
            System.Windows.Forms.Application.DoEvents();

            return ltvMessageBoxResults;
        }

        private void ShowError(string asMessageText)
        {
            this.LogIt(asMessageText);

            if ( null != this.oUI && !this.bNoPrompts )
            this.oUI.Dispatcher.BeginInvoke((Action)(() =>
            {
                tvMessageBox.ShowError(this.oUI, asMessageText);
            }
            ));
            System.Windows.Forms.Application.DoEvents();
        }

        public void ShowError(string asMessageText, string asMessageCaption)
        {
            this.LogIt(String.Format("{1}; {0}", asMessageText, asMessageCaption));

            if ( null != this.oUI && !this.bNoPrompts )
            this.oUI.Dispatcher.BeginInvoke((Action)(() =>
            {
                tvMessageBox.ShowError(this.oUI, asMessageText, asMessageCaption);
            }
            ));
            System.Windows.Forms.Application.DoEvents();
        }

        private void ShowModelessError(string asMessageText, string asMessageCaption, string asProfilePromptKey)
        {
            this.LogIt(String.Format("{1}; {0}", asMessageText, asMessageCaption));

            if ( null != this.oUI && !this.bNoPrompts )
            this.oUI.Dispatcher.BeginInvoke((Action)(() =>
            {
                tvMessageBox.ShowModelessError(
                          this.oUI
                        , asMessageText
                        , asMessageCaption
                        , moProfile
                        , asProfilePromptKey
                        );
            }
            ));
            System.Windows.Forms.Application.DoEvents();
        }

        private void LogDeletedFile(string asPathFile, DateTime adtFileDate, long alFileSize)
        {
            this.LogIt("Deleted: " + asPathFile);

            // Assume the deleted file list will reside with the 
            // profile file unless it has its own absolute path.
            string  lsDeletedFileListOutputPathFile = moProfile.sRelativeToProfilePathFile(this.sDeletedFileListOutputPathFile);

            StreamWriter loStreamWriter = null;

            try
            {
                loStreamWriter = new StreamWriter(lsDeletedFileListOutputPathFile, true);

                // This assumes that the deleted file list header has already been written.
                // Output the data columns properly formatted to match the preceeding column headers.
                loStreamWriter.WriteLine(string.Format(
                          moProfile.sValue("-DeletedFileListOutputColumnFormat", "{0, -22:MM-dd-yyyy hh:mm:ss tt}  {1, 13:#,#}  {2}")
                        , adtFileDate, alFileSize, asPathFile));
            }
            catch (Exception ex)
            {
                Environment.ExitCode = 1;
                this.ShowError(string.Format("File Write Failure: \"{0}\"\r\n"
                        , lsDeletedFileListOutputPathFile) + ex.Message
                        , "Failed Writing File"
                        );
            }
            finally
            {
                if ( null != loStreamWriter )
                    loStreamWriter.Close();
            }
        }

        // This kludge is necessary since neither "Process.CloseMainWindow()" nor "Process.Kill()" work reliably.
        private bool bKillProcess(Process aoProcess)
        {
            bool    lbKillProcess = true;
            int     liProcessId = aoProcess.Id;

            // First try ending the process in the usual way (ie. an orderly shutdown).

            Process loKillProcess = new Process();

            try
            {
                liProcessId = aoProcess.Id;

                loKillProcess = new Process();
                loKillProcess.StartInfo.FileName = "taskkill";
                loKillProcess.StartInfo.Arguments = "/pid " + liProcessId;
                loKillProcess.StartInfo.UseShellExecute = false;
                loKillProcess.StartInfo.CreateNoWindow = true;
                loKillProcess.Start();

                aoProcess.WaitForExit(1000 * moProfile.iValue("-KillProcessOrderlyWaitSecs", 30));
            }
            catch (Exception ex)
            {
                Environment.ExitCode = 1;
                lbKillProcess = false;
                this.LogIt(String.Format("Orderly bKillProcess() Failed on PID={0} (\"{1}\")", liProcessId, ex.Message));
            }

            if ( lbKillProcess && !aoProcess.HasExited )
            {
                // Then try terminating the process forcefully (ie. slam it down).

                try
                {
                    loKillProcess = new Process();
                    loKillProcess.StartInfo.FileName = "taskkill";
                    loKillProcess.StartInfo.Arguments = "/f /t /pid " + liProcessId;
                    loKillProcess.StartInfo.UseShellExecute = false;
                    loKillProcess.StartInfo.CreateNoWindow = true;
                    loKillProcess.Start();

                    aoProcess.WaitForExit(moProfile.iValue("-KillProcessForcedWaitMS", 1000));

                    lbKillProcess = aoProcess.HasExited;
                }
                catch (Exception ex)
                {
                    Environment.ExitCode = 1;
                    lbKillProcess = false;
                    this.LogIt(String.Format("Forced bKillProcess() Failed on PID={0} (\"{1}\")", liProcessId, ex.Message));
                }
            }

            return lbKillProcess;
        }

        private void IncrementUIProgressBar()
        {
            this.IncrementUIProgressBar(false);
        }
        private void IncrementUIProgressBar(bool abFill)
        {
            if ( null != this.oUI )
            this.oUI.Dispatcher.BeginInvoke((Action)(() =>
            {
                this.oUI.IncrementProgressBar(abFill);
            }
            ));
            System.Windows.Forms.Application.DoEvents();
        }

        /// <summary>
        /// Displays the given file (ie. asPathFile) using whatever
        /// application is associated with its filename extension.
        /// </summary>
        private void DisplayFileAsErrors(string asFileAsStream, string asCaption)
        {
            if ( !this.bNoPrompts )
            {
                ScrollingText   loErrors = new ScrollingText(asFileAsStream, asCaption);
                                loErrors.Show();

                                if ( null != this.oUI )
                                this.oUI.oOtherWindows.Add(loErrors);
            }
        }

        /// <summary>
        /// Displays the file cleanup output file.
        /// </summary>
        private void DisplayDeletedFileList()
        {
            if ( moProfile.bValue("-ShowDeletedFileList", false) )
            {
                if ( !this.mbHasNoDeletionGroups )
                {
                    string lsFileAsStream = this.sFileAsStream(this.sDeletedFileListOutputPathFile);

                    if ( null != lsFileAsStream && !this.bNoPrompts )
                    {
                        ScrollingText   loFileList = new ScrollingText(lsFileAsStream, "Deleted File List");
                                        loFileList.TextBackground = Brushes.LightYellow;
                                        loFileList.TextFontFamily = new FontFamily("Courier New");
                                        loFileList.Show();

                                        if ( null != this.oUI )
                                        this.oUI.oOtherWindows.Add(loFileList);
                    }
                }
                else
                {
                    string lsFileGroup1 = moProfile.sValue("-CleanupSet", "One of many file groups to delete goes here.");

                    this.ShowError(
                              string.Format(@"
Here's what you have configured:

-CleanupSet={0}


Please add at least one '-FilesToDelete=' reference. See 'Help' for examples.

No file cleanup will be done until you update the configuration.
"                           , lsFileGroup1)
                            , "No File Cleanup Sets Defined"
                            );
                }
            }
        }

        /// <summary>
        /// Returns the contents of a given file as a string.
        /// </summary>
        /// <param name="asSourcePathFile"></param>
        /// The path\file specification os the given file.
        /// <returns></returns>
        public string sFileAsStream(string asSourcePathFile)
        {
            string lsFileAsStream = null;

            try
            {
                StreamReader loStreamReader = null;

                try
                {
                    loStreamReader = new StreamReader(asSourcePathFile);
                    lsFileAsStream = loStreamReader.ReadToEnd();
                }
                catch (Exception ex)
                {
                    Environment.ExitCode = 1;
                    this.ShowError(string.Format("File Read Failure: \"{0}\"\r\n"
                            , asSourcePathFile) + ex.Message
                            , "Failed Reading File"
                            );
                }
                finally
                {
                    if ( null != loStreamReader )
                        loStreamReader.Close();
                }
            }
            catch (Exception ex)
            {
                Environment.ExitCode = 1;
                this.ShowError(ex.Message);
            }

            return lsFileAsStream;
        }

        /// <summary>
        /// Write the given asMessageText to a text file as well as
        /// to the output console of the UI window (if it exists).
        /// </summary>
        /// <param name="asMessageText">The text message string to log.</param>
        public void LogIt(string asMessageText)
        {
            DoGoPcBackup.LogIt(moProfile, asMessageText);

            if ( null != this.oUI )
            this.oUI.Dispatcher.BeginInvoke((Action)(() =>
            {
                this.oUI.AppendOutputTextLine(asMessageText);
            }
            ));
            System.Windows.Forms.Application.DoEvents();
        }


        /// <summary>
        /// Backup files of the given file specifications and run
        /// the backup scripts to copy the backup around as needed.
        /// </summary>
        public bool BackupFiles()
        {
            // Return if backup is disabled.
            if ( !moProfile.bValue("-BackupFiles", true) )
            {
                this.LogIt("");
                this.LogIt("Backup files is disabled.");
                return true;
            }
            // Return if cleanup is enabled and it was stopped.
            if (  moProfile.bValue("-CleanupFiles", true) && this.bMainLoopStopped )
                return true;
            else
                this.bMainLoopStopped = false;

            bool    lbBackupFiles = false;
                    moProfile.Remove("-Previous1stArchiveOk");
            int     liBackupBeginScriptErrors = 0;          // The error count returned by the "backup begin" script.
            int     liBackupDoneScriptFileCopyFailures = 0; // The copy failures count returned by the "backup done" script.
            int     liCurrentBackupDevicesBitField = 0;     // The current backup devices returned by the "backup done" script.

            // Initialize the zip tool.
            if ( moProfile.bValue("-ZipToolInit", false) )
            {
                // First, remove references in the profile.
                moProfile.Remove("-ZipToolEXE");
                moProfile.Remove("-ZipToolEXEargs");

                // Then remove the zip tool files from the disk.
                foreach (string lsFilename in msZipToolExeFilenamesArray)
                    File.Delete(moProfile.sRelativeToExePathFile(lsFilename));

                // This is used only once then reset.
                moProfile["-ZipToolInit"] = false;
                moProfile.Save();
            }

            // Get the embedded zip compression tool files from the EXE.
            foreach (string lsFilename in msZipToolExeFilenamesArray)
                tvFetchResource.ToDisk(Application.ResourceAssembly.GetName().Name, lsFilename, null);

            // Get all backup sets.
            tvProfile loBackupSetsProfile = moProfile.oOneKeyProfile("-BackupSet");

            miBackupSets = loBackupSetsProfile.Count;
            miBackupSetsRun = 0;
            miBackupSetsGood = 0;

            // Release the lock on the profile file 
            // so that it can be backed up as well.
            moProfile.bEnableFileLock = false;

            try
            {
                lbBackupFiles = true;

                moProfile.Remove("-PreviousBackupOk");
                moProfile.Remove("-PreviousBackupOutputPathFile");
                moProfile.Save();

                // Run the "backup begin" script and return any errors.
                if ( moProfile.bValue("-BackupBeginScriptEnabled", true) )
                    liBackupBeginScriptErrors = this.iBackupBeginScriptErrors();
                else
                    this.LogIt("The \"backup begin\" script is disabled.");

                string  lsZipToolFileListPathFile = moProfile.sRelativeToExePathFile(this.ZipToolFileListPathFile);
                string  lsBackupPathFiles1 = null;      // The first file specification in the set (for logging).
                int     liFileCount = 0;                // The number of file specifications in the current set.

                foreach (DictionaryEntry loEntry in loBackupSetsProfile)
                {
                    System.Windows.Forms.Application.DoEvents();
                    if ( this.bMainLoopStopped )
                        break;

                    string lsProcessPathFile = moProfile.sRelativeToExePathFile(moProfile.sValue("-ZipToolEXE", msZipToolExeFilenamesArray[0]));

                    // Increment the backup set counter.
                    miBackupSetsRun++;

                    // Convert the current backup set from a command-line string to a profile oject.
                    moCurrentBackupSet = new tvProfile(loEntry.Value.ToString());

                    // Get the list of folders to backup within the current backup set.
                    tvProfile       loFolderToBackupProfile = moCurrentBackupSet.oOneKeyProfile(
                                            "-FolderToBackup");
                    StreamWriter    loBackupFileListStreamWriter = null;

                    liFileCount = 0;

                    try
                    {
                        // Create the backup file list file.
                        loBackupFileListStreamWriter = new StreamWriter(moProfile.sRelativeToExePathFile(
                                lsZipToolFileListPathFile), false);

                        // Write the list of files to compress.
                        foreach (DictionaryEntry loFolderToBackup in loFolderToBackupProfile)
                        {
                            System.Windows.Forms.Application.DoEvents();
                            if ( this.bMainLoopStopped )
                                break;

                            string  lsFolderToBackup = loFolderToBackup.Value.ToString().Trim();
                            string  lsBackupPathFiles = null;
                                    if ( Directory.Exists(lsFolderToBackup) )
                                        lsBackupPathFiles = Path.Combine(lsFolderToBackup, this.sBackupFileSpec());
                                    else
                                        lsBackupPathFiles = lsFolderToBackup;
                                        // If the given "lsFolderToBackup" is not actually a folder, assume it's a single file (or a file mask).

                            if ( 1 == ++liFileCount )
                            {
                                if ( 1 == miBackupSetsRun && !moProfile.ContainsKey("-ActivateAlreadyRunningInstance") )
                                {
                                    // Backup the backup as well (1st backup set only) and use the parent filespec (ie. not the current).
                                    loBackupFileListStreamWriter.WriteLine(this.sExeRelativePath(lsProcessPathFile
                                            , moProfile.sRelativeToExePathFile(moProfile.sValue("-BackupFileSpec", "*"))));

                                    // Also, make an extra - easily accessible - backup of just the profile file. The assignment is required
                                    // to create the backup copy (it's a side effect of the property call). The pathfile name is discarded.
                                    string  lsDiscard = null;
                                            if ( moProfile.bSaveEnabled )
                                                lsDiscard = moProfile.sBackupPathFile;
                                }

                                // Save the first pathfile specification (ie. folder) for later reference in the log.
                                lsBackupPathFiles1 = lsBackupPathFiles;
                            }

                            loBackupFileListStreamWriter.WriteLine(this.sExeRelativePath(lsProcessPathFile, lsBackupPathFiles));
                        }
                    }
                    catch (Exception ex)
                    {
                        Environment.ExitCode = 1;
                        lbBackupFiles = false;
                        if ( null != this.oUI )
                        this.oUI.bBackupRunning = false;
                        this.ShowError(string.Format("File Write Failure: \"{0}\"\r\n"
                                , lsZipToolFileListPathFile) + ex.Message
                                , "Failed Writing File"
                                );
                    }
                    finally
                    {
                        if ( null != loBackupFileListStreamWriter )
                            loBackupFileListStreamWriter.Close();
                    }

                    // The backup output path file will be dated and unique.
                    msCurrentBackupOutputPathFile = this.sBackupOutputPathFile();

                    string  lsProcessArgs = moProfile.sValue("-ZipToolEXEargs"
                                    , "a -ssw -bb \"{BackupOutputPathFile}\" @\"{BackupPathFiles}\" -w\"{BackupOutputPath}\" ")
                                    + " " + moProfile.sValue("-ZipToolEXEargsMore", "");
                            lsProcessArgs = lsProcessArgs.Replace("{BackupPathFiles}", lsZipToolFileListPathFile);
                            lsProcessArgs = lsProcessArgs.Replace("{BackupOutputPath}", Path.GetDirectoryName(msCurrentBackupOutputPathFile));
                            lsProcessArgs = lsProcessArgs.Replace("{BackupOutputPathFile}", msCurrentBackupOutputPathFile);
                    string  lsArchivePath = Path.GetDirectoryName(msCurrentBackupOutputPathFile);
                            if ( !Directory.Exists(lsArchivePath) )
                                try
                                {
                                    Directory.CreateDirectory(lsArchivePath);
                                }
                                catch (Exception ex)
                                {
                                    Environment.ExitCode = 1;
                                    lbBackupFiles = false;
                                    if ( null != this.oUI )
                                    this.oUI.bBackupRunning = false;
                                    this.ShowError(
                                              string.Format("Folder: \"{0}\"\r\n", lsArchivePath) + ex.Message
                                            , "Error Creating Archive Folder"
                                            );
                                }

                    Process loProcess = new Process();
                            loProcess.ErrorDataReceived += new DataReceivedEventHandler(this.BackupProcessOutputHandler);
                            loProcess.OutputDataReceived += new DataReceivedEventHandler(this.BackupProcessOutputHandler);
                            loProcess.StartInfo.FileName = lsProcessPathFile;
                            loProcess.StartInfo.Arguments = lsProcessArgs;
                            loProcess.StartInfo.WorkingDirectory = "\\";
                            loProcess.StartInfo.UseShellExecute = false;
                            loProcess.StartInfo.RedirectStandardError = true;
                            loProcess.StartInfo.RedirectStandardInput = true;
                            loProcess.StartInfo.RedirectStandardOutput = true;
                            loProcess.StartInfo.CreateNoWindow = true;

                    moProfile["-PreviousBackupOutputPathFile"] = msCurrentBackupOutputPathFile;
                    moProfile.Save();

                    string  lsLastRunCmd = String.Format("\"{0}\" {1}", lsProcessPathFile, lsProcessArgs);
                    string  lsFileAsStream = string.Format(@"
cd\
@prompt $
{0}
@echo.
cd ""{1}""
@pause
"                                           , lsLastRunCmd
                                            , Path.GetDirectoryName(moProfile.sExePathFile)
                                            );
                    string  lsFilesSuffix = liFileCount <= 1 ? ""
                            : string.Format(" + {0} other file specification" + (1 == (liFileCount - 1) ? "" : "s")
                                    , liFileCount - 1);

                    this.LogIt("");
                    this.LogIt("Backup started ...");
                    this.LogIt("");
                    this.LogIt(string.Format("Backup: \"{0}\"{1}", lsBackupPathFiles1, lsFilesSuffix));
                    this.LogIt("");
                    this.LogIt("Backup command: " + lsLastRunCmd);

                    // Update the output text cache.
                    if ( null != this.oUI )
                    this.oUI.Dispatcher.BeginInvoke((Action)(() =>
                    {
                        this.oUI.GetSetOutputTextPanelCache();
                    }
                    ));
                    System.Windows.Forms.Application.DoEvents();

                    System.Windows.Forms.Application.DoEvents();

                    // Cache arguments passed to subsequent scripts.
                    tvProfile   loArgs = new tvProfile();
                                loArgs.Add("-BackupOutputPathFile"          , msCurrentBackupOutputPathFile                         );
                                loArgs.Add("-BackupOutputFilename"          , Path.GetFileName(msCurrentBackupOutputPathFile)       );
                                loArgs.Add("-BackupBaseOutputFilename"      , Path.GetFileName(this.sBackupOutputPathFileBase())    );
                                loArgs.Add("-LocalArchivePath"              , this.sArchivePath()                                   );
                                loArgs.Add("-VirtualMachineHostArchivePath" , moProfile.sValue("-VirtualMachineHostArchivePath", ""));
                                loArgs.Add("-LogPathFile"                   , DoGoPcBackup.sLogPathFile(moProfile)                  );

                                moProfile["-LastBackupArgs"] = loArgs.sCommandBlock();
                                moProfile.Save();

                    loProcess.Start();

                    // Start output to console also.
                    if ( loProcess.StartInfo.RedirectStandardError )
                        loProcess.BeginErrorReadLine();
                    if ( loProcess.StartInfo.RedirectStandardOutput )
                        loProcess.BeginOutputReadLine();

                    DateTime ltdProcessTimeout = DateTime.Now.AddMinutes(moProfile.iValue("-BackupTimeoutMins", 60));

                    // Wait for the backup process to finish.
                    while ( !this.bMainLoopStopped && !loProcess.HasExited && DateTime.Now < ltdProcessTimeout )
                    {
                        System.Windows.Forms.Application.DoEvents();
                        System.Threading.Thread.Sleep(moProfile.iValue("-MainLoopSleepMS", 100));
                    }

                    if ( DateTime.Now >= ltdProcessTimeout && !this.bMainLoopStopped )
                    {
                        lbBackupFiles = false;

                        this.LogIt("");
                        this.LogIt("The backup process timed-out.");
                    }

                    // Do final wait then call "WaitForExit()" to flush the output steams.
                    if ( lbBackupFiles && !this.bMainLoopStopped && loProcess.WaitForExit(1000 * moProfile.iValue("-KillProcessOrderlyWaitSecs", 30)) )
                        loProcess.WaitForExit();

                    // Stop output to console.
                    if ( loProcess.StartInfo.RedirectStandardError )
                        loProcess.CancelErrorRead();
                    if ( loProcess.StartInfo.RedirectStandardOutput )
                        loProcess.CancelOutputRead();

                    // If the process failed or a stop request came through, kill the backup process.
                    if ( (!lbBackupFiles || this.bMainLoopStopped) && !this.bKillProcess(loProcess) )
                        this.ShowError("The backup process could not be stopped.", "Backup Failed");

                    // The backup process finished uninterrupted.
                    if ( !this.bMainLoopStopped )
                    {
                        if ( !lbBackupFiles || 0 != loProcess.ExitCode )
                        {
                            // The backup process failed.
                            this.LogIt(string.Format("The backup to \"{0}\" failed."
                                    , Path.GetFileName(msCurrentBackupOutputPathFile)));

                            // Run the "backup failed" script.
                            if ( moProfile.bValue("-BackupFailedScriptEnabled", true) )
                                this.BackupFailedScript();
                            else
                                this.LogIt("The \"backup failed\" script is disabled.");
                        }
                        else
                        {
                            if ( 1 == miBackupSetsRun )
                                moProfile["-Previous1stArchiveOk"] = true;

                            // The backup process finished without error.
                            this.LogIt(string.Format("The backup to \"{0}\" was successful."
                                    , Path.GetFileName(msCurrentBackupOutputPathFile)));

                            double ldCompositeResult = 0;   // Composite result returned by the "backup done" script.

                            // Run the "backup done" script and return the failed file count with bit field.
                            // The exit code is defined in the script as a combination of two integers:
                            // a bit field of found backup devices and a count of copy failures (99 max).
                            if ( moProfile.bValue("-BackupDoneScriptEnabled", true) )
                                ldCompositeResult = this.iBackupDoneScriptCopyFailuresWithBitField() / 100.0;
                            else
                                this.LogIt("The \"backup done\" script is disabled.");

                            // The integer part of the composite number is the bit field.
                            // This will be used below after all backup sets are run. We
                            // assume that the bit field remains constant between sets.
                            liCurrentBackupDevicesBitField = (int)ldCompositeResult;

                            // The fractional part (x 100) is the actual number of copy failures.
                            int liCopyFailures = (int)Math.Round(100 * (ldCompositeResult - liCurrentBackupDevicesBitField));

                            // Add failed files from the current backup set to the total.
                            liBackupDoneScriptFileCopyFailures += liCopyFailures;

                            // Increment how many backup sets have succeeded so far.
                            if ( 0 == liCopyFailures )
                                miBackupSetsGood++;
                        }
                    }

                    loProcess.Close();
                }
            }
            catch (Exception ex)
            {
                Environment.ExitCode = 1;
                lbBackupFiles = false;
                if ( null != this.oUI )
                this.oUI.bBackupRunning = false;
                this.ShowError(ex.Message, "Backup Failed");
            }

            if ( null != this.oUI )
            this.oUI.bBackupRunning = false;

            if ( this.bMainLoopStopped )
            {
                this.LogIt("Backup process stopped.");
                this.bMainLoopStopped = false;
                lbBackupFiles = false;
            }
            else
            if ( lbBackupFiles )
            {
                this.LogIt("");

                // Don't bother with the system tray message 
                // if the background timer is not running.
                string  lsSysTrayMsg = null;
                        if ( moProfile.bValue("-AutoStart", true) )
                            lsSysTrayMsg = this.sSysTrayMsg;

                List<char> loMissingBackupDevices = new List<char>();

                // Compare the bit field of current backup devices to the bit field of
                // devices selected by the user to be used by the "backup done" script.
                if ( moProfile.bValue("-BackupDoneScriptEnabled", true) )
                    loMissingBackupDevices = this.oMissingBackupDevices(liCurrentBackupDevicesBitField);

                if (        miBackupSetsGood != miBackupSetsRun
                        ||  0 != liBackupBeginScriptErrors
                        ||  0 != liBackupDoneScriptFileCopyFailures
                        ||  0 != loMissingBackupDevices.Count
                        )
                {
                    if ( 0 != loMissingBackupDevices.Count )
                        moProfile["-PreviousBackupDevicesMissing"] = true;
                    else
                        moProfile["-PreviousBackupDevicesMissing"] = false;

                    this.SetBackupFailed();

                    lbBackupFiles = false;

                    this.ShowError("The backup failed. Check the log for errors." + lsSysTrayMsg
                            , "Backup Failed");
                }
                else
                {
                    moProfile["-PreviousBackupDevicesMissing"] = false;
                    moProfile["-PreviousBackupOk"] = true;
                    moProfile["-PreviousBackupTime"] = DateTime.Now;
                    moProfile.Save();

                    this.Show("Backup finished successfully." + lsSysTrayMsg
                            , "Backup Finished"
                            , tvMessageBoxButtons.OK
                            , tvMessageBoxIcons.Done
                            , "-CurrentBackupFinished"
                            );
                }
            }

            // Turn on the lock that was turned off when we started.
            moProfile.bEnableFileLock = true;

            return lbBackupFiles;
        }

        // Convert an absolute file path (on the same drive as a given executable) to a relative path.
        private string sExeRelativePath(string asExePathFile, string asPathFile)
        {
            string lsPathFile = null;

            // Leave system folder references unchanged to avoid file contention problems. File contention
            // within the system folder is an issue with the latest versions of Windows. Example error:
            //
            //     Windows\System32\drivers\etc : The system cannot find the file specified.
            //
            // It's interesting that the above error occurs only when a relative reference is used.
            // The absolute reference does successfully backup files in "drivers/etc", but the sad
            // result is files in the archive are stripped of their path specification.

            if (asPathFile.ToLower().StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.System).ToLower()))
                lsPathFile = asPathFile;
            else
                lsPathFile = asPathFile.Replace(Path.GetPathRoot(asExePathFile), "");

            return lsPathFile;
        }


        /// <summary>
        /// Do additional scheduled tasks.
        /// </summary>
        public void DoAddTasks()
        {
            this.DoAddTasks(false);
        }
        public void DoAddTasks(bool abStartup)
        {
            // Resetting this once per day gets us back to the top.
            if ( DateTime.Now.TimeOfDay.TotalSeconds < 1 )
                miAddTasksNextTotalMinutes = int.MinValue;

            // Do nothing until the next task time (if not just starting up).
            if ( !abStartup && DateTime.Now.TimeOfDay.TotalMinutes < miAddTasksNextTotalMinutes )
                return;

            try
            {
                if ( null == moAddTasksProfile )
                {
                    tvProfile   loAddTasksProfile = moProfile.oProfile("-AddTasks").oOneKeyProfile("-Task");
                    tvProfile   loAddTasksProfileNoDelay  = new tvProfile();
                    tvProfile   loAddTasksProfileYesDelay = new tvProfile();
                                foreach (DictionaryEntry loEntry in loAddTasksProfile)
                                {
                                    tvProfile loAddTask = new tvProfile(loEntry.Value.ToString());

                                    if ( !loAddTask.bValue("-OnStartup", false) )
                                    {
                                        loAddTasksProfileYesDelay.Add(loEntry);
                                    }
                                    else
                                    {
                                        if ( 0 == loAddTask.iValue("-DelaySecs", 0) )
                                            loAddTasksProfileNoDelay.Add(loEntry);
                                        else
                                            loAddTasksProfileYesDelay.Add(loEntry);
                                    }
                                }
                                if ( 0 != loAddTasksProfileNoDelay.Count && 0 != loAddTasksProfileYesDelay.Count )
                                {
                                    loAddTasksProfile = new tvProfile();

                                    // Place all "no delay" -OnStartup tasks on top.
                                    foreach (DictionaryEntry loEntry in loAddTasksProfileNoDelay)
                                        loAddTasksProfile.Add(loEntry);
                                    foreach (DictionaryEntry loEntry in loAddTasksProfileYesDelay)
                                        loAddTasksProfile.Add(loEntry);
                                }
                                moAddTasksProcessArray = new Process[loAddTasksProfile.Count];

                    moAddTasksProfile = new tvProfile();

                    // Build moAddTasksProfile from loAddTasksProfile, including calculated reruns.
                    foreach (DictionaryEntry loEntry in loAddTasksProfile)
                    {
                        tvProfile   loAddTask = new tvProfile(loEntry.Value.ToString());
                        int         liRerunMins = loAddTask.iValue("-RerunMins", 0);

                        moAddTasksProfile.Add(loEntry);

                        if ( 0 != liRerunMins )
                        {
                            DateTime    ldtTaskStartTime = loAddTask.dtValue("-StartTime", DateTime.MinValue);
                            DateTime    ldtTaskStartTimeTomorrow = ldtTaskStartTime.AddDays(1);
                            int         liRerunLimit = loAddTask.iValue("-RerunLimit", 0);
                                        loAddTask.Remove("-DelaySecs");
                                        loAddTask.Remove("-RerunMins");
                                        loAddTask.Remove("-RerunLimit");
                                        loAddTask.Remove("-OnStartup");
                                        loAddTask.Remove("-UnloadOnExit");
                            int         liRerunCount = 0;

                            do
                            {
                                ldtTaskStartTime = ldtTaskStartTime.AddMinutes(liRerunMins);

                                if ( ldtTaskStartTime < ldtTaskStartTimeTomorrow )
                                {
                                    loAddTask["-StartTime"] = ldtTaskStartTime.ToString("hh:mm:ss tt");
                                    loAddTask["-IsRerun"] = true;

                                    moAddTasksProfile.Add("-Task", loAddTask.ToString());
                                }
                            }
                            while  ( (++liRerunCount < liRerunLimit || 0 == liRerunLimit) && ldtTaskStartTime < ldtTaskStartTimeTomorrow );
                        }
                    }
                }

                DateTime    ldtTasksStarted = DateTime.Now;
                int         liTasksStartedTotalMinutes = (int)ldtTasksStarted.TimeOfDay.TotalMinutes;
                int         liProcessCount = 0;

                miAddTasksNextTotalMinutes = int.MaxValue;

                foreach (DictionaryEntry loEntry in moAddTasksProfile)
                {
                    tvProfile loAddTask = new tvProfile(loEntry.Value.ToString());

                    bool        lbDoTask            = !loAddTask.bValue("-TaskDisabled", false);
                    DateTime    ldtTaskStartTime    = loAddTask.dtValue("-StartTime", DateTime.MinValue);
                    int         liTaskTotalMinutes  = (int)ldtTaskStartTime.TimeOfDay.TotalMinutes;
                    string      lsTaskDaysOfWeek    = loAddTask.sValue("-StartDays", "");
                    string      lsCommandEXE        = loAddTask.sValue("-CommandEXE", "add task -CommandEXE missing");
                    string      lsCommandArgs       = loAddTask.sValue("-CommandArgs", "");

                    // Get the next task time.
                    if ( lbDoTask
                            && liTaskTotalMinutes > liTasksStartedTotalMinutes
                            && liTaskTotalMinutes < miAddTasksNextTotalMinutes
                            && (String.IsNullOrEmpty(lsTaskDaysOfWeek) || this.bListIncludesDay(lsTaskDaysOfWeek, ldtTasksStarted))
                            )
                        miAddTasksNextTotalMinutes = liTaskTotalMinutes;

                    if ( !abStartup )
                    {
                        if ( lbDoTask )
                            lbDoTask = DateTime.MinValue != ldtTaskStartTime && liTaskTotalMinutes == liTasksStartedTotalMinutes
                                    && (String.IsNullOrEmpty(lsTaskDaysOfWeek) || this.bListIncludesDay(lsTaskDaysOfWeek, ldtTasksStarted));
                                    // If -StartTime is within the current minute, start the task.
                                    // If -StartDays is specified, run the task only on those days.
                                    //
                                    // Note: there is no risk of running the same task twice within the same minute since
                                    //       the next run-through must wait for a subsequent minute for a subsequent task.
                    }
                    else
                    {
                        if ( lbDoTask )
                            lbDoTask = loAddTask.bValue("-OnStartup", false) && !moProfile.bValue("-StartupTasksDisabled", false);

                        if ( loAddTask.bValue("-OnStartup", false) && !lbDoTask )
                            this.LogIt(String.Format("Skipping startup task: {0}", loAddTask.sCommandLine()));

                        if ( lbDoTask && !mbAddTasksStartupDelayDone )
                        {
                            if ( 0 != moProfile.iValue("-StartupTasksDelaySecs", 0) )
                                System.Threading.Thread.Sleep(1000 * moProfile.iValue("-StartupTasksDelaySecs", 0));

                            mbAddTasksStartupDelayDone = true;
                        }

                        if ( lbDoTask && 0 != loAddTask.iValue("-DelaySecs", 0) )
                            System.Threading.Thread.Sleep(1000 * loAddTask.iValue("-DelaySecs", 0));
                    }

                    if ( lbDoTask )
                    {
                        Process loProcess = new Process();
                                loProcess.ErrorDataReceived  += new DataReceivedEventHandler(this.BackupProcessOutputHandler);
                                loProcess.OutputDataReceived += new DataReceivedEventHandler(this.BackupProcessOutputHandler);
                                loProcess.StartInfo.FileName  = lsCommandEXE;
                                loProcess.StartInfo.Arguments = lsCommandArgs;
                                loProcess.StartInfo.WorkingDirectory = Path.GetDirectoryName(moProfile.sExePathFile);
                                loAddTask.bValue("-UnloadOnExit", false);

                                // The following subset of parameters (mostly) are overridden when -TimeoutMinutes is set.
                                // This is necessary to guarantee IO redirection is handled properly (ie. goes to the log).
                        bool    lbWaitForExitOverride = (loAddTask.iValue("-TimeoutMinutes", 0) > 0);
                                loProcess.StartInfo.CreateNoWindow          =  lbWaitForExitOverride | loAddTask.bValue("-CreateNoWindow", false);
                                loProcess.StartInfo.WindowStyle             =  (ProcessWindowStyle)Enum.Parse(typeof(ProcessWindowStyle), loAddTask.sValue("-WindowStyle", "Normal"));
                                loProcess.StartInfo.UseShellExecute         = !lbWaitForExitOverride & loAddTask.bValue("-UseShellExecute", true);
                                loProcess.StartInfo.RedirectStandardInput   =  lbWaitForExitOverride | loAddTask.bValue("-RedirectStandardInput", false);
                                loProcess.StartInfo.RedirectStandardError   =  lbWaitForExitOverride | loAddTask.bValue("-RedirectStandardError", false);
                                loProcess.StartInfo.RedirectStandardOutput  =  lbWaitForExitOverride | loAddTask.bValue("-RedirectStandardOutput", false);

                                // Leave reruns out of managed processes.
                                if ( !loAddTask.bValue("-IsRerun", false) )
                                    moAddTasksProcessArray[liProcessCount++] = loProcess;

                        try
                        {
                            if ( !loAddTask.bValue("-OnStartup", false) )
                            {
                                this.LogIt(String.Format("Starting Task: {0}", loAddTask.sCommandLine()));

                                // Update the output text cache.
                                if ( null != this.oUI )
                                this.oUI.Dispatcher.BeginInvoke((Action)(() =>
                                {
                                    this.oUI.GetSetOutputTextPanelCache();
                                }
                                ));
                                System.Windows.Forms.Application.DoEvents();

                                loProcess.Start();

                                // Start output to console also.
                                if ( loProcess.StartInfo.RedirectStandardError )
                                    loProcess.BeginErrorReadLine();
                                if ( loProcess.StartInfo.RedirectStandardOutput )
                                    loProcess.BeginOutputReadLine();

                                if ( lbWaitForExitOverride )
                                {
                                    // Wait the timeout period, then call "WaitForExit()" to flush the output steams.
                                    if ( loProcess.WaitForExit(60000 * loAddTask.iValue("-TimeoutMinutes", 0)) )
                                        loProcess.WaitForExit();

                                    // Stop output to console.
                                    if ( loProcess.StartInfo.RedirectStandardError )
                                        loProcess.CancelErrorRead();
                                    if ( loProcess.StartInfo.RedirectStandardOutput )
                                        loProcess.CancelOutputRead();

                                    loProcess.Close();
                                }
                            }
                            else
                            {
                                bool        lbFound = false;
                                string      lsWindowTitle = loAddTask.sValue("-CommandWindowTitle", "");
                                Process[]   loProcessesArray = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(loProcess.StartInfo.FileName));
                                            // If there's exactly one matching process and no given window title to compare, we're done.
                                            lbFound = (1 == loProcessesArray.Length && String.IsNullOrEmpty(lsWindowTitle) );

                                            // If no window title has been given to compare, there's nothing else to do.
                                            if ( !lbFound && !String.IsNullOrEmpty(lsWindowTitle) )
                                            {
                                                // If no matching processes have been found so far, get them all to compare.
                                                if ( 0 == loProcessesArray.Length )
                                                    loProcessesArray = Process.GetProcesses();

                                                bool    lbWindowTitleStartsWithAsterisk = lsWindowTitle.StartsWith("*");
                                                        lsWindowTitle = lsWindowTitle.Replace("*", "");

                                                // Since a window title has been provided, it must be compared to the process(es) found.
                                                // Wildcards are permitted (at the start or end of titles). We stop at the first match.
                                                foreach (Process loProcessEntry in loProcessesArray)
                                                    if (   ( lbWindowTitleStartsWithAsterisk && loProcessEntry.MainWindowTitle.Contains(lsWindowTitle)   )
                                                        || (!lbWindowTitleStartsWithAsterisk && loProcessEntry.MainWindowTitle.StartsWith(lsWindowTitle) )
                                                            )
                                                    {
                                                        lbFound = true;
                                                        break;
                                                    }
                                            }

                                // Don't start -OnStartup processes that have already been started.
                                if ( lbFound )
                                {
                                    // The process has "already started" if there is only one with the
                                    // same EXE or multiple EXEs with one having the same window title.
                                    this.LogIt(String.Format("Already running, task not started: {0}", loAddTask.sCommandLine()));
                                }
                                else
                                {
                                    this.LogIt(String.Format("Starting Task: {0}", loAddTask.sCommandLine()));

                                    // Update the output text cache.
                                    if ( null != this.oUI )
                                    this.oUI.Dispatcher.BeginInvoke((Action)(() =>
                                    {
                                        this.oUI.GetSetOutputTextPanelCache();
                                    }
                                    ));
                                    System.Windows.Forms.Application.DoEvents();

                                    loProcess.Start();

                                    // Start output to console also.
                                    if ( loProcess.StartInfo.RedirectStandardError )
                                        loProcess.BeginErrorReadLine();
                                    if ( loProcess.StartInfo.RedirectStandardOutput )
                                        loProcess.BeginOutputReadLine();

                                    if ( lbWaitForExitOverride )
                                    {
                                        // Wait the timeout period, then call "WaitForExit()" to flush the output steams.
                                        if ( loProcess.WaitForExit(60000 * loAddTask.iValue("-TimeoutMinutes", 0)) )
                                            loProcess.WaitForExit();

                                        // Stop output to console.
                                        if ( loProcess.StartInfo.RedirectStandardError )
                                            loProcess.CancelErrorRead();
                                        if ( loProcess.StartInfo.RedirectStandardOutput )
                                            loProcess.CancelOutputRead();

                                        loProcess.Close();
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Environment.ExitCode = 1;
                            this.ShowError(ex.Message, String.Format("Failed starting task: {0}", lsCommandEXE));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Environment.ExitCode = 1;
                this.ShowError(ex.Message, "Add Tasks Failed");
            }
        }

        public void KillAddedTasks()
        {
            tvProfile loAddTasksProfile = moProfile.oProfile("-AddTasks").oOneKeyProfile("-Task");

            for (int i=0; i < loAddTasksProfile.Count; ++i)
            {
                tvProfile loAddedTask = new tvProfile(loAddTasksProfile[i].ToString());

                if ( loAddedTask.bValue("-UnloadOnExit", false) && null != moAddTasksProcessArray[i] )
                {
                    this.LogIt(String.Format("Stopping Task: {0}", loAddedTask.sCommandLine()));
                    this.bKillProcess(moAddTasksProcessArray[i]);
                }
            }
        }

        private bool bListIncludesDay(string asDaysOfWeekRangeList, DateTime adtDay)
        {
                            // Look for hyphen separated alpha names (ie. ranges).
            Regex           loRangeRegex = new Regex("([a-z]+)\\s*-\\s*([a-z]+)");
                            // Split the range list into a lowercase array (assuming comma separation).
            string[]        lsRangeArray  = asDaysOfWeekRangeList.ToLower().Split(',');
                            // Replace each trimmed range with an expression that will expand to a list of discrete values.
            string[]        lsRangeRegexArray = new string[lsRangeArray.Length];
                            for (int i = 0; i < lsRangeArray.Length; ++i)
                                lsRangeRegexArray[i] = "(^|,)" + loRangeRegex.Replace(lsRangeArray[i].Trim(), "$1.*?$2") + "(,|$)";

                            // Replace each range expansion expression with the corresponding list
                            // of values (ie. day names). The day names can be full or abbreviated.
                            for (int i = 0; i < lsRangeArray.Length; ++i)
                            {
                                loRangeRegex = new Regex(lsRangeRegexArray[i]);
                                lsRangeArray[i] = loRangeRegex.Match(   // Double the day name lists to handle range wraparounds.
                                        "sunday,monday,tuesday,wednesday,thursday,friday,saturday,sunday,monday,tuesday,wednesday,thursday,friday,saturday").Value;
                                if ( String.IsNullOrEmpty(lsRangeArray[i]) )
                                    lsRangeArray[i] = loRangeRegex.Match("sun,mon,tue,wed,thu,fri,sat,sun,mon,tue,wed,thu,fri,sat").Value;
                            }

                            // Build a single searchable day list from the former day ranges.
            StringBuilder   lsbDaysOfWeekList = new StringBuilder();
                            foreach (string lsDayList in lsRangeArray)
                                lsbDaysOfWeekList.Append(lsDayList);
            string          lsDaysOfWeekList = lsbDaysOfWeekList.ToString();

            return     lsDaysOfWeekList.Contains(adtDay.ToString("dddd").ToLower())
                    || lsDaysOfWeekList.Contains(adtDay.ToString("ddd").ToLower());

            // Return true if either the lowercase day or the abbreviated lowercase day is found in the day list.
        }

        public int iBackupBeginScriptErrors()
        {
            int liBackupBeginScriptErrors = 0;

            // Before the "backup begin" script can be initialized,
            // -BackupBeginScriptPathFile and -BackupBeginScriptHelp
            // must be initialized first.
            if ( moProfile.bValue("-BackupBeginScriptInit", false) )
            {
                moProfile.Remove("-BackupBeginScriptPathFile");
                moProfile.Remove("-BackupBeginScriptHelp");
            }

            string lsBackupBeginScriptPathFile = moProfile.sRelativeToExePathFile(
                    moProfile.sValue("-BackupBeginScriptPathFile", mcsBackupBeginScriptPathFileDefault));
            string lsBackupBeginScriptOutputPathFile = lsBackupBeginScriptPathFile + ".txt";

            // If the "backup begin" script has not been redefined to point elsewhere,
            // prepare to create it from the current -BackupBeginScriptHelp content.
            // We do this even if the script file actually exists already. This way
            // the following default script will be written to the profile file if
            // it's not already there.
            if ( lsBackupBeginScriptPathFile == moProfile.sRelativeToExePathFile(mcsBackupBeginScriptPathFileDefault) )
            {
                bool    lbUseMainhostArchive    = moProfile.bValue("-UseVirtualMachineHostArchive", false);
                bool    lbUseConnectMainhost    = moProfile.bValue("-UseConnectVirtualMachineHost", false);
                string  lsBackupBeginScript     = moProfile.sValue("-BackupBeginScriptHelp", @"
@echo off
::
:: *** ""Backup Begin"" script goes here. ***
::
:: This script is executed before each backup starts. If you prompt for 
:: input within this DOS script (eg. ""pause""), the script will stay in 
:: memory. This is not recommended since such behavior would be similar to 
:: a memory leak.
::
:: You can also create and edit another DOS script file and reference that
:: instead (see ""-BackupBeginScriptPathFile"" in ""{ProfileFile}"").
::
:: You can access parameters prior to backup via the DOS shell command-line:
::
:: %1 = ""MainhostArchive""
::
::      This is the full UNC specification of the Mainhost archive share.
::
:: %2 = ""MainhostShareUsername""
::
::      This is the username needed to connect to the Mainhost network share.
::
:: %3 = ""MainhostSharePassword""
::
::      This is the password needed to connect to the Mainhost network share.
::
::
:: Note: All arguments will be passed with double quotation marks included.
::       So don't use quotes here unless you want ""double double"" quotes.
::
::
:: The following example defines a network connection to the Mainhost using
:: ""pw123"" as the password and ""anyone"" as the share username.
::
:: Example:
::
::     echo net use %1 pw123 /user:anyone  ] ""{BackupBeginScriptOutputPathFile}"" 2>&1
::          net use %1 pw123 /user:anyone ]] ""{BackupBeginScriptOutputPathFile}"" 2>&1
::
::                                        ^^  Replace brackets with darts.

:: The ""Errors"" environment variable is used to keep count of errors to be returned.
set Errors=0

:: Initialize the ""backup begin"" script log file. It's for this run only.
echo.                                      > ""{BackupBeginScriptOutputPathFile}"" 2>&1
"
+
(!lbUseMainhostArchive || !lbUseConnectMainhost ? "" :
@"
echo.                                                                       >> ""{BackupBeginScriptOutputPathFile}"" 2>&1
echo Connect to Mainhost archive:                                           >> ""{BackupBeginScriptOutputPathFile}"" 2>&1
echo.                                                                       >> ""{BackupBeginScriptOutputPathFile}"" 2>&1
echo net use %1  [password not shown]  /user:[username not shown]           >> ""{BackupBeginScriptOutputPathFile}"" 2>&1
     net use %1  %3  /user:%2                                               >> ""{BackupBeginScriptOutputPathFile}"" 2>&1

     :: Ignore Mainhost connection errors.
     ::if ERRORLEVEL 1 set /A Errors += 1
echo Note: 'net use %1' errors are ignored.                                 >> ""{BackupBeginScriptOutputPathFile}"" 2>&1
echo.                                                                       >> ""{BackupBeginScriptOutputPathFile}"" 2>&1

:: Here is some sample network connection script that may be helpful (replace brackets with darts):
::
:: echo Disconnect drive Z:                                                    ]] ""{BackupBeginScriptOutputPathFile}"" 2>&1
:: echo.                                                                       ]] ""{BackupBeginScriptOutputPathFile}"" 2>&1
:: echo net use Z: /delete /y                                                  ]] ""{BackupBeginScriptOutputPathFile}"" 2>&1
::      net use Z: /delete /y                                                  ]] ""{BackupBeginScriptOutputPathFile}"" 2>&1
::
::     :: Ignore disconnection errors.
::     ::if ERRORLEVEL 1 set /A Errors += 1
::
:: echo Connect drive Z:                                                       ]] ""{BackupBeginScriptOutputPathFile}"" 2>&1
:: echo net use Z: \\Mainhost\ArchiveGoPC                                      ]] ""{BackupBeginScriptOutputPathFile}"" 2>&1
::      net use Z: \\Mainhost\ArchiveGoPC                                      ]] ""{BackupBeginScriptOutputPathFile}"" 2>&1
::
::     if ERRORLEVEL 1 set /A Errors += 1
"
)
+
@"
echo.                                                                       >> ""{BackupBeginScriptOutputPathFile}"" 2>&1
echo   Errors=%Errors%                                                      >> ""{BackupBeginScriptOutputPathFile}"" 2>&1
exit  %Errors%
"
)
                        .Replace("{ProfileFile}", Path.GetFileName(moProfile.sActualPathFile))
                        .Replace("{BackupBeginScriptOutputPathFile}", Path.GetFileName(lsBackupBeginScriptOutputPathFile))
                        ;

                // Write the default "backup begin" script if it's
                // not there or if -BackupBeginScriptInit is set.
                if (       !File.Exists(lsBackupBeginScriptPathFile)
                        || moProfile.bValue("-BackupBeginScriptInit", false) )
                {
                    StreamWriter loStreamWriter = null;

                    try
                    {
                        loStreamWriter = new StreamWriter(lsBackupBeginScriptPathFile, false);
                        loStreamWriter.Write(lsBackupBeginScript);

                        // This is used only once then reset.
                        moProfile["-BackupBeginScriptInit"] = false;
                        moProfile.Save();
                    }
                    catch (Exception ex)
                    {
                        Environment.ExitCode = 1;
                        this.ShowError(string.Format("File Write Failure: \"{0}\"\r\n"
                                , lsBackupBeginScript) + ex.Message
                                , "Failed Writing File"
                                );
                    }
                    finally
                    {
                        if ( null != loStreamWriter )
                            loStreamWriter.Close();
                    }
                }
            }

            try
            {
                this.LogIt("");
                this.LogIt("Running \"backup begin\" script ...");

                // Update the output text cache.
                if ( null != this.oUI )
                this.oUI.Dispatcher.BeginInvoke((Action)(() =>
                {
                    this.oUI.GetSetOutputTextPanelCache();
                }
                ));
                System.Windows.Forms.Application.DoEvents();

                // Run the "backup begin" script.
                Process loProcess = new Process();
                        loProcess.StartInfo.FileName = lsBackupBeginScriptPathFile;
                        loProcess.StartInfo.Arguments = string.Format(
                                  " \"{0}\" \"{1}\" \"{2}\" \"{3}\" \"{4}\" \"{5}\" \"{6}\" \"{7}\" \"{8}\" \"{9}\" "
                                , moProfile.sValue("-VirtualMachineHostArchivePath", "")
                                , moProfile.sValue("-VirtualMachineHostUsername", "")
                                , moProfile.sValue("-VirtualMachineHostPassword", "")
                                , ""
                                , ""
                                , ""
                                , ""
                                , ""
                                , ""
                                , ""
                                );
                        loProcess.StartInfo.WorkingDirectory = Path.GetDirectoryName(moProfile.sExePathFile);
                        loProcess.StartInfo.UseShellExecute = true;
                        loProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        loProcess.Start();

                DateTime ltdProcessTimeout = DateTime.Now.AddMinutes(moProfile.iValue("-BackupBeginScriptTimeoutMins", 5));

                // Wait for the "backup begin" script to finish.
                while ( !this.bMainLoopStopped && !loProcess.HasExited && DateTime.Now < ltdProcessTimeout )
                {
                    System.Windows.Forms.Application.DoEvents();
                    System.Threading.Thread.Sleep(moProfile.iValue("-MainLoopSleepMS", 100));
                }

                if ( DateTime.Now >= ltdProcessTimeout && !this.bMainLoopStopped )
                {
                    liBackupBeginScriptErrors++;

                    this.LogIt("");
                    this.LogIt("The \"backup begin\" script timed-out.");
                }

                // If the process failed or a stop request came through, kill the "backup begin" script.
                if ( (liBackupBeginScriptErrors > 0 || this.bMainLoopStopped) && !this.bKillProcess(loProcess) )
                    this.ShowError("The \"backup begin\" script could not be stopped."
                            , "Backup Failed");

                if ( !this.bMainLoopStopped )
                {
                    liBackupBeginScriptErrors += loProcess.ExitCode;

                    if ( 0 == liBackupBeginScriptErrors )
                    {
                        this.LogIt("The \"backup begin\" script finished successfully.");
                    }
                    else
                    {
                        this.LogIt(string.Format("The \"backup begin\" script had {0} error{1}.\r\n"
                                , liBackupBeginScriptErrors
                                , 1 == liBackupBeginScriptErrors ? "" : "s")
                                );

                        // Get the output from the "backup begin" script.

                        string lsFileAsStream = this.sFileAsStream(lsBackupBeginScriptOutputPathFile);

                        this.LogIt("Here's output from the \"backup begin\" script:\r\n" + lsFileAsStream);

                        if ( moProfile.bValue("-ShowBackupBeginScriptErrors", true) )
                            this.DisplayFileAsErrors(lsFileAsStream, "Backup Begin Script Errors");
                    }
                }

                loProcess.Close();
            }
            catch (Exception ex)
            {
                Environment.ExitCode = 1;
                ++liBackupBeginScriptErrors;
                this.SetBackupFailed();
                this.ShowError(ex.Message, "Failed Running \"Backup Begin\" Script");
            }

            return liBackupBeginScriptErrors;
        }

        public int iBackupDoneScriptCopyFailuresWithBitField()
        {
            if ( null == moProfile["-PreviousBackupOutputPathFile"] || !File.Exists(moProfile["-PreviousBackupOutputPathFile"].ToString()) )
            {
                this.LogIt("");
                this.LogIt("The backup does not exist. The \"backup done\" script was not run.");
                return 0;
            }

            int liBackupDoneScriptCopyFailuresWithBitField = 0;

            // Before the "backup done" script can be initialized,
            // -BackupDoneScriptPathFile and -BackupDoneScriptHelp
            // must be initialized first.
            if ( moProfile.bValue("-BackupDoneScriptInit", false) )
            {
                moProfile.Remove("-BackupDoneScriptPathFile");
                moProfile.Remove("-BackupDoneScriptHelp");
            }

            string lsBackupDoneScriptPathFile = moProfile.sRelativeToExePathFile(
                    moProfile.sValue("-BackupDoneScriptPathFile", mcsBackupDoneScriptPathFileDefault));
            string lsBackupDoneScriptOutputPathFile = lsBackupDoneScriptPathFile + ".txt";

            // If the "backup done" script has not been redefined to point elsewhere,
            // prepare to create it from the current -BackupDoneScriptHelp content.
            // We do this even if the script file actually exists already. This way
            // the following default script will be written to the profile file if
            // it's not already there.
            if ( lsBackupDoneScriptPathFile == moProfile.sRelativeToExePathFile(mcsBackupDoneScriptPathFileDefault) )
            {
                bool    lbUseMainhostArchive    = moProfile.bValue("-UseVirtualMachineHostArchive", false);
                bool    lbUseConnectMainhost    = moProfile.bValue("-UseConnectVirtualMachineHost", false);
                string  lsBackupDoneScript      = moProfile.sValue("-BackupDoneScriptHelp", @"
@echo off
if %1=="""" goto :EOF
::
:: *** ""Backup Done"" script goes here. ***
::
:: This script is executed after each successful backup completes. If you 
:: prompt for input within this DOS script (eg. ""pause""), the script
:: will stay in memory. This is not recommended since such behavior would
:: be similar to a memory leak.
::
:: You can also create and edit another DOS script file and reference that
:: instead (see ""-BackupDoneScriptPathFile"" in ""{ProfileFile}""). You
:: can access several parameters from the completed backup via the DOS shell
:: command-line:
::
:: %1 = ""BackupOutputPathFile""
::
::      This is the full path\file specification of the backup file.
::      It includes the output filename as well as the embedded date.
::
:: %2 = ""BackupOutputFilename""
::
::      This is the backup filename only (ie. no path). It includes the
::      embedded date as well as the filename extension.
::
:: %3 = ""BackupBaseOutputFilename""
::
::      This is the backup filename with no path and no date. It's just
::      the base output filename name with the filename extension.
::
:: %4 = ""LocalArchivePath""
::
::      This is the local archive folder.
::
:: %5 = ""VirtualMachineHostArchive""
::
::      This is the virtual machine host archive share name.
::
:: %6 = ""LogPathFile""
::
::      This is the full path\file specification of the backup log file.
::
::
:: Note: All arguments will be passed with double quotation marks included.
::       So don't use quotes here unless you want ""double double"" quotes.
::       Also, ERRORLEVEL is not reliable enough to be heavily used below.
::
:: The following example copies the backup file to the root of drive C:
:: (if it's ""AdministratorFiles.zip""). Then it outputs a directory listing
:: of the archive folder.
::
:: Example:
::
:: if not %3.==""AdministratorFiles.zip"". goto :EOF
::
::     echo copy %1 C:\  ] ""{BackupDoneScriptOutputPathFile}"" 2>&1
::          copy %1 C:\ ]] ""{BackupDoneScriptOutputPathFile}"" 2>&1
::
:: dir %4               ]] ""{BackupDoneScriptOutputPathFile}"" 2>&1
::
::                      ^^  Replace brackets with darts.

:: The ""CopyFailures"" environment variable is used to keep count of errors to be returned.
set CopyFailures=0

:: Initialize the ""backup done"" script log file. It's for this run only.
echo.                    > ""{BackupDoneScriptOutputPathFile}"" 2>&1
"
+
(!lbUseMainhostArchive ? "" :
@"
:: This references the backup destination copy on the VM host:
set FileSpec=%5\%2

echo.                                                                       >> ""{BackupDoneScriptOutputPathFile}"" 2>&1
echo This copies the backup to the virtual machine host archive:            >> ""{BackupDoneScriptOutputPathFile}"" 2>&1
echo.                                                                       >> ""{BackupDoneScriptOutputPathFile}"" 2>&1
echo copy %1 %5                                                             >> ""{BackupDoneScriptOutputPathFile}"" 2>&1
     copy %1 %5                                                             >> ""{BackupDoneScriptOutputPathFile}"" 2>&1
     if not exist %FileSpec% echo   Error: %FileSpec% is not there.         >> ""{BackupDoneScriptOutputPathFile}"" 2>&1

     if not exist %FileSpec% set /A CopyFailures += 1
"
)
+
@"
echo.                                                                       >> ""{BackupDoneScriptOutputPathFile}"" 2>&1
echo The following copies the backup (base name) to each attached backup    >> ""{BackupDoneScriptOutputPathFile}"" 2>&1
echo device with the file ""{BackupDriveToken}"" at its root.               >> ""{BackupDoneScriptOutputPathFile}"" 2>&1

set BackupOutputPathFile=%1
set BackupBaseOutputFilename=%3

set BackupDeviceDecimalBitField=0
set BackupDevicePositionExponent=23

:: There are 23 drive letters listed (ie. possible backup devices). A 32-bit integer
:: can handle no more when a corresponding bit field is combined with copy failures.
for %%d in (D: E: F: G: H: I: J: K: L: M: N: O: P: Q: R: S: T: U: V: W: X: Y: Z:) do call :DoCopy %%d

:: Set bit 24 (ie. add 2^23 = 8,388,608) to preserve bit field's leading zeros.
:: Combine the bit field and the copy failures into a single composite value.
:: The factor of 100 means that there can be a maximum of 99 copy failures.

set /A CompositeResult = 100 * (8388608 + %BackupDeviceDecimalBitField%) + %CopyFailures%

echo.                                                                       >> ""{BackupDoneScriptOutputPathFile}"" 2>&1
echo.                                                                       >> ""{BackupDoneScriptOutputPathFile}"" 2>&1
echo CompositeResult=%CompositeResult%                                      >> ""{BackupDoneScriptOutputPathFile}"" 2>&1

exit  %CompositeResult%

:DoCopy
set /A BackupDevicePositionExponent -= 1
dir %1 > nul 2> nul
if ERRORLEVEL 1 goto :EOF
if not exist %1\""{BackupDriveToken}"" goto :EOF

:: Determine the bit position (and the corresponding decimal value) from the exponent.
set BitFieldDevicePosition=1
for /L %%x in (1, 1, %BackupDevicePositionExponent%) do set /A BitFieldDevicePosition *= 2

:: Add the calculated positional value to the bit field for the current backup device.
set /A BackupDeviceDecimalBitField += %BitFieldDevicePosition%


:: This references the backup destination copy on the current backup device (%1):
set FileSpec=%1\%BackupBaseOutputFilename%

echo.                                                                       >> ""{BackupDoneScriptOutputPathFile}"" 2>&1
echo This removes the previous backup (if any) from %1                      >> ""{BackupDoneScriptOutputPathFile}"" 2>&1
echo.                                                                       >> ""{BackupDoneScriptOutputPathFile}"" 2>&1
echo del %FileSpec%                                                         >> ""{BackupDoneScriptOutputPathFile}"" 2>&1
     del %FileSpec%                                                         >> ""{BackupDoneScriptOutputPathFile}"" 2>&1
     if exist %FileSpec% echo   Error: %FileSpec% is still there.           >> ""{BackupDoneScriptOutputPathFile}"" 2>&1

     if exist %FileSpec% set /A CopyFailures += 1

echo.                                                                       >> ""{BackupDoneScriptOutputPathFile}"" 2>&1
echo This copies the current backup to %1                                   >> ""{BackupDoneScriptOutputPathFile}"" 2>&1
echo.                                                                       >> ""{BackupDoneScriptOutputPathFile}"" 2>&1
echo copy %BackupOutputPathFile% %FileSpec%                                 >> ""{BackupDoneScriptOutputPathFile}"" 2>&1
     copy %BackupOutputPathFile% %FileSpec%                                 >> ""{BackupDoneScriptOutputPathFile}"" 2>&1
     if not exist %FileSpec% echo   Error: %FileSpec% is not there.         >> ""{BackupDoneScriptOutputPathFile}"" 2>&1

     if not exist %FileSpec% set /A CopyFailures += 1
"
)
                        .Replace("{ProfileFile}", Path.GetFileName(moProfile.sActualPathFile))
                        .Replace("{BackupDoneScriptOutputPathFile}", Path.GetFileName(lsBackupDoneScriptOutputPathFile))
                        .Replace("{BackupDriveToken}", this.sBackupDriveToken)
                        ;

                // Write the default "backup done" script if it's
                // not there or if -BackupDoneScriptInit is set.
                if (       !File.Exists(lsBackupDoneScriptPathFile)
                        || moProfile.bValue("-BackupDoneScriptInit", false) )
                {
                    StreamWriter loStreamWriter = null;

                    try
                    {
                        loStreamWriter = new StreamWriter(lsBackupDoneScriptPathFile, false);
                        loStreamWriter.Write(lsBackupDoneScript);

                        // This is used only once then reset.
                        moProfile["-BackupDoneScriptInit"] = false;
                        moProfile.Save();
                    }
                    catch (Exception ex)
                    {
                        Environment.ExitCode = 1;
                        this.ShowError(string.Format("File Write Failure: \"{0}\"\r\n"
                                , lsBackupDoneScript) + ex.Message
                                , "Failed Writing File"
                                );
                    }
                    finally
                    {
                        if ( null != loStreamWriter )
                            loStreamWriter.Close();
                    }
                }
            }

            try
            {
                this.LogIt("");
                this.LogIt("Running \"backup done\" script ...");

                // Update the output text cache.
                if ( null != this.oUI )
                this.oUI.Dispatcher.BeginInvoke((Action)(() =>
                {
                    this.oUI.GetSetOutputTextPanelCache();
                }
                ));
                System.Windows.Forms.Application.DoEvents();

                // Load cached arguments.
                tvProfile   loArgs = new tvProfile();
                            loArgs.LoadFromCommandLine(moProfile.sValue("-LastBackupArgs", ""), tvProfileLoadActions.Append);

                // Run the "backup done" script.
                Process loProcess = new Process();
                        loProcess.StartInfo.FileName = lsBackupDoneScriptPathFile;
                        loProcess.StartInfo.Arguments = string.Format(
                                  " \"{0}\" \"{1}\" \"{2}\" \"{3}\" \"{4}\" \"{5}\" \"{6}\" \"{7}\" \"{8}\" \"{9}\" "
                                , loArgs.sValue("-BackupOutputPathFile"         , "")
                                , loArgs.sValue("-BackupOutputFilename"         , "")
                                , loArgs.sValue("-BackupBaseOutputFilename"     , "")
                                , loArgs.sValue("-LocalArchivePath"             , "")
                                , loArgs.sValue("-VirtualMachineHostArchivePath", "")
                                , loArgs.sValue("-LogPathFile"                  , "")
                                , ""
                                , ""
                                , ""
                                , ""
                                );
                        loProcess.StartInfo.WorkingDirectory = Path.GetDirectoryName(moProfile.sExePathFile);
                        loProcess.StartInfo.UseShellExecute = true;
                        loProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        loProcess.Start();

                DateTime ltdProcessTimeout = DateTime.Now.AddMinutes(moProfile.iValue("-BackupDoneScriptTimeoutMins", 180));

                // Wait for the "backup done" script to finish.
                while ( !this.bMainLoopStopped && !loProcess.HasExited && DateTime.Now < ltdProcessTimeout )
                {
                    System.Windows.Forms.Application.DoEvents();
                    System.Threading.Thread.Sleep(moProfile.iValue("-MainLoopSleepMS", 100));
                }

                if ( DateTime.Now >= ltdProcessTimeout && !this.bMainLoopStopped )
                {
                    this.LogIt("");
                    this.LogIt("The \"backup done\" script timed-out.");
                }

                // If the process failed or a stop request came through, kill the "backup done" script.
                if ( (DateTime.Now >= ltdProcessTimeout || this.bMainLoopStopped) && !this.bKillProcess(loProcess) )
                    this.ShowError("The \"backup done\" script could not be stopped."
                            , "Backup Failed");

                if ( !this.bMainLoopStopped )
                {
                    // The exit code is defined in the script as a combination of two integers:
                    // a bit field of found backup devices and a count of copy failures (99 max).
                    if ( DateTime.Now < ltdProcessTimeout )
                        liBackupDoneScriptCopyFailuresWithBitField = loProcess.ExitCode;
                    else
                        liBackupDoneScriptCopyFailuresWithBitField = 100 * this.iCurrentBackupDevicesBitField() + 1;

                    double ldCompositeResult = liBackupDoneScriptCopyFailuresWithBitField / 100.0;

                    int liCurrentBackupDevicesBitField = (int)ldCompositeResult;// The integer part is the bit field.
                                                                                // The fractional part (x 100) is the number of copy failures.
                    int liBackupDoneScriptCopyFailures = (int)Math.Round(100 * (ldCompositeResult - liCurrentBackupDevicesBitField));

                    // Compare the bit field of current backup devices to the bit field of devices selected by the user.
                    List<char> loMissingBackupDevices = this.oMissingBackupDevices(liCurrentBackupDevicesBitField);

                    if ( 0 == liBackupDoneScriptCopyFailures && DateTime.Now < ltdProcessTimeout && 0 == loMissingBackupDevices.Count )
                    {
                        this.LogIt("The \"backup done\" script finished successfully.");
                    }
                    else
                    {
                        if ( 0 != liBackupDoneScriptCopyFailures )
                        {
                            this.LogIt(string.Format("The \"backup done\" script had {0} copy failure{1}.\r\n"
                                    , liBackupDoneScriptCopyFailures
                                    , 1 == liBackupDoneScriptCopyFailures ? "" : "s")
                                    );

                            // Get the output from the "backup done" script.

                            string lsFileAsStream = this.sFileAsStream(lsBackupDoneScriptOutputPathFile);

                            this.LogIt("Here's output from the \"backup done\" script:\r\n" + lsFileAsStream);

                            if ( moProfile.bValue("-ShowBackupDoneScriptErrors", true) )
                                this.DisplayFileAsErrors(lsFileAsStream, "Backup Done Script Errors");
                        }

                        if ( 0 != loMissingBackupDevices.Count )
                            this.LogIt(string.Format("The \"backup done\" script noticed {0} backup device{1} missing.\r\n"
                                    , loMissingBackupDevices.Count
                                    , 1 == loMissingBackupDevices.Count ? "" : "s")
                                    );
                    }
                }

                loProcess.Close();
            }
            catch (Exception ex)
            {
                Environment.ExitCode = 1;
                ++liBackupDoneScriptCopyFailuresWithBitField;
                this.SetBackupFailed();
                this.ShowError(ex.Message, "Failed Running \"Backup Done\" Script");
            }

            return liBackupDoneScriptCopyFailuresWithBitField;
        }

        public void SetBackupFailed()
        {
            moProfile["-PreviousBackupOk"] = false;
            moProfile["-PreviousBackupTime"] = DateTime.Now;
            moProfile.Save();
        }

        public void BackupFailedScript()
        {
            bool lbScriptFailed = false;

            // Before the "backup failed" script can be initialized,
            // -BackupFailedScriptPathFile and -BackupFailedScriptHelp
            // must be initialized first.
            if ( moProfile.bValue("-BackupFailedScriptInit", false) )
            {
                moProfile.Remove("-BackupFailedScriptPathFile");
                moProfile.Remove("-BackupFailedScriptHelp");
            }

            string  lsBackupFailedScriptPathFile = moProfile.sRelativeToExePathFile(
                    moProfile.sValue("-BackupFailedScriptPathFile", mcsBackupFailedScriptPathFileDefault));
            string  lsBackupFailedScriptOutputPathFile = lsBackupFailedScriptPathFile + ".txt";

            // If the "backup failed" script has not been redefined to point elsewhere,
            // prepare to create it from the current -BackupFailedScriptHelp content.
            // We do this even if the script file actually exists already. This way
            // the following default script will be written to the profile file if
            // it's not already there.
            if ( lsBackupFailedScriptPathFile == moProfile.sRelativeToExePathFile(mcsBackupFailedScriptPathFileDefault) )
            {
                string  lsBackupFailedScript = moProfile.sValue("-BackupFailedScriptHelp", @"
@echo off
if %1=="""" goto :EOF
::
:: *** ""Backup Failed"" script goes here. ***
::
:: This script is executed after each backup fails to complete. If you 
:: prompt for input within this DOS script (eg. ""pause""), the script
:: will stay in memory. This is not recommended since such behavior would
:: be similar to a memory leak.
::
:: You can also create and edit another DOS script file and reference that
:: instead (see ""-BackupFailedScriptPathFile"" in ""{ProfileFile}""). You
:: can access several parameters from the completed backup via the DOS shell
:: command-line:
::
:: %1 = ""BackupOutputPathFile""
::
::      This is the full path\file specification of the backup file.
::      It includes the output filename as well as the embedded date.
::
:: %2 = ""BackupOutputFilename""
::
::      This is the backup filename only (ie. no path). It includes the
::      embedded date as well as the filename extension.
::
:: %3 = ""BackupBaseOutputFilename""
::
::      This is the backup filename with no path and no date. It's just
::      the base output filename name with the filename extension.
::
:: %4 = ""LocalArchivePath""
::
::      This is the local archive folder.
::
:: %5 = ""VirtualMachineHostArchive""
::
::      This is the virtual machine host archive share name.
::
::
:: Note: All arguments will be passed with double quotation marks included.
::       So don't use quotes here unless you want ""double double"" quotes.
::       Also, ERRORLEVEL is not reliable enough to be heavily used below.
::
:: The following example copies the backup file to the root of drive C:
:: (if it's ""AdministratorFiles.zip""). Then it outputs a directory listing
:: of the archive folder.
::
:: Example:
::
:: if not %3.==""AdministratorFiles.zip"". goto :EOF
::
::     echo copy %1 C:\  ] ""{BackupFailedScriptOutputPathFile}"" 2>&1
::          copy %1 C:\ ]] ""{BackupFailedScriptOutputPathFile}"" 2>&1
::
:: dir %4               ]] ""{BackupFailedScriptOutputPathFile}"" 2>&1
::
::                      ^^  Replace brackets with darts.

:: Initialize the ""backup failed"" script log file. It's for this run only.
echo.                    > ""{BackupFailedScriptOutputPathFile}"" 2>&1

:: This references the backup file by named variable (rather than positionally):
set FileSpec=%1

if not exist %FileSpec% echo %FileSpec% is not there. Nothing to do.            >> ""{BackupFailedScriptOutputPathFile}"" 2>&1
if not exist %FileSpec% goto :EOF

:: Any failed backup file less than this size will be removed.
set MinFileBytes=1024

set Filesize=%~z1

echo If the failed backup file is smaller than %MinFileBytes% bytes,            >> ""{BackupFailedScriptOutputPathFile}"" 2>&1
echo it will be removed.                                                        >> ""{BackupFailedScriptOutputPathFile}"" 2>&1
echo.                                                                           >> ""{BackupFailedScriptOutputPathFile}"" 2>&1
echo %FileSpec% is %Filesize% bytes.                                            >> ""{BackupFailedScriptOutputPathFile}"" 2>&1

if %Filesize% lss %MinFileBytes% goto RemoveIt

echo.                                                                           >> ""{BackupFailedScriptOutputPathFile}"" 2>&1
echo The file is not smaller than the minimmum (%MinFileBytes% bytes). Keep it. >> ""{BackupFailedScriptOutputPathFile}"" 2>&1
goto :EOF


:RemoveIt
echo.                                                                           >> ""{BackupFailedScriptOutputPathFile}"" 2>&1
echo The file is smaller than the minimmum (%MinFileBytes% bytes). Remove it.   >> ""{BackupFailedScriptOutputPathFile}"" 2>&1
echo.                                                                           >> ""{BackupFailedScriptOutputPathFile}"" 2>&1
echo This removes the failed backup file:                                       >> ""{BackupFailedScriptOutputPathFile}"" 2>&1
echo.                                                                           >> ""{BackupFailedScriptOutputPathFile}"" 2>&1
echo del %FileSpec%                                                             >> ""{BackupFailedScriptOutputPathFile}"" 2>&1
     del %FileSpec%                                                             >> ""{BackupFailedScriptOutputPathFile}"" 2>&1
     if exist %FileSpec% echo   Error: %FileSpec% is still there.               >> ""{BackupFailedScriptOutputPathFile}"" 2>&1
     if not exist %FileSpec% echo     %FileSpec% has been removed.              >> ""{BackupFailedScriptOutputPathFile}"" 2>&1
"
)
                        .Replace("{ProfileFile}", Path.GetFileName(moProfile.sActualPathFile))
                        .Replace("{BackupFailedScriptOutputPathFile}", Path.GetFileName(lsBackupFailedScriptOutputPathFile))
                        ;

                // Write the default "backup failed" script if it's
                // not there or if -BackupFailedScriptInit is set.
                if (       !File.Exists(lsBackupFailedScriptPathFile)
                        || moProfile.bValue("-BackupFailedScriptInit", false) )
                {
                    StreamWriter loStreamWriter = null;

                    try
                    {
                        loStreamWriter = new StreamWriter(lsBackupFailedScriptPathFile, false);
                        loStreamWriter.Write(lsBackupFailedScript);

                        // This is used only once then reset.
                        moProfile["-BackupFailedScriptInit"] = false;
                        moProfile.Save();
                    }
                    catch (Exception ex)
                    {
                        Environment.ExitCode = 1;
                        this.ShowError(string.Format("File Write Failure: \"{0}\"\r\n"
                                , lsBackupFailedScript) + ex.Message
                                , "Failed Writing File"
                                );
                    }
                    finally
                    {
                        if ( null != loStreamWriter )
                            loStreamWriter.Close();
                    }
                }
            }

            try
            {
                this.LogIt("");
                this.LogIt("Running \"backup failed\" script ...");

                // Update the output text cache.
                if ( null != this.oUI )
                this.oUI.Dispatcher.BeginInvoke((Action)(() =>
                {
                    this.oUI.GetSetOutputTextPanelCache();
                }
                ));
                System.Windows.Forms.Application.DoEvents();

                // Load cached arguments.
                tvProfile   loArgs = new tvProfile();
                            loArgs.LoadFromCommandLine(moProfile.sValue("-LastBackupArgs", ""), tvProfileLoadActions.Append);

                // Run the "backup failed" script.
                Process loProcess = new Process();
                        loProcess.StartInfo.FileName = lsBackupFailedScriptPathFile;
                        loProcess.StartInfo.Arguments = string.Format(
                                  " \"{0}\" \"{1}\" \"{2}\" \"{3}\" \"{4}\" \"{5}\" \"{6}\" \"{7}\" \"{8}\" \"{9}\" "
                                , loArgs.sValue("-BackupOutputPathFile"         , "")
                                , loArgs.sValue("-BackupOutputFilename"         , "")
                                , loArgs.sValue("-BackupBaseOutputFilename"     , "")
                                , loArgs.sValue("-LocalArchivePath"             , "")
                                , loArgs.sValue("-VirtualMachineHostArchivePath", "")
                                , ""
                                , ""
                                , ""
                                , ""
                                , ""
                                );
                        loProcess.StartInfo.WorkingDirectory = Path.GetDirectoryName(moProfile.sExePathFile);
                        loProcess.StartInfo.UseShellExecute = true;
                        loProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        loProcess.Start();

                DateTime ltdProcessTimeout = DateTime.Now.AddMinutes(moProfile.iValue("-BackupFailedScriptTimeoutMins", 1));

                // Wait for the "backup failed" script to finish.
                while ( !this.bMainLoopStopped && !loProcess.HasExited && DateTime.Now < ltdProcessTimeout )
                {
                    System.Windows.Forms.Application.DoEvents();
                    System.Threading.Thread.Sleep(moProfile.iValue("-MainLoopSleepMS", 100));
                }

                if ( DateTime.Now >= ltdProcessTimeout && !this.bMainLoopStopped )
                {
                    lbScriptFailed = true;

                    this.LogIt("");
                    this.LogIt("The \"backup failed\" script timed-out.");
                }

                // If the process failed or a stop request came through, kill the "backup failed" script.
                if ( (lbScriptFailed || this.bMainLoopStopped) && !this.bKillProcess(loProcess) )
                    this.ShowError("The \"backup failed\" script could not be stopped.", "Backup Failed");

                if ( !this.bMainLoopStopped )
                {
                    if ( !lbScriptFailed && 0 == loProcess.ExitCode )
                    {
                        this.LogIt("The \"backup failed\" script finished successfully.");
                    }
                    else
                    {
                        this.LogIt("The \"backup failed\" script did NOT finish successfully.");
                    }
  
                    // Get the output from the "backup failed" script.

                    this.LogIt("\r\nHere's output from the \"backup failed\" script:\r\n"
                            + this.sFileAsStream(lsBackupFailedScriptOutputPathFile));
                }

                loProcess.Close();
            }
            catch (Exception ex)
            {
                Environment.ExitCode = 1;
                this.SetBackupFailed();
                this.ShowError(ex.Message, "Failed Running \"Backup Failed\" Script");
            }
        }

        // Determine missing backup devices by comparing the given bit field of current 
        // backup devices to the bit field of previously selected backup devices.
        public List<char> oMissingBackupDevices(int aiCurrentBackupDevicesBitField)
        {
            List<char> loMissingBackupDevices = new List<char>();

            int liSelectedBackupDevicesBitField = Convert.ToInt32(moProfile.sValue("-SelectedBackupDevicesBitField", "0"), 2);

            char lcPossibleDriveLetter = this.cPossibleDriveLetterBegin;

            for (int i = (this.cPossibleDriveLetterEnd - this.cPossibleDriveLetterBegin); i > -1; --i)
            {
                int liDriveBit = (int)Math.Pow(2, i);

                // Add drive letter to list if selected device bit is set while current device bit is not.
                if ( (liSelectedBackupDevicesBitField & liDriveBit) > (aiCurrentBackupDevicesBitField & liDriveBit) )
                    loMissingBackupDevices.Add(lcPossibleDriveLetter);

                ++lcPossibleDriveLetter;
            };

            return loMissingBackupDevices;
        }

        private void BackupProcessOutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            this.LogIt(outLine.Data);
        }


        /// <summary>
        /// Deletes files greater than the given age
        /// days and of the given file specifications.
        /// </summary>
        public bool CleanupFiles()
        {
            // Return if cleanup is disabled.
            if ( !moProfile.bValue("-CleanupFiles", true) )
            {
                this.LogIt("");
                this.LogIt("Cleanup files is disabled.");
                return true;
            }
            // Return if backup is enabled and it was stopped.
            if (  moProfile.bValue("-BackupFiles", true) && this.bMainLoopStopped )
                return true;
            else
                this.bMainLoopStopped = false;

            bool lbCleanupFiles = true;

            this.LogIt("");
            this.LogIt("File cleanup started ...");

            // Write the deleted file list header to disk.
            string  lsDeletedFileListOutputPathFile = moProfile.sRelativeToProfilePathFile(this.sDeletedFileListOutputPathFile);

            if ( !File.Exists(lsDeletedFileListOutputPathFile) )
            {
                // Get the column header array.
                string[] lsColumnHeaderArray = moProfile.sValue("-DeletedFileListOutputColumnHeaderArray", "Deleted File Time,File Size,Former File Location").Split(',');

                StreamWriter loStreamWriter = null;

                try
                {
                    loStreamWriter = new StreamWriter(lsDeletedFileListOutputPathFile, false);

                    // First, output the file header.
                    loStreamWriter.WriteLine(string.Format(moProfile.sValue("-DeletedFileListOutputHeader"
                            , "{0, -10:MM-dd-yyyy} File Cleanup List"), DateTime.Today));
                    loStreamWriter.WriteLine();

                    // Next output the column headers properly formatted to match the forthcoming data rows.
                    loStreamWriter.WriteLine(string.Format(
                              moProfile.sValue("-DeletedFileListOutputColumnFormat", "{0, -22:MM-dd-yyyy hh:mm:ss tt}  {1, 13:#,#}  {2}")
                            , lsColumnHeaderArray[0], lsColumnHeaderArray[1], lsColumnHeaderArray[2]));
                    loStreamWriter.WriteLine();
                }
                catch (Exception ex)
                {
                    Environment.ExitCode = 1;
                    this.ShowError(string.Format("File Write Failure: \"{0}\"\r\n"
                            , lsDeletedFileListOutputPathFile) + ex.Message
                            , "Failed Writing File"
                            );
                }
                finally
                {
                    if ( null != loStreamWriter )
                        loStreamWriter.Close();
                }
            }

            try
            {
                // This is used elsewhere to warn users if the software
                // has not been properly configured for file cleanups.
                this.mbHasNoDeletionGroups = true;

                if ( !moProfile.ContainsKey("-CleanupSet") )
                {
                    // Create the default file cleanup sets.

                    // Get the primary backup set (ie. the 1st).
                    tvProfile loBackupSet1Profile = new tvProfile(moProfile.sValue("-BackupSet", "(not set)"));
                    string lsBackupOutputPathFileBase = this.sBackupOutputPathFileBase(loBackupSet1Profile);
                    string lsBackupOutputPath = Path.GetDirectoryName(lsBackupOutputPathFileBase);
                    string lsBackupOutputFilenameNoExt = Path.GetFileNameWithoutExtension(lsBackupOutputPathFileBase);

                    // Initially set the cleanup of primary backups to "no cleanup" (ie. 1000 years).
                    // The deletion limit prevents old file removal without new files to replace them.
                    moProfile.Add("-CleanupSet", string.Format(@"
    -AgeDays=365000
    -FilesToDelete={0}*{1}

"
                            , Path.Combine(lsBackupOutputPath, lsBackupOutputFilenameNoExt)
                            , Path.GetExtension(lsBackupOutputPathFileBase)
                            ));

                    // Set the cleanup of temporary backup files to 0 days.
                    // This is necessary to cleanup after killed processes.
                    moProfile.Add("-CleanupSet", string.Format(@"
    -AgeDays=0
    -FilesToDelete={0}.tmp*

"
                            , Path.Combine(this.sArchivePath(), "*" + Path.GetExtension(lsBackupOutputPathFileBase))
                            ));

                    // Set the cleanup of file lists and backup / cleanup log files to 30 days.
                    moProfile.Add("-CleanupSet", string.Format(@"
    -AgeDays=30
    -FilesToDelete={0}
    -FilesToDelete={1}

"
                            , Path.Combine(Path.GetDirectoryName(this.sZipToolFileListPathFileBase)
                                    , "*" + Path.GetExtension(this.sZipToolFileListPathFileBase))

                            , Path.Combine(Path.GetDirectoryName(DoGoPcBackup.sLogPathFileBase(moProfile))
                                    , "*" + Path.GetExtension(DoGoPcBackup.sLogPathFileBase(moProfile)))
                            ));
                }

                // Get all cleanup sets.
                tvProfile loCleanupSetsProfile = moProfile.oOneKeyProfile("-CleanupSet");

                foreach (DictionaryEntry loEntry in loCleanupSetsProfile)
                {
                    System.Windows.Forms.Application.DoEvents();
                    if ( this.bMainLoopStopped )
                        break;

                    // Convert the current cleanup set from a command-line string to a profile oject.
                    tvProfile loCurrentCleanupSet = new tvProfile(loEntry.Value.ToString());

                    // The default "LastWriteTime" is the last modified datetime.
                    FileDateTimeTypes leFileDateTimeType;
                            switch (loCurrentCleanupSet.sValue("-DeletedFileListDateTimeType", "LastWriteTime"))
                            {
                                case "CreationTime":
                                    leFileDateTimeType = FileDateTimeTypes.CreationTime;
                                    break;
                                case "LastAccessTime":
                                    leFileDateTimeType = FileDateTimeTypes.LastAccessTime;
                                    break;
                                default:
                                    leFileDateTimeType = FileDateTimeTypes.LastWriteTime;
                                    break;
                            }
                    // Use 1000 years as the default file age.
                    int         liAgeDays = loCurrentCleanupSet.iValue("-AgeDays", 365000);
                                liAgeDays = 0 == liAgeDays ? -1 : liAgeDays;
                                // "0" means "delete everything." -1 makes that happen.
                    DateTime    ldtOlderThan = DateTime.Now.AddDays(-liAgeDays);

                    // Get the list of path\file specifications to delete.
                    tvProfile loFilesToDeleteProfile = loCurrentCleanupSet.oOneKeyProfile("-FilesToDelete");

                    foreach (DictionaryEntry loPathFilesEntry in loFilesToDeleteProfile)
                    {
                        System.Windows.Forms.Application.DoEvents();
                        if ( this.bMainLoopStopped )
                            break;

                        // Being here means there is at least one set of files to delete.
                        this.mbHasNoDeletionGroups = false;

                        if ( lbCleanupFiles )
                            lbCleanupFiles = this.CleanupPathFileSpec(
                                      moProfile.sRelativeToExePathFile(loPathFilesEntry.Value.ToString())
                                    , ldtOlderThan
                                    , leFileDateTimeType
                                    , loCurrentCleanupSet
                                    );
                    }
                }

                if ( lbCleanupFiles )
                    this.DisplayDeletedFileList();
            }
            catch (Exception ex)
            {
                Environment.ExitCode = 1;
                this.ShowError(ex.Message, "Unanticipated Error");

                lbCleanupFiles = false;
            }

            if ( this.bMainLoopStopped )
            {
                this.LogIt("Cleanup process stopped.");
                this.bMainLoopStopped = false;
                lbCleanupFiles = false;
            }
            else
            {
                if ( lbCleanupFiles )
                    this.LogIt("File cleanup finished.");
                else
                    this.LogIt("File cleanup failed.");
            }

            return lbCleanupFiles;
        }

        /// <summary>
        /// Recursively deletes files of the given path\file
        /// specification older than the given age in days.
        /// </summary>
        /// <param name="asPathFiles">
        /// The path\file specification of files to be deleted.
        /// </param>
        /// <param name="adtOlderThan">
        /// Files with timestamps older than this will be deleted.
        /// </param>
        /// <param name="aeFileDateTimeType">
        /// Each file has multiple timestamps. This specifies which one to use.
        /// </param>
        /// <param name="aoProfile">
        /// This profile contains the various cleanup parameters.
        /// </param>
        public bool CleanupPathFileSpec(
                  string asPathFiles
                , DateTime adtOlderThan
                , FileDateTimeTypes aeFileDateTimeType
                , tvProfile aoProfile
                )
        {
            if ( this.bMainLoopStopped )
                return true;

            bool    lbCleanupPathFileSpec = true;
            string  lsPath = Path.GetDirectoryName(asPathFiles);
            string  lsFiles = Path.GetFileName(asPathFiles);
            bool    lbCleanupHidden = aoProfile.bValue("-CleanupHidden", false);
            bool    lbCleanupReadOnly = aoProfile.bValue("-CleanupReadOnly", false);
            bool    lbRecurse = aoProfile.bValue("-Recurse", false);
/*
            bool    lbDisplayFileDeletionErrors = true;
                    // Don't create a default value here. Let the user create the value via a prompt
                    // below. This must be handled this way since the deletion error messages are 
                    // modeless and therefore the "skip this" checkbox will be presented only once.
                    if ( moProfile.ContainsKey("-MsgBoxPromptFileDeletionErrors") )
                        lbDisplayFileDeletionErrors = moProfile.bValue("-MsgBoxPromptFileDeletionErrors", true);
*/
            bool    lbDisplayFileDeletionErrors = moProfile.bValue("-MsgBoxPromptFileDeletionErrors", false);
            string  lsDirectorySeparatorChar = Path.DirectorySeparatorChar.ToString();
            string  lsRecurseFolder = aoProfile.sValue("-RecurseFolder", "");
                    // The recurse folder must be surrounded by path delimiters. Otherwise,
                    // a matching path name substring may be found instead of a subfolder name.
                    if ( !lsRecurseFolder.StartsWith(lsDirectorySeparatorChar) )
                        lsRecurseFolder = lsDirectorySeparatorChar + lsRecurseFolder;
                    if ( !lsRecurseFolder.EndsWith(lsDirectorySeparatorChar) )
                        lsRecurseFolder += lsDirectorySeparatorChar;

            try
            {
                // Only check for file cleanup if either there is no recursion
                // or the base path contains the recursion subfolder. An empty
                // lsRecurseFolder matches everything from the base path up.
                if ( !lbRecurse || (lbRecurse && (lsPath + lsDirectorySeparatorChar).Contains(lsRecurseFolder)) )
                {
                    IOrderedEnumerable<FileSystemInfo> loFileSysInfoList = null;
                            // If the given file path does not exist, do nothing.
                            if ( Directory.Exists(lsPath) )
                                try
                                {
                                    // Get a list of all files for potential deletion
                                    // sorted by file date (oldest files first).
                                    switch (aeFileDateTimeType)
                                    {
                                        case FileDateTimeTypes.CreationTime:
                                            loFileSysInfoList =
                                                    new DirectoryInfo(lsPath).GetFileSystemInfos(lsFiles)
                                                    .OrderBy(a => a.CreationTime);
                                            break;
                                        case FileDateTimeTypes.LastAccessTime:
                                            loFileSysInfoList =
                                                    new DirectoryInfo(lsPath).GetFileSystemInfos(lsFiles)
                                                    .OrderBy(a => a.LastAccessTime);
                                            break;
                                        default:
                                            loFileSysInfoList =
                                                    new DirectoryInfo(lsPath).GetFileSystemInfos(lsFiles)
                                                    .OrderBy(a => a.LastWriteTime);
                                            break;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Environment.ExitCode = 1;
                                    if ( !lbDisplayFileDeletionErrors )
                                        this.LogIt(string.Format("Folder: \"{0}\"\r\n", lsPath) + ex.Message);
                                    else
                                        this.ShowModelessError(
                                                  string.Format("Folder: \"{0}\"\r\n", lsPath) + ex.Message
                                                , "Error Deleting Files"
                                                , "-FileDeletionErrors"
                                                );
                                }

                    if ( null != loFileSysInfoList )
                    {
                        // This boolean prevents wiping out many old files
                        // that are not regularly replaced with newer files.
                        bool    lbApplyDeletionLimit = aoProfile.bValue("-ApplyDeletionLimit", true);
                        int     liFileDeletionLimit = this.iFileDeletionLimit(
                                        loFileSysInfoList.Count(), adtOlderThan);
                        int     liIndex = 0;

                        foreach (FileSystemInfo loFileSysInfo in loFileSysInfoList)
                        {
                            System.Windows.Forms.Application.DoEvents();
                            System.Threading.Thread.Sleep(moProfile.iValue("-CleanupLoopSleepMS", 1));
                            if ( this.bMainLoopStopped )
                                break;

                            // Show UI activity for each loFileSysInfo evaluated.
                            this.IncrementUIProgressBar();

                            // Only evaluate files for deletion (ie. skip folders).
                            if ( !Directory.Exists(loFileSysInfo.FullName) )
                            {
                                // Since files are deleted in file date order,
                                // the oldest files will always be deleted first.
                                // Once the deletion limit is reached, stop deleting.
                                if ( lbApplyDeletionLimit && ++liIndex > liFileDeletionLimit )
                                    break;

                                DateTime ldtFileDate;
                                        switch (aeFileDateTimeType)
                                        {
                                            case FileDateTimeTypes.CreationTime:
                                                ldtFileDate = loFileSysInfo.CreationTime;
                                                break;
                                            case FileDateTimeTypes.LastAccessTime:
                                                ldtFileDate = loFileSysInfo.LastAccessTime;
                                                break;
                                            default:
                                                ldtFileDate = loFileSysInfo.LastWriteTime;
                                                break;
                                        }
                                // Delete the current file only if its file date is older
                                // than the given date. If it's also a hidden file, the
                                // -CleanupHidden switch must be specified (see above).
                                bool lbDoDelete = ldtFileDate < adtOlderThan
                                        && (    lbCleanupHidden
                                            ||  FileAttributes.Hidden
                                            !=  (loFileSysInfo.Attributes & FileAttributes.Hidden));

                                if ( lbDoDelete )
                                {
                                    try 
                                    {	        
                                        // Get the file size.
                                        long llFileSize = new FileInfo(loFileSysInfo.FullName).Length;

                                        // If the -CleanupReadOnly switch is used (see above),
                                        // set the current file's attributes to "Normal".
                                        if (        lbCleanupReadOnly
                                                &&  FileAttributes.ReadOnly
                                                ==  (loFileSysInfo.Attributes & FileAttributes.ReadOnly)
                                                )
                                            loFileSysInfo.Attributes = FileAttributes.Normal;

                                        // Hidden files can be deleted without changing attributes.

                                        // Attempt to delete the file. If its attributes still
                                        // include "readonly", let it blow an error.
                                        loFileSysInfo.Delete();

                                        this.LogDeletedFile(loFileSysInfo.FullName, ldtFileDate, llFileSize);
                                    }
                                    catch (Exception ex)
                                    {
                                        Environment.ExitCode = 1;
                                        if ( !lbDisplayFileDeletionErrors )
                                            this.LogIt(string.Format("File: \"{0}\"\r\n", loFileSysInfo.FullName) + ex.Message);
                                        else
                                            this.ShowModelessError(
                                                      string.Format("File: \"{0}\"\r\n", loFileSysInfo.FullName) + ex.Message
                                                    , "Error Deleting File"
                                                    , "-FileDeletionErrors"
                                                    );
                                    }
                                }
                            }
                        }
                    }
                }

                // Recursion is determined by the -Recurse switch (see above).
                if ( lbRecurse )
                {
                    // Process the sub-folders in the base folder.

                    // Use an empty array instead of null to
                    // prevent the "foreach" from blowing up.
                    string[]    lsSubfoldersArray = new string[0];
                                if ( Directory.Exists(lsPath) )
                                {
                                    try
                                    {
                                        // Get subdirectories only at the next level.
                                        lsSubfoldersArray = Directory.GetDirectories(lsPath);
                                    }
                                    catch (Exception ex)
                                    {
                                        Environment.ExitCode = 1;
                                        if ( !lbDisplayFileDeletionErrors )
                                            this.LogIt(string.Format("Folder: \"{0}\"\r\n", lsPath) + ex.Message);
                                        else
                                            this.ShowModelessError(
                                                      string.Format("Folder: \"{0}\"\r\n", lsPath) + ex.Message
                                                    , "Error Deleting Folders"
                                                    , "-FileDeletionErrors"
                                                    );
                                    }
                                }

                    foreach (string lsSubfolder in lsSubfoldersArray)
                    {
                        System.Windows.Forms.Application.DoEvents();
                        if ( this.bMainLoopStopped )
                            break;

                        // Get the current subfolder's attributes. Using "Hidden" by default prevents
                        // an attempt at deleting the file if its attributes can't be read for whatever
                        // reason (unless the -CleanupHidden switch is used). In the case of unreadable
                        // attributes the file would not likely be deletable anyway.
                        FileAttributes  loFileAttributes = FileAttributes.Hidden;
                                try
                                {
                                    loFileAttributes = File.GetAttributes(lsSubfolder);
                                }
                                catch (Exception ex)
                                {
                                    Environment.ExitCode = 1;
                                    if ( !lbDisplayFileDeletionErrors )
                                        this.LogIt(string.Format("Folder: \"{0}\"\r\n", lsSubfolder) + ex.Message);
                                    else
                                        this.ShowModelessError(
                                                  string.Format("Folder: \"{0}\"\r\n", lsSubfolder) + ex.Message
                                                , "Error Deleting Folder"
                                                , "-FileDeletionErrors"
                                                );
                                }

                        if (        lbCleanupHidden
                                ||  FileAttributes.Hidden
                                !=  (loFileAttributes & FileAttributes.Hidden)
                                )
                        {
                            // Remove all applicable files in the current subfolder.
                            this.CleanupPathFileSpec(
                                      Path.Combine(lsSubfolder, lsFiles)
                                    , adtOlderThan
                                    , aeFileDateTimeType
                                    , aoProfile
                                    );

                            // This is deliberate. Do not use "Path.Combine()" here. We need
                            // the trailing directory separator character (eg. the backslash)
                            // since the recurse folder will always have a trailing separator.
                            string lsSubfolderPlus = lsSubfolder + lsDirectorySeparatorChar;

                            // If -RemoveRecurseFolder is false, remove empty subfolders
                            // in the recurse folder only (ie. do not remove the recurse
                            // folder itself). In other words, lsSubfolderPlus may contain
                            // lsRecurseFolder, but it can't end with it (unless lsRecurseFolder
                            // is just a backslash).
                            if (              lsSubfolderPlus.Contains(lsRecurseFolder)
                                    && (     !lsSubfolderPlus.EndsWith(lsRecurseFolder)
                                        || lsDirectorySeparatorChar == lsRecurseFolder
                                        || aoProfile.bValue("-RemoveRecurseFolder", false))
                                    )
                            {
                                // These are used to judge the subfolder emptiness.
                                string[]    lsPathFilesArray = new string[0];
                                string[]    lsSubfoldersArray2 = new string[0];
                                        if ( Directory.Exists(lsSubfolder) )
                                            try
                                            {
                                                lsPathFilesArray = Directory.GetFiles(lsSubfolder);
                                                lsSubfoldersArray2 = Directory.GetDirectories(lsSubfolder);
                                            }
                                            catch (Exception ex)
                                            {
                                                Environment.ExitCode = 1;
                                                if ( !lbDisplayFileDeletionErrors )
                                                    this.LogIt(string.Format("Folder: \"{0}\"\r\n", lsSubfolder) + ex.Message);
                                                else
                                                    this.ShowModelessError(
                                                              string.Format("Folder: \"{0}\"\r\n", lsSubfolder) + ex.Message
                                                            , "Error Deleting Files"
                                                            , "-FileDeletionErrors"
                                                            );
                                            }

                                // Remove the folder only if it's empty.
                                if ( 0 == lsPathFilesArray.Length && 0 == lsSubfoldersArray2.Length )
                                {
                                    DirectoryInfo   loDirInfo = new DirectoryInfo(lsSubfolder);
                                    DateTime        ldtFileDate;
                                            switch (aeFileDateTimeType)
                                            {
                                                case FileDateTimeTypes.CreationTime:
                                                    ldtFileDate = loDirInfo.CreationTime;
                                                    break;
                                                case FileDateTimeTypes.LastAccessTime:
                                                    ldtFileDate = loDirInfo.LastAccessTime;
                                                    break;
                                                default:
                                                    ldtFileDate = loDirInfo.LastWriteTime;
                                                    break;
                                            }

                                    try 
                                    {	        
                                        // If the -CleanupReadOnly switch is used,
                                        // set the subfolder to "Normal" attributes.
                                        if (        lbCleanupReadOnly
                                                &&  FileAttributes.ReadOnly
                                                    == (loFileAttributes & FileAttributes.ReadOnly)
                                                )
                                            File.SetAttributes(lsSubfolder, FileAttributes.Normal);

                                        // Hidden folders can be deleted without changing attributes.

                                        // Attempt to delete the subfolder. If its attributes still
                                        // include "readonly", let it blow an error.
                                        Directory.Delete(lsSubfolder);

                                        // Using "0" as the file size also indicates a folder deletion.
                                        this.LogDeletedFile(lsSubfolder + " (dir)", ldtFileDate, 0);
                                    }
                                    catch (Exception ex)
                                    {
                                        Environment.ExitCode = 1;
                                        if ( !lbDisplayFileDeletionErrors )
                                            this.LogIt(string.Format("Folder: \"{0}\"\r\n", lsSubfolder) + ex.Message);
                                        else
                                            this.ShowModelessError(
                                                      string.Format("Folder: \"{0}\"\r\n", lsSubfolder) + ex.Message
                                                    , "Error Deleting Folder"
                                                    , "-FileDeletionErrors"
                                                    );
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Environment.ExitCode = 1;
                this.ShowError(ex.Message, "Unanticipated Error");

                lbCleanupPathFileSpec = false;
            }

            return lbCleanupPathFileSpec;
        }

        private void moBackgroundThread_Start()
        {
            if ( moProfile.ContainsKey("-AddTasks") )
            {
                // First, run any startup related tasks.
                this.DoAddTasks(true);

                // Then periodically check for scheduled tasks to run.
                moBackgroundLoopTimer = new Timer(moBackgroundLoopTimer_Callback, null, 0
                        , moProfile.iValue("-BackgroundLoopSleepMS", 200));
            }
        }

        private void moBackgroundLoopTimer_Callback(Object state)
        {
            // This is the "added tasks" subsystem. Arbitrary command
            // lines are run this way (independent of the main loop).
            this.DoAddTasks();
        }

        void DoGoPcBackup_SessionEnding(object sender, SessionEndingCancelEventArgs e)
        {
            this.LogIt("");
            this.LogIt(String.Format("{0} exiting due to log-off or system shutdown.", Path.GetFileName(Application.ResourceAssembly.Location)));

            if ( null != this.oUI )
            this.oUI.HandleShutdown();
        }
    }
}
