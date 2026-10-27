@echo off
rem Builds ..\Hatpick.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
rem Close Hatpick before rebuilding, or the .exe can't be overwritten.
setlocal
cd /d "%~dp0"
set FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
"%FW%\csc.exe" /nologo /target:winexe /optimize+ /out:..\Hatpick.exe /win32icon:..\hatpick.ico ^
  /lib:"%FW%\WPF" ^
  /r:PresentationFramework.dll /r:PresentationCore.dll /r:WindowsBase.dll /r:System.Xaml.dll ^
  /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Core.dll /r:System.Xml.dll ^
  *.cs
if errorlevel 1 (echo Build failed.& exit /b 1)
echo Built ..\Hatpick.exe
