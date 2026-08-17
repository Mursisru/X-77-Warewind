# Build and deploy X-77 Warewind (Release)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $root

dotnet build ".\X77Warewind\X77Warewind.csproj" -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$deploy = if ($env:NuclearOptionRoot) {
  Join-Path $env:NuclearOptionRoot "BepInEx\plugins\X-77-Warewind"
} else {
  'C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option\BepInEx\plugins\X-77-Warewind'
}
New-Item -ItemType Directory -Force -Path $deploy | Out-Null
Copy-Item -LiteralPath ".\X77Warewind\bin\Release\X77Warewind.dll" -Destination $deploy -Force

$nobpCandidates = @(
  ".\UnityBake\Build\X77Warewind.nobp",
  ".\X77Warewind\Resources\X77Warewind.nobp"
)
foreach ($n in $nobpCandidates) {
  if (Test-Path -LiteralPath $n) {
    $len = (Get-Item -LiteralPath $n).Length
    if ($len -lt 4096) { Write-Warning "Skip tiny nobp ($len bytes): $n"; continue }
    Copy-Item -LiteralPath $n -Destination (Join-Path $deploy "X77Warewind.nobp") -Force
    Write-Host "Deployed nobp ($len bytes) from $n"
    break
  }
}

$wwTex = ".\UnityBake\Assets\MissilePack\Textures\Warewind"
if (Test-Path -LiteralPath $wwTex) {
  $wwDst = Join-Path $deploy "Textures\Warewind"
  New-Item -ItemType Directory -Force -Path $wwDst | Out-Null
  Copy-Item -LiteralPath (Join-Path $wwTex "*") -Destination $wwDst -Force
}

Write-Host "Deployed to $deploy"
Get-ChildItem -LiteralPath $deploy | Format-Table Name, Length
