# Build APK
Write-Host "Building APK..." -ForegroundColor Yellow
dotnet build -f net8.0-android -c Debug
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed" -ForegroundColor Red
    exit 1
}

# Find and install APK
Write-Host "Finding APK..." -ForegroundColor Yellow
$workspaceFolder = Get-Location
$apkPath = Join-Path $workspaceFolder "bin\Debug\net8.0-android"
Write-Host "Looking in: $apkPath" -ForegroundColor Cyan

# Get all APK files, prefer Signed version for installation
$apkFiles = Get-ChildItem -Path $apkPath -Filter "*.apk"
$apk = $apkFiles | Where-Object { $_.Name -like "*-Signed*" } | Select-Object -First 1

# If no Signed APK, use any APK
if (-not $apk -and $apkFiles) {
    $apk = $apkFiles | Select-Object -First 1
}

if ($apk) {
    Write-Host "Installing APK: $($apk.Name)" -ForegroundColor Green
    adb install -r $apk.FullName
    if ($LASTEXITCODE -eq 0) {
        Write-Host "APK installed successfully" -ForegroundColor Green
        Write-Host "Starting app..." -ForegroundColor Yellow
        adb shell am start -n "com.companyname.myandroidapp/crc644254db90c0983b42.MainActivity"
        Write-Host "App started" -ForegroundColor Green
    } else {
        Write-Host "Failed to install APK" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "APK not found in $apkPath" -ForegroundColor Red
    Write-Host "Available files:" -ForegroundColor Yellow
    Get-ChildItem -Path $apkPath -Filter "*.apk" | ForEach-Object { Write-Host "  - $($_.Name)" }
    exit 1
}
