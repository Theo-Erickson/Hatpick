@echo off
rem Builds ..\GitPicker.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
rem Close GitPicker before rebuilding, or the .exe can't be overwritten.
setlocal
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /optimize+ /out:..\GitPicker.exe /win32icon:..\gitpicker.ico ^
  /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll ^
  GitPicker.cs
if errorlevel 1 (echo Build failed.& exit /b 1)
echo Built ..\GitPicker.exe
