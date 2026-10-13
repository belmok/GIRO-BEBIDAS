using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public partial class MainWindow
{
    private void RequireReportAdministrator()
    {
        if (!Vm.Service.IsAdmin) throw new UnauthorizedAccessException("Os resultados do depósito estão disponíveis para administradores.");
    }
    private ReportProductRow ResolveReportProduct(object sender)
    {
        RequireReportAdministrator();
        var selected = sender is FrameworkElement { DataContext: ReportProductRow row } ? row : Require(Vm.SelectedReportRow, "um produto no relatório");
        var current = Vm.ReportFilteredItems.FirstOrDefault(item => item.Id == selected.Id) ?? throw new InvalidOperationException("Este produto não está mais no resultado aplicado. Atualize o relatório.");
        Vm.SelectedReportRow = Vm.ReportPageItems.FirstOrDefault(item => item.Id == current.Id);
        if (sender is MenuItem item && ItemsControl.ItemsControlFromItemContainer(item) is ContextMenu menu) menu.IsOpen = false;
        return current;
    }
    internal void SelectReportProduct(object sender)
    {
        try { ResolveReportProduct(sender); }
        catch (Exception ex) { Notify(ex.Message); }
    }
    internal void ApplyReportDashboard()
    {
        try { RequireReportAdministrator(); Vm.ApplyReportFilters(); }
        catch (Exception ex) { Notify(ex.Message); }
    }
    internal void ClearReportDashboard()
    {
        try { RequireReportAdministrator(); Vm.ClearReportFilters(); }
        catch (Exception ex) { Notify(ex.Message); }
    }
    internal IEnumerable<string[]> ReportCsvRows()
    {
        RequireReportAdministrator();
        return new[] { new[] { "Produto", "Código", "Categoria", "Quantidade", "Receita líquida", "Custo", "Lucro bruto", "Margem" } }
            .Concat(Vm.ReportFilteredItems.Select(row => new[] { row.Name, row.Code, row.Category, row.Quantity.ToString("0.###"), row.Revenue.ToString("0.00"), row.Cost.ToString("0.00"), row.Profit.ToString("0.00"), row.Margin.ToString("P2") })).ToArray();
    }
    private void ExportProfit_Click(object sender, RoutedEventArgs e)
    {
        try { ExportCsv($"lucratividade-{DateTime.Today:yyyyMMdd}.csv", ReportCsvRows()); }
        catch (Exception ex) { Notify(ex.Message); }
    }
    internal void ExportReportDashboard() => ExportProfit_Click(this, new RoutedEventArgs());
    internal ReportProductDetailsWindow CreateReportDetailsDialog(ReportProductRow selected)
    {
        RequireReportAdministrator();
        var state = Vm.Service.Snapshot;
        var metrics = StoreService.ProfitReport(state, Vm.ReportAppliedFrom, Vm.ReportAppliedUntil).FirstOrDefault(row => row.ProductId == selected.Id)
            ?? throw new InvalidOperationException("Nenhuma venda concluída deste produto no período aplicado.");
        return new ReportProductDetailsWindow(this, new ReportProductRow(metrics, state.Products.FirstOrDefault(product => product.Id == selected.Id)), Vm.ReportAppliedHint);
    }
    internal ReportProductHistoryWindow CreateReportHistoryDialog(ReportProductRow selected)
    {
        RequireReportAdministrator();
        var state = Vm.Service.Snapshot;
        var metrics = StoreService.ProfitReport(state, Vm.ReportAppliedFrom, Vm.ReportAppliedUntil).FirstOrDefault(row => row.ProductId == selected.Id)
            ?? throw new InvalidOperationException("Nenhuma venda concluída deste produto no período aplicado.");
        var rows = state.Sales.Where(sale => !sale.Canceled && (Vm.ReportAppliedFrom is null || sale.Date >= Vm.ReportAppliedFrom)
            && (Vm.ReportAppliedUntil is null || sale.Date < Vm.ReportAppliedUntil) && sale.Lines.Any(line => line.ProductId == selected.Id))
            .OrderByDescending(sale => sale.Date).ThenByDescending(sale => sale.Number).Select(sale => new ReportProductSaleRow(sale, selected.Id)).ToList();
        return new ReportProductHistoryWindow(this, metrics.Name, rows, Vm.ReportAppliedHint, (owner, sale) =>
        {
            RequireReportAdministrator();
            var currentState = Vm.Service.Snapshot;
            var current = currentState.Sales.FirstOrDefault(item => item.Id == sale.Id) ?? throw new InvalidOperationException("Venda não encontrada.");
            new SaleDetailsWindow(owner, current, currentState, (printOwner, _, item) => PrintSaleNow(printOwner, item)).ShowDialog();
        });
    }
    internal void ShowReportDetails(object sender)
    {
        try { CreateReportDetailsDialog(ResolveReportProduct(sender)).ShowDialog(); }
        catch (Exception ex) { Notify(ex.Message); }
    }
    internal void ShowReportProductHistory(object sender)
    {
        try { CreateReportHistoryDialog(ResolveReportProduct(sender)).ShowDialog(); }
        catch (Exception ex) { Notify(ex.Message); }
    }
}

public sealed class ReportProductDetailsWindow : DetailWindow
{
    internal ReportProductRow RowForDisplay { get; }
    internal TextBlock RevenueLabel { get; }
    internal TextBlock CostLabel { get; }
    internal TextBlock ProfitLabel { get; }
    internal TextBlock MarginLabel { get; }
    public ReportProductDetailsWindow(Window owner, ReportProductRow row, string period)
        : base(owner, "Resultado do produto", period, "\uE9D9")
    {
        RowForDisplay = row;
        var product = new Grid();
        product.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
        product.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        product.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        product.Children.Add(new Border { Width = 92, Height = 108, Background = ColorBrush("#F0F7F3"), CornerRadius = new CornerRadius(10), Padding = new Thickness(8), Child = new Image { Source = row.Image, Stretch = Stretch.Uniform } });
        var name = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        name.Children.Add(Text(row.Name, 21, "#172B29", FontWeights.SemiBold)); name.Children.Add(Text($"Código {row.Code} • {row.Category}", 12, "#597164"));
        Grid.SetColumn(name, 2); product.Children.Add(name); AddSection("Produto no relatório", product);
        var results = new StackPanel();
        results.Children.Add(ValueRow("Quantidade vendida", row.Quantity.ToString("0.###")));
        RevenueLabel = Text(row.Revenue.ToString("C2"), 17, "#243D31", FontWeights.SemiBold);
        CostLabel = Text(row.Cost.ToString("C2"), 17, "#243D31", FontWeights.SemiBold);
        ProfitLabel = Text(row.Profit.ToString("C2"), 22, row.Profit < 0 ? "#B4483E" : "#187B52", FontWeights.SemiBold);
        MarginLabel = Text(row.Margin.ToString("P1"), 19, row.Profit < 0 ? "#B4483E" : "#187B52", FontWeights.SemiBold);
        results.Children.Add(ValueRow("Receita líquida", RevenueLabel)); results.Children.Add(ValueRow("Custo histórico", CostLabel));
        results.Children.Add(ValueRow("Lucro bruto", ProfitLabel)); results.Children.Add(ValueRow("Margem bruta", MarginLabel));
        AddSection("Resultado do período", results);
        AddSection("Como os valores são calculados", Text("A receita considera os descontos distribuídos entre os produtos. O custo é o registrado em cada venda; alterações posteriores no cadastro não recalculam o histórico. Vendas canceladas, taxas de entrega e despesas operacionais ficam fora deste lucro bruto. A margem é o lucro dividido pela receita líquida.", 13, "#415C4C"));
        BodyPanel.Children.Add(Text("Foto e categoria refletem o cadastro atual. O nome e os valores vêm das vendas registradas.", 12, "#597164"));
    }
    private static UIElement ValueRow(string label, string value) => ValueRow(label, Text(value, 15, "#243D31", FontWeights.SemiBold));
    private static UIElement ValueRow(string label, TextBlock amount)
    {
        var row = new Grid { Margin = new Thickness(0, 8, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var title = Text(label, 13, "#597164"); title.VerticalAlignment = VerticalAlignment.Center; row.Children.Add(title);
        amount.TextAlignment = TextAlignment.Right; Grid.SetColumn(amount, 1); row.Children.Add(amount); return row;
    }
}

public sealed class ReportProductSaleRow
{
    internal Sale Sale { get; }
    public Guid SaleId => Sale.Id;
    public string Number => Sale.NumberLabel;
    public DateTime Date => Sale.Date.LocalDateTime;
    public string CustomerName => Sale.CustomerName;
    public string Payment => Sale.PaymentLabel;
    public decimal Quantity { get; }
    public decimal Revenue { get; }
    public decimal Cost { get; }
    public decimal Profit => Revenue - Cost;
    public ReportProductSaleRow(Sale sale, Guid productId)
    {
        Sale = sale;
        var lines = sale.Lines.Where(line => line.ProductId == productId).ToList();
        Quantity = lines.Sum(line => line.Quantity); Revenue = lines.Sum(line => line.NetRevenue); Cost = lines.Sum(line => Money.Round(line.Quantity * line.UnitCost));
    }
}

public sealed class ReportProductHistoryWindow : DetailWindow
{
    internal IReadOnlyList<ReportProductSaleRow> SalesForDisplay { get; }
    internal DataGrid SalesGrid { get; }
    internal Button SaleDetailsButton { get; }
    internal TextBlock FeedbackLabel { get; }
    public ReportProductHistoryWindow(Window owner, string productName, List<ReportProductSaleRow> rows, string period, Action<Window, Sale> showDetails)
        : base(owner, "Histórico do produto", period, "\uE81C")
    {
        SalesForDisplay = rows.AsReadOnly();
        BodyPanel.Children.Add(Text(productName, 19, "#172B29", FontWeights.SemiBold));
        BodyPanel.Children.Add(Text("Vendas concluídas do produto no período selecionado. Valores de receita, custo e lucro são deste produto dentro de cada venda.", 12, "#597164"));
        SalesGrid = new DataGrid { Name = nameof(SalesGrid), ItemsSource = rows, Height = 288, RowHeight = 47, ColumnHeaderHeight = 38, Margin = new Thickness(0, 16, 0, 14), CanUserSortColumns = true, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        ControlledScrolling.SetIsEnabled(SalesGrid, true);
        SalesGrid.Columns.Add(new DataGridTextColumn { Header = "Venda", Binding = new System.Windows.Data.Binding(nameof(ReportProductSaleRow.Number)), Width = new DataGridLength(90) });
        SalesGrid.Columns.Add(new DataGridTextColumn { Header = "Data", Binding = new System.Windows.Data.Binding(nameof(ReportProductSaleRow.Date)) { StringFormat = "dd/MM/yyyy HH:mm" }, Width = new DataGridLength(144) });
        SalesGrid.Columns.Add(new DataGridTextColumn { Header = "Quantidade", Binding = new System.Windows.Data.Binding(nameof(ReportProductSaleRow.Quantity)) { StringFormat = "0.###" }, Width = new DataGridLength(108) });
        SalesGrid.Columns.Add(new DataGridTextColumn { Header = "Receita", Binding = new System.Windows.Data.Binding(nameof(ReportProductSaleRow.Revenue)) { StringFormat = "C2" }, Width = new DataGridLength(124) });
        SalesGrid.Columns.Add(new DataGridTextColumn { Header = "Lucro", Binding = new System.Windows.Data.Binding(nameof(ReportProductSaleRow.Profit)) { StringFormat = "C2" }, Width = new DataGridLength(124) });
        BodyPanel.Children.Add(SalesGrid);
        var summary = new StackPanel(); summary.Children.Add(Text($"{rows.Count} venda{(rows.Count == 1 ? "" : "s")} • receita {rows.Sum(row => row.Revenue):C2} • custo {rows.Sum(row => row.Cost):C2}", 13, "#365447"));
        AddSection("Total deste produto no período", summary);
        SaleDetailsButton = new Button { Content = "Ver detalhes da venda", MinHeight = 42, IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(14, 9, 14, 9) };
        SalesGrid.SelectionChanged += (_, _) => SaleDetailsButton.IsEnabled = SalesGrid.SelectedItem is ReportProductSaleRow;
        FeedbackLabel = Text("", 12, "#B4483E"); FeedbackLabel.Visibility = Visibility.Collapsed; FeedbackLabel.Margin = new Thickness(0, 9, 0, 0);
        SaleDetailsButton.Click += (_, _) =>
        {
            try { if (SalesGrid.SelectedItem is ReportProductSaleRow selected) showDetails(this, selected.Sale); }
            catch (Exception ex) { FeedbackLabel.Text = ex.Message; FeedbackLabel.Visibility = Visibility.Visible; }
        };
        BodyPanel.Children.Add(SaleDetailsButton); BodyPanel.Children.Add(FeedbackLabel);
    }
}
