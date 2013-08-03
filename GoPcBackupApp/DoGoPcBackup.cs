using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading;
using System.Windows;
using tvToolbox;


namespace GoPcBackup
{
    /// <summary>
    /// GoPcBackup.exe
    ///
    /// Run this program. It will prompt you to create a default profile file:
    ///
    /// GoPcBackup.exe.txt
    ///
    /// The profile will contain help (see -Help) as well as default options.
    ///
    /// Note: This software creates its own support files (including DLLs or
    ///       other EXEs) in the folder that contains it. It will prompt you
    ///       to create its own folder when you run it the first time.
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
    /// Cleanup old files and backup new ones.
    /// </summary>
    public class DoGoPcBackup : Application
    {
        private tvProfile       moCurrentBackupSet;
        private string          msCurrentBackupOutputPathFile;
        private const string    mcsZipToolExeFilename = "7z.exe";
        private const string    mcsZipToolDllFilename = "7z.dll";
        private const string    msBackupDoneScriptPathFileDefault = "Run Last BackupDone.cmd";
        private bool            mbHasNoDeletionGroups;
        private int             miBackupSets;
        private int             miBackupSetsRun;
        private int             miBackupSetsGood;


        private DoGoPcBackup()
        {
            tvMessageBox.ShowError(null, "Don't use this constructor!");
        }

        /// <summary>
        /// Expects a profile object to be provided.
        /// </summary>
        /// <param name="aoProfile">
        /// The given profile object must either contain runtime options
        /// or it will be returned filled with default runtime options.
        /// </param>
        public DoGoPcBackup(tvProfile aoProfile)
        {
            moProfile = aoProfile;
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            DoGoPcBackup loMain  = null;

            try
            {
                bool    lbFirstInstance;
                        Mutex loMutex = new Mutex(false, "Local\\" + Application.ResourceAssembly.GetName().Name, out lbFirstInstance);
                        if ( !lbFirstInstance )
                            return;

                tvProfile loProfile = new tvProfile(args);
                if ( !loProfile.bExit )
                {
                    loProfile.GetAdd("-Help",
                            @"
Introduction


This utility performs file backups and file cleanups in the background.

It also acts as its own scheduler. First it checks for files to be removed on
a given schedule. Then it runs a backup of your files automatically.

There is no need to use a job scheduler unless this software is running on a 
server computer that has no regular user activity (see -RunOnce below).

You provide various file specifications (ie. path\file locations of the files
to backup and to cleanup) as well as file age limits for the files to cleanup.
The rest is automatic.

This utility will run in the background unless its timer is turned off. Its
simple user interface (UI) is usually minimized to the system tray.


Command-Line Usage


Open this utility's profile file to see additional options available. It is
usually located in the same folder as ""{EXE}"" and it typically has
the same name with "".txt"" added (see ""{INI}"").

Profile file options can be overridden with command-line arguments. The
keys for any ""-key=value"" pairs passed on the command-line must match
those that appear in the profile (with the exception of the ""-ini"" key).

For example, the following invokes the use of an alternative profile file:

{EXE} -ini=NewProfile.txt

This tells the software to run in automatic mode:

{EXE} -AutoStart

   or

{EXE} -Auto*


Author:  George Schiro (GeoCode@Schiro.name)
Date:    10/21/2011


Options and Features


The main options for this utility are listed below with their default values.
A brief description of each feature follows.

-ArchivePath=C:\Archive

    This is the destination folder of the backup output files unless
    overridden in -BackupSet (see below).

-AutoStart=True

    This tells the software to run in automatic mode. Set this switch False
    and the main loop in the UI will only start manually. The software will
    also vacate memory after the UI window closes. This is the timer switch.

-BackupDoneScriptHelp= SEE PROFILE FOR DEFAULT VALUE

    This is the default content of the DOS script that is initially written to
    -BackupDoneScriptPathFile and run after each successful backup. It contains
    a description of the command-line arguments passed to the script at runtime.

-BackupDoneScriptInit=False

    Set this switch True and the ""backup done"" script will be automatically
    overwritten from the content of -BackupDoneScriptHelp. Once used this switch
    will be reset to False.

    Note: the content of -BackupDoneScriptHelp will also be overwritten from the
    default value embedded in the executable file.

-BackupDoneScriptPathFile=Run Last BackupDone.cmd

    This DOS shell script is run after each successful backup completes. You 
    can edit the contents of the file or point this parameter to another file.
    If you delete the file, it will be recreated from the content found in 
    -BackupDoneScriptHelp (see above).

-BackupDriveToken=(This is my GoPC backup drive.)

    This is the filename looked for in the root of every storage device attached
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

        -OutputFilename=""The backup output filename goes here.""

            This is the backup output filename with no path and no extension.
            This parameter will be combined with -BackupOutputExtension, 
            -ArchivePath and -BackupOutputFilenameDateFormat to produce a
            full backup output path\file specification.

-BackupTimeMinsPerTick=15

    This determines how many minutes the backup time changes with each tick
    of the backup time selection slider.

-CleanupFiles=True

    Set this switch False to disable cleanups (ie. do file backups only).

-CleanupLoopSleepMS=1

    This is the number of milliseconds of process thread sleep time between
    file deletions. The default of 1 ms should result in rapid deletions. You 
    can increase this value if you are concerned that the UI is not responsive
    enough or the process is using too much CPU while deleting.

-CleanupSet=""One of many file sets to cleanup goes here.""

    Each file cleanup set has its own profile:

        -AgeDays=365000

            This is the maximum file age in days. It is 1000 years by default.
            Only files older than this will be considered for deletion.

        -ApplyDeletionLimit=False

            Set this switch True and the cleanup process will limit deletions
            only to files that are regularly replaced by newer files. This way
            a large collection of very old files won't be wiped out in one run.
            Old files will only be removed if newer files exist to replace them.

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


        Here's a single line example:

        -CleanupSet= -AgeDays=90 -FilesToDelete=C:\WINDOWS\TEMP\*.*


        Here's a multi-line example:

        -CleanupSet=[

            -AgeDays=60
            -FilesToDelete=C:\WINDOWS\TEMP\*.*
            -FilesToDelete=C:\WINDOWS\system32\*.log
            -FilesToDelete=C:\WINDOWS\system32\LogFiles\W3SVC1\*.log
            -FilesToDelete=C:\Documents and Settings\Administrator\Local Settings\Temp\*.*
            -FilesToDelete=C:\Program Files\GoPcBackup\GoPcBackupList*.txt

        -CleanupSet=]
        -CleanupSet=[

            -AgeDays=14
            -FilesToDelete=C:\Documents and Settings\*.*
            -CleanupHidden
            -CleanupReadOnly
            -Recurse
            -RecurseFolder=Local Settings\Temp

        -CleanupSet=]
        -CleanupSet=[

            -AgeDays=90
            -FilesToDelete=C:\Archive\*.*
            -ApplyDeletionLimit

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

-DeletedFileListOutputPathFile=DeletedFileList.txt

    This is the output path\file that will contain the list of deleted files.
    The profile file name will be prepended to the default and the current date
    (see -DeletedFileListDateFormat) will be inserted between the filename and 
    the extension.

-FetchSource=False

    Set this switch True to fetch the source code for this utility from the EXE.
    Look in the containing folder for a ZIP file with the full project sources.

-Help= SEE PROFILE FOR DEFAULT VALUE

    This help content.

-LogEntryDateTimeFormatPrefix""yyyy-MM-dd hh:mm:ss:fff tt  ""

    This format string is used to prepend a timestamp prefix to each log entry in
    the process log file (see -LogPathFile below).    

-LogFileDateFormat=-yyyy-MM-dd

    This format string is used to form the variable part of each backup / cleanup
    log file output filename (see -LogPathFile below). It is inserted between the
    filename and the extension.

-LogPathFile=Log.txt

    This is the output path\file that will contain the backup / cleanup process
    log. The profile file name will be prepended to the default and the current
    date (see -LogFileDateFormat) will be inserted between the filename and the
    extension (see -LogFileDateFormat above).

-MainLoopMinutes=1440

    This is the number of minutes until the next run. One day is the default.

-MainLoopSleepMS=200

    This is the number of milliseconds of process thread sleep wait time between
    loops. The default of 200 ms should be a happy medium between a responsive
    overall UI and a responsive process timer UI. You can increase this value
    if you are concerned that the timer UI is using too much CPU while waiting.

-PreviousBackupOk= NO DEFAULT VALUE

    This is the True or False ""Ok"" status of the previous backup / cleanup run.

-PreviousBackupTime= NO DEFAULT VALUE

    This is the timestamp of the previous backup / cleanup run.

-RunOnce=False

    Set this switch True to run this utility in one loop only (with no UI) and 
    then shutdown automatically thereafter. This switch is useful if the utility
    is run in a batch process or if it is run by a job scheduler.

-ShowBackupDoneScriptErrors=True

    Set this switch False to suppress the pop-up display of ""backup done"" script
    errors (see -BackupDoneScriptPathFile above).

-ShowDeletedFileList=False

    Set this switch True and the list of deleted files will be displayed (using
    the application associated with -DeletedFileListOutputPathFile's filename
    extension, see above).

-UseVirtualMachineHostArchive=False

    Set this switch True and code will be added to the ""backup done"" script
    (see -BackupDoneScriptPathFile above) that copies backups to your virtual
    machine host computer (assuming you have one).

-VirtualMachineHostArchivePath=\\Mainhost\Archive

    This value is used within the ""backup done"" script to copy backups to the
    virtual machine host (assuming you have one, see -BackupDoneScriptPathFile).
    If you haven't already customized the done script, delete it. Then change this
    value. A new script will be made with your new -VirtualMachineHostArchivePath.

-ZipToolEXE=7z.exe

    This is the ZIP tool executable that performs the backup compression.

-ZipToolEXEargs=a -r -spf -ssw ""{{BackupOutputPathFile}}"" @""{{BackupPathFiles}}"" -w""{{BackupOutputPath}}""

    These are the command-line arguments passed to the ZIP compression tool
    (see -ZipToolEXE above). The tokens (in curly brackets) are self-evident
    and they are replaced at runtime.

-ZipToolLastRun= NO DEFAULT VALUE

    This shows the last executed ZIP command-line (see -ZipToolEXE and 
    -ZipToolEXEargs above).

-ZipToolLastRunCmdPathFile=Run Last Backup.cmd

    This is a script file (text) that contains a copy of the last ZIP command 
    line executed (see -ZipToolLastRun above).


Note:   There may be various other settings that can be adjusted also (user
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

                    // Fetch simple setup.
                    tvFetchResource.ToDisk(Application.ResourceAssembly.GetName().Name
                            , "Setup in Program Files.exe", null);

                    // Fetch source code.
                    if ( loProfile.bValue("-FetchSource", false) )
                    {
                        tvFetchResource.ToDisk(Application.ResourceAssembly.GetName().Name
                                , Application.ResourceAssembly.GetName().Name + ".zip", null);
                    }


                    // Updates start here.
                    if ( loProfile.bFileJustCreated )
                    {
                        //loProfile["-UpdatedYYYY-MM-DD"] = true;
                        //loProfile.Save();
                    }
                    else
                    {
                    //    if ( !loProfile.bValue("-UpdatedYYYY-MM-DD", false) )
                    //    {
                    //        if ( tvMessageBoxResults.Cancel ==  this.Show(null,
                    //                  "This software has been updated."
                    //                + " It requires a change to your -???."
                    //                + "Shall we remove the old -??? now?"
                    //                , Application.ResourceAssembly.GetName().Name
                    //                , tvMessageBoxButtons.OKCancel, tvMessageBoxIcons.Question) )
                    //        {
                    //            loProfile.bExit = true;
                    //        }
                    //        else
                    //        {
                    //            loProfile.Remove("-???");
                    //
                    //            loProfile["-UpdatedYYYY-MM-DD"] = true;
                    //            loProfile.Save();
                    //        }
                    //    }
                    }
                    // Updates end here.


                    if ( !loProfile.bExit )
                    {
                        if ( !loProfile.bValue("-RunOnce", false) )
                        {
                            try
                            {
                                loMain = new DoGoPcBackup(loProfile);

                                // Load the UI (only when -RunOnce is off).
                                UI  loUI = new UI(loProfile, loMain);
                                    loMain.oUI = loUI;

                                loMain.Run(loUI);

                                GC.KeepAlive(loMutex);
                            }
                            catch (ObjectDisposedException) {}
                        }
                        else
                        {
                            // Run in batch mode.

                            DoGoPcBackup loDoDa = new DoGoPcBackup(loProfile);

                            GC.KeepAlive(loMutex);

                            loDoDa.CleanupFiles();
                            loDoDa.BackupFiles();
                        }
                    }
                }
            }
            catch (SecurityException)
            {
                tvFetchResource.NetworkSecurityStartupErrorMessage();
            }
            catch (Exception ex)
            {
                tvFetchResource.ErrorMessage(null, ex.Message);
            }
            finally
            {
                if ( null != loMain && null != loMain.oUI )
                    loMain.oUI.Close();
            }
        }

        /// <summary>
        /// This is main application profile object.
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
        /// This switch is used within the main loop of the UI (see "this.oUI")
        /// to stop any loops running within this object as well.
        /// </summary>
        public bool bMainLoopStopped
        {
            get;set;
        }

        /// <summary>
        /// Returns the current "LogPathFile" name.
        /// </summary>
        public string sLogPathFile
        {
            get
            {
                return this.sUniqueOutputPathFile(
                          this.sLogPathFileBase
                        , moProfile.sValue("-LogFileDateFormat", "-yyyy-MM-dd")
                        , true
                        );
            }
        }

        /// <summary>
        /// Returns the current "LogPathFile" base name.
        /// </summary>
        public string sLogPathFileBase
        {
            get
            {
                return moProfile.sValue("-LogPathFile"
                        , Path.GetFileNameWithoutExtension(moProfile.sLoadedPathFile)
                        + "Log.txt");
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
                return this.sUniqueOutputPathFile(
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
                return moProfile.sValue("-DeletedFileListOutputPathFile"
                        , Path.GetFileNameWithoutExtension(moProfile.sLoadedPathFile)
                        + "DeletedFileList.txt");
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
            return this.sUniqueOutputPathFile(
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
                        , aoBackupSetProfile.sValue("-OutputFilename", "(not set)"))
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
                this.ShowError(ex.Message, "Cleanup Failed");
            }

            return liCleanupFilesCount;
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
                        // Count all files represented by the current file
                        // specifcation and add that number to the total.
                        liBackupFilesCount += Directory.GetFiles(
                                  loFolderToBackup.Value.ToString()
                                , this.sBackupFileSpec(loBackupSetProfile)
                                , SearchOption.AllDirectories).Length;
                    }
                }
            }
            catch (Exception ex)
            {
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


        private string sUniqueOutputPathFile(
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
                lbDone = abAppendOutput | !File.Exists(lsOutputPathFile);

                // If the given pathfile already exists, create a variation on the dated
                // filename by appending an integer (see liUniqueFilenameSuffix above).
                // Keep trying until a unique dated pathfile is identified.
                if ( !lbDone )
                    lsOutputFilename = lsBaseFilename + "." + (++liUniqueFilenameSuffix).ToString() + lsBaseFileExt;
            }
            while ( !lbDone );

            return lsOutputPathFile;
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
                )
        {
            this.LogIt(asMessageText);

            tvMessageBoxResults ltvMessageBoxResults = tvMessageBoxResults.None;

            if ( null != this.oUI )
            this.oUI.Dispatcher.BeginInvoke((Action)(() =>
            {
                ltvMessageBoxResults = tvMessageBox.Show(
                          this.oUI
                        , asMessageText
                        , asMessageCaption
                        , aetvMessageBoxButtons
                        , aetvMessageBoxIcon
                        );
            }
            ));

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

            if ( null != this.oUI )
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

            return ltvMessageBoxResults;
        }

        private void ShowError(string asMessageText)
        {
            this.LogIt(asMessageText);

            if ( null != this.oUI )
            this.oUI.Dispatcher.BeginInvoke((Action)(() =>
            {
                tvMessageBox.ShowError(this.oUI, asMessageText);
            }
            ));
        }

        private void ShowError(string asMessageText, string asMessageCaption)
        {
            this.LogIt(asMessageText);

            if ( null != this.oUI )
            this.oUI.Dispatcher.BeginInvoke((Action)(() =>
            {
                tvMessageBox.ShowError(this.oUI, asMessageText, asMessageCaption);
            }
            ));
        }

        private void ShowModelessError(string asMessageText, string asMessageCaption, string asProfilePromptKey)
        {
            this.LogIt(asMessageText);

            if ( null != this.oUI )
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

        // This kludge is necessary since "Process.Kill()" doesn't work reliably.
        private bool bKillProcess(Process aoProcess)
        {
            bool lbKillProcess = false;

            string lsProcessName = null;

            // First try killing the process the usual way.

            try
            {
                lsProcessName = aoProcess.ProcessName;
                aoProcess.Kill();
                aoProcess.WaitForExit(moProfile.iValue("-KillProcessWaitMS", 1000));
            }
            catch {}

            // If the process name could be read (see above)
            // and the process is still out there, keep trying.
            // This approach will be a problem if there are multiple
            // processes with the same name. This will likely only
            // happen with DOS shell consoles (similar to the "backup
            // done" script). We can address this later as needed.
            if ( null != lsProcessName && 0 != Process.GetProcessesByName(lsProcessName).Length )
                for (int i=0; i < moProfile.iValue("-KillProcessRetries", 10); i++)
                    foreach (Process loProcess in Process.GetProcessesByName(lsProcessName))
                    {
                        System.Windows.Forms.Application.DoEvents();
                        System.Threading.Thread.Sleep(moProfile.iValue("-MainLoopSleepMS", 200));

                        try
                        {
                            loProcess.Kill();
                            loProcess.WaitForExit(moProfile.iValue("-KillProcessWaitMS", 1000));
                        }
                        catch
                        {
                            // Ignore all exceptions since we are retrying
                            // with a final test for process existence anyway.
                        }
                    }

            if ( 0 == Process.GetProcessesByName(lsProcessName).Length )
                lbKillProcess = true;

            return lbKillProcess;
        }

        private void IncrementUIProgressBar()
        {
            if ( null != this.oUI )
            this.oUI.Dispatcher.BeginInvoke((Action)(() =>
            {
                this.oUI.IncrementProgressBar();
            }
            ));
        }

        /// <summary>
        /// Displays the given file (ie. asPathFile) using whatever
        /// application is associated with its filename extension.
        /// </summary>
        private void DisplayFile(string asPathFile)
        {
            string lsFilename = Path.GetFileName(asPathFile);

            try
            {
                // First, close any previously displayed file app windows.
                foreach (Process loProcess in Process.GetProcesses())
                {
                   if( loProcess.MainWindowTitle.Contains(lsFilename) )
                   {
                        loProcess.CloseMainWindow();
                        break;
                   }
                }

                Process.Start(asPathFile);
            }
            catch (Exception ex)
            {
                this.ShowError(ex.Message);
            }
        }

        /// <summary>
        /// Displays the file cleanup output file.
        /// </summary>
        private bool DisplayDeletedFileList()
        {
            bool lbDisplayOutputFile = true;

            if ( moProfile.bValue("-ShowDeletedFileList", false) )
            {
                string lsOutputPathFile = this.sDeletedFileListOutputPathFile;

                if ( this.mbHasNoDeletionGroups )
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

                    lbDisplayOutputFile = false;
                }
                else
                {
                    string lsOutputFilename = Path.GetFileName(lsOutputPathFile);

                    try
                    {
                        // First, close any previously displayed output file windows.
                        foreach (Process loProcess in Process.GetProcesses())
                        {
                           if( loProcess.MainWindowTitle.Contains(lsOutputFilename) )
                           {
                                loProcess.CloseMainWindow();
                                break;
                           }
                        }

                        Process.Start(lsOutputPathFile);
                    }
                    catch (Exception ex)
                    {
                        this.ShowError(ex.Message);

                        lbDisplayOutputFile = false;
                    }
                }
            }

            return lbDisplayOutputFile;
        }


        /// <summary>
        /// Write the given asMessageText to a text file as well as
        /// to the output console of the UI window (if it exists).
        /// </summary>
        /// <param name="asMessageText">The text message string to log.</param>
        public void LogIt(string asMessageText)
        {
            StreamWriter loStreamWriter = null;

            try
            {
                loStreamWriter = new StreamWriter(moProfile.sRelativeToProfilePathFile(this.sLogPathFile), true);
                loStreamWriter.WriteLine(DateTime.Now.ToString(moProfile.sValueNoTrim(
                        "-LogEntryDateTimeFormatPrefix", "yyyy-MM-dd hh:mm:ss:fff tt  "))
                        + asMessageText);
            }
            catch { /* Can't log a log failure. */ }
            finally
            {
                if ( null != loStreamWriter )
                    loStreamWriter.Close();
            }

            if ( null != this.oUI )
            this.oUI.Dispatcher.BeginInvoke((Action)(() =>
            {
                this.oUI.AppendOutputTextLine(asMessageText);
            }
            ));
        }


        /// <summary>
        /// Backup files of the given file specifications and run the
        /// "backup done" script to copy the backup around as needed.
        /// </summary>
        public bool BackupFiles()
        {
            // Return if backup is disabled.
            if ( !moProfile.bValue("-BackupFiles", true) )
                return true;
            // Return if cleanup is enabled and it was stopped.
            if (  moProfile.bValue("-CleanupFiles", true) && this.bMainLoopStopped )
                return true;
            else
                this.bMainLoopStopped = false;

            bool    lbBackupFiles = false;
            int     liBackupDoneScriptFileCopyFailures = 0;

            // Get the embedded zip compression tool from the EXE.
            tvFetchResource.ToDisk(Application.ResourceAssembly.GetName().Name, mcsZipToolExeFilename, null);
            tvFetchResource.ToDisk(Application.ResourceAssembly.GetName().Name, mcsZipToolDllFilename, null);

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

                string  lsZipToolFileListPathFile = moProfile.sValue("-ZipToolFileListPathFile", "ZipFileList.txt");
                string  lsBackupPathFiles1 = null;  // The first file specification in the set (for logging).
                int     liFileCount = 0;            // The number of file specifications in the current set.

                foreach (DictionaryEntry loEntry in loBackupSetsProfile)
                {
                    System.Windows.Forms.Application.DoEvents();
                    if ( this.bMainLoopStopped )
                        break;

                    // Increment the backup set counter.
                    miBackupSetsRun++;

                    // Convert the current backup set from a command-line string to a profile oject.
                    moCurrentBackupSet = new tvProfile(loEntry.Value.ToString());

                    // Get the list of folders to backup within the current backup set.
                    tvProfile       loFolderToBackupProfile = moCurrentBackupSet.oOneKeyProfile(
                                            "-FolderToBackup");
                    StreamWriter    loStreamWriter = null;

                    liFileCount = 0;

                    try
                    {
                        // Create the ZIP file list file in the same folder as the main profile.
                        loStreamWriter = new StreamWriter(moProfile.sRelativeToProfilePathFile(
                                lsZipToolFileListPathFile), false);

                        // Write the list of files to compress.
                        foreach (DictionaryEntry loFolderToBackup in loFolderToBackupProfile)
                        {
                            System.Windows.Forms.Application.DoEvents();
                            if ( this.bMainLoopStopped )
                                break;

                            string lsBackupFolder = loFolderToBackup.Value.ToString().Trim();
                            string lsBackupPathFiles = Path.Combine(lsBackupFolder, this.sBackupFileSpec());

                            loStreamWriter.WriteLine(lsBackupPathFiles);

                            if ( 1 == ++liFileCount )
                                lsBackupPathFiles1 = lsBackupPathFiles;
                        }
                    }
                    catch (Exception ex)
                    {
                        this.ShowError(string.Format("File Write Failure: \"{0}\"\r\n"
                                , lsZipToolFileListPathFile) + ex.Message
                                , "Failed Writing File"
                                );
                    }
                    finally
                    {
                        if ( null != loStreamWriter )
                            loStreamWriter.Close();
                    }

                    // The backup output path file will be dated and unique.
                    msCurrentBackupOutputPathFile = this.sBackupOutputPathFile();

                    string  lsProcessPathFile = moProfile.sRelativeToProfilePathFile(moProfile.sValue("-ZipToolEXE", mcsZipToolExeFilename));
                    string  lsProcessArgs = moProfile.sValue("-ZipToolEXEargs"
                                    , "a -r -spf -ssw \"{BackupOutputPathFile}\" @\"{BackupPathFiles}\" -w\"{BackupOutputPath}\" ");
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
                            loProcess.StartInfo.UseShellExecute = false;
                            loProcess.StartInfo.RedirectStandardError = true;
                            loProcess.StartInfo.RedirectStandardInput = true;
                            loProcess.StartInfo.RedirectStandardOutput = true;
                            loProcess.StartInfo.CreateNoWindow = true;

                    string  lsLastRunCmd = Path.GetFileName(lsProcessPathFile) + " " + lsProcessArgs;
                    string  lsFileAsStream = string.Format(@"
@prompt $
{0}
@echo.
@pause
"                                           , lsLastRunCmd);

                    moProfile["-ZipToolLastRun"] = lsLastRunCmd;
                    moProfile.Remove("-PreviousBackupOk");
                    moProfile.Save();

                    // This lets the user see what was run or rerun it.
                    string lsLastRunFile = moProfile.sValue("-ZipToolLastRunCmdPathFile", "Run Last Backup.cmd");

                    try
                    {
                        loStreamWriter = new StreamWriter(moProfile.sRelativeToProfilePathFile(lsLastRunFile), false);
                        loStreamWriter.Write(lsFileAsStream);
                    }
                    catch (Exception ex)
                    {
                        this.ShowError(string.Format("File Write Failure: \"{0}\"\r\n"
                                , lsLastRunFile) + ex.Message
                                , "Failed Writing File"
                                );
                    }
                    finally
                    {
                        if ( null != loStreamWriter )
                            loStreamWriter.Close();
                    }

                    string  lsFilesSuffix = liFileCount <= 1 ? ""
                            : string.Format(" + {0} other file specification" + (1 == liFileCount ? "" : "s")
                                    , liFileCount - 1);
                                
                    this.LogIt("");
                    this.LogIt("Backup started ...");
                    this.LogIt("");
                    this.LogIt(string.Format("Backup: \"{0}\"{1}", lsBackupPathFiles1, lsFilesSuffix));
                    this.LogIt("");
                    this.LogIt("Backup command: " + lsLastRunCmd);

                    System.Windows.Forms.Application.DoEvents();

                    loProcess.Start();
                    loProcess.BeginErrorReadLine();
                    loProcess.BeginOutputReadLine();

                    // Wait for the backup process to finish.
                    while ( !this.bMainLoopStopped && !loProcess.HasExited )
                    {
                        System.Windows.Forms.Application.DoEvents();
                        System.Threading.Thread.Sleep(moProfile.iValue("-MainLoopSleepMS", 200));
                    }

                    // Shutdown output to console.
                    loProcess.CancelErrorRead();
                    loProcess.CancelOutputRead();

                    // If a stop request came through, kill the backup process.
                    if ( this.bMainLoopStopped && !this.bKillProcess(loProcess) )
                        this.ShowError("The backup process could not be stopped."
                                , "Backup Failed");

                    // The backup process finished uninterrupted and without error.
                    if ( !this.bMainLoopStopped && 0 == loProcess.ExitCode )
                    {
                        this.LogIt(string.Format("The backup to \"{0}\" was successful."
                                , Path.GetFileName(msCurrentBackupOutputPathFile)));

                        // Run the "backup done" script and return the failed file count.
                        int liCopyFailures = this.iBackupDoneScriptCopyFailures();

                        // Add failed files from the current backup set to the total.
                        liBackupDoneScriptFileCopyFailures += liCopyFailures;

                        // Increment how many backup sets have succeeded so far.
                        if ( 0 ==liCopyFailures )
                            miBackupSetsGood++;
                    }
                }
            }
            catch (Exception ex)
            {
                this.ShowError(ex.Message, "Backup Failed");
                lbBackupFiles = false;
            }

            if ( lbBackupFiles && !this.bMainLoopStopped )
            {
                this.LogIt("");

                // Don't bother with the system tray message 
                // if the background timer is not running.
                string  lsSysTrayMsg = null;
                        if ( moProfile.bValue("-AutoStart", true) )
                            lsSysTrayMsg = this.sSysTrayMsg;

                if ( miBackupSetsRun != miBackupSetsGood
                        || 0 != liBackupDoneScriptFileCopyFailures )
                {
                    moProfile["-PreviousBackupOk"] = false;

                    this.ShowError("The backup failed. Check the log for errors." + lsSysTrayMsg
                            , "Backup Failed");
                }
                else
                {
                    moProfile["-PreviousBackupOk"] = true;

                    this.ShowModeless("Backup finished successfully." + lsSysTrayMsg
                            , "Backup Finished"
                            , tvMessageBoxButtons.OK
                            , tvMessageBoxIcons.Done
                            , "-CurrentBackupFinished"
                            );
                }

                moProfile["-PreviousBackupTime"] = DateTime.Now;
                moProfile.Save();
            }

            if ( this.bMainLoopStopped )
            {
                this.LogIt("Backup process stopped.");
                this.bMainLoopStopped = false;
                lbBackupFiles = false;
            }

            // Turn on the lock that was turned off when we started.
            moProfile.bEnableFileLock = true;

            return lbBackupFiles;
        }

        private int iBackupDoneScriptCopyFailures()
        {
            int liBackupDoneScriptCopyFailures = 0;

            string lsBackupDoneScriptPathFile = moProfile.sRelativeToProfilePathFile(
                    moProfile.sValue("-BackupDoneScriptPathFile", msBackupDoneScriptPathFileDefault));
            string lsBackupDoneScriptOutputPathFile = lsBackupDoneScriptPathFile + ".txt";

            // Before the "backup done" script can be initialized,
            // -BackupDoneScriptHelp must be initialized first.
            if ( moProfile.bValue("-BackupDoneScriptInit", false) )
                moProfile.Remove("-BackupDoneScriptHelp");

            // If the "backup done" script has not been redefined to point elsewhere,
            // prepare to create it from the current -BackupDoneScriptHelp content.
            // We do this even if the script file actually exists already. This way
            // the following default script will be written to the profile file if
            // its not already there.
            if ( lsBackupDoneScriptPathFile == moProfile.sRelativeToProfilePathFile(msBackupDoneScriptPathFileDefault) )
            {
                bool    lbUseMainhostArchive = moProfile.bValue("-UseVirtualMachineHostArchive", false);
                string  lsMainhostArchivePath = moProfile.sValue("-VirtualMachineHostArchivePath", "\\\\Mainhost\\Archive");
                string  lsBackupDoneScript = (moProfile.sValue("-BackupDoneScriptHelp", @"
@echo off
if ""%1""=="""" goto :EOF
::
:: *** Backup finished successfully script goes here. ***
::
:: This script is executed after each successful backup completes. If you 
:: prompt for input within this DOS script (eg. ""pause""), the script
:: will stay in memory. This is not recommended since that behavior would
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
::      This the backup filename only (ie. no path). It includes the
::      embedded date as well as the filename extension.
::
:: %3 = ""BackupBaseOutputFilename""
::
::      This is the backup filename with no path and no date. It's just
::      the base output filename name with the filename extension.
::
:: %4 = ""ArchivePath""
::
::      This is the archive folder.
::
:: %5 = ""AppName""
::
::      This is the backup software filename with no extension.
::
:: %6 = ""BackupExePathFile""
::
::      This is the full path\file specification of the backup software.
::
:: %7 = ""BackupProfilePathFile""
::
::      This is the full path\file specification of the backup software's
::      profile file (ie. its configuration file).
::
:: %8 = ""BackupProfileFilename""
::
::      This is the filename only of the profile file (with extension).
::
:: %9 = ""LogPathFile""
::
::      This is the full path\file specification of the backup log file.
::
::
:: Note: All arguments will be passed with double quotation marks included.
::       So don't use quotes here unless you want ""double double"" quotes.
::
::
:: The following example copies the backup file to the root of drive C:
:: (if its ""AdministratorFiles.zip""). Then it outputs a directory listing
:: of the archive folder.
::
:: Example:
::
:: if not %3.==""AdministratorFiles.zip"". goto :EOF
::
::     echo copy %1 C:\  ] ""{BackupDoneScriptOutputPathFile}""
::          copy %1 C:\ ]] ""{BackupDoneScriptOutputPathFile}""
::
:: dir %4               ]] ""{BackupDoneScriptOutputPathFile}""
::
::                      ^^  Replace brackets with darts.

:: The ""CopyFailures"" environment variable is used to keep count of errors to be returned.
set CopyFailures=0

:: Initialize the ""backup done"" script log file. It's for this run only.
echo.                    > ""{BackupDoneScriptOutputPathFile}""

echo.                                                           >> ""{BackupDoneScriptOutputPathFile}""
echo This backs up the backup software:                         >> ""{BackupDoneScriptOutputPathFile}""

echo.                   >> ""{BackupDoneScriptOutputPathFile}""
echo xcopy /y %6 %4\%5\ >> ""{BackupDoneScriptOutputPathFile}""
     xcopy /y %6 %4\%5\ >> ""{BackupDoneScriptOutputPathFile}""

     if ERRORLEVEL 1 set /A CopyFailures += 1

echo.                                                           >> ""{BackupDoneScriptOutputPathFile}""
echo This copies the backup software's current profile file     >> ""{BackupDoneScriptOutputPathFile}""
echo to a subfolder with the backup output file base name:      >> ""{BackupDoneScriptOutputPathFile}""

echo.                                          >> ""{BackupDoneScriptOutputPathFile}""
echo echo F : xcopy  /y  %7 %4\%5\%3\%8.%2.txt >> ""{BackupDoneScriptOutputPathFile}""
     echo F | xcopy  /y  %7 %4\%5\%3\%8.%2.txt >> ""{BackupDoneScriptOutputPathFile}""

     if ERRORLEVEL 1 set /A CopyFailures += 1
"
+
(!lbUseMainhostArchive ? "" :
@"
echo.                                                           >> ""{BackupDoneScriptOutputPathFile}""
echo This copies the backup to the virtual machine host archive:>> ""{BackupDoneScriptOutputPathFile}""

echo.                              >> ""{BackupDoneScriptOutputPathFile}""
echo copy %1 ""{MainHostArchive}"" >> ""{BackupDoneScriptOutputPathFile}""
     copy %1 ""{MainHostArchive}"" >> ""{BackupDoneScriptOutputPathFile}""

     if ERRORLEVEL 1 set /A CopyFailures += 1
"
)
+
@"
echo.                                                                   >> ""{BackupDoneScriptOutputPathFile}""
echo The following copies the backup (base name) to each attached backup>> ""{BackupDoneScriptOutputPathFile}""
echo device with the file ""{BackupDriveToken}"" at its root.           >> ""{BackupDoneScriptOutputPathFile}""

set BackupOutputPathFile=%1
set BackupBaseOutputFilename=%3
set BackupToolPath=%4\%5
set BackupToolName=%5

set BackupDeviceDecimalBitField=0
set BackupDevicePositionExponent=23

for %%d in (C: D: E: F: G: H: I: J: K: L: M: N: O: P: Q: R: S: T: U: V: W: X: Y: Z:) do call :DoCopy %%d

:: Combine the bit field and the copy failures into a single composite value.
:: The factor of 100 means that there can be a maximum of 99 copy failures.

set /A CompositeNumber = 100 * %BackupDeviceDecimalBitField% + %CopyFailures%
exit  %CompositeNumber%

:DoCopy
set /A BackupDevicePositionExponent -= 1
dir %1 > nul 2> nul
if ERRORLEVEL 1 goto :EOF
if not exist %1\""{BackupDriveToken}"" goto :EOF

:: Determine the bit position (and the corresponding decimal value) from the exponent.
set BitFieldDevicePosition=1
for /L %%x in (0, 1, %BackupDevicePositionExponent%) do set /A BitFieldDevicePosition *= 2

:: Add the calculated positional value to the bit field for the current backup device.
set /A BackupDeviceDecimalBitField += %BitFieldDevicePosition%

echo.                                                           >> ""{BackupDoneScriptOutputPathFile}""
echo This copies the backup to %1 (overwriting previous backup):>> ""{BackupDoneScriptOutputPathFile}""

echo.                                                           >> ""{BackupDoneScriptOutputPathFile}""
echo copy %BackupOutputPathFile% %1\%BackupBaseOutputFilename%  >> ""{BackupDoneScriptOutputPathFile}""
     copy %BackupOutputPathFile% %1\%BackupBaseOutputFilename%  >> ""{BackupDoneScriptOutputPathFile}""

     if ERRORLEVEL 1 set /A CopyFailures += 1

:: Exit if the following variables are both empty.
if ""%BackupToolName%%BackupBaseOutputFilename%""=="""" goto :EOF

echo.                                                           >> ""{BackupDoneScriptOutputPathFile}""
echo This removes the previous backup software profile files:   >> ""{BackupDoneScriptOutputPathFile}""

echo.                                                           >> ""{BackupDoneScriptOutputPathFile}""
echo rd  /s/q  %1\%BackupToolName%\%BackupBaseOutputFilename%   >> ""{BackupDoneScriptOutputPathFile}""
     rd  /s/q  %1\%BackupToolName%\%BackupBaseOutputFilename%   >> ""{BackupDoneScriptOutputPathFile}""

echo.                                                           >> ""{BackupDoneScriptOutputPathFile}""
echo This copies the backup software to %1\%BackupToolName%:    >> ""{BackupDoneScriptOutputPathFile}""

echo.                                                           >> ""{BackupDoneScriptOutputPathFile}""
echo xcopy  /s/y  %BackupToolPath% %1\%BackupToolName%\         >> ""{BackupDoneScriptOutputPathFile}""
     xcopy  /s/y  %BackupToolPath% %1\%BackupToolName%\         >> ""{BackupDoneScriptOutputPathFile}""

     if ERRORLEVEL 1 set /A CopyFailures += 1

"
)                       .Replace("{ProfileFile}", Path.GetFileName(moProfile.sLoadedPathFile))
                        .Replace("{BackupDoneScriptOutputPathFile}", Path.GetFileName(lsBackupDoneScriptOutputPathFile))
                        .Replace("{MainHostArchive}", lsMainhostArchivePath)
                        .Replace("{BackupDriveToken}", this.sBackupDriveToken)
                        );

                // Write the default "backup done" script if its
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

                Process loProcess = new Process();
                        loProcess.StartInfo.FileName = lsBackupDoneScriptPathFile;
                        loProcess.StartInfo.Arguments = string.Format(
                                  " \"{0}\" \"{1}\" \"{2}\" \"{3}\" \"{4}\" \"{5}\" \"{6}\" \"{7}\" \"{8}\" "
                                , msCurrentBackupOutputPathFile
                                , Path.GetFileName(msCurrentBackupOutputPathFile)
                                , Path.GetFileName(this.sBackupOutputPathFileBase())
                                , this.sArchivePath()
                                , Application.ResourceAssembly.GetName().Name
                                , Application.ResourceAssembly.Location
                                , moProfile.sBackupPathFile
                                , Path.GetFileName(moProfile.sLoadedPathFile)
                                , this.sLogPathFile
                                );
                        loProcess.StartInfo.UseShellExecute = true;
                        loProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        loProcess.Start();

                // Wait for the "backup done script" to finish.
                while ( !this.bMainLoopStopped && !loProcess.HasExited )
                {
                    System.Windows.Forms.Application.DoEvents();
                    System.Threading.Thread.Sleep(moProfile.iValue("-MainLoopSleepMS", 200));
                }

                // If a stop request came through, kill the backup done script.
                if ( this.bMainLoopStopped && !this.bKillProcess(loProcess) )
                    this.ShowError("The \"backup done\" script could not be stopped."
                            , "Backup Failed");

                if ( !this.bMainLoopStopped )
                {
                    // The exit code is defined in the script as a combination of two integers-
                    // a bit field of found backup devices and a count of copy failures (99 max).
                    double  ldCompositeNumber = loProcess.ExitCode / 100.0;
                    int     liBackupDevicesBitField = (int)ldCompositeNumber;   // The integer part is the bit field.
                    string  lsBackupDevicesBitField = Convert.ToString(liBackupDevicesBitField, 2);

                    // The fractional part (x 100) is the number of copy failures.
                    liBackupDoneScriptCopyFailures = (int)(100.0 * (ldCompositeNumber - (double)liBackupDevicesBitField));

                    // Compare the bit field of writeable drives to the bit field of backup devices 
                    // selected by the user.
                    List<char> loMissingDrives = new List<char>();
                    int liASCIIValueOfDrive = 67;
                    string liBackupDeviceSelectionsBitField = null == moProfile["-BackupDeviceSelectionsBitField"]
                                                              ? "000000000000000000000000"
                                                              : moProfile["-BackupDeviceSelectionsBitField"].ToString();

                    while (lsBackupDevicesBitField.Length < 24)
                    {
                        lsBackupDevicesBitField = "0" + lsBackupDevicesBitField;
                    }

                    for (int i = 0; i < 24; ++i)
                    {
                        if (Convert.ToInt32(liBackupDeviceSelectionsBitField[i]) > Convert.ToInt32(lsBackupDevicesBitField[i]))
                        {
                            loMissingDrives.Add((char)liASCIIValueOfDrive);
                        }

                        liASCIIValueOfDrive++;
                    };

                    if (0 == liBackupDoneScriptCopyFailures && 0 == loMissingDrives.Count)
                    {
                        this.LogIt("The \"backup done\" script finished successfully.");
                    }
                    else
                    {
                        string lsMessage = "";
                        string lsMessageCaption = "";

                        if (0 < liBackupDoneScriptCopyFailures)
                        {
                            lsMessageCaption += "Copy Failures";
                            lsMessage += String.Format("The \"backup done\" script had {0} copy failure{1}.\r\n"
                                    , liBackupDoneScriptCopyFailures
                                    , 1 == liBackupDoneScriptCopyFailures ? "" : "s");

                            this.LogIt(lsMessage);

                            // Get the output from the "backup done" script.

                            StreamReader loStreamReader = null;
                            string lsFileAsStream = null;

                            try
                            {
                                loStreamReader = new StreamReader(lsBackupDoneScriptOutputPathFile);
                                lsFileAsStream = loStreamReader.ReadToEnd();
                            }
                            catch (Exception ex)
                            {
                                this.ShowError(string.Format("File Read Failure: \"{0}\"\r\n"
                                        , lsBackupDoneScriptOutputPathFile) + ex.Message
                                        , "Failed Reading File"
                                        );
                            }
                            finally
                            {
                                if (null != loStreamReader)
                                    loStreamReader.Close();
                            }

                            this.LogIt("Here's output from the \"backup done script\":\r\n\r\n" + lsFileAsStream);

                            if (moProfile.bValue("-ShowBackupDoneScriptErrors", true))
                                this.DisplayFile(lsBackupDoneScriptOutputPathFile);
                        }

                        if (0 < loMissingDrives.Count)
                        {
                            lsMessageCaption += "" == lsMessageCaption ? "Missing Backup Devices" : " and Missing Backup Devices";
                            lsMessage += "List of missing backup devices:";
                            foreach (char drive in loMissingDrives)
                            {
                                lsMessage += "\n(" + drive + ":)";
                            }
                        }

                        if ("" != lsMessage)
                            tvMessageBox.ShowWarning(oUI, lsMessage, lsMessageCaption);
                    }
                }
            }
            catch (Exception ex)
            {
                this.ShowError(ex.Message, "Failed Running Backup Done Script");
            }

            return liBackupDoneScriptCopyFailures;
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
                return true;
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
                }
                catch (Exception ex)
                {
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
    -ApplyDeletionLimit

"
                            , Path.Combine(lsBackupOutputPath, lsBackupOutputFilenameNoExt)
                            , Path.GetExtension(lsBackupOutputPathFileBase)
                            ));

                    // Set the cleanup of backup software profile files to 30 days.
                    // The -Recurse switch is used here so that profile files for
                    // backups from various backup sets can be cleaned up in one sweep.
                    moProfile.Add("-CleanupSet", string.Format(@"
    -AgeDays=30
    -FilesToDelete={0}*
    -Recurse
    -ApplyDeletionLimit

"
                            , Path.Combine(Path.Combine(lsBackupOutputPath
                                    , Application.ResourceAssembly.GetName().Name)
                                    , Path.GetFileName(moProfile.sLoadedPathFile))
                            ));

                    // Set the cleanup of temporary backup files to 0 days.
                    // This is necessary to cleanup after killed processes.
                    moProfile.Add("-CleanupSet", string.Format(@"
    -AgeDays=0
    -FilesToDelete={0}.tmp*

"
                            , Path.Combine(this.sArchivePath(), "*" + Path.GetExtension(lsBackupOutputPathFileBase))
                            ));

                    // Set the cleanup of backup / cleanup log files to 30 days.
                    moProfile.Add("-CleanupSet", string.Format(@"
    -AgeDays=30
    -FilesToDelete={0}*{1}
    -FilesToDelete={2}*{3}

"
                            , Path.GetFileNameWithoutExtension(this.sLogPathFileBase)
                            , Path.GetExtension(this.sLogPathFileBase)
                            , Path.GetFileNameWithoutExtension(this.sDeletedFileListOutputPathFileBase)
                            , Path.GetExtension(this.sDeletedFileListOutputPathFileBase)
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
                    DateTime ldtOlderThan = DateTime.Now.AddDays(-loCurrentCleanupSet.iValue("-AgeDays", 365000));

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
                                      moProfile.sRelativeToProfilePathFile(loPathFilesEntry.Value.ToString())
                                    , ldtOlderThan
                                    , leFileDateTimeType
                                    , loCurrentCleanupSet
                                    );
                    }
                }

                if ( lbCleanupFiles )
                    lbCleanupFiles = this.DisplayDeletedFileList();
            }
            catch (Exception ex)
            {
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
                // recursion subfolder matches everything from the base path up.
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
                        bool    lbApplyDeletionLimit = aoProfile.bValue("-ApplyDeletionLimit", false);
                        int     liFileDeletionLimit = this.iFileDeletionLimit(
                                        loFileSysInfoList.Count(), adtOlderThan);
                        int     liIndex = 0;

                        foreach (FileSystemInfo loFileSysInfo in loFileSysInfoList)
                        {
                            System.Windows.Forms.Application.DoEvents();
                            System.Threading.Thread.Sleep(moProfile.iValue("-CleanupLoopSleepMS", 1));
                            if ( this.bMainLoopStopped )
                                break;

                            // Show UI activity for each file evaluated.
                            this.IncrementUIProgressBar();

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
                            // than the given date. If its also a hidden file, the
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

                            // Remove empty subfolders in the recurse folder only (ie.
                            // do not remove the recurse folder itself). In other words,
                            // lsSubfolderPlus may contain lsRecurseFolder, but it can't
                            // end with it (unless lsRecurseFolder is just a backslash).
                            if (              lsSubfolderPlus.Contains(lsRecurseFolder)
                                    && (     !lsSubfolderPlus.EndsWith(lsRecurseFolder)
                                        || lsDirectorySeparatorChar == lsRecurseFolder)
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

                                        // Using "-1" as the file size indicates a folder deletion.
                                        this.LogDeletedFile(lsSubfolder + " (dir)", ldtFileDate, -1);
                                    }
                                    catch (Exception ex)
                                    {
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
                this.ShowError(ex.Message, "Unanticipated Error");

                lbCleanupPathFileSpec = false;
            }

            return lbCleanupPathFileSpec;
        }
    }
}
