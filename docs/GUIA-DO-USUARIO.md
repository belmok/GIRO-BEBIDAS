# Guia do Giro Bebidas — versão 1.8.1

## Instalação e primeiro acesso

1. Abra **InstalarGiroBebidas.exe** e escolha **Instalar agora**. Não é necessário acesso de administrador: a instalação é feita para o seu usuário do Windows.
2. Abra o atalho **Giro Bebidas**.
3. Entre com usuário **ADMIN** e senha **ADMIN**.
4. Em **Configurações**, personalize os dados do depósito. A senha pode ser alterada em **Alterar minha senha**.
5. Os produtos, estoques e códigos de barras iniciais são exemplos fictícios. Edite-os ou desative-os e cadastre os produtos do seu depósito.

## Usuários e senhas

Em **Usuários → Criar usuário**, informe nome, usuário, perfil, senha e confirmação. Use **Editar / redefinir senha** para alterar dados ou redefinir o acesso. Deixe os campos de senha vazios para manter a senha atual. Desmarque **Usuário ativo** para suspender o acesso.

O administrador pode usar todas as funções. O operador pode vender, controlar pedidos, cadastrar clientes, receber fiados e operar o caixa. Somente o administrador acessa produtos, relatórios de lucro, equipamentos, usuários, configurações, configuração fiscal, cancelamento de venda e backup. O sistema impede a remoção do último administrador ativo e a desativação da própria conta.

## Cadastrar produtos e fotos

Abra **Produtos e estoque → Novo produto**. Escolha a foto do computador nos formatos PNG, JPG, JPEG, BMP ou GIF; o sistema adapta a imagem para o cadastro. Preencha nome, descrição, SKU, código de barras, categoria, unidade, embalagem, custo, preço de venda, preço e quantidade mínima de atacado, estoque inicial e estoque mínimo.

Use unidade **kg** para vender por peso. **un** e **cx** exigem quantidades inteiras. Um produto cadastrado como caixa tem seu próprio estoque em caixas; a versão atual não converte automaticamente caixas em unidades. Deixe preço e quantidade de atacado em zero para desativar o atacado. O preço de atacado entra automaticamente quando a quantidade da venda atinge o mínimo.

Depois do cadastro, alterações de quantidade são feitas por **Entrada / ajuste**. Informe quantidade, custo e motivo ou fornecedor. Ajustes não podem consumir produtos reservados em pedidos. Desative produtos para retirá-los da venda sem apagar o histórico.

Em **Produtos e estoque**, a busca aceita nome, código, código de barras ou categoria. Use o filtro de categoria e a ordenação por nome, código, quantidades, preço ou situação. O botão de setas inverte a ordem; clicar no cabeçalho também ordena a tabela. As fotos, categorias e indicadores ajudam a identificar os produtos. **Físico** é a quantidade total; **Reservado** pertence aos pedidos; **Livre** é o que pode ser vendido.

Na coluna **Ações**, o lápis edita o produto. O menu de três pontos permite ver detalhes, duplicar, ativar/desativar e excluir. A duplicação abre um novo cadastro com foto, descrição e preços copiados, código diferente, código de barras vazio e estoque inicial zero; confira os dados antes de salvar. Desativar preserva estoque e histórico e retira o produto do PDV. Produtos reservados em pedidos precisam continuar ativos.

A exclusão exige confirmação e só é permitida quando o produto não possui estoque, reserva, vendas, pedidos ou movimentações. Produtos já utilizados devem ser desativados para preservar os registros, incluindo operações canceladas. Essas ações são restritas ao administrador.

O cartão **Últimas movimentações** mostra os cinco registros mais recentes, com entradas em verde e saídas em vermelho. **Ver todas** abre o histórico completo de movimentações.

## Abrir o caixa e vender

Em **Caixa → Abrir caixa**, informe o fundo de troco. Há um único caixa aberto por computador, utilizado pelos usuários autorizados.

O indicador no alto da tela fica verde quando o caixa está aberto e vermelho claro quando está fechado.

Clique nesse indicador para abrir o menu. A primeira opção alterna entre **Abrir caixa** e **Fechar caixa**, mantendo a confirmação dos valores. **Histórico de caixas** leva à tabela de aberturas e fechamentos. O menu também funciona pelo teclado e pode ser fechado com **Esc**.

Em **Configurações do caixa**, o administrador pode definir o fundo de troco sugerido e ativar ou desativar a impressão automática dos cupons. O fundo sugerido preenche a abertura, mas pode ser ajustado antes de confirmar; salvar a configuração não abre nem movimenta o caixa. A opção de configurações fica desabilitada para operadores.

No **Frente de caixa**, clique no produto ou busque pelo nome, SKU ou código de barras. **F2** posiciona o cursor na busca; **Enter** adiciona o produto encontrado. O leitor USB deve estar configurado para digitar como teclado e enviar Enter depois do código.

Use as categorias com ícones para filtrar os produtos. Os botões **Exibir em grade** e **Exibir em lista**, ao lado da quantidade encontrada, alternam a apresentação sem alterar a pesquisa nem os produtos da venda. A grade adapta a largura dos cartões ao tamanho da janela. Quando a pesquisa não encontra produtos, a tela informa isso; apague a busca ou escolha outra categoria.

A rolagem do catálogo usa movimentos menores e proporcionais ao touchpad, tanto na grade quanto na lista. Você pode rolar sobre os cartões sem adicionar produtos à venda; para incluir um item, clique nele ou no sinal **+**.

O carrinho mostra a foto cadastrada ou a ilustração do produto, quantidade, preço aplicado e total de cada item. Os botões **+**, **−** e **Remover produto** ajustam a seleção. Desconto, entrega, subtotal e total ficam na parte inferior. Em janelas menores, use a rolagem para acessar os produtos e todas as ações.

Escolha o cliente quando necessário, ajuste quantidades, desconto em reais e taxa de entrega. **F4** abre o pagamento. Selecione dinheiro, Pix, débito, crédito ou fiado. Para dinheiro, informe o recebido e confira o troco. Para fiado, é obrigatório um cliente com limite disponível. O registro de Pix ou cartão é manual: confira o recebimento na sua conta ou maquininha.

Ao finalizar ou salvar um pedido, o sistema confirma os valores de desconto e entrega ainda em edição. Isso vale também ao pressionar **F4** sem sair do campo; valores que não sejam números precisam ser corrigidos antes de continuar.

Ao confirmar, venda, estoque e caixa são gravados juntos. O campo de observações e os dados de entrega podem aparecer no cupom. Após uma falha de impressão, a venda permanece registrada: reimprima em **Vendas**.

## Pedidos e entregas

Monte os produtos no PDV e clique **Salvar como pedido**. Escolha cliente, entrega ou retirada, endereço e observações. O pedido reserva o estoque e pode ser acompanhado em **Pedidos e entregas**.

Use a pesquisa para encontrar número do pedido, cliente, telefone ou endereço. Combine os filtros de período e situação. No rodapé, escolha 10, 20 ou 50 pedidos por página e use as setas para navegar. O histórico completo e as reservas não são alterados pelos filtros.

O menu de três pontos de cada pedido oferece **Ver detalhes**, **Imprimir pedido**, **Alterar situação** e **Receber no PDV**. As situações e os tipos de atendimento aparecem com cores. Pedidos concluídos ou cancelados continuam disponíveis para consulta e impressão, com alteração e recebimento desativados.

Use **Alterar situação** para marcar pendente, preparando, em entrega ou cancelado. Para receber e concluir, selecione o pedido e clique **Receber no PDV**. Os produtos e preços ficam preservados; finalize o pagamento normalmente. Pedido cancelado libera sua reserva. O comprovante de pedido não comprova pagamento.

## Fiados

Em **Clientes e fiados**, consulte o saldo devedor e o limite. Selecione o cliente e clique **Extrato de fiados** para ver vendas e recebimentos. **Receber fiado** permite recebimento parcial ou integral no caixa aberto. O valor é aplicado às dívidas mais antigas e não pode superar o saldo.

## Caixa, histórico e lucro

Em **Controle do caixa**, os cartões ilustrados mostram dinheiro em verde, Pix em azul, débito em violeta e crédito em âmbar. O dinheiro inclui fundo, vendas, recebimentos de fiado, suprimentos e sangrias. Os demais cartões incluem vendas e recebimentos por forma de pagamento. Na janela menor, os cartões ficam em duas linhas.

**Suprimento** adiciona dinheiro físico, e **sangria** registra retirada. Ambas exigem motivo. **Fechar caixa** mostra o resumo por pagamento; informe o dinheiro contado. O fechamento registra a diferença entre o esperado e o contado: faltas aparecem em vermelho, sobras em âmbar e conferência sem diferença em verde.

Escolha **Hoje**, **Últimos 7 dias**, **Últimos 30 dias** ou **Todo o período** para filtrar o histórico e as movimentações. O filtro não altera os totais do caixa aberto. No menu de três pontos de cada caixa, use **Ver detalhes**, **Imprimir fechamento** ou **Exportar relatório** em CSV. A impressão exige caixa encerrado; contagem e diferença ficam disponíveis após o fechamento. Os registros encerrados permanecem preservados.

Em **Vendas**, consulte detalhes, reimprima o cupom ou exporte CSV. O administrador pode cancelar uma venda no caixa de origem ainda aberto, desde que não tenha recebimento de fiado. O estoque volta e os totais de caixa são ajustados. Não se cancela a mesma venda duas vezes.

Em **Relatórios**, escolha o período e consulte receita dos produtos, custos históricos, lucro e margem bruta. O desconto é distribuído entre os produtos; canceladas ficam fora do resultado. A entrega e as despesas operacionais não entram no lucro bruto dos produtos.

## Impressoras e balanças

Em **Equipamentos → Adicionar impressora**, escolha uma impressora já instalada no Windows, dê um nome e escolha o papel: 58 mm, 80 mm ou A4. Marque como padrão. Ajuste também o tamanho do papel no driver da impressora. Em **Configurações**, ative a impressão automática se quiser. É possível escolher uma impressora PDF do Windows para salvar um cupom.

Em **Adicionar balança**, informe nome, porta COM, velocidade e formato de leitura. A conexão preparada é serial **8 bits, sem paridade, 1 stop bit (8N1)**. Formatos disponíveis: texto decimal em kg, como `1.250 kg`, e inteiro em gramas, como `001250`. Um comando de solicitação em hexadecimal pode ser informado se o manual exigir. Use **Testar leitura** para verificar.

Esse recurso precisa ser compatível com o protocolo real da balança. USB só funciona neste módulo se o equipamento disponibilizar uma porta COM serial. Outros protocolos exigem adaptação. Sem balança, digite o peso em kg manualmente na venda.

## Configuração do cupom fiscal

O administrador encontra a tela **Configuração fiscal** pelo menu **Cupom fiscal**. Ela reúne os parâmetros de **NFC-e, modelo 65**, separadamente dos dados do depósito, backup e registro de operações, que ficam somente em **Configurações**. A emissão ainda depende de integração com um emissor SEFAZ; preencher esta tela não transforma o comprovante atual em documento fiscal.

1. Em **Dados da empresa**, informe razão social, nome fantasia, CNPJ, inscrição estadual, regime tributário (CRT), UF, município, código IBGE e endereço. O cadastro aceita CNPJ numérico e alfanumérico e confere os dígitos verificadores. É possível salvar um cadastro parcial.
2. Em **Configurar NFC-e**, escolha homologação ou produção, série, próximo número, identificação do caixa e uma impressora cadastrada em **Equipamentos**. Cada ambiente mantém sua própria série e numeração. Vendas não fiscais não consomem esses números.
3. Em **Importar A1**, escolha o certificado `.pfx` ou `.p12` e informe a senha. O sistema verifica a abertura, a presença da chave privada e a validade. Não certifica a cadeia ICP-Brasil nem a titularidade do emitente. Certificados A3, tokens e cartões não são atendidos neste módulo.
4. Em **Cadastrar / trocar CSC**, selecione o ambiente e informe identificador e token fornecidos pela SEFAZ. Os códigos de homologação e produção ficam separados; o token salvo não é exibido.
5. No cadastro dos produtos, preencha NCM, CEST quando aplicável, CFOP de saída, CSOSN ou CST de ICMS e origem da mercadoria, conforme orientação contábil. Esses campos verificam o formato e não calculam os tributos nem substituem uma configuração tributária completa.
6. No cartão da impressora, use **Configurar impressora** para escolher uma impressora já cadastrada e salvar. **Cadastrar em Equipamentos** abre o cadastro quando necessário; a impressora fiscal pode ser diferente da impressora padrão do PDV.
7. Use **Atualizar conferência** para atualizar as pendências e ver quantos itens estão conferidos localmente. A conferência é local, sem consultar credenciamento ou autorização na SEFAZ. Mesmo com os campos preenchidos, a integração do emissor permanece pendente nesta versão.
8. **Revisar produtos**, no cartão de natureza da operação e tributos, abre **Produtos e estoque**. Use **Editar produto** para conferir NCM, CEST, CFOP, CSOSN/CST e origem de cada mercadoria.

Certificado A1, sua senha e o token CSC são protegidos pelo Windows e acompanham o backup. Ao restaurar em outro computador ou usuário do Windows, reimporte o A1 e cadastre os CSCs novamente. Mantenha também o arquivo original do certificado em local seguro.

A referência enviada descreve SAT/MFE (CF-e modelo 59). Esta versão concentra os parâmetros em NFC-e. Para São Paulo, a emissão de CF-e-SAT foi vedada a partir de 1º de janeiro de 2026, conforme a [SEFAZ-SP](https://legislacao.fazenda.sp.gov.br/Paginas/RC32314_2025.aspx). A configuração e o credenciamento devem seguir a SEFAZ da UF do estabelecimento.

## Backup

Em **Configurações**, os dados do depósito ficam em campos com ícones. Use os interruptores verdes para ativar a impressão automática e o backup diário; cinza indica desativado. Clique **Salvar configurações** para gravar as alterações. A seção **Backup e recuperação** reúne criar, restaurar e abrir a pasta de dados.

Abra **Configurações → Criar backup** e salve o arquivo em outra unidade ou na nuvem. O backup inclui produtos, fotos, usuários, clientes, pedidos, caixa, vendas e configuração fiscal, com certificado e CSC protegidos pelo Windows. Também existe uma cópia diária automática na pasta `Backups` e uma cópia `.bak` da gravação anterior.

Para recuperar, feche o caixa, limpe ou salve a venda atual e escolha **Restaurar backup**. O botão fica desativado enquanto o caixa estiver aberto ou houver produtos na venda. Confirme o arquivo. O sistema valida a versão, preserva uma cópia dos dados atuais e pede novo login. Os usuários e senhas voltam ao estado do backup restaurado. Os backups têm dados pessoais e devem ser guardados em local de acesso restrito.

## Atualizações do GitHub

O administrador encontra **Atualizações** no menu. O sistema consulta por padrão o repositório público **belmok/GIRO-BEBIDAS** ao entrar. Os cartões mostram a versão instalada, a configuração e a disponibilidade de novas versões. Use **Mostrar/Ocultar** para exibir ou esconder o endereço na tela. Você pode alterar o endereço ou desativar a consulta automática. A consulta não interrompe o atendimento nem instala arquivos automaticamente.

As opções usam interruptores: **verde** significa ativado e **cinza** significa desativado. Clique no interruptor ou no texto ao lado; pelo teclado, use **Tab** para selecionar e **Espaço** para alternar. As mudanças de configuração continuam exigindo o botão **Salvar** da tela.

Use **Verificar atualizações** para procurar uma versão estável mais recente. A publicação precisa conter `InstalarGiroBebidas.exe` e `SHA256.txt`. A tela mostra a versão e suas novidades. O botão **Baixar e instalar atualização** exige caixa fechado, carrinho vazio e uma cópia instalada pelo instalador. O download é conferido com SHA-256, e o sistema cria um backup na pasta `Backups` antes de abrir o instalador. A confirmação mostra a versão, o nome e o caminho selecionável do backup: escolha **Atualizar agora** para fechar o PDV e abrir o instalador, ou **Agora não** para continuar no sistema. Esc e o botão de fechar também cancelam a confirmação. Instale na pasta já selecionada e reabra o aplicativo. Dados e fotos ficam preservados na pasta de dados.

Se o download falhar ou for cancelado, tente novamente. Se ainda não houver uma publicação pública mais recente, o sistema informa essa situação. Cópias portáteis devem ser atualizadas manualmente pelo instalador. O fluxo não usa senhas ou tokens do GitHub e atende repositórios públicos.

Para publicar uma versão, consulte [Atualizações via GitHub](ATUALIZACOES-GITHUB.md).

## Desinstalar

Use **Desinstalar Giro Bebidas** no menu Iniciar ou no painel de aplicativos do Windows. Feche o PDV antes. Os dados são preservados por padrão. A opção **Excluir também todos os dados, fotos e backups locais** apaga a pasta local do depósito, mediante confirmação. Backups exportados para outras pastas são preservados.

O aplicativo gera cupom não fiscal. Esta versão não emite NF-e/NFC-e e não é um sistema compartilhado entre vários computadores.
