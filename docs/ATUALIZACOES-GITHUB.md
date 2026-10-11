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

Para uma próxima versão, ajuste `Version`, `AssemblyVersion` e `FileVersion` em `Directory.Build.props`. Registre as alterações e envie o commit. Crie uma tag exatamente igual a `v` mais o campo `Version`, por exemplo `v1.2.0` quando `Version` for `1.2.0`, e envie essa tag:

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

A última chamada confere os arquivos e mostra o que será publicado, sem realizar envios. Depois que o commit e a tag correspondente já estiverem no GitHub, execute:

```powershell
./scripts/Publicar-GitHub.ps1 -Publicar
```

O script verifica o SHA-256, a versão do instalador, o commit usado na compilação e a tag remota. Ele envia os quatro arquivos para um rascunho, confere os anexos e então publica a Release estável. Releases já publicadas não são substituídas; crie uma nova versão para novas alterações. Não execute a publicação manual ao mesmo tempo que o workflow automático da mesma tag.

A documentação oficial explica a [criação de Releases pelo GitHub CLI](https://cli.github.com/manual/gh_release_create), o [envio de anexos](https://cli.github.com/manual/gh_release_upload) e a [compilação de .NET no GitHub Actions](https://docs.github.com/en/actions/tutorials/build-and-test-code/net).
