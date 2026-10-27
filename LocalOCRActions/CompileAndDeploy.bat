@echo off
setlocal EnableDelayedExpansion
echo ============================================================
echo  LocalOCRActions - Local OCR Actions for IBM Datacap
echo  Compiling: LocalOCR_PageIdentify + LocalOCR_ExtractFields
echo ============================================================
echo.

set CSC="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist %CSC% set CSC="C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"

set SRC_DIR=%~dp0src
set SOURCE1="%SRC_DIR%\LocalOCRCommon.cs"
set SOURCE2="%SRC_DIR%\LocalOCR_PageIdentify.cs"
set SOURCE3="%SRC_DIR%\LocalOCR_ExtractFields.cs"

set OUTPUT="%~dp0LocalOCRActions.dll"
set RRX="%~dp0RRS\LocalOCRActions.rrx"

set APP_NAME=GoldenDemo
set RRS_DIR=C:\Datacap\RRS
set APP_RULES_DIR=C:\Datacap\%APP_NAME%\dco_%APP_NAME%\rules

echo Step 1: Cleaning previous build...
if exist %OUTPUT% del /Q %OUTPUT%
echo.

echo Step 2: Compiling LocalOCRActions.dll...
%CSC% /target:library ^
  /out:%OUTPUT% ^
  /platform:anycpu ^
  /reference:System.dll ^
  /reference:System.Core.dll ^
  /reference:System.Web.Extensions.dll ^
  /reference:Microsoft.CSharp.dll ^
  /optimize+ ^
  /utf8output ^
  %SOURCE1% %SOURCE2% %SOURCE3%

if errorlevel 1 (
    echo.
    echo *** COMPILATION FAILED - review errors above ***
    exit /b 1
)

echo.
echo Step 3: Verifying output...
if not exist %OUTPUT% (
    echo *** ERROR: DLL was not created ***
    exit /b 1
)

echo   [OK] LocalOCRActions.dll created successfully
echo.

echo Step 4: Deploying DLL and RRX to RRS folder...
if not exist "%RRS_DIR%" mkdir "%RRS_DIR%"
copy /Y %OUTPUT% "%RRS_DIR%\"
copy /Y %RRX%    "%RRS_DIR%\"
echo   [OK] Copied to %RRS_DIR%\LocalOCRActions.dll
echo   [OK] Copied to %RRS_DIR%\LocalOCRActions.rrx
echo.

echo Step 5: Deploying DLL to application rules folder...
if exist "%APP_RULES_DIR%\" (
    copy /Y %OUTPUT% "%APP_RULES_DIR%\"
    copy /Y %RRX%    "%APP_RULES_DIR%\"
    echo   [OK] Copied to %APP_RULES_DIR%\LocalOCRActions.dll
    echo   [OK] Copied to %APP_RULES_DIR%\LocalOCRActions.rrx
)

echo.
echo ============================================================
echo  BUILD AND DEPLOY SUCCESSFUL
echo ============================================================
