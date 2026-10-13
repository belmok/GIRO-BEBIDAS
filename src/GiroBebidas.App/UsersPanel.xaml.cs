using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public partial class UsersPanel : UserControl
{
    public UsersPanel() => InitializeComponent();
    private MainWindow Main => (MainWindow)Window.GetWindow(this);
    private MainViewModel Vm => (MainViewModel)DataContext;
    private UserDashboardRow? Target(object sender)
    {
        if (sender is FrameworkElement element && element.DataContext is UserDashboardRow row) { Vm.SelectedUserRow = row; return row; }
        return Vm.SelectedUserRow;
    }
    private void New_Click(object sender, RoutedEventArgs e) => Main.ShowUserEditor(new UserAccount { Role = UserRole.Operador }, false);
    private void Edit_Click(object sender, RoutedEventArgs e) { if (Target(sender) is { } row) Main.ShowUserEditor(row.Profile, true); }
    private void Details_Click(object sender, RoutedEventArgs e) { if (Target(sender) is { } row) Main.ShowUserDetails(row.Profile); }
    private void ResetPassword_Click(object sender, RoutedEventArgs e) { if (Target(sender) is { } row) Main.ShowUserPasswordReset(row.Profile); }
    private void Duplicate_Click(object sender, RoutedEventArgs e) { if (Target(sender) is { } row) Main.ShowUserEditor(row.Profile, false, true); }
    private void Active_Click(object sender, RoutedEventArgs e) { if (Target(sender) is { CanChangeState: true } row) Main.ShowUserStateChange(row.Profile); }
    private void More_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button || Target(sender) is null) return;
        menu.PlacementTarget = button; menu.Placement = PlacementMode.Bottom;
        menu.HorizontalOffset = button.ActualWidth - menu.Width; menu.IsOpen = !menu.IsOpen;
        e.Handled = true;
    }
}
