# Builds the Battlegrounds Tracker.
#
# The project targets .NET Framework 4.7.2 with WPF. There is no Visual Studio IDE required, but the
# .NET SDK (for Microsoft.NET.Sdk.WindowsDesktop) and MSBuild are. The script locates both automatically.

param(
	[ValidateSet("Debug", "Release")]
	[string]$Configuration = "Debug",
	[switch]$SkipRestore
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$project = Join-Path $root "BattlegroundsTracker\BattlegroundsTracker.csproj"

# --- locate the .NET SDK (provides Microsoft.NET.Sdk.WindowsDesktop) ---------------------------------
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if(-not $dotnet)
{
	throw "dotnet SDK not found. Install the .NET SDK (8.0 or later) and make sure 'dotnet' is on PATH."
}

$sdkLine = dotnet --list-sdks | Select-Object -Last 1
if(-not $sdkLine)
{
	throw "No .NET SDK installed. Install the .NET SDK (8.0 or later)."
}

$sdkVersion = ($sdkLine -split "\s+")[0]
$sdkRoot = [regex]::Match($sdkLine, "\[(.+)\]").Groups[1].Value
$env:MSBuildSDKsPath = Join-Path $sdkRoot "$sdkVersion\Sdks"
# Workloads are resolved through the SDK's resolver, which the full framework MSBuild cannot use.
$env:MSBuildEnableWorkloadResolver = "false"

# --- locate MSBuild (the 64-bit variant of the VS Build Tools) ---------------------------------------
$msbuild = $null
$vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
if(Test-Path $vswhere)
{
	$msbuild = & $vswhere -latest -products * -find "MSBuild\**\Bin\amd64\MSBuild.exe" | Select-Object -First 1
}
if(-not $msbuild)
{
	$msbuild = (Get-Command msbuild -ErrorAction SilentlyContinue).Source
}
if(-not $msbuild)
{
	throw "MSBuild not found. Install the Visual Studio Build Tools (with the .NET Framework 4.7.2 targeting pack)."
}

Write-Host "MSBuild: $msbuild"
Write-Host "SDK:     $env:MSBuildSDKsPath"

$arguments = @(
	$project
	"/p:Platform=x64"
	"/p:Configuration=$Configuration"
	"/v:m"
	"/nologo"
)
if(-not $SkipRestore)
{
	$arguments += "/restore"
}

& $msbuild @arguments
if($LASTEXITCODE -ne 0)
{
	Write-Host ""
	Write-Host "Build failed." -ForegroundColor Red
	Write-Host "If the build failed with MSB3821 ('Couldn't process file ... due to its being in the Internet" -ForegroundColor Yellow
	Write-Host "or Restricted zone'), the repository is marked with the mark of the web. Trust the files or set" -ForegroundColor Yellow
	Write-Host "HKLM\SOFTWARE\Microsoft\.NETFramework\SDK\AllowProcessOfUntrustedResourceFiles to 'true'." -ForegroundColor Yellow
	exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Output: $(Join-Path $root "BattlegroundsTracker\bin\x64\$Configuration\BattlegroundsTracker.exe")"
