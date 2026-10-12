param([switch]$ValidarFluxoPublicacao)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Notas-Versao.ps1')
$taskTestsRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$taskFixture = Join-Path $taskTestsRoot ('artifacts\testes-notas\' + [DateTime]::Now.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N'))
$taskFixtureNotes = Join-Path $taskFixture 'docs\notas-versoes'
[IO.Directory]::CreateDirectory($taskFixtureNotes) | Out-Null
$taskFixtureVersion = '1.6.0'
$taskFixturePath = Join-Path $taskFixtureNotes ($taskFixtureVersion + '.md')
$taskCheckCount = 0
$taskUtf8 = [Text.UTF8Encoding]::new($false, $true)

function Assert-Notes([bool]$Condition, [string]$Description) {
    if (-not $Condition) { throw ('Falha: ' + $Description) }
    $script:taskCheckCount++
}
function Expect-NotesFailure([scriptblock]$Action, [string]$ExpectedMessage, [string]$Description) {
    $taskFailure = $null
    try { & $Action | Out-Null }
    catch { $taskFailure = $_.Exception.Message }
    Assert-Notes ($null -ne $taskFailure -and $taskFailure -match $ExpectedMessage) $Description
}
function Write-NotesFixture([string]$Content) {
    [IO.File]::WriteAllText($script:taskFixturePath, $Content, $script:taskUtf8)
}
$taskGoodNotes = @'
# Giro Bebidas 1.6.0

## Novidades

- Busca de pedidos por cliente e endereço, com ações por pedido.
- Consulta organizada: P00001, R$ 29,40 e entrega em São Paulo.

## Correções

- Os resumos das publicações agora informam os ajustes da versão correta.

## Verificações

- Conferidos filtros, seleção, cancelamento, permissões e estados vazios em dados de teste.
'@

Expect-NotesFailure { Get-GiroReleaseNotes -Versao $taskFixtureVersion -RepositorioRaiz $taskFixture } 'Notas obrigatórias ausentes' 'Bloqueia a ausência do arquivo de notas.'
Expect-NotesFailure { Get-GiroReleaseNotes -Versao '../1.6.0' -RepositorioRaiz $taskFixture } 'formato estável' 'Bloqueia versão com caminho.'
Expect-NotesFailure { Get-GiroReleaseNotes -Versao '1.6.0-beta' -RepositorioRaiz $taskFixture } 'formato estável' 'Bloqueia versão de teste.'

Write-NotesFixture ($taskGoodNotes.Replace('# Giro Bebidas 1.6.0', '# Giro Bebidas 1.5.2'))
Expect-NotesFailure { Get-GiroReleaseNotes -Versao $taskFixtureVersion -RepositorioRaiz $taskFixture } 'título das notas' 'Bloqueia título de outra versão.'
Write-NotesFixture ''
Expect-NotesFailure { Get-GiroReleaseNotes -Versao $taskFixtureVersion -RepositorioRaiz $taskFixture } 'título das notas' 'Bloqueia arquivo vazio.'
Write-NotesFixture ('Descrição antes do título.' + "`n" + $taskGoodNotes)
Expect-NotesFailure { Get-GiroReleaseNotes -Versao $taskFixtureVersion -RepositorioRaiz $taskFixture } 'título das notas' 'Exige o título correto no início.'

foreach ($taskRequiredSection in @('Novidades', 'Correções', 'Verificações')) {
    Write-NotesFixture ($taskGoodNotes.Replace('## ' + $taskRequiredSection, '## Outra seção'))
    Expect-NotesFailure { Get-GiroReleaseNotes -Versao $taskFixtureVersion -RepositorioRaiz $taskFixture } 'Seção obrigatória ausente' ('Bloqueia a falta de ' + $taskRequiredSection + '.')
    $taskEmptySource = [regex]::Replace($taskGoodNotes, '(?ms)^## ' + [regex]::Escape($taskRequiredSection) + '\r?\n.*?(?=^## |\z)', ('## ' + $taskRequiredSection + "`n`n"))
    Write-NotesFixture $taskEmptySource
    Expect-NotesFailure { Get-GiroReleaseNotes -Versao $taskFixtureVersion -RepositorioRaiz $taskFixture } 'conteúdo específico' ('Bloqueia ' + $taskRequiredSection + ' sem descrição.')
}

$taskPlaceholderSource = [regex]::Replace($taskGoodNotes, '(?ms)^## Novidades\r?\n.*?(?=^## |\z)', "## Novidades`n`n- TODO`n- ...`n<!-- Descrever depois -->`n`n")
Write-NotesFixture $taskPlaceholderSource
Expect-NotesFailure { Get-GiroReleaseNotes -Versao $taskFixtureVersion -RepositorioRaiz $taskFixture } 'conteúdo específico' 'Comentários, marcadores e lembretes não contam como novidade.'
Write-NotesFixture ($taskGoodNotes + "`n`n## Correções`n`n- Outra correção.`n")
Expect-NotesFailure { Get-GiroReleaseNotes -Versao $taskFixtureVersion -RepositorioRaiz $taskFixture } 'Seção repetida' 'Bloqueia seções obrigatórias repetidas.'
Write-NotesFixture ($taskGoodNotes + "`n`n# Giro Bebidas 1.5.2`n")
Expect-NotesFailure { Get-GiroReleaseNotes -Versao $taskFixtureVersion -RepositorioRaiz $taskFixture } 'único título' 'Bloqueia outro título principal.'
Write-NotesFixture ($taskGoodNotes + "`n`n## Como instalar ou atualizar`n`n- Instrução duplicada.`n")
Expect-NotesFailure { Get-GiroReleaseNotes -Versao $taskFixtureVersion -RepositorioRaiz $taskFixture } 'adicionadas pelo script' 'Evita instruções de instalação duplicadas.'
[IO.File]::WriteAllBytes($taskFixturePath, [byte[]]@(0xFF, 0xFE, 0xFF))
Expect-NotesFailure { Get-GiroReleaseNotes -Versao $taskFixtureVersion -RepositorioRaiz $taskFixture } 'UTF-8' 'Bloqueia conteúdo que não pode ser lido como UTF-8.'

Write-NotesFixture $taskGoodNotes
$taskOriginalHash = (Get-FileHash -LiteralPath $taskFixturePath -Algorithm SHA256).Hash
$taskNotes = Get-GiroReleaseNotes -Versao $taskFixtureVersion -RepositorioRaiz $taskFixture
Assert-Notes ($taskNotes.Title -ceq 'Giro Bebidas 1.6.0') 'Retorna o título da versão correta.'
Assert-Notes ($taskNotes.SourcePath -ceq $taskFixturePath) 'Identifica o arquivo específico da versão.'
Assert-Notes ($taskNotes.Content.StartsWith('# Giro Bebidas 1.6.0')) 'As novidades aparecem antes das instruções genéricas.'
Assert-Notes ($taskNotes.Content.Contains('endereço') -and $taskNotes.Content.Contains('Correções') -and $taskNotes.Content.Contains('São Paulo')) 'Preserva os acentos nas notas.'
Assert-Notes ($taskNotes.Content.Contains("`n`n## Correções`n`n")) 'Preserva quebras de linha reais e separação das seções.'
Assert-Notes ($taskNotes.Content.Contains('## Como instalar ou atualizar') -and $taskNotes.Content.Contains('InstalarGiroBebidas.exe') -and $taskNotes.Content.Contains('SHA256.txt')) 'Acrescenta as instruções e os arquivos de instalação.'
Assert-Notes ($taskNotes.Content.IndexOf('## Verificações') -lt $taskNotes.Content.IndexOf('## Como instalar ou atualizar')) 'As verificações ficam antes das instruções finais.'
Assert-Notes (([regex]::Matches($taskNotes.Content, '(?m)^## Como instalar ou atualizar$')).Count -eq 1) 'Acrescenta as instruções somente uma vez.'
Assert-Notes ((Get-FileHash -LiteralPath $taskFixturePath -Algorithm SHA256).Hash -ceq $taskOriginalHash) 'A consulta não modifica as notas originais.'

$taskOutputPath = Join-Path $taskFixture 'notas-publicacao.md'
[IO.File]::WriteAllText($taskOutputPath, $taskNotes.Content, $taskUtf8)
$taskRoundTrip = $taskUtf8.GetString([IO.File]::ReadAllBytes($taskOutputPath))
Assert-Notes ($taskRoundTrip -ceq $taskNotes.Content) 'O arquivo de publicação mantém conteúdo e quebras de linha em UTF-8.'
Assert-Notes ($taskRoundTrip -notmatch '\\n## Correções') 'As quebras de linha não viram sequências literais de escape.'

[IO.File]::WriteAllText($taskFixturePath, $taskGoodNotes.Replace("`r`n", "`n").Replace("`n", "`r`n"), [Text.UTF8Encoding]::new($true, $true))
$taskBomNotes = Get-GiroReleaseNotes -Versao $taskFixtureVersion -RepositorioRaiz $taskFixture
Assert-Notes ($taskBomNotes.Content -ceq $taskNotes.Content) 'Aceita UTF-8 com BOM e normaliza quebras de linha do Windows.'
Write-NotesFixture ($taskGoodNotes + "`n`n## Exemplo técnico`n`n~~~text`n## Correções`n~~~`n")
$taskCodeNotes = Get-GiroReleaseNotes -Versao $taskFixtureVersion -RepositorioRaiz $taskFixture
Assert-Notes ($taskCodeNotes.Content.Contains('## Exemplo técnico')) 'Um título em bloco de código não é confundido com seção duplicada.'
Write-NotesFixture ($taskGoodNotes + "`n`n~~~text`nSem fechamento`n")
Expect-NotesFailure { Get-GiroReleaseNotes -Versao $taskFixtureVersion -RepositorioRaiz $taskFixture } 'bloco de código sem fechamento' 'Bloqueia um bloco de código que esconderia as instruções finais.'

# Validate the actual entry point without making a network request or needing a package.
$taskFixtureScripts = Join-Path $taskFixture 'scripts'
[IO.Directory]::CreateDirectory($taskFixtureScripts) | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Publicar-GitHub.ps1') -Destination $taskFixtureScripts
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Notas-Versao.ps1') -Destination $taskFixtureScripts
[IO.File]::WriteAllText((Join-Path $taskFixture 'Directory.Build.props'), '<Project><PropertyGroup><Version>1.6.0</Version></PropertyGroup></Project>', $taskUtf8)
Write-NotesFixture ($taskGoodNotes.Replace('# Giro Bebidas 1.6.0', '# Giro Bebidas 1.5.2'))
Expect-NotesFailure { & (Join-Path $taskFixtureScripts 'Publicar-GitHub.ps1') -Versao $taskFixtureVersion } 'título das notas' 'Publicar-GitHub sem -Publicar também bloqueia notas inválidas antes de conferir o pacote.'
Write-NotesFixture $taskGoodNotes
Expect-NotesFailure { & (Join-Path $taskFixtureScripts 'Publicar-GitHub.ps1') -Versao $taskFixtureVersion } 'Gere e verifique a distribuição' 'Notas válidas permitem seguir para a conferência obrigatória da distribuição.'

if ($ValidarFluxoPublicacao) {
    # All Git/GitHub commands below are local doubles. The installed application and
    # real repository are never executed, modified or contacted by these scenarios.
    $taskInstallerSource = Join-Path $taskTestsRoot 'InstalarGiroBebidas.exe'
    if (-not [IO.File]::Exists($taskInstallerSource)) { throw 'Gere o instalador antes de conferir o fluxo simulado de publicação.' }
    $taskInstallerInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($taskInstallerSource)
    if ($taskInstallerInfo.ProductVersion -cnotmatch '^(\d+\.\d+\.\d+)\+([A-Fa-f0-9]{40})$') { throw 'O instalador de teste precisa identificar sua versão e origem.' }
    $taskMockVersion = $Matches[1]
    $taskMockHead = $Matches[2]
    $taskMockDistribution = Join-Path $taskFixture ('artifacts\distribuicao\' + $taskMockVersion)
    [IO.Directory]::CreateDirectory($taskMockDistribution) | Out-Null
    # Copy into the fixture: the actual installer is only read, never executed or
    # changed. Hard links are deliberately avoided on synchronized workspaces.
    Copy-Item -LiteralPath $taskInstallerSource -Destination (Join-Path $taskMockDistribution 'InstalarGiroBebidas.exe')
    [IO.File]::WriteAllText((Join-Path $taskFixture 'Directory.Build.props'), ('<Project><PropertyGroup><Version>' + $taskMockVersion + '</Version></PropertyGroup></Project>'), $taskUtf8)
    $taskMockNotesPath = Join-Path $taskFixtureNotes ($taskMockVersion + '.md')
    [IO.File]::WriteAllText($taskMockNotesPath, $taskGoodNotes.Replace('# Giro Bebidas 1.6.0', ('# Giro Bebidas ' + $taskMockVersion)), $taskUtf8)
    [IO.File]::WriteAllText((Join-Path $taskMockDistribution ('GiroBebidas-' + $taskMockVersion + '-portatil.zip')), 'Pacote fictício: somente teste do fluxo de notas, sem instalação.', $taskUtf8)
    [IO.File]::WriteAllText((Join-Path $taskMockDistribution 'GUIA-DO-USUARIO.md'), '# Guia fictício para conferir somente a publicação das notas.', $taskUtf8)
    $taskMockNames = @('InstalarGiroBebidas.exe', 'SHA256.txt', ('GiroBebidas-' + $taskMockVersion + '-portatil.zip'), 'GUIA-DO-USUARIO.md')
    $taskMockHashLines = foreach ($taskMockName in $taskMockNames | Where-Object { $_ -match '\.(exe|zip)$' }) {
        (Get-FileHash -LiteralPath (Join-Path $taskMockDistribution $taskMockName) -Algorithm SHA256).Hash + '  ' + $taskMockName
    }
    [IO.File]::WriteAllLines((Join-Path $taskMockDistribution 'SHA256.txt'), [string[]]$taskMockHashLines, $taskUtf8)
    $taskMockAssets = @(foreach ($taskMockName in $taskMockNames) {
        $taskMockFile = Get-Item -LiteralPath (Join-Path $taskMockDistribution $taskMockName)
        [pscustomobject]@{ name = $taskMockName; state = 'uploaded'; size = $taskMockFile.Length; digest = 'sha256:' + (Get-FileHash -LiteralPath $taskMockFile.FullName -Algorithm SHA256).Hash }
    })

    function Invoke-MockedPublication([string]$Mode) {
        $taskSimulation = [pscustomobject]@{ Mode = $Mode; Body = 'Texto genérico antigo.'; Commands = [Collections.Generic.List[object]]::new(); NotesBytes = $null; Failure = $null }
        function git {
            $global:LASTEXITCODE = 0
            if ($args -contains 'rev-parse') { return $taskMockHead }
            if ($args -contains 'status') { return }
            if ($args -contains 'ls-remote') { return ($taskMockHead + "`trefs/tags/v" + $taskMockVersion) }
            throw 'Comando Git inesperado no teste de publicação.'
        }
        function gh {
            $global:LASTEXITCODE = 0
            $taskGhArgs = [string[]]$args
            $taskSimulation.Commands.Add($taskGhArgs)
            if ($taskGhArgs[0] -ceq 'api') {
                if ($taskGhArgs[1] -ceq 'repos/belmok/GIRO-BEBIDAS') { return (@{ private = $false; full_name = 'belmok/GIRO-BEBIDAS' } | ConvertTo-Json -Compress) }
                if ($taskGhArgs[1] -ceq 'repos/belmok/GIRO-BEBIDAS/releases/123') {
                    $taskBody = if ($taskSimulation.Mode -ceq 'stale') { 'Texto genérico antigo.' } else { $taskSimulation.Body }
                    return (@{ draft = $true; tag_name = 'v' + $taskMockVersion; id = 123; assets = $taskMockAssets; body = $taskBody } | ConvertTo-Json -Depth 8 -Compress)
                }
            }
            if ($taskGhArgs[0] -ceq 'release' -and $taskGhArgs[1] -ceq 'view') {
                if ($taskGhArgs -contains 'isDraft,isPrerelease') {
                    if ($taskSimulation.Mode -in 'new', 'stale') { $global:LASTEXITCODE = 1; return }
                    return (@{ isDraft = $taskSimulation.Mode -cne 'published'; isPrerelease = $false } | ConvertTo-Json -Compress)
                }
                return (@{ databaseId = 123; tagName = 'v' + $taskMockVersion; isDraft = $true } | ConvertTo-Json -Compress)
            }
            if ($taskGhArgs[0] -ceq 'release' -and $taskGhArgs[1] -in 'create', 'edit', 'upload') {
                $taskNotesIndex = [Array]::IndexOf($taskGhArgs, '--notes-file')
                if ($taskNotesIndex -ge 0) {
                    $taskSimulation.NotesBytes = [IO.File]::ReadAllBytes($taskGhArgs[$taskNotesIndex + 1])
                    $taskSimulation.Body = $taskUtf8.GetString($taskSimulation.NotesBytes)
                }
                return
            }
            throw ('Comando GitHub inesperado no teste de publicação: ' + ($taskGhArgs -join ' '))
        }
        $taskLastExitCode = $global:LASTEXITCODE
        try { & (Join-Path $taskFixtureScripts 'Publicar-GitHub.ps1') -Versao $taskMockVersion -Publicar | Out-Null }
        catch { $taskSimulation.Failure = $_.Exception.Message }
        finally { $global:LASTEXITCODE = $taskLastExitCode }
        return $taskSimulation
    }

    $taskNewRelease = Invoke-MockedPublication 'new'
    Assert-Notes ($null -eq $taskNewRelease.Failure) ('Cria uma Release nova com notas específicas; erro: ' + $taskNewRelease.Failure)
    Assert-Notes (@($taskNewRelease.Commands | Where-Object { $_[0] -eq 'release' -and $_[1] -eq 'create' -and $_ -contains '--notes-file' }).Count -eq 1) 'A criação usa um arquivo de notas, com quebras reais.'
    Assert-Notes ($taskNewRelease.Body.Contains('endereço') -and $taskNewRelease.Body.Contains('## Correções') -and $taskNewRelease.Body.Contains('## Verificações')) 'O texto enviado à criação preserva acentos e as informações específicas.'
    Assert-Notes (@($taskNewRelease.Commands | Where-Object { $_ -contains '--draft=false' }).Count -eq 1) 'Libera a nova versão depois de conferir o corpo e os anexos.'

    $taskResumedRelease = Invoke-MockedPublication 'draft'
    Assert-Notes ($null -eq $taskResumedRelease.Failure) ('Retoma um rascunho; erro: ' + $taskResumedRelease.Failure)
    Assert-Notes (@($taskResumedRelease.Commands | Where-Object { $_[0] -eq 'release' -and $_[1] -eq 'create' }).Count -eq 0) 'A retomada não cria outro rascunho.'
    Assert-Notes (@($taskResumedRelease.Commands | Where-Object { $_[0] -eq 'release' -and $_[1] -eq 'edit' -and $_ -contains '--notes-file' }).Count -eq 1) 'A retomada substitui o texto genérico usando as notas da versão.'
    Assert-Notes ($taskResumedRelease.Body -ceq $taskNewRelease.Body) 'Criação e retomada publicam o mesmo conteúdo validado.'
    $taskNotesEditIndex = @($taskResumedRelease.Commands | ForEach-Object { $_ -contains '--notes-file' }).IndexOf($true)
    $taskUploadIndex = @($taskResumedRelease.Commands | ForEach-Object { $_[0] -eq 'release' -and $_[1] -eq 'upload' }).IndexOf($true)
    Assert-Notes ($taskNotesEditIndex -ge 0 -and $taskNotesEditIndex -lt $taskUploadIndex) 'A retomada atualiza as notas antes de enviar os anexos.'

    $taskPublishedRelease = Invoke-MockedPublication 'published'
    Assert-Notes ($taskPublishedRelease.Failure -match 'já foi publicada') 'Uma Release publicada continua protegida contra substituição.'
    Assert-Notes (@($taskPublishedRelease.Commands | Where-Object { $_[0] -eq 'release' -and $_[1] -in 'create', 'edit', 'upload' }).Count -eq 0) 'O script não altera notas nem anexos de Release já publicada.'

    $taskStaleRelease = Invoke-MockedPublication 'stale'
    Assert-Notes ($taskStaleRelease.Failure -match 'notas do rascunho no GitHub não correspondem') 'Notas remotas genéricas impedem a liberação da versão.'
    Assert-Notes (@($taskStaleRelease.Commands | Where-Object { $_ -contains '--draft=false' }).Count -eq 0) 'Uma divergência no corpo preserva a Release como rascunho.'
    Assert-Notes (@(Get-ChildItem -LiteralPath (Join-Path $taskFixture '.tools\github-publicacao') -Filter '*.md').Count -eq 0) 'Os arquivos temporários das notas são removidos, inclusive após falhas.'
    Remove-Item -LiteralPath (Join-Path $taskMockDistribution 'InstalarGiroBebidas.exe')
}

$taskResult = ('Notas de versão: ' + $taskCheckCount + ' verificações passaram. Dados isolados: ' + $taskFixture)
[IO.File]::WriteAllText((Join-Path $taskFixture 'resultado.txt'), $taskResult + "`n", $taskUtf8)
Write-Output $taskResult
