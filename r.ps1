[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = $PSScriptRoot
$serverProject = Join-Path $projectRoot 'Server_WPF\RemoteDesktopServer.csproj'
$serverDirectory = Join-Path $projectRoot 'Server_WPF'
$serverExecutable = Join-Path $serverDirectory 'bin\Debug\net8.0-windows\RemoteDesktopServer.exe'

$clientProject = Join-Path $projectRoot 'Client_WPF\RemoteDesktopClient.csproj'
$clientDirectory = Join-Path $projectRoot 'Client_WPF'
$clientExecutable = Join-Path $clientDirectory 'bin\Debug\net8.0-windows\RemoteDesktopClient.exe'

function Stop-RemoteMiniProcesses {
    Write-Host '[1/3] Stopping running Remote Mini processes...' -ForegroundColor Cyan

    Get-Process -Name 'RemoteDesktopServer' -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue

    Get-Process -Name 'RemoteDesktopClient' -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
}

function Build-DebugProjects {
    Write-Host '[2/3] Building latest WPF Server & WPF Client code...' -ForegroundColor Cyan

    Write-Host ' -> Building Server (WPF)...' -ForegroundColor Gray
    & dotnet build $serverProject --configuration Debug --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Server WPF Debug build failed with exit code $LASTEXITCODE."
    }

    Write-Host ' -> Building Client (WPF)...' -ForegroundColor Gray
    & dotnet build $clientProject --configuration Debug --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Client WPF Debug build failed with exit code $LASTEXITCODE."
    }

    if (-not (Test-Path -LiteralPath $serverExecutable)) {
        throw "Server executable not found: $serverExecutable"
    }

    if (-not (Test-Path -LiteralPath $clientExecutable)) {
        throw "Client executable not found: $clientExecutable"
    }
}

function Start-RemoteMini {
    Write-Host '[3/3] Starting WPF Server and WPF Client...' -ForegroundColor Cyan

    $serverProcess = Start-Process `
        -FilePath $serverExecutable `
        -WorkingDirectory $serverDirectory `
        -WindowStyle Normal `
        -PassThru

    Start-Sleep -Milliseconds 600

    $clientProcess = Start-Process `
        -FilePath $clientExecutable `
        -WorkingDirectory $clientDirectory `
        -WindowStyle Normal `
        -PassThru

    Write-Host ''
    Write-Host 'Remote Mini is running with 100% WPF .NET 8 (Server WPF + Client WPF).' -ForegroundColor Green
    Write-Host "Server PID: $($serverProcess.Id)"
    Write-Host "Client PID: $($clientProcess.Id)"
}

Stop-RemoteMiniProcesses
Build-DebugProjects
Start-RemoteMini
