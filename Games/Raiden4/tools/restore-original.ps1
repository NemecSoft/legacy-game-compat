# 恢复原版基线：TP 全家与适配产物整体移入 _prev-20261119（不删除），
# 从原始 zip 恢复 teknoparrot.ini；用户键位绑定与 hiscore 存档随移入目录保留并另抄一份到仓库 evidence。
$ErrorActionPreference = 'Stop'
$g = 'D:\YunGame\G\Raiden4'
$prev = Join-Path $g '_prev-20261119'
$ev = 'D:\AI\legacy-game-compat\Games\Raiden4\evidence'
$zip = 'D:\BaiduNetdiskDownload\Raiden4 雷电4汉化版.zip'
$za = 'D:\Temp\nj\node_modules\7zip-bin\win\x64\7za.exe'

# 只允许清我自己开的 UI；有游戏局在跑就停手
if (@(Get-Process -Name game,game_starter -ErrorAction SilentlyContinue).Count -gt 0) {
  Write-Output '有游戏局或启动器在跑，停手不恢复'; exit 1
}
Get-Process -Name TeknoParrotUi -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

New-Item -ItemType Directory -Force -Path $prev | Out-Null
$items = @(
  'TeknoParrotUi.exe','TeknoParrotUi.exe.config','GameProfiles','GameDiscovery','GameSetup',
  'Metadata','Metadata.catalog.json','Icons','ParrotPatcher.exe','ParrotPatcher.exe.config',
  'ParrotPatcher.pdb','Newtonsoft.Json.xml','Utf8Json.dll','libs','OpenParrotWin32',
  'ParrotData.xml','UserProfiles','TeknoParrot','HookedWindows.txt',
  'game_starter.exe','launcher.ini','readings.txt','jjread.txt','exception.txt','运行日志.txt',
  '_dgv','_兼容备份','teknoparrot.ini','JJ11.ini.sjis-bak'
)
$moved = @(); $miss = @()
foreach ($it in $items) {
  $src = Join-Path $g $it
  if (Test-Path $src) { Move-Item -LiteralPath $src -Destination (Join-Path $prev $it) -Force; $moved += $it }
  else { $miss += $it }
}
Write-Output ('已移入 _prev：' + ($moved -join ', '))
if ($miss.Count -gt 0) { Write-Output ('本来就不在：' + ($miss -join ', ')) }

# 原版 teknoparrot.ini 从 zip 平铺解出
& $za e -r -o"$g" -y "$zip" 'teknoparrot.ini' | Out-Null
if (-not (Test-Path (Join-Path $g 'teknoparrot.ini'))) { Write-Output 'zip 解出 teknoparrot.ini 失败'; exit 1 }

# 用户数据双保险
New-Item -ItemType Directory -Force -Path $ev | Out-Null
Copy-Item (Join-Path $prev 'UserProfiles\RaidenIVNesica.xml') (Join-Path $ev 'userdata-bindings.xml') -Force
Copy-Item (Join-Path $prev 'TeknoParrot\hiscore.dat') (Join-Path $ev 'userdata-hiscore.dat') -Force
Copy-Item (Join-Path $prev 'TeknoParrot\income.log') (Join-Path $ev 'userdata-income.log') -Force

Write-Output '==== 恢复后顶层 ===='
Get-ChildItem $g | ForEach-Object { Write-Output $_.Name }
Write-Output '恢复完成'