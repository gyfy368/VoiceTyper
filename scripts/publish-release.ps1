#Requires -Version 5.1
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\VoiceTyper\VoiceTyper.csproj"
$version = "1.1.2"
$publishDir = Join-Path $root "dist\publish"
$stageDir = Join-Path $root "dist\stage"
$zipName = "VoiceTyper-v$version-win-x64.zip"
$zipPath = Join-Path $root "dist\$zipName"
$modelsSrc = Join-Path $root "models"
$readmeSrc = Join-Path $root "README.md"

function Test-ModelsReady {
  param([string]$Root)
  $onnx = Join-Path $Root "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2024-07-17\model.int8.onnx"
  $tokens = Join-Path $Root "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2024-07-17\tokens.txt"
  $vad = Join-Path $Root "silero_vad.onnx"
  return (Test-Path $onnx) -and ((Get-Item $onnx).Length -gt 1MB) -and
         (Test-Path $tokens) -and ((Get-Item $tokens).Length -gt 32) -and
         (Test-Path $vad) -and ((Get-Item $vad).Length -gt 32)
}

Write-Host "Waiting for models/ (sibling downloader) up to 15 minutes..."
$deadline = (Get-Date).AddMinutes(15)
while (-not (Test-ModelsReady $modelsSrc)) {
  if ((Get-Date) -ge $deadline) {
    Write-Warning "Models still missing after wait. Will publish code build but SKIP zip packaging."
    break
  }
  Start-Sleep -Seconds 20
  Write-Host ("  polling models... {0:HH:mm:ss}" -f (Get-Date))
}

Write-Host "Publishing VoiceTyper (win-x64, self-contained, single file)..."
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

dotnet publish $project `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o $publishDir

if ($LASTEXITCODE -ne 0) {
  throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$exe = Join-Path $publishDir "VoiceTyper.exe"
if (-not (Test-Path $exe)) {
  throw "VoiceTyper.exe not found after publish."
}

if (-not (Test-ModelsReady $modelsSrc)) {
  Write-Host "STATUS: ZIP_SKIPPED_MODELS_MISSING"
  Write-Host "Published exe is at: $exe"
  Write-Host "Re-run this script after models/ is complete."
  exit 2
}

if (Test-Path $stageDir) { Remove-Item $stageDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stageDir | Out-Null
Copy-Item $exe (Join-Path $stageDir "VoiceTyper.exe") -Force
Copy-Item $readmeSrc (Join-Path $stageDir "README.md") -Force
$modelsDst = Join-Path $stageDir "models"
New-Item -ItemType Directory -Force -Path $modelsDst | Out-Null
Copy-Item -Path (Join-Path $modelsSrc "*") -Destination $modelsDst -Recurse -Force

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $stageDir "*") -DestinationPath $zipPath -CompressionLevel Optimal

$sizeMB = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
Write-Host "STATUS: ZIP_OK"
Write-Host "Done -> $zipPath ($sizeMB MB)"
