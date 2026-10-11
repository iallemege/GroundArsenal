@echo off
setlocal EnableExtensions
set GAME=d:\Steam\steamapps\common\Nuclear Option
set MANAGED=%GAME%\NuclearOption_Data\Managed
set BEP=%GAME%\BepInEx\core
set ROOT=%~dp0
set PLUGINS=%GAME%\BepInEx\plugins
set DIST=%ROOT%..\dist
set OUT=%ROOT%GroundArsenal.dll
set CSC=%~dp0..\_csc9.cmd

if not exist "%CSC%" (
  echo csc.exe not found
  exit /b 1
)
if not exist "%MANAGED%\Assembly-CSharp.dll" (
  echo Assembly-CSharp.dll not found
  exit /b 1
)

call "%CSC%" /noconfig /nostdlib /nologo /optimize+ /unsafe /target:library /platform:anycpu /langversion:10 /out:"%OUT%" ^
  /r:"%MANAGED%\mscorlib.dll" ^
  /r:"%MANAGED%\netstandard.dll" ^
  /r:"%MANAGED%\System.dll" ^
  /r:"%MANAGED%\System.Core.dll" ^
  /r:"%BEP%\BepInEx.dll" ^
  /r:"%BEP%\0Harmony.dll" ^
  /r:"%MANAGED%\Assembly-CSharp.dll" ^
  /r:"%MANAGED%\Mirage.dll" ^
  /r:"%MANAGED%\Mirage.SocketLayer.dll" ^
  /r:"%MANAGED%\UnityEngine.CoreModule.dll" ^
  /r:"%MANAGED%\UnityEngine.dll" ^
  /r:"%MANAGED%\UnityEngine.PhysicsModule.dll" ^
  /r:"%MANAGED%\UnityEngine.IMGUIModule.dll" ^
  /r:"%MANAGED%\UnityEngine.TextRenderingModule.dll" ^
  /r:"%MANAGED%\UnityEngine.AnimationModule.dll" ^
  /r:"%MANAGED%\UnityEngine.UI.dll" ^
  /r:"%MANAGED%\UnityEngine.UIModule.dll" ^
  /r:"%MANAGED%\Unity.TextMeshPro.dll" ^
  /r:"%MANAGED%\UnityEngine.InputLegacyModule.dll" ^
  /r:"%MANAGED%\UnityEngine.ImageConversionModule.dll" ^
  /r:"%MANAGED%\UnityEngine.ParticleSystemModule.dll" ^
  /r:"%MANAGED%\UnityEngine.XRModule.dll" ^
  /r:"%MANAGED%\UnityEngine.VRModule.dll" ^
  /r:"%MANAGED%\Unity.InputSystem.dll" ^
  /r:"%MANAGED%\UnityEngine.SubsystemsModule.dll" ^
  /r:"%MANAGED%\Rewired_Core.dll" ^
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
  "%ROOT%src\GaRadar.cs" ^
  "%ROOT%src\GaForeignGuard.cs" ^
  "%ROOT%src\GaPerfFrameGate.cs" ^
  "%ROOT%src\GaMtPureMath.cs" ^
  "%ROOT%src\GaVr.cs" ^
  "%ROOT%src\GaRifleDrop.cs" ^
  "%ROOT%src\GaPalmMenu.cs" ^
  "%ROOT%src\GaControllerPointer.cs" ^
  "%ROOT%src\GaVtol.cs" ^
  "%ROOT%src\VrHandoff.cs" ^
  "%ROOT%src\GaChute.cs" ^
  "%ROOT%src\GaPhys.cs" ^
  "%ROOT%src\GaDropBot.cs" ^
  "%ROOT%src\GaDropSquad.cs" ^
  "%ROOT%src\GaForce.cs" ^
  "%ROOT%src\GaClass.cs" ^
  "%ROOT%src\GaDeploy.cs" ^
  "%ROOT%src\GaRide.cs" ^
  "%ROOT%src\GaIff.cs" ^
  "%ROOT%src\GaShop.cs" ^
  "%ROOT%src\GaGarage.cs" ^
  "%ROOT%src\GaRadio.cs" ^
  "%ROOT%src\GaFps.cs" ^
  "%ROOT%src\GaBody.cs" ^
  "%ROOT%src\GaNet.cs" ^
  "%ROOT%src\GaSpot.cs" ^
  "%ROOT%src\GaHotensia.cs" ^
  "%ROOT%src\GaAirLift.cs" ^
  "%ROOT%src\GaJet.cs" ^
  "%ROOT%src\GaTsf.cs" ^
  "%ROOT%src\GaSupply.cs" ^
  "%ROOT%src\GaSupport.cs" ^
  "%ROOT%src\GaAtLaser.cs" ^
  "%ROOT%src\GaSpawnUi.cs" ^
  "%ROOT%src\GaFxUi.cs"

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
