<#
.SYNOPSIS
    Builds and starts the Battlegrounds Tracker with a single command.

.DESCRIPTION
    Runs the build (build.ps1) and launches the resulting BattlegroundsTracker.exe.
    Works from the repository root of the standalone project as well as from the
    Battlegrounds Tracker folder inside the Hearthstone Deck Tracker fork.

    A running tracker locks its assemblies, so it is stopped before the build and
    the freshly built executable is started afterwards. Use -NoBuild to start the
    existing executable without rebuilding (an already running instance is then
    activated instead of being restarted).

.PARAMETER Configuration
    Build configuration to use: Debug (default) or Release.

.PARAMETER NoBuild
    Skip the build and only start the existing executable.

.PARAMETER Wait
    Wait for the tracker to exit and return its exit code (useful for debugging).

.EXAMPLE
    .\run.cmd

.EXAMPLE
    .\run.cmd Release

.EXAMPLE
    .\run.cmd -NoBuild
#>
param(
	[ValidateSet("Debug", "Release")]
	[string]$Configuration = "Debug",
	[switch]$NoBuild,
	[switch]$Wait
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

# --- locate the project (standalone repo layout or the HDT fork layout) ------------------------------
$projectCandidates = @(
	(Join-Path $root "BattlegroundsTracker\BattlegroundsTracker.csproj"),
	(Join-Path $root "Battlegrounds Tracker\Battlegrounds Tracker.csproj"),
	(Join-Path $root "BattlegroundsTracker.csproj"),
	(Join-Path $root "Battlegrounds Tracker.csproj")
)
$project = $projectCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if(-not $project)
{
	$project = Get-ChildItem -Path $root -Recurse -Depth 3 -Filter "*.csproj" -ErrorAction SilentlyContinue |
		Where-Object { $_.Name -match '^Battlegrounds' -and $_.FullName -notmatch '\\(bin|obj)\\' } |
		Select-Object -First 1 -ExpandProperty FullName
}
if(-not $project)
{
	Write-Host "Could not find the BattlegroundsTracker.csproj under $root" -ForegroundColor Red
	exit 1
}
$projectDir = Split-Path $project -Parent

# The build script lives in the repository root (standalone) or next to the project (HDT fork).
$buildScript = Join-Path $root "build.ps1"
if(-not (Test-Path $buildScript))
{
	$buildScript = Join-Path $projectDir "build.ps1"
}

$exe = Join-Path $projectDir "bin\x64\$Configuration\BattlegroundsTracker.exe"

function Stop-RunningTracker
{
	$running = @(Get-Process -Name "BattlegroundsTracker" -ErrorAction SilentlyContinue)
	if($running.Count -eq 0)
	{
		return
	}
	Write-Host "Stopping the running instance so the new build is used..." -ForegroundColor Yellow
	$running | Stop-Process -Force -ErrorAction SilentlyContinue
	Start-Sleep -Milliseconds 800
}

# --- build ------------------------------------------------------------------------------------------
$stoppedForBuild = $false
if(-not $NoBuild)
{
	if(@(Get-Process -Name "BattlegroundsTracker" -ErrorAction SilentlyContinue).Count -gt 0)
	{
		# A running tracker keeps HearthWatcher.dll and the exe locked, so stop it before building.
		Stop-RunningTracker
		$stoppedForBuild = $true
	}

	Write-Host "Building Battlegrounds Tracker ($Configuration)..." -ForegroundColor Cyan
	if(Test-Path $buildScript)
	{
		& powershell -NoProfile -ExecutionPolicy Bypass -File $buildScript -Configuration $Configuration
		if($LASTEXITCODE -ne 0)
		{
			Write-Host "Build failed. The tracker was stopped for the build; fix the errors and run this script again." -ForegroundColor Red
			exit $LASTEXITCODE
		}
	}
	else
	{
		# Fall back to MSBuild directly when the build script is missing.
		Write-Host "build.ps1 not found, building the project directly..." -ForegroundColor Yellow
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
			Write-Host "MSBuild not found. Please build with build.cmd instead." -ForegroundColor Red
			exit 1
		}
		$sdkLine = dotnet --list-sdks | Select-Object -Last 1
		if($sdkLine)
		{
			$sdkVersion = ($sdkLine -split "\s+")[0]
			$sdkRoot = [regex]::Match($sdkLine, "\[(.+)\]").Groups[1].Value
			$env:MSBuildSDKsPath = Join-Path $sdkRoot "$sdkVersion\Sdks"
			$env:MSBuildEnableWorkloadResolver = "false"
		}
		& $msbuild $project /restore /p:Platform=x64 /p:Configuration=$Configuration /v:m /nologo
		if($LASTEXITCODE -ne 0)
		{
			Write-Host "Build failed. The tracker was stopped for the build; fix the errors and run this script again." -ForegroundColor Red
			exit $LASTEXITCODE
		}
	}
}

# --- start ------------------------------------------------------------------------------------------
if(-not (Test-Path $exe))
{
	Write-Host "Executable not found: $exe" -ForegroundColor Red
	Write-Host "Run without -NoBuild to build it first." -ForegroundColor Yellow
	exit 1
}

if(-not $NoBuild -and -not $stoppedForBuild)
{
	Stop-RunningTracker
}
elseif($NoBuild -and @(Get-Process -Name "BattlegroundsTracker" -ErrorAction SilentlyContinue).Count -gt 0)
{
	Write-Host "Battlegrounds Tracker is already running; activating the existing window." -ForegroundColor Yellow
}

Write-Host "Starting $exe" -ForegroundColor Green
$process = Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe) -PassThru
if($Wait)
{
	$process.WaitForExit()
	exit $process.ExitCode
}
