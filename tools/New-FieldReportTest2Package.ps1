[CmdletBinding()]
param(
    [string] $SnapshotRoot = (Split-Path -Parent $PSScriptRoot),

    [string] $OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'Build\release\v1.3.0-field-report-test.2')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$tag = 'v1.3.0-field-report-test.2'
$zipName = 'BallisticPenetration-1.3.0-field-report-test.2-SPT-4.1.3.zip'
$zipPath = Join-Path $OutputDirectory $zipName
$sidecarPath = "$zipPath.sha256.txt"
$dllPath = Join-Path $SnapshotRoot 'src\BallisticPenetration\bin\Release\netstandard2.1\BallisticPenetration.dll'
$fieldReportsPath = Join-Path $SnapshotRoot 'docs\FIELD_REPORTS.md'
$acceptancePath = Join-Path $SnapshotRoot 'docs\community-alpha\TEST_2_ACCEPTANCE.md'
$verifierPath = Join-Path $SnapshotRoot 'tools\Test-Test2FieldReport.ps1'

$commit = (& git -C $SnapshotRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[0-9a-f]{40}$') {
    throw 'Could not resolve the exact release commit.'
}

foreach ($requiredPath in @($dllPath, $fieldReportsPath, $acceptancePath, $verifierPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required package input is missing: $requiredPath"
    }
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
if (Test-Path -LiteralPath $sidecarPath) {
    Remove-Item -LiteralPath $sidecarPath -Force
}

Add-Type -AssemblyName System.IO.Compression
$utf8NoBom = [Text.UTF8Encoding]::new($false)
$fixedTimestamp = [DateTimeOffset]::Parse('2026-09-01T00:00:00Z')
$dllItem = Get-Item -LiteralPath $dllPath
$dllHash = (Get-FileHash -LiteralPath $dllPath -Algorithm SHA256).Hash

$installText = @'
BallisticPenetration 1.3.0 - Field Report Test 2 Candidate

Compatibility:
SPT 4.1.3 only.

Installation:
1. Close SPT, the launcher, server, and Escape from Tarkov.
2. Back up BepInEx\plugins\BallisticPenetration and the existing configuration.
3. Extract this ZIP into the root of the SPT installation.
4. Allow BallisticPenetration.dll to replace the older copy.
5. Verify the installed DLL against DLL-SHA256.txt before launching SPT.

Testing:
- Follow TEST_2_ACCEPTANCE.md for the required one-run actor, world-surface, moving-door, and target-spall checks.
- Pass the DLL-SHA256.txt value to Test-Test2FieldReport.ps1 with -ExpectedDllSha256.
- Preserve the passing report hash and screenshots. Reports remain local and are not uploaded automatically.

Important:
- Experimental physical projectiles remain disabled by default.
- This candidate has passed offline build and validation only.
- Do not publish Test 2 until the exact installed DLL produces a completed SPT 4.1.3 report that the verifier marks PASS.
'@

$dllChecksumText = @"
BallisticPenetration.dll SHA-256: $dllHash
File length: $($dllItem.Length)
Release commit: $commit
Selected release tag: $tag
"@

function Add-ZipBytes {
    param(
        [Parameter(Mandatory)]
        [IO.Compression.ZipArchive] $Archive,

        [Parameter(Mandatory)]
        [string] $EntryName,

        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [byte[]] $Bytes
    )

    $entry = $Archive.CreateEntry($EntryName, [IO.Compression.CompressionLevel]::Optimal)
    $entry.LastWriteTime = $fixedTimestamp
    $stream = $entry.Open()
    try {
        if ($Bytes.Length -gt 0) {
            $stream.Write($Bytes, 0, $Bytes.Length)
        }
    }
    finally {
        $stream.Dispose()
    }
}

$fileStream = [IO.File]::Open($zipPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
try {
    $archive = [IO.Compression.ZipArchive]::new($fileStream, [IO.Compression.ZipArchiveMode]::Create, $false)
    try {
        Add-ZipBytes -Archive $archive -EntryName 'BepInEx/FieldReports/' -Bytes ([byte[]]@())
        Add-ZipBytes -Archive $archive -EntryName 'BepInEx/plugins/' -Bytes ([byte[]]@())
        Add-ZipBytes -Archive $archive -EntryName 'BepInEx/FieldReports/BallisticPenetration/' -Bytes ([byte[]]@())
        Add-ZipBytes -Archive $archive -EntryName 'BepInEx/plugins/BallisticPenetration/BallisticPenetration.dll' -Bytes ([IO.File]::ReadAllBytes($dllPath))
        Add-ZipBytes -Archive $archive -EntryName 'FIELD_REPORTS.md' -Bytes ([IO.File]::ReadAllBytes($fieldReportsPath))
        Add-ZipBytes -Archive $archive -EntryName 'INSTALL.txt' -Bytes ($utf8NoBom.GetBytes($installText))
        Add-ZipBytes -Archive $archive -EntryName 'TEST_2_ACCEPTANCE.md' -Bytes ([IO.File]::ReadAllBytes($acceptancePath))
        Add-ZipBytes -Archive $archive -EntryName 'Test-Test2FieldReport.ps1' -Bytes ([IO.File]::ReadAllBytes($verifierPath))
        Add-ZipBytes -Archive $archive -EntryName 'DLL-SHA256.txt' -Bytes ($utf8NoBom.GetBytes($dllChecksumText))
    }
    finally {
        $archive.Dispose()
    }
}
finally {
    $fileStream.Dispose()
}

$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
[IO.File]::WriteAllText($sidecarPath, "$zipHash  $zipName`r`n", $utf8NoBom)

[pscustomobject]@{
    ZipPath = $zipPath
    ZipLength = (Get-Item -LiteralPath $zipPath).Length
    ZipSha256 = $zipHash
    SidecarPath = $sidecarPath
    DllLength = $dllItem.Length
    DllSha256 = $dllHash
    Commit = $commit
    Tag = $tag
}
