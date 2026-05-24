# Build release artifacts: SmcManager_<version>.apk and SmcManager_<version>.exe
# Run from repo root: .\publish.ps1

$ErrorActionPreference = "Stop"

$Root = $PSScriptRoot
$Project = Join-Path $Root "src\SmcManager.Maui\SmcManager.Maui.csproj"
$PublishDir = Join-Path $Root "src\SmcManager.Maui\bin\Release\publish"
$AndroidWorkDir = Join-Path $PublishDir "_work-android"
$WindowsWorkDir = Join-Path $PublishDir "_work-windows"

$Version = (dotnet msbuild $Project -getProperty:ApplicationDisplayVersion).Trim()
if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "ApplicationDisplayVersion not found in $Project"
}

$ApkName = "SmcManager_$Version.apk"
$ExeName = "SmcManager_$Version.exe"
$ApkPath = Join-Path $PublishDir $ApkName
$ExePath = Join-Path $PublishDir $ExeName

Write-Host "SmcManager publish $Version"
Write-Host "Output: $PublishDir"
Write-Host ""

if (Test-Path $PublishDir) {
    Remove-Item $PublishDir -Recurse -Force
}

New-Item -ItemType Directory -Path $AndroidWorkDir -Force | Out-Null
New-Item -ItemType Directory -Path $WindowsWorkDir -Force | Out-Null

try {
    Write-Host ">>> Android APK"
    dotnet publish $Project `
        -f net10.0-android `
        -c Release `
        -p:AndroidPackageFormat=apk `
        -o $AndroidWorkDir
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $apkSource = Get-ChildItem $AndroidWorkDir -Filter "*-Signed.apk" -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if (-not $apkSource) {
        $apkSource = Get-ChildItem $AndroidWorkDir -Filter "*.apk" |
            Select-Object -First 1
    }
    if (-not $apkSource) {
        throw "APK not found in $AndroidWorkDir"
    }

    Write-Host "    $($apkSource.Name) -> $ApkName"
    Copy-Item $apkSource.FullName $ApkPath -Force

    Write-Host ""
    Write-Host ">>> Windows EXE"
    # Do not pass -r win-x64: MAUI picks the correct Windows RID (avoids Mono.win-x64 NU1102).
    dotnet publish $Project `
        -f net10.0-windows10.0.19041.0 `
        -c Release `
        -p:PublishSingleFile=true `
        -p:SelfContained=true `
        -o $WindowsWorkDir
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $exeSource = Join-Path $WindowsWorkDir "SmcManager.Maui.exe"
    if (-not (Test-Path $exeSource)) {
        throw "EXE not found: $exeSource"
    }

    Write-Host "    SmcManager.Maui.exe -> $ExeName"
    Copy-Item $exeSource $ExePath -Force
}
finally {
    if (Test-Path $AndroidWorkDir) { Remove-Item $AndroidWorkDir -Recurse -Force }
    if (Test-Path $WindowsWorkDir) { Remove-Item $WindowsWorkDir -Recurse -Force }
}

Write-Host ""
Write-Host "Done:"
Write-Host "  $ApkPath"
Write-Host "  $ExePath"
