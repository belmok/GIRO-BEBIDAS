# Controle de versões

## 1.1.0 — 11/10/2026

Atualizações por GitHub configuradas para `belmok/GIRO-BEBIDAS`, com consulta automática opcional, comparação de versões estáveis, download cancelável, limites e conferência SHA-256. A instalação exige caixa fechado e carrinho vazio, cria backup e abre o instalador na pasta existente. Workflow e script de publicação preparados; o envio ao repositório requer permissão de escrita.

Novo ícone multirresolução para aplicativo, instalador e desinstalador. Seletores e barras com visual próprio, categorias sem corte, destaque verde ao passar o mouse, menu com touchpad e item selecionado visível, carrinho com maior área para produtos e rodapé adaptável. O instalador gerado é copiado para a pasta principal.

171 verificações automatizadas aprovadas, incluindo regras do PDV, permissões, backup e cenários de atualização. 26 verificações WPF isoladas aprovadas; módulos e layout de 1200 × 760 renderizados no aplicativo real. A emissão SEFAZ continua pendente e os comprovantes permanecem não fiscais.

## 1.0.0 — 11/10/2026

Primeira entrega do Giro Bebidas. Aplicativo, instalador e desinstalador usam C# + WPF e compartilham a versão definida em `Directory.Build.props`. Todo o código está no repositório Git local, na branch `main`, com histórico de implementação e tag `v1.0.0`.

Inclui login ADMIN/ADMIN, usuários com perfis, produtos com fotos e descrições, preços de varejo/atacado, estoque, pedidos com reservas, clientes e fiados, caixa, histórico de vendas, relatórios, impressão não fiscal, cadastro de equipamentos e backup. Acrescenta configuração de NFC-e (emitente, CRT, CNPJ numérico/alfanumérico, UF/IBGE, ambiente, série/numeração, A1, CSC e impressora), campos fiscais de produtos e conferência de pendências. Certificado, senha e CSC são protegidos pelo Windows. A emissão SEFAZ não está integrada.

Verificação da entrega:

- 124 verificações automatizadas de operações, autenticação, permissões, persistência, backup, instalação e configuração fiscal, incluindo bloqueio temporário de arquivo, proteção do A1/CSC, restauração e independência dos ambientes.
- Aplicativo, instalador e desinstalador compilados sem avisos ou erros.
- Executáveis independentes com o .NET incluído.
- Pacote extraído em uma pasta de teste; o executável instalado foi aberto e os módulos foram renderizados.
- Foto importada, gravada e incluída no backup.
- Remoção dos executáveis verificada com preservação de dados e de arquivos adicionais.
- Telas de login, PDV, cadastros, módulos, instalador e desinstalador conferidas visualmente.

Não houve instalação nos diretórios do usuário, criação de atalhos ou alteração do registro durante os testes. Impressoras e balanças físicas não foram testadas. A versão é local, para um computador, com comprovante não fiscal.

Os arquivos de distribuição ficam em `artifacts/distribuicao/1.0.0/`, com hashes SHA-256. São gerados por `scripts/Gerar-Distribuicao.ps1`. Os executáveis e dados de teste ficam fora do histórico Git; o código e os scripts necessários para reproduzi-los são versionados.

Para as próximas versões, altere `Directory.Build.props`, registre as mudanças neste documento, execute os testes, gere a distribuição e crie uma nova tag. Uma atualização do aplicativo preserva a pasta separada dos dados do depósito.
