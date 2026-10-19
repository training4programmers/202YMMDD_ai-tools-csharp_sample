#Requires -Version 7.3
<#
.SYNOPSIS
    Copies a demo or activity starter to a lab folder OUTSIDE the course repo and makes it a git repo.

.DESCRIPTION
    AI coding tools read instruction files from the folders around your code: Claude Code loads every
    CLAUDE.md from the current folder up to the drive root (and reads AGENTS.md when there is no
    CLAUDE.md), Copilot in VS Code reads AGENTS.md, and Visual Studio reads
    .github/copilot-instructions.md from the root of the git repository. The course repo has its own
    AGENTS.md, CLAUDE.md, and copilot-instructions.md (instructions for maintaining the courseware), so
    running an assistant inside the clone would quietly mix those into your lab.

    This script copies the starter (begin/) to ~/labs/<name>, runs `git init`, and commits a baseline.
    You get a clean workspace whose only instructions are the ones the lab ships with, and `git diff`
    shows exactly what you and your assistant changed. To start over, run it again with -Force.

.EXAMPLE
    ./start-lab.ps1 -Activity 12
    Copies activities/12-*/begin to ~/labs/activity-12-<slug>.

.EXAMPLE
    ./start-lab.ps1 -Activity 3 -Bonus
    Copies activities/bonus/03-*/begin to ~/labs/bonus-activity-03-<slug>.

.EXAMPLE
    ./start-lab.ps1 -Demo 14 -Open
    Copies demos/14-*/begin and opens the copy in VS Code (instructor use).

.EXAMPLE
    ./start-lab.ps1 -Activity 12 -Solution
    Copies the reference solution instead, to ~/labs/activity-12-<slug>-solution, for comparison.
#>
[CmdletBinding(DefaultParameterSetName = 'Activity')]
param(
    [Parameter(ParameterSetName = 'Activity', Mandatory, Position = 0)]
    [ValidateRange(1, 99)]
    [int] $Activity,

    [Parameter(ParameterSetName = 'Demo', Mandatory)]
    [ValidateRange(1, 99)]
    [int] $Demo,

    # Use the bonus tier (activities/bonus or demos/bonus).
    [switch] $Bonus,

    # Copy solution/ instead of begin/.
    [switch] $Solution,

    # Where lab folders are created. Must be outside the course repo.
    [string] $LabsRoot = (Join-Path $HOME 'labs'),

    # Replace the lab folder if it already exists (your changes there are lost).
    [switch] $Force,

    # Open the lab folder in VS Code when done.
    [switch] $Open
)

$ErrorActionPreference = 'Stop'

$courseware = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent $courseware

$kind = if ($PSCmdlet.ParameterSetName -eq 'Demo') { 'demo' } else { 'activity' }
$number = if ($kind -eq 'demo') { $Demo } else { $Activity }
$tierFolder = Join-Path $courseware ($kind -eq 'demo' ? 'demos' : 'activities')
if ($Bonus) { $tierFolder = Join-Path $tierFolder 'bonus' }

$pattern = '{0:D2}-*' -f $number
$match = @(Get-ChildItem -Path $tierFolder -Directory -Filter $pattern)
if ($match.Count -ne 1) {
    throw "Could not find exactly one folder matching '$pattern' in $tierFolder."
}
$pair = $match[0]

$sourceName = $Solution ? 'solution' : 'begin'
$source = Join-Path $pair.FullName $sourceName
if (-not (Test-Path $source)) {
    throw "$($pair.Name) has no $sourceName/ folder."
}

$prefix = ($Bonus ? 'bonus-' : '') + $kind
$labName = "$prefix-$($pair.Name)" + ($Solution ? '-solution' : '')
$labsFull = [System.IO.Path]::GetFullPath($LabsRoot)
$destination = Join-Path $labsFull $labName

# The whole point is to be outside the course repo, so refuse to create the lab inside it.
$repoFull = [System.IO.Path]::GetFullPath($repoRoot).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
if (($labsFull.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar).StartsWith($repoFull, [StringComparison]::OrdinalIgnoreCase)) {
    throw "LabsRoot ($labsFull) is inside the course repo. Choose a folder outside it, such as ~/labs."
}

# Build output nests deep (tests\X.Tests\bin\Debug\net10.0\runtimes\win-x64\native\…), and some native
# libraries (SQLite's e_sqlite3.dll) fail to load from very long paths on Windows.
if ($destination.Length -gt 120) {
    Write-Warning "Lab path is $($destination.Length) characters long. Some native libraries fail to load from deep paths on Windows; if tests fail with 'filename or extension is too long', use a shorter -LabsRoot such as C:\labs."
}

if (Test-Path $destination) {
    if (-not $Force) {
        throw "$destination already exists. Re-run with -Force to replace it (your changes there will be lost)."
    }
    Remove-Item -Recurse -Force $destination
}

New-Item -ItemType Directory -Force -Path $destination | Out-Null

# Copy everything except build output and IDE state, skipping those folders at every depth (the same
# excludes as start-lab.sh's tar). Copy-Item -Exclude only filters the top level, so walk the tree.
$skip = @('bin', 'obj', '.vs', 'packages', 'TestResults')
function Copy-LabTree([string] $from, [string] $to) {
    foreach ($item in Get-ChildItem -LiteralPath $from -Force) {
        $target = Join-Path $to $item.Name
        if ($item.PSIsContainer) {
            if ($item.Name -in $skip) { continue }
            New-Item -ItemType Directory -Force -Path $target | Out-Null
            Copy-LabTree $item.FullName $target
        }
        else {
            Copy-Item -LiteralPath $item.FullName -Destination $target -Force
        }
    }
}
Copy-LabTree $source $destination

# Make it a repository with a baseline commit, so `git diff` shows exactly what changed.
Push-Location $destination
try {
    git init --quiet --initial-branch=main
    if ($LASTEXITCODE -ne 0) { throw "git init failed in $destination (see git's message above). Is Git 2.28 or later installed?" }

    git -c core.safecrlf=false add --all
    if ($LASTEXITCODE -ne 0) { throw "git add failed in $destination (see git's message above)." }

    $identity = @()
    if (-not (git config user.email)) { $identity += @('-c', 'user.email=lab@example.invalid') }
    if (-not (git config user.name)) { $identity += @('-c', 'user.name=Course Lab') }
    # The baseline is a local snapshot, not your work, so it is never signed (no GPG/SSH key prompt).
    $message = "Baseline: $($pair.Name) ($sourceName)"
    git @identity -c commit.gpgsign=false commit --quiet --message $message
    if ($LASTEXITCODE -ne 0) {
        $retry = (@('git') + @($identity | ForEach-Object { $_ -match '\s' ? "`"$_`"" : $_ }) +
            @('-c', 'commit.gpgsign=false', 'commit', '-m', "`"$message`"")) -join ' '
        throw @"
The lab files were copied to $destination, but the baseline commit failed (see git's message above),
so 'git diff' has nothing to compare against yet.

The usual cause is a commit hook, such as a company secret scanner set with a global core.hooksPath.
Some labs plant FAKE secrets on purpose (Activity 24 ships a made-up connection string with a password,
Demo 24 a made-up API key; finding them is the lesson), and a scanner rightly flags them. Check with
your instructor, then make the baseline commit yourself in a way your organization's policy allows:

    cd "$destination"
    $retry

To start over instead, re-run this script with -Force.
"@
    }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "Lab ready: $destination" -ForegroundColor Green
Write-Host "  cd `"$destination`""
Write-Host "  dotnet build            # confirm the starter builds"
Write-Host "  git diff                # review what you and your assistant changed"
Write-Host "  git restore . ; git clean -fd   # throw away changes and return to the baseline"
Write-Host ""

if ($Open) {
    code $destination
}
