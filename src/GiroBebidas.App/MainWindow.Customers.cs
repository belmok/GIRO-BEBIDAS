using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public partial class MainWindow
{
    private Customer CustomerRowNow(object sender)
    {
        if (Vm.Service.CurrentUser is null) throw new UnauthorizedAccessException("Entre novamente no sistema.");
        var row = sender is FrameworkElement { DataContext: CustomerRow selected } ? selected : Require(Vm.SelectedCustomer, "um cliente");
        var customer = Vm.Service.Snapshot.Customers.FirstOrDefault(c => c.Id == row.Customer.Id)
            ?? throw new InvalidOperationException("Cliente não encontrado.");
        Vm.SelectedCustomer = Vm.CustomerRows.FirstOrDefault(c => c.Customer.Id == customer.Id);
        if (sender is MenuItem item && ItemsControl.ItemsControlFromItemContainer(item) is ContextMenu menu) menu.IsOpen = false;
        return customer;
    }
    internal FormDialog CreateCustomerEditor(Customer customer, bool creating = false)
    {
        if (Vm.Service.CurrentUser is null) throw new UnauthorizedAccessException("Entre novamente no sistema.");
        var c = creating ? customer : Vm.Service.Snapshot.Customers.FirstOrDefault(item => item.Id == customer.Id)
            ?? throw new InvalidOperationException("Este cliente não está mais disponível.");
        var dialog = new FormDialog(this, "Cadastro de cliente", "Os dados de entrega poderão ser usados nos pedidos e cupons.", "Salvar cliente");
        dialog.Text("name", "Nome", c.Name); dialog.Text("phone", "Telefone", c.Phone); dialog.Text("doc", "CPF / CNPJ", c.Document);
        dialog.Text("address", "Endereço de entrega", c.Address); dialog.Number("limit", "Limite de fiado (R$)", c.CreditLimit);
        if (c.CreditBlocked) dialog.Info("Fiado bloqueado. A edição do cadastro mantém o bloqueio e o saldo devedor.");
        dialog.Text("notes", "Observações", c.Notes, true);
        dialog.Submit = f =>
        {
            if (!creating && !Vm.Service.Snapshot.Customers.Any(item => item.Id == c.Id)) throw new InvalidOperationException("Este cliente não está mais disponível.");
            Vm.Service.SaveCustomer(new() { Id = c.Id, Name = f.Value("name"), Phone = f.Value("phone"), Document = f.Value("doc"), Address = f.Value("address"), CreditLimit = f.Decimal("limit"), Notes = f.Value("notes"), CreditBlocked = c.CreditBlocked });
        };
        return dialog;
    }
    internal FormDialog CreateCustomerReceipt(Customer selected)
    {
        var state = Vm.Service.Snapshot;
        if (Vm.Service.CurrentUser is null) throw new UnauthorizedAccessException("Entre novamente no sistema.");
        var customer = state.Customers.FirstOrDefault(c => c.Id == selected.Id) ?? throw new InvalidOperationException("Cliente não encontrado.");
        decimal balance = StoreService.Balance(state, customer.Id);
        if (!state.CashSessions.Any(c => c.ClosedAt is null)) throw new InvalidOperationException("Abra o caixa antes de receber fiado.");
        if (balance <= 0) throw new InvalidOperationException("Este cliente não possui saldo devedor.");
        var dialog = new FormDialog(this, "Receber fiado", customer.Name, "Registrar recebimento");
        dialog.Summary("Saldo do cliente", [("A receber", balance.ToString("C2")), ("Fiado", customer.CreditBlocked ? "Bloqueado para novas vendas" : "Disponível dentro do limite")]);
        dialog.Number("amount", "Valor recebido (R$)", balance);
        dialog.Choice("method", "Forma de pagamento", new[] { PaymentMethod.Dinheiro, PaymentMethod.Pix, PaymentMethod.Debito, PaymentMethod.Credito });
        dialog.Submit = f => Vm.Service.ReceiveDebt(customer.Id, f.Decimal("amount"), f.Selected<PaymentMethod>("method"));
        return dialog;
    }
    internal DetailWindow CreateCustomerDetails(Customer selected, bool statement = false)
    {
        if (Vm.Service.CurrentUser is null) throw new UnauthorizedAccessException("Entre novamente no sistema.");
        var state = Vm.Service.Snapshot;
        var customer = state.Customers.FirstOrDefault(c => c.Id == selected.Id) ?? throw new InvalidOperationException("Cliente não encontrado.");
        var dialog = new CustomerDetailsWindow(this, customer, state, statement);
        return dialog;
    }
    internal FormDialog CreateCustomerCreditDialog(Customer selected, bool delete = false)
    {
        if (!Vm.Service.IsAdmin) throw new UnauthorizedAccessException("Esta ação exige um administrador.");
        var state = Vm.Service.Snapshot; var c = state.Customers.FirstOrDefault(c => c.Id == selected.Id) ?? throw new InvalidOperationException("Cliente não encontrado.");
        if (delete && !StoreService.CanDeleteCustomer(state, c.Id)) throw new InvalidOperationException("Este cliente possui histórico e não pode ser excluído.");
        bool blocked = !c.CreditBlocked;
        var dialog = new FormDialog(this, delete ? "Excluir cliente" : blocked ? "Bloquear fiado" : "Liberar fiado", c.Name,
            delete ? "Confirmar exclusão" : blocked ? "Confirmar bloqueio" : "Confirmar liberação");
        dialog.Summary("Cliente selecionado", [("Nome", c.Name), ("Limite", c.CreditLimit.ToString("C2")), ("Saldo devedor", StoreService.Balance(state, c.Id).ToString("C2"))]);
        dialog.Info(delete ? "O cadastro será removido. A exclusão só é permitida para clientes sem vendas ou pedidos." : blocked ? "Novas vendas em fiado serão impedidas. Você poderá registrar recebimentos e consultar o histórico." : "Novas vendas em fiado poderão ser registradas dentro do limite de crédito.");
        dialog.Submit = _ => { if (delete) Vm.Service.DeleteCustomer(c.Id); else Vm.Service.SetCustomerCreditBlocked(c.Id, blocked); };
        return dialog;
    }
    private void ShowCustomerEditor(Customer customer, bool creating = false)
    {
        if (CreateCustomerEditor(customer, creating).ShowDialog() != true) return;
        Vm.Refresh(); Vm.SelectedCustomer = Vm.CustomerRows.FirstOrDefault(c => c.Customer.Id == customer.Id); Notify("Cliente salvo.");
    }
    internal void CustomerNew(object sender, RoutedEventArgs e) { try { ShowCustomerEditor(new(), true); } catch (Exception ex) { Notify(ex.Message); } }
    internal void CustomerEdit(object sender, RoutedEventArgs e) { try { ShowCustomerEditor(CustomerRowNow(sender)); } catch (Exception ex) { Notify(ex.Message); } }
    internal void CustomerDuplicate(object sender, RoutedEventArgs e)
    {
        try { var c = CustomerRowNow(sender); ShowCustomerEditor(new() { Name = c.Name + " • cópia", Phone = c.Phone, Address = c.Address, CreditLimit = c.CreditLimit, Notes = c.Notes }, true); }
        catch (Exception ex) { Notify(ex.Message); }
    }
    internal void CustomerDetails(object sender, RoutedEventArgs e) { try { CreateCustomerDetails(CustomerRowNow(sender)).ShowDialog(); } catch (Exception ex) { Notify(ex.Message); } }
    internal void CustomerStatement(object sender, RoutedEventArgs e) { try { CreateCustomerDetails(CustomerRowNow(sender), true).ShowDialog(); } catch (Exception ex) { Notify(ex.Message); } }
    internal void CustomerReceive(object sender, RoutedEventArgs e)
    {
        try { if (CreateCustomerReceipt(CustomerRowNow(sender)).ShowDialog() == true) { Vm.Refresh(); Notify("Recebimento registrado; saldo e caixa atualizados."); } }
        catch (Exception ex) { Notify(ex.Message); }
    }
    internal void CustomerCredit(object sender, RoutedEventArgs e) => ChangeCustomerCredit(sender, false);
    internal void CustomerDelete(object sender, RoutedEventArgs e) => ChangeCustomerCredit(sender, true);
    private void ChangeCustomerCredit(object sender, bool delete)
    {
        try { if (CreateCustomerCreditDialog(CustomerRowNow(sender), delete).ShowDialog() == true) { Vm.Refresh(); Notify(delete ? "Cliente excluído." : "Situação do fiado atualizada."); } }
        catch (Exception ex) { Notify(ex.Message); }
    }
    internal void CustomerMenu(object sender, RoutedEventArgs e)
    {
        try
        {
            var customer = CustomerRowNow(sender); var button = (Button)sender;
            var menu = button.ContextMenu ?? throw new InvalidOperationException("Menu do cliente indisponível.");
            var row = new CustomerRow(customer, StoreService.Balance(Vm.Service.Snapshot, customer.Id), Vm.IsAdmin && StoreService.CanDeleteCustomer(Vm.Service.Snapshot, customer.Id));
            menu.DataContext = row; menu.PlacementTarget = button; menu.Placement = PlacementMode.Bottom;
            menu.HorizontalOffset = button.ActualWidth - menu.Width; menu.VerticalOffset = 4;
            foreach (var item in menu.Items.OfType<MenuItem>())
            {
                if (Equals(item.Header, "Registrar recebimento")) item.IsEnabled = row.HasDebt && Vm.Service.Snapshot.CashSessions.Any(c => c.ClosedAt is null);
                if (Equals(item.Header, "Bloquear fiado") || Equals(item.Header, "Liberar fiado")) item.IsEnabled = Vm.IsAdmin;
            }
            menu.IsOpen = true;
        }
        catch (Exception ex) { Notify(ex.Message); }
    }
}

internal sealed class CustomerDetailsWindow : DetailWindow
{
    public CustomerDetailsWindow(Window owner, Customer customer, StoreState state, bool statement)
        : base(owner, statement ? "Extrato de fiados" : "Detalhes do cliente", customer.Name, "\uE716")
    {
        decimal balance = StoreService.Balance(state, customer.Id);
        AddSection("Cadastro", Rows([("Telefone", customer.Phone), ("CPF / CNPJ", customer.Document), ("Endereço", customer.Address)]));
        AddSection("Crédito do cliente", Rows([("Limite de fiado", customer.CreditLimit.ToString("C2")), ("Saldo devedor", balance.ToString("C2")), ("Disponível no limite", Math.Max(0, customer.CreditLimit - balance).ToString("C2")), ("Novas vendas em fiado", customer.CreditBlocked ? "Bloqueadas" : "Permitidas dentro do limite")]));
        if (statement)
        {
            var sales = state.Sales.Where(s => s.CustomerId == customer.Id && !s.Canceled && s.PaymentMethod == PaymentMethod.Fiado).OrderByDescending(s => s.Date).ToArray();
            var body = new StackPanel();
            if (sales.Length == 0) body.Children.Add(Text("Nenhuma venda em fiado registrada.", 13, "#597164"));
            foreach (var sale in sales)
            {
                body.Children.Add(Text($"{sale.NumberLabel} • {sale.Date:dd/MM/yyyy HH:mm}", 14, "#243D31", FontWeights.SemiBold));
                body.Children.Add(Text($"Venda: {sale.Total:C2}  •  A receber: {StoreService.Outstanding(state, sale):C2}", 12, "#597164"));
                foreach (var p in state.ReceivablePayments.Where(p => p.SaleId == sale.Id).OrderBy(p => p.Date))
                    body.Children.Add(Text($"Recebimento {p.Date:dd/MM/yyyy HH:mm}: {p.Amount:C2} • {Labels.Payment(p.Method)} • {p.UserName}", 12, "#597164"));
                body.Children.Add(new Border { Height = 1, Background = ColorBrush("#DFEAE2"), Margin = new Thickness(0, 12, 0, 12) });
            }
            AddSection("Vendas e recebimentos", body);
        }
        if (!string.IsNullOrWhiteSpace(customer.Notes)) AddSection("Observações", Text(customer.Notes, 12, "#597164"));
    }
    private static StackPanel Rows((string Label, string Value)[] rows)
    {
        var body = new StackPanel();
        foreach (var row in rows)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 9) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.Children.Add(Text(row.Label, 12, "#597164"));
            var value = Text(string.IsNullOrWhiteSpace(row.Value) ? "Não informado" : row.Value, 13); Grid.SetColumn(value, 1); grid.Children.Add(value);
            body.Children.Add(grid);
        }
        return body;
    }
}
