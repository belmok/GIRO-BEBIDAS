# Controle de versões

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
