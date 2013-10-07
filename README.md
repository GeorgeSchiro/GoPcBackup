Overview
========
<br>


<b>GoPC Backup</b> is a simple software utility used for backing up virtual machines.

It backs up files from a virtual PC (running Windows) to its virtual machine host. It can also automatically copy each backup to network shares as well as any attached devices (eg. thumb drives). It can also clean up files older than a given number of days.

<b>GoPC Backup</b> is not limited to virtual machines. It can also backup and cleanup files on any Windows PC.

It uses the standard ZIP file format for its backups. So any ZIP software can browse and restore your backup files. It also creates date-named ZIP files. That way you can keep and review previous versions of your files as an archive.

This utility performs file backups and file cleanups in the background.

It also acts as its own scheduler. First, it checks for files to be removed on a given schedule. Then it runs a backup of your files automatically.

There is no need to use a job scheduler unless this software is running on a server computer that has no regular user activity (see -RunOnce below).

You provide various file specifications (ie. path\file locations of the files to backup and to cleanup) as well as file age limits for the files to cleanup. The rest is automatic.

This utility will run in the background unless its timer is turned off. Its simple user interface (UI) is usually minimized to the system tray.


Give it a try.

The first time you run "GoPcBackup.exe" it will prompt you to create a "GoPcBackup" folder on your desktop. It will copy itself and continue running from there.

Everything the software needs to run is created in the "GoPcBackup" folder. Nothing is written anywhere else (except your backups).

If you like the software, you can leave it on your desktop or you can run "Setup in Program Files.exe" (be sure to run it as an administrator). If you decide not to keep the software, simply delete the "GoPcBackup" folder from your desktop.


<br>
Features
========

-   Simple setup - try it out fast!
-   Uses standard ZIP files for backups
-   Backs up any number of folders anywhere (local or LAN)
-   Cleans up any number of files anywhere (local or LAN)
-   Runs automatically in the background on a simple daily timer
-   Automatically copies backups to the virtual machine host
-   Automatically copies backups to any number of attached devices
-   Comprehensive dated log files are produced with every backup
-   Dated logs of deleted files are produced with every cleanup
-   Log files are automatically cleaned up on schedule
-   Backup files can be automatically cleaned up on schedule also
-   "Backup Begin" and "Backup Done" scripts are user modifiable
-   Can be command-line driven from a batch job scheduler
-   Software is highly configurable
-   Software is totally self-contained (EXE is its own setup)


<br>
Details
=======
<br>


<b>GoPC Backup</b> was designed for programmers.

Programmers work with many small files (mostly text). These files usually change in small ways over time. They are often kept in one place (eg. in a "Projects" folder on the desktop).

Losing a single file can often do serious damage to a software project. Seeing a previous version of a file (and restoring it) is often a saving grace. The ability to see old versions of your files without the complexity of a version control system is also a benefit.

If you are not a programmer, yet you have many relatively small files to backup (general documents and images, not large audio / video files), this software may be helpful to you too. This is especially true if you keep your files in various folders in one place (eg. on your desktop or some other central location like "My Documents").

<b>GoPC Backup</b> initially presents a setup wizard to make it easy to get the software up-and-running fast. All of its flexibility is managed through a plain-text profile file (ie. a configuration file). Everything is managed through the profile file. <b>GoPC Backup</b> does not use the windows registry at all.

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


<br>
Here's an example of some of what you might see in a profile file:

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
        -FilesToDelete=Logs\GoPcBackup.exeLog*.txt
        -FilesToDelete=Logs\GoPcBackup.exeDeletedFileList*.txt

    -CleanupSet=]


<br>
You can have multiple backup sets as well as multiple cleanup sets in the same profile. Each of these sets can in turn reference multiple sets of files.

The setup wizard only edits the first folder referenced within the first backup set. For anything else you need to edit the profile file by hand.

You will notice that the profile data is not formatted as XML. It is expressed in "command-line" format. That makes it easier to read and parse. It is also a reminder that anything you see in the profile file can be overridden with the equivalent "-key=value" pairs passed on the "GoPcBackup.exe" command-line.

<b>GoPC Backup</b> uses "7-zip" as its ZIP compression engine. "7-zip" is an excellent tool.  

<b>GoPC Backup</b> is essentially an automation front-end for "7-zip". That said, you can replace "7-zip" with any other command-line driven ZIP tool you might prefer instead. The choice of compression tool is entirely yours. This change is made in the profile file like everything else (see "-ZipToolEXE" below).
