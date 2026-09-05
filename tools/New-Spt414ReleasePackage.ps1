[CmdletBinding()]
param(
    [string] $SnapshotRoot = (Split-Path -Parent $PSScriptRoot),

    [string] $OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'Build\release\v1.3.1')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$tag = 'v1.3.1'
$zipName = 'BallisticPenetration-1.3.1-SPT-4.1.4.zip'
$zipPath = Join-Path $OutputDirectory $zipName
$sidecarPath = "$zipPath.sha256.txt"
$dllPath = Join-Path $SnapshotRoot 'src\BallisticPenetration\bin\Release\netstandard2.1\BallisticPenetration.dll'

$commit = (& git -C $SnapshotRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[0-9a-f]{40}$') {
    throw 'Could not resolve the exact release commit.'
}

if (-not (Test-Path -LiteralPath $dllPath -PathType Leaf)) {
    throw "Release DLL is missing: $dllPath"
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
if (Test-Path -LiteralPath $sidecarPath) {
    Remove-Item -LiteralPath $sidecarPath -Force
}

Add-Type -AssemblyName System.IO.Compression
$fixedTimestamp = [DateTimeOffset]::Parse('2026-09-01T00:00:00Z')
$entryName = 'BepInEx/plugins/BallisticPenetration/BallisticPenetration.dll'

$fileStream = [IO.File]::Open($zipPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
try {
    $archive = [IO.Compression.ZipArchive]::new($fileStream, [IO.Compression.ZipArchiveMode]::Create, $false)
    try {
        $entry = $archive.CreateEntry($entryName, [IO.Compression.CompressionLevel]::Optimal)
        $entry.LastWriteTime = $fixedTimestamp
        $entryStream = $entry.Open()
        try {
            $dllBytes = [IO.File]::ReadAllBytes($dllPath)
            $entryStream.Write($dllBytes, 0, $dllBytes.Length)
        }
        finally {
            $entryStream.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
}
finally {
    $fileStream.Dispose()
}

$utf8NoBom = [Text.UTF8Encoding]::new($false)
$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
[IO.File]::WriteAllText($sidecarPath, "$zipHash  $zipName`r`n", $utf8NoBom)

[pscustomobject]@{
    ZipPath = $zipPath
    ZipLength = (Get-Item -LiteralPath $zipPath).Length
    ZipSha256 = $zipHash
    SidecarPath = $sidecarPath
    DllLength = (Get-Item -LiteralPath $dllPath).Length
    DllSha256 = (Get-FileHash -LiteralPath $dllPath -Algorithm SHA256).Hash
    Commit = $commit
    Tag = $tag
    Entry = $entryName
}
