param(
    [string]$RuntimeSkillsPath = "$env:USERPROFILE\.codex\skills"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$sourceRoot = Join-Path $repoRoot "skills"

if (-not (Test-Path -LiteralPath $sourceRoot -PathType Container)) {
    throw "Source skills directory not found: $sourceRoot"
}

if (-not (Test-Path -LiteralPath $RuntimeSkillsPath -PathType Container)) {
    New-Item -ItemType Directory -Path $RuntimeSkillsPath | Out-Null
}

$resolvedRuntime = (Resolve-Path -LiteralPath $RuntimeSkillsPath).Path
$skills = Get-ChildItem -LiteralPath $sourceRoot -Directory

foreach ($skill in $skills) {
    $manifest = Join-Path $skill.FullName "SKILL.md"

    if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) {
        Write-Warning "Skipping $($skill.Name): SKILL.md not found."
        continue
    }

    $target = Join-Path $resolvedRuntime $skill.Name

    if (Test-Path -LiteralPath $target) {
        $resolvedTarget = (Resolve-Path -LiteralPath $target).Path

        if (-not $resolvedTarget.StartsWith($resolvedRuntime, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove path outside runtime skills directory: $resolvedTarget"
        }

        Remove-Item -LiteralPath $resolvedTarget -Recurse -Force
    }

    Copy-Item -LiteralPath $skill.FullName -Destination $target -Recurse
    Write-Host "Synced $($skill.Name)"
}
