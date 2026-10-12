using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;

namespace GiroBebidas.App;

public partial class ConfirmationWindow : Window
{
    public static readonly DependencyProperty NoticeTextProperty = DependencyProperty.Register(
        nameof(NoticeText), typeof(string), typeof(ConfirmationWindow), new PropertyMetadata(""));

    public string NoticeText
    {
        get => (string)GetValue(NoticeTextProperty);
        set => SetValue(NoticeTextProperty, value ?? "");
    }

    public ConfirmationWindow(Window owner, string title, string message, string confirmText = "Confirmar")
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmText);
        InitializeComponent();
        Owner = owner;
        Title = $"Giro Bebidas • {title}";
        TitleLabel.Text = title;
        MessageLabel.Text = message;
        ConfirmLabel.Text = confirmText;
        AutomationProperties.SetName(ConfirmButton, confirmText);
        MaxWidth = Math.Max(1, SystemParameters.WorkArea.Width - 24);
        MaxHeight = Math.Max(1, SystemParameters.WorkArea.Height - 24);
        Width = Math.Min(580, MaxWidth);
        MessageScrollViewer.MaxHeight = Math.Max(40, MaxHeight - 246);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        FocusManager.SetFocusedElement(this, CancelButton);
        CancelButton.Focus();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        DialogResult = false;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || CloseButton.IsMouseOver) return;
        DragMove();
        e.Handled = true;
    }
}
