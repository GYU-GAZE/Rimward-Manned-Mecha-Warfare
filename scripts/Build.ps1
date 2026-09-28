param([Parameter(Mandatory=$true)][string]$RimWorldDir, [switch]$Clean)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$gameVersion = (Get-Content (Join-Path $RimWorldDir 'Version.txt') -Raw).Trim()
if ($gameVersion -notmatch '^1\.6\.') { throw "Expected RimWorld 1.6, found $gameVersion" }
$project = Join-Path $repo 'Source/ZenithFrame/ZenithFrame.csproj'
if ($Clean) {
    dotnet clean $project -c Release "-p:RimWorldDir=$RimWorldDir" --nologo
    if ($LASTEXITCODE) { throw 'Clean failed' }
}
dotnet build $project -c Release "-p:RimWorldDir=$RimWorldDir" --nologo
if ($LASTEXITCODE) { throw 'Build failed' }
$release = Join-Path $repo 'artifacts/ZenithFrame'
if (Test-Path $release) { Remove-Item $release -Recurse -Force }
New-Item -ItemType Directory -Force "$release/Assemblies" | Out-Null
Copy-Item "$repo/Mod/*" $release -Recurse -Force
Copy-Item "$repo/Source/ZenithFrame/bin/Release/net472/ZenithFrame.dll" "$release/Assemblies/"
Write-Host "Built against $gameVersion -> $release"
