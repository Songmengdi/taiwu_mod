@echo off
REM ==========================================
REM  TaiwuDebugMod 部署脚本
REM  编译 + 复制到游戏 Mod 目录
REM ==========================================
setlocal

set "GAME_DIR=D:\SteamLibrary\steamapps\common\The Scroll Of Taiwu"
set "MOD_NAME=TaiwuDebugMod"
set "MOD_DEST=%GAME_DIR%\Mod\%MOD_NAME%"

echo [1/3] Building...
dotnet build "%~dp0TaiwuDebugMod.csproj" -c Release
if %ERRORLEVEL% NEQ 0 (
    echo BUILD FAILED
    exit /b %ERRORLEVEL%
)

echo.
echo [2/3] Deploying to game Mod directory...
if not exist "%MOD_DEST%" mkdir "%MOD_DEST%"
if not exist "%MOD_DEST%\Plugins" mkdir "%MOD_DEST%\Plugins"

REM Copy Config.lua
copy /Y "%~dp0Config.lua" "%MOD_DEST%\Config.lua" >nul
echo   Config.lua -^> %MOD_DEST%

REM Copy DLL and PDB
copy /Y "%~dp0plugins\TaiwuDebugMod.dll" "%MOD_DEST%\Plugins\TaiwuDebugMod.dll" >nul
echo   TaiwuDebugMod.dll -^> %MOD_DEST%\Plugins\

if exist "%~dp0plugins\TaiwuDebugMod.pdb" (
    copy /Y "%~dp0plugins\TaiwuDebugMod.pdb" "%MOD_DEST%\Plugins\TaiwuDebugMod.pdb" >nul
    echo   TaiwuDebugMod.pdb -^> %MOD_DEST%\Plugins\  (debug symbols)
)

echo.
echo [3/3] Done! Mod deployed to:
echo   %MOD_DEST%
echo.
echo Verify: launch game -^> Mod Manager -^> enable "TaiwuDebugMod"
echo Debug log will be at: %MOD_DEST%\debug.log
