@echo off

echo Uploading English sources to Crowdin...
java -jar crowdin-cli.jar upload sources --no-colors --no-progress > upload.log 2>&1
if not %ERRORLEVEL%==0 (
    type upload.log
    exit /b 1
)

echo Downloading translations from Crowdin...
rmdir /S /Q translations >nul 2>&1
java -jar crowdin-cli.jar download --no-colors --no-progress > download.log 2>&1
if not %ERRORLEVEL%==0 (
    type download.log
    exit /b 1
)

echo Checking translations...
TranslationChecker.exe translations --fix
set CHECKER_ERRORLEVEL=%ERRORLEVEL%

echo Generating Qt translations...
for %%f in (translations\*.ts) do (
    lrelease -nounfinished %%f
)
exit /b %CHECKER_ERRORLEVEL%
