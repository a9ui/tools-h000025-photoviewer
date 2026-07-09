@echo off
setlocal
cd /d "%~dp0"

set "PROJECT=local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj"

if not exist "%PROJECT%" (
    echo [PhotoViewer WPF] Project not found: %PROJECT%
    pause
    exit /b 1
)

echo [PhotoViewer WPF] Launching native WPF viewer...
echo.
dotnet run --project "%PROJECT%" %*
set "EXIT_CODE=%ERRORLEVEL%"

if not "%EXIT_CODE%"=="0" (
    echo.
    echo [PhotoViewer WPF] Exited with code %EXIT_CODE%.
)
pause
exit /b %EXIT_CODE%
