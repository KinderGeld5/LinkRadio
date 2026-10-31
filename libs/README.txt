Hier hinein die Referenz-DLLs kopieren (werden nicht mitgeliefert, gehoeren dem Spiel / BepInEx):

Aus  <Spielordner>\<Spiel>_Data\Managed\ :
  Mirror.dll
  Unity.InputSystem.dll

Aus  <Spielordner>\unstripped_libs\ (NICHT aus Managed: dort fehlen GUIStyle-Teile, die Ui/Theme brauchen.
Zur Laufzeit laedt Doorstop diese Fassung, siehe dll_search_path_override in doorstop_config.ini) :
  UnityEngine.dll
  UnityEngine.CoreModule.dll
  UnityEngine.IMGUIModule.dll
  UnityEngine.AudioModule.dll
  UnityEngine.UnityWebRequestModule.dll
  UnityEngine.UnityWebRequestAudioModule.dll
  UnityEngine.InputLegacyModule.dll
  UnityEngine.TextRenderingModule.dll

Aus  <Spielordner>\BepInEx\core\ (BepInExPack_NomadDrive, BepInEx 5.4.23.2) :
  BepInEx.dll

Alternativ: build.sh / build.bat mit gesetzten Variablen MANAGED und BEPINEX_CORE auf die Originalordner aufrufen.
