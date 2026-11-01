$d = 'D:\YunGame\Race\RoadRash'
$save = Join-Path $d '存档-Milwaukee Jon(荘臣)-5级.rrs'
Copy-Item $save (Join-Path $d 'probe.rrs') -Force
& D:/Tools/re-toolkit/scripts/etw_run.ps1 -Exe (Join-Path $d 'Roadrash.exe') -WorkDir $d -Args @('probe.rrs') -Seconds 15 -Trace rrsonly
Remove-Item (Join-Path $d 'probe.rrs') -Force
