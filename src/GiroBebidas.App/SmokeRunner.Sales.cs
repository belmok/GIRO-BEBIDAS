using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public static partial class SmokeRunner
{
    private static void CheckSalesUi(string output)
    {
        var service = new StoreService(new JsonStore(Path.Combine(output, "dados-vendas-" + Guid.NewGuid().ToString("N"))));
        service.Login("ADMIN", "ADMIN");
        service.SaveUser(new() { Name = "Operador das vendas", Username = "VENDAS_OPERADOR", Role = UserRole.Operador }, "1234");
        var marina = new Customer { Name = "Marina Souza", Phone = "(11) 99999-0000", CreditLimit = 10000 }; service.SaveCustomer(marina);
        var joao = new Customer { Name = "João Pereira", CreditLimit = 10000 }; service.SaveCustomer(joao);
        var product = service.Snapshot.Products[0]; DateTime today = DateTime.Today;
        var oldCash = service.OpenCash(500);
        var closedSale = service.CompleteSale(new([new(product.Id, 1)], 0, 0, joao.Id, PaymentMethod.Dinheiro, 20));
        service.CloseCash(StoreService.SummarizeCash(service.Snapshot, oldCash.Id).Expected, "Caixa anterior de teste");
        var cash = service.OpenCash(500); var sales = new List<Sale>();
        var methods = new[] { PaymentMethod.Dinheiro, PaymentMethod.Pix, PaymentMethod.Debito, PaymentMethod.Credito, PaymentMethod.Fiado };
        for (int index = 0; index < 24; index++)
        {
            var method = methods[index % methods.Length]; Guid? customer = method == PaymentMethod.Fiado || index % 3 == 0 ? marina.Id : index % 3 == 1 ? joao.Id : null;
            int quantity = index % 3 + 1;
            sales.Add(service.CompleteSale(new([new(product.Id, quantity)], .25m, .50m, customer, method, method == PaymentMethod.Dinheiro ? product.Price * quantity + 5.25m : 0,
                "Observação histórica da venda " + index, "Rua das Palmeiras, 175")));
        }
        service.CancelSale(sales[1].Id, "Cancelamento de teste anterior"); service.CancelSale(sales[7].Id, "Cancelamento de teste anterior");
        service.ReceiveDebt(marina.Id, 1, PaymentMethod.Pix);
        service.Store.Update(state =>
        {
            state.Sales.Single(sale => sale.Id == closedSale.Id).Date = new DateTimeOffset(today.AddDays(-8).AddHours(16));
            for (int index = 0; index < sales.Count; index++)
                state.Sales.Single(sale => sale.Id == sales[index].Id).Date = new DateTimeOffset(index switch
                {
                    0 => today.AddDays(-7), 1 => today.AddDays(-7).AddDays(1).AddTicks(-1),
                    2 => today.AddDays(-6), 3 => today.AddDays(-6).AddDays(1).AddTicks(-1),
                    4 => today.AddDays(-5), _ => today.AddHours(12).AddMinutes(index)
                });
            return 0;
        });
        var renamed = service.Snapshot.Products.Single(p => p.Id == product.Id); renamed.Name = "Cadastro atual renomeado"; renamed.Price = 99; service.SaveProduct(renamed);
        var window = new MainWindow(service, false) { Width = 1480, Height = 940 };
        ContextMenu? menu = null;
        try
        {
            var vm = window.Vm; vm.Page = "Vendas";
            Capture(window, Path.Combine(output, "97-historico-vendas.png")); window.Width = 1480; window.Height = 940; FlushLayout(window);
            string State() => JsonSerializer.Serialize(service.Snapshot, JsonStore.JsonOptions);
            T Control<T>(string name) where T : FrameworkElement => window.FindName(name) as T ?? throw new InvalidOperationException($"O controle de vendas {name} precisa existir.");
            var grid = Control<DataGrid>("SalesGrid"); var scroll = Control<ScrollViewer>("SalesScroll");
            var search = Control<TextBox>("SalesSearchBox"); var fromPicker = Control<DatePicker>("SalesFromPicker"); var untilPicker = Control<DatePicker>("SalesUntilPicker");
            var payment = Control<ComboBox>("SalesPaymentBox"); var status = Control<ComboBox>("SalesStatusBox"); var pageSize = Control<ComboBox>("SalesPageSizeBox");
            static void Click(Button button) => ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            static void Invoke(MenuItem item) => ((IInvokeProvider)new MenuItemAutomationPeer(item).GetPattern(PatternInterface.Invoke)).Invoke();
            void Draft(string query = "", DateTime? from = null, DateTime? until = null, string method = "Todos", string situation = "Todas")
            {
                ((IValueProvider)new TextBoxAutomationPeer(search).GetPattern(PatternInterface.Value)).SetValue(query);
                fromPicker.SelectedDate = from; untilPicker.SelectedDate = until; payment.SelectedItem = method; status.SelectedItem = situation; FlushLayout(window);
            }
            void Apply() { Click(Control<Button>("SalesApplyButton")); FlushLayout(window); }
            void Clear() { Click(Control<Button>("SalesClearButton")); FlushLayout(window); }
            Sale[] Expected(string query = "", DateTime? from = null, DateTime? until = null, string method = "Todos", string situation = "Todas")
                => service.Snapshot.Sales.Where(sale => (from is null || sale.Date.LocalDateTime.Date >= from.Value.Date)
                    && (until is null || sale.Date.LocalDateTime.Date <= until.Value.Date)
                    && (method == "Todos" || sale.PaymentLabel == method)
                    && (situation == "Todas" || sale.Canceled == (situation == "Canceladas"))
                    && new[] { sale.NumberLabel, sale.Number.ToString(CultureInfo.InvariantCulture), sale.CustomerName, sale.UserName }
                        .Any(text => text.Contains(query.Trim(), StringComparison.CurrentCultureIgnoreCase)))
                    .OrderByDescending(sale => sale.Date).ThenByDescending(sale => sale.Number).ThenBy(sale => sale.Id).ToArray();
            void Result(Sale[] expected)
            {
                if (!vm.SalesFilteredItems.Select(sale => sale.Id).SequenceEqual(expected.Select(sale => sale.Id)) || vm.SalesFilteredCount != expected.Length
                    || !vm.SalesPageItems.Select(sale => sale.Id).SequenceEqual(expected.Skip((vm.SalesPageNumber - 1) * vm.SalesPageSize).Take(vm.SalesPageSize).Select(sale => sale.Id))
                    || grid.Items.Count != vm.SalesPageItems.Count || vm.SalesCompletedTotal != expected.Where(sale => !sale.Canceled).Sum(sale => sale.Total)
                    || vm.SalesCompletedCount != expected.Count(sale => !sale.Canceled) || vm.SalesCanceledCount != expected.Count(sale => sale.Canceled)
                    || vm.SalesCanceledTotal != expected.Where(sale => sale.Canceled).Sum(sale => sale.Total)
                    || vm.SalesCustomerCount != expected.Where(sale => !sale.Canceled).Select(sale => sale.CustomerId).Distinct().Count())
                    throw new InvalidOperationException("Filtros, página e cartões devem corresponder ao histórico real aplicado, excluindo canceladas do total e dos clientes.");
                var labels = new Dictionary<string, string>
                {
                    ["SalesCompletedTotalValue"] = vm.SalesCompletedTotal.ToString("C2"), ["SalesCompletedCountValue"] = vm.SalesCompletedCount.ToString(),
                    ["SalesCanceledCountValue"] = vm.SalesCanceledCount.ToString(), ["SalesCustomerCountValue"] = vm.SalesCustomerCount.ToString(),
                    ["SalesCanceledTotalValue"] = vm.SalesCanceledTotal.ToString("C2")
                };
                foreach (var pair in labels) if (Control<TextBlock>(pair.Key).Text != pair.Value) throw new InvalidOperationException("Os valores renderizados dos cartões precisam refletir todos os registros filtrados.");
            }
            string stable = State(); Result(Expected());
            if (vm.SalesFilteredCount != 25 || vm.SalesPageCount != 3 || vm.SalesPageItems.Count != 10 || Control<Button>("SalesPreviousButton").IsEnabled || !Control<Button>("SalesNextButton").IsEnabled)
                throw new InvalidOperationException("O histórico deve iniciar na primeira página de dez vendas e resumir os 25 registros.");
            Click(Control<Button>("SalesNextButton")); FlushLayout(window); var selected = vm.SalesPageItems[0]; grid.SelectedItem = selected; vm.Refresh(); FlushLayout(window);
            if (vm.SalesPageNumber != 2 || vm.SelectedSale?.Id != selected.Id || !vm.SalesPageItems.Contains(vm.SelectedSale)) throw new InvalidOperationException("Atualizar precisa preservar a seleção e a página pelo identificador da venda.");
            Click(Control<Button>("SalesNextButton")); FlushLayout(window);
            if (vm.SalesPageNumber != 3 || vm.SalesPageItems.Count != 5 || Control<Button>("SalesNextButton").IsEnabled || vm.SelectedSale is not null)
                throw new InvalidOperationException("A última página deve mostrar somente as vendas restantes e remover seleção fora da página.");
            vm.SalesNextPageCommand.Execute(null); FlushLayout(window); if (vm.SalesPageNumber != 3) throw new InvalidOperationException("A paginação não pode ultrapassar o final.");
            foreach (int count in new[] { 20, 50, 10 })
            {
                pageSize.SelectedItem = count; FlushLayout(window);
                if (vm.SalesPageNumber != 1 || vm.SalesPageItems.Count != Math.Min(25, count)) throw new InvalidOperationException("Mudar a quantidade por página deve retornar ao início e respeitar o limite escolhido.");
                Result(Expected());
            }
            void SortHeader(string member)
            {
                grid.BringIntoView(); FlushLayout(window);
                var header = Descendants<DataGridColumnHeader>(grid).Single(header => header.Column?.SortMemberPath == member);
                var peer = new DataGridColumnHeaderAutomationPeer(header);
                if (peer.GetPattern(PatternInterface.Invoke) is IInvokeProvider invoke) invoke.Invoke();
                else
                {
                    // WPF exposes sorting through the header's native OnClick pipeline even
                    // when its automation peer does not provide an Invoke pattern.
                    var nativeClick = typeof(DataGridColumnHeader).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                        null, Type.EmptyTypes, null) ?? throw new InvalidOperationException("O adaptador nativo do cabeçalho WPF não oferece Invoke nem OnClick para conferir a ordenação.");
                    try { nativeClick.Invoke(header, null); }
                    catch (System.Reflection.TargetInvocationException ex) { throw new InvalidOperationException("O clique nativo no cabeçalho de vendas falhou ao executar a ordenação.", ex.InnerException ?? ex); }
                }
                FlushLayout(window);
            }
            SortHeader("Total");
            if (vm.SalesSortMember != "Total" || vm.SalesSortDescending || vm.SalesPageNumber != 1) throw new InvalidOperationException("Ordenar total pelo cabeçalho deve começar crescente e retornar à primeira página.");
            Result(Expected().OrderBy(sale => sale.Total).ThenByDescending(sale => sale.Number).ThenBy(sale => sale.Id).ToArray());
            Click(Control<Button>("SalesNextButton")); FlushLayout(window); Result(Expected().OrderBy(sale => sale.Total).ThenByDescending(sale => sale.Number).ThenBy(sale => sale.Id).ToArray());
            SortHeader("Total");
            if (!vm.SalesSortDescending || vm.SalesPageNumber != 1) throw new InvalidOperationException("Novo clique no total deve ordenar decrescente e reiniciar a página.");
            Result(Expected().OrderByDescending(sale => sale.Total).ThenByDescending(sale => sale.Number).ThenBy(sale => sale.Id).ToArray());
            SortHeader("Number"); Result(Expected().OrderBy(sale => sale.Number).ThenBy(sale => sale.Id).ToArray());
            if (vm.SalesSortMember != "Number" || vm.SalesSortDescending) throw new InvalidOperationException("Ordenar número da venda deve reorganizar o conjunto completo antes da paginação.");
            Click(Control<Button>("SalesNextButton")); FlushLayout(window); Result(Expected().OrderBy(sale => sale.Number).ThenBy(sale => sale.Id).ToArray());
            if (!window.SalesCsvRows().Skip(1).Select(row => row[0]).SequenceEqual(Expected().OrderBy(sale => sale.Number).Select(sale => sale.NumberLabel)))
                throw new InvalidOperationException("A exportação deve seguir a ordenação aplicada do histórico inteiro.");
            Clear(); Result(Expected());
            var csv = window.SalesCsvRows().ToArray();
            if (csv.Length != 26 || Expected().Any(sale => !csv.Skip(1).Any(row => row.Contains(sale.NumberLabel) && row.Contains(sale.Total.ToString("0.00")))))
                throw new InvalidOperationException("O CSV deve incluir o cabeçalho e todas as vendas aplicadas, inclusive as que estão fora da página visível.");
            var initialIds = vm.SalesFilteredItems.Select(sale => sale.Id).ToArray();
            Draft("marina", today.AddDays(-7), today.AddDays(-6), Labels.Payment(PaymentMethod.Dinheiro), "Concluídas");
            if (!vm.SalesFilteredItems.Select(sale => sale.Id).SequenceEqual(initialIds)) throw new InvalidOperationException("Editar os filtros sem Aplicar deve preservar os resultados atuais.");
            vm.Refresh(); FlushLayout(window); Result(Expected());
            Apply(); Result(Expected("marina", today.AddDays(-7), today.AddDays(-6), Labels.Payment(PaymentMethod.Dinheiro), "Concluídas"));
            Draft(from: today.AddDays(-7), until: today.AddDays(-6)); Apply(); Result(Expected(from: today.AddDays(-7), until: today.AddDays(-6)));
            if (vm.SalesFilteredCount != 4) throw new InvalidOperationException("O intervalo deve incluir meia-noite e o fim do último dia, sem incluir o dia seguinte.");
            grid.SelectedItem = vm.SalesPageItems[0]; FlushLayout(window); var appliedIds = vm.SalesFilteredItems.Select(sale => sale.Id).ToArray(); Guid? selectedId = vm.SelectedSale?.Id;
            Draft("consulta ainda não aplicada", today, today.AddDays(-1)); Apply();
            if (!vm.SalesFilteredItems.Select(sale => sale.Id).SequenceEqual(appliedIds) || vm.SelectedSale?.Id != selectedId || vm.SalesPageNumber != 1
                || !vm.Message.Contains("data", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Datas invertidas devem informar o erro e preservar filtros aplicados, página e seleção.");
            fromPicker.Text = "data inválida"; Apply();
            if (!vm.SalesFilteredItems.Select(sale => sale.Id).SequenceEqual(appliedIds) || vm.SelectedSale?.Id != selectedId || State() != stable)
                throw new InvalidOperationException("Texto inválido de data deve preservar os resultados aplicados e o histórico.");
            Clear(); Result(Expected());
            Draft(from: DateTime.MinValue, until: DateTime.MaxValue.Date); Apply(); Result(Expected()); Clear();
            foreach (var method in Enum.GetValues<PaymentMethod>()) { Draft(method: Labels.Payment(method)); Apply(); Result(Expected(method: Labels.Payment(method))); }
            foreach (string situation in new[] { "Concluídas", "Canceladas" }) { Draft(situation: situation); Apply(); Result(Expected(situation: situation)); }
            foreach (string query in new[] { sales[13].NumberLabel.ToLowerInvariant(), "MARINA", "admin" })
            {
                Draft(query); Apply(); Result(Expected(query)); AssertSearchReadable(search);
            }
            Draft("venda inexistente " + Guid.NewGuid().ToString("N")); Apply(); Result([]);
            if (vm.SalesEmptyVisibility != "Visible" || Control<Button>("SalesDetailsButton").IsEnabled || Control<Button>("SalesPrintButton").IsEnabled
                || Control<Button>("SalesCancelButton").IsEnabled || Control<Button>("SalesExportButton").IsEnabled || Control<Button>("SalesNextButton").IsEnabled)
                throw new InvalidOperationException("Sem resultados, o histórico deve mostrar o estado vazio e desativar ações que dependem de registros.");
            Capture(window, Path.Combine(output, "99-vendas-sem-resultados.png")); Clear(); Result(Expected());
            if (State() != stable) throw new InvalidOperationException("Filtrar, paginar, consultar e preparar CSV não podem alterar vendas, estoque ou caixa.");

            void OnPage(Guid id)
            {
                int index = vm.SalesFilteredItems.ToList().FindIndex(sale => sale.Id == id);
                if (index < 0) throw new InvalidOperationException("A venda de teste precisa estar disponível no histórico.");
                vm.SalesPageNumber = index / vm.SalesPageSize + 1; FlushLayout(window);
            }
            MenuItem[] OpenMenu(Guid id)
            {
                if (menu is not null) menu.IsOpen = false; OnPage(id); var row = vm.SalesPageItems.Single(sale => sale.Id == id);
                grid.BringIntoView(); grid.ScrollIntoView(row, grid.Columns.Last()); FlushLayout(window);
                var button = Descendants<Button>(grid).Single(button => button.Name == "SaleMoreButton" && button.DataContext is Sale sale && sale.Id == id);
                Click(button); FlushLayout(window); menu = button.ContextMenu;
                if (menu is null || !menu.IsOpen) throw new InvalidOperationException("A linha precisa abrir seu menu de ações.");
                var items = menu.Items.OfType<MenuItem>().ToArray();
                if (items.Length != 3 || !items[0].IsEnabled || !items[1].IsEnabled || items[2].IsEnabled != vm.CanCancelSale(row))
                    throw new InvalidOperationException("Detalhes e reimpressão ficam disponíveis; cancelar depende do administrador e das regras do caixa de origem.");
                return items;
            }
            static MenuItem Action(MenuItem[] items, string text) => items.Single(item => item.Header?.ToString() == text);
            void WithDialog<T>(Action invoke, Action<T> examine) where T : Window
            {
                Exception? failure = null; bool handled = false; var started = DateTime.UtcNow;
                var watcher = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
                watcher.Tick += (_, _) =>
                {
                    var dialog = Application.Current.Windows.OfType<T>().FirstOrDefault(candidate => candidate.IsVisible && ReferenceEquals(candidate.Owner, window));
                    if (dialog is not null)
                    {
                        watcher.Stop(); handled = true;
                        try { FlushLayout(dialog); examine(dialog); } catch (Exception ex) { failure = ex; dialog.Close(); }
                    }
                    else if ((DateTime.UtcNow - started).TotalSeconds > 6) { watcher.Stop(); failure = new InvalidOperationException("A ação do histórico deve abrir a janela correspondente."); }
                };
                try { watcher.Start(); invoke(); FlushLayout(window); while (!handled && failure is null) FlushLayout(window); if (failure is not null) throw failure; FlushLayout(window); }
                finally { watcher.Stop(); }
            }
            var target = sales[5]; var actions = OpenMenu(target.Id); CaptureElement(menu!, Path.Combine(output, "102-menu-venda.png"));
            vm.SelectedSale = vm.SalesPageItems.First(sale => sale.Id != target.Id);
            WithDialog<SaleDetailsWindow>(() => Invoke(Action(actions, "Ver detalhes")), dialog =>
            {
                var historical = service.Snapshot.Sales.Single(sale => sale.Id == target.Id);
                if (dialog.SaleForDisplay.Id != target.Id || dialog.TotalLabel.Text != historical.Total.ToString("C2") || dialog.ItemsPanel.Children.Count != historical.Lines.Count
                    || !Descendants<TextBlock>(dialog.ItemsPanel).Any(block => block.Text == product.Name))
                    throw new InvalidOperationException("Detalhes do menu devem mostrar a própria venda e os nomes e valores históricos dos itens.");
                Capture(dialog, Path.Combine(output, "100-detalhes-venda.png")); Click(dialog.CloseButton);
            });
            Guid? printed = null; string? receipt = null;
            window.SalePrinter = (_, state, sale) => { printed = sale.Id; receipt = ReceiptText.Sale(state.Settings, sale); };
            actions = OpenMenu(target.Id); vm.SelectedSale = vm.SalesPageItems.First(sale => sale.Id != target.Id);
            Invoke(Action(actions, "Reimprimir cupom")); FlushLayout(window);
            if (printed != target.Id || receipt is null || !receipt.Contains(product.Name, StringComparison.Ordinal) || !receipt.Contains(target.Total.ToString("C2"), StringComparison.Ordinal) || State() != stable)
                throw new InvalidOperationException("Reimprimir deve usar a venda da linha e preservar seus dados e valores históricos, sem alterar os registros.");
            actions = OpenMenu(target.Id);
            WithDialog<SaleDetailsWindow>(() => Invoke(Action(actions, "Ver detalhes")), dialog =>
            {
                printed = null; dialog.DetailScroll.ScrollToEnd(); FlushLayout(dialog); Click(dialog.PrintButton); FlushLayout(dialog);
                if (printed != target.Id || !dialog.PrintButton.IsEnabled || !dialog.PrintFeedbackLabel.IsVisible || State() != stable)
                    throw new InvalidOperationException("Reimprimir dentro dos detalhes deve usar a venda histórica e recuperar o botão sem alterar registros.");
                window.SalePrinter = (_, _, _) => throw new InvalidOperationException("Falha dentro dos detalhes no teste.");
                Click(dialog.PrintButton); FlushLayout(dialog);
                if (!dialog.PrintButton.IsEnabled || !dialog.PrintFeedbackLabel.Text.Contains("Falha dentro dos detalhes", StringComparison.Ordinal) || State() != stable)
                    throw new InvalidOperationException("Falha de impressão dentro dos detalhes deve mostrar aviso e recuperar o botão.");
                Click(dialog.CloseButton);
            });
            window.SalePrinter = (_, _, _) => throw new InvalidOperationException("Impressão indisponível no teste isolado.");
            actions = OpenMenu(target.Id); Invoke(Action(actions, "Reimprimir cupom")); FlushLayout(window);
            if (!vm.Message.Contains("Impressão indisponível", StringComparison.Ordinal) || State() != stable) throw new InvalidOperationException("Falha de reimpressão deve informar o erro e preservar a venda.");
            foreach (var protectedSale in new[] { closedSale, sales[1], sales[4] })
            {
                OpenMenu(protectedSale.Id); menu!.IsOpen = false;
                if (vm.CanCancelSale(service.Snapshot.Sales.Single(sale => sale.Id == protectedSale.Id))) throw new InvalidOperationException("Caixa fechado, venda já cancelada e fiado recebido não podem permitir novo cancelamento.");
            }
            decimal withdrawal = StoreService.SummarizeCash(service.Snapshot, cash.Id).Expected - .01m;
            service.MoveCash(CashMovementKind.Sangria, withdrawal, "Conferência isolada do limite de estorno");
            string lowCashState = State();
            bool lowCashRejected = false;
            try { window.CreateSaleCancelDialog(target).Close(); } catch (InvalidOperationException) { lowCashRejected = true; }
            vm.Refresh(); actions = OpenMenu(target.Id);
            if (!lowCashRejected || Action(actions, "Cancelar venda").IsEnabled || State() != lowCashState) throw new InvalidOperationException("Saldo insuficiente no caixa real deve bloquear o formulário e o menu de estorno, mesmo sem atualizar antes da solicitação, preservando os dados.");
            menu!.IsOpen = false; service.MoveCash(CashMovementKind.Suprimento, withdrawal, "Restaurar o saldo da conferência isolada"); vm.Refresh(); stable = State();
            for (int choice = 0; choice < 3; choice++)
            {
                actions = OpenMenu(target.Id);
                WithDialog<FormDialog>(() => Invoke(Action(actions, "Cancelar venda")), dialog =>
                {
                    if (choice == 0) Click(dialog.CancelButton); else if (choice == 1) Click(dialog.CloseButton);
                    else dialog.RaiseEvent(KeyEvent(dialog, Key.Escape, Keyboard.PreviewKeyDownEvent));
                });
                if (State() != stable) throw new InvalidOperationException("Cancelar, fechar ou Esc no formulário devem preservar venda, estoque e caixa.");
            }
            var beforeCancel = service.Snapshot; decimal stockBefore = beforeCancel.Products.Single(p => p.Id == product.Id).Stock;
            decimal cashBefore = StoreService.SummarizeCash(beforeCancel, cash.Id).Expected;
            actions = OpenMenu(target.Id); vm.SelectedSale = vm.SalesPageItems.First(sale => sale.Id != target.Id);
            WithDialog<FormDialog>(() => Invoke(Action(actions, "Cancelar venda")), dialog =>
            {
                Click(dialog.SubmitButton); FlushLayout(dialog);
                if (!dialog.IsVisible || State() != stable) throw new InvalidOperationException("Motivo vazio deve bloquear o cancelamento sem alterar registros.");
                var reason = dialog.Field<TextBox>("reason"); ((IValueProvider)new TextBoxAutomationPeer(reason).GetPattern(PatternInterface.Value)).SetValue("Cliente desistiu da compra no teste.");
                Capture(dialog, Path.Combine(output, "101-cancelar-venda.png")); Click(dialog.SubmitButton);
            });
            var afterCancel = service.Snapshot; var canceled = afterCancel.Sales.Single(sale => sale.Id == target.Id);
            if (!canceled.Canceled || canceled.CancelReason != "Cliente desistiu da compra no teste." || afterCancel.Products.Single(p => p.Id == product.Id).Stock != stockBefore + target.Lines.Sum(line => line.Quantity)
                || StoreService.SummarizeCash(afterCancel, cash.Id).Expected != cashBefore - target.Total
                || afterCancel.Sales.Where(sale => sale.Id != target.Id).Any(sale => sale.Canceled != beforeCancel.Sales.Single(old => old.Id == sale.Id).Canceled))
                throw new InvalidOperationException("Confirmar cancelamento deve marcar somente a venda acionada, devolver sua quantidade ao estoque e estornar exatamente o valor do caixa.");
            Result(Expected()); stable = State();
            if (!new JsonStore(service.Store.DirectoryPath).Snapshot().Sales.Single(sale => sale.Id == target.Id).Canceled) throw new InvalidOperationException("Cancelamento confirmado deve persistir após reabrir os dados.");
            actions = OpenMenu(target.Id); if (Action(actions, "Cancelar venda").IsEnabled) throw new InvalidOperationException("Venda cancelada não pode permitir estorno duplicado."); menu!.IsOpen = false;

            window.Width = 1200; window.Height = 760; Clear(); pageSize.SelectedItem = 50; FlushLayout(window); Result(Expected());
            var root = (FrameworkElement)window.Content;
            foreach (string name in new[] { "SalesSearchBox", "SalesFromPicker", "SalesUntilPicker", "SalesPaymentBox", "SalesStatusBox", "SalesApplyButton", "SalesClearButton",
                "SalesExportButton", "SalesDetailsButton", "SalesPrintButton", "SalesCancelButton", "SalesPreviousButton", "SalesNextButton", "SalesPageSizeBox" })
            {
                var element = Control<FrameworkElement>(name); element.BringIntoView(); FlushLayout(window);
                var bounds = element.TransformToAncestor(root).TransformBounds(new Rect(element.RenderSize));
                if (!element.IsVisible || bounds.Width <= 0 || bounds.Height <= 0 || bounds.Left < -.5 || bounds.Top < -.5 || bounds.Right > root.ActualWidth + .5 || bounds.Bottom > root.ActualHeight + .5)
                    throw new InvalidOperationException($"O controle de histórico {name} precisa ficar acessível na janela menor.");
            }
            var cards = Control<UniformGrid>("SalesSummaryCards"); cards.BringIntoView(); FlushLayout(window);
            Capture(window, Path.Combine(output, "98-historico-vendas-tela-menor.png"));
            foreach (var label in Descendants<TextBlock>(cards))
            {
                var bounds = label.TransformToAncestor(cards).TransformBounds(new Rect(label.RenderSize));
                var text = new FormattedText(label.Text, CultureInfo.CurrentCulture, label.FlowDirection, new Typeface(label.FontFamily, label.FontStyle, label.FontWeight, label.FontStretch), label.FontSize, label.Foreground, VisualTreeHelper.GetDpi(label).PixelsPerDip);
                double desiredContentHeight = Math.Max(0, label.DesiredSize.Height - label.Margin.Top - label.Margin.Bottom);
                if (bounds.Left < -.5 || bounds.Right > cards.ActualWidth + .5 || desiredContentHeight > label.ActualHeight + .5 || label.TextWrapping == TextWrapping.NoWrap && text.Width > label.ActualWidth + 1)
                    throw new InvalidOperationException($"Títulos e valores dos cartões de vendas devem aparecer completos na janela menor. "
                        + $"Controle='{label.Name}'; texto='{label.Text}'; desejado={label.DesiredSize.Width:0.##}×{label.DesiredSize.Height:0.##}; "
                        + $"conteúdo desejado={Math.Max(0, label.DesiredSize.Width - label.Margin.Left - label.Margin.Right):0.##}×{desiredContentHeight:0.##}; "
                        + $"real={label.ActualWidth:0.##}×{label.ActualHeight:0.##}; margem={label.Margin}; "
                        + $"texto medido={text.Width:0.##}×{text.Height:0.##}; limites transformados={bounds}; área cartões={cards.ActualWidth:0.##}×{cards.ActualHeight:0.##}.");
            }
            if (scroll.ExtentWidth > scroll.ViewportWidth + .5 || !ControlledScrolling.GetIsEnabled(scroll)) throw new InvalidOperationException("Histórico precisa caber na largura menor e usar rolagem reduzida.");
            scroll.ScrollToTop(); FlushLayout(window); Capture(window, Path.Combine(output, "98-historico-vendas-tela-menor.png"));
            void CheckWheel(FrameworkElement area, Func<FrameworkElement> source, bool inner)
            {
                var viewer = ControlledScrolling.GetScrollViewer(area) ?? throw new InvalidOperationException("A área do histórico precisa ser rolável.");
                if (!ControlledScrolling.GetIsEnabled(area) || viewer.ScrollableHeight < 30) throw new InvalidOperationException("O histórico deve permitir conferir gestos e limites de rolagem.");
                double outer = scroll.VerticalOffset, step = SystemParameters.WheelScrollLines == 0 ? 0 : 22;
                void Wheel(int delta, bool flush = true) { var args = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta) { RoutedEvent = Mouse.PreviewMouseWheelEvent }; source().RaiseEvent(args); if (flush) FlushLayout(window); if (!args.Handled) throw new InvalidOperationException("O touchpad deve ser tratado na área acionada do histórico."); }
                void Offset(double expected) { if (Math.Abs(viewer.VerticalOffset - Math.Clamp(expected, 0, viewer.ScrollableHeight)) > .6 || inner && Math.Abs(scroll.VerticalOffset - outer) > .6 || State() != stable) throw new InvalidOperationException("Rolagem deve ser proporcional, respeitar limites e preservar dados e a área externa."); }
                viewer.ScrollToVerticalOffset(25); FlushLayout(window); double start = viewer.VerticalOffset;
                Wheel(-20); Offset(start + step / 6); Wheel(20); Offset(start);
                for (int index = 0; index < 6; index++) Wheel(-20, false); FlushLayout(window); Offset(start + step);
                for (int index = 0; index < 6; index++) Wheel(20, false); FlushLayout(window); Offset(start);
                viewer.ScrollToTop(); FlushLayout(window); Wheel(120); Offset(0); viewer.ScrollToBottom(); FlushLayout(window); Wheel(-120); Offset(viewer.ScrollableHeight); viewer.ScrollToTop(); FlushLayout(window);
            }
            grid.BringIntoView(); FlushLayout(window); CheckWheel(grid, () => Descendants<TextBlock>(grid).First(block => block.IsVisible && block.DataContext is Sale), true);
            CheckWheel(scroll, () => scroll, false);
            payment.BringIntoView(); FlushLayout(window); payment.MaxDropDownHeight = 110; payment.IsDropDownOpen = true; FlushLayout(window);
            var popupViewer = payment.Template.FindName("PART_PopupScrollViewer", payment) as ScrollViewer ?? throw new InvalidOperationException("A lista de pagamentos precisa de rolagem controlada.");
            string chosenPayment = vm.SalesPaymentFilter; double popupStep = SystemParameters.WheelScrollLines == 0 ? 0 : 22;
            void PopupWheel(int delta, bool flush = true) { var args = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta) { RoutedEvent = Mouse.PreviewMouseWheelEvent }; popupViewer.RaiseEvent(args); if (flush) FlushLayout(window); if (!args.Handled) throw new InvalidOperationException("A lista de pagamentos deve tratar o touchpad."); }
            void PopupOffset(double expected) { if (Math.Abs(popupViewer.VerticalOffset - Math.Clamp(expected, 0, popupViewer.ScrollableHeight)) > .6 || vm.SalesPaymentFilter != chosenPayment || State() != stable) throw new InvalidOperationException("Rolar os pagamentos deve preservar escolha e resultados aplicados, usando movimentos menores."); }
            popupViewer.ScrollToVerticalOffset(25); FlushLayout(window); double popupBefore = popupViewer.VerticalOffset;
            PopupWheel(-20); PopupOffset(popupBefore + popupStep / 6); PopupWheel(20); PopupOffset(popupBefore);
            for (int index = 0; index < 6; index++) PopupWheel(-20, false); FlushLayout(window); PopupOffset(popupBefore + popupStep);
            for (int index = 0; index < 6; index++) PopupWheel(20, false); FlushLayout(window); PopupOffset(popupBefore);
            popupViewer.ScrollToTop(); FlushLayout(window); PopupWheel(120); PopupOffset(0); popupViewer.ScrollToBottom(); FlushLayout(window); PopupWheel(-120); PopupOffset(popupViewer.ScrollableHeight);
            payment.IsDropDownOpen = false; FlushLayout(window);
            var brand = Control<Image>("SidebarBrandLogo");
            if (brand.Source is not BitmapSource bitmap || bitmap.PixelWidth < 32 || bitmap.PixelHeight < 32 || brand.ActualWidth < 30 || brand.ActualHeight < 30)
                throw new InvalidOperationException("A lateral deve carregar a logo do programa como imagem legível.");
            var login = new LoginWindow(service);
            try { Capture(login, Path.Combine(output, "103-login-logo-programa.png")); var image = login.FindName("LoginBrandLogo") as Image; if (image?.Source is not BitmapSource logo || logo.PixelWidth < 32 || image.ActualWidth < 30 || image.ActualHeight < 30) throw new InvalidOperationException("A tela de acesso deve usar a mesma logo do programa."); }
            finally { login.Close(); }
            service.CloseCash(StoreService.SummarizeCash(service.Snapshot, cash.Id).Expected, "Encerramento do teste de vendas");
            stable = State(); bool rejected = false; try { window.CreateSaleCancelDialog(vm.Snapshot.Sales.Single(sale => sale.Id == sales[10].Id)).Close(); } catch (InvalidOperationException) { rejected = true; }
            if (!rejected || State() != stable) throw new InvalidOperationException("Cancelamento com linha desatualizada deve consultar o caixa real recém-fechado e preservar os dados.");
            vm.Refresh(); Clear();
            OnPage(sales[10].Id); grid.SelectedItem = vm.SalesPageItems.Single(sale => sale.Id == sales[10].Id); FlushLayout(window);
            if (Control<Button>("SalesCancelButton").IsEnabled || vm.SalesCanCancelSelection) throw new InvalidOperationException("Fechar o caixa de origem deve desativar cancelamento imediatamente.");
            stable = State(); rejected = false; try { window.CreateSaleCancelDialog(service.Snapshot.Sales.Single(sale => sale.Id == sales[10].Id)).Close(); } catch (InvalidOperationException) { rejected = true; }
            if (!rejected || State() != stable) throw new InvalidOperationException("Solicitação antiga de cancelamento com caixa fechado deve ser recusada sem alterar dados.");
            service.OpenCash(0); var operatorProbe = service.CompleteSale(new([new(product.Id, 1)], 0, 0, null, PaymentMethod.Pix, 0));
            service.Login("VENDAS_OPERADOR", "1234"); vm.Refresh(); Clear(); OnPage(operatorProbe.Id); grid.SelectedItem = vm.SalesPageItems.Single(sale => sale.Id == operatorProbe.Id); FlushLayout(window);
            if (Control<Button>("SalesCancelButton").IsEnabled || !Control<Button>("SalesDetailsButton").IsEnabled || !Control<Button>("SalesPrintButton").IsEnabled || !Control<Button>("SalesExportButton").IsEnabled)
                throw new InvalidOperationException("Operador deve conservar consulta, reimpressão e exportação, mantendo cancelamento protegido.");
            stable = State(); printed = null; window.SalePrinter = (_, _, sale) => printed = sale.Id; Click(Control<Button>("SalesPrintButton")); FlushLayout(window);
            if (printed != operatorProbe.Id || window.SalesCsvRows().Count() != vm.SalesFilteredCount + 1 || State() != stable) throw new InvalidOperationException("Operador deve consultar e reimprimir a venda selecionada e preparar todos os registros do CSV sem alterar negócios.");
            rejected = false; try { window.CreateSaleCancelDialog(operatorProbe).Close(); } catch (UnauthorizedAccessException) { rejected = true; }
            if (!rejected || State() != stable) throw new InvalidOperationException("Operador não pode cancelar uma venda do caixa aberto, mesmo com pedido direto antigo.");
            File.WriteAllText(Path.Combine(output, "vendas-smoke-ok.txt"), "Filtros aplicados com datas inclusivas e inválidas preservando resultados, busca, pagamento/situação e estado vazio. Cartões correspondem a todas as vendas filtradas e excluem canceladas do total/clientes. Paginação, seleção, CSV completo e valores históricos conferidos. Menus usam a venda da linha; detalhes/reimpressão não alteram dados; impressão injetada sem hardware. Motivo vazio recusado, cancelar/X/Esc preservam e cancelamento confirmado restaura estoque e caixa e persiste. Caixa fechado, canceladas, fiado recebido e operador protegidos. Janela menor, cartões/controles, popup de pagamentos e touchpad em grade/página conferidos. Logos existentes carregadas na lateral e acesso.\n");
        }
        finally { if (menu is not null) menu.IsOpen = false; window.Vm.ClearCart(); if (window.IsVisible) window.Close(); }
    }
}
