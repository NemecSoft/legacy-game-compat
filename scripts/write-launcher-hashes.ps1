$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$dirs = @{
  CS16          = 'D:\YunGame\CS\CS1.6'
  DFBHD         = 'D:\YunGame\II\DFBHD'
  FutureSoldier = 'D:\YunGame\V\Future Soldier'
  Heroes3       = 'D:\YunGame\E\Heroes3'
  JustCause     = 'D:\YunGame\Z\JustCause1'
  Prototype1    = 'D:\YunGame\Prototype\Prototype'
  Prototype2    = 'D:\YunGame\Prototype\Prototype.2'
  RoadRash      = 'D:\YunGame\Race\RoadRash'
  TheSaboteur   = 'D:\YunGame\2023\TheSaboteur'
  FiveStar      = 'D:\YunGame\Command.and.Conquer\Command.and.Conquer.FiveStar'
}

foreach ($game in $dirs.Keys) {
  $exe = Join-Path $dirs[$game] 'game_starter.exe'
  if (-not (Test-Path $exe)) { Write-Host "$game : no game_starter.exe"; continue }
  $hash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()
  $line = "$hash  game_starter.exe`r`n"
  [IO.File]::WriteAllText((Join-Path $dirs[$game] 'game_starter.sha256'), $line, [Text.Encoding]::ASCII)
  $out = Join-Path $root "Games\$game\out"
  if (Test-Path $out) { [IO.File]::WriteAllText((Join-Path $out 'game_starter.sha256'), $line, [Text.Encoding]::ASCII) }
  Write-Host ("{0} : {1}" -f $game, $hash)
}
