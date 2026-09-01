[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Path,

    [string] $ExpectedSptVersion = '4.1.3',

    [string] $ExpectedDllSha256 = 'EBFA1B58A8770D973C43D957C7D9FEC3BFF4C05505653106D7B3814EA41CBDF3',

    [switch] $ActorVisualConfirmed,

    [switch] $WorldSurfaceVisualConfirmed,

    [switch] $MovingDoorVisualConfirmed,

    [switch] $TargetSpallVisualConfirmed
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RecordValue {
    param(
        [Parameter(Mandatory)]
        [object] $Record,

        [Parameter(Mandatory)]
        [string] $Name
    )

    $property = $Record.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Add-Count {
    param(
        [Parameter(Mandatory)]
        [hashtable] $Table,

        [Parameter(Mandatory)]
        [string] $Key
    )

    if ($Table.ContainsKey($Key)) {
        $Table[$Key] = [int] $Table[$Key] + 1
    }
    else {
        $Table[$Key] = 1
    }
}

$resolvedPath = (Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path
$failures = [Collections.Generic.List[string]]::new()
$records = [Collections.Generic.List[object]]::new()
$lineNumber = 0

foreach ($line in Get-Content -LiteralPath $resolvedPath) {
    $lineNumber++
    if ([string]::IsNullOrWhiteSpace($line)) {
        $failures.Add("Line $lineNumber is empty.")
        continue
    }

    try {
        $record = $line | ConvertFrom-Json -ErrorAction Stop
        $records.Add($record)
    }
    catch {
        $failures.Add("Line $lineNumber is not valid JSON: $($_.Exception.Message)")
    }
}

if ($records.Count -eq 0) {
    $failures.Add('The report contains no JSON records.')
}

$sessionStarts = @($records | Where-Object { (Get-RecordValue -Record $_ -Name 'event') -eq 'session-start' })
$sessionEnds = @($records | Where-Object { (Get-RecordValue -Record $_ -Name 'event') -eq 'session-end' })

if ($sessionStarts.Count -ne 1) {
    $failures.Add("Expected exactly one session-start record; found $($sessionStarts.Count).")
}
if ($sessionEnds.Count -ne 1) {
    $failures.Add("Expected exactly one session-end record; found $($sessionEnds.Count).")
}
if ($records.Count -gt 0 -and (Get-RecordValue -Record $records[0] -Name 'event') -ne 'session-start') {
    $failures.Add('The first record is not session-start.')
}
if ($records.Count -gt 0 -and (Get-RecordValue -Record $records[$records.Count - 1] -Name 'event') -ne 'session-end') {
    $failures.Add('The final record is not session-end; use a completed report.')
}

$previousSequence = 0L
foreach ($record in $records) {
    $schemaVersion = Get-RecordValue -Record $record -Name 'schemaVersion'
    if ($schemaVersion -ne 1) {
        $failures.Add("Unsupported or missing schemaVersion at report sequence $(Get-RecordValue -Record $record -Name 'reportSequence').")
    }

    $sequence = Get-RecordValue -Record $record -Name 'reportSequence'
    if ($null -eq $sequence -or [long] $sequence -le $previousSequence) {
        $failures.Add("Report sequence is missing or non-increasing after $previousSequence.")
    }
    else {
        $previousSequence = [long] $sequence
    }
}

$sessionStart = if ($sessionStarts.Count -eq 1) { $sessionStarts[0] } else { $null }
$sessionEnd = if ($sessionEnds.Count -eq 1) { $sessionEnds[0] } else { $null }

if ($null -ne $sessionStart) {
    $actualSptVersion = [string] (Get-RecordValue -Record $sessionStart -Name 'sptVersion')
    $actualDllHash = [string] (Get-RecordValue -Record $sessionStart -Name 'runningDllSha256')
    if ($actualSptVersion -ne $ExpectedSptVersion) {
        $failures.Add("SPT version is $actualSptVersion; expected exact $ExpectedSptVersion.")
    }
    if ($actualDllHash -ne $ExpectedDllSha256) {
        $failures.Add("Running DLL SHA-256 is $actualDllHash; expected $ExpectedDllSha256.")
    }
}

$observed = @($records | Where-Object { (Get-RecordValue -Record $_ -Name 'event') -eq 'collision-observed' })
$resolved = @($records | Where-Object { (Get-RecordValue -Record $_ -Name 'event') -eq 'collision-resolved' })
$observedCounts = @{}
$resolvedCounts = @{}

foreach ($record in $observed) {
    $identity = [string] (Get-RecordValue -Record $record -Name 'collisionIdentity')
    if ([string]::IsNullOrWhiteSpace($identity)) {
        $failures.Add('A collision-observed record has no collisionIdentity.')
        continue
    }
    Add-Count -Table $observedCounts -Key $identity
}

foreach ($record in $resolved) {
    $identity = [string] (Get-RecordValue -Record $record -Name 'collisionIdentity')
    if ([string]::IsNullOrWhiteSpace($identity)) {
        $failures.Add('A collision-resolved record has no collisionIdentity.')
        continue
    }
    Add-Count -Table $resolvedCounts -Key $identity
}

$allCollisionIdentities = @($observedCounts.Keys) + @($resolvedCounts.Keys) | Sort-Object -Unique
$unpairedCollisionCount = 0
foreach ($identity in $allCollisionIdentities) {
    $observedCount = if ($observedCounts.ContainsKey($identity)) { [int] $observedCounts[$identity] } else { 0 }
    $resolvedCount = if ($resolvedCounts.ContainsKey($identity)) { [int] $resolvedCounts[$identity] } else { 0 }
    if ($observedCount -ne 1 -or $resolvedCount -ne 1) {
        $unpairedCollisionCount++
        $failures.Add("Collision $identity has $observedCount observed and $resolvedCount resolved records; expected 1 and 1.")
    }
}

if ($allCollisionIdentities.Count -eq 0) {
    $failures.Add('The report contains no collision evidence.')
}

$missingBindingContext = @($observed + $resolved | Where-Object {
    $null -eq (Get-RecordValue -Record $_ -Name 'shotBindingMatched') -or
    [string]::IsNullOrWhiteSpace([string] (Get-RecordValue -Record $_ -Name 'contextSource'))
})
if ($missingBindingContext.Count -gt 0) {
    $failures.Add("$($missingBindingContext.Count) collision records are missing shotBindingMatched or contextSource.")
}

$numericRunaway = @($records | Where-Object { (Get-RecordValue -Record $_ -Name 'event') -eq 'numeric-runaway' })
$runtimeErrors = @($records | Where-Object { (Get-RecordValue -Record $_ -Name 'event') -eq 'runtime-error' })
$terminalMissing = @($records | Where-Object { (Get-RecordValue -Record $_ -Name 'event') -eq 'terminal-missing' })
$terminalDuplicate = @($records | Where-Object { (Get-RecordValue -Record $_ -Name 'event') -eq 'terminal-duplicate' })
$targetSpall = @($observed + $resolved | Where-Object { (Get-RecordValue -Record $_ -Name 'projectileKind') -eq 'TargetSpall' })

if ($numericRunaway.Count -gt 0) {
    $failures.Add("The report contains $($numericRunaway.Count) numeric-runaway records.")
}
if ($runtimeErrors.Count -gt 0) {
    $failures.Add("The report contains $($runtimeErrors.Count) runtime-error records.")
}
if ($terminalMissing.Count -gt 0) {
    $failures.Add("The report contains $($terminalMissing.Count) terminal-missing records.")
}
if ($terminalDuplicate.Count -gt 0) {
    $failures.Add("The report contains $($terminalDuplicate.Count) terminal-duplicate records.")
}
if ($targetSpall.Count -eq 0) {
    $failures.Add('The report contains no target-spall collision telemetry.')
}

if ($null -ne $sessionEnd) {
    foreach ($counterName in @('droppedEventCount', 'suppressedEventCount', 'recorderErrorCount')) {
        $counterValue = Get-RecordValue -Record $sessionEnd -Name $counterName
        if ($null -eq $counterValue -or [long] $counterValue -ne 0) {
            $failures.Add("session-end $counterName is $counterValue; expected 0.")
        }
    }

    $reportTruncated = Get-RecordValue -Record $sessionEnd -Name 'reportTruncated'
    if ($reportTruncated -ne $false) {
        $failures.Add("session-end reportTruncated is $reportTruncated; expected false.")
    }
}

$manualChecks = [ordered]@{
    ActorVisualConfirmed = [bool] $ActorVisualConfirmed
    WorldSurfaceVisualConfirmed = [bool] $WorldSurfaceVisualConfirmed
    MovingDoorVisualConfirmed = [bool] $MovingDoorVisualConfirmed
    TargetSpallVisualConfirmed = [bool] $TargetSpallVisualConfirmed
}

foreach ($manualCheck in $manualChecks.GetEnumerator()) {
    if (-not $manualCheck.Value) {
        $failures.Add("Manual acceptance is missing: $($manualCheck.Key).")
    }
}

$result = [pscustomobject]@{
    Status = if ($failures.Count -eq 0) { 'PASS' } else { 'FAIL' }
    Report = $resolvedPath
    ReportSHA256 = (Get-FileHash -LiteralPath $resolvedPath -Algorithm SHA256).Hash
    RecordCount = $records.Count
    SptVersion = if ($null -ne $sessionStart) { Get-RecordValue -Record $sessionStart -Name 'sptVersion' } else { $null }
    RunningDllSHA256 = if ($null -ne $sessionStart) { Get-RecordValue -Record $sessionStart -Name 'runningDllSha256' } else { $null }
    CollisionObserved = $observed.Count
    CollisionResolved = $resolved.Count
    UnpairedCollisionIdentities = $unpairedCollisionCount
    NumericRunaway = $numericRunaway.Count
    RuntimeErrors = $runtimeErrors.Count
    TerminalMissing = $terminalMissing.Count
    TerminalDuplicate = $terminalDuplicate.Count
    TargetSpallCollisionRecords = $targetSpall.Count
    ManualChecks = $manualChecks
    Failures = @($failures)
}

$result | ConvertTo-Json -Depth 6
if ($failures.Count -gt 0) {
    exit 1
}

