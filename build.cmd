@echo off
rem Builds Hatpick.exe here: one file, nothing to install. Needs the .NET 10 SDK (dot.net).
rem Close Hatpick before rebuilding, or the .exe can't be overwritten.
rem   build.cmd           Windows x64
rem   build.cmd win-arm64 Windows on ARM
setlocal
cd /d "%~dp0"
set RID=%1
if "%RID%"=="" set RID=win-x64
dotnet publish src\Hatpick.csproj -c Release -r %RID% -o dist\%RID% -nologo
if errorlevel 1 (echo Build failed.& exit /b 1)
copy /y dist\%RID%\Hatpick.exe Hatpick.exe >nul
if errorlevel 1 (echo Couldn't copy Hatpick.exe - is Hatpick still open?& exit /b 1)
echo Built Hatpick.exe
