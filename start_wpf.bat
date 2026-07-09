@echo off
setlocal
cd /d "%~dp0"

set "PROJECT=local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj"
set "CONFIG=Release"
set "TARGET=local-native\PhotoViewer.Wpf\bin\%CONFIG%\net8.0-windows\PhotoViewer.Wpf.exe"

if not exist "%PROJECT%" (
    echo [PhotoViewer WPF] Project not found: %PROJECT%
    pause
    exit /b 1
)

if /I "%PHOTOVIEWER_WPF_DOTNET_RUN%"=="1" (
    echo [PhotoViewer WPF] Launching via dotnet run for development...
    echo.
    dotnet run --project "%PROJECT%" -- %*
    set "EXIT_CODE=%ERRORLEVEL%"
    goto exit_with_code
)

if /I "%PHOTOVIEWER_WPF_REBUILD%"=="1" (
    call :build_target
    if not "%ERRORLEVEL%"=="0" exit /b %ERRORLEVEL%
)

if not exist "%TARGET%" (
    call :build_target
    if not "%ERRORLEVEL%"=="0" exit /b %ERRORLEVEL%
)

echo [PhotoViewer WPF] Launching direct %CONFIG% executable...
echo.
"%TARGET%" %*
set "EXIT_CODE=%ERRORLEVEL%"

:exit_with_code
if not "%EXIT_CODE%"=="0" (
    echo.
    echo [PhotoViewer WPF] Exited with code %EXIT_CODE%.
)
pause
exit /b %EXIT_CODE%

:build_target
echo [PhotoViewer WPF] Building direct %CONFIG% launch target...
dotnet build "%PROJECT%" -c %CONFIG% --nologo
exit /b %ERRORLEVEL%
