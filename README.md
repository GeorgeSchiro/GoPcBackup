Overview
========


<b>GoPC Backup</b> is a simple software utility for backing up virtual machines (screenshots below).

It backs up files from a virtual PC (running Windows) to its virtual machine host. It can also automatically copy each backup to network shares as well as any attached devices (eg. thumb drives). It can also clean up files older than a given number of days.

<b>GoPC Backup</b> is not limited to virtual machines. It can also backup and cleanup files on any Windows PC.

During a crisis, scrambling to assemble pieces of backups to restore can be harrowing. Having everything in one place really makes more sense. Have you heard about backups that were never tested until a system crash? Have you ever worried that your backup may fail to restore when you need it?

For these reasons <b>GoPC Backup</b> does full backups rather than incremental backups and it uses common ZIP files that can be verified anytime simply by opening them and browsing their contents. Any ZIP file software can browse and restore your backups. This means that you can easily restore individual files or all of them.

<b>GoPC Backup</b> creates date-named ZIP files. That way you can keep and review previous versions of your files as an archive.

This utility performs file backups and file cleanups in the background.

It also acts as its own scheduler. First, it checks for files to be removed on a given schedule. Then it runs a backup of your files automatically.

There is no need to use a job scheduler unless this software is running on a server computer that has no regular user activity (see -NoPrompts and -RunOnce below).

You provide various file specifications (ie. locations of the files to backup and to cleanup) as well as file age limits for the files to cleanup. The rest is automatic.

<b>GoPC Backup</b> will run in the background unless its timer is turned off. The simple user interface (UI) is usually minimized to the system tray.

Give it a try.

The first time you run "GoPcBackup.exe" it will prompt you to create a "GoPcBackup" folder on your desktop. It will copy itself and continue running from there.

Everything the software needs is written to the "GoPcBackup" folder. Nothing is written anywhere else (except your backups).

If you like the software, you can leave it on your desktop or you can run "Setup Application Folder.exe" (also in the "GoPcBackup" folder, be sure to run it as administrator). If you decide not to keep the software, simply delete the "GoPcBackup" folder from your desktop.


Features
========


-   Simple setup - try it out fast
-   Uses standard ZIP files for backups
-   Backs up any number of folders (local or LAN)
-   Cleans up any number of files (local or LAN)
-   Runs automatically in the background on a daily timer
-   Includes a simple general-purpose built-in task scheduler
-   Automatically copies backups to the virtual machine host
-   Automatically copies backups to any number of attached devices
-   Comprehensive dated log files are produced with every backup
-   Dated logs of deleted files are produced with every cleanup
-   Log files are automatically cleaned up on schedule
-   Backup files can be automatically cleaned up on schedule also
-   "Backup Begin" and "Backup Done" scripts are user modifiable
-   Can be command-line driven from a server batch job scheduler
-   Software is highly configurable
-   Software is totally self-contained (EXE is its own setup)


Details
=======


<b>GoPC Backup</b> was designed for programmers.

Programmers work with many small files (mostly text). These files usually change in small ways over time. They are often kept in one place (eg. in a "Projects" folder on the desktop).

Losing a single file can often do serious damage to a software project. Seeing a previous version of a file (and restoring it) is often a saving grace. The ability to see old versions of your files without the complexity of a version control system is also a benefit.

If you are not a programmer, yet you have many relatively small files to backup (general data files, documents and images, not large audio / video files), this software may be helpful to you too. This is especially true if you keep your files in various folders in one place (eg. on your desktop or some other central location like "My Documents").

<b>GoPC Backup</b> initially presents a setup wizard to make it easy to get the software up-and-running fast. All of its flexibility is managed through a single plain-text profile file (ie. a configuration file). Everything is managed through the profile file. <b>GoPC Backup</b> does not use the windows registry at all.

The setup wizard asks for a few basic pieces of information, most of which have default values:

-   Folder to Backup           ("Desktop")
-   Output Filename            ("UsernameFiles")
-   Local Archive Folder       ("C:\Archive")
-   Backup to VM Host?         ("false")
-   VM Host Archive            (none by default)
-   Additional Backup Devices  (none by default)
-   Backup Time                ("12:00 am")

After running a backup you can inspect the results and make adjustments as needed. The adjustments can be made either through the setup wizard or through the profile file directly.

Whatever you can't configure through the <b>GoPC Backup</b> UI you can configure by editing the profile file by hand (ie. "GoPcBackup.exe.txt"). This is typically done with notepad. The profile file is usually located in the same folder as the backup software (ie. "GoPcBackup.exe").

Here's some of what you might see in a profile file:

    -BackupTime="4:00 AM"
    -UseVirtualMachineHostArchive=True
    -VirtualMachineHostArchivePath=\\Mainhost\Archive
    -BackupSet=[
 
        -FolderToBackup=C:\Desktop
        -FolderToBackup=C:\Documents and Settings\Admin\My Documents
        -OutputFilename=AdminFiles.zip

    -BackupSet=]
    -BackupSet=[
 
        -FolderToBackup=D:\OtherFiles 
        -OutputFilename=AdminOtherFiles.zip

    -BackupSet=]
    -CleanupSet=[

        -AgeDays=365
        -FilesToDelete=C:\Archive\AdminFiles*.zip
        -FilesToDelete=C:\Archive\AdminOtherFiles*.zip
        -ApplyDeletionLimit

    -CleanupSet=]
    -CleanupSet=[

        -AgeDays=30
        -FilesToDelete=Logs\*.txt

    -CleanupSet=]

You can have multiple backup sets as well as multiple cleanup sets in the same profile. Each of these sets can in turn reference multiple sets of files.

The setup wizard only edits the first folder referenced within the first backup set. For anything else you need to edit the profile file by hand.

You will notice that the profile data is not formatted as XML. It is expressed in "command-line" format. That makes it easier to read and parse. It is also a reminder that anything you see in the profile file can be overridden with the equivalent "-key=value" pairs passed on the "GoPcBackup.exe" command-line.

<b>GoPC Backup</b> uses "7-zip" as its ZIP compression engine. "7-zip" is an excellent tool.  

<b>GoPC Backup</b> is essentially an automation front-end for "7-zip". That said, you can replace "7-zip" with any other command-line driven ZIP tool you might prefer instead. The choice of compression tool is entirely yours. This change is made in the profile file like everything else (see "-ZipToolEXE" below).


Screenshots
===========


![Copy EXE to Desktop?] (https://raw.github.com/GeorgeSchiro/GoPcBackup/master/Project/Screenshots/Shot00i.png)
![Main UI             ] (https://raw.github.com/GeorgeSchiro/GoPcBackup/master/Project/Screenshots/Shot00j.png)

The following setup wizard tabs are displayed when the software is started the first time. They ask a few basic questions about what you want to backup, to where and on what schedule.

The screenshots after that show an actual backup run.

![Setup Wizard Step 1 ] (https://raw.github.com/GeorgeSchiro/GoPcBackup/master/Project/Screenshots/Shot01.png)
![Setup Wizard Step 2 ] (https://raw.github.com/GeorgeSchiro/GoPcBackup/master/Project/Screenshots/Shot02.png)
![Setup Wizard Step 3 ] (https://raw.github.com/GeorgeSchiro/GoPcBackup/master/Project/Screenshots/Shot03.png)
![Setup Wizard Step 4 ] (https://raw.github.com/GeorgeSchiro/GoPcBackup/master/Project/Screenshots/Shot04.png)
![Setup Wizard Finish ] (https://raw.github.com/GeorgeSchiro/GoPcBackup/master/Project/Screenshots/Shot05.png)
![Run Backup Prompt   ] (https://raw.github.com/GeorgeSchiro/GoPcBackup/master/Project/Screenshots/Shot06.png)
![Cleanup Started     ] (https://raw.github.com/GeorgeSchiro/GoPcBackup/master/Project/Screenshots/Shot07.png)
![Backup Started      ] (https://raw.github.com/GeorgeSchiro/GoPcBackup/master/Project/Screenshots/Shot08.png)
![Backup Running      ] (https://raw.github.com/GeorgeSchiro/GoPcBackup/master/Project/Screenshots/Shot09.png)
![Backup Finished     ] (https://raw.github.com/GeorgeSchiro/GoPcBackup/master/Project/Screenshots/Shot10.png)

With "Use Timer" checked, the software closes to the system tray. There it is ready for the next scheduled backup.

After clicking "GoPC" in the system tray and the "Toggle Backup Timer" icon you can see when the next backup starts, the previous run and the previous backup result.

![Next Backup Waiting ] (https://raw.github.com/GeorgeSchiro/GoPcBackup/master/Project/Screenshots/Shot11.png)


Command-Line Usage
==================


    Open this utility's profile file to see additional options available. It is
    usually located in the same folder as "GoPcBackup.exe" and has the same name
    with ".txt" added (see "GoPcBackup.exe.txt").

    Profile file options can be overridden with command-line arguments. The
    keys for any "-key=value" pairs passed on the command-line must match
    those that appear in the profile (with the exception of the "-ini" key).

    For example, the following invokes the use of an alternative profile file:

        GoPcBackup.exe -ini=NewProfile.txt

    This tells the software to run in automatic mode:

        GoPcBackup.exe -AutoStart


    Author:  George Schiro (GeoCode@Schiro.name)

    Date:    7/3/2013

 
Options and Features
====================


    The main options for this utility are listed below with their default values.
    A brief description of each feature follows.

-AddTasks= NO DEFAULT VALUE

    Each added task has its own profile:

    -Task= NO DEFAULT VALUE

        -CommandEXE= NO DEFAULT VALUE

            This is the path\file specification of the task executable to be run.

        -CommandArgs=""

            This is the list of arguments passed to the task executable.

        -CommandWindowTitle=""

            This is the main window title for this instance of the task executable.
            This will be used to determine, during startup, if the task is already
            running (when multiple instances of the same executable are found).

        -CreateNoWindow=False

            Set this switch True and nothing will be displayed when the task runs.

        -OnStartup=False

            Set this switch True and the task will start each time "GoPcBackup.exe"
            starts. If the task EXE is already running, it will not be started again.

        -StartTime= NO DEFAULT VALUE

            Set this to the time of day to run the task (eg. 3:00am, 9:30pm, etc).

        -StartDays=""

            Set this to days of the week to run the task (eg. Monday, Friday, etc).
            This value may include a comma-separated list of days as well as ranges
            of days. Leave this blank and the task will run every day at -StartTime.

        -TimeoutMinutes=0

            Set this to a number greater than zero to have the task run to completion
            and have all output properly logged before proceeding to the next task
            (ie. -CreateNoWindow will be set and IO redirection will be handled).

        -UnloadOnExit=False

            Set this switch True and the task executable will be removed from memory
            (if it's still running) when "GoPcBackup.exe" exits.


        Here's an example:

        -AddTasks=[

            -Task= -OnStartup -CommandEXE=http://xkcd.com
            -Task= -StartTime=6:00am -CommandEXE=shutdown.exe -CommandArgs=/r /t 60
            -Task= -StartTime=7:00am -StartDays="Mon-Wed,Friday,Saturday" -CommandEXE="C:\Program Files\Calibre2\calibre.exe"  -Note=Fetch NY Times after 6:30am
            -Task= -StartTime=8:00am -StartDays="Sunday" -CommandEXE="C:\Program Files\Calibre2\calibre.exe"  -Note=Fetch NY Times after 7:30am Sundays

        -AddTasks=]

-ArchivePath=C:\Archive

    This is the destination folder of the backup output files unless
    overridden in -BackupSet (see below).

-AutoStart=True

    This tells the software to run in automatic mode. Set this switch False
    and the main loop in the UI will only start manually. The software will
    also vacate memory after the UI window closes. This is the timer switch.

-BackupBeginScriptEnabled=True

    Set this switch False to skip running the "backup begin" script.

-BackupBeginScriptHelp= SEE PROFILE FOR DEFAULT VALUE

    This is the default content of the DOS script that is initially written to
    -BackupBeginScriptPathFile and run before each backup starts. It contains
    a description of the command-line arguments passed to the script at runtime.

-BackupBeginScriptInit=False

    Set this switch True and the "backup begin" script will be automatically
    overwritten from the content of -BackupBeginScriptHelp. Once used this switch
    will be reset to False.

    Note: the content of -BackupBeginScriptHelp will also be overwritten from the
    default value embedded in the executable file.

-BackupBeginScriptPathFile=GoPcBackupBegin.cmd

    This DOS shell script is run before each backup starts. Edit the contents
    of the file or point this parameter to another file. If you delete the file,
    it will be recreated from the content found in -BackupBeginScriptHelp (see 
    above).

-BackupDoneScriptEnabled=True

    Set this switch False to skip running the "backup done" script.

-BackupDoneScriptHelp= SEE PROFILE FOR DEFAULT VALUE

    This is the default content of the DOS script that is initially written to
    -BackupDoneScriptPathFile and run after each successful backup. It contains
    a description of the command-line arguments passed to the script at runtime.

-BackupDoneScriptInit=False

    Set this switch True and the "backup done" script will be automatically
    overwritten from the content of -BackupDoneScriptHelp. Once used this switch
    will be reset to False.

    Note: the content of -BackupDoneScriptHelp will also be overwritten from the
    default value embedded in the executable file.

-BackupDoneScriptPathFile=GoPcBackupDone.cmd

    This DOS shell script is run after each successful backup completes. You 
    can edit the contents of the file or point this parameter to another file.
    If you delete the file, it will be recreated from the content found in 
    -BackupDoneScriptHelp (see above).

-BackupFailedScriptEnabled=True

    Set this switch False to skip running the "backup failed" script.

-BackupFailedScriptHelp= SEE PROFILE FOR DEFAULT VALUE

    This is the default content of the DOS script that is initially written to
    -BackupFailedScriptPathFile and run after each failed backup. It contains
    a description of the command-line arguments passed to the script at runtime.

-BackupFailedScriptInit=False

    Set this switch True and the "backup failed" script will be automatically
    overwritten from the content of -BackupFailedScriptHelp. Once used this switch
    will be reset to False.

    Note: the content of -BackupFailedScriptHelp will also be overwritten from the
    default value embedded in the executable file.

-BackupFailedScriptPathFile=GoPcBackupFailed.cmd

    This DOS shell script is run after a backup fails to complete. You can
    edit the contents of the file or point this parameter to another file.
    If you delete the file, it will be recreated from the content found in 
    -BackupFailedScriptHelp (see above).

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

-BackupSet="One of many file sets to backup goes here."

    Each file backup set has its own profile:

        -ArchivePath= INHERITED

            This is the destination folder of the backup output files. If
            provided, this value will override the parent -ArchivePath (see
            above).

        -BackupFileSpec= INHERITED

            This wildcard is appended to each folder to backup. If provided,
            it will override the parent -BackupFileSpec (see above).

        -FolderToBackup="One of many folders to backup goes here."

            This is the full path\file specification of a folder to backup.
            This parameter can appear multiple times in each backup set.

            Instead of an entire folder you can use a path\file pattern like
            "C:\Folder\File?.*" to backup a subset of files or a single file.

        -OutputFilename=Files

            This is the backup output filename with no path and no extension.
            This parameter will be combined with -BackupOutputExtension, 
            -ArchivePath and -BackupOutputFilenameDateFormat to produce a
            full backup output path\file specification.

-BackupTime=12:00 AM

    This is the time each day that the backup starts.

-BackupTimeMinsPerTick=15

    This determines how many minutes the backup time changes with each tick
    of the backup time selection slider in the UI.

-CleanupFiles=True

    Set this switch False to disable cleanups (ie. do file backups only).

-CleanupLoopSleepMS=1

    This is the number of milliseconds of process thread sleep time between
    file deletions. The default of 1 ms should result in rapid deletions. You 
    can increase this value if you are concerned that the UI is not responsive
    enough or the process is using too much CPU while deleting.

-CleanupSet="One of many file sets to cleanup goes here."

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

            The default ("LastWriteTime") is the file modification date.

        -FilesToDelete="One of many path\file specifications goes here."

            These are the files evaluated for deletion based on their age
            (see -AgeDays above). Wildcards are expected but not required
            (you can reference a single file if you like).

        -Recurse=False

            Set this switch True and the file cleanup process will recurse 
            through all subdirectories starting from the path of the given
            -FilesToDelete (see above) looking for files to remove with the
            same file specification found in the -FilesToDelete parameter.

        -RecurseFolder=""

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

    This is the maximum number of seconds given to a process after a "close"
    command is given before the process is forcibly terminated.

-KillProcessForcedWaitMS=1000

    This is the maximum milliseconds to wait while force killing a process.

-LogEntryDateTimeFormatPrefix"yyyy-MM-dd hh:mm:ss:fff tt  "

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
    if you are concerned that the timer UI is using too much CPU while waiting.

-NoPrompts=False

    Set this switch True and all pop-up prompts will be suppressed. Messages
    are written to the log instead (see -LogPathFile above). You must use this
    switch whenever the software is run via a server computer batch job or job
    scheduler (ie. where no user interaction is permitted).

-PreviousBackupDevicesMissing=False

    This is the True or False "Devices Missing" status of the previous backup 
    run. If True, at least one external device was missing when the backup ran.

-PreviousBackupOk= NO DEFAULT VALUE

    This is the True or False "Ok" status of the previous backup / cleanup run.

-PreviousBackupTime= NO DEFAULT VALUE

    This is the completion timestamp of the previous backup / cleanup run.

-RunOnce=False

    Set this switch True to run this utility one time only (with no UI) then
    shutdown automatically thereafter. This switch is useful if the utility
    is run in a batch process or if it is run by a server job scheduler.

-SaveProfile=True

    Set this switch False to prevent saving to the profile file by the backup
    software itself. This is not recommended since backup status information is 
    written to the profile after each backup runs.

-SelectedBackupDevices= NO DEFAULT VALUE

    This is the list of selected backup devices as human readable text.

-SelectedBackupDevicesBitField=0 (0 means not yet set)

    This is the list of selected backup devices as a bit field. All bit fields
    have a leading 1 bit to preserve leading zeros. The second bit starts the
    device list (ie. drive letter list). Drive C: is not available as a backup
    device. So the second bit identifies drive D:.

-ShowBackupBeginScriptErrors=True

    Set this switch False to suppress the pop-up display of "backup begin" script
    errors (see -BackupBeginScriptPathFile above).

-ShowBackupDoneScriptErrors=True

    Set this switch False to suppress the pop-up display of "backup done" script
    errors (see -BackupDoneScriptPathFile above).

-ShowDeletedFileList=False

    Set this switch True and the list of deleted files will be displayed in a 
    pop-up window.

-ShowProfile=False

    Set this switch True to immediately display the entire contents of the profile
    file at startup in command-line format. This is sometimes helpful to diagnose
    problems.

-UseConnectVirtualMachineHost=False

    Set this switch True to force a connection to the virtual machine share archive
    before each backup starts (ie. during the "backup begin" script).

-UseVirtualMachineHostArchive=False

    Set this switch True and code will be added to the "backup done" script
    (see -BackupDoneScriptPathFile above) to copy backups to your virtual
    machine host computer (assuming you have one). Alternatively, any network
    share can be referenced here for a similar purpose.

-VirtualMachineHostArchivePath= NO DEFAULT VALUE

    This value is used within the "backup done" script to copy backups to the
    virtual machine host share (see -UseVirtualMachineHostArchive above).

    You may want to reference your VM host by IP address rather than by name.
    Doing so is often more reliable than using net bios names on your local
    area network.

-VirtualMachineHostPassword= NO DEFAULT VALUE

    This value is the password used within the "backup begin" script to log
    into the virtual machine host share (see -UseConnectVirtualMachineHost
    above).

-VirtualMachineHostUsername= NO DEFAULT VALUE

    This value is the username used within the "backup begin" script to log
    into the virtual machine host share (see -UseConnectVirtualMachineHost
    above).

-XML_Profile=False

    Set this switch True to change the profile file from command-line format
    to XML format.

-ZipToolEXE=7z.exe

    This is the ZIP tool executable that performs the backup compression.

-ZipToolEXEargs=a -ssw "{BackupOutputPathFile}" @"{BackupPathFiles}" -w"{BackupOutputPath}"

    These are command-line arguments passed to the ZIP compression tool (see
    -ZipToolEXE above). The tokens (in curly brackets) are self-evident. They
    are replaced at runtime.

-ZipToolEXEargsMore= NO DEFAULT VALUE

    These are additional command line arguments for the ZIP tool. Using
    this parameter makes it easier to add functionality without changing
    the existing command line. A typical example would be to supply an 
    encryption password on the command line to "GoPcBackup.exe" itself.

-ZipToolFileListFileDateFormat=-yyyy-MM-dd

    This format string is used to form the variable part of each file list
    output filename (see -ZipToolFileListPathFile below). It is inserted
    between the filename and the extension.

-ZipToolFileListPathFile=FileLists\ZipFileList.txt

    This is the file used to store the list of filenames to be compressed.
    The profile file name will be prepended to the default and the current
    date (see -ZipToolFileListFileDateFormat above) will be inserted (with
    a GUID) between the filename and the extension.

-ZipToolLastRunCmdPathFile=Run Last Backup.cmd

    This is a script file (text), which contains a copy of the last ZIP tool
    command line executed.


Notes:

    There may be various other settings that can be adjusted also (user
    interface settings, etc). See the profile file ("GoPcBackup.exe.txt")
    for all available options.

    To see the options related to any particular behavior, you must run that
    part of the software first. Configuration options are added "on the fly"
    (in order of execution) to "GoPcBackup.exe.txt" as the software runs.
