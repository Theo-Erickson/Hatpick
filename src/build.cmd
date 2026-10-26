@echo off
rem Builds ..\Hatpick.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
rem Close Hatpick before rebuilding, or the .exe can't be overwritten.
setlocal
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /optimize+ /out:..\Hatpick.exe /win32icon:..\hatpick.ico ^
  /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll ^
  *.cs
if errorlevel 1 (echo Build failed.& exit /b 1)
echo Built ..\Hatpick.exe
