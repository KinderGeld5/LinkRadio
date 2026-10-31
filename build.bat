@echo off
rem Windows: benoetigt das .NET SDK (dotnet). Referenz-DLLs vorher nach libs\ kopieren (siehe libs\README.txt).
rem Optional andere Ordner:  dotnet build -c Release -p:LibsDir=C:\pfad\mit\dlls\
cd /d "%~dp0"
dotnet build LinkRadio.csproj -c Release
if errorlevel 1 exit /b 1
echo.
echo Fertig: bin\Release\net472\LinkRadio.dll
