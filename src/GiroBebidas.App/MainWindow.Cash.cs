using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Win32;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public partial class MainWindow
{
    private CashSession CashRow(object sender)
    {
        if (Vm.Service.CurrentUser is null) throw new UnauthorizedAccessException("Entre novamente no sistema.");
        var selected = sender is FrameworkElement { DataContext: CashSession row } ? row : Require(Vm.SelectedCash, "um caixa");
        var cash = Vm.Service.Snapshot.CashSessions.FirstOrDefault(c => c.Id == selected.Id)
            ?? throw new InvalidOperationException("Este caixa não está mais disponível.");
        Vm.SelectedCash = Vm.CashSessions.FirstOrDefault(c => c.Id == cash.Id);
        if (sender is MenuItem item && ItemsControl.ItemsControlFromItemContainer(item) is ContextMenu menu) menu.IsOpen = false;
        return cash;
    }
    private void CashRowMenu_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var button = (Button)sender; _ = CashRow(sender);
            var menu = button.ContextMenu ?? throw new InvalidOperationException("Menu do caixa indisponível.");
            menu.PlacementTarget = button; menu.Placement = PlacementMode.Bottom;
            menu.HorizontalOffset = button.ActualWidth - menu.Width; menu.VerticalOffset = 4; menu.IsOpen = true;
        }
        catch (Exception ex) { Notify(ex.Message); }
    }
    private void CashDetails_Click(object sender, RoutedEventArgs e) => Guard(() => CreateCashDetailsDialog(CashRow(sender)).ShowDialog());
    internal CashDetailsWindow CreateCashDetailsDialog(CashSession selected)
    {
        if (Vm.Service.CurrentUser is null) throw new UnauthorizedAccessException("Entre novamente no sistema.");
        var state = Vm.Service.Snapshot;
        var cash = state.CashSessions.FirstOrDefault(c => c.Id == selected.Id) ?? throw new InvalidOperationException("Caixa não encontrado.");
        var totals = StoreService.SummarizeCash(state, cash.Id);
        return new CashDetailsWindow(this, cash, totals);
    }
    private void CashRowPrint_Click(object sender, RoutedEventArgs e) => Guard(() =>
    {
        var cash = CashRow(sender);
        if (cash.ClosedAt is null) throw new InvalidOperationException("Feche o caixa antes de imprimir o fechamento.");
        ReceiptPrinter.PrintCash(this, Vm.Service.Snapshot, cash);
    });
    private void CashExport_Click(object sender, RoutedEventArgs e) => Guard(() =>
    {
        var cash = CashRow(sender);
        var file = new SaveFileDialog { Title = "Exportar relatório do caixa", Filter = "Planilha CSV|*.csv", FileName = $"caixa-{cash.OpenedAt:yyyyMMdd-HHmm}.csv", DefaultExt = ".csv", AddExtension = true };
        if (file.ShowDialog(this) != true) return;
        File.WriteAllText(file.FileName, CreateCashReportCsv(cash), new UTF8Encoding(true));
        Notify("Relatório do caixa exportado.");
    });
    internal string CreateCashReportCsv(CashSession selected)
    {
        if (Vm.Service.CurrentUser is null) throw new UnauthorizedAccessException("Entre novamente no sistema.");
        var state = Vm.Service.Snapshot;
        var cash = state.CashSessions.FirstOrDefault(c => c.Id == selected.Id) ?? throw new InvalidOperationException("Caixa não encontrado.");
        var totals = StoreService.SummarizeCash(state, cash.Id);
        var output = new StringBuilder();
        static string Cell(string value)
        {
            // Keep names and notes as text when the CSV is opened in a spreadsheet.
            if (value.TrimStart().StartsWith('=') || value.TrimStart().StartsWith('+') || value.TrimStart().StartsWith('-') || value.TrimStart().StartsWith('@')) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        string Value(object value) => value switch
        {
            decimal amount => "\"" + amount.ToString("0.00", CultureInfo.GetCultureInfo("pt-BR")) + "\"",
            int count => count.ToString(CultureInfo.InvariantCulture),
            _ => Cell(value.ToString() ?? "")
        };
        void Row(params object[] cells) => output.AppendLine(string.Join(';', cells.Select(Value)));
        Row("RELATÓRIO DO CAIXA", state.Settings.Name);
        Row("Abertura", cash.OpenedAt.ToString("dd/MM/yyyy HH:mm")); Row("Responsável", cash.OpenedBy); Row("Situação", cash.StatusLabel);
        Row("Fundo de troco", totals.Opening); Row("Dinheiro recebido", totals.CashSales);
        Row("Pix", totals.Pix); Row("Débito", totals.Debit); Row("Crédito", totals.Credit);
        Row("Vendas em fiado", totals.CreditSales); Row("Suprimentos", totals.Supplies); Row("Sangrias", totals.Withdrawals);
        Row("Dinheiro esperado", totals.Expected); Row("Quantidade de vendas", totals.SaleCount); Row("Total de vendas", totals.TotalSales);
        if (cash.ClosedAt is { } closed)
        {
            Row("Fechamento", closed.ToString("dd/MM/yyyy HH:mm")); Row("Responsável pelo fechamento", cash.ClosedBy);
            Row("Dinheiro contado", cash.CountedCash); Row("Diferença", cash.Difference); Row("Observações", cash.ClosingNotes);
        }
        Row(""); Row("Data", "Tipo", "Valor", "Motivo", "Usuário");
        foreach (var move in state.CashMovements.Where(m => m.SessionId == cash.Id).OrderBy(m => m.Date))
            Row(move.Date.ToString("dd/MM/yyyy HH:mm"), move.KindLabel, move.Amount, move.Notes, move.UserName);
        return output.ToString();
    }
}
