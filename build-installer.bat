@echo off
REM Double-click on Windows to build the MSI installer.
REM Needs the .NET 8 SDK and the WiX Toolset v7 CLI (wix.exe) on PATH.
setlocal
cd /d "%~dp0"

where wix >nul 2>nul
if errorlevel 1 (
  echo WiX Toolset not found on PATH.
  echo Install it with:
  echo   dotnet tool install --global wix --version 7.0.1
  echo   wix extension add --global WixToolset.UI.wixext/7.0.1
  echo   wix extension add --global WixToolset.Util.wixext/7.0.1
  goto done
)

set "VERSION=1.0.0"
for /f "tokens=3 delims=<>" %%v in ('findstr /i "<Version>" Directory.Build.props') do set "VERSION=%%v"

echo Building Bass Coach %VERSION% ...
dotnet run --project tests\BassCoach.SelfTest -c Release || goto done
dotnet publish src\BassCoach.Desktop\BassCoach.Desktop.csproj -c Release -r win-x64 --self-contained true -o publish || goto done

if not exist dist mkdir dist
wix build installer\BassCoach.wxs -arch x64 -d AppVersion=%VERSION% -d PublishDir="%CD%\publish" -ext WixToolset.UI.wixext -ext WixToolset.Util.wixext -o "dist\Install-BassCoach-%VERSION%-x64.msi" || goto done
wix msi validate "dist\Install-BassCoach-%VERSION%-x64.msi" || goto done

powershell -NoProfile -Command "Get-FileHash 'dist\Install-BassCoach-%VERSION%-x64.msi' -Algorithm SHA256 | ForEach-Object { $_.Hash.ToLower() + '  ' + (Split-Path $_.Path -Leaf) } | Set-Content 'dist\SHA256SUMS.txt'"

echo.
echo Installer created: dist\Install-BassCoach-%VERSION%-x64.msi
:done
pause
