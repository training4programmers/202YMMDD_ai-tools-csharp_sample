#Requires -Version 7
<#
.SYNOPSIS
    Claude Code PreToolUse guard: blocks edits to test files that already exist in git.

.DESCRIPTION
    Used while fixing failing tests, so the agent changes the code under test instead of the tests.
    New test files are allowed, so the agent can still add coverage. Exit code 2 blocks the tool call,
    and Claude Code shows the stderr text to the model as the reason. Any other failure in this script
    exits 1, which Claude Code reports but does not treat as a block.
#>
$ErrorActionPreference = 'Stop'

$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
$path = $payload.tool_input.file_path
if (-not $path) { exit 0 }

$root = Split-Path -Parent $PSScriptRoot
$relative = [System.IO.Path]::GetRelativePath($root, $path).Replace('\', '/')
if (-not $relative.StartsWith('tests/', [StringComparison]::OrdinalIgnoreCase)) { exit 0 }

# Exit code 0 means the file exists in the last commit, so it's an existing test.
git -C $root cat-file -e "HEAD:$relative" 2>$null
if ($LASTEXITCODE -eq 0) {
    [Console]::Error.WriteLine(
        "Blocked: $relative is an existing test, and this task says fix the code, not the tests. " +
        'Add new tests in a new file, or stop and explain why you think this test is wrong.')
    exit 2
}
exit 0
