@echo off
setlocal

set "GAME_DIR=D:\SteamLibrary\steamapps\common\The Scroll Of Taiwu"
set "MOD_NAME=CombatSkillPresetBinding"
set "MOD_DEST=%GAME_DIR%\Mod\%MOD_NAME%"

dotnet build "%~dp0CombatSkillPresetBinding.Backend.csproj" -c Release
if errorlevel 1 exit /b %errorlevel%

if not exist "%MOD_DEST%\Plugins" mkdir "%MOD_DEST%\Plugins"
copy /Y "%~dp0Config.lua" "%MOD_DEST%\Config.lua" >nul
copy /Y "%~dp0mod\Plugins\CombatSkillPresetBinding.Backend.dll" "%MOD_DEST%\Plugins\CombatSkillPresetBinding.Backend.dll" >nul
if exist "%~dp0mod\Plugins\CombatSkillPresetBinding.Backend.pdb" copy /Y "%~dp0mod\Plugins\CombatSkillPresetBinding.Backend.pdb" "%MOD_DEST%\Plugins\CombatSkillPresetBinding.Backend.pdb" >nul

echo Deployed to %MOD_DEST%
