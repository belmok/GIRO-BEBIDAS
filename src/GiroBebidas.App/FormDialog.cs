using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace GiroBebidas.App;

public sealed class FormDialog : Window
{
    private readonly Dictionary<string, FrameworkElement> fields = [];
    private readonly StackPanel form = new() { Margin = new Thickness(26, 4, 26, 20) };
    private readonly TextBlock error = new() { Foreground = Brush("#B4483E"), FontSize = 13, TextWrapping = TextWrapping.Wrap };
    private readonly Border errorPanel, header, footer;
    internal ScrollViewer FormScroll { get; }
    internal Button CloseButton { get; }
    internal Button CancelButton { get; }
    internal TextBlock HeaderLabel { get; }
    public Button SubmitButton { get; }
    public Action<FormDialog>? Submit { get; set; }
    public FormDialog(Window owner, string title, string description = "", string submit = "Salvar")
    {
        Owner = owner; Title = $"Giro Bebidas • {title}";
        Width = Math.Min(620, Math.Max(280, SystemParameters.WorkArea.Width - 32));
        SizeToContent = SizeToContent.Height; MaxHeight = Math.Max(240, SystemParameters.WorkArea.Height - 32);
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ShowInTaskbar = false;

        var root = new Grid();
        var surface = new Border
        {
            Margin = new Thickness(12), Background = Brushes.White, BorderBrush = Brush("#DCE8DF"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(17),
            Effect = new DropShadowEffect { Color = (Color)ColorConverter.ConvertFromString("#112D25"), BlurRadius = 22, ShadowDepth = 4, Opacity = .16 }
        };
        root.Children.Add(surface);
        var layout = new Grid(); surface.Child = layout;
        layout.RowDefinitions.Add(new() { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new() { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new() { Height = GridLength.Auto });

        header = new Border { Background = Brush("#F4FAF6"), BorderBrush = Brush("#E1ECE5"), BorderThickness = new Thickness(0, 0, 0, 1), CornerRadius = new CornerRadius(16, 16, 0, 0), Padding = new Thickness(25, 23, 23, 22) };
        var headerGrid = new Grid(); header.Child = headerGrid;
        headerGrid.ColumnDefinitions.Add(new() { Width = new GridLength(48) });
        headerGrid.ColumnDefinitions.Add(new() { Width = new GridLength(15) });
        headerGrid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new() { Width = new GridLength(32) });
        var icon = new Border { Width = 48, Height = 48, CornerRadius = new CornerRadius(13), Background = Brush("#DFF2E6"), VerticalAlignment = VerticalAlignment.Top };
        icon.Child = new System.Windows.Shapes.Path { Data = Geometry.Parse("M 4,1 L 15,1 L 20,6 L 20,22 L 4,22 Z M 14,1 L 14,7 L 20,7 M 8,11 L 16,11 M 8,15 L 16,15 M 8,19 L 13,19"), Width = 23, Height = 25, Stretch = Stretch.Uniform, Stroke = Brush("#187B52"), StrokeThickness = 1.6, StrokeLineJoin = PenLineJoin.Round, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        headerGrid.Children.Add(icon);
        var heading = new StackPanel { Margin = new Thickness(0, 1, 10, 0) }; Grid.SetColumn(heading, 2); headerGrid.Children.Add(heading);
        HeaderLabel = new TextBlock { Text = title, FontSize = 23, FontWeight = FontWeights.SemiBold, Foreground = Brush("#172B29"), TextWrapping = TextWrapping.Wrap };
        heading.Children.Add(HeaderLabel);
        if (description.Length > 0) heading.Children.Add(new TextBlock { Text = description, Foreground = Brush("#536D60"), FontSize = 13, Margin = new Thickness(0, 7, 0, 0), TextWrapping = TextWrapping.Wrap });
        CloseButton = new Button
        {
            Content = new TextBlock { Text = "\uE711", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 12 },
            Style = CloseStyle(), ToolTip = "Fechar", VerticalAlignment = VerticalAlignment.Top
        };
        System.Windows.Automation.AutomationProperties.SetName(CloseButton, "Fechar formulário");
        CloseButton.Click += (_, _) => Close(); Grid.SetColumn(CloseButton, 3); headerGrid.Children.Add(CloseButton);
        header.MouseLeftButtonDown += (_, e) => { if (e.LeftButton == MouseButtonState.Pressed && !CloseButton.IsMouseOver) { DragMove(); e.Handled = true; } };
        layout.Children.Add(header);

        FormScroll = new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, CanContentScroll = false, MaxHeight = Math.Max(40, MaxHeight - 220) };
        ControlledScrolling.SetIsEnabled(FormScroll, true); Grid.SetRow(FormScroll, 1); layout.Children.Add(FormScroll);
        errorPanel = new Border { Child = error, Background = Brush("#FFF4F1"), BorderBrush = Brush("#F0D5CF"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(13, 10, 13, 10), Margin = new Thickness(26, 0, 26, 12), Visibility = Visibility.Collapsed };
        Grid.SetRow(errorPanel, 2); layout.Children.Add(errorPanel);

        footer = new Border { Background = Brush("#FAFCFA"), BorderBrush = Brush("#E3ECE6"), BorderThickness = new Thickness(0, 1, 0, 0), CornerRadius = new CornerRadius(0, 0, 16, 16), Padding = new Thickness(25, 17, 25, 20) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        CancelButton = new Button { Content = "Cancelar", IsCancel = true, Margin = new Thickness(0, 0, 10, 0), MinHeight = 44, MinWidth = 108, FontSize = 13, Foreground = Brush("#365447") };
        CancelButton.Click += (_, _) => Close(); buttons.Children.Add(CancelButton);
        SubmitButton = new Button { Content = submit, Style = (Style)FindResource("PrimaryButton"), IsDefault = true, MinWidth = 148, MinHeight = 44, FontSize = 13 };
        SubmitButton.Click += (_, _) => { try { ShowError(""); Submit?.Invoke(this); DialogResult = true; } catch (Exception ex) { ShowError(ex.Message); } };
        buttons.Children.Add(SubmitButton); footer.Child = buttons; Grid.SetRow(footer, 3); layout.Children.Add(footer);
        Content = root;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            var openList = fields.Values.OfType<ComboBox>().FirstOrDefault(list => list.IsDropDownOpen);
            if (openList is not null) { openList.IsDropDownOpen = false; return; }
            Close();
        };
        Loaded += (_, _) => { UpdateScrollLimit(); var first = fields.Values.FirstOrDefault(c => c.IsEnabled && c.Focusable); if (first is not null) first.Focus(); else SubmitButton.Focus(); };
        SizeChanged += (_, _) => UpdateScrollLimit();
    }
    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color));
    private Style CloseStyle()
    {
        var style = new Style(typeof(Button), (Style)FindResource(typeof(Button)));
        style.Setters.Add(new Setter(WidthProperty, 32d)); style.Setters.Add(new Setter(HeightProperty, 32d));
        style.Setters.Add(new Setter(MinHeightProperty, 32d)); style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent)); style.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Brush("#597465")));
        var hover = new Trigger { Property = IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Control.BackgroundProperty, Brush("#E0F0E6"))); hover.Setters.Add(new Setter(Control.ForegroundProperty, Brush("#187B52"))); style.Triggers.Add(hover);
        var focus = new Trigger { Property = IsKeyboardFocusedProperty, Value = true }; focus.Setters.Add(new Setter(Control.BorderBrushProperty, Brush("#187B52"))); style.Triggers.Add(focus);
        return style;
    }
    private void UpdateScrollLimit()
    {
        if (!IsLoaded) return;
        double reserved = 26 + header.ActualHeight + footer.ActualHeight + (errorPanel.Visibility == Visibility.Visible ? errorPanel.ActualHeight + errorPanel.Margin.Top + errorPanel.Margin.Bottom : 0);
        FormScroll.MaxHeight = Math.Max(40, MaxHeight - reserved);
    }
    private void Add(string key, string label, FrameworkElement control)
    {
        form.Children.Add(new TextBlock { Text = label, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Brush("#354F43"), Margin = new Thickness(0, 16, 0, 7), TextWrapping = TextWrapping.Wrap });
        form.Children.Add(control); fields[key] = control;
    }
    public TextBox Text(string key, string label, string value = "", bool multiline = false, bool enabled = true)
    {
        var input = new TextBox { Text = value, MaxLength = multiline ? 2000 : 300, IsEnabled = enabled, MinHeight = 46, Padding = new Thickness(13, 10, 13, 10), FontSize = 14, Foreground = Brush("#203B2E"), BorderBrush = Brush("#D8E4DD") };
        if (multiline) { input.AcceptsReturn = true; input.TextWrapping = TextWrapping.Wrap; input.Height = 88; input.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; input.VerticalContentAlignment = VerticalAlignment.Top; }
        Add(key, label, input); return input;
    }
    public TextBox Number(string key, string label, decimal value = 0, bool enabled = true) => Text(key, label, value.ToString("0.###"), enabled: enabled);
    public PasswordBox Password(string key, string label)
    {
        var input = new PasswordBox { MaxLength = 128, MinHeight = 46, FontSize = 14, Padding = new Thickness(13, 10, 13, 10) }; Add(key, label, input); return input;
    }
    public ComboBox Choice<T>(string key, string label, IEnumerable<T> options, T? selected = default, string? display = null, bool editable = false)
    {
        var input = new ComboBox { ItemsSource = options.ToList(), IsEditable = editable, MinHeight = 46, FontSize = 14, Padding = new Thickness(13, 10, 13, 10), BorderBrush = Brush("#D8E4DD") };
        if (display is not null) input.DisplayMemberPath = display;
        input.SelectedItem = selected; if (input.SelectedIndex < 0 && input.Items.Count > 0) input.SelectedIndex = 0;
        Add(key, label, input); return input;
    }
    public CheckBox Check(string key, string label, bool value = false)
    {
        var input = new CheckBox { Content = label, IsChecked = value, FontSize = 13, Foreground = Brush("#354F43"), Margin = new Thickness(0, 12, 0, 4) }; form.Children.Add(input); fields[key] = input; return input;
    }
    public TextBlock Info(string text)
    {
        var block = new TextBlock { Text = text, FontSize = 13, Foreground = Brush("#3F6250"), TextWrapping = TextWrapping.Wrap };
        var card = new Border { Child = block, Background = Brush("#F3F9F5"), BorderBrush = Brush("#DFEBE2"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 12, 14, 12), Margin = new Thickness(0, 14, 0, 4) };
        form.Children.Add(card); return block;
    }
    public FrameworkElement Summary(string title, IEnumerable<(string Label, string Value)> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var contents = new StackPanel();
        contents.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Brush("#187B52"), Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap });
        foreach (var row in rows)
        {
            var line = new Grid { Margin = new Thickness(0, 6, 0, 6) };
            line.ColumnDefinitions.Add(new() { Width = new GridLength(1.1, GridUnitType.Star) });
            line.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            line.Children.Add(new TextBlock { Text = row.Label, FontSize = 13, Foreground = Brush("#526B5D"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 15, 0), VerticalAlignment = VerticalAlignment.Center });
            var value = new TextBlock { Text = row.Value, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Brush("#203B2E"), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(value, 1); line.Children.Add(value); contents.Children.Add(line);
        }
        var card = new Border { Child = contents, Background = Brush("#F3FAF5"), BorderBrush = Brush("#D8EADC"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(16, 14, 16, 14), Margin = new Thickness(0, 14, 0, 4) };
        form.Children.Add(card); return card;
    }
    public Image Photo(ImageSource? source)
    {
        var image = new Image { Source = source, Height = 125, Stretch = Stretch.Uniform, Margin = new Thickness(0, 10, 0, 10) };
        form.Children.Add(image); return image;
    }
    public Button Action(string label, Action click)
    {
        var button = new Button { Content = label, Margin = new Thickness(0, 7, 0, 0) };
        button.Click += (_, _) => { try { click(); } catch (Exception ex) { ShowError(ex.Message); } };
        form.Children.Add(button); return button;
    }
    public void ShowError(string message) { error.Text = message; errorPanel.Visibility = string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible; UpdateScrollLimit(); }
    public string Value(string key) => fields[key] switch { TextBox t => t.Text.Trim(), PasswordBox p => p.Password, ComboBox c => c.Text.Trim(), _ => "" };
    public decimal Decimal(string key)
    {
        string value = Value(key).Replace('.', ',');
        if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.GetCultureInfo("pt-BR"), out var result)) throw new InvalidOperationException("Informe valores válidos, sem separador de milhar.");
        return result;
    }
    public T Selected<T>(string key) => (T)((ComboBox)fields[key]).SelectedItem;
    public bool Checked(string key) => ((CheckBox)fields[key]).IsChecked == true;
    internal T Field<T>(string key) where T : FrameworkElement => (T)fields[key];
    internal void ScrollToField(string key) { if (fields.TryGetValue(key, out var field)) field.BringIntoView(); }
}
