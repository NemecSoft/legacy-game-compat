param(
  [Parameter(Mandatory = $true)][string]$Exe,
  [string]$SrcDir,
  [string]$GameDir,
  [int]$MaxKB = 60
)

# 启动器交付前检查：体积红线 + 不许出现的资源/源码文件
$fail = 0

if (-not (Test-Path $Exe)) {
  Write-Output "FAIL 找不到 $Exe"
  exit 1
}
$fi = Get-Item $Exe
$kb = [Math]::Round($fi.Length / 1KB, 1)
if ($kb -gt $MaxKB) {
  Write-Output "FAIL $($_.Name) 体积 ${kb}KB > 红线 ${MaxKB}KB（查内嵌图片/字体/图标/资源）"
  $fail = 1
} else {
  Write-Output "OK   体积 ${kb}KB <= ${MaxKB}KB  ($($fi.Name))"
}

$bad = @('*.png', '*.jpg', '*.jpeg', '*.gif', '*.bmp', '*.ico', '*.resx', '*.Designer.cs', '*.csproj', '*.sln')
foreach ($dir in @($SrcDir, $GameDir)) {
  if (-not $dir -or -not (Test-Path $dir)) { continue }
  $hits = @()
  foreach ($pat in $bad) { $hits += Get-ChildItem $dir -Filter $pat -Recurse -ErrorAction SilentlyContinue }
  if ($dir -eq $GameDir) { $hits += Get-ChildItem $dir -Filter '*.cs' -Recurse -ErrorAction SilentlyContinue }
  if ($hits.Count -gt 0) {
    Write-Output "FAIL $dir 出现禁止文件：$(($hits | ForEach-Object { $_.Name }) -join ', ')"
    $fail = 1
  } else {
    Write-Output "OK   $dir 无图片/资源/源码文件"
  }
}

if ($fail -eq 0) { Write-Output 'PASS 启动器体积与文件规范检查通过' } else { Write-Output 'FAIL 见上面条目逐条修' }
exit $fail
