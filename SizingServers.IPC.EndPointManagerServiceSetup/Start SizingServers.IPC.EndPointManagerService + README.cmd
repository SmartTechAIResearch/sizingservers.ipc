@ECHO OFF
ECHO If this script fails, run with elevated rights.
ECHO The default tcp port the service will listen on is port 4455.
ECHO Set your own port in the service Start parameters. Password and salt are optional: encryption of traffic to and from the service.
ECHO The given salt is a byte array representation. Alternatively you can use an ssh tunnel, that will probably be safer and faster.
ECHO.
ECHO Examples:
ECHO.
ECHO    sc start SizingServers.IPC.EndPointManagerService
ECHO    sc start SizingServers.IPC.EndPointManagerService 4567
ECHO    sc start SizingServers.IPC.EndPointManagerService 4567 password {0x01,0x02,0x03}
ECHO.
ECHO.
ECHO Don't forget to add a firewall exception for the inbound tcp port.
ECHO Status messages and errors are logged to the Windows Event Log (Event Viewer).
ECHO.
@ECHO ON

sc start SizingServers.IPC.EndPointManagerService
REM sc start SizingServers.IPC.EndPointManagerService 4567
REM sc start SizingServers.IPC.EndPointManagerService 4567 password {0x01,0x02,0x03}

@ECHO OFF
PAUSE