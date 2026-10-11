using System.Globalization;
using System.Text;
using System.IO.Compression;
using GiroBebidas.Core;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
string root = args.FirstOrDefault() ?? Path.Combine(Environment.CurrentDirectory, "artifacts", "test-data", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
int checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception("FALHOU: " + name); checks++; Console.WriteLine("OK " + name); }
void Equal<T>(T expected, T actual, string name) => Check(EqualityComparer<T>.Default.Equals(expected, actual), name);
void Reject(Action action, string name)
{
    bool rejected = false;
    try { action(); } catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or InvalidDataException) { rejected = true; }
    Check(rejected, name);
}
try
{
    var store = new JsonStore(Path.Combine(root, "loja")); var service = new StoreService(store);
    Reject(() => service.Login("ADMIN", "incorreta"), "Login recusa senha incorreta");
    var admin = service.Login("admin", "ADMIN"); Equal(UserRole.Administrador, admin.Role, "ADMIN acessa sem distinguir maiúsculas");
    Check(admin.PasswordHash != "ADMIN" && admin.PasswordSalt.Length >= 40, "Senha inicial armazenada como hash com salt");
    var p = service.Snapshot.Products[0]; decimal startStock = p.Stock;
    Reject(() => service.CompleteSale(new([new(p.Id, 1)], 0, 0, null, PaymentMethod.Dinheiro, 10)), "Venda exige caixa aberto");
    service.OpenCash(100); Reject(() => service.OpenCash(0), "Segundo caixa aberto é impedido");
    Sale sale = service.CompleteSale(new([new(p.Id, 12)], 1.88m, 0, null, PaymentMethod.Dinheiro, 50));
    Equal(46m, sale.Total, "Atacado e desconto calculados a centavos"); Equal(4m, sale.Change, "Troco calculado"); Equal(11.20m, sale.Profit, "Lucro considera desconto e custo histórico");
    Equal(startStock - 12, service.Snapshot.Products.Single(x => x.Id == p.Id).Stock, "Baixa de estoque automática");
    var cash = service.Snapshot.CashSessions.Single(); Equal(146m, StoreService.SummarizeCash(service.Snapshot, cash.Id).Expected, "Troco não infla o caixa");
    string before = File.ReadAllText(store.FilePath);
    Reject(() => service.CompleteSale(new([new(p.Id, 999999)], 0, 0, null, PaymentMethod.Pix, 10)), "Venda acima do estoque recusada");
    Equal(before, File.ReadAllText(store.FilePath), "Rejeição preserva todos os dados em disco");
    Reject(() => service.CompleteSale(new([new(p.Id, -1), new(p.Id, 2)], 0, 0, null, PaymentMethod.Pix, 10)), "Quantidade negativa em item duplicado recusada");
    Reject(() => service.CompleteSale(new([new(p.Id, 1)], 100, 0, null, PaymentMethod.Pix, 10)), "Desconto maior que subtotal recusado");
    Reject(() => service.CompleteSale(new([new(p.Id, 1)], 0, 0, null, PaymentMethod.Dinheiro, 1)), "Dinheiro recebido insuficiente recusado");
    Reject(() => service.CompleteSale(new([new(p.Id, 1)], 0, 0, null, PaymentMethod.Fiado, 10)), "Fiado exige cliente");
    var customer = new Customer { Name = "Cliente de teste", CreditLimit = 100, Address = "Rua Teste, 10" }; service.SaveCustomer(customer);
    var debt = service.CompleteSale(new([new(p.Id, 2)], 0, 0, customer.Id, PaymentMethod.Fiado, 9, Address: customer.Address));
    Equal(9m, StoreService.Balance(service.Snapshot, customer.Id), "Fiado cria saldo devedor");
    Check(ReceiptText.Sale(service.Snapshot.Settings, debt).Contains("Rua Teste, 10"), "Cupom inclui endereço de entrega");
    Equal(146m, StoreService.SummarizeCash(service.Snapshot, cash.Id).Expected, "Fiado não entra como dinheiro recebido");
    service.ReceiveDebt(customer.Id, 4, PaymentMethod.Dinheiro); Equal(5m, StoreService.Balance(service.Snapshot, customer.Id), "Recebimento parcial de fiado");
    Equal(150m, StoreService.SummarizeCash(service.Snapshot, cash.Id).Expected, "Recebimento de fiado entra no caixa");
    Reject(() => service.ReceiveDebt(customer.Id, 6, PaymentMethod.Pix), "Recebimento acima da dívida recusado");
    Reject(() => service.CancelSale(debt.Id, "teste"), "Venda com fiado recebido não é cancelada");
    Reject(() => service.CompleteSale(new([new(p.Id, 30)], 0, 0, customer.Id, PaymentMethod.Fiado, 119.70m)), "Limite de crédito é respeitado");
    var order = service.CreateOrder([new(p.Id, 3)], customer.Id, true, customer.Address, "pedido", 0, 5);
    Equal(3m, StoreService.Reserved(service.Snapshot, p.Id), "Pedido reserva estoque");
    var currentProduct = service.Snapshot.Products.Single(x => x.Id == p.Id);
    Equal(currentProduct.Stock - 3, service.Available(p.Id), "Reserva descontada do disponível");
    Reject(() => service.AdjustStock(p.Id, -currentProduct.Stock, "teste"), "Ajuste não consome estoque reservado");
    var fulfilled = service.CompleteSale(new([], 0, 0, null, PaymentMethod.Pix, 18.50m, OrderId: order.Id));
    Equal(18.50m, fulfilled.Total, "Pedido preserva preço, cliente e taxa"); Equal(0m, StoreService.Reserved(service.Snapshot, p.Id), "Pedido finalizado libera reserva e baixa estoque uma vez");
    Reject(() => service.CompleteSale(new([], 0, 0, null, PaymentMethod.Pix, 20, OrderId: order.Id)), "Pedido não é cobrado duas vezes");
    service.CancelSale(sale.Id, "Teste de cancelamento"); Equal(104m, StoreService.SummarizeCash(service.Snapshot, cash.Id).Expected, "Cancelamento estorna caixa");
    Check(service.Snapshot.StockMovements.Any(m => m.Quantity == 12 && m.Reason.Contains("Cancelamento")), "Cancelamento devolve produtos ao estoque");
    Reject(() => service.CancelSale(sale.Id, "de novo"), "Cancelamento duplicado recusado");
    service.MoveCash(CashMovementKind.Suprimento, 20, "troco"); service.MoveCash(CashMovementKind.Sangria, 10, "cofre");
    Equal(114m, StoreService.SummarizeCash(service.Snapshot, cash.Id).Expected, "Suprimento e sangria reconciliam caixa");
    Reject(() => service.MoveCash(CashMovementKind.Sangria, 1000, "teste"), "Sangria maior que saldo recusada");
    var weighted = service.Snapshot.Products.First(x => x.IsWeighed);
    var weightSale = service.CompleteSale(new([new(weighted.Id, 1.125m)], 0, 0, null, PaymentMethod.Pix, 3.38m));
    Equal(3.38m, weightSale.Total, "Produtos por peso arredondam o total corretamente");
    Reject(() => service.CompleteSale(new([new(p.Id, .5m)], 0, 0, null, PaymentMethod.Pix, 10)), "Produto unitário não aceita fração");
    var changedProduct = service.Snapshot.Products.Single(x => x.Id == weighted.Id); changedProduct.Cost = 99; service.SaveProduct(changedProduct);
    Equal(1.35m, Money.Round(weightSale.Lines[0].Quantity * weightSale.Lines[0].UnitCost), "Alterar cadastro não modifica custo de vendas anteriores");
    var report = StoreService.ProfitReport(service.Snapshot); Equal(service.Snapshot.Sales.Where(v => !v.Canceled).Sum(v => v.Profit), report.Sum(v => v.Profit), "Relatório ignora canceladas e reconcilia lucro");
    var operatorUser = new UserAccount { Name = "Operador", Username = "CAIXA", Role = UserRole.Operador }; service.SaveUser(operatorUser, "1234");
    Reject(() => service.SaveUser(new() { Name = "Duplicado", Username = "caixa" }, "1234"), "Usuários duplicados recusados");
    Reject(() => service.SaveUser(new() { Id = admin.Id, Name = admin.Name, Username = "ADMIN", Role = UserRole.Operador }, null), "Último administrador não pode perder perfil");
    service.Login("CAIXA", "1234");
    Reject(() => service.SaveProduct(new() { Name = "A", Code = "A", Price = 1 }), "Operador não cadastra produtos");
    Reject(() => service.SaveUser(new() { Name = "A", Username = "A" }, "1234"), "Operador não cria usuários");
    Reject(() => service.SaveSettings(new()), "Operador não altera configurações");
    Reject(() => service.ExportBackup(Path.Combine(root, "proibido.json")), "Operador não exporta backup");
    service.ChangePassword("1234", "5678"); service.Logout(); Reject(() => service.Login("CAIXA", "1234"), "Senha antiga deixa de funcionar"); service.Login("CAIXA", "5678"); checks++;
    service.Login("ADMIN", "ADMIN"); var inactive = service.Snapshot.Users.Single(u => u.Username == "CAIXA"); inactive.Active = false; service.SaveUser(inactive, null);
    Reject(() => service.Login("CAIXA", "5678"), "Conta inativa não entra");
    var closed = service.CloseCash(114, "Conferido"); Equal(0m, closed.Difference, "Fechamento confere dinheiro contado");
    Reject(() => service.CompleteSale(new([new(p.Id, 1)], 0, 0, null, PaymentMethod.Pix, 10)), "Caixa fechado bloqueia venda");
    string backup = Path.Combine(root, "backup.json"); service.ExportBackup(backup);
    Check(Directory.GetFiles(Path.Combine(store.DirectoryPath, "Backups")).Length > 0, "Backup diário automático criado");
    int products = service.Snapshot.Products.Count; service.SaveProduct(new() { Name = "Novo", Code = "NOVO", Price = 10, Stock = 1 });
    service.RestoreBackup(backup); Equal(products, service.Snapshot.Products.Count, "Restauração recupera estado anterior"); Check(service.CurrentUserId is null, "Restauração encerra sessão");
    Check(Directory.GetFiles(store.DirectoryPath, "antes-restauracao-*.json").Length > 0, "Restauração preserva dados anteriores");
    var reloaded = new JsonStore(store.DirectoryPath); Equal(service.Snapshot.Sales.Count, reloaded.Snapshot().Sales.Count, "Dados sobrevivem à reinicialização");
    File.WriteAllText(store.FilePath, "corrompido"); var recovered = new JsonStore(store.DirectoryPath); Check(recovered.RecoveredFromBackup, "Recuperação de arquivo corrompido pela cópia automática");
    Check(Directory.GetFiles(store.DirectoryPath, "dados-corrompidos-*.json").Length > 0, "Arquivo corrompido é preservado");
    Equal(1.25m, ScaleProtocol.Parse("ST, +001.250 kg\r\n", "Texto em kg"), "Protocolo de peso em kg");
    Equal(1.25m, ScaleProtocol.Parse("\u0002" + "001250" + "\u0003", "Inteiro em gramas"), "Protocolo de gramas converte para kg");
    Reject(() => ScaleProtocol.Parse("US, 1.250 kg", "Texto em kg"), "Leitura instável recusada");
    Reject(() => ScaleProtocol.Parse("0.000 kg", "Texto em kg"), "Peso zero recusado");
    byte[] Package(params (string Name, string Contents)[] files)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true)) foreach (var file in files) { using var writer = new StreamWriter(zip.CreateEntry(file.Name).Open()); writer.Write(file.Contents); }
        return memory.ToArray();
    }
    string installation = Path.Combine(root, "programa");
    byte[] valid = Package(("GiroBebidas.exe", "app versão 1"), ("DesinstalarGiroBebidas.exe", "uninstaller"), ("libs/modulo.dll", "modulo"));
    using (var package = new MemoryStream(valid)) Installation.Install(package, installation);
    Check(File.Exists(Path.Combine(installation, "GiroBebidas.exe")), "Instalador extrai o aplicativo");
    Equal(3, Installation.ReadManifest(installation)!.Files.Count, "Instalador registra todos os arquivos no manifesto");
    File.WriteAllText(Path.Combine(installation, "anotacoes-do-usuario.txt"), "preservar");
    byte[] updated = Package(("GiroBebidas.exe", "app versão 2"), ("DesinstalarGiroBebidas.exe", "uninstaller"), ("libs/modulo.dll", "novo"));
    using (var package = new MemoryStream(updated)) Installation.Install(package, installation);
    Equal("app versão 2", File.ReadAllText(Path.Combine(installation, "GiroBebidas.exe")), "Reinstalação atualiza os arquivos registrados");
    Equal("preservar", File.ReadAllText(Path.Combine(installation, "anotacoes-do-usuario.txt")), "Reinstalação preserva arquivos adicionais");
    byte[] traversal = Package(("GiroBebidas.exe", "invalido"), ("DesinstalarGiroBebidas.exe", "uninstaller"), ("../fora-do-programa.txt", "não criar"));
    Reject(() => { using var package = new MemoryStream(traversal); Installation.Install(package, installation); }, "Instalador recusa escape de diretório no pacote");
    Check(!File.Exists(Path.Combine(root, "fora-do-programa.txt")), "Pacote inválido não escreve fora da instalação");
    Equal("app versão 2", File.ReadAllText(Path.Combine(installation, "GiroBebidas.exe")), "Falha de pacote preserva instalação anterior");
    Reject(() => { using var package = new MemoryStream(Package(("GiroBebidas.exe", "incompleto"))); Installation.Install(package, installation); }, "Instalador recusa pacote incompleto");
    Reject(() => Installation.ResolveInside(installation, "../arquivo.txt"), "Validação de caminho impede remoção fora do programa");
    string userData = Path.Combine(root, "dados-pessoais"); Directory.CreateDirectory(userData); File.WriteAllText(Path.Combine(userData, "giro-dados.json"), "dados preservados");
    var deferred = Installation.Uninstall(installation);
    Equal(0, deferred.Count, "Desinstalação externa não precisa postergar arquivos");
    Check(!File.Exists(Path.Combine(installation, "GiroBebidas.exe")), "Desinstalador remove o executável");
    Equal("preservar", File.ReadAllText(Path.Combine(installation, "anotacoes-do-usuario.txt")), "Desinstalador preserva arquivos que não pertencem ao pacote");
    Check(File.Exists(Path.Combine(userData, "giro-dados.json")), "Desinstalador preserva dados do depósito");
    Reject(() => Installation.Uninstall(userData), "Desinstalador recusa pasta sem manifesto");
    var separate = new StoreService(new JsonStore(Path.Combine(root, "isolamento"))); separate.Login("ADMIN", "ADMIN");
    var independent = new Product { Name = "Produto isolado", Code = "ISO", Price = 1, Stock = 1 }; separate.SaveProduct(independent); independent.Stock = -99;
    Equal(1m, separate.Snapshot.Products.Single(p => p.Id == independent.Id).Stock, "Objetos externos não alteram o estoque fora de transações");
    separate.OpenCash(0); var isolatedSale = separate.CompleteSale(new([new(independent.Id, 1)], 0, 0, null, PaymentMethod.Dinheiro, 1));
    isolatedSale.Canceled = true; Check(!separate.Snapshot.Sales.Single().Canceled, "Resultado retornado não permite adulterar venda gravada");
    var historical = separate.Snapshot.Products.Single(p => p.Id == independent.Id); historical.Unit = "kg";
    Reject(() => separate.SaveProduct(historical), "Unidade com histórico de venda é preservada");
    separate.MoveCash(CashMovementKind.Sangria, 1, "retirada");
    Reject(() => separate.CancelSale(isolatedSale.Id, "teste"), "Cancelamento não deixa caixa com dinheiro negativo");
    separate.MoveCash(CashMovementKind.Suprimento, 1, "estorno"); separate.CancelSale(isolatedSale.Id, "teste");
    Equal(0m, StoreService.SummarizeCash(separate.Snapshot, separate.Snapshot.CashSessions.Single().Id).Expected, "Estorno após suprimento reconcilia o caixa");
    string invalidBackup = Path.Combine(root, "invalido.json"); File.WriteAllText(invalidBackup, "{\"SchemaVersion\":1,\"Users\":null}");
    separate.CloseCash(0, "teste");
    Reject(() => separate.RestoreBackup(invalidBackup), "Backup com estrutura incompleta é recusado");
    Console.WriteLine($"\n{checks} verificações aprovadas. Dados de teste: {root}"); return 0;
}
catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
