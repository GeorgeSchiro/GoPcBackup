@echo off

set   AppName=GoPcBackup
set    AppExe=%AppName%.exe
set AppFolder=%AppName%App
set DevServer=192.168.0.3
set ExeFolder=\\%DevServer%\%AppName%\DotNet EXEs

echo.
echo *** Rebuild All %AppName% Versions ***

if exist ..\README.md   goto ErrorExit
if exist ..\Windows\*.* goto ErrorExit


echo.
echo This build process will rebuild all versions of the "%AppName%" application.
echo.
echo Be sure the .Net 4.x SDK and the .Net 3.5 SDK (full) have been installed.
echo.
echo The SDKs can be installed from ISO images. They
echo are required only for their reference assemblies.
echo.
echo ********************************************************************************
echo *** Note: Any new files added to the base project (eg. images, styles, etc.) ***
echo ***       MUST also be added to "%AppName%.csproj (3.5)".                    ***
echo ***                                                                          ***
echo *** IF you FAIL to do this, the 3.5 version may compile but FAIL to run!!!   ***
echo ********************************************************************************
pause


:: Remove local application folder, etc.
if exist "%AppFolder%\*.*" rmdir /s/q "%AppFolder%"
if exist "%ExeFolder%\*.*" rmdir /s/q "%ExeFolder%"

xcopy /s "\\%DevServer%\%AppName%\%AppFolder%" "%AppFolder%\"
cd "%AppFolder%"


:: Use version 4.x make file by default.
echo.
if exist bin\*.* rmdir /s/q bin\
if exist obj\*.* rmdir /s/q obj\
C:\Windows\Microsoft.NET\Framework\v4.0.30319\msbuild -p:Configuration=Release -verbosity:m

echo.
xcopy "bin\Release\%AppExe%" "%ExeFolder%\DotNet Version 4.x\"


:: Switch to version 3.5 files.
ren "%AppName%.csproj"       "%AppName%.csproj (4.x)"
ren "%AppName%.csproj (3.5)" "%AppName%.csproj"

echo.
if exist bin\*.* rmdir /s/q bin\
if exist obj\*.* rmdir /s/q obj\
C:\Windows\Microsoft.NET\Framework\v3.5\msbuild -p:Configuration=Release -verbosity:m

echo.
xcopy "bin\Release\%AppExe%" "%ExeFolder%\DotNet Version 3.5\"
echo.
pause
cd ..
if exist "%AppFolder%\*.*" rmdir /s/q "%AppFolder%"
goto :EOF


:ErrorExit
echo.
echo This script must be copied to and run on the "rebuild all versions" machine.
pause
