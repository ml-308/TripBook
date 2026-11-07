@echo off
rem ==========================================================
rem  TripBook - build script
rem  Requires .NET SDK 6.0+ (targets .NET Framework 4.8, built into Windows)
rem  Output: dist\TripBook.exe (single file, dependencies embedded)
rem ==========================================================
setlocal
cd /d "%~dp0"

set "DOTNET=dotnet"
where dotnet >nul 2>nul
if errorlevel 1 if exist "%ProgramFiles%\dotnet\dotnet.exe" set "DOTNET=%ProgramFiles%\dotnet\dotnet.exe"

"%DOTNET%" --version >nul 2>nul
if errorlevel 1 (
  echo [ERROR] .NET SDK not found. Download: https://dotnet.microsoft.com/download
  exit /b 1
)

echo [1/3] Running parser tests...
pushd tests
"%DOTNET%" run -c Release
if errorlevel 1 (
  echo [ERROR] tests failed, build aborted
  popd
  exit /b 1
)
popd

echo [2/3] Building Release...
"%DOTNET%" build -c Release
if errorlevel 1 exit /b 1

echo [3/3] Copying artifact...
if not exist dist mkdir dist
copy /y "bin\Release\net48\TripBook.exe" "dist\TripBook.exe" >nul
if errorlevel 1 exit /b 1

echo.
echo Done: %CD%\dist\TripBook.exe
endlocal
