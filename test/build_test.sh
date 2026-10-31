#!/usr/bin/env bash
# Unit-Tests fuer die reinen Hilfsfunktionen (src/Util.cs). Kein Spiel noetig.  Benoetigt: mono + mcs
set -euo pipefail
cd "$(dirname "$0")"
mcs -out:test.exe Test.cs ../src/Util.cs
mono test.exe
