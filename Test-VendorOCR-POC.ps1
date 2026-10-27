# Test-VendorOCR-POC.ps1 - End-to-end verification of Vendor OCR POC flow

$baseUrl = "http://34.69.150.249:8019"
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  Vendor OCR POC - API & Workflow Verification" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

# 1. API Health & Language Checks
Write-Host "[1/5] Testing Vendor OCR API Diagnostics..." -ForegroundColor Yellow

$alive = Invoke-RestMethod -Uri "$baseUrl/alive" -Method Get -TimeoutSec 10
Write-Host "  * GET /alive: $($alive | ConvertTo-Json -Compress)" -ForegroundColor Green

$health = Invoke-RestMethod -Uri "$baseUrl/health_checks" -Method Get -TimeoutSec 10
Write-Host "  * GET /health_checks: $($health | ConvertTo-Json -Compress)" -ForegroundColor Green

$langs = Invoke-RestMethod -Uri "$baseUrl/get_supported_languages" -Method Get -TimeoutSec 10
Write-Host "  * GET /get_supported_languages: $($langs | ConvertTo-Json -Compress)" -ForegroundColor Green
Write-Host ""

# 2. Test Image Files
Write-Host "[2/5] Inspecting Test Images in Input Directories..." -ForegroundColor Yellow
$imageDir = "C:\Datacap\GoldenDemo\images\Input_MultiFormat"
$testFiles = @(
    "signature_form_01.jpg",
    "npa_form_01.jpg",
    "national_id_01.jpg"
)

foreach ($tf in $testFiles) {
    $p = Join-Path $imageDir $tf
    if (Test-Path $p) {
        $fi = Get-Item $p
        Write-Host "  [OK] Found $tf ($($fi.Length) bytes)" -ForegroundColor Green
    } else {
        Write-Host "  [WARN] Missing $tf at $p" -ForegroundColor Red
    }
}
Write-Host ""

# 3. Test Direct Cropping & Field Recognition via .NET assembly
Write-Host "[3/5] Testing ExternalOCRActions.dll in-process..." -ForegroundColor Yellow
[System.Reflection.Assembly]::LoadFrom("C:\Datacap\RRS\ExternalOCRActions.dll") | Out-Null

$testConnection = New-Object ExternalOCRActions.ExternalOCR_TestConnection
$connRes = $testConnection.Execute("")
Write-Host "  * ExternalOCR_TestConnection result: $connRes (0 = Success)" -ForegroundColor Green

# 4. Test Cropping and Field Recognition on each test image
Write-Host ""
Write-Host "[4/5] Testing Field Cropping & Recognition on Test Files..." -ForegroundColor Yellow

Add-Type -AssemblyName System.Drawing

function Test-FieldRecognition([string]$imagePath, [string]$pageType, [string]$fieldId, [double]$x, [double]$y, [double]$w, [double]$h, [string]$docType, [string]$lang) {
    $img = [System.Drawing.Image]::FromFile($imagePath)
    $imgW = $img.Width
    $imgH = $img.Height
    $img.Dispose()

    $rectX = [int]($x * $imgW)
    $rectY = [int]($y * $imgH)
    $rectW = [int]($w * $imgW)
    $rectH = [int]($h * $imgH)

    $rect = New-Object System.Drawing.Rectangle($rectX, $rectY, $rectW, $rectH)
    [int]$cropW = 0
    [int]$cropH = 0
    $cropBytes = [ExternalOCRActions.ExternalOCRCommon]::CropFieldImage($imagePath, $rect, [ref]$cropW, [ref]$cropH)

    $settings = [ExternalOCRActions.ExternalOCRCommon]::LoadSettings("")
    $result = [ExternalOCRActions.ExternalOCRCommon]::RecognizeCroppedField($cropBytes, $cropW, $cropH, $settings, $docType, $lang, $null)

    Write-Host "    -> Field: $fieldId [$cropW x $cropH px] | Route: $(if ($docType -eq 'handwritten') {'Vendor_OCR_Handwritten'} else {'Vendor_OCR_Typewritten'})" -ForegroundColor Cyan
    Write-Host "       DocType: $docType | HTTP: $($result.HttpStatusCode) | Text: '$($result.Text)'" -ForegroundColor White
}

# 4a. Signature Form
$sigImg = Join-Path $imageDir "signature_form_01.jpg"
Write-Host "  * Page: Signature_Form_1 (File: signature_form_01.jpg)" -ForegroundColor Yellow
Test-FieldRecognition $sigImg "Signature_Form_1" "AccountHolderName" 0.23 0.130 0.67 0.058 "handwritten" "Arabic/Latest"
Test-FieldRecognition $sigImg "Signature_Form_1" "AccountNumber"     0.10 0.188 0.84 0.068 "handwritten" "Arabic/Latest"

# 4b. NPA Form
$npaImg = Join-Path $imageDir "npa_form_01.jpg"
Write-Host "  * Page: Form_Page_1 (File: npa_form_01.jpg)" -ForegroundColor Yellow
Test-FieldRecognition $npaImg "Form_Page_1" "EmployerName2"     0.22 0.224 0.67 0.048 "handwritten" "Arabic/Latest"

# 4c. National ID
$natImg = Join-Path $imageDir "national_id_01.jpg"
Write-Host "  * Page: National ID Front (File: national_id_01.jpg)" -ForegroundColor Yellow
Test-FieldRecognition $natImg "National ID Front" "FullNameArabic"   0.33 0.042 0.38 0.060 "typewritten" "Arabic/Latest"
Test-FieldRecognition $natImg "National ID Front" "NationalIdNumber" 0.18 0.173 0.60 0.058 "typewritten" "Arabic/Latest"

# 5. Verify Legacy Local OCR flow isolation
Write-Host ""
Write-Host "[5/5] Verifying Legacy Local OCR Flow Isolation..." -ForegroundColor Yellow
$legacyFiles = @(
    "C:\Datacap\RRS\LocalOCRActions.dll",
    "C:\Datacap\RRS\LocalOCRActions.rrx",
    "C:\Datacap\GoldenDemo\dco_GoldenDemo\rules\localocr_pageidentify.rul",
    "C:\Datacap\GoldenDemo\dco_GoldenDemo\rules\localocr_extractfields.rul"
)

foreach ($lf in $legacyFiles) {
    if (Test-Path $lf) {
        Write-Host "  [OK] Legacy file intact: $lf" -ForegroundColor Green
    } else {
        Write-Host "  [FAIL] Legacy file missing: $lf" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  ALL CHECKS COMPLETED" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
