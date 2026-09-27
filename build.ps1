# Builds ClaudeUsageTray.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
# No SDK, NuGet packages or other downloads needed.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "C# compiler not found at $csc" }

$out = Join-Path $root 'bin'
New-Item -ItemType Directory -Force $out | Out-Null

& $csc /nologo /target:winexe /optimize+ /warn:4 /codepage:65001 `
    "/out:$out\ClaudeUsageTray.exe" `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll `
    (Join-Path $root 'src\*.cs')
if ($LASTEXITCODE -ne 0) { throw "Build failed (csc exit code $LASTEXITCODE)" }
Write-Host "Built $out\ClaudeUsageTray.exe"
