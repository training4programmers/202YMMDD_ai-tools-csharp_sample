#Requires -Version 7
<#
.SYNOPSIS
    Agent hook: runs `dotnet format` on the C# files that changed since this hook last ran.

.DESCRIPTION
    Claude Code (PostToolUse on Edit|Write) and GitHub Copilot (postToolUse) both call this script after
    the agent uses a tool. Their JSON payloads differ, so the script ignores the payload and asks git which
    .cs files changed instead. A stamp file in .git/ remembers the last run, so calls after a read or a
    search find nothing new and return in well under a second.

    Exit code 2 sends the error text back to the agent (Claude Code feeds stderr to the model).
#>
$null = [Console]::In.ReadToEnd()   # drain the payload the agent wrote to stdin

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$gitDir = git rev-parse --git-dir 2>$null
if (-not $gitDir) { exit 0 }   # not a git repo: nothing to compare against

$stamp = Join-Path $gitDir 'format-hook.stamp'
$since = (Test-Path $stamp) ? (Get-Item $stamp).LastWriteTimeUtc : [datetime]::MinValue

$changed = @(git diff --name-only --relative --diff-filter=d HEAD -- '*.cs' 2>$null) +
    @(git ls-files --others --exclude-standard -- '*.cs' 2>$null)
$files = @($changed | Sort-Object -Unique | Where-Object { (Test-Path $_) -and (Get-Item $_).LastWriteTimeUtc -gt $since })
if ($files.Count -eq 0) { exit 0 }

$solution = Get-ChildItem -Path $root -Filter '*.slnx' | Select-Object -First 1
dotnet format $solution.Name --include @files --verbosity quiet
$formatExit = $LASTEXITCODE
New-Item -ItemType File -Path $stamp -Force | Out-Null   # the stamp is an empty marker file

if ($formatExit -ne 0) {
    [Console]::Error.WriteLine("dotnet format failed (exit $formatExit) for: $($files -join ', ')")
    exit 2
}
exit 0
