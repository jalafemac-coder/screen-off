<#
.SYNOPSIS
    Compiles ScreenOff.exe.
.DESCRIPTION
    Uses the C# compiler shipped with the .NET Framework, which is present on
    every supported Windows build, so no SDK or toolchain install is needed.
.EXAMPLE
    .\build.ps1
.EXAMPLE
    .\build.ps1 -Platform x64 -OutputDir D:\tools
#>
[CmdletBinding()]
param(
    # Defaults are resolved in the body rather than here: $PSScriptRoot is not
    # populated while an advanced script's parameter defaults are evaluated, so
    # a Join-Path default fails outright under "powershell -File".
    [string] $OutputDir,

    [ValidateSet('anycpu', 'x64', 'x86')]
    [string] $Platform = 'anycpu'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $OutputDir) {
    $OutputDir = Join-Path $root 'bin'
}

$compiler = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $compiler) {
    throw 'No .NET Framework C# compiler found under %WINDIR%\Microsoft.NET. Install .NET Framework 4.x.'
}

$sources = @(Get-ChildItem -Path (Join-Path $root 'src') -Filter '*.cs' |
             Sort-Object Name | ForEach-Object { $_.FullName })
if ($sources.Count -eq 0) {
    throw 'No source files found in src.'
}

$icon = Join-Path $root 'assets\ScreenOff.ico'
$manifest = Join-Path $root 'src\ScreenOff.manifest'
foreach ($required in @($icon, $manifest)) {
    if (-not (Test-Path $required)) {
        throw "Missing build input: $required"
    }
}

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}
$exe = Join-Path (Resolve-Path -LiteralPath $OutputDir).Path 'ScreenOff.exe'

$arguments = @(
    '/nologo'
    '/optimize+'
    '/target:winexe'
    "/platform:$Platform"
    '/codepage:65001'
    '/utf8output'
    "/win32icon:$icon"
    "/win32manifest:$manifest"
    '/r:System.dll'
    '/r:System.Drawing.dll'
    '/r:System.Windows.Forms.dll'
    "/out:$exe"
) + $sources

$sourceNames = ($sources | Split-Path -Leaf) -join ', '
Write-Host "compiler: $compiler"
Write-Host "sources : $sourceNames"

& $compiler @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE."
}

$built = Get-Item $exe
Write-Host ("built   : {0} ({1} bytes)" -f $built.FullName, $built.Length)
