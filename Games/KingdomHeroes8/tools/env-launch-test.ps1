param(
  [string]$Root = 'D:\YunGame\F\KingdomHeroes8',
  [int]$Rounds = 25,
  [string]$Shot = '',
  [switch]$NoSteam   # 只带环境变量启动游戏，不拉便携 Steam
)
$ErrorActionPreference = 'Stop'
function Hex([int]$c) { return '0x' + ('{0:X8}' -f ($c -band 0x7FFFFFFF)) + ' (' + $c + ')' }

$env2 = @{
  SG8_MINIPIPE_DIR          = Join-Path $Root 'Runtime\logs'
  SG8_STEAM_RUNTIME         = Join-Path $Root 'Runtime\steam'
  STEAMPATH                 = Join-Path $Root 'Runtime\steam'
  STEAMAPPID                = '875210'
  STEAMGAMEID               = '875210'
  STEAMOVERLAYGAMEID        = '875210'
  STEAMAPPUSER              = 'offline'
  STEAMUSER                 = 'offline'
  STEAMID                   = '76561198747582902'
  STEAMCLIENTLAUNCH         = '1'
  STEAMENV                  = '1'
  STEAM_MASTER_IPC_NAME_OVERRIDE = 'SG8envtest1'
}
foreach ($k in $env2.Keys) { Set-Item -Path ("Env:" + $k) -Value $env2[$k] }

$steam = $null
if (-not $NoSteam) {
  $steam = Start-Process -FilePath (Join-Path $Root 'Runtime\steam\steam.exe') -ArgumentList '-silent' `
    -WorkingDirectory (Join-Path $Root 'Runtime\steam') -PassThru
  Write-Output "steam.exe pid=$($steam.Id) -silent（便携 Steam，只带环境变量）"
  Start-Sleep -Seconds 4
  if ($steam.HasExited) { Write-Output ("  steam.exe 已退出 " + (Hex $steam.ExitCode)) }
}

$g = Start-Process -FilePath (Join-Path $Root 'GAME\SG8.exe') -WorkingDirectory (Join-Path $Root 'GAME') -PassThru
Write-Output "SG8.exe pid=$($g.Id)"
for ($i = 0; $i -lt $Rounds; $i++) {
  Start-Sleep -Seconds 2
  if ($g.HasExited) { Write-Output ("  游戏退出 " + (Hex $g.ExitCode) + "，用时约 " + (($i + 1) * 2) + "s"); break }
}
if (-not $g.HasExited) {
  Write-Output ("  游戏仍在运行（" + ($Rounds * 2) + "s）")
  if ($Shot -ne '') {
    & (Join-Path $PSScriptRoot 'win-shot.ps1') -Pid2 $g.Id -Shot $Shot | Out-Null
    Write-Output ("SHOT " + $Shot)
  }
  Stop-Process -Id $g.Id -Force -ErrorAction SilentlyContinue
  Write-Output '  已测试，关掉游戏'
}
if ($steam -and -not $steam.HasExited) {
  Stop-Process -Id $steam.Id -Force -ErrorAction SilentlyContinue
  Write-Output '  已关掉便携 Steam'
}
Get-ChildItem Env: | Where-Object { $_.Name -like 'SG8_*' -or $_.Name -like 'STEAM*' } |
  ForEach-Object { Remove-Item ("Env:" + $_.Name) -ErrorAction SilentlyContinue }
