using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GiroBebidas.App;

public sealed class FormDialog : Window
{
    private readonly Dictionary<string, FrameworkElement> fields = [];
    private readonly StackPanel form = new() { Margin = new Thickness(28, 0, 28, 12) };
    private readonly TextBlock error = new() { Foreground = new SolidColorBrush(Color.FromRgb(181, 68, 55)), Margin = new Thickness(28, 8, 28, 0), TextWrapping = TextWrapping.Wrap };
    public Button SubmitButton { get; }
    public Action<FormDialog>? Submit { get; set; }
    public FormDialog(Window owner, string title, string description = "", string submit = "Salvar")
    {
        Owner = owner; Title = $"Giro Bebidas • {title}"; Width = 570; SizeToContent = SizeToContent.Height; MaxHeight = Math.Max(600, SystemParameters.WorkArea.Height - 50);
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize; Background = new SolidColorBrush(Color.FromRgb(247, 249, 246));
        var root = new Grid(); root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) }); root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var heading = new StackPanel { Margin = new Thickness(28, 26, 28, 20) }; heading.Children.Add(new TextBlock { Text = title, FontSize = 25, FontWeight = FontWeights.SemiBold });
        if (description.Length > 0) heading.Children.Add(new TextBlock { Text = description, Foreground = Brushes.Gray, FontSize = 12, Margin = new Thickness(0, 9, 0, 0), TextWrapping = TextWrapping.Wrap });
        root.Children.Add(heading); var scroll = new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = MaxHeight - 205 }; Grid.SetRow(scroll, 1); root.Children.Add(scroll);
        Grid.SetRow(error, 2); root.Children.Add(error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(28, 17, 28, 24) };
        var cancel = new Button { Content = "Cancelar", IsCancel = true, Margin = new Thickness(0, 0, 10, 0) }; cancel.Click += (_, _) => Close(); buttons.Children.Add(cancel);
        SubmitButton = new Button { Content = submit, Style = (Style)FindResource("PrimaryButton"), IsDefault = true, MinWidth = 135 };
        SubmitButton.Click += (_, _) => { try { error.Text = ""; Submit?.Invoke(this); DialogResult = true; } catch (Exception ex) { ShowError(ex.Message); } };
        buttons.Children.Add(SubmitButton); Grid.SetRow(buttons, 3); root.Children.Add(buttons); Content = root;
    }
    private void Add(string key, string label, FrameworkElement control)
    {
        form.Children.Add(new TextBlock { Text = label, FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 13, 0, 7) });
        form.Children.Add(control); fields[key] = control;
    }
    public TextBox Text(string key, string label, string value = "", bool multiline = false, bool enabled = true)
    {
        var input = new TextBox { Text = value, MaxLength = multiline ? 2000 : 300, IsEnabled = enabled };
        if (multiline) { input.AcceptsReturn = true; input.TextWrapping = TextWrapping.Wrap; input.Height = 80; input.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; }
        Add(key, label, input); return input;
    }
    public TextBox Number(string key, string label, decimal value = 0, bool enabled = true) => Text(key, label, value.ToString("0.###"), enabled: enabled);
    public PasswordBox Password(string key, string label)
    {
        var input = new PasswordBox { MaxLength = 128 }; Add(key, label, input); return input;
    }
    public ComboBox Choice<T>(string key, string label, IEnumerable<T> options, T? selected = default, string? display = null, bool editable = false)
    {
        var input = new ComboBox { ItemsSource = options.ToList(), IsEditable = editable };
        if (display is not null) input.DisplayMemberPath = display;
        input.SelectedItem = selected; if (input.SelectedIndex < 0 && input.Items.Count > 0) input.SelectedIndex = 0;
        Add(key, label, input); return input;
    }
    public CheckBox Check(string key, string label, bool value = false)
    {
        var input = new CheckBox { Content = label, IsChecked = value }; form.Children.Add(input); fields[key] = input; return input;
    }
    public TextBlock Info(string text)
    {
        var block = new TextBlock { Text = text, FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(99, 126, 91)), Margin = new Thickness(0, 14, 0, 4), TextWrapping = TextWrapping.Wrap };
        form.Children.Add(block); return block;
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
    public void ShowError(string message) => error.Text = message;
    public string Value(string key) => fields[key] switch { TextBox t => t.Text.Trim(), PasswordBox p => p.Password, ComboBox c => c.Text.Trim(), _ => "" };
    public decimal Decimal(string key)
    {
        string value = Value(key).Replace('.', ',');
        if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.GetCultureInfo("pt-BR"), out var result)) throw new InvalidOperationException("Informe valores válidos, sem separador de milhar.");
        return result;
    }
    public T Selected<T>(string key) => (T)((ComboBox)fields[key]).SelectedItem;
    public bool Checked(string key) => ((CheckBox)fields[key]).IsChecked == true;
    internal void ScrollToField(string key) { if (fields.TryGetValue(key, out var field)) field.BringIntoView(); }
}
