using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public sealed class CashDetailsWindow : DetailWindow
{
    internal CashSession CashForDisplay { get; }
    internal TextBlock StatusLabel { get; }
    internal Border StatusBadge { get; }
    internal IReadOnlyDictionary<string, TextBlock> ValueLabels => values;
    internal IReadOnlyList<Border> SectionCards => sections;
    private readonly Dictionary<string, TextBlock> values = [];
    private readonly List<Border> sections = [];

    public CashDetailsWindow(Window owner, CashSession cash, CashSummary totals)
        : base(owner, "Detalhes do caixa", $"Abertura em {cash.OpenedAt:dd/MM/yyyy HH:mm}", "\uE8A5")
    {
        CashForDisplay = cash;
        Width = Math.Min(800, MaxWidth);
        Height = Math.Min(860, MaxHeight);
        bool closed = cash.ClosedAt is not null;
        StatusLabel = Text(cash.StatusLabel, 12, closed ? "#B4483E" : "#187B52", FontWeights.SemiBold);
        var badgeContent = new StackPanel { Orientation = Orientation.Horizontal };
        badgeContent.Children.Add(Text("●", 10, closed ? "#B4483E" : "#187B52"));
        StatusLabel.Margin = new Thickness(7, 0, 0, 0); badgeContent.Children.Add(StatusLabel);
        StatusBadge = new Border
        {
            Child = badgeContent, Background = ColorBrush(closed ? "#FFF1EE" : "#E3F5E7"),
            BorderBrush = ColorBrush(closed ? "#F0D6D2" : "#CEE8D6"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12), Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(8, 0, 0, 0)
        };
        HeaderSubtitleLabel.Inlines.Add(new Run("  • "));
        HeaderSubtitleLabel.Inlines.Add(new InlineUIContainer(StatusBadge) { BaselineAlignment = BaselineAlignment.Center });

        var opening = Section("Abertura do caixa", "\uE8C7", $"{cash.OpenedAt:dd/MM/yyyy HH:mm}  •  {cash.OpenedBy}");
        Row(opening, "openedBy", "Responsável pela abertura", cash.OpenedBy, "\uE77B");
        Row(opening, "opening", "Fundo de troco", totals.Opening.ToString("C2"), "\uE8EC");
        Row(opening, "cash", "Dinheiro recebido", totals.CashSales.ToString("C2"), "\uE8C7");
        Row(opening, "supplies", "Suprimentos", totals.Supplies.ToString("C2"), "\uE896");
        Row(opening, "withdrawals", "Sangrias", totals.Withdrawals.ToString("C2"), "\uE898");
        Row(opening, "expected", "Dinheiro esperado", (closed ? cash.ExpectedCash : totals.Expected).ToString("C2"), "\uE9D9", total: true);

        var sales = Section("Movimentações de vendas", "\uE9D9");
        Row(sales, "pix", "Pix", totals.Pix.ToString("C2"), "\uED14");
        Row(sales, "debit", "Cartão de débito", totals.Debit.ToString("C2"), "\uE8C7");
        Row(sales, "credit", "Cartão de crédito", totals.Credit.ToString("C2"), "\uE8C7");
        Row(sales, "creditSales", "Vendas em fiado", totals.CreditSales.ToString("C2"), "\uE8A5");
        Row(sales, "saleCount", "Vendas realizadas", totals.SaleCount.ToString(), "\uE7BF");
        Row(sales, "total", "Total de vendas", totals.TotalSales.ToString("C2"), "\uE9D9", total: true);

        var closing = Section("Fechamento do caixa", "\uE72E", cash.ClosedAt is { } date ? $"{date:dd/MM/yyyy HH:mm}  •  {cash.ClosedBy}" : "Aguardando fechamento");
        if (closed)
        {
            Row(closing, "counted", "Dinheiro contado", cash.CountedCash.ToString("C2"), "\uE8EC");
            Row(closing, "difference", "Diferença", cash.Difference.ToString("C2"), "\uE9D9",
                color: cash.Difference < 0 ? "#B4483E" : cash.Difference > 0 ? "#906617" : "#187B52");
            string notes = string.IsNullOrWhiteSpace(cash.ClosingNotes) ? "Sem observações do fechamento." : cash.ClosingNotes;
            if (notes.Length > 160 || notes.Contains('\n'))
            {
                var description = Text("Observações", 13, "#597164"); description.Margin = new Thickness(0, 12, 0, 7);
                closing.Children.Add(description);
                var note = Text(notes, 13, "#425C50"); note.Name = "CashDetail_notes";
                values.Add("notes", note); closing.Children.Add(note);
            }
            else Row(closing, "notes", "Observações", notes, "\uE8BD");
        }
        else
        {
            var notice = Text("Caixa em aberto. O dinheiro contado e a diferença estarão disponíveis após o fechamento.", 12, "#597164");
            notice.Margin = new Thickness(2, 3, 0, 4); closing.Children.Add(notice);
        }
    }

    private StackPanel Section(string title, string glyph, string metadata = "")
    {
        var content = new StackPanel();
        var header = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        header.ColumnDefinitions.Add(new ColumnDefinition());
        var badge = new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(22), Background = ColorBrush("#E4F4EB") };
        var badgeIcon = GlyphIcon(glyph, 22); badgeIcon.VerticalAlignment = VerticalAlignment.Center; badgeIcon.Margin = new Thickness();
        badge.Child = badgeIcon; header.Children.Add(badge);
        var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        labels.Children.Add(Text(title, 17, "#172B29", FontWeights.SemiBold));
        if (metadata.Length > 0)
        {
            var note = Text(metadata, 11, "#597164"); note.Margin = new Thickness(0, 4, 0, 0); labels.Children.Add(note);
        }
        Grid.SetColumn(labels, 2); header.Children.Add(labels); content.Children.Add(header);
        var card = new Border
        {
            Background = Brushes.White, BorderBrush = ColorBrush("#DCE8DF"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(13), Padding = new Thickness(16), Margin = new Thickness(0, 0, 0, 14), Child = content
        };
        sections.Add(card); BodyPanel.Children.Add(card); return content;
    }

    private void Row(StackPanel panel, string key, string label, string value, string glyph, bool total = false, string color = "#425C50")
    {
        var row = new Grid { Margin = new Thickness(0, 5, 0, 5) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Children.Add(GlyphIcon(glyph, 16));
        var description = Text(label, total ? 14 : 13, total ? "#172B29" : "#597164", total ? FontWeights.SemiBold : FontWeights.Normal);
        description.Margin = new Thickness(0, 0, 12, 0); Grid.SetColumn(description, 1); row.Children.Add(description);
        var amount = Text(value, total ? 17 : 13, total ? "#187B52" : color, total || key == "difference" ? FontWeights.SemiBold : FontWeights.Normal);
        amount.TextAlignment = TextAlignment.Right; amount.Name = "CashDetail_" + key; Grid.SetColumn(amount, 2); row.Children.Add(amount);
        values.Add(key, amount);
        if (total)
            panel.Children.Add(new Border { BorderBrush = ColorBrush("#E5EEE8"), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 8, 0, 0), Margin = new Thickness(0, 5, 0, 0), Child = row });
        else panel.Children.Add(row);
    }

    private static TextBlock GlyphIcon(string glyph, double size) => new()
    {
        Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = size, Foreground = ColorBrush("#187B52"),
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 0, 0)
    };
}
