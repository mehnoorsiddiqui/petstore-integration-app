<#
.SYNOPSIS
  Install a Claude-format Context Plugin into this folder for OpenCode.

.DESCRIPTION
  OpenCode cannot install a Claude Code plugin by name, so this does it by hand, the same way the
  plugin-experiments harness does (test-suite/scripts/new-run.ps1):

    * every skill the plugin's .claude-plugin/plugin.json declares -> .opencode/skills/<skill>/
    * every agent it declares -> .opencode/agents/<name>.md, as an OpenCode subagent
      (Claude frontmatter replaced by OpenCode frontmatter; the body is copied unchanged)
    * what was installed, and from which commit -> .opencode/plugin-pin.json

  The plugin is fetched at an exact commit, so a reinstall is reproducible. Re-running replaces
  whatever an earlier install put there.
#>
param(
    [string]$Repo = 'https://github.com/mehnoorsiddiqui/petstore-context-plugin',
    [string]$Ref  = 'b5eee98a3e7fe1fbbc1af1872134ffaf011f44dc'
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

# ---- 1. Fetch the plugin at the exact commit, into temp (never into this folder) -----------
$src = Join-Path ([IO.Path]::GetTempPath()) "opencode-plugin-src\$($Ref.Substring(0, [Math]::Min(12, $Ref.Length)))"
if (Test-Path $src) { Remove-Item $src -Recurse -Force }
New-Item -ItemType Directory -Force $src | Out-Null
git -C $src init -q
git -C $src remote add origin $Repo
git -C $src fetch -q --depth 1 origin $Ref
if ($LASTEXITCODE -ne 0) { throw "Could not fetch $Ref from $Repo." }
git -C $src checkout -q FETCH_HEAD
$commit = (git -C $src rev-parse HEAD).Trim()

$manifestPath = Join-Path $src '.claude-plugin\plugin.json'
if (-not (Test-Path $manifestPath)) { throw "No .claude-plugin/plugin.json in $Repo at $Ref." }
$manifest = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json

# ---- 2. Skills ------------------------------------------------------------------------------
$skillsDst = Join-Path $root '.opencode\skills'
if (Test-Path $skillsDst) { Remove-Item $skillsDst -Recurse -Force }
New-Item -ItemType Directory -Force $skillsDst | Out-Null

$skills = @()
foreach ($decl in @($manifest.skills)) {
    $dir = Join-Path $src (($decl -replace '^\./', '') -replace '/', '\')
    if (-not (Test-Path $dir)) { throw "plugin.json declares skill path '$decl', which does not exist." }
    # A declared path is either one skill (holds SKILL.md) or a folder of skills (skills/dotnet).
    # Flatten to .opencode/skills/<skill>/ so every skill sits where OpenCode looks for it.
    $skillDirs = if (Test-Path (Join-Path $dir 'SKILL.md')) { @(Get-Item $dir) }
                 else { @(Get-ChildItem $dir -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'SKILL.md') }) }
    if ($skillDirs.Count -eq 0) { throw "Skill path '$decl' holds no SKILL.md." }
    foreach ($s in $skillDirs) {
        Copy-Item $s.FullName (Join-Path $skillsDst $s.Name) -Recurse -Force
        $skills += $s.Name
    }
}
if ($skills.Count -eq 0) { throw 'The plugin declares no skills: nothing would be installed.' }

# ---- 3. Agents ------------------------------------------------------------------------------
$agentsDst = Join-Path $root '.opencode\agents'
if (Test-Path $agentsDst) { Remove-Item $agentsDst -Recurse -Force }
$agents = @()
foreach ($decl in @($manifest.agents)) {
    if (-not $decl) { continue }
    $file = Join-Path $src (($decl -replace '^\./', '') -replace '/', '\')
    if (-not (Test-Path $file)) { throw "plugin.json declares agent '$decl', which does not exist." }
    $text = (Get-Content $file -Raw -Encoding UTF8).TrimStart([char]0xFEFF)
    $m = [regex]::Match($text, '^---\r?\n(.*?)\r?\n---\r?\n(.*)$', 'Singleline')
    if (-not $m.Success) { throw "Agent file $decl has no frontmatter." }
    $front = $m.Groups[1].Value
    $body  = $m.Groups[2].Value
    $name  = [regex]::Match($front, '(?m)^name:\s*"?([^"\r\n]+)"?\s*$').Groups[1].Value
    $desc  = [regex]::Match($front, '(?m)^description:\s*"(.*)"\s*$').Groups[1].Value -replace '\\"', '"'
    if (-not $name) { $name = [IO.Path]::GetFileNameWithoutExtension($file) }
    if (-not $desc) { throw "Agent $name has no description; OpenCode would not know when to use it." }
    if (-not $body.Trim()) { throw "Agent $name has an empty body; the subagent would get a blank brief." }
    # Claude's `model: inherit` and `tools:` lines are dropped: an OpenCode subagent with no model
    # runs on the model of the agent that started it, and gets the default tool set.
    $yamlDesc = "'" + ($desc -replace "'", "''") + "'"
    New-Item -ItemType Directory -Force $agentsDst | Out-Null
    $out = "---`ndescription: $yamlDesc`nmode: subagent`n---`n$body"
    [IO.File]::WriteAllText((Join-Path $agentsDst "$name.md"), $out, (New-Object Text.UTF8Encoding $false))
    $agents += $name
}

# ---- 4. Record what was installed -----------------------------------------------------------
$pin = [ordered]@{
    plugin      = $manifest.name
    version     = $manifest.version
    repo        = $Repo
    commit      = $commit
    installedAt = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    skills      = $skills
    agents      = $agents
}
$pin | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $root '.opencode\plugin-pin.json') -Encoding UTF8

Remove-Item $src -Recurse -Force
Write-Host "Installed $($manifest.name) $($manifest.version) @ $($commit.Substring(0,12)): $($skills.Count) skills, $($agents.Count) agent(s)."
Write-Host "Check with:  opencode debug skill   and   opencode debug agent <name>"
