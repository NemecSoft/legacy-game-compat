$d = 'D:\YunGame\Race\RoadRash'
Copy-Item (Join-Path $d '存档-Milwaukee Jon(荘臣)-5级.rrs') (Join-Path $d 'probe.rrs') -Force
& D:/Tools/re-toolkit/scripts/etw_run.ps1 -Exe (Join-Path $d 'Roadrash.exe') -WorkDir $d -Args @('probe.rrs','-load','/load') -Seconds 12 -Trace rrscmd
Remove-Item (Join-Path $d 'probe.rrs') -Force
