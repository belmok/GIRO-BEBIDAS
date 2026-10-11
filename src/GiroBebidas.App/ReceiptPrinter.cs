using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public static class ReceiptPrinter
{
    public static List<string> InstalledPrinters()
    {
        using var server = new LocalPrintServer();
        using var queues = server.GetPrintQueues([EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections]);
        return queues.Select(q => q.FullName).OrderBy(q => q).ToList();
    }
    public static void PrintSale(Window owner, StoreState state, Sale sale, bool dialog = true) => Print(owner, state, ReceiptText.Sale(state.Settings, sale), $"Venda {sale.NumberLabel}", dialog);
    public static void PrintOrder(Window owner, StoreState state, Order order) => Print(owner, state, ReceiptText.Order(state.Settings, order), $"Pedido {order.NumberLabel}", true);
    public static void PrintCash(Window owner, StoreState state, CashSession cash) => Print(owner, state, ReceiptText.Cash(state, cash), "Resumo do caixa", true);
    private static void Print(Window owner, StoreState state, string text, string title, bool showDialog)
    {
        var profile = state.Printers.FirstOrDefault(p => p.Id == state.Settings.DefaultPrinterId);
        var printer = new PrintDialog();
        using var server = new LocalPrintServer();
        using var queues = server.GetPrintQueues([EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections]);
        if (profile is not null)
        {
            var queue = queues.FirstOrDefault(q => q.FullName == profile.QueueName || q.Name == profile.QueueName);
            if (queue is null && !showDialog) throw new InvalidOperationException("A impressora padrão não está disponível.");
            if (queue is not null) { printer.PrintQueue = queue; printer.PrintTicket = queue.UserPrintTicket ?? queue.DefaultPrintTicket; }
        }
        else if (!showDialog) throw new InvalidOperationException("Cadastre uma impressora padrão em Equipamentos.");
        if (showDialog && printer.ShowDialog() != true) return;
        double width = profile?.Paper switch { "58 mm" => 219, "A4" => printer.PrintableAreaWidth, _ => 302 };
        var document = new FlowDocument { FontFamily = new FontFamily("Consolas"), FontSize = profile?.Paper == "58 mm" ? 9 : 11,
            PageWidth = Math.Min(width, printer.PrintableAreaWidth), PageHeight = printer.PrintableAreaHeight, PagePadding = new Thickness(10), ColumnWidth = 10000 };
        foreach (string line in text.Split('\n')) document.Blocks.Add(new Paragraph(new Run(line.TrimEnd('\r'))) { Margin = new Thickness(0), LineHeight = 16 });
        printer.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, $"Giro Bebidas • {title}");
    }
}
