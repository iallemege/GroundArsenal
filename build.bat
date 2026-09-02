@echo off
setlocal EnableExtensions
set GAME=d:\Steam\steamapps\common\Nuclear Option
set MANAGED=%GAME%\NuclearOption_Data\Managed
set BEP=%GAME%\BepInEx\core
set ROOT=%~dp0
set PLUGINS=%GAME%\BepInEx\plugins
set DIST=%ROOT%..\dist
set OUT=%ROOT%GroundArsenal.dll
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC%" (
  echo csc.exe not found
  exit /b 1
)
if not exist "%MANAGED%\Assembly-CSharp.dll" (
  echo Assembly-CSharp.dll not found
  exit /b 1
)

"%CSC%" /noconfig /nostdlib /nologo /optimize+ /target:library /platform:anycpu /langversion:5 /out:"%OUT%" ^
  /r:"%MANAGED%\mscorlib.dll" ^
  /r:"%MANAGED%\netstandard.dll" ^
  /r:"%MANAGED%\System.dll" ^
  /r:"%MANAGED%\System.Core.dll" ^
  /r:"%BEP%\BepInEx.dll" ^
  /r:"%BEP%\0Harmony.dll" ^
  /r:"%MANAGED%\Assembly-CSharp.dll" ^
  /r:"%MANAGED%\Mirage.dll" ^
  /r:"%MANAGED%\UnityEngine.CoreModule.dll" ^
  /r:"%MANAGED%\UnityEngine.dll" ^
  /r:"%MANAGED%\UnityEngine.PhysicsModule.dll" ^
  /r:"%MANAGED%\UnityEngine.IMGUIModule.dll" ^
  /r:"%MANAGED%\UnityEngine.TextRenderingModule.dll" ^
  /r:"%MANAGED%\UnityEngine.InputLegacyModule.dll" ^
  /r:"%MANAGED%\UnityEngine.ImageConversionModule.dll" ^
  /r:"%MANAGED%\UnityEngine.ParticleSystemModule.dll" ^
  "%ROOT%src\GaPlugin.cs" ^
  "%ROOT%src\GaCatalog.cs" ^
  "%ROOT%src\GaMode.cs" ^
  "%ROOT%src\GaCombat.cs" ^
  "%ROOT%src\GaHud.cs" ^
  "%ROOT%src\GaHangar.cs" ^
  "%ROOT%src\GaHarmony.cs" ^
  "%ROOT%src\GaObj.cs" ^
  "%ROOT%src\GaFx.cs" ^
  "%ROOT%src\GaOritasy.cs" ^
  "%ROOT%src\GaKit.cs" ^
  "%ROOT%src\GaView.cs" ^
  "%ROOT%src\GaPawn.cs" ^
  "%ROOT%src\GaArmorMath.cs" ^
  "%ROOT%src\GaArmorCatalog.cs" ^
  "%ROOT%src\GaIcons.cs" ^
  "%ROOT%src\GaVehicleArmor.cs" ^
  "%ROOT%src\GaConfig.cs" ^
  "%ROOT%src\GaFunds.cs" ^
  "%ROOT%src\GaLoot.cs" ^
  "%ROOT%src\GaRadar.cs"

if errorlevel 1 (
  echo GROUND ARSENAL BUILD FAILED
  exit /b 1
)

if not exist "%DIST%" mkdir "%DIST%"
copy /Y "%OUT%" "%DIST%\GroundArsenal.dll" >nul
if not exist "%PLUGINS%\GroundArsenal" mkdir "%PLUGINS%\GroundArsenal"
copy /Y "%ROOT%assets\Weapons\*" "%PLUGINS%\GroundArsenal\" >nul 2>nul
if exist "%PLUGINS%\GroundArsenal.dll" del /f /q "%PLUGINS%\GroundArsenal.dll" 2>nul
copy /Y "%OUT%" "%PLUGINS%\GroundArsenal.dll"
echo Installed: %PLUGINS%\GroundArsenal.dll
endlocal
