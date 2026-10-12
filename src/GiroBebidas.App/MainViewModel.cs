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
    public List<CategoryChoice> CategoryOptions { get; } =
    [
        new("Todos", "M1,1 L7,1 7,7 1,7 Z M11,1 L17,1 17,7 11,7 Z M1,11 L7,11 7,17 1,17 Z M11,11 L17,11 17,17 11,17 Z"),
        new("Cervejas", "M3,5 L13,5 13,17 3,17 Z M13,7 L16,7 Q18,7 18,10 Q18,13 16,13 L13,13 M3,5 Q1,2 4,2 Q5,0 7,2 Q10,0 12,2 Q15,2 13,5 M6,8 L6,14 M10,8 L10,14"),
        new("Refrigerantes", "M7,1 L11,1 11,5 Q11,7 13,9 L13,17 5,17 5,9 Q7,7 7,5 Z M5,11 L13,11 M5,14 L13,14"),
        new("Destilados", "M1,1 L17,1 9,10 Z M9,10 L9,17 M5,17 L13,17 M4,4 L14,4"),
        new("Água e gelo", "M9,1 C7,5 2,8 2,12 A7,7 0 0 0 16,12 C16,8 11,5 9,1 Z"),
        new("Outros", "M2,8 A1,1 0 1 0 2,10 A1,1 0 1 0 2,8 M9,8 A1,1 0 1 0 9,10 A1,1 0 1 0 9,8 M16,8 A1,1 0 1 0 16,10 A1,1 0 1 0 16,8")
    ];
    public string StoreName => Service.Snapshot.Settings.Name;
    public string VersionLabel => $"Versão {typeof(StoreService).Assembly.GetName().Version!.ToString(3)} • Windows";
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
    public string CashActionLabel => CashIsOpen ? "Fechar caixa" : "Abrir caixa";
    public string ProductCount => $"{Tiles.Count} produto{(Tiles.Count == 1 ? " encontrado" : "s encontrados")}";
    public string GridViewVisibility => IsProductsList ? "Collapsed" : "Visible";
    public string ListViewVisibility => IsProductsList ? "Visible" : "Collapsed";
    public string CartHint => ActiveOrderId is null ? "O preço de atacado é aplicado automaticamente." : "Os produtos e preços deste pedido estão reservados.";
    public string EmptyCartVisibility => Cart.Count == 0 ? "Visible" : "Collapsed";
    public Guid? ActiveOrderId { get; private set; }
    public string ActiveOrderNumber { get; private set; } = "";
    public string ActiveOrderAddress { get; private set; } = "";
    public string ActiveOrderNotes { get; private set; } = "";
    public StoreState Snapshot { get; private set; } = new();
    public List<ProductTile> Tiles { get; private set; } = [];
    public List<StockRow> StockRows { get; private set; } = [];
    public List<string> StockCategories { get; private set; } = ["Todas as categorias"];
    public List<string> StockSortOptions { get; } = ["Nome do produto", "Código", "Categoria", "Estoque físico", "Estoque reservado", "Estoque livre", "Unidade", "Preço de venda", "Situação"];
    public List<StockMovementRow> RecentStockMovements { get; private set; } = [];
    public string StockCount => $"{StockRows.Count} produto{(StockRows.Count == 1 ? " encontrado" : "s encontrados")}";
    public string StockEmptyVisibility => StockRows.Count == 0 ? "Visible" : "Collapsed";
    public string StockSortDirectionLabel => StockSortDescending ? "Ordem decrescente" : "Ordem crescente";
    public List<CustomerRow> CustomerRows { get; private set; } = [];
    public List<CustomerChoice> CustomerOptions { get; private set; } = [];
    public List<Order> Orders { get; private set; } = [];
    public List<Order> OrderPageItems { get; private set; } = [];
    public List<string> OrderPeriodOptions { get; } = ["Hoje", "Últimos 7 dias", "Últimos 30 dias", "Todo o período"];
    public List<string> OrderStatusOptions { get; } = ["Todas as situações", .. Enum.GetValues<OrderStatus>().Select(Labels.Order)];
    public List<int> OrderPageSizeOptions { get; } = [10, 20, 50];
    public int OrderFilteredCount => filteredOrders.Count;
    public int OrderPageCount => Math.Max(1, (int)Math.Ceiling(OrderFilteredCount / (double)OrderPageSize));
    public string OrderRangeLabel => OrderFilteredCount == 0 ? "0 pedidos" : $"{(OrderPageNumber - 1) * OrderPageSize + 1}–{Math.Min(OrderPageNumber * OrderPageSize, OrderFilteredCount)} de {OrderFilteredCount} pedido{(OrderFilteredCount == 1 ? "" : "s")}";
    public string OrderPageLabel => $"Página {OrderPageNumber} de {OrderPageCount}";
    public string OrderEmptyVisibility => OrderPageItems.Count == 0 ? "Visible" : "Collapsed";
    public bool OrderCanPrevious => OrderPageNumber > 1;
    public bool OrderCanNext => OrderPageNumber < OrderPageCount;
    public bool OrderHasSelection => SelectedOrder is not null;
    public bool OrderCanChangeStatus => SelectedOrder?.IsOpen == true;
    public bool OrderCanReceiveSelected => SelectedOrder?.IsOpen == true;
    public List<Sale> Sales { get; private set; } = [];
    public List<UserAccount> Users { get; private set; } = [];
    public List<ProfitRow> ProfitRows { get; private set; } = [];
    public List<CashSession> CashSessions { get; private set; } = [];
    public List<CashMovement> CashMovements { get; private set; } = [];
    public List<string> CashPeriodOptions { get; } = ["Hoje", "Últimos 7 dias", "Últimos 30 dias", "Todo o período"];
    public string CashHistoryEmptyVisibility => CashSessions.Count == 0 ? "Visible" : "Collapsed";
    public string CashMovementsEmptyVisibility => CashMovements.Count == 0 ? "Visible" : "Collapsed";
    public bool CashHasSelectedClosed => SelectedCash?.ClosedAt is not null;
    public List<StockMovement> StockMovements { get; private set; } = [];
    public List<AuditEntry> AuditEntries { get; private set; } = [];
    public List<PrinterProfile> Printers { get; private set; } = [];
    public List<ScaleProfile> Scales { get; private set; } = [];
    public CashSummary? CashSummary { get; private set; }
    public bool CashIsOpen => Snapshot.CashSessions.Any(c => c.ClosedAt is null);
    public bool CashCanOpen => !CashIsOpen;
    public string CashOpenedLabel => Snapshot.CashSessions.FirstOrDefault(c => c.ClosedAt is null) is { } c ? $"Aberto por {c.OpenedBy}, em {c.OpenedAt:dd/MM 'às' HH:mm}" : "Abra um caixa para começar a registrar vendas.";
    public string PrinterDefaultLabel => Snapshot.Printers.FirstOrDefault(p => p.Id == Snapshot.Settings.DefaultPrinterId)?.Name ?? "Nenhuma impressora padrão";
    public string ScaleDefaultLabel => Snapshot.Scales.FirstOrDefault(p => p.Id == Snapshot.Settings.DefaultScaleId)?.Name ?? "Pesagem manual";
    public string FiscalIssuerLabel => Snapshot.Fiscal.Issuer.LegalName.Length > 0 ? Snapshot.Fiscal.Issuer.LegalName : "Cadastre os dados reais da empresa";
    public string FiscalEnvironmentLabel => Snapshot.Fiscal.EnvironmentLabel;
    public string FiscalNumberLabel => $"Modelo 65 • série {Snapshot.Fiscal.ActiveProfile.Series} • próximo número {Snapshot.Fiscal.ActiveProfile.NextNumber}";
    public string FiscalCertificateLabel => Snapshot.Fiscal.Certificate.IsImported ? $"A1 importado • validade {Snapshot.Fiscal.Certificate.NotAfter:dd/MM/yyyy}" : "Nenhum certificado importado";
    public string FiscalCscLabel => Snapshot.Fiscal.ActiveProfile.ProtectedCsc.Length > 0 ? $"CSC {Snapshot.Fiscal.ActiveProfile.CscId} • token protegido" : "CSC do ambiente ainda não cadastrado";
    public string FiscalPrinterLabel => Snapshot.Printers.FirstOrDefault(p => p.Id == Snapshot.Fiscal.Options.PrinterId)?.Name ?? "Escolha uma impressora em Configurar NFC-e";
    public List<FiscalCheck> FiscalChecks { get; private set; } = [];
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
    private Order? selectedOrder;
    public Order? SelectedOrder
    {
        get => selectedOrder;
        set
        {
            if (!Set(ref selectedOrder, value)) return;
            Changed(nameof(OrderHasSelection)); Changed(nameof(OrderCanChangeStatus)); Changed(nameof(OrderCanReceiveSelected));
        }
    }
    public Sale? SelectedSale { get; set; }
    public UserAccount? SelectedUser { get; set; }
    private CashSession? selectedCash;
    public CashSession? SelectedCash { get => selectedCash; set { if (Set(ref selectedCash, value)) Changed(nameof(CashHasSelectedClosed)); } }
    public PrinterProfile? SelectedPrinter { get; set; }
    public ScaleProfile? SelectedScale { get; set; }
    private string page = "PDV", search = "", category = "Todos", stockSearch = "", clientSearch = "", reportPeriod = "Últimos 30 dias", message = "";
    private string stockCategory = "Todas as categorias", stockSort = "Nome do produto";
    private string cashPeriod = "Últimos 30 dias";
    private string orderSearch = "", orderPeriod = "Todo o período", orderStatusFilter = "Todas as situações";
    private int orderPageSize = 10, orderPageNumber = 1;
    private List<Order> filteredOrders = [];
    private bool stockSortDescending;
    private decimal discount, deliveryFee;
    private bool isProductsList;
    private double productCardWidth = 194;
    private CustomerChoice? cartCustomer;
    public string Page { get => page; set { if (Set(ref page, value)) { Changed(nameof(PageTitle)); Changed(nameof(PageSubtitle)); if (value == "Fiscal" && IsAdmin) BuildFiscalChecks(); } } }
    public string Search { get => search; set { if (Set(ref search, value)) FilterProducts(); } }
    public string StockSearch { get => stockSearch; set { if (Set(ref stockSearch, value)) FilterStock(); } }
    public string StockCategory { get => stockCategory; set { if (Set(ref stockCategory, value)) FilterStock(); } }
    public string StockSort { get => stockSort; set { if (Set(ref stockSort, value)) FilterStock(); } }
    public bool StockSortDescending { get => stockSortDescending; set { if (Set(ref stockSortDescending, value)) { Changed(nameof(StockSortDirectionLabel)); FilterStock(); } } }
    public string ClientSearch { get => clientSearch; set { if (Set(ref clientSearch, value)) FilterClients(); } }
    public string Category { get => category; set { if (Set(ref category, value)) FilterProducts(); } }
    public string ReportPeriod { get => reportPeriod; set { if (Set(ref reportPeriod, value)) BuildReport(); } }
    public string CashPeriod { get => cashPeriod; set { if (Set(ref cashPeriod, value)) FilterCash(); } }
    public string OrderSearch { get => orderSearch; set { if (Set(ref orderSearch, value ?? "")) FilterOrders(true); } }
    public string OrderPeriod { get => orderPeriod; set { if (Set(ref orderPeriod, OrderPeriodOptions.Contains(value) ? value : "Todo o período")) FilterOrders(true); } }
    public string OrderStatusFilter { get => orderStatusFilter; set { if (Set(ref orderStatusFilter, OrderStatusOptions.Contains(value) ? value : "Todas as situações")) FilterOrders(true); } }
    public int OrderPageSize { get => orderPageSize; set { if (Set(ref orderPageSize, OrderPageSizeOptions.Contains(value) ? value : 10)) FilterOrders(true); } }
    public int OrderPageNumber
    {
        get => orderPageNumber;
        set
        {
            Guid? selectedId = SelectedOrder?.Id;
            if (Set(ref orderPageNumber, Math.Clamp(value, 1, OrderPageCount))) UpdateOrderPage(selectedId);
        }
    }
    public string Message { get => message; set => Set(ref message, value); }
    public bool IsProductsList { get => isProductsList; set { if (Set(ref isProductsList, value)) { Changed(nameof(GridViewVisibility)); Changed(nameof(ListViewVisibility)); } } }
    public double ProductCardWidth { get => productCardWidth; set => Set(ref productCardWidth, value); }
    public decimal Discount { get => discount; set { if (Set(ref discount, value)) Changed(nameof(Total)); } }
    public decimal DeliveryFee { get => deliveryFee; set { if (Set(ref deliveryFee, value)) Changed(nameof(Total)); } }
    public CustomerChoice? CartCustomer { get => cartCustomer; set => Set(ref cartCustomer, value); }
    public string PageTitle => Page switch { "PDV" => "Frente de caixa", "Estoque" => "Produtos e estoque", "Clientes" => "Clientes e fiados", "Caixa" => "Controle do caixa", "Vendas" => "Histórico de vendas", "Relatórios" => "Resultados do depósito", "Usuários" => "Usuários e acessos", "Equipamentos" => "Impressoras e balanças", "Fiscal" => "Cupom fiscal e NFC-e", "Atualizações" => "Atualizações do sistema", "Configurações" => "Configurações", _ => "Pedidos e entregas" };
    public string PageSubtitle => Page switch { "PDV" => "Tudo pronto para a próxima venda.", "Estoque" => "Cuide dos seus produtos e acompanhe cada movimentação.", "Clientes" => "Um atendimento próximo, com crédito sob controle.", "Caixa" => "Entradas, saídas e fechamento em um só lugar.", "Vendas" => "Consulte vendas, reimprima cupons e confira os detalhes.", "Relatórios" => "Acompanhe a receita, o custo e o lucro dos seus produtos.", "Usuários" => "Defina quem pode acessar e administrar o depósito.", "Equipamentos" => "Conecte o que você usa no balcão.", "Fiscal" => "Prepare os dados fiscais e acompanhe o que falta configurar.", "Atualizações" => "Consulte as novidades e mantenha seu PDV atualizado.", "Configurações" => "Deixe o sistema com a cara do seu negócio.", _ => "Do balcão até a entrega, acompanhe cada pedido." };
    public ICommand AddProductCommand { get; }
    public ICommand IncrementCommand { get; }
    public ICommand DecrementCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand OrderPreviousPageCommand { get; }
    public ICommand OrderNextPageCommand { get; }
    public Action<Product>? RequestWeight { get; set; }
    public Action<string>? Notify { get; set; }

    public MainViewModel(StoreService service)
    {
        Service = service;
        Navigation = [new("PDV", "Frente de caixa", "\uE719"), new("Pedidos", "Pedidos e entregas", "\uE8CB"), new("Clientes", "Clientes e fiados", "\uE716"), new("Caixa", "Caixa", "\uE8C7"), new("Vendas", "Vendas", "\uE9D9")];
        if (service.IsAdmin) { Navigation.Insert(2, new("Estoque", "Produtos e estoque", "\uE7B8")); Navigation.AddRange([new("Relatórios", "Relatórios", "\uE9D2"), new("Usuários", "Usuários", "\uE77B"), new("Equipamentos", "Equipamentos", "\uE772"), new("Fiscal", "Cupom fiscal", "\uE8A5"), new("Atualizações", "Atualizações", "\uE895"), new("Configurações", "Configurações", "\uE713")]); }
        AddProductCommand = new RelayCommand(p => { if (p is ProductTile tile) { if (tile.Product.IsWeighed) RequestWeight?.Invoke(tile.Product); else AddProduct(tile.Product, 1); } }, _ => CanEditCart);
        IncrementCommand = new RelayCommand(p => { if (p is CartLine line) { if (line.Product.IsWeighed) RequestWeight?.Invoke(line.Product); else AddProduct(line.Product, 1); } }, _ => CanEditCart);
        DecrementCommand = new RelayCommand(p => { if (p is CartLine line) { if (line.Product.IsWeighed || line.Quantity <= 1) Cart.Remove(line); else line.Quantity--; CartChanged(); } }, _ => CanEditCart);
        RemoveCommand = new RelayCommand(p => { if (p is CartLine line) { Cart.Remove(line); CartChanged(); } }, _ => CanEditCart);
        OrderPreviousPageCommand = new RelayCommand(_ => { if (OrderCanPrevious) OrderPageNumber--; }, _ => OrderCanPrevious);
        OrderNextPageCommand = new RelayCommand(_ => { if (OrderCanNext) OrderPageNumber++; }, _ => OrderCanNext);
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
    private void FilterStock()
    {
        Guid? selected = SelectedStock?.Product.Id;
        var rows = Snapshot.Products.Where(p => (StockCategory == "Todas as categorias" || p.Category.Equals(StockCategory, StringComparison.CurrentCultureIgnoreCase)) && Matches(StockSearch.Trim(), p.Name, p.Code, p.Barcode, p.Category))
            .Select(p => new StockRow(p, StoreService.Reserved(Snapshot, p.Id), StoreService.CanDeleteProduct(Snapshot, p.Id)));
        var textComparer = StringComparer.CurrentCultureIgnoreCase;
        IOrderedEnumerable<StockRow> ordered = StockSort switch
        {
            "Código" => StockSortDescending ? rows.OrderByDescending(r => r.Code, textComparer) : rows.OrderBy(r => r.Code, textComparer),
            "Categoria" => StockSortDescending ? rows.OrderByDescending(r => r.Category, textComparer) : rows.OrderBy(r => r.Category, textComparer),
            "Estoque físico" => StockSortDescending ? rows.OrderByDescending(r => r.Stock) : rows.OrderBy(r => r.Stock),
            "Estoque reservado" => StockSortDescending ? rows.OrderByDescending(r => r.Reserved) : rows.OrderBy(r => r.Reserved),
            "Estoque livre" => StockSortDescending ? rows.OrderByDescending(r => r.Available) : rows.OrderBy(r => r.Available),
            "Unidade" => StockSortDescending ? rows.OrderByDescending(r => r.Unit, textComparer) : rows.OrderBy(r => r.Unit, textComparer),
            "Preço de venda" => StockSortDescending ? rows.OrderByDescending(r => r.Price) : rows.OrderBy(r => r.Price),
            "Situação" => StockSortDescending ? rows.OrderByDescending(r => r.Status, textComparer) : rows.OrderBy(r => r.Status, textComparer),
            _ => StockSortDescending ? rows.OrderByDescending(r => r.Name, textComparer) : rows.OrderBy(r => r.Name, textComparer)
        };
        StockRows = ordered.ThenBy(r => r.Code, textComparer).ToList(); Changed(nameof(StockRows));
        SelectedStock = StockRows.FirstOrDefault(r => r.Product.Id == selected); Changed(nameof(SelectedStock));
        Changed(nameof(StockCount)); Changed(nameof(StockEmptyVisibility));
    }
    private void FilterClients() { CustomerRows = Snapshot.Customers.Where(c => Matches(ClientSearch.Trim(), c.Name, c.Phone, c.Document)).OrderBy(c => c.Name).Select(c => new CustomerRow(c, StoreService.Balance(Snapshot, c.Id))).ToList(); Changed(nameof(CustomerRows)); }
    private void FilterOrders(bool resetPage = false)
    {
        Guid? selectedId = SelectedOrder?.Id;
        DateTime today = DateTime.Today;
        DateTime? firstDate = OrderPeriod switch { "Hoje" => today, "Últimos 7 dias" => today.AddDays(-6), "Últimos 30 dias" => today.AddDays(-29), _ => null };
        string term = OrderSearch.Trim();
        filteredOrders = Orders.Where(o => (firstDate is null || (o.Date.LocalDateTime.Date >= firstDate.Value && o.Date.LocalDateTime.Date <= today))
            && (OrderStatusFilter == "Todas as situações" || o.StatusLabel == OrderStatusFilter)
            && Matches(term, o.NumberLabel, o.CustomerName, o.Phone, o.Address)).ToList();
        orderPageNumber = resetPage ? 1 : Math.Clamp(orderPageNumber, 1, OrderPageCount);
        UpdateOrderPage(selectedId);
    }
    private void UpdateOrderPage(Guid? selectedId)
    {
        OrderPageItems = filteredOrders.Skip((OrderPageNumber - 1) * OrderPageSize).Take(OrderPageSize).ToList();
        Changed(nameof(OrderPageItems));
        SelectedOrder = OrderPageItems.FirstOrDefault(o => o.Id == selectedId);
        foreach (string property in new[] { nameof(OrderFilteredCount), nameof(OrderPageNumber), nameof(OrderPageCount), nameof(OrderRangeLabel), nameof(OrderPageLabel), nameof(OrderEmptyVisibility), nameof(OrderCanPrevious), nameof(OrderCanNext) }) Changed(property);
        CommandManager.InvalidateRequerySuggested();
    }
    private void FilterCash()
    {
        Guid? selectedId = SelectedCash?.Id;
        DateTime today = DateTime.Today;
        DateTime? firstDate = CashPeriod switch { "Hoje" => today, "Últimos 7 dias" => today.AddDays(-6), "Últimos 30 dias" => today.AddDays(-29), _ => null };
        bool InPeriod(DateTimeOffset date) => firstDate is null || (date.LocalDateTime.Date >= firstDate.Value && date.LocalDateTime.Date <= today);
        CashSessions = Snapshot.CashSessions.Where(c => InPeriod(c.OpenedAt)).OrderByDescending(c => c.OpenedAt).ToList();
        CashMovements = Snapshot.CashMovements.Where(c => InPeriod(c.Date)).OrderByDescending(c => c.Date).ToList();
        Changed(nameof(CashSessions)); Changed(nameof(CashMovements));
        SelectedCash = CashSessions.FirstOrDefault(c => c.Id == selectedId);
        Changed(nameof(CashHistoryEmptyVisibility)); Changed(nameof(CashMovementsEmptyVisibility));
    }
    private void BuildReport()
    {
        DateTimeOffset? from = ReportPeriod switch { "Hoje" => new DateTimeOffset(DateTime.Today), "Últimos 7 dias" => new DateTimeOffset(DateTime.Today.AddDays(-6)), "Últimos 30 dias" => new DateTimeOffset(DateTime.Today.AddDays(-29)), _ => null };
        ProfitRows = StoreService.ProfitReport(Snapshot, from);
        foreach (string property in new[] { nameof(ProfitRows), nameof(ReportRevenue), nameof(ReportCost), nameof(ReportProfit), nameof(ReportMargin) }) Changed(property);
    }
    public void BuildFiscalChecks() { FiscalChecks = Service.CheckFiscalConfiguration(); Changed(nameof(FiscalChecks)); }
    public void Refresh()
    {
        Snapshot = Service.Snapshot;
        StockCategories = ["Todas as categorias", .. Snapshot.Products.Select(p => p.Category).Distinct(StringComparer.CurrentCultureIgnoreCase).OrderBy(c => c, StringComparer.CurrentCultureIgnoreCase)];
        if (!StockCategories.Contains(stockCategory, StringComparer.CurrentCultureIgnoreCase)) { stockCategory = "Todas as categorias"; Changed(nameof(StockCategory)); }
        Changed(nameof(StockCategories));
        Guid? customerId = CartCustomer?.Id;
        CustomerOptions = [new(null, "Consumidor final"), .. Snapshot.Customers.OrderBy(c => c.Name).Select(c => new CustomerChoice(c.Id, c.Name))];
        CartCustomer = CustomerOptions.FirstOrDefault(c => c.Id == customerId) ?? CustomerOptions[0];
        Orders = Snapshot.Orders.OrderByDescending(o => o.Date).ToList(); Sales = Snapshot.Sales.OrderByDescending(v => v.Date).ToList();
        Users = Snapshot.Users.OrderBy(u => u.Username).ToList(); FilterCash();
        StockMovements = Snapshot.StockMovements.OrderByDescending(m => m.Date).Take(250).ToList();
        RecentStockMovements = StockMovements.Take(5).Select(m => new StockMovementRow(m)).ToList(); Changed(nameof(RecentStockMovements));
        AuditEntries = Snapshot.Audit.OrderByDescending(a => a.Date).Take(300).ToList(); Printers = Snapshot.Printers; Scales = Snapshot.Scales;
        var open = Snapshot.CashSessions.FirstOrDefault(c => c.ClosedAt is null);
        CashSummary = open is null ? null : StoreService.SummarizeCash(Snapshot, open.Id);
        FilterProducts(); FilterStock(); FilterClients(); FilterOrders(); BuildReport(); if (Page == "Fiscal" && IsAdmin) BuildFiscalChecks();
        foreach (string property in new[] { nameof(Snapshot), nameof(StoreName), nameof(CustomerOptions), nameof(Orders), nameof(Sales), nameof(Users), nameof(StockMovements), nameof(AuditEntries), nameof(Printers), nameof(Scales), nameof(CashSummary), nameof(CashIsOpen), nameof(CashCanOpen), nameof(CashActionLabel), nameof(CashStatus), nameof(CashOpenedLabel), nameof(PrinterDefaultLabel), nameof(ScaleDefaultLabel), nameof(TodayTotal), nameof(TodayCount), nameof(TodayProfit), nameof(TotalDebt), nameof(LowStockCount), nameof(OpenOrderCount), nameof(FiscalIssuerLabel), nameof(FiscalEnvironmentLabel), nameof(FiscalNumberLabel), nameof(FiscalCertificateLabel), nameof(FiscalCscLabel), nameof(FiscalPrinterLabel) }) Changed(property);
        CartChanged();
    }
}
