# Guia do Giro Bebidas — versão 1.0.0

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

## Abrir o caixa e vender

Em **Caixa → Abrir caixa**, informe o fundo de troco. Há um único caixa aberto por computador, utilizado pelos usuários autorizados.

No **Frente de caixa**, clique no produto ou busque pelo nome, SKU ou código de barras. **F2** posiciona o cursor na busca; **Enter** adiciona o produto encontrado. O leitor USB deve estar configurado para digitar como teclado e enviar Enter depois do código.

Escolha o cliente quando necessário, ajuste quantidades, desconto em reais e taxa de entrega. **F4** abre o pagamento. Selecione dinheiro, Pix, débito, crédito ou fiado. Para dinheiro, informe o recebido e confira o troco. Para fiado, é obrigatório um cliente com limite disponível. O registro de Pix ou cartão é manual: confira o recebimento na sua conta ou maquininha.

Ao confirmar, venda, estoque e caixa são gravados juntos. O campo de observações e os dados de entrega podem aparecer no cupom. Após uma falha de impressão, a venda permanece registrada: reimprima em **Vendas**.

## Pedidos e entregas

Monte os produtos no PDV e clique **Salvar como pedido**. Escolha cliente, entrega ou retirada, endereço e observações. O pedido reserva o estoque e pode ser acompanhado em **Pedidos e entregas**.

Use **Alterar situação** para marcar pendente, preparando, em entrega ou cancelado. Para receber e concluir, selecione o pedido e clique **Receber no PDV**. Os produtos e preços ficam preservados; finalize o pagamento normalmente. Pedido cancelado libera sua reserva. O comprovante de pedido não comprova pagamento.

## Fiados

Em **Clientes e fiados**, consulte o saldo devedor e o limite. Selecione o cliente e clique **Extrato de fiados** para ver vendas e recebimentos. **Receber fiado** permite recebimento parcial ou integral no caixa aberto. O valor é aplicado às dívidas mais antigas e não pode superar o saldo.

## Caixa, histórico e lucro

**Suprimento** adiciona dinheiro físico, e **sangria** registra retirada. Ambas exigem motivo. **Fechar caixa** mostra o resumo por pagamento; informe o dinheiro contado. O fechamento registra a diferença entre o esperado e o contado. Selecione um caixa no histórico para imprimir o resumo.

Em **Vendas**, consulte detalhes, reimprima o cupom ou exporte CSV. O administrador pode cancelar uma venda no caixa de origem ainda aberto, desde que não tenha recebimento de fiado. O estoque volta e os totais de caixa são ajustados. Não se cancela a mesma venda duas vezes.

Em **Relatórios**, escolha o período e consulte receita dos produtos, custos históricos, lucro e margem bruta. O desconto é distribuído entre os produtos; canceladas ficam fora do resultado. A entrega e as despesas operacionais não entram no lucro bruto dos produtos.

## Impressoras e balanças

Em **Equipamentos → Adicionar impressora**, escolha uma impressora já instalada no Windows, dê um nome e escolha o papel: 58 mm, 80 mm ou A4. Marque como padrão. Ajuste também o tamanho do papel no driver da impressora. Em **Configurações**, ative a impressão automática se quiser. É possível escolher uma impressora PDF do Windows para salvar um cupom.

Em **Adicionar balança**, informe nome, porta COM, velocidade e formato de leitura. A conexão preparada é serial **8 bits, sem paridade, 1 stop bit (8N1)**. Formatos disponíveis: texto decimal em kg, como `1.250 kg`, e inteiro em gramas, como `001250`. Um comando de solicitação em hexadecimal pode ser informado se o manual exigir. Use **Testar leitura** para verificar.

Esse recurso precisa ser compatível com o protocolo real da balança. USB só funciona neste módulo se o equipamento disponibilizar uma porta COM serial. Outros protocolos exigem adaptação. Sem balança, digite o peso em kg manualmente na venda.

## Configuração do cupom fiscal

O administrador encontra a tela **Cupom fiscal** no menu. Ela prepara os parâmetros de **NFC-e, modelo 65**. A emissão ainda depende de integração com um emissor SEFAZ; preencher esta tela não transforma o comprovante atual em documento fiscal.

1. Em **Dados da empresa**, informe razão social, nome fantasia, CNPJ, inscrição estadual, regime tributário (CRT), UF, município, código IBGE e endereço. O cadastro aceita CNPJ numérico e alfanumérico e confere os dígitos verificadores. É possível salvar um cadastro parcial.
2. Em **Configurar NFC-e**, escolha homologação ou produção, série, próximo número, identificação do caixa e uma impressora cadastrada em **Equipamentos**. Cada ambiente mantém sua própria série e numeração. Vendas não fiscais não consomem esses números.
3. Em **Importar A1**, escolha o certificado `.pfx` ou `.p12` e informe a senha. O sistema verifica a abertura, a presença da chave privada e a validade. Não certifica a cadeia ICP-Brasil nem a titularidade do emitente. Certificados A3, tokens e cartões não são atendidos neste módulo.
4. Em **Cadastrar / trocar CSC**, selecione o ambiente e informe identificador e token fornecidos pela SEFAZ. Os códigos de homologação e produção ficam separados; o token salvo não é exibido.
5. No cadastro dos produtos, preencha NCM, CEST quando aplicável, CFOP de saída, CSOSN ou CST de ICMS e origem da mercadoria, conforme orientação contábil. Esses campos verificam o formato e não calculam os tributos nem substituem uma configuração tributária completa.
6. Use **Conferir configuração** para ver as pendências. A conferência é local, sem consultar credenciamento ou autorização na SEFAZ. Mesmo com os campos preenchidos, a integração do emissor permanece pendente nesta versão.

Certificado A1, sua senha e o token CSC são protegidos pelo Windows e acompanham o backup. Ao restaurar em outro computador ou usuário do Windows, reimporte o A1 e cadastre os CSCs novamente. Mantenha também o arquivo original do certificado em local seguro.

A referência enviada descreve SAT/MFE (CF-e modelo 59). Esta versão concentra os parâmetros em NFC-e. Para São Paulo, a emissão de CF-e-SAT foi vedada a partir de 1º de janeiro de 2026, conforme a [SEFAZ-SP](https://legislacao.fazenda.sp.gov.br/Paginas/RC32314_2025.aspx). A configuração e o credenciamento devem seguir a SEFAZ da UF do estabelecimento.

## Backup

Abra **Configurações → Criar backup** e salve o arquivo em outra unidade ou na nuvem. O backup inclui produtos, fotos, usuários, clientes, pedidos, caixa, vendas e configuração fiscal, com certificado e CSC protegidos pelo Windows. Também existe uma cópia diária automática na pasta `Backups` e uma cópia `.bak` da gravação anterior.

Para recuperar, feche o caixa, limpe a venda atual e escolha **Restaurar backup**. Confirme o arquivo. O sistema valida a versão, preserva uma cópia dos dados atuais e pede novo login. Os usuários e senhas voltam ao estado do backup restaurado. Os backups têm dados pessoais e devem ser guardados em local de acesso restrito.

## Desinstalar

Use **Desinstalar Giro Bebidas** no menu Iniciar ou no painel de aplicativos do Windows. Feche o PDV antes. Os dados são preservados por padrão. A opção **Excluir também todos os dados, fotos e backups locais** apaga a pasta local do depósito, mediante confirmação. Backups exportados para outras pastas são preservados.

O aplicativo gera cupom não fiscal. Esta versão não emite NF-e/NFC-e e não é um sistema compartilhado entre vários computadores.
