param([string]$Versao = '')
$ErrorActionPreference = 'Stop'
$taskRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if ([string]::IsNullOrWhiteSpace($Versao)) { $Versao = ([xml][System.IO.File]::ReadAllText((Join-Path $taskRoot 'Directory.Build.props'))).Project.PropertyGroup.Version }
Set-Location -LiteralPath $taskRoot
$taskRelease = [System.IO.Path]::GetFullPath((Join-Path $taskRoot ('artifacts\distribuicao\' + $Versao)))
if (-not $taskRelease.StartsWith($taskRoot.TrimEnd('\') + '\', [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Destino inválido.' }
$taskOutput = Join-Path $taskRoot ('artifacts\verificacao\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$taskScreens = Join-Path $taskOutput 'telas'
$taskInstalled = Join-Path $taskOutput 'instalacao-teste'
$taskData = Join-Path $taskOutput 'dados-teste'
[System.IO.Directory]::CreateDirectory($taskScreens) | Out-Null
$env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = Join-Path $taskRoot 'artifacts\runtime-cache'
function Invoke-GiroCheck([string]$TaskExecutable, [string[]]$TaskArguments) {
    $taskStart = [System.Diagnostics.ProcessStartInfo]::new()
    $taskStart.FileName = $TaskExecutable
    $taskStart.UseShellExecute = $false
    $taskStart.CreateNoWindow = $true
    $taskStart.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
    foreach ($taskArgument in $TaskArguments) { $taskStart.ArgumentList.Add($taskArgument) }
    $taskProcess = [System.Diagnostics.Process]::Start($taskStart)
    if (-not $taskProcess.WaitForExit(45000)) { $taskProcess.Kill($true); throw 'A verificação excedeu o tempo esperado.' }
    if ($taskProcess.ExitCode -ne 0) { throw ('A verificação do programa falhou: ' + $TaskExecutable) }
}
Invoke-GiroCheck -TaskExecutable (Join-Path $taskRelease 'InstalarGiroBebidas.exe') -TaskArguments @('--ui-smoke', (Join-Path $taskScreens '13-instalador.png'))
Write-Output 'OK: tela do instalador independente.'
Invoke-GiroCheck -TaskExecutable (Join-Path $taskRelease 'DesinstalarGiroBebidas.exe') -TaskArguments @('--ui-smoke', (Join-Path $taskScreens '14-desinstalador.png'))
Write-Output 'OK: tela do desinstalador independente.'
Invoke-GiroCheck -TaskExecutable (Join-Path $taskRelease 'InstalarGiroBebidas.exe') -TaskArguments @('--verify-package', $taskInstalled)
if (-not (Test-Path -LiteralPath (Join-Path $taskInstalled '.giro-instalacao.json'))) { throw 'Manifesto não encontrado.' }
Write-Output 'OK: pacote extraído em pasta isolada, sem registro ou atalhos no Windows.'
$taskSourceHash = (Get-FileHash -LiteralPath (Join-Path $taskRelease 'GiroBebidas.exe') -Algorithm SHA256).Hash
$taskInstalledHash = (Get-FileHash -LiteralPath (Join-Path $taskInstalled 'GiroBebidas.exe') -Algorithm SHA256).Hash
if ($taskSourceHash -ne $taskInstalledHash) { throw 'Executável extraído não confere com a distribuição.' }
Invoke-GiroCheck -TaskExecutable (Join-Path $taskInstalled 'GiroBebidas.exe') -TaskArguments @('--ui-smoke', '--data-dir', $taskData, '--output', $taskScreens)
if (-not (Test-Path -LiteralPath (Join-Path $taskScreens 'smoke-ok.txt'))) { throw 'Verificação visual incompleta.' }
foreach ($taskFiscalImage in @('16-configuracao-fiscal.png', '17-empresa-emitente.png', '18-parametros-nfce.png', '19-dados-fiscais-produto.png', '20-conferencia-fiscal.png')) {
    if (-not (Test-Path -LiteralPath (Join-Path $taskScreens $taskFiscalImage))) { throw 'A verificação visual fiscal ficou incompleta.' }
}
Write-Output 'OK: aplicativo instalado abre, renderiza os módulos e salva foto e backup.'
$taskExtra = Join-Path $taskInstalled 'arquivo-do-usuario.txt'
[System.IO.File]::WriteAllText($taskExtra, 'preservar')
Invoke-GiroCheck -TaskExecutable (Join-Path $taskRelease 'DesinstalarGiroBebidas.exe') -TaskArguments @('--verify-uninstall', $taskInstalled)
if (Test-Path -LiteralPath (Join-Path $taskInstalled 'GiroBebidas.exe')) { throw 'O aplicativo não foi removido.' }
if (-not (Test-Path -LiteralPath (Join-Path $taskData 'giro-dados.json'))) { throw 'Os dados de teste não foram preservados.' }
if ([System.IO.File]::ReadAllText($taskExtra) -ne 'preservar') { throw 'Um arquivo adicional foi alterado.' }
Write-Output 'OK: desinstalação remove o pacote e preserva dados e arquivos adicionais.'
$taskLines = @('Distribuição: ' + $Versao, 'Aplicativo, instalador e desinstalador independentes verificados.', 'Fotos importadas e exportadas no backup.', 'Instalação e desinstalação testadas em pasta isolada.', 'Não houve alterações no registro, nos atalhos ou nos dados do usuário.', 'Impressora e balança físicas não foram testadas.')
[System.IO.File]::WriteAllLines((Join-Path $taskOutput 'resultado.txt'), $taskLines)
Write-Output ('Verificação concluída: ' + $taskOutput)
