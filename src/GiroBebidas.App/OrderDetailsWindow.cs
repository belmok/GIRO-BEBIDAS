using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public sealed class OrderDetailsWindow : DetailWindow
{
    internal Order OrderForDisplay { get; }
    internal TextBlock CustomerNameLabel { get; }
    internal TextBlock AddressLabel { get; }
    internal TextBlock TotalLabel { get; }
    internal TextBlock StatusLabel { get; }
    internal StackPanel ItemsPanel { get; }
    public OrderDetailsWindow(Window owner, Order order, StoreState state)
        : base(owner, $"Pedido {order.NumberLabel}", $"{order.Date:dd/MM/yyyy HH:mm} • {order.TypeLabel}", "\uE8A7")
    {
        OrderForDisplay = order;
        var statusColor = order.Status switch { OrderStatus.Cancelado => "#B4483E", OrderStatus.Pendente => "#97651F", OrderStatus.Preparando => "#8154AD", OrderStatus.EmEntrega => "#286FAD", _ => "#187B52" };
        var statusBackground = order.Status switch { OrderStatus.Cancelado => "#FFF1EE", OrderStatus.Pendente => "#FFF3DC", OrderStatus.Preparando => "#F0E8F9", OrderStatus.EmEntrega => "#E8F2FC", _ => "#E3F5E7" };
        StatusLabel = Text(order.StatusLabel, 12, statusColor, FontWeights.SemiBold);
        BodyPanel.Children.Add(new Border { Background = Brush(statusBackground), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 7, 12, 7), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 14), Child = StatusLabel });
        var customer = new StackPanel(); CustomerNameLabel = Text(order.CustomerName, 17, "#172B29", FontWeights.SemiBold);
        customer.Children.Add(CustomerNameLabel); customer.Children.Add(Text(order.Phone.Length > 0 ? order.Phone : "Telefone não informado", 12, "#597164", margin: new Thickness(0, 5, 0, 12)));
        AddressLabel = Text(order.Delivery ? order.Address : "Retirada no balcão", 13); customer.Children.Add(AddressLabel);
        AddSection(order.Delivery ? "Cliente e entrega" : "Cliente e retirada", customer);
        ItemsPanel = new StackPanel();
        foreach (var line in order.Lines)
        {
            var row = new Grid { Margin = new Thickness(0, 5, 0, 5) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(106) });
            var product = state.Products.FirstOrDefault(p => p.Id == line.ProductId);
            if (product is not null) row.Children.Add(new Border { Background = Brush("#F2F7F3"), CornerRadius = new CornerRadius(9), Padding = new Thickness(5), Width = 48, Height = 54, VerticalAlignment = VerticalAlignment.Center, Child = new Image { Source = PhotoTools.Decode(product.PhotoBase64) ?? BeverageArt.Create(product), Stretch = Stretch.Uniform } });
            var description = new StackPanel { Margin = new Thickness(6, 4, 8, 4), VerticalAlignment = VerticalAlignment.Center };
            description.Children.Add(Text(line.ProductName, 13, "#243D31", FontWeights.SemiBold));
            description.Children.Add(Text($"{line.Quantity:0.###} {line.Unit} × {line.UnitPrice:C2}", 12, "#597164", margin: new Thickness(0, 5, 0, 0)));
            Grid.SetColumn(description, 1); row.Children.Add(description);
            var value = Text(line.GrossTotal.ToString("C2"), 14, "#172B29", FontWeights.SemiBold); value.TextAlignment = TextAlignment.Right; value.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(value, 2); row.Children.Add(value);
            ItemsPanel.Children.Add(new Border { BorderBrush = Brush("#E7EFE9"), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 0, 0, 8), Child = row });
        }
        AddSection($"Produtos do pedido • {order.Lines.Count}", ItemsPanel);
        var totals = new StackPanel(); totals.Children.Add(MoneyRow("Subtotal", order.Lines.Sum(line => line.GrossTotal))); totals.Children.Add(MoneyRow("Desconto", order.Discount)); totals.Children.Add(MoneyRow("Entrega", order.DeliveryFee));
        TotalLabel = Text(order.Total.ToString("C2"), 25, "#187B52", FontWeights.SemiBold);
        var total = new Grid(); total.ColumnDefinitions.Add(new ColumnDefinition()); total.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        total.Children.Add(Text("Total do pedido", 14, "#24503A", FontWeights.SemiBold)); Grid.SetColumn(TotalLabel, 1); total.Children.Add(TotalLabel);
        totals.Children.Add(new Border { Background = Brush("#EAF6EE"), CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 13, 14, 13), Margin = new Thickness(0, 12, 0, 0), Child = total });
        AddSection("Resumo do pedido", totals);
        if (order.Notes.Length > 0) AddSection("Observações", Text(order.Notes, 13));
        BodyPanel.Children.Add(Text(order.IsOpen ? "Os produtos estão reservados. Use Receber no PDV para registrar o pagamento e concluir o pedido." : "Pedido encerrado. Este histórico permanece disponível para consulta.", 12, "#597164", margin: new Thickness(2, 2, 2, 8)));
    }
    private static UIElement MoneyRow(string label, decimal value)
    {
        var row = new Grid { Margin = new Thickness(0, 5, 0, 5) }; row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(Text(label, 13, "#597164")); var amount = Text(value.ToString("C2"), 14, "#243D31", FontWeights.SemiBold); Grid.SetColumn(amount, 1); row.Children.Add(amount); return row;
    }
    private static TextBlock Text(string text, double size = 13, string color = "#365447", FontWeight? weight = null, Thickness? margin = null)
        => new() { Text = text, FontSize = size, Foreground = Brush(color), FontWeight = weight ?? FontWeights.Normal, TextWrapping = TextWrapping.Wrap, Margin = margin ?? new Thickness() };
    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color));
}
