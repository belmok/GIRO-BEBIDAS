using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public sealed class SaleDetailsWindow : DetailWindow
{
    internal Sale SaleForDisplay { get; }
    internal TextBlock CustomerNameLabel { get; }
    internal TextBlock AddressLabel { get; }
    internal TextBlock TotalLabel { get; }
    internal TextBlock StatusLabel { get; }
    internal TextBlock PaymentLabel { get; }
    internal TextBlock? CancelReasonLabel { get; }
    internal TextBlock PrintFeedbackLabel { get; }
    internal StackPanel ItemsPanel { get; }
    internal Button PrintButton { get; }

    public SaleDetailsWindow(Window owner, Sale sale, StoreState state, Action<Window, StoreState, Sale> print)
        : base(owner, $"Venda {sale.NumberLabel}", $"{sale.Date.LocalDateTime:dd/MM/yyyy HH:mm} • {sale.UserName}", "\uE9D9")
    {
        ArgumentNullException.ThrowIfNull(sale);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(print);
        SaleForDisplay = sale;
        StatusLabel = Text(sale.StatusLabel, 12, sale.Canceled ? "#B4483E" : "#187B52", FontWeights.SemiBold);
        BodyPanel.Children.Add(new Border
        {
            Background = ColorBrush(sale.Canceled ? "#FFF1EE" : "#E3F5E7"), CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 7, 12, 7), HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 14), Child = StatusLabel
        });

        var customer = new StackPanel();
        CustomerNameLabel = Text(sale.CustomerName, 17, "#172B29", FontWeights.SemiBold);
        customer.Children.Add(CustomerNameLabel);
        customer.Children.Add(Muted(sale.CustomerPhone.Length > 0 ? sale.CustomerPhone : "Telefone não informado", new Thickness(0, 5, 0, 10)));
        AddressLabel = Text(sale.DeliveryAddress.Length > 0 ? sale.DeliveryAddress : "Atendimento no balcão", 13, "#365447");
        customer.Children.Add(AddressLabel);
        AddSection(sale.DeliveryAddress.Length > 0 ? "Cliente e entrega" : "Cliente e atendimento", customer);

        ItemsPanel = new StackPanel();
        foreach (var line in sale.Lines)
        {
            var row = new Grid { Margin = new Thickness(0, 5, 0, 5) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MaxWidth = 140 });
            var product = state.Products.FirstOrDefault(p => p.Id == line.ProductId);
            if (product is not null)
            {
                row.Children.Add(new Border
                {
                    Background = ColorBrush("#F2F7F3"), CornerRadius = new CornerRadius(9), Padding = new Thickness(5),
                    Width = 48, Height = 54, VerticalAlignment = VerticalAlignment.Center,
                    Child = new Image { Source = PhotoTools.Decode(product.PhotoBase64) ?? BeverageArt.Create(product), Stretch = Stretch.Uniform }
                });
            }
            var description = new StackPanel { Margin = new Thickness(6, 4, 8, 4), VerticalAlignment = VerticalAlignment.Center };
            description.Children.Add(Text(line.ProductName, 13, "#243D31", FontWeights.SemiBold));
            description.Children.Add(Muted($"{line.Quantity:0.###} {line.Unit} × {line.UnitPrice:C2}", new Thickness(0, 5, 0, 0)));
            Grid.SetColumn(description, 1); row.Children.Add(description);
            var amount = Text(line.GrossTotal.ToString("C2"), 14, "#172B29", FontWeights.SemiBold);
            amount.TextAlignment = TextAlignment.Right; amount.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(amount, 2); row.Children.Add(amount);
            ItemsPanel.Children.Add(new Border
            {
                BorderBrush = ColorBrush("#E7EFE9"), BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 0, 0, 8), Child = row
            });
        }
        AddSection($"Produtos da venda • {sale.Lines.Count}", ItemsPanel);

        var payment = new StackPanel();
        PaymentLabel = Text(sale.PaymentLabel, 16, "#187B52", FontWeights.SemiBold);
        payment.Children.Add(PaymentLabel);
        if (sale.PaymentMethod == PaymentMethod.Dinheiro)
        {
            payment.Children.Add(MoneyRow("Recebido em dinheiro", sale.Tendered));
            payment.Children.Add(MoneyRow("Troco", sale.Change));
        }
        else if (sale.PaymentMethod == PaymentMethod.Fiado)
        {
            payment.Children.Add(MoneyRow("Saldo a receber", StoreService.Outstanding(state, sale)));
        }
        AddSection("Pagamento", payment);

        var totals = new StackPanel();
        totals.Children.Add(MoneyRow("Subtotal", sale.Subtotal));
        totals.Children.Add(MoneyRow("Desconto", sale.Discount));
        totals.Children.Add(MoneyRow("Entrega", sale.DeliveryFee));
        TotalLabel = Text(sale.Total.ToString("C2"), 25, "#187B52", FontWeights.SemiBold);
        TotalLabel.TextAlignment = TextAlignment.Right;
        var total = new Grid();
        total.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        total.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var totalTitle = Text("Total da venda", 14, "#24503A", FontWeights.SemiBold);
        totalTitle.VerticalAlignment = VerticalAlignment.Center;
        total.Children.Add(totalTitle); Grid.SetColumn(TotalLabel, 1); total.Children.Add(TotalLabel);
        totals.Children.Add(new Border
        {
            Background = ColorBrush("#EAF6EE"), CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 13, 14, 13),
            Margin = new Thickness(0, 12, 0, 0), Child = total
        });
        AddSection("Resumo da venda", totals);

        var record = new StackPanel();
        record.Children.Add(TextRow("Data", sale.Date.LocalDateTime.ToString("dd/MM/yyyy HH:mm")));
        record.Children.Add(TextRow("Vendedor", sale.UserName));
        if (sale.OrderId is { } orderId)
        {
            var order = state.Orders.FirstOrDefault(o => o.Id == orderId);
            if (order is not null) record.Children.Add(TextRow("Pedido de origem", order.NumberLabel));
        }
        AddSection("Registro da venda", record);
        if (sale.Notes.Length > 0) AddSection("Observações", Text(sale.Notes, 13, "#365447"));
        if (sale.Canceled)
        {
            CancelReasonLabel = Text(sale.CancelReason.Length > 0 ? sale.CancelReason : "Motivo não informado.", 13, "#A53E35");
            var cancellation = AddSection("Motivo do cancelamento", CancelReasonLabel);
            cancellation.Background = ColorBrush("#FFF7F5"); cancellation.BorderBrush = ColorBrush("#F1D5D0");
            BodyPanel.Children.Add(Muted("Esta venda permanece no histórico para consulta. O valor cancelado não compõe o total de vendas concluídas.", new Thickness(2, 0, 2, 14)));
        }
        else BodyPanel.Children.Add(Muted("Os produtos e valores exibidos são os registrados no momento da venda.", new Thickness(2, 0, 2, 14)));

        PrintButton = new Button
        {
            Name = nameof(PrintButton), Content = "Reimprimir cupom", MinHeight = 44, MinWidth = 172,
            HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(16, 10, 16, 10),
            Background = ColorBrush("#187B52"), Foreground = Brushes.White, BorderThickness = new Thickness(0), FontSize = 13
        };
        if (TryFindResource("PrimaryButton") is Style primary) PrintButton.Style = primary;
        AutomationProperties.SetName(PrintButton, "Reimprimir cupom da venda");
        PrintFeedbackLabel = Text("", 12, "#597164");
        PrintFeedbackLabel.Name = nameof(PrintFeedbackLabel); PrintFeedbackLabel.Margin = new Thickness(0, 9, 0, 0);
        PrintFeedbackLabel.Visibility = Visibility.Collapsed;
        PrintButton.Click += (_, _) =>
        {
            if (!PrintButton.IsEnabled) return;
            PrintButton.IsEnabled = false;
            try
            {
                print(this, state, sale);
                PrintFeedbackLabel.Text = "Reimpressão solicitada.";
                PrintFeedbackLabel.Foreground = ColorBrush("#187B52");
            }
            catch (Exception ex)
            {
                PrintFeedbackLabel.Text = $"Não foi possível reimprimir o cupom. {ex.Message}";
                PrintFeedbackLabel.Foreground = ColorBrush("#B4483E");
            }
            finally { PrintFeedbackLabel.Visibility = Visibility.Visible; PrintButton.IsEnabled = true; }
        };
        BodyPanel.Children.Add(PrintButton); BodyPanel.Children.Add(PrintFeedbackLabel);
        RegisterName(nameof(PrintButton), PrintButton); RegisterName(nameof(PrintFeedbackLabel), PrintFeedbackLabel);
    }

    private static TextBlock Muted(string text, Thickness margin)
    {
        var label = Text(text, 12, "#597164"); label.Margin = margin; return label;
    }
    private static UIElement MoneyRow(string label, decimal value) => TextRow(label, value.ToString("C2"));
    private static UIElement TextRow(string label, string value)
    {
        var row = new Grid { Margin = new Thickness(0, 5, 0, 5) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Children.Add(Text(label, 13, "#597164"));
        var amount = Text(value, 14, "#243D31", FontWeights.SemiBold); amount.TextAlignment = TextAlignment.Right;
        Grid.SetColumn(amount, 1); row.Children.Add(amount); return row;
    }
}
