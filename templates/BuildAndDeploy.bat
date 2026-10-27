@echo off
setlocal enabledelayedexpansion

REM ==============================================================================
REM Generic Build & Dual-Deployment Script for IBM Datacap C# Custom Actions
REM Usage: BuildAndDeploy.bat [ActionLibraryName] [ApplicationName]
REM Example: BuildAndDeploy.bat MyCustomActions GoldenDemo
REM ==============================================================================

set ACTION_LIB=%1
if "%ACTION_LIB%"=="" set ACTION_LIB=LocalOCRActions

set APP_NAME=%2
if "%APP_NAME%"=="" set APP_NAME=GoldenDemo

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" (
    set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
)

if not exist "%CSC%" (
    echo [ERROR] C# Compiler (csc.exe) not found in .NET Framework directory.
    exit /b 1
)

set SRC_DIR=%~dp0src
set RRS_SYS_DIR=C:\Datacap\RRS
set APP_RULES_DIR=C:\Datacap\%APP_NAME%\dco_%APP_NAME%\rules

echo ==============================================================================
echo Building Action Library : %ACTION_LIB%
echo Target Application      : %APP_NAME%
echo Compiler                : %CSC%
echo ==============================================================================

if not exist "%RRS_SYS_DIR%" (
    mkdir "%RRS_SYS_DIR%"
)

if not exist "%APP_RULES_DIR%" (
    mkdir "%APP_RULES_DIR%"
)

REM 1. Compile C# sources into DLL
echo [1/3] Compiling C# source files...
"%CSC%" /target:library /platform:anycpu /optimize+ ^
    /out:"%RRS_SYS_DIR%\%ACTION_LIB%.dll" ^
    /reference:System.dll,System.Core.dll,System.Web.Extensions.dll ^
    "%SRC_DIR%\*.cs"

if %ERRORLEVEL% neq 0 (
    echo [ERROR] Compilation failed.
    exit /b %ERRORLEVEL%
)

REM 2. Deploy DLL to Application Rules folder
echo [2/3] Deploying %ACTION_LIB%.dll to %APP_RULES_DIR%...
copy /Y "%RRS_SYS_DIR%\%ACTION_LIB%.dll" "%APP_RULES_DIR%\" >nul

REM 3. Deploy RRX definition to both RRS and Application Rules
echo [3/3] Deploying RRX definitions...
if exist "%~dp0RRS\%ACTION_LIB%.rrx" (
    copy /Y "%~dp0RRS\%ACTION_LIB%.rrx" "%RRS_SYS_DIR%\" >nul
    copy /Y "%~dp0RRS\%ACTION_LIB%.rrx" "%APP_RULES_DIR%\" >nul
)

echo ==============================================================================
echo [SUCCESS] %ACTION_LIB% compiled and deployed successfully to:
echo   - %RRS_SYS_DIR%\%ACTION_LIB%.dll
echo   - %APP_RULES_DIR%\%ACTION_LIB%.dll
echo ==============================================================================
exit /b 0
