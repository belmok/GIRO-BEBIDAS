using System.Windows.Input;
using System.Windows.Media;
using System.Windows;
using System.Windows.Data;
using System.Globalization;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public sealed class ReportProfitToneConverter : IValueConverter
{
    private static readonly Brush Negative = Frozen("#B4483E"), Positive = Frozen("#187B52"), NegativeBackground = Frozen("#FFE8E3"), PositiveBackground = Frozen("#E3F5E7");
    private static Brush Frozen(string color) { var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); brush.Freeze(); return brush; }
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not decimal profit) return DependencyProperty.UnsetValue;
        return string.Equals(parameter?.ToString(), "background", StringComparison.OrdinalIgnoreCase)
            ? profit < 0 ? NegativeBackground : PositiveBackground : profit < 0 ? Negative : Positive;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class ReportProductRow(ProfitRow metrics, Product? product)
{
    public ProfitRow Metrics { get; } = metrics;
    public Guid Id => Metrics.ProductId;
    public string Name => Metrics.Name;
    public string Code => product?.Code ?? "Sem cadastro";
    public string Category => product?.Category ?? "Sem categoria";
    public string ProductHint => $"{Name} • código {Code}. Foto e categoria do cadastro atual; valores registrados nas vendas.";
    public decimal Quantity => Metrics.Quantity;
    public decimal Revenue => Metrics.Revenue;
    public decimal Cost => Metrics.Cost;
    public decimal Profit => Metrics.Profit;
    public decimal Margin => Metrics.Margin;
    public string ProfitTone => Profit < 0 ? "Negative" : "Positive";
    private ImageSource? image;
    public ImageSource Image => image ??= PhotoTools.Decode(product?.PhotoBase64 ?? "") ?? BeverageArt.Create(product ?? new Product { Id = Id, Name = Name });
}

public sealed partial class MainViewModel
{
    private string reportSearch = "", reportCategory = "Todas as categorias", appliedReportSearch = "", appliedReportCategory = "Todas as categorias";
    private string reportSortMember = "Profit";
    private bool reportSortDescending = true;
    private int reportPageSize = 10, reportPageNumber = 1;
    private ReportProductRow? selectedReportRow;
    private List<ReportProductRow> filteredReportRows = [];
    private IReadOnlyList<ReportProductRow> reportFilteredItems = Array.Empty<ReportProductRow>();
    private DateTimeOffset? reportAppliedFrom, reportAppliedUntil;
    private string reportAppliedPeriod = "Últimos 30 dias";
    public List<string> ReportPeriodOptions { get; } = ["Hoje", "Últimos 7 dias", "Últimos 30 dias", "Todo o período"];
    public List<string> ReportCategories { get; private set; } = ["Todas as categorias"];
    public List<int> ReportPageSizeOptions { get; } = [10, 20, 50];
    public string ReportSearch { get => reportSearch; set => Set(ref reportSearch, value ?? ""); }
    public string ReportCategory { get => reportCategory; set => Set(ref reportCategory, value ?? "Todas as categorias"); }
    public List<ReportProductRow> ReportPageItems { get; private set; } = [];
    public IReadOnlyList<ReportProductRow> ReportFilteredItems => reportFilteredItems;
    public int ReportFilteredCount => filteredReportRows.Count;
    public int ReportPageCount => ReportFilteredCount == 0 ? 1 : 1 + (ReportFilteredCount - 1) / ReportPageSize;
    public string ReportPageLabel => $"Página {ReportPageNumber} de {ReportPageCount}";
    public string ReportRangeLabel => ReportFilteredCount == 0 ? "Nenhum produto encontrado" : $"Mostrando {(ReportPageNumber - 1) * ReportPageSize + 1} a {Math.Min((long)ReportPageNumber * ReportPageSize, ReportFilteredCount)} de {ReportFilteredCount} produto{(ReportFilteredCount == 1 ? "" : "s")}";
    public string ReportEmptyVisibility => ReportFilteredCount == 0 ? "Visible" : "Collapsed";
    public bool ReportCanPrevious => ReportPageNumber > 1;
    public bool ReportCanNext => ReportPageNumber < ReportPageCount;
    public bool ReportHasSelection => IsAdmin && SelectedReportRow is not null;
    public bool ReportCanExport => IsAdmin && ReportFilteredCount > 0;
    public string ReportSortMember => reportSortMember;
    public bool ReportSortDescending => reportSortDescending;
    public string ReportAppliedHint => reportAppliedPeriod + " • " + appliedReportCategory + (appliedReportSearch.Length == 0 ? "" : $" • Busca: {appliedReportSearch}");
    internal DateTimeOffset? ReportAppliedFrom => reportAppliedFrom;
    internal DateTimeOffset? ReportAppliedUntil => reportAppliedUntil;
    public ReportProductRow? SelectedReportRow
    {
        get => selectedReportRow;
        set { if (Set(ref selectedReportRow, value)) { Changed(nameof(ReportHasSelection)); CommandManager.InvalidateRequerySuggested(); } }
    }
    public int ReportPageSize
    {
        get => reportPageSize;
        set
        {
            Guid? selectedId = SelectedReportRow?.Id;
            if (!Set(ref reportPageSize, ReportPageSizeOptions.Contains(value) ? value : 10)) return;
            reportPageNumber = 1; UpdateReportPage(selectedId);
        }
    }
    public int ReportPageNumber
    {
        get => reportPageNumber;
        set
        {
            Guid? selectedId = SelectedReportRow?.Id;
            if (Set(ref reportPageNumber, Math.Clamp(value, 1, ReportPageCount))) UpdateReportPage(selectedId);
        }
    }
    public ICommand ReportPreviousPageCommand { get; private set; } = null!;
    public ICommand ReportNextPageCommand { get; private set; } = null!;
    private void InitializeReportDashboard()
    {
        ReportPreviousPageCommand = new RelayCommand(_ => { if (ReportCanPrevious) ReportPageNumber--; }, _ => ReportCanPrevious);
        ReportNextPageCommand = new RelayCommand(_ => { if (ReportCanNext) ReportPageNumber++; }, _ => ReportCanNext);
    }
    public void ApplyReportFilters()
    {
        if (!ReportCategories.Contains(ReportCategory, StringComparer.CurrentCultureIgnoreCase))
            throw new InvalidOperationException("Escolha uma categoria disponível no relatório.");
        appliedReportSearch = ReportSearch.Trim(); appliedReportCategory = ReportCategory;
        RefreshReportDashboard(true);
    }
    public void ClearReportFilters()
    {
        ReportSearch = ""; ReportCategory = "Todas as categorias";
        appliedReportSearch = ""; appliedReportCategory = "Todas as categorias";
        reportSortMember = "Profit"; reportSortDescending = true;
        Changed(nameof(ReportSortMember)); Changed(nameof(ReportSortDescending));
        RefreshReportDashboard(true);
    }
    public void SetReportSort(string member, bool descending)
    {
        if (!new[] { "Name", "Quantity", "Revenue", "Cost", "Profit", "Margin" }.Contains(member, StringComparer.Ordinal))
            throw new InvalidOperationException("A coluna escolhida não permite ordenar o relatório.");
        reportSortMember = member; reportSortDescending = descending;
        Changed(nameof(ReportSortMember)); Changed(nameof(ReportSortDescending));
        RefreshReportDashboard(true);
    }
    public void RefreshReportDashboard(bool resetPage = false)
    {
        Guid? selectedId = SelectedReportRow?.Id;
        DateTime today = DateTime.Today;
        string period = ReportPeriodOptions.Contains(ReportPeriod) ? ReportPeriod : "Últimos 30 dias";
        bool periodChanged = period != reportAppliedPeriod;
        reportAppliedPeriod = period;
        reportAppliedFrom = reportAppliedPeriod switch
        {
            "Hoje" => new DateTimeOffset(today),
            "Últimos 7 dias" => new DateTimeOffset(today.AddDays(-6)),
            "Últimos 30 dias" => new DateTimeOffset(today.AddDays(-29)),
            _ => null
        };
        reportAppliedUntil = reportAppliedFrom is null ? null : new DateTimeOffset(today.AddDays(1));
        var metrics = StoreService.ProfitReport(Snapshot, reportAppliedFrom, reportAppliedUntil);
        var products = Snapshot.Products.ToDictionary(p => p.Id);
        var allRows = metrics.Select(row => new ReportProductRow(row, products.GetValueOrDefault(row.ProductId))).ToList();
        ReportCategories = ["Todas as categorias", .. allRows.Select(row => row.Category).Distinct(StringComparer.CurrentCultureIgnoreCase).OrderBy(category => category, StringComparer.CurrentCultureIgnoreCase)];
        Changed(nameof(ReportCategories));
        if (!ReportCategories.Contains(ReportCategory, StringComparer.CurrentCultureIgnoreCase)) ReportCategory = "Todas as categorias";
        // An applied category that disappears remains an empty result until the user clears
        // or applies another category; refreshing must not silently broaden a saved filter.
        var rows = allRows.Where(row => (appliedReportCategory == "Todas as categorias" || row.Category.Equals(appliedReportCategory, StringComparison.CurrentCultureIgnoreCase))
            && Matches(appliedReportSearch, row.Name, row.Code, row.Category));
        var comparer = StringComparer.CurrentCultureIgnoreCase;
        IOrderedEnumerable<ReportProductRow> sorted = ReportSortMember switch
        {
            "Name" => ReportSortDescending ? rows.OrderByDescending(row => row.Name, comparer) : rows.OrderBy(row => row.Name, comparer),
            "Quantity" => ReportSortDescending ? rows.OrderByDescending(row => row.Quantity) : rows.OrderBy(row => row.Quantity),
            "Revenue" => ReportSortDescending ? rows.OrderByDescending(row => row.Revenue) : rows.OrderBy(row => row.Revenue),
            "Cost" => ReportSortDescending ? rows.OrderByDescending(row => row.Cost) : rows.OrderBy(row => row.Cost),
            "Margin" => ReportSortDescending ? rows.OrderByDescending(row => row.Margin) : rows.OrderBy(row => row.Margin),
            _ => ReportSortDescending ? rows.OrderByDescending(row => row.Profit) : rows.OrderBy(row => row.Profit)
        };
        filteredReportRows = sorted.ThenBy(row => row.Name, comparer).ThenBy(row => row.Id).ToList();
        reportFilteredItems = filteredReportRows.AsReadOnly();
        ProfitRows = filteredReportRows.Select(row => row.Metrics).ToList();
        reportPageNumber = resetPage || periodChanged ? 1 : Math.Clamp(reportPageNumber, 1, ReportPageCount);
        foreach (string name in new[] { nameof(ProfitRows), nameof(ReportRevenue), nameof(ReportCost), nameof(ReportProfit), nameof(ReportMargin), nameof(ReportFilteredItems), nameof(ReportCanExport), nameof(ReportAppliedHint) }) Changed(name);
        UpdateReportPage(selectedId);
    }
    private void UpdateReportPage(Guid? selectedId)
    {
        ReportPageItems = filteredReportRows.Skip((ReportPageNumber - 1) * ReportPageSize).Take(ReportPageSize).ToList();
        Changed(nameof(ReportPageItems)); SelectedReportRow = ReportPageItems.FirstOrDefault(row => row.Id == selectedId);
        Changed(nameof(SelectedReportRow)); Changed(nameof(ReportHasSelection));
        foreach (string name in new[] { nameof(ReportFilteredCount), nameof(ReportPageNumber), nameof(ReportPageCount), nameof(ReportPageLabel), nameof(ReportRangeLabel), nameof(ReportCanPrevious), nameof(ReportCanNext), nameof(ReportEmptyVisibility) }) Changed(name);
        CommandManager.InvalidateRequerySuggested();
    }
}
