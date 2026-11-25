<#
Runs the same loose prompt through headless Claude Code twice - without and with the AEF MCP server - and scores both.

Each task is a real past commit: the prompt is the commit subject, the starting point is the commit's parent, and the
commit's own file list is the reference for "which files should change". Every run happens in a throwaway local clone,
so the source repositories are never touched.

  .\run.ps1 -TasksFile tasks.json -McpDll <path>\AefPlatform.Mcp.dll -OutDir <dir> [-Model sonnet] [-MaxBudgetUsd 1.5]

tasks.json: [{ "repo": "name", "root": "C:/path", "sha": "...", "parent": "...", "subject": "...", "files": ["..."] }]

What this cannot measure: live back-and-forth with a user. A headless run has no user, so the proxies are: files changed
that the real commit did not touch, real files missed, paths named in the final answer that do not exist, questions
asked, tool calls made, and cost.
#>
param(
    [Parameter(Mandatory)] [string] $TasksFile,
    [Parameter(Mandatory)] [string] $McpDll,
    [Parameter(Mandatory)] [string] $OutDir,
    [string] $Model = 'sonnet',
    [double] $MaxBudgetUsd = 1.5,
    [string[]] $Only = @()
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $OutDir | Out-Null
$tasks = Get-Content $TasksFile -Raw | ConvertFrom-Json
$Only = @($Only | ForEach-Object { $_ -split ',' } | Where-Object { $_ })   # -File passes "a,b" as one string
if ($Only.Count -gt 0) { $tasks = @($tasks | Where-Object { $Only -contains $_.sha.Substring(0, 7) }) }
if ($tasks.Count -eq 0) { throw 'No task matched.' }

$noServers = Join-Path $OutDir 'mcp-none.json'
'{"mcpServers":{}}' | Out-File $noServers -Encoding ascii
$aefServer = Join-Path $OutDir 'mcp-aef.json'
(@{ mcpServers = @{ aef = @{ command = 'dotnet'; args = @($McpDll) } } } | ConvertTo-Json -Depth 5) | Out-File $aefServer -Encoding ascii

function Invoke-Arm($task, [string] $arm) {
    $id = "$($task.repo)-$($task.sha.Substring(0, 7))-$arm"
    $clone = Join-Path $OutDir "clone-$id"
    if (Test-Path $clone) { Remove-Item $clone -Recurse -Force }
    git clone --quiet $task.root $clone
    git -C $clone checkout --quiet --detach $task.parent

    $prompt = $task.subject
    $mcp = $noServers
    $extra = @()
    if ($arm -eq 'aef') {
        # A tokensave git hook may already have indexed the clone on checkout; then refresh instead.
        cmd /c "cd /d `"$clone`" && (tokensave init >nul 2>&1 || tokensave sync >nul 2>&1)"
        Add-Content (Join-Path $clone '.git\info\exclude') '.tokensave'
        $mcp = $aefServer
        $extra = @('--allowedTools', 'mcp__aef__aef_refine_prompt', 'mcp__aef__aef_check_change')
        $root = $clone -replace '\\', '/'
        $prompt += "`n`nBefore editing, call the aef_refine_prompt tool with UserPrompt set to the request above, RepositoryName `"$($task.repo)`" and RepositoryRoot `"$root`". When you have finished, call aef_check_change with the same RepositoryName and RepositoryRoot and SkipBuildAndTests `"true`"."
    }

    $transcript = Join-Path $OutDir "$id.jsonl"
    $started = Get-Date
    Push-Location $clone
    try {
        $prompt | claude -p --verbose --output-format stream-json --model $Model --max-budget-usd $MaxBudgetUsd `
            --permission-mode acceptEdits --setting-sources project --strict-mcp-config --mcp-config $mcp @extra |
            Out-File $transcript -Encoding utf8
    }
    finally { Pop-Location }
    $seconds = [int]((Get-Date) - $started).TotalSeconds

    $events = Get-Content $transcript | Where-Object { $_.Trim().StartsWith('{') } | ForEach-Object { try { $_ | ConvertFrom-Json } catch { } }
    $result = $events | Where-Object { $_.type -eq 'result' } | Select-Object -Last 1
    $toolCalls = @($events | Where-Object { $_.type -eq 'assistant' } | ForEach-Object { $_.message.content } | Where-Object { $_.type -eq 'tool_use' } | ForEach-Object { $_.name })
    $finalText = [string]$result.result

    $changed = @(git -C $clone status --porcelain --untracked-files=all | ForEach-Object { $_.Substring(3).Trim('"') })
    $real = @($task.files)
    $mentioned = @([regex]::Matches($finalText, '[\w./\\-]+\.(py|kt|kts|ts|tsx|js|md|json|xml|yml|yaml|toml|cs|gradle)\b') | ForEach-Object { $_.Value.Trim('.') } | Sort-Object -Unique)
    $missing = @($mentioned | Where-Object { -not (Test-Path (Join-Path $clone $_)) -and -not (Get-ChildItem $clone -Recurse -File -Filter (Split-Path $_ -Leaf) -ErrorAction SilentlyContinue | Select-Object -First 1) })

    git -C $clone diff | Out-File (Join-Path $OutDir "$id.diff") -Encoding utf8
    Remove-Item $clone -Recurse -Force

    [pscustomobject]@{
        task               = "$($task.repo):$($task.sha.Substring(0, 7))"
        arm                = $arm
        subject            = $task.subject
        realFiles          = $real.Count
        changedFiles       = $changed.Count
        changedThatAreReal = @($changed | Where-Object { $real -contains $_ }).Count
        changedNotInReal   = @($changed | Where-Object { $real -notcontains $_ }).Count
        pathsNamed         = $mentioned.Count
        pathsThatDoNotExist = $missing.Count
        questionsAsked     = ([regex]::Matches($finalText, '\?')).Count
        toolCalls          = $toolCalls.Count
        aefToolCalls       = @($toolCalls | Where-Object { $_ -like 'mcp__aef__*' }).Count
        turns              = $result.num_turns
        costUsd            = [math]::Round([double]$result.total_cost_usd, 3)
        seconds            = $seconds
        stopped            = $result.subtype
        finalAnswer        = $finalText
    }
}

$rows = foreach ($task in $tasks) {
    foreach ($arm in 'baseline', 'aef') {
        $row = Invoke-Arm $task $arm
        # Saved per run so one failure does not lose the runs already paid for.
        $row | ConvertTo-Json -Depth 4 | Out-File (Join-Path $OutDir "row-$($task.repo)-$($task.sha.Substring(0, 7))-$arm.json") -Encoding utf8
        $row
    }
}
$rows | ConvertTo-Json -Depth 4 | Out-File (Join-Path $OutDir 'results.json') -Encoding utf8
$rows | Select-Object task, arm, realFiles, changedFiles, changedThatAreReal, changedNotInReal, pathsThatDoNotExist, questionsAsked, toolCalls, aefToolCalls, costUsd, seconds, stopped | Format-Table -AutoSize
