using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; Changed(name); return true;
    }
}
public sealed class RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) : ICommand
{
    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => execute(parameter);
    public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
}
public sealed class PageVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => Equals(value?.ToString(), parameter?.ToString()) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
public sealed class CashDifferenceToneConverter : IValueConverter
{
    private static readonly Brush Negative = Tone("#B4483E"), Positive = Tone("#906617"), Balanced = Tone("#187B52");
    private static readonly Brush NegativeBackground = Tone("#FFF4F1"), PositiveBackground = Tone("#FFF1D8"), BalancedBackground = Tone("#E3F5E7");
    private static Brush Tone(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); brush.Freeze(); return brush;
    }
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not decimal difference) return DependencyProperty.UnsetValue;
        bool background = string.Equals(parameter?.ToString(), "background", StringComparison.OrdinalIgnoreCase);
        return background
            ? difference < 0 ? NegativeBackground : difference > 0 ? PositiveBackground : BalancedBackground
            : difference < 0 ? Negative : difference > 0 ? Positive : Balanced;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
public record NavItem(string Key, string Title, string Icon, string Group);
public record CategoryChoice(string Name, string Icon);
public class ProductTile
{
    public Product Product { get; }
    public string Name => Product.Name;
    public string Code => $"SKU {Product.Code}";
    public string Price => Product.Price.ToString("C2");
    public string Unit => $"por {Product.Unit}";
    public decimal Available { get; }
    public string Stock => $"{Available:0.###} {Product.Unit} disponíveis";
    public string Wholesale => Product.WholesaleMinimum > 0 ? $"{Product.WholesalePrice:C2} a partir de {Product.WholesaleMinimum:0.###} {Product.Unit}" : "Preço de varejo";
    public Brush StockBrush => new SolidColorBrush((Color)ColorConverter.ConvertFromString(Available <= Product.MinimumStock ? "#B37725" : "#6C8274"));
    public ImageSource Image { get; }
    public ProductTile(Product product, decimal available) { Product = product; Available = available; Image = PhotoTools.Decode(product.PhotoBase64) ?? BeverageArt.Create(product); }
}
public sealed class CartLine(Product product, decimal quantity, decimal? fixedPrice = null) : Observable
{
    private decimal quantity = quantity;
    public Product Product { get; } = product;
    public ImageSource Image { get; } = PhotoTools.Decode(product.PhotoBase64) ?? BeverageArt.Create(product);
    public string Name => Product.Name;
    public decimal Quantity { get => quantity; set { if (Set(ref quantity, value)) { Changed(nameof(QuantityLabel)); Changed(nameof(UnitPrice)); Changed(nameof(Total)); Changed(nameof(PriceLabel)); } } }
    public decimal UnitPrice => fixedPrice ?? Product.UnitPrice(Quantity);
    public decimal Total => Money.Round(UnitPrice * Quantity);
    public string QuantityLabel => $"{Quantity:0.###}";
    public string PriceLabel => $"{UnitPrice:C2} / {Product.Unit}" + (fixedPrice is null && UnitPrice != Product.Price ? " • atacado" : "");
}
public record CustomerRow(Customer Customer, decimal Balance, bool CanDelete = false)
{
    public string Name => Customer.Name; public string Phone => Customer.Phone; public string Address => Customer.Address;
    public decimal Limit => Customer.CreditLimit; public string Document => Customer.Document;
    public string Initials => string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(n => n[..1])).ToUpperInvariant();
    public string Status => Customer.CreditBlocked ? "Fiado bloqueado" : Balance > 0 ? "Saldo em aberto" : "Em dia";
    public string StatusBackground => Customer.CreditBlocked ? "#FDEBE9" : Balance > 0 ? "#FFF2D9" : "#E3F4E8";
    public string StatusForeground => Customer.CreditBlocked ? "#B83B30" : Balance > 0 ? "#97641E" : "#187B52";
    public string CreditActionLabel => Customer.CreditBlocked ? "Liberar fiado" : "Bloquear fiado";
    public bool HasDebt => Balance > 0;
}
public record StockRow(Product Product, decimal Reserved, bool CanDelete = false)
{
    private ImageSource? image;
    public ImageSource Image => image ??= PhotoTools.Decode(Product.PhotoBase64) ?? BeverageArt.Create(Product);
    public string Name => Product.Name; public string Code => Product.Code; public string Category => Product.Category;
    public decimal Stock => Product.Stock; public decimal Available => Stock - Reserved;
    public decimal Price => Product.Price; public decimal Cost => Product.Cost; public string Unit => Product.Unit;
    public string Status => !Product.Active ? "Inativo" : Available <= 0 ? "Sem estoque" : Available <= Product.MinimumStock ? "Estoque baixo" : "Disponível";
    public string ToggleActiveLabel => Product.Active ? "Desativar produto" : "Ativar produto";
    public string DeleteHint => CanDelete ? "Excluir produto sem estoque e sem histórico." : "Este produto possui estoque ou histórico. Desative para preservar os registros.";
    public string CategoryBackground => Category switch { "Cervejas" => "#FFF1CF", "Refrigerantes" => "#E0F0FE", "Destilados" => "#EEE6FA", "Água e gelo" => "#DFF2EC", _ => "#EEF1F0" };
    public string CategoryForeground => Category switch { "Cervejas" => "#805E1B", "Refrigerantes" => "#276599", "Destilados" => "#76569E", "Água e gelo" => "#28785C", _ => "#5C7265" };
    public string StatusBackground => Status switch { "Disponível" => "#E3F5E7", "Estoque baixo" => "#FFF1D8", "Sem estoque" => "#FFF0ED", _ => "#EEF1F0" };
    public string StatusForeground => Status switch { "Disponível" => "#187B52", "Estoque baixo" => "#906617", "Sem estoque" => "#B4483E", _ => "#6B7C72" };
    public string StatusDot => StatusForeground;
}
public record StockMovementRow(StockMovement Movement)
{
    public DateTimeOffset Date => Movement.Date;
    public string ProductName => Movement.ProductName;
    public decimal Quantity => Movement.Quantity;
    public string QuantityLabel => Quantity.ToString("+0.###;-0.###;0");
    public int Sign => Math.Sign(Quantity);
    public string Reason => Movement.Reason;
    public string UserName => Movement.UserName;
}

public static class BeverageArt
{
    public static DrawingImage Create(Product product)
    {
        var group = new DrawingGroup();
        Color color = (Color)ColorConverter.ConvertFromString(product.Color);
        using (var d = group.Open())
        {
            var body = new SolidColorBrush(color); var dark = new SolidColorBrush(Color.FromRgb((byte)(color.R * .67), (byte)(color.G * .67), (byte)(color.B * .67)));
            d.DrawEllipse(new SolidColorBrush(Color.FromArgb(25, 30, 50, 40)), null, new Point(80, 117), 36, 5);
            if (product.Package == "Garrafa")
            {
                d.DrawRoundedRectangle(dark, null, new Rect(68, 7, 24, 11), 3, 3);
                d.DrawRoundedRectangle(body, null, new Rect(70, 18, 20, 23), 3, 3);
                var geometry = Geometry.Parse("M70,32 C70,47 54,47 54,58 L54,104 Q54,117 68,117 L92,117 Q106,117 106,104 L106,58 C106,47 90,47 90,32 Z");
                d.DrawGeometry(body, null, geometry);
                d.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(85, 255, 255, 255)), null, new Rect(61, 53, 5, 53), 2, 2);
                d.DrawRoundedRectangle(Brushes.White, null, new Rect(53, 65, 54, 32), 3, 3);
                d.DrawRectangle(dark, null, new Rect(54, 92, 52, 5));
            }
            else if (product.Package == "Pacote" || product.Package == "Caixa")
            {
                d.DrawRoundedRectangle(body, null, new Rect(37, 27, 86, 86), 11, 11);
                d.DrawRectangle(dark, null, new Rect(40, 27, 80, 8));
                d.DrawRoundedRectangle(Brushes.White, null, new Rect(42, 59, 76, 35), 4, 4);
                d.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)), 5), new Point(47, 40), new Point(47, 104));
            }
            else
            {
                d.DrawRoundedRectangle(body, null, new Rect(51, 22, 58, 92), 11, 11);
                d.DrawEllipse(new SolidColorBrush(Color.FromRgb(206, 211, 209)), null, new Point(80, 23), 28, 6);
                d.DrawEllipse(new SolidColorBrush(Color.FromRgb(145, 155, 150)), null, new Point(80, 22), 10, 3);
                d.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(75, 255, 255, 255)), null, new Rect(57, 32, 5, 73), 2, 2);
                d.DrawRectangle(Brushes.White, null, new Rect(51, 60, 58, 31));
                d.DrawRectangle(dark, null, new Rect(51, 91, 58, 6));
            }
            var text = new FormattedText("GIRO", CultureInfo.GetCultureInfo("pt-BR"), FlowDirection.LeftToRight, new Typeface("Segoe UI Semibold"), 13, dark, 1);
            d.DrawText(text, new Point(80 - text.Width / 2, product.Package == "Garrafa" ? 70 : 65));
        }
        group.Freeze(); var image = new DrawingImage(group); image.Freeze(); return image;
    }
}
