function Get-GiroReleaseNotes {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Versao,
        [string]$RepositorioRaiz = (Split-Path -Parent $PSScriptRoot)
    )

    if ($Versao -cnotmatch '^\d+\.\d+\.\d+$') { throw 'A versão das notas deve usar o formato estável maior.menor.correção.' }
    $taskNotesRoot = [IO.Path]::GetFullPath($RepositorioRaiz)
    $taskNotesPath = Join-Path $taskNotesRoot ('docs\notas-versoes\' + $Versao + '.md')
    if (-not [IO.File]::Exists($taskNotesPath)) { throw ('Notas obrigatórias ausentes: docs/notas-versoes/' + $Versao + '.md. Descreva novidades, correções e verificações antes de publicar.') }
    if (([IO.File]::GetAttributes($taskNotesPath) -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'As notas da versão devem estar em um arquivo regular do projeto.' }
    try {
        $taskUtf8 = [Text.UTF8Encoding]::new($false, $true)
        $taskSource = $taskUtf8.GetString([IO.File]::ReadAllBytes($taskNotesPath)).TrimStart([char]0xFEFF)
    }
    catch [Text.DecoderFallbackException] { throw 'Salve as notas da versão em UTF-8 para preservar os acentos.' }
    $taskSource = $taskSource.Replace("`r`n", "`n").Replace("`r", "`n").Trim()
    $taskLines = $taskSource -split "`n"
    $taskExpectedTitle = '# Giro Bebidas ' + $Versao
    if ($taskLines[0] -cne $taskExpectedTitle) { throw ('O título das notas deve ser exatamente: ' + $taskExpectedTitle) }

    $taskRequiredSections = @('Novidades', 'Correções', 'Verificações')
    $taskSections = @{}
    $taskSection = $null
    $taskFence = $null
    $taskTitles = 0
    foreach ($taskLine in $taskLines) {
        if ($taskLine -match '^\s*(`{3,}|~{3,})') {
            $taskFenceMark = $Matches[1].Substring(0, 1)
            if (-not $taskFence) { $taskFence = $taskFenceMark }
            elseif ($taskFence -ceq $taskFenceMark) { $taskFence = $null }
            if ($taskSection) { $taskSections[$taskSection].Add($taskLine) }
            continue
        }
        if (-not $taskFence -and $taskLine -match '^#\s+') { $taskTitles++ }
        if (-not $taskFence -and $taskLine -match '^##\s+(.+?)\s*$') {
            $taskSectionName = $Matches[1]
            if ($taskSectionName -ceq 'Como instalar ou atualizar') { throw 'As instruções de instalação são adicionadas pelo script; mantenha apenas as informações específicas nas notas.' }
            $taskSection = $null
            if ($taskSectionName -cin $taskRequiredSections) {
                if ($taskSections.ContainsKey($taskSectionName)) { throw ('Seção repetida nas notas: ' + $taskSectionName) }
                $taskSection = $taskSectionName
                $taskSections[$taskSection] = [Collections.Generic.List[string]]::new()
            }
            continue
        }
        if ($taskSection) { $taskSections[$taskSection].Add($taskLine) }
    }
    if ($taskTitles -ne 1) { throw 'As notas devem conter um único título principal para a versão informada.' }
    if ($taskFence) { throw 'Há um bloco de código sem fechamento nas notas da versão.' }
    foreach ($taskSectionName in $taskRequiredSections) {
        if (-not $taskSections.ContainsKey($taskSectionName)) { throw ('Seção obrigatória ausente nas notas: ## ' + $taskSectionName) }
        $taskSectionContent = ($taskSections[$taskSectionName] -join "`n") -replace '(?s)<!--.*?-->', ''
        $taskMeaningfulLines = @($taskSectionContent -split "`n" | Where-Object {
            $taskDescription = $_.Trim() -replace '^(?:[-*+]\s*|\d+[.)]\s*)', ''
            $taskDescription = $taskDescription.Trim()
            $taskDescription -match '[\p{L}\p{N}]' -and
                $taskDescription -notmatch '^(?i:TBD|TODO|a definir|pendente|preencher|\.\.\.)[.!]?$' -and
                $taskDescription -notmatch '^(?:#{1,6}\s|`{3,}|~{3,})'
        })
        if ($taskMeaningfulLines.Count -eq 0) { throw ('Descreva o conteúdo específico da seção ## ' + $taskSectionName + '. Uma seção vazia ou com marcadores de preenchimento impede a publicação.') }
    }

    $taskInstructions = @'
## Como instalar ou atualizar

Giro Bebidas para Windows 64 bits.

Use **InstalarGiroBebidas.exe** para instalar ou atualizar. O instalador inclui o tempo de execução necessário.
Antes de atualizar, finalize a venda e feche o caixa. O aplicativo cria um backup antes de abrir o instalador.
**SHA256.txt** permite conferir a integridade dos programas. O pacote portátil e o guia de uso também estão disponíveis.
'@
    [pscustomobject]@{
        Version = $Versao
        Title = 'Giro Bebidas ' + $Versao
        SourcePath = $taskNotesPath
        Content = $taskSource + "`n`n" + $taskInstructions.Replace("`r`n", "`n") + "`n"
    }
}
