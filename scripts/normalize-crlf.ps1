$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $root

$textExt = @(
  '.md', '.ps1', '.cs', '.bat', '.cmd', '.ini', '.txt', '.json', '.xml',
  '.conf', '.py', '.java', '.cpp', '.h', '.hpp', '.sln', '.vcxproj',
  '.yml', '.yaml', '.csv', '.sample', '.prp'
)
$plainNames = @('LICENSE', '.gitignore', '.gitattributes')

$files = Get-ChildItem -LiteralPath $root -Recurse -File -Force |
  ForEach-Object { [IO.Path]::GetRelativePath($root, $_.FullName).Replace('\', '/') } |
  Where-Object { $_ -notlike '.git/*' -and $_ -notlike 'Games/*/backup/*' } |
  Sort-Object -Unique

foreach ($rel in $files) {
  $ext = [IO.Path]::GetExtension($rel).ToLowerInvariant()
  $isText = ($textExt -contains $ext) -or ($plainNames -contains $rel) -or
    ($rel -like 'Games/RoadRash/src/*.reg')
  if (-not $isText) { continue }

  $full = Join-Path $root $rel
  $bytes = [IO.File]::ReadAllBytes($full)
  if ($bytes.Length -eq 0) { continue }
  if ($bytes.Length -ge 2 -and (($bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) -or
      ($bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF))) { continue }

  $out = New-Object System.Collections.Generic.List[byte]
  $changed = $false
  for ($i = 0; $i -lt $bytes.Length; $i++) {
    $b = $bytes[$i]
    if ($b -eq 13) {
      $out.Add(13)
      if ($i + 1 -lt $bytes.Length -and $bytes[$i + 1] -eq 10) { $i++ }
      else { $out.Add(10); $changed = $true }
    }
    elseif ($b -eq 10) {
      $out.Add(13); $out.Add(10); $changed = $true
    }
    else { $out.Add($b) }
  }

  if ($changed) {
    [IO.File]::WriteAllBytes($full, $out.ToArray())
    Write-Output $rel
  }
}
