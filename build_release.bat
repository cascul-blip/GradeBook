@echo off
rem Builds a standalone GradeBook.App.exe for Windows: self-contained (no .NET install needed)
rem and a single file (native libraries and PDF fonts are bundled inside the .exe).
rem Output: publish\win-x64\GradeBook.App.exe

cd /d "%~dp0"

dotnet publish src\GradeBook.App\GradeBook.App.csproj -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:IncludeAllContentForSelfExtract=true ^
  -p:DebugType=none ^
  -o publish\win-x64

if errorlevel 1 (
  echo.
  echo Build FAILED.
  pause
  exit /b 1
)

echo.
echo Built publish\win-x64\GradeBook.App.exe
pause
