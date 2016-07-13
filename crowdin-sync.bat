@echo off
call ..\..\..\setenv.bat x64
SET PATH=%PATH%;c:\Program Files (x86)\7-Zip
curl https://api.crowdin.com/api/project/x64dbg/export?key=%CROWDIN_API_KEY%
curl -o translations.zip https://api.crowdin.com/api/project/x64dbg/download/all.zip?key=%CROWDIN_API_KEY%
rmdir /S /Q translations
7z x -otranslations translations.zip
cd translations
for /D %%a in (*) do (set fname=%%a) & call :rename
move *.qm ..\
cd ..
goto :eof
:rename
set trname=x64dbg_%fname:-=_%.ts
copy %fname%\x64dbg.ts %trname%
lrelease -nounfinished %trname%