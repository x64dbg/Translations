@echo off
if "%QT64PATH%"=="" set QT64PATH=c:\Qt\qt-5.6.2-x64-msvc2013\5.6\msvc2013_64\bin
SET PATH=%PATH%;%QT64PATH%;c:\Program Files (x86)\7-Zip
del /S /Q *.qm
curl -k https://api.crowdin.com/api/project/x64dbg/export?key=%CROWDIN_API_KEY%
curl -k -o translations.zip https://api.crowdin.com/api/project/x64dbg/download/all.zip?key=%CROWDIN_API_KEY%
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
