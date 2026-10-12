using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public sealed class ProductDetailsWindow : DetailWindow
{
    private static readonly CultureInfo Brazilian = CultureInfo.GetCultureInfo("pt-BR");
    public Product ProductForDisplay { get; }
    public TextBlock ProductNameLabel { get; }
    public Image ProductImage { get; }
    public TextBlock PriceLabel { get; }
    public TextBlock ReservedLabel { get; }
    public TextBlock AvailableLabel { get; }
    public TextBlock DescriptionLabel { get; }

    public ProductDetailsWindow(Window owner, Product product, decimal reserved)
        : base(owner, "Detalhes do produto", "Cadastro, preços e disponibilidade em um só lugar.", "\uE946")
    {
        ArgumentNullException.ThrowIfNull(product);
        ProductForDisplay = JsonSerializer.Deserialize<Product>(JsonSerializer.Serialize(product, JsonStore.JsonOptions), JsonStore.JsonOptions)!;
        var display = ProductForDisplay;
        decimal available = display.Stock - reserved;

        ProductNameLabel = Text(display.Name, 22, "#172B29", FontWeights.SemiBold);
        ProductNameLabel.Name = nameof(ProductNameLabel);
        ProductImage = new Image
        {
            Name = nameof(ProductImage), Source = PhotoTools.Decode(display.PhotoBase64) ?? BeverageArt.Create(display),
            Height = 152, Stretch = Stretch.Uniform, Margin = new Thickness(8)
        };
        PriceLabel = Text(display.Price.ToString("C2", Brazilian), 23, "#187B52", FontWeights.SemiBold);
        PriceLabel.Name = nameof(PriceLabel);
        ReservedLabel = Text(Quantity(reserved, display.Unit), 22, reserved > 0 ? "#906617" : "#526D5C", FontWeights.SemiBold);
        ReservedLabel.Name = nameof(ReservedLabel);
        AvailableLabel = Text(Quantity(available, display.Unit), 22,
            available <= 0 ? "#B4483E" : available <= display.MinimumStock ? "#906617" : "#187B52", FontWeights.SemiBold);
        AvailableLabel.Name = nameof(AvailableLabel);
        DescriptionLabel = Text(string.IsNullOrWhiteSpace(display.Description) ? "Descrição não informada." : display.Description, 13, "#415C4C");
        DescriptionLabel.Name = nameof(DescriptionLabel);
        foreach (var field in new FrameworkElement[] { ProductNameLabel, ProductImage, PriceLabel, ReservedLabel, AvailableLabel, DescriptionLabel }) RegisterName(field.Name, field);

        var productHeader = new Grid();
        productHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(154) });
        productHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        productHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        productHeader.Children.Add(new Border
        {
            Background = ColorBrush("#EFF6F0"), BorderBrush = ColorBrush("#E0EAE2"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12), Child = ProductImage, VerticalAlignment = VerticalAlignment.Top
        });
        var identity = new StackPanel();
        identity.Children.Add(ProductNameLabel);
        var code = Text("Código: " + display.Code, 12, "#536E5D", FontWeights.SemiBold); code.Margin = new Thickness(0, 9, 0, 0); identity.Children.Add(code);
        var barcode = Text("Código de barras: " + Present(display.Barcode), 12, "#657D6E"); barcode.Margin = new Thickness(0, 5, 0, 0); identity.Children.Add(barcode);
        var category = Text("Categoria: " + display.Category, 12, "#536E5D"); category.Margin = new Thickness(0, 7, 0, 0); identity.Children.Add(category);
        var state = new Border
        {
            Background = ColorBrush(display.Active ? "#E3F5E7" : "#FFF4F1"), CornerRadius = new CornerRadius(9),
            HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(9, 5, 9, 5), Margin = new Thickness(0, 12, 0, 0),
            Child = Text(display.Active ? "Ativo para venda" : "Inativo", 12, display.Active ? "#187B52" : "#B4483E", FontWeights.SemiBold)
        };
        identity.Children.Add(state); Grid.SetColumn(identity, 2); productHeader.Children.Add(identity);
        AddSection("", productHeader);

        var metrics = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, -10, -10) };
        metrics.Children.Add(Metric("Venda por " + display.Unit, PriceLabel, "#EDF7F0", "#D9EBDC"));
        metrics.Children.Add(Metric("Custo por " + display.Unit, Text(display.Cost.ToString("C2", Brazilian), 22, "#3E5D4B", FontWeights.SemiBold)));
        metrics.Children.Add(Metric("Estoque físico", Text(Quantity(display.Stock, display.Unit), 22, "#3E5D4B", FontWeights.SemiBold)));
        metrics.Children.Add(Metric("Reservado", ReservedLabel, "#FFF8EA", "#F0E5CF"));
        metrics.Children.Add(Metric("Livre para venda", AvailableLabel, available <= 0 ? "#FFF4F1" : "#EDF7F0", available <= 0 ? "#F0D5CF" : "#D9EBDC"));
        metrics.Children.Add(Metric("Estoque mínimo", Text(Quantity(display.MinimumStock, display.Unit), 22, "#3E5D4B", FontWeights.SemiBold)));
        bool wholesale = display.WholesaleMinimum > 0 && display.WholesalePrice > 0;
        metrics.Children.Add(Metric("Preço de atacado", Text(wholesale ? display.WholesalePrice.ToString("C2", Brazilian) : "Não configurado", wholesale ? 22 : 15, "#3E5D4B", FontWeights.SemiBold)));
        metrics.Children.Add(Metric("Atacado a partir de", Text(wholesale ? Quantity(display.WholesaleMinimum, display.Unit) : "—", 22, "#3E5D4B", FontWeights.SemiBold)));
        AddSection("Preços e estoque", metrics);
        AddSection("Descrição do produto", DescriptionLabel);

        var tax = new StackPanel();
        tax.Children.Add(TaxLine("NCM", Present(display.Tax.Ncm)));
        tax.Children.Add(TaxLine("CEST", Present(display.Tax.Cest)));
        tax.Children.Add(TaxLine("CFOP", Present(display.Tax.Cfop)));
        if (!string.IsNullOrWhiteSpace(display.Tax.Csosn)) tax.Children.Add(TaxLine("CSOSN", display.Tax.Csosn));
        if (!string.IsNullOrWhiteSpace(display.Tax.Cst)) tax.Children.Add(TaxLine("CST de ICMS", display.Tax.Cst));
        if (display.Tax.Origin is int origin) tax.Children.Add(TaxLine("Origem da mercadoria", origin.ToString(Brazilian)));
        AddSection("Dados fiscais", tax);
    }

    private static string Quantity(decimal quantity, string unit) => quantity.ToString("0.###", Brazilian) + " " + unit;
    private static string Present(string value) => string.IsNullOrWhiteSpace(value) ? "Não informado" : value;

    private static Border Metric(string title, TextBlock value, string background = "#F2F6F3", string border = "#E0E9E2")
    {
        var content = new StackPanel();
        content.Children.Add(Text(title, 12, "#597164"));
        value.TextWrapping = TextWrapping.NoWrap;
        content.Children.Add(new Viewbox { Child = value, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left, Height = 30, Margin = new Thickness(0, 8, 0, 0) });
        return new Border
        {
            Background = ColorBrush(background), BorderBrush = ColorBrush(border), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
            Padding = new Thickness(13, 12, 13, 12), Margin = new Thickness(0, 0, 10, 10), Child = content
        };
    }

    private static FrameworkElement TaxLine(string title, string value)
    {
        var line = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(155) });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        line.Children.Add(Text(title + ":", 12, "#597164", FontWeights.SemiBold));
        var text = Text(value, 13, "#365447"); Grid.SetColumn(text, 1); line.Children.Add(text);
        return line;
    }
}
