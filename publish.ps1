$ErrorActionPreference = "Stop"

$localSdk = "C:\tmp\dotnet8\dotnet.exe"
$dotnet = if (Test-Path $localSdk) { $localSdk } else { (Get-Command dotnet).Source }
$cliHome = Join-Path $PSScriptRoot ".dotnet-home"
$nugetPackages = Join-Path $PSScriptRoot ".nuget"

New-Item -ItemType Directory -Force $cliHome, $nugetPackages | Out-Null
$env:DOTNET_CLI_HOME = $cliHome
$env:APPDATA = $cliHome
$env:NUGET_PACKAGES = $nugetPackages
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

& $dotnet restore "$PSScriptRoot\src\StudyTimer.App\StudyTimer.App.csproj" `
    --runtime win-x64 `
    --source "https://api.nuget.org/v3/index.json"
if ($LASTEXITCODE -ne 0) {
    throw "项目还原失败，退出代码：$LASTEXITCODE"
}

& $dotnet publish "$PSScriptRoot\src\StudyTimer.App\StudyTimer.App.csproj" `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --no-restore `
    --output "$PSScriptRoot\dist\win-x64" `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) {
    throw "项目发布失败，退出代码：$LASTEXITCODE"
}

Write-Host "发布完成：$PSScriptRoot\dist\win-x64\StudyTimer.exe"
