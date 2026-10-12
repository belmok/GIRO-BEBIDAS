using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public static partial class SmokeRunner
{
    private static void CheckOrdersDashboard(string output)
    {
        var store = new JsonStore(Path.Combine(output, "dados-pedidos-teste-" + Guid.NewGuid().ToString("N")));
        var service = new StoreService(store); service.Login("ADMIN", "ADMIN"); service.OpenCash(100);
        var marina = new Customer { Name = "Marina Souza", Address = "Rua das Palmeiras, 175", Phone = "(11) 99999-0000" };
        var ana = new Customer { Name = "Ana Lima", Address = "Rua dos Ipês, 85" };
        service.SaveCustomer(marina); service.SaveCustomer(ana);
        var product = service.Snapshot.Products[0];
        for (int i = 0; i < 22; i++)
        {
            var customer = i % 2 == 0 ? marina : ana;
            bool delivery = i % 2 == 0 || i % 5 == 2;
            var order = service.CreateOrder([new(product.Id, 1)], customer.Id, delivery, customer.Address, $"Observação de teste {i + 1}", 1, delivery ? 5 : 0);
            if (i % 5 == 4) service.CompleteSale(new([new(product.Id, 1)], order.Discount, order.DeliveryFee, customer.Id, PaymentMethod.Pix, order.Total, order.Notes, order.Address, order.Id));
            else if (i % 5 > 0) service.SetOrderStatus(order.Id, (i % 5) switch { 1 => OrderStatus.Preparando, 2 => OrderStatus.EmEntrega, _ => OrderStatus.Cancelado });
            int days = i switch { 0 => 40, 1 => 30, 2 => 29, 3 => 7, 4 => 6, _ => 0 };
            store.Update(state => { state.Orders.Single(o => o.Id == order.Id).Date = new DateTimeOffset(DateTime.Today.AddDays(-days).AddHours(9).AddMinutes(i)); return 0; });
        }
        var window = new MainWindow(service, false) { Width = 1480, Height = 940 };
        try
        {
            var vm = window.Vm; vm.Page = "Pedidos";
            Capture(window, Path.Combine(output, "53-pedidos-e-entregas.png"));
            window.Width = 1480; window.Height = 940; FlushLayout(window);
            static void Click(Button button) => ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            string State() => JsonSerializer.Serialize(service.Snapshot, JsonStore.JsonOptions);
            string before = State(); decimal reserved = StoreService.Reserved(service.Snapshot, product.Id); int openCount = vm.OpenOrderCount;
            if (vm.Orders.Count != 22 || vm.OrderFilteredCount != 22 || vm.OrderPageItems.Count != 10 || vm.OrderPageCount != 3 || window.OrdersPreviousButton.IsEnabled || !window.OrdersNextButton.IsEnabled)
                throw new InvalidOperationException("O histórico deve mostrar todos os pedidos e iniciar na primeira página de dez registros.");
            Click(window.OrdersNextButton); FlushLayout(window);
            if (vm.OrderPageNumber != 2 || vm.OrderPageItems.Count != 10) throw new InvalidOperationException("A próxima página precisa mostrar o segundo grupo de pedidos.");
            var selected = vm.OrderPageItems.First(); window.OrdersGrid.SelectedItem = selected; vm.Refresh(); FlushLayout(window);
            if (vm.SelectedOrder?.Id != selected.Id || !vm.OrderPageItems.Contains(vm.SelectedOrder)) throw new InvalidOperationException("Atualizar pedidos precisa preservar a seleção usando a linha atual.");
            Click(window.OrdersNextButton); FlushLayout(window);
            if (vm.OrderPageNumber != 3 || vm.OrderPageItems.Count != 2 || window.OrdersNextButton.IsEnabled || vm.SelectedOrder is not null)
                throw new InvalidOperationException("A última página deve respeitar o total e remover uma seleção fora da página.");
            vm.OrderNextPageCommand.Execute(null); FlushLayout(window);
            if (vm.OrderPageNumber != 3) throw new InvalidOperationException("A paginação não pode ultrapassar a última página.");
            Click(window.OrdersPreviousButton); FlushLayout(window);
            window.OrdersPageSizeFilter.SelectedItem = 20; FlushLayout(window);
            if (vm.OrderPageNumber != 1 || vm.OrderPageItems.Count != 20 || vm.OrderPageCount != 2) throw new InvalidOperationException("Alterar o tamanho precisa retornar à primeira página.");
            window.OrdersPageSizeFilter.SelectedItem = 50; FlushLayout(window);
            if (vm.OrderPageItems.Count != 22 || window.OrdersNextButton.IsEnabled) throw new InvalidOperationException("A página de cinquenta registros precisa mostrar o histórico inteiro deste teste.");
            window.OrdersPageSizeFilter.SelectedItem = 10; FlushLayout(window);
            foreach (var (period, count) in new[] { ("Hoje", 17), ("Últimos 7 dias", 18), ("Últimos 30 dias", 20), ("Todo o período", 22) })
            {
                window.OrdersPeriodFilter.SelectedItem = period; FlushLayout(window);
                if (vm.OrderFilteredCount != count || vm.OrderPageNumber != 1 || vm.OpenOrderCount != openCount)
                    throw new InvalidOperationException($"O filtro {period} precisa respeitar os limites das datas sem mudar a quantidade total de pedidos abertos.");
            }
            foreach (var status in Enum.GetValues<OrderStatus>())
            {
                window.OrdersStatusFilter.SelectedItem = Labels.Order(status); FlushLayout(window);
                if (vm.OrderFilteredCount != service.Snapshot.Orders.Count(o => o.Status == status) || vm.OrderPageItems.Any(o => o.Status != status))
                    throw new InvalidOperationException("O filtro da situação precisa mostrar somente os pedidos correspondentes.");
            }
            window.OrdersStatusFilter.SelectedItem = "Todas as situações"; FlushLayout(window);
            var search = (IValueProvider)new TextBoxAutomationPeer(window.OrdersSearch).GetPattern(PatternInterface.Value);
            var probe = vm.Orders.First();
            foreach (string query in new[] { probe.NumberLabel.ToLowerInvariant(), "marina", "IPÊS", "(11) 99999-0000" })
            {
                search.SetValue(query); FlushLayout(window);
                var expected = vm.Orders.Where(o => new[] { o.NumberLabel, o.CustomerName, o.Phone, o.Address }.Any(text => text.Contains(query, StringComparison.CurrentCultureIgnoreCase))).ToArray();
                if (vm.OrderFilteredCount != expected.Length || vm.OrderPageItems.Any(o => !expected.Any(match => match.Id == o.Id)))
                    throw new InvalidOperationException("A pesquisa precisa encontrar pedidos, clientes e endereços sem distinguir maiúsculas.");
                AssertSearchReadable(window.OrdersSearch);
            }
            search.SetValue("pedido-inexistente-" + Guid.NewGuid().ToString("N")); FlushLayout(window);
            if (vm.OrderFilteredCount != 0 || vm.OrderEmptyVisibility != "Visible" || window.OrdersPrintButton.IsEnabled || window.OrdersStatusButton.IsEnabled || window.OrdersReceiveButton.IsEnabled || window.OrdersNextButton.IsEnabled)
                throw new InvalidOperationException("Uma pesquisa vazia deve informar que não há pedidos e desativar ações sem seleção.");
            Capture(window, Path.Combine(output, "55-pedidos-sem-resultados.png")); search.SetValue(""); FlushLayout(window);
            if (State() != before || StoreService.Reserved(service.Snapshot, product.Id) != reserved) throw new InvalidOperationException("Pesquisar, filtrar e paginar não pode mudar pedidos ou suas reservas.");
            Button More(Order order)
            {
                window.OrdersGrid.ScrollIntoView(order, window.OrdersGrid.Columns.Last()); FlushLayout(window);
                return Descendants<Button>(window.OrdersGrid).Single(b => b.Name == "OrderMoreButton" && b.DataContext is Order row && row.Id == order.Id);
            }
            foreach (bool closed in new[] { true, false })
            {
                var order = vm.OrderPageItems.First(o => o.IsOpen != closed);
                var button = More(order); Click(button); FlushLayout(window);
                var menu = button.ContextMenu ?? throw new InvalidOperationException("O pedido precisa ter menu de ações.");
                if (!menu.IsOpen || menu.DataContext is not Order row || row.Id != order.Id || menu.Items.Count != 4 || vm.SelectedOrder?.Id != order.Id || !vm.OrderPageItems.Contains(vm.SelectedOrder) ||
                    !((MenuItem)menu.Items[1]).IsEnabled || ((MenuItem)menu.Items[2]).IsEnabled == closed || ((MenuItem)menu.Items[3]).IsEnabled == closed ||
                    !window.OrdersPrintButton.IsEnabled || window.OrdersStatusButton.IsEnabled == closed || window.OrdersReceiveButton.IsEnabled == closed)
                    throw new InvalidOperationException("As ações devem usar a linha correta e impedir alteração ou recebimento de pedidos encerrados.");
                if (!closed) CaptureElement(menu, Path.Combine(output, "56-menu-pedido.png")); menu.IsOpen = false;
            }
            var detailOrder = vm.OrderPageItems.First(o => o.IsOpen && o.Delivery);
            var details = window.CreateOrderDetailsDialog(detailOrder);
            try { details.Owner = null; details.SizeToContent = SizeToContent.Manual; details.Height = Math.Min(650, details.MaxHeight); Capture(details, Path.Combine(output, "57-detalhes-pedido.png")); }
            finally { details.Close(); }
            string receipt = ReceiptText.Order(service.Snapshot.Settings, detailOrder);
            if (!receipt.Contains(detailOrder.NumberLabel, StringComparison.Ordinal) || !receipt.Contains(detailOrder.Address, StringComparison.Ordinal) || !receipt.Contains("NÃO COMPROVA PAGAMENTO", StringComparison.Ordinal))
                throw new InvalidOperationException("O comprovante precisa identificar o pedido, o endereço e sua natureza de pedido.");
            foreach (var order in vm.Orders.Where(o => o.IsOpen).GroupBy(o => o.Delivery).Select(group => group.First()))
            {
                var dialog = window.CreateOrderStatusDialog(order);
                try
                {
                    dialog.Owner = null; dialog.SizeToContent = SizeToContent.Manual; dialog.Height = Math.Min(430, dialog.MaxHeight);
                    Capture(dialog, Path.Combine(output, order.Delivery ? "58-situacao-entrega.png" : "59-situacao-retirada.png"));
                    var choice = Descendants<ComboBox>((FrameworkElement)dialog.Content).Single();
                    if (choice.Items.Cast<MainWindow.OrderSituationChoice>().Any(c => c.Status == OrderStatus.Concluido || (!order.Delivery && c.Status == OrderStatus.EmEntrega)))
                        throw new InvalidOperationException("Concluir exige pagamento; Em entrega deve estar disponível somente para entrega.");
                }
                finally { dialog.Close(); }
            }
            bool rejected = false;
            try { window.CreateOrderStatusDialog(vm.Orders.First(o => !o.IsOpen)).Close(); } catch (InvalidOperationException) { rejected = true; }
            if (!rejected || State() != before) throw new InvalidOperationException("Detalhes e formulários cancelados devem preservar o histórico; pedido encerrado não muda de situação.");
            var statusOrder = vm.OrderPageItems.First(o => o.Status == OrderStatus.Pendente);
            var statusDialog = window.CreateOrderStatusDialog(statusOrder);
            try
            {
                statusDialog.Owner = null; statusDialog.WindowStartupLocation = WindowStartupLocation.Manual;
                statusDialog.Left = -10000; statusDialog.Top = -10000; statusDialog.ShowInTaskbar = false; statusDialog.ShowActivated = false;
                statusDialog.Show(); FlushLayout(statusDialog);
                var choice = Descendants<ComboBox>((FrameworkElement)statusDialog.Content).Single();
                choice.SelectedItem = choice.Items.Cast<MainWindow.OrderSituationChoice>().Single(item => item.Status == OrderStatus.Preparando);
                statusDialog.Submit?.Invoke(statusDialog);
            }
            finally { statusDialog.Close(); }
            vm.Refresh(); FlushLayout(window);
            if (service.Snapshot.Orders.Single(o => o.Id == statusOrder.Id).Status != OrderStatus.Preparando || StoreService.Reserved(service.Snapshot, product.Id) != reserved || vm.OpenOrderCount != openCount)
                throw new InvalidOperationException("Alterar a situação pelo formulário deve atualizar o pedido sem cobrar ou consumir sua reserva.");
            Capture(window, Path.Combine(output, "53-pedidos-e-entregas.png"));
            window.Width = 1200; window.Height = 760; FlushLayout(window);
            foreach (var element in new FrameworkElement[] { window.OrdersSearch, window.OrdersPeriodFilter, window.OrdersStatusFilter, window.OrdersPrintButton, window.OrdersStatusButton, window.OrdersReceiveButton, window.OrdersPreviousButton, window.OrdersNextButton, window.OrdersPageSizeFilter })
            {
                var root = (FrameworkElement)window.Content; var bounds = element.TransformToAncestor(root).TransformBounds(new Rect(element.RenderSize));
                if (!element.IsVisible || bounds.Left < -.5 || bounds.Top < -.5 || bounds.Right > root.ActualWidth + .5 || bounds.Bottom > root.ActualHeight + .5)
                    throw new InvalidOperationException($"A tela menor não pode recortar {element.Name}.");
            }
            Capture(window, Path.Combine(output, "54-pedidos-tela-menor.png"));
            var sourceOrder = vm.OrderPageItems.First(o => o.IsOpen); decimal stock = service.Snapshot.Products.Single(p => p.Id == product.Id).Stock;
            var changedProduct = service.Snapshot.Products.Single(p => p.Id == product.Id); changedProduct.Price = 99; service.SaveProduct(changedProduct); vm.Refresh(); FlushLayout(window);
            sourceOrder = vm.OrderPageItems.Single(o => o.Id == sourceOrder.Id); var receiveButton = More(sourceOrder); Click(receiveButton); FlushLayout(window);
            var receive = (MenuItem)receiveButton.ContextMenu!.Items[3]; before = State();
            ((IInvokeProvider)new MenuItemAutomationPeer(receive).GetPattern(PatternInterface.Invoke)).Invoke(); FlushLayout(window);
            if (vm.Page != "PDV" || vm.ActiveOrderId != sourceOrder.Id || vm.Total != sourceOrder.Total || vm.Discount != sourceOrder.Discount || vm.DeliveryFee != sourceOrder.DeliveryFee || vm.CartCustomer?.Id != sourceOrder.CustomerId ||
                vm.Cart.Count != sourceOrder.Lines.Count || vm.Cart.Any(line => !sourceOrder.Lines.Any(saved => saved.ProductId == line.Product.Id && saved.Quantity == line.Quantity && saved.UnitPrice == line.UnitPrice)) || vm.CanEditCart ||
                State() != before || service.Snapshot.Products.Single(p => p.Id == product.Id).Stock != stock || StoreService.Reserved(service.Snapshot, product.Id) != reserved)
                throw new InvalidOperationException("Receber no PDV deve carregar somente o pedido escolhido, preservando preços históricos, valores e reservas até o pagamento.");
            vm.ClearCart(); vm.Page = "Pedidos";
            File.WriteAllText(Path.Combine(output, "pedidos-smoke-ok.txt"), "Pesquisa nativa, filtros e limites de datas, paginação10/20/50, seleção e menu por pedido conferidos. Histórico completo e reservas preservados. Pedidos encerrados bloqueiam alteração/recebimento. Detalhes, comprovante e cancelamento de formulário preservam dados. Receber por menu carrega o pedido correto com preços históricos, quantidade, cliente, desconto e entrega, sem cobrar ou baixar estoque antecipadamente. Controles acessíveis na janela menor.\n");
        }
        finally { window.Vm.ClearCart(); if (window.IsVisible) window.Close(); }
    }
}
