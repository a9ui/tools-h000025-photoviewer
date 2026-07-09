@echo off
setlocal
cd /d "%~dp0"

set "PROJECT=local-native\PhotoViewer.Native\PhotoViewer.Native.csproj"

if not exist "%PROJECT%" (
    echo [PhotoViewer WinForms] Project not found: %PROJECT%
    pause
    exit /b 1
)

echo [PhotoViewer WinForms] Launching native WinForms viewer...
echo.
dotnet run --project "%PROJECT%" %*
set "EXIT_CODE=%ERRORLEVEL%"

if not "%EXIT_CODE%"=="0" (
    echo.
    echo [PhotoViewer WinForms] Exited with code %EXIT_CODE%.
)
pause
exit /b %EXIT_CODE%
