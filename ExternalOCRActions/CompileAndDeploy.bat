@echo off
setlocal EnableDelayedExpansion
echo ============================================================
echo  ExternalOCRActions - Vendor OCR Actions for IBM Datacap
echo  Compiling: ExternalOCR_FileNamePageIdentify + ExternalOCR_RecognizeField + ExternalOCR_TestConnection
echo ============================================================
echo.

set CSC="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist %CSC% set CSC="C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"

set SRC_DIR=%~dp0src
set SOURCE1="%SRC_DIR%\ExternalOCRCommon.cs"
set SOURCE2="%SRC_DIR%\ExternalOCR_FileNamePageIdentify.cs"
set SOURCE3="%SRC_DIR%\ExternalOCR_RecognizeField.cs"
set SOURCE4="%SRC_DIR%\ExternalOCR_TestConnection.cs"

set OUTPUT="%~dp0ExternalOCRActions.dll"
set RRX="%~dp0RRS\ExternalOCRActions.rrx"
set CFG1="%SRC_DIR%\external_ocr_settings.json"
set CFG2="%SRC_DIR%\external_crop_config.json"

set APP_NAME=GoldenDemo
set RRS_DIR=C:\Datacap\RRS
set APP_RULES_DIR=C:\Datacap\%APP_NAME%\dco_%APP_NAME%\rules

echo Step 1: Cleaning previous build...
if exist %OUTPUT% del /Q %OUTPUT%
echo.

echo Step 2: Compiling ExternalOCRActions.dll...
%CSC% /target:library ^
  /out:%OUTPUT% ^
  /platform:anycpu ^
  /reference:System.dll ^
  /reference:System.Core.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Web.Extensions.dll ^
  /reference:Microsoft.CSharp.dll ^
  /optimize+ ^
  /utf8output ^
  %SOURCE1% %SOURCE2% %SOURCE3% %SOURCE4%

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

echo   [OK] ExternalOCRActions.dll created successfully
echo.

echo Step 4: Deploying DLL, RRX and configs to RRS folder...
if not exist "%RRS_DIR%" mkdir "%RRS_DIR%"
copy /Y %OUTPUT% "%RRS_DIR%\"
copy /Y %RRX%    "%RRS_DIR%\"
copy /Y %CFG1%   "%RRS_DIR%\"
copy /Y %CFG2%   "%RRS_DIR%\"
echo   [OK] Copied to %RRS_DIR%\ExternalOCRActions.dll
echo   [OK] Copied to %RRS_DIR%\ExternalOCRActions.rrx
echo   [OK] Copied to %RRS_DIR%\external_ocr_settings.json
echo   [OK] Copied to %RRS_DIR%\external_crop_config.json
echo.

echo Step 5: Deploying DLL, RRX and configs to application rules folder...
if exist "%APP_RULES_DIR%\" (
    copy /Y %OUTPUT% "%APP_RULES_DIR%\"
    copy /Y %RRX%    "%APP_RULES_DIR%\"
    copy /Y %CFG1%   "%APP_RULES_DIR%\"
    copy /Y %CFG2%   "%APP_RULES_DIR%\"
    echo   [OK] Copied to %APP_RULES_DIR%\ExternalOCRActions.dll
    echo   [OK] Copied to %APP_RULES_DIR%\ExternalOCRActions.rrx
    echo   [OK] Copied to %APP_RULES_DIR%\external_ocr_settings.json
    echo   [OK] Copied to %APP_RULES_DIR%\external_crop_config.json
)

echo.
echo ============================================================
echo  BUILD AND DEPLOY SUCCESSFUL
echo ============================================================
