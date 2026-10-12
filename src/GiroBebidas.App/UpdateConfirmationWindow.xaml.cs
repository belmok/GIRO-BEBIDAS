using System.IO;
using System.Windows;
using System.Windows.Input;

namespace GiroBebidas.App;

public partial class UpdateConfirmationWindow : Window
{
    public UpdateConfirmationWindow(Window owner, Version version, string backupPath)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);
        InitializeComponent();
        Owner = owner;
        VersionLabel.Text = version.ToString(3);
        BackupPathBox.Text = Path.GetFullPath(backupPath);
        BackupFileLabel.Text = Path.GetFileName(BackupPathBox.Text);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e) => ConfirmButton.Focus();
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
