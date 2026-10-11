using System.Text;

namespace GiroBebidas.Core;

public static class ReceiptText
{
    private static StringBuilder Header(StoreSettings settings, string title)
    {
        var text = new StringBuilder(); text.AppendLine(settings.Name.ToUpperInvariant());
        if (settings.Document.Length > 0) text.AppendLine(settings.Document);
        if (settings.Address.Length > 0) text.AppendLine(settings.Address);
        if (settings.Phone.Length > 0) text.AppendLine(settings.Phone);
        text.AppendLine(new string('-', 32)); text.AppendLine(title); text.AppendLine("DOCUMENTO NÃO FISCAL"); text.AppendLine(new string('-', 32)); return text;
    }
    private static void Lines(StringBuilder text, IEnumerable<SaleLine> lines)
    {
        foreach (var line in lines) { text.AppendLine(line.ProductName); text.AppendLine($"{line.Quantity:0.###} {line.Unit} x {line.UnitPrice:C2} = {line.GrossTotal:C2}"); }
    }
    public static string Sale(StoreSettings settings, Sale sale)
    {
        var text = Header(settings, $"VENDA {sale.NumberLabel}"); text.AppendLine($"{sale.Date:dd/MM/yyyy HH:mm}");
        text.AppendLine($"Operador: {sale.UserName}"); text.AppendLine($"Cliente: {sale.CustomerName}");
        if (sale.CustomerPhone.Length > 0) text.AppendLine($"Telefone: {sale.CustomerPhone}");
        if (sale.DeliveryAddress.Length > 0) { text.AppendLine("ENTREGA:"); text.AppendLine(sale.DeliveryAddress); }
        if (sale.Canceled) { text.AppendLine("*** VENDA CANCELADA ***"); text.AppendLine(sale.CancelReason); }
        text.AppendLine(new string('-', 32)); Lines(text, sale.Lines); text.AppendLine(new string('-', 32));
        text.AppendLine($"Subtotal: {sale.Subtotal:C2}"); text.AppendLine($"Desconto: {sale.Discount:C2}"); text.AppendLine($"Entrega: {sale.DeliveryFee:C2}"); text.AppendLine($"TOTAL: {sale.Total:C2}"); text.AppendLine($"Pagamento: {sale.PaymentLabel}");
        if (sale.PaymentMethod == PaymentMethod.Dinheiro) { text.AppendLine($"Recebido: {sale.Tendered:C2}"); text.AppendLine($"Troco: {sale.Change:C2}"); }
        if (sale.PaymentMethod == PaymentMethod.Fiado) text.AppendLine("VALOR A RECEBER DO CLIENTE");
        if (sale.Notes.Length > 0) text.AppendLine(sale.Notes);
        text.AppendLine(new string('-', 32)); text.AppendLine(settings.ReceiptFooter); return text.ToString();
    }
    public static string Order(StoreSettings settings, Order order)
    {
        var text = Header(settings, $"PEDIDO {order.NumberLabel}"); text.AppendLine($"{order.Date:dd/MM/yyyy HH:mm}"); text.AppendLine($"Cliente: {order.CustomerName}"); text.AppendLine(order.Phone);
        text.AppendLine($"{order.TypeLabel} • {order.StatusLabel}"); if (order.Delivery) text.AppendLine(order.Address);
        text.AppendLine(new string('-', 32)); Lines(text, order.Lines); text.AppendLine($"Desconto: {order.Discount:C2}"); text.AppendLine($"Entrega: {order.DeliveryFee:C2}"); text.AppendLine($"TOTAL: {order.Total:C2}"); text.AppendLine("PEDIDO • NÃO COMPROVA PAGAMENTO"); text.AppendLine(order.Notes); return text.ToString();
    }
    public static string Cash(StoreState state, CashSession session)
    {
        var totals = StoreService.SummarizeCash(state, session.Id); var text = Header(state.Settings, "RESUMO DO CAIXA");
        text.AppendLine($"Abertura: {session.OpenedAt:dd/MM/yyyy HH:mm}"); text.AppendLine($"Responsável: {session.OpenedBy}");
        if (session.ClosedAt is not null) text.AppendLine($"Fechado: {session.ClosedAt:dd/MM/yyyy HH:mm} por {session.ClosedBy}"); else text.AppendLine("CAIXA AINDA ABERTO");
        text.AppendLine($"Fundo de troco: {totals.Opening:C2}"); text.AppendLine($"Dinheiro recebido: {totals.CashSales:C2}"); text.AppendLine($"Pix: {totals.Pix:C2}"); text.AppendLine($"Débito: {totals.Debit:C2}"); text.AppendLine($"Crédito: {totals.Credit:C2}"); text.AppendLine($"Vendas em fiado: {totals.CreditSales:C2}");
        text.AppendLine($"Suprimentos: {totals.Supplies:C2}"); text.AppendLine($"Sangrias: {totals.Withdrawals:C2}"); text.AppendLine($"DINHEIRO ESPERADO: {totals.Expected:C2}");
        if (session.ClosedAt is not null) { text.AppendLine($"DINHEIRO CONTADO: {session.CountedCash:C2}"); text.AppendLine($"DIFERENÇA: {session.Difference:C2}"); }
        text.AppendLine($"Vendas: {totals.SaleCount} • {totals.TotalSales:C2}"); text.AppendLine(session.ClosingNotes); return text.ToString();
    }
}
