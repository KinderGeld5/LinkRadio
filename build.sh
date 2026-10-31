#!/usr/bin/env bash
# Getestete Build-Variante (Linux/macOS/WSL): Mono-Compiler "mcs" (apt install mono-mcs).
# Aufruf:  ./build.sh                       (nutzt ./libs)
#          MANAGED=/pfad/Managed BEPINEX_CORE=/pfad/BepInEx/core ./build.sh
set -euo pipefail
cd "$(dirname "$0")"
M="${MANAGED:-libs}"
B="${BEPINEX_CORE:-libs}"
mkdir -p bin
mcs -target:library -debug- -optimize+ -codepage:utf8 -out:bin/LinkRadio.dll -nowarn:618,612,168,219 \
  -r:"$B/BepInEx.dll" -r:"$M/Mirror.dll" -r:"$M/UnityEngine.dll" -r:"$M/UnityEngine.CoreModule.dll" \
  -r:"$M/UnityEngine.IMGUIModule.dll" -r:"$M/UnityEngine.AudioModule.dll" \
  -r:"$M/UnityEngine.UnityWebRequestModule.dll" -r:"$M/UnityEngine.UnityWebRequestAudioModule.dll" \
  -r:"$M/UnityEngine.InputLegacyModule.dll" -r:"$M/UnityEngine.TextRenderingModule.dll" -r:"$M/Unity.InputSystem.dll" \
  src/LinkRadio.cs src/Ui.cs src/Theme.cs src/Util.cs
echo "OK -> bin/LinkRadio.dll"
