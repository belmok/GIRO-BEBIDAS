using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public static partial class SmokeRunner
{
    private static void CheckModernDialogs(string output)
    {
        var service = new StoreService(new JsonStore(Path.Combine(output, "dados-janelas-" + Guid.NewGuid().ToString("N"))));
        service.Login("ADMIN", "ADMIN"); service.OpenCash(100);
        var customer = new Customer { Name = "Marina Souza", Address = "Rua das Palmeiras, 175", Phone = "(11) 99999-0000", CreditLimit = 300 }; service.SaveCustomer(customer);
        var products = service.Snapshot.Products.Take(4).ToArray();
        var order = service.CreateOrder(products.Select(p => new DraftItem(p.Id, 1)).ToList(), customer.Id, true, customer.Address, "Entregar após 18h. " + new string('x', 300), 1, 5);
        var window = new MainWindow(service, false) { Width = 1200, Height = 760 };
        try
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = -10000; window.Top = -10000; window.ShowActivated = false; window.ShowInTaskbar = false; window.Show(); FlushLayout(window);
            var vm = window.Vm;
            string State() => JsonSerializer.Serialize(service.Snapshot, JsonStore.JsonOptions);
            static void Click(Button button) => ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            static void Prepare(Window dialog) { dialog.Owner = null; dialog.WindowStartupLocation = WindowStartupLocation.Manual; dialog.Left = -10000; dialog.Top = -10000; dialog.ShowActivated = false; dialog.ShowInTaskbar = false; }
            void Bounds(Window dialog, params FrameworkElement[] controls)
            {
                FlushLayout(dialog); var root = (FrameworkElement)dialog.Content;
                foreach (var control in controls)
                {
                    var bounds = control.TransformToAncestor(root).TransformBounds(new Rect(control.RenderSize));
                    if (!control.IsVisible || bounds.Width <= 0 || bounds.Height <= 0 || bounds.Left < -.5 || bounds.Top < -.5 || bounds.Right > root.ActualWidth + .5 || bounds.Bottom > root.ActualHeight + .5)
                        throw new InvalidOperationException("As novas janelas precisam manter seus cabeçalhos e botões dentro da área visível.");
                }
            }
            vm.LoadOrder(vm.Snapshot.Orders.Single(o => o.Id == order.Id)); FlushLayout(window);
            string before = State(); decimal total = vm.Total; var quantities = vm.Cart.Select(line => (line.Product.Id, line.Quantity)).ToArray();
            foreach (int choice in Enumerable.Range(0, 5))
            {
                var dialog = window.CreateClearCartConfirmation(); Prepare(dialog);
                Exception? failure = null; var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
                timeout.Tick += (_, _) => { failure = new InvalidOperationException("A confirmação de limpar precisa fechar após a escolha."); dialog.Close(); };
                dialog.Loaded += (_, _) => dialog.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
                {
                    try
                    {
                        Bounds(dialog, dialog.TitleLabel, dialog.MessageLabel, dialog.NoticeLabel, dialog.CancelButton, dialog.ConfirmButton, dialog.CloseButton);
                        if (!dialog.CancelButton.IsDefault || dialog.ConfirmButton.IsDefault || !dialog.NoticeText.Contains("não cancela", StringComparison.Ordinal)) throw new InvalidOperationException("Cancelar deve ser a escolha padrão; o aviso precisa esclarecer a preservação dos pedidos.");
                        if (choice == 0) Capture(dialog, Path.Combine(output, "62-confirmacao-limpar-venda.png"));
                        if (choice == 0) Click(dialog.CancelButton);
                        else if (choice == 1) Click(dialog.CloseButton);
                        else if (choice == 2) dialog.RaiseEvent(KeyEvent(dialog, Key.Escape, Keyboard.PreviewKeyDownEvent));
                        else if (choice == 3) dialog.Close();
                        else Click(dialog.ConfirmButton);
                    }
                    catch (Exception ex) { failure = ex; dialog.Close(); }
                }));
                try
                {
                    timeout.Start(); bool? result = dialog.ShowDialog();
                    if (failure is not null) throw failure;
                    if ((result == true) != (choice == 4) || State() != before || vm.Total != total || !vm.Cart.Select(line => (line.Product.Id, line.Quantity)).SequenceEqual(quantities))
                        throw new InvalidOperationException("Cancelar, Esc e fechar preservam a venda; a confirmação não pode cancelar o pedido salvo ou movimentar estoque.");
                }
                finally { timeout.Stop(); if (dialog.IsVisible) dialog.Close(); }
            }
            vm.ClearCart(); if (State() != before || service.Snapshot.Orders.Single(o => o.Id == order.Id).Status != OrderStatus.Pendente) throw new InvalidOperationException("Limpar a seleção precisa preservar o pedido e sua reserva.");
            var details = window.CreateOrderDetailsDialog(order);
            try
            {
                Prepare(details); details.Width = 600; details.Height = Math.Min(680, details.MaxHeight); Capture(details, Path.Combine(output, "61-detalhes-pedido-moderno.png"));
                if (details.TotalLabel.Text != order.Total.ToString("C2") || details.ItemsPanel.Children.Count != 4 || details.CustomerNameLabel.Text != customer.Name || details.AddressLabel.Text != customer.Address)
                    throw new InvalidOperationException("Os detalhes do pedido devem manter itens, cliente, endereço e total histórico.");
                details.DetailScroll.ScrollToEnd(); FlushLayout(details); Bounds(details, details.CloseButton, details.HeaderCloseButton);
            }
            finally { details.Close(); }
            var product = service.Snapshot.Products[0]; product.Description = string.Join(" ", Enumerable.Repeat("Descrição completa da bebida, embalagem e composição.", 25)); service.SaveProduct(product); vm.Refresh(); before = State();
            var productDialog = window.CreateStockDetailsDialog(product);
            try
            {
                Prepare(productDialog); productDialog.Width = 600; productDialog.Height = Math.Min(680, productDialog.MaxHeight); Capture(productDialog, Path.Combine(output, "60-detalhes-produto-moderno.png"));
                var reserved = StoreService.Reserved(service.Snapshot, product.Id);
                if (productDialog.ProductForDisplay.Stock - reserved != StoreService.Available(service.Snapshot, product) || productDialog.DescriptionLabel.Text != product.Description ||
                    productDialog.PriceLabel.Text != product.Price.ToString("C2") || productDialog.ProductImage.Source is null)
                    throw new InvalidOperationException("Detalhes do produto precisam preservar foto, descrição, preços e disponibilidade.");
                productDialog.DetailScroll.ScrollToEnd(); FlushLayout(productDialog); Bounds(productDialog, productDialog.CloseButton, productDialog.HeaderCloseButton);
                Capture(productDialog, Path.Combine(output, "65-produto-descricao-completa.png"));
            }
            finally { productDialog.Close(); }
            if (State() != before) throw new InvalidOperationException("Consultar detalhes não pode alterar dados do depósito.");
            vm.AddProduct(vm.Snapshot.Products[0], 2); vm.Discount = 1; vm.DeliveryFee = 2; vm.CartCustomer = vm.CustomerOptions.Single(c => c.Id == customer.Id); FlushLayout(window);
            Sale? sale = null; var checkout = window.CreateCheckoutDialog(saved => sale = saved);
            try
            {
                Prepare(checkout); checkout.SizeToContent = SizeToContent.Manual; checkout.Height = Math.Min(760, checkout.MaxHeight); Capture(checkout, Path.Combine(output, "63-finalizar-venda-moderno.png"));
                var method = checkout.Field<ComboBox>("method");
                method.IsDropDownOpen = true; FlushLayout(checkout);
                checkout.RaiseEvent(KeyEvent(checkout, Key.Escape, Keyboard.PreviewKeyDownEvent)); FlushLayout(checkout);
                if (method.IsDropDownOpen || !checkout.IsVisible) throw new InvalidOperationException("Esc deve fechar somente a lista de pagamento enquanto ela está aberta.");
                method.SelectedItem = method.Items.Cast<MainWindow.PaymentChoice>().Single(c => c.Method == PaymentMethod.Pix); FlushLayout(checkout);
                var received = checkout.Field<TextBox>("received");
                if (received.IsEnabled) throw new InvalidOperationException("Recebido em dinheiro deve ficar desativado no Pix.");
                method.SelectedItem = method.Items.Cast<MainWindow.PaymentChoice>().Single(c => c.Method == PaymentMethod.Dinheiro); FlushLayout(checkout);
                received.Text = "1"; before = State(); bool rejected = false;
                try { checkout.Submit?.Invoke(checkout); } catch (InvalidOperationException) { rejected = true; }
                if (!rejected || State() != before || sale is not null) throw new InvalidOperationException("Pagamento insuficiente deve bloquear a venda sem gravar dados.");
                received.Text = "20,00"; checkout.FormScroll.ScrollToEnd(); FlushLayout(checkout); Bounds(checkout, checkout.CancelButton, checkout.SubmitButton, checkout.CloseButton);
                checkout.Submit?.Invoke(checkout);
                if (sale is null || sale.Total != products[0].Price * 2 - 1 + 2 || sale.Change != 20m - sale.Total || sale.CustomerId != customer.Id)
                    throw new InvalidOperationException("Confirmar a venda deve preservar o total, o cliente e o cálculo do troco.");
            }
            finally { checkout.Close(); vm.ClearCart(); vm.Refresh(); }
            var closeCash = window.CreateCloseCashDialog(); before = State();
            try
            {
                Prepare(closeCash); closeCash.SizeToContent = SizeToContent.Manual; closeCash.Height = Math.Min(650, closeCash.MaxHeight); Capture(closeCash, Path.Combine(output, "64-fechar-caixa-moderno.png"));
                var counted = closeCash.Field<TextBox>("counted"); counted.Text = "inválido"; bool rejected = false;
                try { closeCash.Submit?.Invoke(closeCash); } catch (InvalidOperationException) { rejected = true; }
                if (!rejected || State() != before) throw new InvalidOperationException("Dinheiro contado inválido não pode fechar o caixa.");
                counted.Text = vm.CashSummary!.Expected.ToString("0.00"); closeCash.Submit?.Invoke(closeCash);
                if (service.Snapshot.CashSessions.Any(c => c.ClosedAt is null) || service.Snapshot.CashSessions.Last().Difference != 0) throw new InvalidOperationException("O fechamento deve preservar a conferência do dinheiro contado.");
            }
            finally { closeCash.Close(); }
            File.WriteAllText(Path.Combine(output, "janelas-modernas-smoke-ok.txt"), "Detalhes de produto e pedido, confirmação de limpar, finalizar venda e fechar caixa renderizados. Detalhes preservam os dados e permitem rolar descrições longas; botões acessíveis em janelas menores. Cancelar, X, Esc e fechar preservam seleção e pedido salvo. Pix desativa recebido em dinheiro; pagamento insuficiente e contagem inválida não gravam dados; venda e fechamento válidos reconciliam valores e troco.\n");
        }
        finally { window.Vm.ClearCart(); if (window.IsVisible) window.Close(); }
    }
}
