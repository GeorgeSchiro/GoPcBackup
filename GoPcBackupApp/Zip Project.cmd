set Project=GoPcBackup
set  ZipEXE=7za.exe

for /f "tokens=1-9 delims=\" %%a in ("%cd%") do set a=%%a %%b %%c %%d %%e %%f %%g %%h %%i
for %%i in (%a%) do set ProjectFolder=%%i

if not %Project%.==. goto Continue

set Project=%ProjectFolder%

:Continue
cd ..

del                                              %ProjectFolder%\%Project%.zip
echo %ProjectFolder%\%ZipEXE% -bb a -r -xr!bin -xr!obj %ProjectFolder%\%Project%.zip %ProjectFolder%\*.*
     %ProjectFolder%\%ZipEXE% -bb a -r -xr!bin -xr!obj %ProjectFolder%\%Project%.zip %ProjectFolder%\*.*

if not exist %ProjectFolder%\bin\Release\*.* goto EOF

cd %ProjectFolder%\bin\Release

      del Setup.zip
      del Setup.zzz
..\..\%ZipEXE% a Setup.zip %Project%.exe
      copy Setup.zip *.zzz
