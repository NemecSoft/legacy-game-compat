# 探针：D3D9 本机状态 + 驱动重置事件 + Vulkan 枚举（一次跑完）
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [Text.Encoding]::UTF8

Write-Output '===== 1. D3D9 探针（32 位，游戏同进程位数）====='
& 'D:\AI\legacy-game-compat\scripts\D3D9Probe32.exe' 2>&1 | Select-Object -Last 15

Write-Output '===== 2. 最近 60 分钟 System 日志里的显示驱动重置/错误 ====='
$since = (Get-Date).AddMinutes(-60)
Get-WinEvent -FilterHashtable @{LogName='System'; StartTime=$since} -ErrorAction SilentlyContinue |
  Where-Object { $_.Id -in 4101,4113,141,153 -or $_.ProviderName -match 'amd|Display|dxgkrnl' } |
  Select-Object -First 8 | ForEach-Object { Write-Output ($_.TimeCreated.ToString('HH:mm:ss') + " [" + $_.Id + "] " + $_.ProviderName + " " + ($_.Message -split "`n")[0]) }

Write-Output '===== 3. Vulkan 运行时（vulkaninfo 摘要）====='
$vi = Get-Command vulkaninfo -ErrorAction SilentlyContinue
if ($vi) { vulkaninfo --summary 2>&1 | Select-String 'GPU|driver|apiVersion' | Select-Object -First 10 } else {
  $vk = 'C:\Windows\System32\vulkan-1.dll'
  if (Test-Path $vk) { Write-Output ('vulkan-1.dll 存在: ' + (Get-Item $vk).Length + ' 字节') } else { Write-Output 'vulkan-1.dll 不存在' }
}

Write-Output '===== 4. 显卡当前状态（PnP）====='
Get-PnpDevice -Class Display -Status OK -ErrorAction SilentlyContinue | ForEach-Object { Write-Output ($_.FriendlyName + " [" + $_.Status + "]") }
Get-PnpDevice -Class Display -ErrorAction SilentlyContinue | Where-Object { $_.Status -ne 'OK' } | ForEach-Object { Write-Output ("异常设备: " + $_.FriendlyName + " [" + $_.Status + "]") }
