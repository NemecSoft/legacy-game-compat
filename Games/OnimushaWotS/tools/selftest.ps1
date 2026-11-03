$exe = 'D:\YunGame\F\OnimushaWotS\game_starter.exe'
$p = Start-Process -FilePath $exe -ArgumentList @('/selftest', 'C:\Windows\System32\cmd.exe', '/c', 'exit', '7') -Wait -PassThru
Write-Output ('selftest_exit=' + $p.ExitCode)
