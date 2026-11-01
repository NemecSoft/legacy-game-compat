Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Vis {
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
}
"@

$nircmdc = 'D:\YunGame\Race\RoadRash\_tools\nircmd\nircmdc.exe'
$p = Start-Process "$env:SystemRoot\System32\notepad.exe" -PassThru
Start-Sleep -Seconds 2
$p.Refresh()
$title = $p.MainWindowTitle
"before : title=[$title] visible=$([Vis]::IsWindowVisible($p.MainWindowHandle))"

& $nircmdc win hide ititle $title | Out-Null
"nircmdc exit=$LASTEXITCODE"
Start-Sleep -Milliseconds 900
"after  : visible=$([Vis]::IsWindowVisible($p.MainWindowHandle))"

& $nircmdc win show ititle $title | Out-Null
Start-Sleep -Milliseconds 400
"restore: visible=$([Vis]::IsWindowVisible($p.MainWindowHandle))"
Stop-Process -Id $p.Id -Force
