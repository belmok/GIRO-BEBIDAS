using System.Globalization;
using System.Windows.Input;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public sealed partial class MainViewModel
{
    private sealed record SalesFilter(string Search, DateTime? From, DateTime? Until, string Payment, string Status);
    private SalesFilter appliedSalesFilter = new("", null, null, "Todos", "Todas");
    private string salesSearch = "", salesPaymentFilter = "Todos", salesStatusFilter = "Todas";
    private DateTime? salesFrom, salesUntil;
    private int salesPageSize = 10, salesPageNumber = 1;
    private string salesSortMember = "Date";
    private bool salesSortDescending = true;
    private List<Sale> filteredSales = [];
    private IReadOnlyList<Sale> salesFilteredItems = Array.Empty<Sale>();

    public List<string> SalesPaymentOptions { get; } = ["Todos", .. Enum.GetValues<PaymentMethod>().Select(Labels.Payment)];
    public List<string> SalesStatusOptions { get; } = ["Todas", "Concluídas", "Canceladas"];
    public List<int> SalesPageSizeOptions { get; } = [10, 20, 50];
    public string SalesSearch { get => salesSearch; set => Set(ref salesSearch, value ?? ""); }
    public DateTime? SalesFrom { get => salesFrom; set => Set(ref salesFrom, value?.Date); }
    public DateTime? SalesUntil { get => salesUntil; set => Set(ref salesUntil, value?.Date); }
    public string SalesPaymentFilter { get => salesPaymentFilter; set => Set(ref salesPaymentFilter, SalesPaymentOptions.Contains(value) ? value : "Todos"); }
    public string SalesStatusFilter { get => salesStatusFilter; set => Set(ref salesStatusFilter, SalesStatusOptions.Contains(value) ? value : "Todas"); }
    public List<Sale> SalesPageItems { get; private set; } = [];
    public IReadOnlyList<Sale> SalesFilteredItems => salesFilteredItems;
    public int SalesFilteredCount => filteredSales.Count;
    public int SalesCompletedCount => filteredSales.Count(s => !s.Canceled);
    public decimal SalesCompletedTotal => filteredSales.Where(s => !s.Canceled).Sum(s => s.Total);
    public int SalesCanceledCount => filteredSales.Count(s => s.Canceled);
    public decimal SalesCanceledTotal => filteredSales.Where(s => s.Canceled).Sum(s => s.Total);
    public int SalesCustomerCount => filteredSales.Where(s => !s.Canceled).Select(s => s.CustomerId).Distinct().Count();
    public int SalesPageCount => SalesFilteredCount == 0 ? 1 : 1 + (SalesFilteredCount - 1) / SalesPageSize;
    public string SalesPageLabel => $"Página {SalesPageNumber} de {SalesPageCount}";
    public string SalesRangeLabel => SalesFilteredCount == 0 ? "Nenhuma venda encontrada" : $"Mostrando {(SalesPageNumber - 1) * SalesPageSize + 1} a {Math.Min((long)SalesPageNumber * SalesPageSize, SalesFilteredCount)} de {SalesFilteredCount} registro{(SalesFilteredCount == 1 ? "" : "s")}";
    public string SalesEmptyVisibility => SalesPageItems.Count == 0 ? "Visible" : "Collapsed";
    public bool SalesCanPrevious => SalesPageNumber > 1;
    public bool SalesCanNext => SalesPageNumber < SalesPageCount;
    public bool SalesHasSelection => SelectedSale is not null;
    public bool SalesCanCancelSelection => CanCancelSale(SelectedSale);
    public bool SalesCanExport => Service.CurrentUser is not null && SalesFilteredCount > 0;
    public string SalesSortMember => salesSortMember;
    public bool SalesSortDescending => salesSortDescending;
    public string SalesAppliedHint
    {
        get
        {
            string period = appliedSalesFilter switch
            {
                { From: { } from, Until: { } until } => $"{from:dd/MM/yyyy} a {until:dd/MM/yyyy}",
                { From: { } from } => $"A partir de {from:dd/MM/yyyy}",
                { Until: { } until } => $"Até {until:dd/MM/yyyy}",
                _ => "Todo o período"
            };
            string payment = appliedSalesFilter.Payment == "Todos" ? "Todos os pagamentos" : appliedSalesFilter.Payment;
            string status = appliedSalesFilter.Status == "Todas" ? "Todas as situações" : appliedSalesFilter.Status;
            return $"{period} • {payment} • {status}" + (appliedSalesFilter.Search.Length == 0 ? "" : $" • Busca: {appliedSalesFilter.Search}");
        }
    }
    public int SalesPageSize
    {
        get => salesPageSize;
        set
        {
            Guid? selectedId = SelectedSale?.Id;
            if (!Set(ref salesPageSize, SalesPageSizeOptions.Contains(value) ? value : 10)) return;
            salesPageNumber = 1;
            UpdateSalesPage(selectedId);
        }
    }
    public int SalesPageNumber
    {
        get => salesPageNumber;
        set
        {
            Guid? selectedId = SelectedSale?.Id;
            if (Set(ref salesPageNumber, Math.Clamp(value, 1, SalesPageCount))) UpdateSalesPage(selectedId);
        }
    }
    public ICommand SalesPreviousPageCommand { get; private set; } = null!;
    public ICommand SalesNextPageCommand { get; private set; } = null!;
    private void InitializeSalesHistory()
    {
        SalesPreviousPageCommand = new RelayCommand(_ => { if (SalesCanPrevious) SalesPageNumber--; }, _ => SalesCanPrevious);
        SalesNextPageCommand = new RelayCommand(_ => { if (SalesCanNext) SalesPageNumber++; }, _ => SalesCanNext);
    }
    public void ApplySalesFilters()
    {
        if (SalesFrom is { } from && SalesUntil is { } until && from > until)
            throw new InvalidOperationException("A data inicial não pode ser posterior à data final.");
        appliedSalesFilter = new(SalesSearch.Trim(), SalesFrom, SalesUntil, SalesPaymentFilter, SalesStatusFilter);
        RebuildSalesHistory(true);
    }
    public void ClearSalesFilters()
    {
        SalesSearch = "";
        SalesFrom = SalesUntil = null;
        SalesPaymentFilter = "Todos";
        SalesStatusFilter = "Todas";
        salesSortMember = "Date";
        salesSortDescending = true;
        Changed(nameof(SalesSortMember)); Changed(nameof(SalesSortDescending));
        ApplySalesFilters();
    }
    public void SetSalesSort(string member, bool descending)
    {
        if (!new[] { "Number", "Date", "CustomerName", "PaymentLabel", "Total", "UserName", "StatusLabel" }.Contains(member, StringComparer.Ordinal))
            throw new InvalidOperationException("A coluna escolhida não permite ordenar as vendas.");
        salesSortMember = member;
        salesSortDescending = descending;
        Changed(nameof(SalesSortMember)); Changed(nameof(SalesSortDescending));
        RebuildSalesHistory(true);
    }
    public void RefreshSalesHistory() => RebuildSalesHistory(false);
    internal bool CanCancelSale(Sale? sale)
    {
        if (!IsAdmin || sale is null) return false;
        StoreState state = Service.Snapshot;
        var current = state.Sales.FirstOrDefault(s => s.Id == sale.Id);
        if (current is null || current.Canceled) return false;
        var open = state.CashSessions.FirstOrDefault(c => c.ClosedAt is null);
        if (open is null || current.CashSessionId != open.Id || state.ReceivablePayments.Any(p => p.SaleId == current.Id)) return false;
        return current.PaymentMethod != PaymentMethod.Dinheiro || StoreService.SummarizeCash(state, open.Id).Expected >= current.Total;
    }
    private void RebuildSalesHistory(bool resetPage)
    {
        Guid? selectedId = SelectedSale?.Id;
        SalesFilter filter = appliedSalesFilter;
        IEnumerable<Sale> rows = Sales.Where(s =>
            (filter.From is null || s.Date.LocalDateTime.Date >= filter.From.Value)
            && (filter.Until is null || s.Date.LocalDateTime.Date <= filter.Until.Value)
            && (filter.Payment == "Todos" || s.PaymentLabel == filter.Payment)
            && (filter.Status == "Todas" || s.Canceled == (filter.Status == "Canceladas"))
            && Matches(filter.Search, s.NumberLabel, s.Number.ToString(CultureInfo.InvariantCulture), s.CustomerName, s.UserName));
        var textComparer = StringComparer.CurrentCultureIgnoreCase;
        IOrderedEnumerable<Sale> ordered = SalesSortMember switch
        {
            "Number" => SalesSortDescending ? rows.OrderByDescending(s => s.Number) : rows.OrderBy(s => s.Number),
            "CustomerName" => SalesSortDescending ? rows.OrderByDescending(s => s.CustomerName, textComparer) : rows.OrderBy(s => s.CustomerName, textComparer),
            "PaymentLabel" => SalesSortDescending ? rows.OrderByDescending(s => s.PaymentLabel, textComparer) : rows.OrderBy(s => s.PaymentLabel, textComparer),
            "Total" => SalesSortDescending ? rows.OrderByDescending(s => s.Total) : rows.OrderBy(s => s.Total),
            "UserName" => SalesSortDescending ? rows.OrderByDescending(s => s.UserName, textComparer) : rows.OrderBy(s => s.UserName, textComparer),
            "StatusLabel" => SalesSortDescending ? rows.OrderByDescending(s => s.StatusLabel, textComparer) : rows.OrderBy(s => s.StatusLabel, textComparer),
            _ => SalesSortDescending ? rows.OrderByDescending(s => s.Date) : rows.OrderBy(s => s.Date)
        };
        filteredSales = ordered.ThenByDescending(s => s.Number).ThenBy(s => s.Id).ToList();
        salesFilteredItems = filteredSales.AsReadOnly();
        salesPageNumber = resetPage ? 1 : Math.Clamp(salesPageNumber, 1, SalesPageCount);
        foreach (string property in new[] { nameof(SalesFilteredItems), nameof(SalesCompletedTotal), nameof(SalesCompletedCount), nameof(SalesCanceledCount), nameof(SalesCanceledTotal), nameof(SalesCustomerCount), nameof(SalesAppliedHint), nameof(SalesCanExport) }) Changed(property);
        UpdateSalesPage(selectedId);
    }
    private void UpdateSalesPage(Guid? selectedId)
    {
        SalesPageItems = filteredSales.Skip((SalesPageNumber - 1) * SalesPageSize).Take(SalesPageSize).ToList();
        Changed(nameof(SalesPageItems));
        SelectedSale = SalesPageItems.FirstOrDefault(s => s.Id == selectedId);
        Changed(nameof(SelectedSale));
        foreach (string property in new[] { nameof(SalesFilteredCount), nameof(SalesPageNumber), nameof(SalesPageCount), nameof(SalesRangeLabel), nameof(SalesPageLabel), nameof(SalesEmptyVisibility), nameof(SalesCanPrevious), nameof(SalesCanNext) }) Changed(property);
        SalesSelectionChanged();
    }
    private void SalesSelectionChanged()
    {
        Changed(nameof(SalesHasSelection));
        Changed(nameof(SalesCanCancelSelection));
        CommandManager.InvalidateRequerySuggested();
    }
}
