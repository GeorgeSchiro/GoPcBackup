for /f "tokens=1-9 delims=\" %%a in ("%cd%") do set a=%%a %%b %%c %%d %%e %%f %%g %%h %%i
for %%i in (%a%) do set Project=%%i

del  ..\..\%Project%.lnk
del "..\..\%Project% - Move to Startup.cmd"
rd /s/q ..\..\%Project%

if exist "C:\Program Files\%Project%\[Don't zap this.]" goto Continue

del "%ALLUSERSPROFILE%\Start Menu\Programs\Startup\%Project%.lnk"
rd /s/q "C:\Program Files"\%Project%

:Continue
del bin\*.cmd         /s/q
del bin\*.config      /s/q
del bin\*.application /s/q
del bin\*.manifest    /s/q
del bin\*.txt         /s/q
del bin\*.xml         /s/q
del bin\*.pdb         /s/q
del bin\*.vshost.*    /s/q
del bin\Release\*.z*  /s/q
rd  bin\Debug         /s/q
rd  obj               /s/q
