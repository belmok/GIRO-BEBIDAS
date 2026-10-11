using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public partial class MainWindow : Window
{
    public MainViewModel Vm { get; }
    private readonly DispatcherTimer messageTimer = new() { Interval = TimeSpan.FromSeconds(9) };
    public bool ReturnToLogin { get; private set; }
    public MainWindow(StoreService service)
    {
        InitializeComponent(); Vm = new(service); DataContext = Vm;
        Vm.Notify = Notify; Vm.RequestWeight = AddWeighed;
        messageTimer.Tick += (_, _) => { Vm.Message = ""; messageTimer.Stop(); };
        Loaded += (_, _) => ProductSearch.Focus();
        Closing += Window_Closing;
    }
    private void Notify(string message) { Vm.Message = message; messageTimer.Stop(); messageTimer.Start(); }
    private void Guard(Action action, string? success = null)
    {
        try { action(); Vm.Refresh(); if (success is not null) Notify(success); }
        catch (Exception ex) { Notify(ex.Message); }
    }
    private T Require<T>(T? value, string item) where T : class => value ?? throw new InvalidOperationException($"Selecione {item} na lista.");
    private bool Confirm(string message) => MessageBox.Show(this, message, "Giro Bebidas", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (Vm.Cart.Count > 0 && !Confirm("Há uma venda no carrinho. Sair e descartar essa seleção? Pedidos salvos serão preservados.")) e.Cancel = true;
        if (!e.Cancel) messageTimer.Stop();
    }
    private void Logout_Click(object sender, RoutedEventArgs e) { ReturnToLogin = true; Close(); }
    private void DismissMessage_Click(object sender, RoutedEventArgs e) => Vm.Message = "";
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2) { Vm.Page = "PDV"; ProductSearch.Focus(); ProductSearch.SelectAll(); e.Handled = true; }
        else if (e.Key == Key.F4) { Checkout_Click(sender, e); e.Handled = true; }
        else if (e.Key == Key.F5) { Guard(Vm.Refresh, "Informações atualizadas."); e.Handled = true; }
    }
    private void ProductSearch_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        Guard(() =>
        {
            var product = Vm.Snapshot.Products.FirstOrDefault(p => p.Active && (p.Barcode == Vm.Search.Trim() || p.Code.Equals(Vm.Search.Trim(), StringComparison.OrdinalIgnoreCase)));
            if (product is null && Vm.Tiles.Count == 1) product = Vm.Tiles[0].Product;
            if (product is null) throw new InvalidOperationException("Produto não encontrado. Confira o código ou selecione um produto na lista.");
            if (!Vm.CanEditCart) throw new InvalidOperationException("Finalize ou limpe o pedido antes de adicionar produtos.");
            if (product.IsWeighed) AddWeighed(product); else Vm.AddProduct(product, 1);
            Vm.Search = ""; ProductSearch.Focus();
        });
    }
    private void ClearCart_Click(object sender, RoutedEventArgs e) { if (Vm.Cart.Count == 0 || Confirm("Limpar os produtos da venda atual? O pedido salvo, se houver, continuará disponível.")) Vm.ClearCart(); }
    private void NewProduct_Click(object sender, RoutedEventArgs e) => Guard(() => ProductForm(new(), false));
    private void EditProduct_Click(object sender, RoutedEventArgs e) => Guard(() => ProductForm(Require(Vm.SelectedStock, "um produto").Product, true));
    private void ProductForm(Product p, bool editing)
    {
        if (CreateProductDialog(p, editing).ShowDialog() == true) Notify("Produto salvo com sucesso.");
    }
    internal FormDialog CreateProductDialog(Product p, bool editing)
    {
        var d = new FormDialog(this, editing ? "Editar produto" : "Cadastrar produto", "Personalize a foto, a descrição e os valores do produto.");
        string photo = p.PhotoBase64;
        var image = d.Photo(PhotoTools.Decode(photo) ?? BeverageArt.Create(p));
        d.Action("Adicionar / trocar foto", () =>
        {
            var pick = new OpenFileDialog { Title = "Foto do produto", Filter = "Imagens|*.png;*.jpg;*.jpeg;*.bmp;*.gif" };
            if (pick.ShowDialog(d) == true) { photo = PhotoTools.Import(pick.FileName); image.Source = PhotoTools.Decode(photo); }
        });
        d.Action("Remover foto", () => { photo = ""; image.Source = BeverageArt.Create(p); });
        d.Text("name", "Nome do produto", p.Name); d.Text("description", "Descrição", p.Description, true);
        d.Text("code", "Código / SKU", editing ? p.Code : (Vm.Snapshot.Products.Count + 1).ToString("000")); d.Text("barcode", "Código de barras", p.Barcode);
        d.Choice("category", "Categoria", new[] { "Cervejas", "Refrigerantes", "Destilados", "Água e gelo", "Outros" }, p.Category, editable: true);
        d.Choice("unit", "Unidade de venda", new[] { "un", "cx", "kg" }, p.Unit);
        d.Choice("package", "Ilustração quando não houver foto", new[] { "Lata", "Garrafa", "Pacote", "Caixa" }, p.Package);
        d.Number("cost", "Custo unitário (R$)", p.Cost); d.Number("price", "Preço de venda (R$)", p.Price);
        d.Number("wholesale", "Preço de atacado (R$) • zero para desativar", p.WholesalePrice); d.Number("threshold", "Quantidade mínima para atacado", p.WholesaleMinimum);
        d.Number("stock", editing ? "Estoque físico • use Entrada / ajuste para movimentar" : "Estoque inicial", p.Stock, !editing);
        d.Number("min", "Estoque mínimo", p.MinimumStock); d.Check("active", "Produto ativo para venda", p.Active);
        d.Submit = f => Vm.Service.SaveProduct(new() { Id = p.Id, Name = f.Value("name"), Description = f.Value("description"), PhotoBase64 = photo,
            Code = f.Value("code"), Barcode = f.Value("barcode"), Category = f.Value("category"), Unit = f.Value("unit"), Package = f.Value("package"), Color = p.Color,
            Cost = f.Decimal("cost"), Price = f.Decimal("price"), WholesalePrice = f.Decimal("wholesale"), WholesaleMinimum = f.Decimal("threshold"),
            Stock = f.Decimal("stock"), MinimumStock = f.Decimal("min"), Active = f.Checked("active") });
        return d;
    }
    private void AdjustStock_Click(object sender, RoutedEventArgs e) => Guard(() =>
    {
        var p = Require(Vm.SelectedStock, "um produto").Product;
        var d = new FormDialog(this, "Movimentar estoque", $"{p.Name} • Físico: {p.Stock:0.###} {p.Unit}");
        d.Choice("type", "Movimentação", new[] { "Entrada de mercadoria", "Saída / perda" }); d.Number("qty", "Quantidade", 1);
        d.Number("cost", "Custo unitário atualizado (R$)", p.Cost); d.Text("reason", "Motivo / fornecedor / documento");
        d.Submit = f => { decimal quantity = f.Decimal("qty"); if (quantity <= 0) throw new InvalidOperationException("Informe uma quantidade maior que zero."); Vm.Service.AdjustStock(p.Id, quantity * (f.Value("type") == "Saída / perda" ? -1 : 1), f.Value("reason"), f.Decimal("cost")); };
        if (d.ShowDialog() == true) Notify("Estoque atualizado.");
    });
    private void NewCustomer_Click(object sender, RoutedEventArgs e) => Guard(() => CustomerForm(new()));
    private void EditCustomer_Click(object sender, RoutedEventArgs e) => Guard(() => CustomerForm(Require(Vm.SelectedCustomer, "um cliente").Customer));
    private void CustomerForm(Customer c)
    {
        var d = new FormDialog(this, "Cadastro de cliente", "Os dados de entrega poderão ser usados nos pedidos e cupons.");
        d.Text("name", "Nome", c.Name); d.Text("phone", "Telefone", c.Phone); d.Text("doc", "CPF / CNPJ", c.Document); d.Text("address", "Endereço de entrega", c.Address);
        d.Number("limit", "Limite de fiado (R$)", c.CreditLimit); d.Text("notes", "Observações", c.Notes, true);
        d.Submit = f => Vm.Service.SaveCustomer(new() { Id = c.Id, Name = f.Value("name"), Phone = f.Value("phone"), Document = f.Value("doc"), Address = f.Value("address"), CreditLimit = f.Decimal("limit"), Notes = f.Value("notes") });
        if (d.ShowDialog() == true) Notify("Cliente salvo.");
    }
    private void Checkout_Click(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (!Vm.CanSell) throw new InvalidOperationException("Adicione produtos à venda.");
        if (!Vm.CashIsOpen) { OpenCash_Click(sender, e); if (!Vm.Service.Snapshot.CashSessions.Any(c => c.ClosedAt is null)) return; }
        if (Vm.Discount < 0 || Vm.Discount > Vm.Subtotal || Vm.DeliveryFee < 0) throw new InvalidOperationException("Confira o desconto e a taxa de entrega.");
        var d = new FormDialog(this, "Finalizar venda", $"Total a pagar: {Vm.Total:C2}", "Confirmar venda");
        d.Choice("method", "Forma de pagamento", Enum.GetValues<PaymentMethod>(), PaymentMethod.Dinheiro);
        var tendered = d.Number("received", "Recebido em dinheiro (R$)", Vm.Total);
        var change = d.Info("Troco: R$ 0,00");
        tendered.TextChanged += (_, _) => { try { change.Text = $"Troco: {Math.Max(0, d.Decimal("received") - Vm.Total):C2}"; } catch { change.Text = "Confira o valor recebido."; } };
        var customer = Vm.Snapshot.Customers.FirstOrDefault(c => c.Id == Vm.CartCustomer?.Id);
        d.Info($"Cliente: {customer?.Name ?? "Consumidor final"}");
        d.Check("delivery", "Entrega no endereço abaixo", Vm.ActiveOrderAddress.Length > 0);
        d.Text("address", "Endereço de entrega", Vm.ActiveOrderId is null ? customer?.Address ?? "" : Vm.ActiveOrderAddress, enabled: Vm.CanEditCart);
        d.Text("notes", "Observações", Vm.ActiveOrderNotes, true, Vm.CanEditCart);
        Sale? sale = null;
        d.Submit = f =>
        {
            if (Vm.CanEditCart && f.Checked("delivery") && f.Value("address").Length == 0) throw new InvalidOperationException("Informe o endereço de entrega.");
            var method = f.Selected<PaymentMethod>("method");
            sale = Vm.Service.CompleteSale(new(Vm.Draft(), Vm.Discount, Vm.DeliveryFee, Vm.CartCustomer?.Id, method,
                method == PaymentMethod.Dinheiro ? f.Decimal("received") : Vm.Total, f.Value("notes"), f.Checked("delivery") ? f.Value("address") : "", Vm.ActiveOrderId));
        };
        if (d.ShowDialog() == true && sale is not null)
        {
            Vm.ClearCart(); Vm.Refresh();
            Notify($"Venda {sale.NumberLabel} registrada. Total: {sale.Total:C2}" + (sale.Change > 0 ? $" • Troco: {sale.Change:C2}" : ""));
            if (Vm.Snapshot.Settings.AutoPrint)
                try { ReceiptPrinter.PrintSale(this, Vm.Snapshot, sale, false); }
                catch (Exception ex) { Notify($"Venda registrada. Não foi possível imprimir: {ex.Message} Reimprima em Vendas."); }
        }
    });
    private void SaveOrder_Click(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (!Vm.CanSell) throw new InvalidOperationException("Adicione os produtos do pedido no PDV.");
        if (!Vm.CanEditCart) throw new InvalidOperationException("Este pedido já está salvo.");
        if (Vm.CustomerOptions.Count <= 1) throw new InvalidOperationException("Cadastre um cliente antes de salvar um pedido.");
        var choices = Vm.CustomerOptions.Where(c => c.Id is not null).ToList();
        var d = new FormDialog(this, "Salvar pedido", "O estoque será reservado. O pagamento e a baixa serão registrados ao concluir no PDV.", "Salvar pedido");
        var pick = d.Choice("customer", "Cliente", choices, choices.FirstOrDefault(c => c.Id == Vm.CartCustomer?.Id) ?? choices[0], nameof(CustomerChoice.Name));
        d.Check("delivery", "Pedido para entrega", true);
        var address = d.Text("address", "Endereço de entrega", Vm.Snapshot.Customers.First(c => c.Id == ((CustomerChoice)pick.SelectedItem).Id).Address);
        pick.SelectionChanged += (_, _) => address.Text = Vm.Snapshot.Customers.First(c => c.Id == ((CustomerChoice)pick.SelectedItem).Id).Address;
        d.Text("notes", "Observações", "", true); d.Info($"Total do pedido: {Vm.Total:C2}");
        d.Submit = f => Vm.Service.CreateOrder(Vm.Draft(), f.Selected<CustomerChoice>("customer").Id!.Value, f.Checked("delivery"), f.Value("address"), f.Value("notes"), Vm.Discount, Vm.DeliveryFee);
        if (d.ShowDialog() == true) { Vm.ClearCart(); Notify("Pedido salvo. Os produtos estão reservados."); }
    });
    private void LoadOrder_Click(object sender, RoutedEventArgs e) => Guard(() =>
    {
        var order = Require(Vm.SelectedOrder, "um pedido");
        if (!order.IsOpen) throw new InvalidOperationException("Este pedido já está encerrado.");
        if (Vm.Cart.Count > 0 && !Confirm("Substituir a venda atual pelo pedido selecionado?")) return;
        Vm.LoadOrder(order);
    });
    private void OrderStatus_Click(object sender, RoutedEventArgs e) => Guard(() =>
    {
        var order = Require(Vm.SelectedOrder, "um pedido");
        var d = new FormDialog(this, $"Situação • {order.NumberLabel}", "Para concluir, use Receber no PDV.");
        d.Choice("status", "Situação", new[] { OrderStatus.Pendente, OrderStatus.Preparando, OrderStatus.EmEntrega, OrderStatus.Cancelado }, order.Status);
        d.Submit = f => Vm.Service.SetOrderStatus(order.Id, f.Selected<OrderStatus>("status")); d.ShowDialog();
    });
    private void OpenCash_Click(object sender, RoutedEventArgs e) => Guard(() =>
    {
        var d = new FormDialog(this, "Abrir caixa", "Informe o dinheiro disponível para troco.", "Abrir caixa"); d.Number("opening", "Fundo de troco (R$)");
        d.Submit = f => Vm.Service.OpenCash(f.Decimal("opening")); if (d.ShowDialog() == true) Notify("Caixa aberto. Bom trabalho!");
    });
    private void MoveCash_Click(object sender, RoutedEventArgs e) => Guard(() =>
    {
        var d = new FormDialog(this, "Movimentar caixa", "Suprimento adiciona dinheiro. Sangria registra a retirada.");
        d.Choice("kind", "Tipo", Enum.GetValues<CashMovementKind>()); d.Number("amount", "Valor (R$)"); d.Text("notes", "Motivo");
        d.Submit = f => Vm.Service.MoveCash(f.Selected<CashMovementKind>("kind"), f.Decimal("amount"), f.Value("notes")); d.ShowDialog();
    });
    private void CloseCash_Click(object sender, RoutedEventArgs e) => Guard(() =>
    {
        var state = Vm.Service.Snapshot; var cash = state.CashSessions.FirstOrDefault(c => c.ClosedAt is null) ?? throw new InvalidOperationException("Não há caixa aberto.");
        var totals = StoreService.SummarizeCash(state, cash.Id);
        var d = new FormDialog(this, "Fechar caixa", "Conte o dinheiro físico. Confira as outras formas de pagamento.", "Confirmar fechamento");
        d.Info($"Dinheiro esperado: {totals.Expected:C2}\nPix: {totals.Pix:C2}\nDébito: {totals.Debit:C2}\nCrédito: {totals.Credit:C2}\nVendas em fiado: {totals.CreditSales:C2}");
        d.Number("counted", "Dinheiro contado (R$)"); d.Text("notes", "Observações do fechamento", "", true);
        d.Submit = f => Vm.Service.CloseCash(f.Decimal("counted"), f.Value("notes")); if (d.ShowDialog() == true) Notify("Caixa fechado. O resumo está disponível no histórico.");
    });
    private void ReceiveDebt_Click(object sender, RoutedEventArgs e) => Guard(() =>
    {
        var c = Require(Vm.SelectedCustomer, "um cliente");
        var d = new FormDialog(this, "Receber fiado", $"{c.Name} • Saldo devedor: {c.Balance:C2}", "Registrar recebimento");
        d.Number("amount", "Valor recebido (R$)", c.Balance); d.Choice("method", "Forma de pagamento", new[] { PaymentMethod.Dinheiro, PaymentMethod.Pix, PaymentMethod.Debito, PaymentMethod.Credito });
        d.Submit = f => Vm.Service.ReceiveDebt(c.Customer.Id, f.Decimal("amount"), f.Selected<PaymentMethod>("method")); d.ShowDialog();
    });
    private void DebtStatement_Click(object sender, RoutedEventArgs e) => Guard(() =>
    {
        var c = Require(Vm.SelectedCustomer, "um cliente"); var state = Vm.Service.Snapshot;
        var d = new FormDialog(this, $"Fiados • {c.Name}", $"Saldo devedor: {StoreService.Balance(state, c.Customer.Id):C2}", "Fechar");
        var sales = state.Sales.Where(v => v.CustomerId == c.Customer.Id && v.PaymentMethod == PaymentMethod.Fiado && !v.Canceled).OrderBy(v => v.Date);
        foreach (var sale in sales) d.Info($"{sale.NumberLabel} • {sale.Date:dd/MM/yyyy}\nVenda: {sale.Total:C2}  |  A receber: {StoreService.Outstanding(state, sale):C2}");
        if (!sales.Any()) d.Info("Nenhuma venda em fiado registrada.");
        foreach (var payment in state.ReceivablePayments.Where(p => sales.Any(v => v.Id == p.SaleId)).OrderByDescending(p => p.Date)) d.Info($"Recebido em {payment.Date:dd/MM HH:mm}: {payment.Amount:C2} • {Labels.Payment(payment.Method)}");
        d.ShowDialog();
    });
    private void NewUser_Click(object sender, RoutedEventArgs e) => Guard(() => UserForm(new() { Role = UserRole.Operador }, false));
    private void EditUser_Click(object sender, RoutedEventArgs e) => Guard(() => UserForm(Require(Vm.SelectedUser, "um usuário"), true));
    private void UserForm(UserAccount user, bool editing)
    {
        var d = new FormDialog(this, editing ? "Editar usuário" : "Criar usuário", "Use uma conta individual para cada pessoa.");
        d.Text("name", "Nome", user.Name); d.Text("username", "Usuário", user.Username); d.Choice("role", "Perfil", Enum.GetValues<UserRole>(), user.Role);
        d.Password("password", editing ? "Nova senha • deixe vazio para manter" : "Senha (mínimo de 4 caracteres)"); d.Password("confirm", "Confirmar senha"); d.Check("active", "Usuário ativo", user.Active);
        d.Submit = f =>
        {
            if (f.Value("password") != f.Value("confirm")) throw new InvalidOperationException("As senhas não conferem.");
            Vm.Service.SaveUser(new() { Id = user.Id, Name = f.Value("name"), Username = f.Value("username"), Role = f.Selected<UserRole>("role"), Active = f.Checked("active") }, f.Value("password"));
        };
        if (d.ShowDialog() == true) { Notify("Usuário salvo."); if (editing && user.Id == Vm.Service.CurrentUserId) { ReturnToLogin = true; Close(); } }
    }
    private void ChangePassword_Click(object sender, RoutedEventArgs e) => Guard(() =>
    {
        var d = new FormDialog(this, "Alterar minha senha", "Informe a senha atual e escolha a nova senha.");
        d.Password("old", "Senha atual"); d.Password("new", "Nova senha (mínimo de 4 caracteres)"); d.Password("confirm", "Confirmar nova senha");
        d.Submit = f => { if (f.Value("new") != f.Value("confirm")) throw new InvalidOperationException("As senhas não conferem."); Vm.Service.ChangePassword(f.Value("old"), f.Value("new")); };
        if (d.ShowDialog() == true) Notify("Senha alterada.");
    });
    private void NewPrinter_Click(object sender, RoutedEventArgs e) => Guard(() => PrinterForm(new()));
    private void EditPrinter_Click(object sender, RoutedEventArgs e) => Guard(() => PrinterForm(Require(Vm.SelectedPrinter, "uma impressora")));
    private void PrinterForm(PrinterProfile profile)
    {
        var printers = ReceiptPrinter.InstalledPrinters();
        if (printers.Count == 0) throw new InvalidOperationException("Nenhuma impressora foi encontrada. Adicione a impressora nas configurações do Windows e tente novamente.");
        var d = new FormDialog(this, "Cadastrar impressora", "Instale o driver no Windows antes de cadastrar a impressora no PDV.");
        d.Text("name", "Nome para identificar", profile.Name); d.Choice("queue", "Impressora instalada", printers, profile.QueueName);
        d.Choice("paper", "Formato do cupom", new[] { "58 mm", "80 mm", "A4" }, profile.Paper);
        d.Check("default", "Usar como impressora padrão do PDV", Vm.Snapshot.Settings.DefaultPrinterId == profile.Id || Vm.Printers.Count == 0);
        d.Submit = f => Vm.Service.SavePrinter(new() { Id = profile.Id, Name = f.Value("name"), QueueName = f.Value("queue"), Paper = f.Value("paper") }, f.Checked("default"));
        if (d.ShowDialog() == true) Notify("Impressora cadastrada.");
    }
    private void DeletePrinter_Click(object sender, RoutedEventArgs e) => Guard(() => { var p = Require(Vm.SelectedPrinter, "uma impressora"); if (Confirm($"Remover {p.Name} do PDV?")) Vm.Service.DeleteDevice(p.Id, true); });
    private void WindowsPrinters_Click(object sender, RoutedEventArgs e) => Guard(() => Process.Start(new ProcessStartInfo("ms-settings:printers") { UseShellExecute = true }));
    private void NewScale_Click(object sender, RoutedEventArgs e) => Guard(() => ScaleForm(new()));
    private void EditScale_Click(object sender, RoutedEventArgs e) => Guard(() => ScaleForm(Require(Vm.SelectedScale, "uma balança")));
    private void ScaleForm(ScaleProfile profile)
    {
        var d = new FormDialog(this, "Cadastrar balança", "Conexão serial 8N1. Confira os parâmetros no manual do equipamento. Balanças USB precisam disponibilizar uma porta COM compatível.");
        d.Text("name", "Nome para identificar", profile.Name);
        d.Info("Portas detectadas: " + string.Join(", ", SerialScaleReader.Ports().DefaultIfEmpty("nenhuma")));
        d.Text("port", "Porta serial", profile.Port); d.Choice("baud", "Velocidade (baud)", new[] { 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200 }, profile.BaudRate);
        d.Choice("protocol", "Formato de leitura", new[] { "Texto em kg", "Inteiro em gramas" }, profile.Protocol);
        d.Text("command", "Comando de solicitação em hexadecimal • opcional", profile.CommandHex);
        d.Check("default", "Usar como balança padrão", Vm.Snapshot.Settings.DefaultScaleId == profile.Id || Vm.Scales.Count == 0);
        d.Submit = f => Vm.Service.SaveScale(new() { Id = profile.Id, Name = f.Value("name"), Port = f.Value("port"), BaudRate = f.Selected<int>("baud"), Protocol = f.Value("protocol"), CommandHex = f.Value("command") }, f.Checked("default"));
        if (d.ShowDialog() == true) Notify("Balança cadastrada. Use Testar leitura para validar a conexão.");
    }
    private void DeleteScale_Click(object sender, RoutedEventArgs e) => Guard(() => { var p = Require(Vm.SelectedScale, "uma balança"); if (Confirm($"Remover {p.Name} do PDV?")) Vm.Service.DeleteDevice(p.Id, false); });
    private async void TestScale_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender; button.IsEnabled = false;
        try { var scale = Require(Vm.SelectedScale, "uma balança"); var result = await SerialScaleReader.ReadAsync(scale); Notify($"Leitura de {scale.Name}: {result:0.000} kg."); }
        catch (Exception ex) { Notify(ex.Message); }
        finally { button.IsEnabled = true; }
    }
    private void AddWeighed(Product product)
    {
        var d = new FormDialog(this, "Produto por peso", $"{product.Name} • {product.Price:C2} / kg", "Adicionar à venda");
        var weight = d.Number("weight", "Peso em kg", 1);
        var scale = Vm.Snapshot.Scales.FirstOrDefault(s => s.Id == Vm.Snapshot.Settings.DefaultScaleId);
        if (scale is not null)
        {
            var read = d.Action($"Ler da balança • {scale.Name}", () => { });
            read.Click += async (_, _) =>
            {
                read.IsEnabled = false;
                try { weight.Text = (await SerialScaleReader.ReadAsync(scale)).ToString("0.000"); }
                catch (Exception ex) { d.ShowError(ex.Message); }
                finally { read.IsEnabled = true; }
            };
        }
        else d.Info("Informe o peso manualmente. Uma balança pode ser cadastrada em Equipamentos.");
        d.Submit = f =>
        {
            decimal quantity = f.Decimal("weight");
            if (quantity <= 0 || quantity != decimal.Round(quantity, 3)) throw new InvalidOperationException("Informe um peso positivo com até 3 casas decimais.");
            if (Vm.Service.Available(product.Id) < quantity + (Vm.Cart.FirstOrDefault(c => c.Product.Id == product.Id)?.Quantity ?? 0)) throw new InvalidOperationException("Estoque insuficiente.");
            Vm.AddProduct(product, quantity);
        };
        d.ShowDialog();
    }
    private void SaleDetails_Click(object sender, RoutedEventArgs e) => Guard(() =>
    {
        var sale = Require(Vm.SelectedSale, "uma venda");
        var d = new FormDialog(this, $"Venda {sale.NumberLabel}", $"{sale.Date:dd/MM/yyyy HH:mm} • {sale.UserName} • {sale.StatusLabel}", "Fechar");
        d.Info($"Cliente: {sale.CustomerName}\n{sale.CustomerPhone}\n{sale.DeliveryAddress}");
        foreach (var line in sale.Lines) d.Info($"{line.Quantity:0.###} {line.Unit} • {line.ProductName}\n{line.UnitPrice:C2} cada  →  {line.GrossTotal:C2}");
        d.Info($"Subtotal: {sale.Subtotal:C2}\nDesconto: {sale.Discount:C2}\nEntrega: {sale.DeliveryFee:C2}\nTotal: {sale.Total:C2}\n{sale.PaymentLabel}\nTroco: {sale.Change:C2}");
        if (sale.Notes.Length > 0) d.Info(sale.Notes); if (sale.Canceled) d.Info($"Cancelamento: {sale.CancelReason}");
        d.Action("Imprimir cupom", () => ReceiptPrinter.PrintSale(d, Vm.Snapshot, sale)); d.ShowDialog();
    });
    private void CancelSale_Click(object sender, RoutedEventArgs e) => Guard(() =>
    {
        var sale = Require(Vm.SelectedSale, "uma venda"); var d = new FormDialog(this, "Cancelar venda", $"{sale.NumberLabel} • Os produtos voltarão ao estoque e o caixa será atualizado.", "Confirmar cancelamento");
        d.Text("reason", "Motivo"); d.Submit = f => Vm.Service.CancelSale(sale.Id, f.Value("reason")); if (d.ShowDialog() == true) Notify("Venda cancelada; estoque e caixa atualizados.");
    });
    private void PrintSale_Click(object sender, RoutedEventArgs e) => Guard(() => ReceiptPrinter.PrintSale(this, Vm.Snapshot, Require(Vm.SelectedSale, "uma venda")));
    private void PrintOrder_Click(object sender, RoutedEventArgs e) => Guard(() => ReceiptPrinter.PrintOrder(this, Vm.Snapshot, Require(Vm.SelectedOrder, "um pedido")));
    private void PrintCash_Click(object sender, RoutedEventArgs e) => Guard(() => ReceiptPrinter.PrintCash(this, Vm.Snapshot, Require(Vm.SelectedCash, "um caixa")));
    private static string Csv(string value)
    {
        if (value.Length > 0 && "=+-@".Contains(value[0])) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
    private void ExportCsv(string suggested, IEnumerable<string[]> rows)
    {
        var file = new SaveFileDialog { Title = "Exportar relatório", FileName = suggested, Filter = "Planilha CSV|*.csv" };
        if (file.ShowDialog(this) != true) return;
        File.WriteAllLines(file.FileName, rows.Select(r => string.Join(";", r.Select(Csv))), new UTF8Encoding(true)); Notify("Relatório exportado.");
    }
    private void ExportSales_Click(object sender, RoutedEventArgs e) => Guard(() => ExportCsv($"vendas-{DateTime.Today:yyyyMMdd}.csv",
        new[] { new[] { "Venda", "Data", "Cliente", "Pagamento", "Total", "Vendedor", "Situação" } }.Concat(Vm.Sales.Select(v => new[] { v.NumberLabel, v.Date.ToString("dd/MM/yyyy HH:mm"), v.CustomerName, v.PaymentLabel, v.Total.ToString("0.00"), v.UserName, v.StatusLabel }))));
    private void ExportProfit_Click(object sender, RoutedEventArgs e) => Guard(() => ExportCsv($"lucratividade-{DateTime.Today:yyyyMMdd}.csv",
        new[] { new[] { "Produto", "Quantidade", "Receita líquida", "Custo", "Lucro bruto", "Margem" } }.Concat(Vm.ProfitRows.Select(v => new[] { v.Name, v.Quantity.ToString("0.###"), v.Revenue.ToString("0.00"), v.Cost.ToString("0.00"), v.Profit.ToString("0.00"), v.Margin.ToString("P2") }))));
    private void SaveSettings_Click(object sender, RoutedEventArgs e) => Guard(() => Vm.Service.SaveSettings(Vm.Snapshot.Settings), "Configurações salvas.");
    private void Backup_Click(object sender, RoutedEventArgs e) => Guard(() =>
    {
        var file = new SaveFileDialog { Title = "Criar backup", FileName = $"GiroBebidas-backup-{DateTime.Now:yyyyMMdd-HHmm}.json", Filter = "Backup Giro Bebidas|*.json" };
        if (file.ShowDialog(this) == true) { Vm.Service.ExportBackup(file.FileName); Notify("Backup criado, incluindo cadastros, fotos, usuários e movimentos."); }
    });
    private void Restore_Click(object sender, RoutedEventArgs e) => Guard(() =>
    {
        if (Vm.Cart.Count > 0) throw new InvalidOperationException("Limpe ou salve a venda atual antes de restaurar.");
        var file = new OpenFileDialog { Title = "Restaurar backup", Filter = "Backup Giro Bebidas|*.json" };
        if (file.ShowDialog(this) != true || !Confirm("Restaurar este backup e substituir os dados atuais? Uma cópia dos dados atuais será preservada. Você precisará entrar novamente.")) return;
        Vm.Service.RestoreBackup(file.FileName); ReturnToLogin = true; Close();
    });
    private void DataFolder_Click(object sender, RoutedEventArgs e) => Guard(() => Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { Vm.Service.Store.DirectoryPath } }));
}
