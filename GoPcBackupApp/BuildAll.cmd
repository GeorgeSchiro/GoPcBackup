@echo off

set   AppName=GoPcBackup
set    AppExe=%AppName%.exe
set AppFolder=%AppName%App
set DevServer=main
set ExeFolder=\\%DevServer%\GoPcBackup\DotNet EXEs

echo.
echo *** Rebuild All %AppName% Versions ***

if exist ..\README.md   goto ErrorExit
if exist ..\Windows\*.* goto ErrorExit


echo.
echo This build process will rebuild all versions of the "%AppName%" application.
echo.
echo Be sure the .Net 3.5 SDK (full) and the .Net 4.0 SDK have been installed.
echo.
echo The SDKs can be installed from ISO images. They
echo are required only for their reference assemblies.
echo.
echo ********************************************************************************
echo *** Note: Any new files added to the base project (eg. images, styles, etc.) ***
echo ***       MUST also be added to "GoPcBackup.csproj (4.x)".                   ***
echo ***                                                                          ***
echo *** IF you FAIL to do this, the 4.x version may compile but FAIL to run!!!   ***
echo ********************************************************************************
pause


:: Remove local application folder, etc.
if exist "%AppFolder%\*.*" rmdir /s/q "%AppFolder%"
if exist "%ExeFolder%\*.*" rmdir /s/q "%ExeFolder%"
if exist bin\*.* rmdir /s/q bin\
if exist obj\*.* rmdir /s/q obj\

xcopy /s "\\%DevServer%\%AppName%\%AppFolder%" "%AppFolder%\"
cd "%AppFolder%"


:: Use version 3.5 make file by default.
echo.
C:\Windows\Microsoft.NET\Framework\v3.5\msbuild       -p:Configuration=Release -verbosity:m

echo.
xcopy "bin\Release\%AppExe%" "%ExeFolder%\DotNet Version 3.5\"


:: Switch to version 4 files.
ren "%AppName%.csproj"       "%AppName%.csproj (3.5)"
ren "%AppName%.csproj (4.x)" "%AppName%.csproj"

ren "Setup Application Folder.exe"       "Setup Application Folder.exe (3.5)"
ren "Setup Application Folder.exe (4.x)" "Setup Application Folder.exe"

echo.
C:\Windows\Microsoft.NET\Framework\v4.0.30319\msbuild -p:Configuration=Release -verbosity:m
:: (for 4.5 specifically)
::C:\Windows\Microsoft.NET\Framework\v4.0.30319\msbuild -p:Configuration=Release -verbosity:m -p:FrameworkPathOverride="C:\Program Files\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5"
ren "%AppName%.csproj"       "%AppName%.csproj (4.x)"
ren "Setup Application Folder.exe"       "Setup Application Folder.exe (4.x)"

echo.
xcopy "bin\Release\%AppExe%" "%ExeFolder%\DotNet Version 4.x\"
echo.
pause
cd ..
if exist "%AppFolder%\*.*" rmdir /s/q "%AppFolder%"
goto :EOF


:ErrorExit
echo.
echo This script must be copied to and run on the "rebuild all versions" machine.
pause
