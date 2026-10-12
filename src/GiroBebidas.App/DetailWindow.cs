using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace GiroBebidas.App;

public class DetailWindow : Window
{
    public StackPanel BodyPanel { get; }
    public ScrollViewer DetailScroll { get; }
    public Button CloseButton { get; }
    public Button HeaderCloseButton { get; }
    public TextBlock HeaderTitleLabel { get; }
    public TextBlock HeaderSubtitleLabel { get; }

    public DetailWindow(Window owner, string title, string subtitle = "", string glyph = "\uE946")
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Owner = owner;
        Title = $"Giro Bebidas • {title}";
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Background = Brushes.Transparent;
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 13;
        Foreground = ColorBrush("#172B29");
        UseLayoutRounding = true;
        MaxWidth = Math.Max(320, SystemParameters.WorkArea.Width - 32);
        MaxHeight = Math.Max(320, SystemParameters.WorkArea.Height - 32);
        MinWidth = Math.Min(560, MaxWidth);
        MinHeight = Math.Min(480, MaxHeight);
        Width = Math.Min(720, MaxWidth);
        Height = Math.Min(760, MaxHeight);
        NameScope.SetNameScope(this, new NameScope());

        // Keep the capture root unpadded; the surface owns its shadow margin.
        var root = new Grid();
        var surface = new Border
        {
            Margin = new Thickness(12), Background = Brushes.White,
            BorderBrush = ColorBrush("#DCE8DF"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(17),
            Effect = new DropShadowEffect { Color = (Color)ColorConverter.ConvertFromString("#112D25"), BlurRadius = 22, ShadowDepth = 4, Opacity = .18 }
        };
        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new Border
        {
            Background = ColorBrush("#F6FAF7"), BorderBrush = ColorBrush("#E3ECE6"), BorderThickness = new Thickness(0, 0, 0, 1),
            CornerRadius = new CornerRadius(16, 16, 0, 0), Padding = new Thickness(24, 21, 24, 21)
        };
        var headingGrid = new Grid();
        headingGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
        headingGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        headingGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headingGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        var icon = new Border { Width = 48, Height = 48, Background = ColorBrush("#E1F2E6"), CornerRadius = new CornerRadius(13), VerticalAlignment = VerticalAlignment.Center };
        icon.Child = new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 24, Foreground = ColorBrush("#187B52"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        headingGrid.Children.Add(icon);
        HeaderTitleLabel = Text(title, 23, "#172B29", FontWeights.SemiBold);
        HeaderTitleLabel.Name = nameof(HeaderTitleLabel);
        HeaderSubtitleLabel = Text(subtitle, 12, "#597164");
        HeaderSubtitleLabel.Name = nameof(HeaderSubtitleLabel);
        HeaderSubtitleLabel.Margin = new Thickness(0, 5, 12, 0);
        HeaderSubtitleLabel.Visibility = string.IsNullOrEmpty(subtitle) ? Visibility.Collapsed : Visibility.Visible;
        var headingLabels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        headingLabels.Children.Add(HeaderTitleLabel); headingLabels.Children.Add(HeaderSubtitleLabel);
        Grid.SetColumn(headingLabels, 2); headingGrid.Children.Add(headingLabels);
        HeaderCloseButton = new Button
        {
            Name = nameof(HeaderCloseButton), Content = "×", FontSize = 22, FontWeight = FontWeights.Normal,
            Width = 32, Height = 32, MinHeight = 32, Padding = new Thickness(0), BorderThickness = new Thickness(0),
            Background = Brushes.Transparent, Foreground = ColorBrush("#657C6E"), VerticalAlignment = VerticalAlignment.Top,
            ToolTip = "Fechar detalhes"
        };
        AutomationProperties.SetName(HeaderCloseButton, "Fechar detalhes");
        HeaderCloseButton.Click += (_, _) => Close();
        Grid.SetColumn(HeaderCloseButton, 3); headingGrid.Children.Add(HeaderCloseButton);
        heading.Child = headingGrid;
        heading.MouseLeftButtonDown += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed || HeaderCloseButton.IsMouseOver) return;
            DragMove(); e.Handled = true;
        };
        layout.Children.Add(heading);

        BodyPanel = new StackPanel { Name = nameof(BodyPanel), Margin = new Thickness(24, 20, 24, 20) };
        DetailScroll = new ScrollViewer
        {
            Name = nameof(DetailScroll), Content = BodyPanel, CanContentScroll = false,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        ControlledScrolling.SetIsEnabled(DetailScroll, true);
        AutomationProperties.SetName(DetailScroll, "Informações dos detalhes");
        Grid.SetRow(DetailScroll, 1); layout.Children.Add(DetailScroll);

        var footer = new Border
        {
            BorderBrush = ColorBrush("#E3ECE6"), BorderThickness = new Thickness(0, 1, 0, 0),
            Background = ColorBrush("#FAFCFA"), CornerRadius = new CornerRadius(0, 0, 16, 16), Padding = new Thickness(24, 15, 24, 18)
        };
        CloseButton = new Button
        {
            Name = nameof(CloseButton), Content = "Fechar", MinWidth = 132, MinHeight = 44,
            HorizontalAlignment = HorizontalAlignment.Right, Padding = new Thickness(16, 10, 16, 10),
            Background = ColorBrush("#187B52"), Foreground = Brushes.White, BorderThickness = new Thickness(0), FontSize = 13
        };
        if (TryFindResource("PrimaryButton") is Style primary) CloseButton.Style = primary;
        CloseButton.Click += (_, _) => Close();
        AutomationProperties.SetName(CloseButton, "Fechar detalhes");
        footer.Child = CloseButton;
        Grid.SetRow(footer, 2); layout.Children.Add(footer);
        surface.Child = layout; root.Children.Add(surface); Content = root;
        foreach (var named in new FrameworkElement[] { BodyPanel, DetailScroll, CloseButton, HeaderCloseButton, HeaderTitleLabel, HeaderSubtitleLabel }) RegisterName(named.Name, named);
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
        Loaded += (_, _) => CloseButton.Focus();
    }

    public Border AddSection(string title, UIElement content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var panel = new StackPanel();
        if (!string.IsNullOrWhiteSpace(title))
        {
            var heading = Text(title, 14, "#284C37", FontWeights.SemiBold);
            heading.Margin = new Thickness(0, 0, 0, 12); panel.Children.Add(heading);
        }
        panel.Children.Add(content);
        var card = new Border
        {
            Background = ColorBrush("#F8FBF8"), BorderBrush = ColorBrush("#DFEAE2"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12), Padding = new Thickness(16), Margin = new Thickness(0, 0, 0, 14), Child = panel
        };
        BodyPanel.Children.Add(card);
        return card;
    }

    protected static TextBlock Text(string text, double size = 13, string color = "#172B29", FontWeight? weight = null)
        => new() { Text = text, FontSize = size, Foreground = ColorBrush(color), FontWeight = weight ?? FontWeights.Normal, TextWrapping = TextWrapping.Wrap };

    protected static SolidColorBrush ColorBrush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); brush.Freeze(); return brush;
    }
}
