param([string]$GodotPath = $env:GODOT_PATH)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $GodotPath) {
    $command = Get-Command godot -ErrorAction SilentlyContinue
    if ($command) { $GodotPath = $command.Source }
}
if (-not $GodotPath -or -not (Test-Path -LiteralPath $GodotPath)) {
    throw 'Supply -GodotPath with the Godot .NET console executable, or set GODOT_PATH.'
}
$GodotPath = (Resolve-Path -LiteralPath $GodotPath).Path
$logRoot = Join-Path $projectRoot '.godot/test-results'
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
& dotnet build (Join-Path $projectRoot 'ProjectX.csproj')
if ($LASTEXITCODE -ne 0) { throw 'C# build failed.' }
$checks = @(
    @{ Name = 'import'; Args = @('--editor', '--import', '--quit'); Marker = '' },
    @{ Name = 'quest'; Args = @('res://tests/QuestFlowTests.tscn'); Marker = 'QUEST TESTS PASSED:' },
    @{ Name = 'minipuzzles'; Args = @('--script', 'res://tests/MiniPuzzleTests.gd'); Marker = 'MINIPUZZLE TESTS PASSED:' },
    @{ Name = 'world'; Args = @('res://tests/WorldSmokeTests.tscn'); Marker = 'WORLD TESTS PASSED:' },
    @{ Name = 'story'; Args = @('--script', 'res://tests/StoryFlowTests.gd'); Marker = 'STORY FLOW PASSED:' },
    @{ Name = 'scenes'; Args = @('--script', 'res://tests/SceneResourceTests.gd'); Marker = 'SCENE RESOURCE CHECKS PASSED:' }
)
foreach ($check in $checks) {
    $log = Join-Path $logRoot ($check.Name + '.log')
    $arguments = @('--headless', '--path', $projectRoot, '--quit-after', '7200', '--log-file', $log) + $check.Args
    # Debug prints on stderr (e.g. GD.PrintErr) must not abort the run; real
    # failures are detected below by exit code and ERROR markers in the log.
    $ErrorActionPreference = 'Continue'
    $output = & $GodotPath @arguments 2>&1 | Out-String
    $code = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    $record = if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Raw } else { $output }
    # Godot can return zero after a script error or timeout; require a success marker too.
    if ($code -ne 0 -or $record -match 'ERROR:|SCRIPT ERROR|Unhandled exception' -or ($check.Marker -and -not $record.Contains($check.Marker))) {
        Write-Output $record
        throw "Check '$($check.Name)' failed. See $log"
    }
    $summary = ($record -split "`n" | Where-Object { $_ -like '*PASSED:*' }) -join ' '
    Write-Output ("PASS: {0} {1}" -f $check.Name, $summary)
}
Write-Output "All checks passed. Logs: $logRoot"
