# Controle de versões

## 1.6.0 — 12/10/2026

Pedidos e entregas redesenhados no padrão do programa: pesquisa por pedido, cliente, telefone e endereço, filtros de período e situação, paginação e indicadores coloridos para entrega, retirada e andamento. Menu de cada pedido permite consultar detalhes, imprimir, alterar a situação e receber no PDV. Pedidos encerrados permanecem no histórico e bloqueiam alteração/recebimento; reservas e preços do pedido são preservados.

Publicações do GitHub passam a exigir notas próprias da versão com novidades, correções e verificações. As instruções de instalação ficam ao final. Notas ausentes, de outra versão ou incompletas impedem a publicação; rascunhos retomados recebem as notas atualizadas.

Compilação sem avisos ou erros, 273 verificações de negócio e 47 verificações das notas/publicação aprovadas. Conferência nativa validou busca, limites de datas, páginas de 10/20/50, seleção, menus, alteração de situação, detalhes e recebimento no PDV com preços históricos, quantidades, cliente, desconto e entrega preservados. Controles acessíveis em janela menor; nenhum dado real foi alterado.

## 1.5.2 — 12/10/2026

Conferência visual usa a largura real da janela, respeitando a área disponível em computadores com telas menores. O limite entre duas e quatro colunas é verificado separadamente, e valores e cartões continuam sendo conferidos dentro da área renderizada. O diagnóstico de falhas visuais passa a aparecer também no registro da verificação do pacote.

Versão pública do Controle do caixa com cartões ilustrados, histórico e português brasileiro. As tags 1.5.0 e 1.5.1, cuja publicação foi interrompida por diferenças do ambiente de conferência, permanecem no histórico.

## 1.5.1 — 12/10/2026

Aplicativo usa português brasileiro também quando o Windows está configurado em outro idioma. A data, os valores nos formulários e os relatórios usam a mesma cultura da interface. Conferência dos cartões aceita diferenças de espaço na formatação monetária entre versões do Windows, mantendo a verificação dos valores.

A publicação de 1.5.0 foi interrompida pela conferência do idioma no ambiente do GitHub; sua tag permanece no histórico de versões.

## 1.5.0 — 12/10/2026

Controle do caixa redesenhado com cartões ilustrados por ondas e cores para dinheiro, Pix, débito e crédito. Os cartões exibem o caixa aberto e permanecem independentes do filtro do histórico. Histórico de caixas e suprimentos/sangrias recebem filtros por período, indicadores e mensagens quando não há registros.

Menu de cada caixa com detalhes, impressão de fechamento e exportação de relatório CSV. A impressão de fechamento exige caixa encerrado; detalhes e exportação também podem consultar caixas abertos, sem apresentar contagem ou diferença de fechamento antes da conferência. Histórico de caixas encerrados permanece preservado.

Conferência nativa aprovada em 1480 × 940 e 1200 × 760: cartões em quatro e duas colunas, valores de vendas e recebimentos por pagamento, fundo e movimentações, limites de datas, estados vazios, seleção e menu. Detalhes e CSV consultados sem alterar registros; exportação preserva números negativos e protege nomes e observações ao abrir em planilha. Compilação sem avisos ou erros; testes somente em dados isolados.

## 1.4.3 — 12/10/2026

O catálogo de produtos da frente de caixa passa a usar a mesma rolagem reduzida e proporcional ao touchpad, nas visualizações em grade e lista. Gestos consecutivos são acumulados e os limites da área são respeitados. Fotos, pesquisa, filtros e inclusão de produtos na venda permanecem disponíveis.

Conferência nativa aprovada nas duas visualizações para roda, pequenos deltas, eventos consecutivos, inversão e limites. Rolar sobre cartões visíveis preserva quantidades e total; o clique continua adicionando somente o produto escolhido, e a busca por nome funciona depois de rolar. Compilação sem avisos ou erros. Nenhum dado real foi alterado.

## 1.4.2 — 12/10/2026

Rolagem reduzida e proporcional ao touchpad também no carrinho da venda, nos produtos em estoque e nas últimas movimentações. Os gestos pequenos avançam em pixels, com limites no início e no fim da lista. As grades continuam usando virtualização; seleção e ações dos produtos permanecem disponíveis.

Conferência nativa aprovada para passos da roda, pequenos deltas nos dois sentidos, sequência de eventos sem layout intermediário, inversão e deslocamento externo da barra. Quantidades, total, dados gravados, seleção, rolagem horizontal, botões do carrinho e menu do produto foram preservados. Compilação sem avisos ou erros; testes realizados somente em dados isolados.

## 1.4.1 — 12/10/2026

Confirmação de atualização redesenhada no padrão do Giro Bebidas: título e versão em destaque, cartão de cópia de segurança, caminho completo selecionável e ações Atualizar agora e Agora não. Cancelar, Esc ou fechar a janela mantém o PDV aberto. A verificação do instalador, o backup e a validação de caixa fechado e carrinho vazio permanecem no fluxo de atualização.

Rolagem das listas abertas de opções, como categorias e ordenação, com sensibilidade reduzida e resposta proporcional aos gestos do touchpad. Seleção por clique e teclado preservada.

Conferência nativa aprovada: confirmação modal, cancelamento, Esc e fechamento; caminho longo selecionável; backup e dados preservados. Lista real de ordenação verificada com roda, pequenos deltas nos dois sentidos, limites e seleção por teclado e automação. Compilação sem avisos ou erros. Nenhum instalador foi aberto pelas escolhas simuladas.

## 1.4.0 — 12/10/2026

Produtos e estoque redesenhado com miniaturas, categorias e situações coloridas, filtros por categoria, busca por nome/código/barras/categoria e ordenação pelo seletor, botão de direção ou cabeçalho. Os totais físico, reservado e livre permanecem separados. O cartão inferior exibe as cinco movimentações mais recentes, com entradas e saídas diferenciadas, e abre o histórico completo.

Menu por produto com detalhes, duplicar, ativar/desativar e excluir. Duplicar abre um cadastro para conferência, mantendo foto, descrição, categoria personalizada, valores e dados fiscais, com nova identificação, código sugerido livre, código de barras vazio e estoque zero. A exclusão é restrita a cadastros sem estoque e sem histórico de vendas, pedidos ou movimentos; registros cancelados continuam protegidos. Desativação preserva o cadastro e é bloqueada quando há reservas. Ações exigem administrador e ficam registradas na auditoria.

273 verificações de regras, permissões e persistência aprovadas, incluindo rejeições sem alteração dos dados. Aplicativo, instalador e desinstalador usam a versão 1.4.0.

Conferência nativa WPF aprovada para busca, filtros, ordenação crescente/decrescente, miniaturas, sinais e cores das movimentações, menu por linha, ativação persistida, duplicação independente, detalhes e histórico completo. Layout renderizado em janela normal e de 1200 × 760. Nenhum dado real do depósito foi alterado durante os testes.

## 1.3.0 — 11/10/2026

Indicador de caixa transformado em botão com seta e menu arredondado. A ação principal alterna entre Abrir caixa e Fechar caixa conforme a situação atual. Histórico de caixas leva diretamente à tabela de aberturas e fechamentos; Configurações do caixa permite ao administrador ajustar o fundo de troco sugerido e a impressão automática dos cupons.

As ações reutilizam a abertura e o fechamento com confirmação de valores. Operadores mantêm acesso às operações e ao histórico; as configurações são protegidas no menu e no serviço. O fundo sugerido não movimenta dinheiro, aceita valores não negativos com até duas casas decimais e é preservado no backup. Backups anteriores recebem valor zero. Cores vermelho/verde, pesquisa, grade/lista e rodapé da versão 1.2.0 permanecem.

201 verificações de regras, persistência, backup e permissões aprovadas. Compilação sem avisos ou erros. Menu e formulário renderizados no aplicativo WPF; ação atualizada sem reabrir, fechamento por Esc e clique externo, acesso ao histórico e restrição de configurações para operadores conferidos em dados isolados.

## 1.2.0 — 11/10/2026

Frente de caixa redesenhada a partir da referência visual: cabeçalho com ícone, categorias ilustradas, cartões adaptáveis ao tamanho da janela, alternância entre grade e lista, contagem de resultados e mensagem para pesquisa sem resultados. O carrinho apresenta miniaturas dos produtos, controles de quantidade e valores em destaque, com rolagem na janela menor.

Corrigido o recorte do texto digitado: os campos aplicavam o espaço interno duas vezes no host de edição. A pesquisa mantém ícone, texto e atalho em colunas próprias. Os campos de cadastro e de repositório também receberam a correção do espaço duplicado.

O funcionamento das vendas, preços de atacado, pedidos reservados, leitura de códigos e permissões foi preservado. Aplicativo, instalador e desinstalador compartilham a versão 1.2.0.

O indicador de caixa fechado passa a usar fundo vermelho claro, borda suave e texto vermelho; caixa aberto mantém o verde. Desconto e entrega são confirmados e validados antes de abrir o pagamento ou salvar o pedido, incluindo finalização por F4 com o campo ainda em edição.

Rodapé com ponto verde em Dados locais, textos maiores e mais escuros, e atalhos F2, F4 e F5 destacados para facilitar a leitura.

171 verificações de regras aprovadas, compilação sem avisos ou erros e conferência nativa WPF das pesquisas por nome, código e código de barras nas duas visualizações. Enter e F2, imagens do carrinho, bloqueio de pedidos reservados, confirmação de valores em edição, validação de texto inválido e cores do caixa foram verificados com dados isolados. Layout renderizado em janela normal e de 1200 × 760.

## 1.1.1 — 11/10/2026

Painel de atualizações redesenhado com dois cartões, ícones verdes, versão instalada em destaque, ações organizadas e campo de repositório com opção de mostrar ou ocultar. O conteúdo se adapta à janela menor e permite rolagem.

Opções do aplicativo usam interruptores: verde para ativado e cinza para desativado. Mantêm os vínculos de configuração, navegação por teclado e nomes acessíveis.

A publicação no GitHub passa a conferir o rascunho pelo ID da Release, corrigindo a consulta que retornava 404 antes da publicação. Versões já publicadas continuam protegidas contra substituição.

171 verificações automatizadas aprovadas. Painel renderizado em janela normal e de 1200 × 760, com confirmação de que mostrar/ocultar preserva o endereço, o interruptor alterna pela automação e o botão de instalação permanece acessível pela rolagem.

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
