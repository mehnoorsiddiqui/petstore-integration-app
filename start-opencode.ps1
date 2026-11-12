<#
.SYNOPSIS
  Start OpenCode in this folder on an open model, with only the Petstore plugin loaded.

.EXAMPLE
  $env:TOGETHER_API_KEY = '<key>'
  .\start-opencode.ps1                                   # Kimi K3 on Together
  .\start-opencode.ps1 -Model together/zai-org/GLM-5.3   # any model in opencode.json
  .\start-opencode.ps1 -Model openrouter/<slug>          # needs OPENROUTER_API_KEY
#>
param(
    [string]$Model = 'together/moonshotai/Kimi-K3'
)
$ErrorActionPreference = 'Stop'

$keyVar = switch -Regex ($Model) {
    '^together/'   { 'TOGETHER_API_KEY' }
    '^openrouter/' { 'OPENROUTER_API_KEY' }
    default        { $null }
}
if ($keyVar -and -not [Environment]::GetEnvironmentVariable($keyVar)) {
    throw "Set `$env:$keyVar first. Without it the first model call fails with a 401."
}
if (-not (Test-Path (Join-Path $PSScriptRoot '.opencode\plugin-pin.json'))) {
    throw 'The plugin is not installed. Run .\install-plugin.ps1 first.'
}

# Keep everything except this folder's plugin out of the session:
#   your ~/.claude/skills and ~/.agents/skills, and ~/.claude/CLAUDE.md as an instruction file.
$env:OPENCODE_DISABLE_CLAUDE_CODE_SKILLS = '1'
$env:OPENCODE_DISABLE_EXTERNAL_SKILLS    = '1'
$env:OPENCODE_DISABLE_CLAUDE_CODE_PROMPT = '1'
$env:OPENCODE_DISABLE_AUTOUPDATE         = '1'

Set-Location $PSScriptRoot
opencode $PSScriptRoot -m $Model
