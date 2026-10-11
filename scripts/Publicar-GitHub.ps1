param(
    [string]$Repositorio = 'belmok/GIRO-BEBIDAS',
    [string]$Versao = '',
    [switch]$Publicar
)

$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if ($Repositorio -notmatch '^[A-Za-z0-9][A-Za-z0-9-]{0,38}/[A-Za-z0-9_.-]{1,100}$') { throw 'Repositório inválido. Use usuário/projeto.' }
$taskProjectVersion = ([xml][IO.File]::ReadAllText((Join-Path $taskRoot 'Directory.Build.props'))).Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($Versao)) { $Versao = $taskProjectVersion }
if ($Versao -notmatch '^\d+\.\d+\.\d+$' -or $Versao -cne $taskProjectVersion) { throw 'A versão deve ser estável e corresponder a Directory.Build.props.' }
$taskTag = 'v' + $Versao
$taskDistributionRoot = [IO.Path]::GetFullPath((Join-Path $taskRoot 'artifacts\distribuicao'))
$taskDistribution = [IO.Path]::GetFullPath((Join-Path $taskDistributionRoot $Versao))
if (-not $taskDistribution.StartsWith($taskDistributionRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Pasta de distribuição inválida.' }
if (-not [IO.Directory]::Exists($taskDistribution)) { throw 'Gere e verifique a distribuição antes de publicar.' }
if (([IO.File]::GetAttributes($taskDistribution) -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'A distribuição deve estar em uma pasta regular.' }
$taskNames = @('InstalarGiroBebidas.exe', 'SHA256.txt', ('GiroBebidas-' + $Versao + '-portatil.zip'), 'GUIA-DO-USUARIO.md')
$taskFiles = foreach ($taskName in $taskNames) {
    $taskPath = Join-Path $taskDistribution $taskName
    if (-not [IO.File]::Exists($taskPath)) { throw ('Arquivo obrigatório ausente: ' + $taskName) }
    $taskFile = Get-Item -LiteralPath $taskPath
    if (($taskFile.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or $taskFile.Length -le 0 -or $taskFile.Length -ge 2GB) { throw ('Arquivo inválido ou acima do limite de Releases: ' + $taskName) }
    $taskFile
}
if ($taskFiles[0].Length -gt 512MB) { throw 'O instalador excede o limite de 512 MB do atualizador do aplicativo.' }
if ($taskFiles[1].Length -gt 256KB) { throw 'SHA256.txt excede o limite do atualizador.' }
$taskHashLines = [IO.File]::ReadAllLines($taskFiles[1].FullName)
$taskHashes = @{}
foreach ($taskFile in $taskFiles) {
    $taskHash = (Get-FileHash -LiteralPath $taskFile.FullName -Algorithm SHA256).Hash
    $taskHashes[$taskFile.Name] = $taskHash
    if ($taskFile.Extension -in '.exe', '.zip') {
        $taskPattern = '^([A-Fa-f0-9]{64})  ' + [regex]::Escape($taskFile.Name) + '$'
        $taskMatches = @($taskHashLines | Where-Object { $_ -cmatch $taskPattern })
        if ($taskMatches.Count -ne 1) { throw ('SHA256.txt precisa conter uma única entrada exata para ' + $taskFile.Name) }
        if ([regex]::Match($taskMatches[0], $taskPattern).Groups[1].Value -ine $taskHash) { throw ('SHA-256 divergente para ' + $taskFile.Name) }
    }
}
$taskFileInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($taskFiles[0].FullName)
if ($taskFileInfo.FileVersion -ne ($Versao + '.0')) { throw 'A versão do instalador não corresponde à versão da distribuição.' }
Write-Output ('Distribuição conferida: Giro Bebidas ' + $Versao)
foreach ($taskFile in $taskFiles) { Write-Output ($taskFile.Name + ' — ' + [Math]::Round($taskFile.Length / 1MB, 2) + ' MB — SHA256 ' + $taskHashes[$taskFile.Name]) }
if (-not $Publicar) {
    Write-Output ('Nenhum arquivo enviado. Para publicar depois de enviar a tag ' + $taskTag + ', execute este script com -Publicar.')
    return
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue) -or -not (Get-Command git -ErrorAction SilentlyContinue)) { throw 'Instale Git e GitHub CLI e entre na sua conta pelo GitHub CLI antes de publicar.' }
function Invoke-GiroGh([string[]]$TaskArguments) {
    $taskResult = & gh @TaskArguments
    if ($LASTEXITCODE -ne 0) { throw 'O GitHub CLI não concluiu a operação. Confira sua conta e as permissões no repositório.' }
    return $taskResult
}
$taskHead = (& git -C $taskRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $taskHead -notmatch '^[A-Fa-f0-9]{40}$') { throw 'Não foi possível identificar o commit local.' }
$taskPending = @(& git -C $taskRoot status --porcelain --untracked-files=no)
if ($LASTEXITCODE -ne 0 -or $taskPending.Count -gt 0) { throw 'Registre as alterações do código antes de publicar.' }
if ($taskFileInfo.ProductVersion -notmatch ('^' + [regex]::Escape($Versao) + '\+([A-Fa-f0-9]{40})$') -or $Matches[1] -ine $taskHead) { throw 'O instalador não foi gerado a partir do commit atual. Gere novamente a distribuição após registrar o código.' }
$taskRemoteRefs = @(& git ls-remote --tags ('https://github.com/' + $Repositorio + '.git') ('refs/tags/' + $taskTag) ('refs/tags/' + $taskTag + '^{}'))
if ($LASTEXITCODE -ne 0) { throw 'Não foi possível consultar a tag remota. Este script não envia código ou tags.' }
$taskDirect = $null
$taskPeeled = $null
foreach ($taskRef in $taskRemoteRefs) {
    if ($taskRef -cmatch ('^([A-Fa-f0-9]{40})\s+refs/tags/' + [regex]::Escape($taskTag) + '(\^\{\})?$')) {
        if ($Matches[2]) { $taskPeeled = $Matches[1] } else { $taskDirect = $Matches[1] }
    }
}
$taskRemoteCommit = if ($taskPeeled) { $taskPeeled } else { $taskDirect }
if (-not $taskRemoteCommit -or $taskRemoteCommit -ine $taskHead) { throw ('A tag ' + $taskTag + ' precisa existir no GitHub e apontar para o commit atual. Envie o código e a tag antes de publicar.') }
$taskRepoInfo = (Invoke-GiroGh -TaskArguments @('api', ('repos/' + $Repositorio))) | ConvertFrom-Json
if ($taskRepoInfo.private -or $taskRepoInfo.full_name -ine $Repositorio) { throw 'O atualizador precisa de Releases em um repositório público configurado corretamente.' }

# A failed upload remains a draft; the application never offers incomplete releases.
$taskExistingJson = & gh release view $taskTag --repo $Repositorio --json isDraft,isPrerelease 2>$null
$taskExisting = if ($LASTEXITCODE -eq 0) { $taskExistingJson | ConvertFrom-Json } else { $null }
if ($taskExisting -and -not $taskExisting.isDraft) { throw 'Esta versão já foi publicada. Use uma nova versão; não substitua arquivos de uma atualização disponível.' }
if (-not $taskExisting) {
    $taskNotesDirectory = Join-Path $taskRoot '.tools\github-publicacao'
    [IO.Directory]::CreateDirectory($taskNotesDirectory) | Out-Null
    $taskNotesPath = Join-Path $taskNotesDirectory ('notas-' + $Versao + '-' + [Guid]::NewGuid().ToString('N') + '.md')
    try {
        $taskNotes = @(
            ('Giro Bebidas ' + $Versao + ' para Windows 64 bits.'),
            '',
            'Use InstalarGiroBebidas.exe para instalar ou atualizar. O instalador inclui o runtime necessário.',
            'Antes de atualizar, finalize a venda e feche o caixa. O aplicativo cria um backup antes de abrir o instalador.',
            'SHA256.txt permite conferir a integridade dos programas. O pacote portátil e o guia de uso também estão disponíveis.'
        )
        [IO.File]::WriteAllLines($taskNotesPath, [string[]]$taskNotes)
        Invoke-GiroGh -TaskArguments @('release', 'create', $taskTag, '--repo', $Repositorio, '--verify-tag', '--draft', '--title', ('Giro Bebidas ' + $Versao), '--notes-file', $taskNotesPath) | Out-Null
    }
    finally { if ([IO.File]::Exists($taskNotesPath)) { Remove-Item -LiteralPath $taskNotesPath } }
}
Invoke-GiroGh -TaskArguments (@('release', 'upload', $taskTag, '--repo', $Repositorio, '--clobber') + @($taskFiles.FullName)) | Out-Null
$taskReleaseInfo = (Invoke-GiroGh -TaskArguments @('api', ('repos/' + $Repositorio + '/releases/tags/' + $taskTag))) | ConvertFrom-Json
if (-not $taskReleaseInfo.draft) { throw 'A publicação deixou de ser um rascunho antes da conferência. Interrompida.' }
foreach ($taskFile in $taskFiles) {
    $taskAssets = @($taskReleaseInfo.assets | Where-Object { $_.name -ceq $taskFile.Name })
    if ($taskAssets.Count -ne 1 -or $taskAssets[0].state -ne 'uploaded' -or $taskAssets[0].size -ne $taskFile.Length) { throw ('Upload incompleto: ' + $taskFile.Name + '. O rascunho foi preservado.') }
    if ($taskAssets[0].digest -and $taskAssets[0].digest -ine ('sha256:' + $taskHashes[$taskFile.Name])) { throw ('SHA-256 remoto divergente: ' + $taskFile.Name + '. O rascunho foi preservado.') }
}
Invoke-GiroGh -TaskArguments @('release', 'edit', $taskTag, '--repo', $Repositorio, '--draft=false', '--prerelease=false', '--latest') | Out-Null
Write-Output ('Publicação concluída: https://github.com/' + $Repositorio + '/releases/tag/' + $taskTag)
