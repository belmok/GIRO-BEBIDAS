using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace GiroBebidas.App;

public partial class ReportsPanel : UserControl
{
    public ReportsPanel() => InitializeComponent();
    private MainWindow Host => Window.GetWindow(this) as MainWindow ?? throw new InvalidOperationException("Painel de resultados fora da janela principal.");
    private MainViewModel Vm => (MainViewModel)DataContext;
    private void Panel_Loaded(object sender, RoutedEventArgs e) => UpdateSortHeaders();
    private void ReportExport_Click(object sender, RoutedEventArgs e) => Host.ExportReportDashboard();
    private void ReportApply_Click(object sender, RoutedEventArgs e) { Host.ApplyReportDashboard(); UpdateSortHeaders(); }
    private void ReportClear_Click(object sender, RoutedEventArgs e) { Host.ClearReportDashboard(); UpdateSortHeaders(); }
    private void ReportDetails_Click(object sender, RoutedEventArgs e) => Host.ShowReportDetails(sender);
    private void ReportHistory_Click(object sender, RoutedEventArgs e) => Host.ShowReportProductHistory(sender);
    private void ReportRowMenu_Click(object sender, RoutedEventArgs e)
    {
        if (!Vm.IsAdmin || sender is not Button { ContextMenu: { } menu } button) return;
        Host.SelectReportProduct(sender);
        menu.PlacementTarget = button; menu.Placement = PlacementMode.Bottom;
        menu.HorizontalOffset = button.ActualWidth - menu.Width; menu.VerticalOffset = 4; menu.IsOpen = true;
    }
    private void ReportGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        Vm.SetReportSort(e.Column.SortMemberPath, Vm.ReportSortMember == e.Column.SortMemberPath ? !Vm.ReportSortDescending : false);
        UpdateSortHeaders();
    }
    internal void UpdateSortHeaders()
    {
        if (DataContext is not MainViewModel vm) return;
        foreach (var column in ReportGrid.Columns)
            column.SortDirection = column.SortMemberPath == vm.ReportSortMember
                ? vm.ReportSortDescending ? ListSortDirection.Descending : ListSortDirection.Ascending : null;
    }
}
