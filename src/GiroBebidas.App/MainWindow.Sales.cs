using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public partial class MainWindow
{
    internal Action<Window, StoreState, Sale> SalePrinter { get; set; } = (owner, state, sale) => ReceiptPrinter.PrintSale(owner, state, sale);

    private Sale SaleRow(object sender)
    {
        if (Vm.Service.CurrentUser is null) throw new UnauthorizedAccessException("Entre novamente no sistema.");
        var selected = sender is FrameworkElement { DataContext: Sale row } ? row : Require(Vm.SelectedSale, "uma venda");
        var sale = Vm.Service.Snapshot.Sales.FirstOrDefault(s => s.Id == selected.Id)
            ?? throw new InvalidOperationException("Esta venda não está mais disponível.");
        Vm.SelectedSale = Vm.SalesPageItems.FirstOrDefault(s => s.Id == sale.Id);
        if (sender is MenuItem item && ItemsControl.ItemsControlFromItemContainer(item) is ContextMenu menu) menu.IsOpen = false;
        return sale;
    }
    private void SaleRowMenu_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var sale = SaleRow(sender); var button = (Button)sender;
            var menu = button.ContextMenu ?? throw new InvalidOperationException("Menu da venda indisponível.");
            menu.PlacementTarget = button; menu.Placement = PlacementMode.Bottom;
            menu.HorizontalOffset = button.ActualWidth - menu.Width; menu.VerticalOffset = 4;
            menu.Items.OfType<MenuItem>().Single(item => Equals(item.Header, "Cancelar venda")).IsEnabled = Vm.CanCancelSale(sale);
            menu.IsOpen = true;
        }
        catch (Exception ex) { Notify(ex.Message); }
    }
    internal SaleDetailsWindow CreateSaleDetailsDialog(Sale selected)
    {
        if (Vm.Service.CurrentUser is null) throw new UnauthorizedAccessException("Entre novamente no sistema.");
        var state = Vm.Service.Snapshot;
        var sale = state.Sales.FirstOrDefault(s => s.Id == selected.Id) ?? throw new InvalidOperationException("Venda não encontrada.");
        return new SaleDetailsWindow(this, sale, state, (owner, _, item) => PrintSaleNow(owner, item));
    }
    private void PrintSaleNow(Window owner, Sale selected)
    {
        if (Vm.Service.CurrentUser is null) throw new UnauthorizedAccessException("Entre novamente no sistema.");
        var state = Vm.Service.Snapshot;
        var sale = state.Sales.FirstOrDefault(s => s.Id == selected.Id) ?? throw new InvalidOperationException("Venda não encontrada.");
        SalePrinter(owner, state, sale);
    }
    internal FormDialog CreateSaleCancelDialog(Sale selected)
    {
        if (!Vm.Service.IsAdmin) throw new UnauthorizedAccessException("Esta ação exige um administrador.");
        var sale = Vm.Service.Snapshot.Sales.FirstOrDefault(s => s.Id == selected.Id) ?? throw new InvalidOperationException("Venda não encontrada.");
        if (!Vm.CanCancelSale(sale)) throw new InvalidOperationException("Esta venda não pode ser cancelada. Confira o caixa de origem, o saldo para estorno e os recebimentos de fiado.");
        var dialog = new FormDialog(this, $"Cancelar venda {sale.NumberLabel}", "Os produtos voltarão ao estoque e o caixa será atualizado. A venda continuará no histórico.", "Confirmar cancelamento");
        dialog.Summary("Venda selecionada", [("Cliente", sale.CustomerName), ("Pagamento", sale.PaymentLabel), ("Total", sale.Total.ToString("C2"))]);
        dialog.Text("reason", "Motivo do cancelamento");
        dialog.Submit = form => Vm.Service.CancelSale(sale.Id, form.Value("reason"));
        return dialog;
    }
    private void SaleDetails_Click(object sender, RoutedEventArgs e)
    {
        try { CreateSaleDetailsDialog(SaleRow(sender)).ShowDialog(); }
        catch (Exception ex) { Notify(ex.Message); }
    }
    private void PrintSale_Click(object sender, RoutedEventArgs e)
    {
        try { PrintSaleNow(this, SaleRow(sender)); }
        catch (Exception ex) { Notify(ex.Message); }
    }
    private void CancelSale_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (CreateSaleCancelDialog(SaleRow(sender)).ShowDialog() != true) return;
            Vm.Refresh(); Notify("Venda cancelada; estoque e caixa atualizados.");
        }
        catch (Exception ex) { Notify(ex.Message); }
    }
    private void ApplySalesFilters_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            static DateTime? ReadDate(DatePicker picker, string name)
            {
                if (string.IsNullOrWhiteSpace(picker.Text)) return null;
                if (DateTime.TryParse(picker.Text, CultureInfo.CurrentCulture, DateTimeStyles.None, out var value)) return value.Date;
                throw new InvalidOperationException($"Informe uma {name} válida.");
            }
            var from = ReadDate(SalesFromPicker, "data inicial"); var until = ReadDate(SalesUntilPicker, "data final");
            Vm.SalesFrom = from; Vm.SalesUntil = until; Vm.ApplySalesFilters(); UpdateSalesSortHeaders();
        }
        catch (Exception ex) { Notify(ex.Message); }
    }
    private void ClearSalesFilters_Click(object sender, RoutedEventArgs e)
    {
        Vm.ClearSalesFilters(); SalesFromPicker.Text = SalesUntilPicker.Text = ""; UpdateSalesSortHeaders();
    }
    private void SalesGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        Vm.SetSalesSort(e.Column.SortMemberPath, Vm.SalesSortMember == e.Column.SortMemberPath ? !Vm.SalesSortDescending : false);
        UpdateSalesSortHeaders();
    }
    private void UpdateSalesSortHeaders()
    {
        foreach (var column in SalesGrid.Columns)
            column.SortDirection = column.SortMemberPath == Vm.SalesSortMember
                ? Vm.SalesSortDescending ? ListSortDirection.Descending : ListSortDirection.Ascending : null;
    }
    internal IEnumerable<string[]> SalesCsvRows()
    {
        if (Vm.Service.CurrentUser is null) throw new UnauthorizedAccessException("Entre novamente no sistema.");
        return new[] { new[] { "Venda", "Data", "Cliente", "Pagamento", "Total", "Vendedor", "Situação" } }
            .Concat(Vm.SalesFilteredItems.Select(s => new[] { s.NumberLabel, s.Date.LocalDateTime.ToString("dd/MM/yyyy HH:mm"), s.CustomerName, s.PaymentLabel, s.Total.ToString("0.00"), s.UserName, s.StatusLabel })).ToArray();
    }
    private void ExportSales_Click(object sender, RoutedEventArgs e)
    {
        try { ExportCsv($"vendas-{DateTime.Today:yyyyMMdd}.csv", SalesCsvRows()); }
        catch (Exception ex) { Notify(ex.Message); }
    }
}
