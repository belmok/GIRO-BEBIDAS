using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public static partial class SmokeRunner
{
    private static void CheckCustomersDashboard(string output)
    {
        var service = new StoreService(new JsonStore(Path.Combine(output, "dados-clientes-" + Guid.NewGuid().ToString("N"))));
        service.Login("ADMIN", "ADMIN"); var cash = service.OpenCash(50);
        var marina = new Customer { Name = "Marina Souza", Phone = "11999990000", Document = "12345678900", Address = "Rua das Palmeiras, 175", CreditLimit = 500 };
        var ana = new Customer { Name = "Ana Lima", Address = "Rua dos Ipês, 85", CreditLimit = 200 };
        service.SaveCustomer(marina); service.SaveCustomer(ana);
        service.SaveUser(new() { Name = "Operador de clientes", Username = "CLIENTES_OP", Role = UserRole.Operador }, "1234");
        var product = service.Snapshot.Products[0];
        var sale = service.CompleteSale(new([new(product.Id, 2)], 0, 0, marina.Id, PaymentMethod.Fiado, 0));
        service.SetCustomerCreditBlocked(marina.Id, true);
        service.Store.Update(s => { for (int i = 0; i < 30; i++) s.Customers.Add(new() { Name = "Cliente de teste " + i.ToString("00"), Address = "Rua da conferência " + i }); return 0; });
        var window = new MainWindow(service, false) { Width = 1480, Height = 940, Left = -10000, Top = -10000, WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false, ShowInTaskbar = false };
        try
        {
            window.Show(); var vm = window.Vm; vm.Page = "Clientes"; FlushLayout(window);
            var panel = window.CustomersDashboard; var grid = panel.CustomersGrid;
            string State() => JsonSerializer.Serialize(service.Snapshot, JsonStore.JsonOptions);
            void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
            static void Click(Button b) => ((IInvokeProvider)new ButtonAutomationPeer(b).GetPattern(PatternInterface.Invoke)).Invoke();
            static void Invoke(MenuItem item) => ((IInvokeProvider)new MenuItemAutomationPeer(item).GetPattern(PatternInterface.Invoke)).Invoke();
            void Search(string query) { ((IValueProvider)new TextBoxAutomationPeer(panel.CustomerSearch).GetPattern(PatternInterface.Value)).SetValue(query); FlushLayout(window); }
            void Modal<T>(Action invoke, Action<T> examine) where T : Window
            {
                Exception? failure = null; bool handled = false; DateTime started = DateTime.UtcNow;
                var watcher = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
                watcher.Tick += (_, _) =>
                {
                    var dialog = Application.Current.Windows.OfType<T>().FirstOrDefault(d => d != window && d.IsVisible);
                    if (dialog is not null) { handled = true; watcher.Stop(); try { FlushLayout(dialog); examine(dialog); } catch (Exception ex) { failure = ex; dialog.Close(); } }
                    else if ((DateTime.UtcNow - started).TotalSeconds > 6) { watcher.Stop(); failure = new InvalidOperationException("A ação de clientes não abriu o formulário correspondente."); }
                };
                try { watcher.Start(); invoke(); while (!handled && failure is null) FlushLayout(window); if (failure is not null) throw failure; FlushLayout(window); }
                finally { watcher.Stop(); }
            }
            ContextMenu Menu(Guid id)
            {
                Search(""); var row = vm.CustomerRows.Single(c => c.Customer.Id == id); grid.ScrollIntoView(row); FlushLayout(window);
                var button = Descendants<Button>(grid).Single(b => b.Name == "CustomerRowMenuButton" && b.DataContext is CustomerRow c && c.Customer.Id == id);
                Click(button); FlushLayout(window); return button.ContextMenu!;
            }
            MenuItem Item(ContextMenu menu, string header) => menu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, header));
            string stable = State();
            Assert(panel.CustomerDebtValue.Text == sale.Total.ToString("C2"), "O cartão de fiados deve mostrar o saldo real de todos os clientes.");
            foreach (var query in new[] { "MARINA", "11999990000", "12345678900", "Palmeiras" }) { Search(query); Assert(vm.CustomerRows.Count == 1 && vm.CustomerRows[0].Customer.Id == marina.Id, "Buscar clientes deve localizar nome, telefone, documento e endereço."); }
            grid.SelectedItem = vm.CustomerRows[0]; vm.Refresh(); FlushLayout(window);
            Assert(vm.SelectedCustomer?.Customer.Id == marina.Id && vm.CustomerCanReceive, "Atualizar preserva o cliente selecionado e permite receber o fiado bloqueado.");
            Search("sem resultado de cliente"); Assert(vm.SelectedCustomer is null && !panel.CustomerReceiveButton.IsEnabled && !panel.CustomerEditButton.IsEnabled && vm.CustomerEmptyVisibility == "Visible", "Busca vazia deve limpar seleção e bloquear ações sem cliente.");
            Capture(window, Path.Combine(output, "106-clientes-sem-resultados.png")); Search("");
            Capture(window, Path.Combine(output, "104-clientes-fiados.png"));
            var menu = Menu(marina.Id); Assert(!Item(menu, "Excluir cliente").IsEnabled && Item(menu, "Registrar recebimento").IsEnabled && Item(menu, "Liberar fiado").IsEnabled, "Menu deve preservar cliente com histórico e permitir recebimentos do fiado bloqueado.");
            CaptureElement(menu, Path.Combine(output, "107-menu-cliente.png"));
            vm.SelectedCustomer = vm.CustomerRows.Single(c => c.Customer.Id == ana.Id);
            Modal<CustomerDetailsWindow>(() => Invoke(Item(menu, "Extrato do cliente")), dialog =>
            {
                Assert(dialog.HeaderSubtitleLabel.Text == marina.Name && Descendants<TextBlock>(dialog).Any(t => t.Text.Contains(sale.NumberLabel)), "Extrato deve usar o cliente da linha, mesmo com outra seleção.");
                Capture(dialog, Path.Combine(output, "108-extrato-cliente.png")); Click(dialog.CloseButton);
            });
            Assert(State() == stable, "Consultar o extrato não pode modificar os dados.");
            menu = Menu(marina.Id);
            Modal<FormDialog>(() => Invoke(Item(menu, "Editar cliente")), dialog => { dialog.Field<TextBox>("name").Text = "Nome cancelado"; Click(dialog.CancelButton); });
            Assert(State() == stable, "Cancelar edição de cliente deve manter os dados.");
            menu = Menu(marina.Id);
            Modal<FormDialog>(() => Invoke(Item(menu, "Duplicar cliente")), dialog =>
            {
                Assert(dialog.Field<TextBox>("doc").Text == "" && dialog.Field<TextBox>("name").Text.Contains("cópia"), "Duplicar cliente precisa criar cadastro independente sem copiar o documento.");
                dialog.Field<TextBox>("name").Text = "Cliente copiado da conferência"; Click(dialog.SubmitButton);
            });
            var copy = service.Snapshot.Customers.Single(c => c.Name == "Cliente copiado da conferência");
            Assert(copy.Id != marina.Id && !copy.CreditBlocked && StoreService.Balance(service.Snapshot, copy.Id) == 0 && copy.Document == "", "Cópia não pode compartilhar dívida, bloqueio ou identidade do cliente original.");
            menu = Menu(marina.Id); stable = State();
            Modal<FormDialog>(() => Invoke(Item(menu, "Registrar recebimento")), dialog =>
            {
                dialog.Field<TextBox>("amount").Text = (sale.Total + 1).ToString("0.00"); Click(dialog.SubmitButton); FlushLayout(dialog);
                Assert(dialog.IsVisible && State() == stable, "Recebimento acima da dívida deve permanecer aberto e preservar os dados.");
                dialog.Field<TextBox>("amount").Text = "1,00"; dialog.Field<ComboBox>("method").SelectedItem = PaymentMethod.Pix;
                Capture(dialog, Path.Combine(output, "109-receber-fiado.png")); Click(dialog.SubmitButton);
            });
            Assert(StoreService.Balance(service.Snapshot, marina.Id) == sale.Total - 1 && vm.TotalDebt == sale.Total - 1 && StoreService.SummarizeCash(service.Snapshot, cash.Id).Pix == 1, "Receber fiado deve baixar exatamente o saldo e registrar o valor no Pix do caixa.");
            menu = Menu(marina.Id); stable = State();
            Modal<FormDialog>(() => Invoke(Item(menu, "Liberar fiado")), d => Click(d.CancelButton)); Assert(State() == stable, "Cancelar liberação não altera o bloqueio.");
            menu = Menu(marina.Id); Modal<FormDialog>(() => Invoke(Item(menu, "Liberar fiado")), d => Click(d.SubmitButton));
            Assert(!service.Snapshot.Customers.Single(c => c.Id == marina.Id).CreditBlocked, "A liberação confirmada deve atualizar o fiado.");
            menu = Menu(copy.Id); Modal<FormDialog>(() => Invoke(Item(menu, "Excluir cliente")), d => Click(d.SubmitButton)); Assert(!service.Snapshot.Customers.Any(c => c.Id == copy.Id), "Excluir cliente sem histórico deve remover somente seu cadastro.");
            stable = State(); bool absentRejected = false;
            try { window.CreateCustomerEditor(copy).Close(); } catch (InvalidOperationException) { absentRejected = true; }
            Assert(absentRejected && State() == stable, "O editor não pode recriar um identificador antigo de cliente ausente.");
            window.Width = 1200; window.Height = 760; Search(""); FlushLayout(window);
            foreach (var button in new[] { panel.CustomerStatementButton, panel.CustomerReceiveButton, panel.CustomerEditButton, panel.CustomerNewButton })
            {
                var bounds = button.TransformToAncestor(panel).TransformBounds(new Rect(button.RenderSize));
                Assert(bounds.Width > 0 && bounds.Left >= 0 && bounds.Right <= panel.ActualWidth + 1 && bounds.Bottom <= panel.ActualHeight + 1, "Botões de clientes precisam ficar acessíveis na janela menor.");
            }
            Capture(window, Path.Combine(output, "105-clientes-tela-menor.png"));
            var scroll = Descendants<ScrollViewer>(grid).First(); scroll.ScrollToTop(); FlushLayout(window);
            void Wheel(int delta) => grid.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta) { RoutedEvent = UIElement.PreviewMouseWheelEvent, Source = grid });
            Wheel(-20); FlushLayout(window); Assert(scroll.VerticalOffset > 0 && scroll.VerticalOffset < 10, "Touchpad de clientes deve responder a pequenos deltas.");
            scroll.ScrollToTop(); FlushLayout(window); for (int i = 0; i < 6; i++) Wheel(-20); FlushLayout(window);
            Assert(Math.Abs(scroll.VerticalOffset - 22) < 1, "Gestos consecutivos de clientes não podem ser perdidos.");
            scroll.ScrollToEnd(); FlushLayout(window); Wheel(-120); FlushLayout(window); Assert(Math.Abs(scroll.VerticalOffset - scroll.ScrollableHeight) < 1, "A rolagem de clientes deve respeitar o limite final.");
            service.CloseCash(StoreService.SummarizeCash(service.Snapshot, cash.Id).Expected, "Teste"); vm.Refresh(); vm.SelectedCustomer = vm.CustomerRows.Single(c => c.Customer.Id == marina.Id);
            Assert(!vm.CustomerCanReceive, "Recebimento deve ficar indisponível com caixa fechado.");
            service.Login("CLIENTES_OP", "1234"); vm.Refresh(); menu = Menu(marina.Id);
            Assert(!Item(menu, "Bloquear fiado").IsEnabled && !Item(menu, "Excluir cliente").IsEnabled, "Operador não pode bloquear fiado ou excluir cliente."); menu.IsOpen = false;
            bool denied = false; try { window.CreateCustomerCreditDialog(marina).Close(); } catch (UnauthorizedAccessException) { denied = true; }
            Assert(denied, "A fábrica de alteração de fiado deve validar a permissão atual.");
            File.WriteAllText(Path.Combine(output, "clientes-smoke-ok.txt"), "Clientes: valores, busca por nome/telefone/documento/endereço, seleção por ID, menus por linha, extrato, edição cancelada, cópia independente, recebimento validado, Pix e saldo, bloqueio/liberação, exclusão sem histórico, permissões, caixa fechado, layout 1200 x 760 e touchpad conferidos em dados isolados.");
        }
        finally { window.Close(); }
    }
}
