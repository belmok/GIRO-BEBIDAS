param([switch]$SemTestes)
$ErrorActionPreference = 'Stop'
$taskRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
Set-Location -LiteralPath $taskRoot
$env:DOTNET_CLI_HOME = Join-Path $taskRoot '.tools'
$env:NUGET_PACKAGES = Join-Path $taskRoot '.tools\packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:MSBuildEnableWorkloadResolver = 'false'
function Invoke-GiroDotnet([string[]]$TaskArguments) {
    & dotnet @TaskArguments
    if ($LASTEXITCODE -ne 0) { throw 'Não foi possível compilar ou verificar o projeto.' }
}
function Get-GiroOutput([string]$Relative) {
    $taskPath = [System.IO.Path]::GetFullPath((Join-Path $taskRoot $Relative))
    if (-not $taskPath.StartsWith($taskRoot.TrimEnd('\') + '\', [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Destino fora da pasta do projeto.' }
    return $taskPath
}
$taskVersion = ([xml][System.IO.File]::ReadAllText((Join-Path $taskRoot 'Directory.Build.props'))).Project.PropertyGroup.Version
$taskBuild = Get-GiroOutput ('artifacts\build\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$taskRelease = Get-GiroOutput ('artifacts\distribuicao\' + $taskVersion)
$taskApp = Join-Path $taskBuild 'aplicativo'
$taskUninstaller = Join-Path $taskBuild 'desinstalador'
$taskSetup = Join-Path $taskBuild 'instalador'
[System.IO.Directory]::CreateDirectory($taskRelease) | Out-Null
if (-not $SemTestes) { Invoke-GiroDotnet -TaskArguments @('run', '--project', 'tests/GiroBebidas.Tests/GiroBebidas.Tests.csproj', '-c', 'Release') }
$taskProjects = @('src/GiroBebidas.App/GiroBebidas.App.csproj', 'src/GiroBebidas.Uninstaller/GiroBebidas.Uninstaller.csproj', 'src/GiroBebidas.Setup/GiroBebidas.Setup.csproj')
foreach ($taskProject in $taskProjects) { Invoke-GiroDotnet -TaskArguments @('restore', $taskProject, '-r', 'win-x64', '-p:SelfContained=true', '--source', 'https://api.nuget.org/v3/index.json', '--nologo', '-m:1') }
$taskPublishOptions = @('-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '--no-restore', '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:EnableCompressionInSingleFile=true', '-p:DebugType=None', '-p:DebugSymbols=false', '--nologo', '-m:1', '-nodeReuse:false')
Invoke-GiroDotnet -TaskArguments (@('publish', $taskProjects[0], '-o', $taskApp) + $taskPublishOptions)
Invoke-GiroDotnet -TaskArguments (@('publish', $taskProjects[1], '-o', $taskUninstaller) + $taskPublishOptions)
[System.IO.File]::Copy((Join-Path $taskUninstaller 'DesinstalarGiroBebidas.exe'), (Join-Path $taskApp 'DesinstalarGiroBebidas.exe'), $true)
$taskPayload = Join-Path $taskBuild 'pacote.zip'
[System.IO.Compression.ZipFile]::CreateFromDirectory($taskApp, $taskPayload, [System.IO.Compression.CompressionLevel]::Optimal, $false)
Invoke-GiroDotnet -TaskArguments (@('publish', $taskProjects[2], '-o', $taskSetup, ('-p:PayloadPath=' + $taskPayload)) + $taskPublishOptions)
[System.IO.File]::Copy((Join-Path $taskSetup 'InstalarGiroBebidas.exe'), (Join-Path $taskRelease 'InstalarGiroBebidas.exe'), $true)
[System.IO.File]::Copy((Join-Path $taskUninstaller 'DesinstalarGiroBebidas.exe'), (Join-Path $taskRelease 'DesinstalarGiroBebidas.exe'), $true)
[System.IO.File]::Copy((Join-Path $taskApp 'GiroBebidas.exe'), (Join-Path $taskRelease 'GiroBebidas.exe'), $true)
$taskPortableZip = Join-Path $taskRelease ('GiroBebidas-' + $taskVersion + '-portatil.zip')
if ([System.IO.File]::Exists($taskPortableZip)) { [System.IO.File]::Delete($taskPortableZip) }
[System.IO.Compression.ZipFile]::CreateFromDirectory($taskApp, $taskPortableZip, [System.IO.Compression.CompressionLevel]::Optimal, $false)
[System.IO.File]::Copy((Join-Path $taskRoot 'docs\GUIA-DO-USUARIO.md'), (Join-Path $taskRelease 'GUIA-DO-USUARIO.md'), $true)
$taskFiles = Get-ChildItem -LiteralPath $taskRelease -File | Where-Object { $_.Extension -in '.exe', '.zip' }
$taskHashes = foreach ($taskFile in $taskFiles) { $taskHash = Get-FileHash -LiteralPath $taskFile.FullName -Algorithm SHA256; $taskHash.Hash + '  ' + $taskFile.Name }
[System.IO.File]::WriteAllLines((Join-Path $taskRelease 'SHA256.txt'), [string[]]$taskHashes)
[System.IO.File]::Copy((Join-Path $taskRelease 'InstalarGiroBebidas.exe'), (Join-Path $taskRoot 'InstalarGiroBebidas.exe'), $true)
Write-Output ('Distribuição concluída: ' + $taskRelease)
