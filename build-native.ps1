$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$outDir = Join-Path $root "native-dist"
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path $csc)) {
  $csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
}

if (-not (Test-Path $csc)) {
  throw "Cannot find .NET Framework C# compiler."
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

& $csc `
  /nologo `
  /target:winexe `
  /optimize+ `
  /win32icon:"$root\build\icon.ico" `
  /out:"$outDir\DNS Switcher Pro 1.0.exe" `
  /reference:System.dll `
  /reference:System.Core.dll `
  /reference:System.Drawing.dll `
  /reference:System.Windows.Forms.dll `
  "$root\native-windows\DnsSwitcherPro.cs"

Get-Item "$outDir\DNS Switcher Pro 1.0.exe"
