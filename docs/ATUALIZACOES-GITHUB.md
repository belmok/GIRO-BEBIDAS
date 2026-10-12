# Atualizações do Giro Bebidas pelo GitHub

O Giro Bebidas consulta as versões estáveis de [belmok/GIRO-BEBIDAS](https://github.com/belmok/GIRO-BEBIDAS/releases). A tela **Atualizações** permite mudar o repositório e ativar ou desativar a consulta ao entrar como administrador.

Uma versão fica disponível quando uma Release pública contém `InstalarGiroBebidas.exe` e `SHA256.txt`. O aplicativo compara a numeração com a versão instalada, ignora rascunhos e versões de teste, confere o tamanho e verifica o SHA-256 do instalador. O hash verifica a integridade do arquivo; utilize somente um repositório em que você confie.

## Atualizar o computador do caixa

1. Entre como administrador e abra **Atualizações**.
2. Clique em **Verificar atualizações** e leia as novidades.
3. Finalize a venda atual e feche o caixa.
4. Escolha a instalação da versão disponível. Você pode cancelar o download.
5. Quando o download terminar, o programa cria um backup na pasta de dados e informa seu caminho. Confirme para fechar o PDV e abrir o instalador.
6. Conclua a instalação na mesma pasta. Abra o Giro Bebidas novamente.

O download e a consulta exigem internet. A operação do PDV continua local. A edição portátil pode consultar versões; para instalar pela tela, primeiro use o instalador do aplicativo. A consulta não envia produtos, clientes, vendas, senhas ou configuração fiscal para o GitHub. Não é necessário inserir token ou senha do GitHub no PDV. Repositórios privados não são atendidos por este fluxo.

## Primeiro envio do projeto

O fluxo está preparado localmente. Ele precisa que o código e a tag de versão existam no GitHub antes de publicar a distribuição. Um repositório vazio ainda não oferece atualizações.

Use uma conta com permissão de escrita em `belmok/GIRO-BEBIDAS`. No GitHub Desktop ou Git autenticado, envie os commits do projeto para a branch `main`. Envie somente o código registrado: a pasta `artifacts`, os programas gerados e `.tools` estão ignorados no Git. Os executáveis serão anexados à Release, evitando o limite de arquivos do Git.

Mantenha banco de dados, backups, fotos de clientes, certificados, CSC, senhas e dados reais da loja fora do repositório público. Os dados normais do programa ficam em `%LOCALAPPDATA%\GiroBebidas`.

Para conferir o remoto existente sem alterá-lo:

```powershell
git remote -v
```

Se ainda não houver remoto `origin`, adicione o endereço informado e envie a branch:

```powershell
git remote add origin https://github.com/belmok/GIRO-BEBIDAS.git
git push -u origin main
```

Se `origin` já existir, use o endereço correto já configurado. Não substitua um remoto de outro projeto sem conferir.

## Publicação automática de uma versão

O arquivo `.github/workflows/publicar.yml` executa a publicação quando uma tag estável `v*` é enviada. Ele instala o SDK definido em `global.json` (**8.0.425**), compila, executa as verificações, gera o instalador completo e verifica o pacote, as telas, a instalação e a remoção. Só depois prepara a Release.

Cada atualização deve informar o que mudou. Antes de publicar, crie `docs/notas-versoes/<versão>.md`, usando a versão exata que será enviada. O arquivo começa com `# Giro Bebidas <versão>` e contém as seções `## Novidades`, `## Correções` e `## Verificações`. Descreva as funções adicionadas, os problemas corrigidos e os testes que realmente passaram. Se não houver novidades ou correções, explique isso na seção correspondente; não deixe a seção vazia nem use lembretes como “TODO” ou “a definir”. Informe também as limitações materiais das verificações.

As informações específicas da versão aparecem primeiro na Release e na consulta de atualizações do PDV. O script acrescenta as instruções de instalação ao final. A publicação é interrompida se faltar o arquivo, se o título informar outra versão ou se faltar conteúdo nas seções obrigatórias. Ao retomar um rascunho, o script atualiza também suas notas, evitando manter descrições genéricas de uma tentativa anterior.

Confira a geração e a validação das notas com dados isolados:

```powershell
./scripts/Testar-Notas-Versao.ps1
```

Depois de gerar o instalador, `./scripts/Testar-Notas-Versao.ps1 -ValidarFluxoPublicacao` também confere a criação de Release, a retomada de rascunho, o bloqueio de notas remotas divergentes e a proteção de versões já publicadas. Esses cenários usam uma cópia do instalador e respostas locais simuladas, sem enviar arquivos nem acessar o GitHub.

Para uma próxima versão, ajuste `Version`, `AssemblyVersion` e `FileVersion` em `Directory.Build.props` e escreva suas notas. Registre as alterações e envie o commit. Crie uma tag exatamente igual a `v` mais o campo `Version`, por exemplo `v1.2.0` quando `Version` for `1.2.0`, e envie essa tag:

```powershell
git tag v1.2.0
git push origin v1.2.0
```

Se a tag já existir localmente, envie a tag existente depois de conferir seu commit. Não mova uma tag já publicada para outro código.

Na aba **Actions** do GitHub, acompanhe **Publicar Giro Bebidas**. Em caso de falha de envio, a Release permanece como rascunho e o PDV não a oferece. O workflow utiliza a credencial temporária do próprio GitHub Actions; não grave credenciais no projeto. A política da conta ou organização precisa permitir que o workflow publique Releases.

Os arquivos publicados são:

- `InstalarGiroBebidas.exe`;
- `SHA256.txt`;
- `GiroBebidas-<versão>-portatil.zip`;
- `GUIA-DO-USUARIO.md`.

O aplicativo e o desinstalador fazem parte do instalador e do pacote portátil. A consulta do PDV precisa dos dois primeiros arquivos. O instalador pode ter até 512 MB, e cada anexo precisa ficar abaixo de 2 GiB. [Limites de Releases do GitHub](https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases)

## Publicação manual da distribuição local

Use esta alternativa quando a distribuição já foi gerada e verificada no computador. Instale Git e GitHub CLI; faça login com a conta que pode publicar no repositório. O script não cria nem envia tags ou código.

Depois de registrar o código, gere e confira o pacote:

```powershell
./scripts/Gerar-Distribuicao.ps1
./scripts/Verificar-Distribuicao.ps1
./scripts/Publicar-GitHub.ps1
```

A última chamada valida as notas específicas da versão, confere os arquivos e mostra o que será publicado, sem realizar envios. Depois que o commit e a tag correspondente já estiverem no GitHub, execute:

```powershell
./scripts/Publicar-GitHub.ps1 -Publicar
```

O script verifica as notas, o SHA-256, a versão do instalador, o commit usado na compilação e a tag remota. Ele prepara a descrição com novidades, correções e verificações, envia os quatro arquivos para um rascunho, confere os anexos e então publica a Release estável. Releases já publicadas não têm seus arquivos substituídos; crie uma nova versão para novas alterações. Uma correção apenas na descrição pode ser feita na Release existente sem mudar seus anexos nem sua tag. Não execute a publicação manual ao mesmo tempo que o workflow automático da mesma tag.

A documentação oficial explica a [criação de Releases pelo GitHub CLI](https://cli.github.com/manual/gh_release_create), o [envio de anexos](https://cli.github.com/manual/gh_release_upload) e a [compilação de .NET no GitHub Actions](https://docs.github.com/en/actions/tutorials/build-and-test-code/net).
