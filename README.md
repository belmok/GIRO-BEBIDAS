# GIRO-BEBIDAS

PDV Giro Bebidas em C# + WPF.

Aplicativo para Windows, com identidade e depósito fictícios, desenvolvido em C# e WPF (.NET 8). Aplicativo, instalador e desinstalador fazem parte desta solução e são versionados no mesmo repositório Git.

## Executar a versão pronta

Abra `InstalarGiroBebidas.exe` na pasta principal, ou `artifacts/distribuicao/1.5.0/InstalarGiroBebidas.exe`. O instalador cria atalhos, registra a remoção nos aplicativos do Windows e inclui o desinstalador. A distribuição contém o .NET e funciona sem instalar o runtime separadamente em Windows 10/11 de 64 bits.

Também é possível abrir `GiroBebidas.exe` diretamente ou extrair o pacote portátil. O desinstalador atua sobre instalações feitas pelo instalador; para a versão portátil, basta remover os executáveis. Os dados ficam em outra pasta.

**Primeiro acesso: usuário `ADMIN`, senha `ADMIN`.**

O cadastro inicial contém 16 produtos fictícios para experimentar. Não há vendas, clientes ou caixas falsos na primeira execução normal. Ajuste o cadastro e o estoque antes de usar dados reais.

## Recursos

- Login, senhas com PBKDF2-SHA256, cadastro de usuários e perfis administrador/operador.
- Cadastro de produtos com fotos, descrição, SKU, código de barras, custo, varejo, atacado, unidades e estoque mínimo.
- Painel de estoque com fotos, busca/filtros, ordenação, indicadores, detalhes, duplicação e menu de ações, com proteção de histórico na exclusão.
- PDV com busca, leitor USB que funciona como teclado, produtos por peso, desconto, entrega, troco e cinco formas de pagamento.
- Frente de caixa com categorias ilustradas, grade adaptável, apresentação em lista, fotos no carrinho e pesquisa com texto legível.
- Pedidos de entrega ou retirada, reservas de estoque e conclusão no PDV.
- Clientes, limite de fiado, extrato e recebimentos parciais.
- Abertura, suprimento, sangria e fechamento de caixa por forma de pagamento.
- Menu no indicador do caixa para abrir/fechar, consultar o histórico e configurar fundo de troco sugerido e impressão automática.
- Histórico, cupom com endereço de entrega, cancelamento com estorno e exportação CSV.
- Relatório de lucratividade com custo histórico e rateio de desconto entre produtos.
- Cadastro de impressoras do Windows, papel 58/80 mm e A4, impressão automática opcional.
- Cadastro e teste de balanças seriais 8N1, leitura de texto em kg ou inteiro em gramas, pesagem manual.
- Backup completo com fotos e usuários, cópias diárias, restauração e registro de operações.
- Configuração de NFC-e, certificado A1/CSC protegidos, campos fiscais de produtos e conferência das pendências.
- Atualizações por Releases públicas do GitHub, com download conferido e backup antes da instalação.
- Painel de atualizações com cartões, ícones e opção de mostrar ou ocultar o repositório.
- Interruptores verdes para opções ativadas e cinza para opções desativadas, carrinho ampliado, destaque ao passar o mouse e rolagem do menu pelo touchpad.

## Persistência

Os dados ficam em `%LOCALAPPDATA%\GiroBebidas\giro-dados.json`. São gravados atomicamente, com cópia `.bak` da versão anterior. Antes da primeira alteração de cada dia é criada uma cópia em `Backups/`. A restauração preserva os dados anteriores em um arquivo separado e encerra a sessão.

O aplicativo usa uma única instância por pasta de dados. A gravação de uma venda, baixa do estoque e lançamento no caixa ocorre em uma única transação do repositório. O arquivo é local: esta versão é para um computador, sem servidor compartilhado ou sincronização entre terminais. O login protege o uso do aplicativo; quem tem acesso direto à pasta do Windows pode ler ou alterar os arquivos. Senhas de login são armazenadas como hashes; certificado A1, sua senha e tokens CSC são protegidos pelo Windows. Os demais cadastros, fotos e movimentos do backup não são criptografados.

## Limites da versão

A tela **Cupom fiscal** permite configurar emitente, CRT, UF/IBGE, ambiente, série, numeração, certificado A1, CSC e impressora. Homologação e produção mantêm parâmetros separados. Os produtos têm campos opcionais de NCM, CEST, CFOP, CSOSN/CST e origem. Certificado, senha e CSC são protegidos com DPAPI para o usuário do Windows e incluídos no backup; em outro computador ou usuário do Windows, precisam ser cadastrados novamente. A conferência local não verifica credenciamento, cadastro público, cadeia ICP-Brasil ou tributação. A emissão SEFAZ não está integrada: não gera XML fiscal, chave, QR Code, protocolo ou DANFE autorizado.

O cupom é **não fiscal**. Não há emissão de NF-e/NFC-e, TEF, confirmação bancária automática de Pix ou integração de maquininha. As formas de pagamento registram o recebimento informado pelo operador. Impressoras exigem driver instalado; balanças dependem de protocolo compatível e precisam ser testadas no equipamento real. Não há balança conectada neste projeto. Cancelamentos são permitidos no caixa de origem ainda aberto, sem recebimentos de fiado já efetuados. O lucro mostrado é bruto dos produtos, excluindo frete, taxas financeiras e despesas gerais.

## Projetos e versões

- `src/GiroBebidas.Core`: dados, autenticação, regras do depósito e instalação segura.
- `src/GiroBebidas.App`: aplicativo WPF.
- `src/GiroBebidas.Setup`: instalador WPF com pacote embutido.
- `src/GiroBebidas.Uninstaller`: desinstalador WPF.
- `tests/GiroBebidas.Tests`: verificações de comportamento, sem dependências de testes externas.

A versão de todos os programas é definida em `Directory.Build.props`. Alterações são registradas em commits e tags de versão. O remoto configurado é `belmok/GIRO-BEBIDAS`. A publicação automática e manual está descrita em `docs/ATUALIZACOES-GITHUB.md`.

## Compilar e testar

Requer o SDK .NET 8 no Windows. Os projetos não usam bibliotecas externas. `NuGet.Config` permite compilação normal sem conexão; a geração dos executáveis independentes baixa apenas os pacotes oficiais de runtime da Microsoft.

```powershell
dotnet build GiroBebidas.sln -c Release -m:1
dotnet run --project tests/GiroBebidas.Tests -c Release
dotnet run --project src/GiroBebidas.App -c Release
```

Para gerar aplicativo, instalador, desinstalador e pacote portátil:

```powershell
./scripts/Gerar-Distribuicao.ps1
```

O script usa pastas de construção isoladas, testa as regras antes de publicar e gera hashes SHA-256 dos arquivos de distribuição. Para testes visuais isolados, o aplicativo aceita `--ui-smoke --data-dir <pasta-vazia-de-teste> --output <pasta-de-imagens>`. Dados de teste nunca são enviados no instalador.

Veja `docs/GUIA-DO-USUARIO.md` para o uso diário.

O histórico da entrega está em `docs/VERSOES.md`. Para verificar o pacote em uma pasta isolada, sem criar atalhos ou alterar o registro do Windows, execute `./scripts/Verificar-Distribuicao.ps1`.
