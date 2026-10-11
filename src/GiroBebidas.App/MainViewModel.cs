using System.Collections.ObjectModel;
using System.Windows.Input;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public record CustomerChoice(Guid? Id, string Name);
public sealed class MainViewModel : Observable
{
    public StoreService Service { get; }
    public ObservableCollection<CartLine> Cart { get; } = [];
    public List<NavItem> Navigation { get; }
    public string StoreName => Service.Snapshot.Settings.Name;
    public string OperatorName => Service.CurrentUser?.Name ?? "";
    public string OperatorInitial => OperatorName.Length > 0 ? OperatorName[..1].ToUpperInvariant() : "G";
    public string OperatorRole => Service.CurrentUser?.RoleLabel ?? "";
    public string Today => DateTime.Today.ToString("dddd, dd 'de' MMMM");
    public bool IsAdmin => Service.IsAdmin;
    public bool CanEditCart => ActiveOrderId is null;
    public bool CanSell => Cart.Count > 0;
    public string CartTitle => ActiveOrderId is null ? "Venda atual" : $"Pedido {ActiveOrderNumber}";
    public string CartCount => $"{Cart.Count} produto{(Cart.Count == 1 ? "" : "s")} na venda";
    public decimal Subtotal => Cart.Sum(l => l.Total);
    public decimal Total => Subtotal - Discount + DeliveryFee;
    public string CashStatus => Snapshot.CashSessions.Any(c => c.ClosedAt is null) ? "Caixa aberto" : "Caixa fechado";
    public string ProductCount => $"{Tiles.Count} produtos encontrados";
    public string CartHint => ActiveOrderId is null ? "O preço de atacado é aplicado automaticamente." : "Os produtos e preços deste pedido estão reservados.";
    public string EmptyCartVisibility => Cart.Count == 0 ? "Visible" : "Collapsed";
    public Guid? ActiveOrderId { get; private set; }
    public string ActiveOrderNumber { get; private set; } = "";
    public string ActiveOrderAddress { get; private set; } = "";
    public string ActiveOrderNotes { get; private set; } = "";
    public StoreState Snapshot { get; private set; } = new();
    public List<ProductTile> Tiles { get; private set; } = [];
    public List<StockRow> StockRows { get; private set; } = [];
    public List<CustomerRow> CustomerRows { get; private set; } = [];
    public List<CustomerChoice> CustomerOptions { get; private set; } = [];
    public List<Order> Orders { get; private set; } = [];
    public List<Sale> Sales { get; private set; } = [];
    public List<UserAccount> Users { get; private set; } = [];
    public List<ProfitRow> ProfitRows { get; private set; } = [];
    public List<CashSession> CashSessions { get; private set; } = [];
    public List<CashMovement> CashMovements { get; private set; } = [];
    public List<StockMovement> StockMovements { get; private set; } = [];
    public List<AuditEntry> AuditEntries { get; private set; } = [];
    public List<PrinterProfile> Printers { get; private set; } = [];
    public List<ScaleProfile> Scales { get; private set; } = [];
    public CashSummary? CashSummary { get; private set; }
    public bool CashIsOpen => Snapshot.CashSessions.Any(c => c.ClosedAt is null);
    public string CashOpenedLabel => Snapshot.CashSessions.FirstOrDefault(c => c.ClosedAt is null) is { } c ? $"Aberto por {c.OpenedBy}, em {c.OpenedAt:dd/MM 'às' HH:mm}" : "Abra um caixa para começar a registrar vendas.";
    public string PrinterDefaultLabel => Snapshot.Printers.FirstOrDefault(p => p.Id == Snapshot.Settings.DefaultPrinterId)?.Name ?? "Nenhuma impressora padrão";
    public string ScaleDefaultLabel => Snapshot.Scales.FirstOrDefault(p => p.Id == Snapshot.Settings.DefaultScaleId)?.Name ?? "Pesagem manual";
    public decimal TodayTotal => Snapshot.Sales.Where(v => !v.Canceled && v.Date.LocalDateTime.Date == DateTime.Today).Sum(v => v.Total);
    public int TodayCount => Snapshot.Sales.Count(v => !v.Canceled && v.Date.LocalDateTime.Date == DateTime.Today);
    public decimal TodayProfit => Snapshot.Sales.Where(v => !v.Canceled && v.Date.LocalDateTime.Date == DateTime.Today).Sum(v => v.Profit);
    public decimal TotalDebt => Snapshot.Sales.Sum(v => StoreService.Outstanding(Snapshot, v));
    public int LowStockCount => Snapshot.Products.Count(p => p.Active && StoreService.Available(Snapshot, p) <= p.MinimumStock);
    public int OpenOrderCount => Snapshot.Orders.Count(o => o.IsOpen);
    public decimal ReportRevenue => ProfitRows.Sum(r => r.Revenue);
    public decimal ReportCost => ProfitRows.Sum(r => r.Cost);
    public decimal ReportProfit => ProfitRows.Sum(r => r.Profit);
    public string ReportMargin => ReportRevenue == 0 ? "0%" : (ReportProfit / ReportRevenue).ToString("P1");
    public StockRow? SelectedStock { get; set; }
    public CustomerRow? SelectedCustomer { get; set; }
    public Order? SelectedOrder { get; set; }
    public Sale? SelectedSale { get; set; }
    public UserAccount? SelectedUser { get; set; }
    public CashSession? SelectedCash { get; set; }
    public PrinterProfile? SelectedPrinter { get; set; }
    public ScaleProfile? SelectedScale { get; set; }
    private string page = "PDV", search = "", category = "Todos", stockSearch = "", clientSearch = "", reportPeriod = "Últimos 30 dias", message = "";
    private decimal discount, deliveryFee;
    private CustomerChoice? cartCustomer;
    public string Page { get => page; set { if (Set(ref page, value)) { Changed(nameof(PageTitle)); Changed(nameof(PageSubtitle)); } } }
    public string Search { get => search; set { if (Set(ref search, value)) FilterProducts(); } }
    public string StockSearch { get => stockSearch; set { if (Set(ref stockSearch, value)) FilterStock(); } }
    public string ClientSearch { get => clientSearch; set { if (Set(ref clientSearch, value)) FilterClients(); } }
    public string Category { get => category; set { if (Set(ref category, value)) FilterProducts(); } }
    public string ReportPeriod { get => reportPeriod; set { if (Set(ref reportPeriod, value)) BuildReport(); } }
    public string Message { get => message; set => Set(ref message, value); }
    public decimal Discount { get => discount; set { if (Set(ref discount, value)) Changed(nameof(Total)); } }
    public decimal DeliveryFee { get => deliveryFee; set { if (Set(ref deliveryFee, value)) Changed(nameof(Total)); } }
    public CustomerChoice? CartCustomer { get => cartCustomer; set => Set(ref cartCustomer, value); }
    public string PageTitle => Page switch { "PDV" => "Frente de caixa", "Estoque" => "Produtos e estoque", "Clientes" => "Clientes e fiados", "Caixa" => "Controle do caixa", "Vendas" => "Histórico de vendas", "Relatórios" => "Resultados do depósito", "Usuários" => "Usuários e acessos", "Equipamentos" => "Impressoras e balanças", "Configurações" => "Configurações", _ => "Pedidos e entregas" };
    public string PageSubtitle => Page switch { "PDV" => "Tudo pronto para a próxima venda.", "Estoque" => "Cuide dos seus produtos e acompanhe cada movimentação.", "Clientes" => "Um atendimento próximo, com crédito sob controle.", "Caixa" => "Entradas, saídas e fechamento em um só lugar.", "Vendas" => "Consulte vendas, reimprima cupons e confira os detalhes.", "Relatórios" => "Acompanhe a receita, o custo e o lucro dos seus produtos.", "Usuários" => "Defina quem pode acessar e administrar o depósito.", "Equipamentos" => "Conecte o que você usa no balcão.", "Configurações" => "Deixe o sistema com a cara do seu negócio.", _ => "Do balcão até a entrega, acompanhe cada pedido." };
    public ICommand AddProductCommand { get; }
    public ICommand IncrementCommand { get; }
    public ICommand DecrementCommand { get; }
    public ICommand RemoveCommand { get; }
    public Action<Product>? RequestWeight { get; set; }
    public Action<string>? Notify { get; set; }

    public MainViewModel(StoreService service)
    {
        Service = service;
        Navigation = [new("PDV", "Frente de caixa", "\uE719"), new("Pedidos", "Pedidos e entregas", "\uE8CB"), new("Clientes", "Clientes e fiados", "\uE716"), new("Caixa", "Caixa", "\uE8C7"), new("Vendas", "Vendas", "\uE9D9")];
        if (service.IsAdmin) { Navigation.Insert(2, new("Estoque", "Produtos e estoque", "\uE7B8")); Navigation.AddRange([new("Relatórios", "Relatórios", "\uE9D2"), new("Usuários", "Usuários", "\uE77B"), new("Equipamentos", "Equipamentos", "\uE772"), new("Configurações", "Configurações", "\uE713")]); }
        AddProductCommand = new RelayCommand(p => { if (p is ProductTile tile) { if (tile.Product.IsWeighed) RequestWeight?.Invoke(tile.Product); else AddProduct(tile.Product, 1); } }, _ => CanEditCart);
        IncrementCommand = new RelayCommand(p => { if (p is CartLine line) { if (line.Product.IsWeighed) RequestWeight?.Invoke(line.Product); else AddProduct(line.Product, 1); } }, _ => CanEditCart);
        DecrementCommand = new RelayCommand(p => { if (p is CartLine line) { if (line.Product.IsWeighed || line.Quantity <= 1) Cart.Remove(line); else line.Quantity--; CartChanged(); } }, _ => CanEditCart);
        RemoveCommand = new RelayCommand(p => { if (p is CartLine line) { Cart.Remove(line); CartChanged(); } }, _ => CanEditCart);
        Refresh();
    }
    public void AddProduct(Product p, decimal quantity)
    {
        if (!CanEditCart) return;
        var existing = Cart.FirstOrDefault(l => l.Product.Id == p.Id);
        if (Service.Available(p.Id) < (existing?.Quantity ?? 0) + quantity) { Notify?.Invoke("Estoque insuficiente para este produto."); return; }
        if (existing is null) Cart.Add(new(p, quantity)); else existing.Quantity += quantity;
        CartChanged();
    }
    private void CartChanged()
    {
        foreach (string property in new[] { nameof(Subtotal), nameof(Total), nameof(CanSell), nameof(CartCount), nameof(EmptyCartVisibility), nameof(CanEditCart), nameof(CartTitle), nameof(CartHint) }) Changed(property);
        CommandManager.InvalidateRequerySuggested();
    }
    public void ClearCart()
    {
        Cart.Clear(); ActiveOrderId = null; ActiveOrderNumber = ""; ActiveOrderAddress = ""; ActiveOrderNotes = "";
        Discount = 0; DeliveryFee = 0; CartCustomer = CustomerOptions.FirstOrDefault(); CartChanged();
    }
    public void LoadOrder(Order order)
    {
        ClearCart(); ActiveOrderId = order.Id; ActiveOrderNumber = order.NumberLabel; ActiveOrderAddress = order.Address; ActiveOrderNotes = order.Notes;
        foreach (var line in order.Lines) Cart.Add(new(Snapshot.Products.Single(p => p.Id == line.ProductId), line.Quantity, line.UnitPrice));
        Discount = order.Discount; DeliveryFee = order.DeliveryFee; CartCustomer = CustomerOptions.FirstOrDefault(c => c.Id == order.CustomerId);
        Page = "PDV"; CartChanged();
    }
    public List<DraftItem> Draft() => Cart.Select(l => new DraftItem(l.Product.Id, l.Quantity)).ToList();
    private static bool Matches(string term, params string[] values) => values.Any(v => v.Contains(term, StringComparison.CurrentCultureIgnoreCase));
    private void FilterProducts()
    {
        Tiles = Snapshot.Products.Where(p => p.Active && (Category == "Todos" || p.Category == Category) && Matches(Search.Trim(), p.Name, p.Code, p.Barcode)).Select(p => new ProductTile(p, StoreService.Available(Snapshot, p, ActiveOrderId))).ToList();
        Changed(nameof(Tiles)); Changed(nameof(ProductCount));
    }
    private void FilterStock() { StockRows = Snapshot.Products.Where(p => Matches(StockSearch.Trim(), p.Name, p.Code, p.Barcode)).Select(p => new StockRow(p, StoreService.Reserved(Snapshot, p.Id))).ToList(); Changed(nameof(StockRows)); }
    private void FilterClients() { CustomerRows = Snapshot.Customers.Where(c => Matches(ClientSearch.Trim(), c.Name, c.Phone, c.Document)).OrderBy(c => c.Name).Select(c => new CustomerRow(c, StoreService.Balance(Snapshot, c.Id))).ToList(); Changed(nameof(CustomerRows)); }
    private void BuildReport()
    {
        DateTimeOffset? from = ReportPeriod switch { "Hoje" => new DateTimeOffset(DateTime.Today), "Últimos 7 dias" => new DateTimeOffset(DateTime.Today.AddDays(-6)), "Últimos 30 dias" => new DateTimeOffset(DateTime.Today.AddDays(-29)), _ => null };
        ProfitRows = StoreService.ProfitReport(Snapshot, from);
        foreach (string property in new[] { nameof(ProfitRows), nameof(ReportRevenue), nameof(ReportCost), nameof(ReportProfit), nameof(ReportMargin) }) Changed(property);
    }
    public void Refresh()
    {
        Snapshot = Service.Snapshot;
        Guid? customerId = CartCustomer?.Id;
        CustomerOptions = [new(null, "Consumidor final"), .. Snapshot.Customers.OrderBy(c => c.Name).Select(c => new CustomerChoice(c.Id, c.Name))];
        CartCustomer = CustomerOptions.FirstOrDefault(c => c.Id == customerId) ?? CustomerOptions[0];
        Orders = Snapshot.Orders.OrderByDescending(o => o.Date).ToList(); Sales = Snapshot.Sales.OrderByDescending(v => v.Date).ToList();
        Users = Snapshot.Users.OrderBy(u => u.Username).ToList(); CashSessions = Snapshot.CashSessions.OrderByDescending(c => c.OpenedAt).ToList();
        CashMovements = Snapshot.CashMovements.OrderByDescending(c => c.Date).ToList(); StockMovements = Snapshot.StockMovements.OrderByDescending(m => m.Date).Take(250).ToList();
        AuditEntries = Snapshot.Audit.OrderByDescending(a => a.Date).Take(300).ToList(); Printers = Snapshot.Printers; Scales = Snapshot.Scales;
        var open = Snapshot.CashSessions.FirstOrDefault(c => c.ClosedAt is null);
        CashSummary = open is null ? null : StoreService.SummarizeCash(Snapshot, open.Id);
        FilterProducts(); FilterStock(); FilterClients(); BuildReport();
        foreach (string property in new[] { nameof(StoreName), nameof(CustomerOptions), nameof(Orders), nameof(Sales), nameof(Users), nameof(CashSessions), nameof(CashMovements), nameof(StockMovements), nameof(AuditEntries), nameof(Printers), nameof(Scales), nameof(CashSummary), nameof(CashIsOpen), nameof(CashStatus), nameof(CashOpenedLabel), nameof(PrinterDefaultLabel), nameof(ScaleDefaultLabel), nameof(TodayTotal), nameof(TodayCount), nameof(TodayProfit), nameof(TotalDebt), nameof(LowStockCount), nameof(OpenOrderCount) }) Changed(property);
        CartChanged();
    }
}
