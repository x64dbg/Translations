@echo off

echo Uploading English sources to Crowdin...
java -jar crowdin-cli.jar upload sources
if not %ERRORLEVEL%==0 exit /b

echo Downloading translations from Crowdin...
rmdir /S /Q translations >nul 2>&1
java -jar crowdin-cli.jar download
if not %ERRORLEVEL%==0 exit /b

echo Checking translations...
TranslationChecker.exe translations --folder --fix
set CHECKER_ERRORLEVEL=%ERRORLEVEL%

echo Generating Qt translations...
for %%f in (translations\*.ts) do (
    lrelease -nounfinished %%f
)
exit /b %CHECKER_ERRORLEVEL%
