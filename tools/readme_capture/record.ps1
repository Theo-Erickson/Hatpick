<#
Records Hatpick's demo mode into PNG frames, for making README GIFs.

    powershell -ExecutionPolicy Bypass -File tools\readme_capture\record.ps1 -Name tour -Script tour -Seconds 24

It starts "Hatpick.exe --demo --autoplay <Script>" (made-up accounts and repos in
%TEMP%\Hatpick demo, nothing real is touched), waits for the window, then screenshots it
-Fps times a second into .work\<Name>\frames\f00000000.png, f00000001.png, ...
Frames are numbered by the clock, so a slow frame is repeated rather than the clip
speeding up. Hatpick is closed when recording ends.

Scripts: tour (everything), overview, repos, hats, fix.
Keep the Hatpick window uncovered while it records (it's captured from the screen).
#>
param(
    [string]$Name = "tour",
    [string]$Script = "tour",
    [double]$Seconds = 24,
    [int]$Fps = 15,
    [double]$Warmup = 2.5,       # seconds to wait after the window appears (first scan)
    [switch]$NoTitleBar          # crop off the title bar
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    public static RECT Bounds(IntPtr h) { RECT r; DwmGetWindowAttribute(h, 9, out r, Marshal.SizeOf(typeof(RECT))); return r; }
}
"@
[Win]::SetProcessDPIAware() | Out-Null

$repo = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$exe = Join-Path $repo "Hatpick.exe"
if (-not (Test-Path $exe)) { throw "Build Hatpick first (build.cmd): $exe not found" }

$out = Join-Path $repo ".work\$Name\frames"
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force $out | Out-Null

$p = Start-Process $exe -ArgumentList "--demo", "--autoplay", $Script -PassThru
try {
    $deadline = (Get-Date).AddSeconds(30)
    while ($p.MainWindowHandle -eq 0) {
        if ((Get-Date) -gt $deadline) { throw "Hatpick's window didn't appear" }
        Start-Sleep -Milliseconds 200; $p.Refresh()
    }
    $h = $p.MainWindowHandle
    [Win]::SetForegroundWindow($h) | Out-Null
    Start-Sleep -Seconds $Warmup

    $r = [Win]::Bounds($h)
    $scale = ([System.Drawing.Graphics]::FromHwnd($h)).DpiY / 96
    $top = $r.Top; if ($NoTitleBar) { $top += [int][Math]::Round(31 * $scale) }
    $w = $r.Right - $r.Left; $ht = $r.Bottom - $top
    Write-Host "Recording $w x $ht at $Fps fps for $Seconds s -> $out"

    $bmp = New-Object System.Drawing.Bitmap $w, $ht
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $total = [int]($Seconds * $Fps)
    $clock = [System.Diagnostics.Stopwatch]::StartNew()
    $last = -1; $lastFile = $null
    while ($last + 1 -lt $total) {
        $n = [Math]::Min($total - 1, [int][Math]::Floor($clock.Elapsed.TotalSeconds * $Fps))
        if ($n -le $last) { Start-Sleep -Milliseconds 5; continue }
        $g.CopyFromScreen($r.Left, $top, 0, 0, $bmp.Size)
        $file = Join-Path $out ("f{0:D8}.png" -f $n)
        $bmp.Save($file, [System.Drawing.Imaging.ImageFormat]::Png)
        # fill frames skipped while saving with the previous picture
        $src = if ($lastFile) { $lastFile } else { $file }
        for ($k = $last + 1; $k -lt $n; $k++) { Copy-Item $src (Join-Path $out ("f{0:D8}.png" -f $k)) }
        $last = $n; $lastFile = $file
    }
    $g.Dispose(); $bmp.Dispose()
    Write-Host "Saved $total frames."
}
finally {
    if (-not $p.HasExited) { $p.CloseMainWindow() | Out-Null; Start-Sleep -Milliseconds 800; if (-not $p.HasExited) { $p.Kill() } }
}
