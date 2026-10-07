[CmdletBinding()]
param([string]$Version = '1.0.0', [string]$InnoCompiler)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$payload = Join-Path $repo 'artifacts\payload'
$portable = Join-Path $repo 'artifacts\DesktopLayoutManager-Portable'
$dist = Join-Path $repo 'dist'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must use major.minor.patch format.' }
foreach ($directory in @($payload, $portable)) {
    $full = [IO.Path]::GetFullPath($directory)
    if (!$full.StartsWith([IO.Path]::GetFullPath((Join-Path $repo 'artifacts')) + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected staging directory.' }
    if (Test-Path -LiteralPath $full) { Remove-Item -LiteralPath $full -Recurse -Force }
}
New-Item -ItemType Directory -Path $dist -Force | Out-Null
& dotnet publish (Join-Path $repo 'Native\Native.csproj') -c Release -p:Platform=x64 -r win-x64 -p:PublishTrimmed=false -p:WindowsAppSDKSelfContained=true "-p:Version=$Version" -o $payload
if ($LASTEXITCODE -ne 0) { throw 'Application publish failed.' }
Copy-Item -LiteralPath (Join-Path $repo 'README.md') -Destination (Join-Path $payload 'README.md')
Copy-Item -LiteralPath $payload -Destination $portable -Recurse
[IO.File]::WriteAllText((Join-Path $portable 'portable.flag'), 'Store application data in the Data folder beside the executable.')
$zip = Join-Path $dist "DesktopLayoutManager-$Version-Portable-x64.zip"
Compress-Archive -Path (Join-Path $portable '*') -DestinationPath $zip -Force
if (!$InnoCompiler) {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { $InnoCompiler = $command.Source }
    else {
        $candidates = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe")
        $InnoCompiler = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    }
}
if (!$InnoCompiler) { throw 'Inno Setup compiler not found. Install Inno Setup, or pass -InnoCompiler with the full ISCC.exe path.' }
& $InnoCompiler "/DPayloadDir=$payload" "/DReleaseDir=$dist" "/DAppVersion=$Version" (Join-Path $PSScriptRoot 'DesktopLayoutManager.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$setup = Join-Path $dist "DesktopLayoutManager-$Version-Setup-x64.exe"
$hashLines = @($zip, $setup) | ForEach-Object { $hash = Get-FileHash -LiteralPath $_ -Algorithm SHA256; $hash.Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($_) }
[IO.File]::WriteAllLines((Join-Path $dist 'SHA256SUMS.txt'), $hashLines)
Get-Item -LiteralPath $zip, $setup | Select-Object Name, Length, FullName
