<#
.SYNOPSIS
    Creates desktop shortcuts for ScreenOff, with optional global hotkeys.
.DESCRIPTION
    Two shortcuts are made by default: one opens the delay picker, the other
    turns the display off straight away. A shortcut hotkey only works while the
    shortcut sits on the desktop or in the Start menu, so both go to the desktop.
.EXAMPLE
    .\install.ps1
.EXAMPLE
    .\install.ps1 -TimerHotkey 'Ctrl+Alt+S' -SkipImmediate
#>
[CmdletBinding()]
param(
    # Defaults are resolved in the body rather than here: $PSScriptRoot is not
    # populated while an advanced script's parameter defaults are evaluated, so
    # a Join-Path default fails outright under "powershell -File".
    [string] $ExePath,

    # Pass an empty string to create the shortcut without a hotkey.
    [string] $TimerHotkey = 'Ctrl+Alt+T',
    [string] $ImmediateHotkey = 'Ctrl+Alt+Q',

    [string] $TimerName = 'Screen Off Timer',
    [string] $ImmediateName = 'Screen Off',

    [switch] $SkipImmediate
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $ExePath) {
    $ExePath = Join-Path $root 'bin\ScreenOff.exe'
}

# Checked before Resolve-Path, which would otherwise throw its own error first
# and bury the hint about building.
if (-not (Test-Path -LiteralPath $ExePath)) {
    throw "Executable not found: $ExePath. Run build.ps1 first."
}
$exe = (Resolve-Path -LiteralPath $ExePath).Path

$desktop = [Environment]::GetFolderPath('Desktop')

function New-AppShortcut {
    param(
        [object] $Shell,
        [string] $Directory,
        [string] $Name,
        [string] $Arguments,
        [string] $Hotkey,
        [string] $Description,
        [string] $Target
    )

    $path = Join-Path $Directory ($Name + '.lnk')
    $link = $Shell.CreateShortcut($path)
    try {
        $link.TargetPath = $Target
        $link.Arguments = $Arguments
        $link.WorkingDirectory = Split-Path -Parent $Target
        $link.IconLocation = "$Target,0"
        $link.Description = $Description
        if ($Hotkey) {
            $link.Hotkey = $Hotkey
        }
        $link.Save()
    }
    finally {
        [System.Runtime.InteropServices.Marshal]::ReleaseComObject($link) | Out-Null
    }

    return $path
}

$created = @()
$shell = New-Object -ComObject WScript.Shell
try {
    $created += New-AppShortcut -Shell $shell -Directory $desktop -Name $TimerName `
        -Arguments '' -Hotkey $TimerHotkey -Target $exe `
        -Description 'Pick a delay, then turn the display off'

    if (-not $SkipImmediate) {
        $created += New-AppShortcut -Shell $shell -Directory $desktop -Name $ImmediateName `
            -Arguments '0' -Hotkey $ImmediateHotkey -Target $exe `
            -Description 'Turn the display off now; the system keeps running'
    }
}
finally {
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($shell) | Out-Null
}

foreach ($path in $created) {
    if (Test-Path -LiteralPath $path) {
        Write-Host "created : $path"
    }
    else {
        Write-Warning "shortcut did not persist: $path"
    }
}

Write-Host 'Remove a shortcut by deleting its .lnk file from the desktop.'
