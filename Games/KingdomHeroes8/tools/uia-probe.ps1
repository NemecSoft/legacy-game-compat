param([int]$Pid2 = 0)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
if ($Pid2 -le 0) {
  $p = Get-Process -Name SG8Launcher -ErrorAction SilentlyContinue | Select-Object -First 1
  if (-not $p) { throw 'NO SG8Launcher RUNNING' }
  $Pid2 = $p.Id
}
$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $Pid2)
$win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
if ($null -eq $win) { throw "no UIA window for pid=$Pid2" }
Write-Output ("WINDOW: " + $win.Current.Name + " class=" + $win.Current.ClassName)
$all = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants,
  [System.Windows.Automation.Condition]::TrueCondition)
Write-Output ("descendants: " + $all.Count)
foreach ($e in $all) {
  $c = $e.Current
  $patterns = ''
  try {
    foreach ($ap in $e.GetSupportedPatterns()) { $patterns += $ap.ProgrammaticName + ' ' }
  } catch { }
  if ($c.ControlType.ProgrammaticName -match 'Button|Edit|Text|Hyperlink|List|Custom|Pane' -or $c.Name.Length -gt 0) {
    Write-Output ('  {0,-14} name="{1}" rect={2} patterns={3}' -f `
        ($c.ControlType.ProgrammaticName -replace 'ControlType.', ''), $c.Name, $c.BoundingRectangle, $patterns)
  }
}
