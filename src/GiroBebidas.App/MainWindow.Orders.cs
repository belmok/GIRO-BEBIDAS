using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public partial class MainWindow
{
    private Order OrderRow(object sender)
    {
        if (Vm.Service.CurrentUser is null) throw new UnauthorizedAccessException("Entre novamente no sistema.");
        var selected = sender is FrameworkElement { DataContext: Order row } ? row : Require(Vm.SelectedOrder, "um pedido");
        var order = Vm.Service.Snapshot.Orders.FirstOrDefault(o => o.Id == selected.Id)
            ?? throw new InvalidOperationException("Este pedido não está mais disponível.");
        Vm.SelectedOrder = Vm.OrderPageItems.FirstOrDefault(o => o.Id == order.Id);
        if (sender is MenuItem item && ItemsControl.ItemsControlFromItemContainer(item) is ContextMenu menu) menu.IsOpen = false;
        return order;
    }
    private void OrderRowMenu_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var button = (Button)sender; _ = OrderRow(sender);
            var menu = button.ContextMenu ?? throw new InvalidOperationException("Menu do pedido indisponível.");
            menu.PlacementTarget = button; menu.Placement = PlacementMode.Bottom;
            menu.HorizontalOffset = button.ActualWidth - menu.Width; menu.VerticalOffset = 4; menu.IsOpen = true;
        }
        catch (Exception ex) { Notify(ex.Message); }
    }
    private void OrderDetails_Click(object sender, RoutedEventArgs e) => Guard(() => CreateOrderDetailsDialog(OrderRow(sender)).ShowDialog());
    private void OrderRowPrint_Click(object sender, RoutedEventArgs e) => PrintOrder_Click(sender, e);
    private void OrderRowStatus_Click(object sender, RoutedEventArgs e) => OrderStatus_Click(sender, e);
    private void OrderRowReceive_Click(object sender, RoutedEventArgs e) => LoadOrder_Click(sender, e);

    internal OrderDetailsWindow CreateOrderDetailsDialog(Order selected)
    {
        if (Vm.Service.CurrentUser is null) throw new UnauthorizedAccessException("Entre novamente no sistema.");
        var state = Vm.Service.Snapshot;
        var order = state.Orders.FirstOrDefault(o => o.Id == selected.Id) ?? throw new InvalidOperationException("Pedido não encontrado.");
        return new OrderDetailsWindow(this, order, state);
    }
    internal FormDialog CreateOrderStatusDialog(Order selected)
    {
        if (Vm.Service.CurrentUser is null) throw new UnauthorizedAccessException("Entre novamente no sistema.");
        var order = Vm.Service.Snapshot.Orders.FirstOrDefault(o => o.Id == selected.Id) ?? throw new InvalidOperationException("Pedido não encontrado.");
        if (!order.IsOpen) throw new InvalidOperationException("Este pedido já está encerrado.");
        var choices = new[] { OrderStatus.Pendente, OrderStatus.Preparando, OrderStatus.EmEntrega, OrderStatus.Cancelado }
            .Where(status => order.Delivery || status != OrderStatus.EmEntrega).Select(status => new OrderSituationChoice(status, Labels.Order(status))).ToList();
        var dialog = new FormDialog(this, $"Situação • {order.NumberLabel}", "Para concluir o pedido e registrar o pagamento, use Receber no PDV.");
        dialog.Choice("status", "Situação", choices, choices.FirstOrDefault(c => c.Status == order.Status), nameof(OrderSituationChoice.Name));
        dialog.Submit = form => Vm.Service.SetOrderStatus(order.Id, form.Selected<OrderSituationChoice>("status").Status);
        return dialog;
    }
    internal sealed record OrderSituationChoice(OrderStatus Status, string Name);
}
