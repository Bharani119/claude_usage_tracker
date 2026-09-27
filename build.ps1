# Builds ClaudeUsageTray.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
# No SDK, NuGet packages or other downloads needed.
#
#   build.ps1                   build using the committed assets\app.ico
#   build.ps1 -RegenerateIcon   redraw assets\app.ico from tools\make-icon.ps1 first
#                               (after changing the icon design; commit the new .ico)
param([switch]$RegenerateIcon)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "C# compiler not found at $csc" }

$out = Join-Path $root 'bin'
New-Item -ItemType Directory -Force $out | Out-Null

$icon = Join-Path $root 'assets\app.ico'
if ($RegenerateIcon -or -not (Test-Path $icon)) {
    & (Join-Path $root 'tools\make-icon.ps1') -OutFile $icon
    Write-Host "Generated $icon"
}

& $csc /nologo /target:winexe /optimize+ /warn:4 /codepage:65001 `
    "/out:$out\ClaudeUsageTray.exe" "/win32icon:$icon" `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll `
    (Join-Path $root 'src\*.cs')
if ($LASTEXITCODE -ne 0) { throw "Build failed (csc exit code $LASTEXITCODE)" }
Write-Host "Built $out\ClaudeUsageTray.exe"
