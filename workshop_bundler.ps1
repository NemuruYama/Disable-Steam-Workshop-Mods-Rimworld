[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [string] $SteamCmdRoot = 'D:\SteamCmd',
    [string] $ChangeNote = 'Update preview image and AI disclosure.'
)

$ErrorActionPreference = 'Stop'

$modRoot = $PSScriptRoot
$aboutPath = Join-Path $modRoot 'About\About.xml'
$about = [xml](Get-Content -LiteralPath $aboutPath)
$version = $about.ModMetaData.modVersion
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Could not read <modVersion> from $aboutPath"
}

$projectPath = Join-Path $modRoot 'Source\DisableSteamMods\DisableSteamMods.csproj'
dotnet build $projectPath --configuration $Configuration

$outRoot = Join-Path $modRoot 'Workshop'
$packageDir = Join-Path $outRoot 'DisableSteamMods'
$zipPath = Join-Path $modRoot "DisableSteamMods-v$version.zip"
$publishedFileIdPath = Join-Path $modRoot 'About\PublishedFileId.txt'
$publishedFileId = (Get-Content -LiteralPath $publishedFileIdPath -Raw).Trim()

if (Test-Path -LiteralPath $packageDir) {
    Remove-Item -LiteralPath $packageDir -Recurse -Force
}

New-Item -ItemType Directory -Force -Path (Join-Path $packageDir '1.6\Assemblies') | Out-Null
Copy-Item -LiteralPath (Join-Path $modRoot 'About') -Destination $packageDir -Recurse
Copy-Item -LiteralPath (Join-Path $modRoot 'loadFolders.xml') -Destination $packageDir

Get-ChildItem -LiteralPath (Join-Path $modRoot '1.6\Assemblies') -File |
    Where-Object { $_.Extension -in '.dll', '.xml' } |
    Copy-Item -Destination (Join-Path $packageDir '1.6\Assemblies')

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -LiteralPath $packageDir -DestinationPath $zipPath
Write-Host "Ok, $zipPath ready for uploading to Workshop"

$vdfPath = Join-Path $outRoot 'DisableSteamMods.vdf'
$previewPath = Join-Path $packageDir 'About\Preview.png'
$escapedPackageDir = $packageDir.Replace('\', '\\')
$escapedPreviewPath = $previewPath.Replace('\', '\\')
$escapedDescription = $about.ModMetaData.description.
    Replace('\', '\\').
    Replace('"', '\"').
    Replace("`r`n", '\n').
    Replace("`n", '\n')
$escapedChangeNote = $ChangeNote.Replace('\', '\\').Replace('"', '\"')
$vdf = @"
"workshopitem"
{
    "appid" "294100"
    "publishedfileid" "$publishedFileId"
    "contentfolder" "$escapedPackageDir"
    "previewfile" "$escapedPreviewPath"
    "title" "$($about.ModMetaData.name)"
    "description" "$escapedDescription"
    "changenote" "$escapedChangeNote"
}
"@

Set-Content -LiteralPath $vdfPath -Value $vdf -Encoding UTF8
Write-Host "SteamCMD VDF: $vdfPath"

$steamCmdPath = Join-Path $SteamCmdRoot 'steamcmd.exe'
if (Test-Path -LiteralPath $steamCmdPath -PathType Leaf) {
    Write-Host "SteamCMD detected. Upload command:"
    Write-Host "`"$steamCmdPath`" +login <steam-user> +workshop_build_item `"$vdfPath`" +quit"
} else {
    Write-Host "SteamCMD not found at $steamCmdPath; pass -SteamCmdRoot if it is installed elsewhere."
}
