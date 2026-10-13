param([Parameter(Mandatory=$true)][string]$RimWorldDir,
    [ValidateSet('CoreOnly','WithVEF')][string]$PackageMode = 'CoreOnly',
    [string]$VEFDir, [switch]$Clean)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if ($PackageMode -eq 'CoreOnly' -and ![string]::IsNullOrWhiteSpace($VEFDir)) {
    throw 'VEFDir requires explicit -PackageMode WithVEF.'
}
if ($PackageMode -eq 'WithVEF' -and [string]::IsNullOrWhiteSpace($VEFDir)) {
    $steamApps = Split-Path (Split-Path $RimWorldDir -Parent) -Parent
    $VEFDir = Join-Path $steamApps 'workshop/content/294100/2023507013'
}
if ($PackageMode -eq 'WithVEF' -and !(Test-Path -LiteralPath "$VEFDir/1.6/Assemblies/VEF.dll")) {
    throw "WithVEF package requires the installed 1.6 framework API at $VEFDir"
}
$gameVersion = (Get-Content (Join-Path $RimWorldDir 'Version.txt') -Raw).Trim()
if ($gameVersion -notmatch '^1\.6\.') { throw "Expected RimWorld 1.6, found $gameVersion" }
$project = Join-Path $repo 'Source/ZenithFrame/ZenithFrame.csproj'
if ($Clean) {
    dotnet clean $project -c Release "-p:RimWorldDir=$RimWorldDir" --nologo
    if ($LASTEXITCODE) { throw 'Clean failed' }
}
dotnet build $project -c Release "-p:RimWorldDir=$RimWorldDir" --nologo
if ($LASTEXITCODE) { throw 'Build failed' }
$vefProject = Join-Path $repo 'Source/ZenithFrame.VEF/ZenithFrame.VEF.csproj'
if ($PackageMode -eq 'WithVEF') {
    if ($Clean) {
        dotnet clean $vefProject -c Release "-p:RimWorldDir=$RimWorldDir" "-p:VEFDir=$VEFDir" --nologo
        if ($LASTEXITCODE) { throw 'VEF compatibility clean failed' }
    }
    dotnet build $vefProject -c Release "-p:RimWorldDir=$RimWorldDir" "-p:VEFDir=$VEFDir" --nologo
    if ($LASTEXITCODE) { throw 'VEF compatibility build failed' }
}
$release = Join-Path $repo 'artifacts/ZenithFrame'
$repoRoot = [IO.Path]::GetFullPath($repo).TrimEnd('\') + '\'
$releaseFull = [IO.Path]::GetFullPath($release)
if (!$releaseFull.StartsWith($repoRoot, [StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe release target: $releaseFull" }
if (Test-Path -LiteralPath $releaseFull) { Remove-Item -LiteralPath $releaseFull -Recurse -Force }
New-Item -ItemType Directory -Force "$release/Assemblies" | Out-Null
Copy-Item "$repo/Mod/*" $release -Recurse -Force
Copy-Item "$repo/Source/ZenithFrame/bin/Release/net472/ZenithFrame.dll" "$release/Assemblies/"
if ($PackageMode -eq 'WithVEF') {
    New-Item -ItemType Directory -Force "$release/Compatibility/VEF/Assemblies" | Out-Null
    Copy-Item "$repo/Source/ZenithFrame.VEF/bin/Release/net472/ZenithFrame.VEF.dll" "$release/Compatibility/VEF/Assemblies/"
}
else {
    [xml]$folders = Get-Content -LiteralPath "$release/loadFolders.xml" -Raw
    $optional = $folders.SelectSingleNode('//li[@IfModActive="OskarPotocki.VanillaFactionsExpanded.Core"]')
    if ($optional) { $null = $optional.ParentNode.RemoveChild($optional) }
    $folders.Save((Join-Path $release 'loadFolders.xml'))
}
Set-Content -LiteralPath "$release/PackageMode.txt" -Value $PackageMode
$adapter = Test-Path -LiteralPath "$release/Compatibility/VEF/Assemblies/ZenithFrame.VEF.dll"
if ($adapter -ne ($PackageMode -eq 'WithVEF')) { throw 'Release adapter does not match explicit package mode.' }
Write-Host "Built $PackageMode against $gameVersion -> $release"
