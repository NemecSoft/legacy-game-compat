#NoTrayIcon
#RequireAdmin
#Region AutoIt3Wrapper 预编译参数(常用参数)
#AutoIt3Wrapper_Icon= 	1.ico
#AutoIt3Wrapper_OutFile=reg.fix.exe
#AutoIt3Wrapper_OutFile_Type=exe
#AutoIt3Wrapper_Compression=4
#AutoIt3Wrapper_UseUpx=y
#AutoIt3Wrapper_UseX64=n
#AutoIt3Wrapper_Change2CUI=N
#EndRegion ACN预处理程序参数设置完成
RegWrite ( "HKEY_LOCAL_MACHINE\SOFTWARE\Eidos\Just Cause" )
RegWrite ( "HKEY_LOCAL_MACHINE\SOFTWARE\Eidos\Just Cause" , "Language" , "REG_DWORD" , "0x00000409" )
RegWrite ( "HKEY_LOCAL_MACHINE\SOFTWARE\Eidos\Just Cause" , "InstallPath" , "REG_SZ" , @ScriptDir )
RegWrite ( "HKEY_LOCAL_MACHINE\SOFTWARE\Eidos\Just Cause\1.00.0000" )
Exit
