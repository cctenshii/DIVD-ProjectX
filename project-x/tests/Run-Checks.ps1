param(
    [Parameter(Mandatory = $true)] [string] $GodotPath,
    [string] $OutputDirectory = (Join-Path $env:TEMP 'ProjectX-checks')
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$engine = (Resolve-Path -LiteralPath $GodotPath).Path
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$outputPath = (Resolve-Path -LiteralPath $OutputDirectory).Path

# Run build -> imports -> scene contracts -> full combat -> real story transitions.
# Godot sometimes logs errors while returning exit code 0, so inspect the logs too.
Push-Location $projectRoot
try {
    & dotnet build --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'C# build failed.' }

    $checks = @(
        @{ Name = 'import'; Args = @('--editor', '--import', '--quit') },
        @{ Name = 'scenes'; Args = @('--script', 'res://tests/MergeSceneTests.gd', '--quit-after', '1800'); Success = 'MERGE SCENE TESTS COMPLETE: 0 failures' },
        @{ Name = 'world'; Args = @('res://tests/WorldSmokeTests.tscn', '--quit-after', '1800'); Success = 'WORLD TESTS PASSED:' },
        @{ Name = 'story'; Args = @('--script', 'res://tests/StoryFlowTests.gd', '--quit-after', '1800'); Success = 'STORY FLOW PASSED:' }
    )
    foreach ($check in $checks) {
        $logPath = Join-Path $outputPath ($check.Name + '.log')
        $engineArguments = @('--headless', '--path', $projectRoot, '--log-file', $logPath) + $check.Args
        & $engine @engineArguments | Out-Null
        $engineExitCode = $LASTEXITCODE
        $logText = Get-Content -LiteralPath $logPath -Raw
        if ($engineExitCode -ne 0 -or $logText -match '(?m)^(ERROR:|SCRIPT ERROR:|WARNING:)') {
            throw "Godot check '$($check.Name)' failed. See $logPath"
        }
        if ($check.Success -and -not $logText.Contains($check.Success)) {
            throw "Godot check '$($check.Name)' did not finish. See $logPath"
        }
        Write-Output "PASS: $($check.Name) ($logPath)"
    }
    Write-Output 'ALL PROJECT X CHECKS PASSED'
}
finally { Pop-Location }
