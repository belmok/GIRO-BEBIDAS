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
using System.Windows.Threading;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public static partial class SmokeRunner
{
    private static void CheckReportDashboard(MainWindow main, MainViewModel originalVm, string output)
    {
        var service = new StoreService(new JsonStore(Path.Combine(output, "dados-relatorios-" + Guid.NewGuid().ToString("N"))));
        service.Login("ADMIN", "ADMIN");
        service.SaveUser(new() { Name = "Operador relatório", Username = "RELATORIO_OPERADOR", Role = UserRole.Operador }, "1234");
        var products = new List<Product>(); var sales = new List<Sale>(); var today = DateTime.Today;
        var cash = service.OpenCash(500);
        for (int index = 0; index < 26; index++)
        {
            var product = new Product
            {
                Code = $"R{index:000}", Name = index < 2 ? "Produto histórico repetido" : $"Produto do relatório {index:00}",
                Category = index % 2 == 0 ? "Cervejas" : "Refrigerantes", Price = index == 1 ? 2 : 10 + index,
                Cost = index == 1 ? 4 : 4 + index / 2m, Stock = 100, MinimumStock = 0, Unit = index == 10 ? "kg" : "un"
            };
            service.SaveProduct(product); products.Add(product);
            decimal quantity = product.IsWeighed ? .375m : 1;
            sales.Add(service.CompleteSale(new([new(product.Id, quantity)], index == 2 ? product.Price : .25m, 7, null, PaymentMethod.Pix, 0)));
        }
        sales.Add(service.CompleteSale(new([new(products[0].Id, 2)], 1.15m, 12, null, PaymentMethod.Pix, 0)));
        service.CancelSale(sales[3].Id, "Venda cancelada não compõe lucro do relatório");
        service.Store.Update(state =>
        {
            for (int index = 0; index < sales.Count; index++)
                state.Sales.Single(sale => sale.Id == sales[index].Id).Date = new DateTimeOffset(index switch
                {
                    4 => today.AddDays(-31).AddHours(16), 5 => today.AddDays(1),
                    6 => today.AddDays(-29), 7 => today.AddDays(-29).AddTicks(-1),
                    _ => today.AddHours(12).AddMinutes(index)
                });
            return 0;
        });
        var changed = service.Snapshot.Products.Single(product => product.Id == products[0].Id);
        changed.Name = "Nome atual diferente do histórico"; changed.Price = 99; changed.Cost = 88; changed.Category = "Especial"; service.SaveProduct(changed);
        var window = new MainWindow(service, false) { Width = 1480, Height = 940 };
        ContextMenu? menu = null;
        try
        {
            var vm = window.Vm; vm.Page = "Relatórios";
            Capture(window, Path.Combine(output, "120-resultados-deposito.png")); window.Width = 1480; window.Height = 940; FlushLayout(window);
            var panel = Descendants<ReportsPanel>(window).Single();
            T Control<T>(string name) where T : FrameworkElement => panel.FindName(name) as T ?? throw new InvalidOperationException($"O controle de relatório {name} precisa existir.");
            var grid = Control<DataGrid>("ReportGrid"); var scroll = Control<ScrollViewer>("ReportsScroll");
            var search = Control<TextBox>("ReportSearchBox"); var category = Control<ComboBox>("ReportCategoryBox"); var period = Control<ComboBox>("ReportPeriodBox"); var pageSize = Control<ComboBox>("ReportPageSizeBox");
            string State() => JsonSerializer.Serialize(service.Snapshot, JsonStore.JsonOptions);
            static void Click(Button button) => ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            static void Invoke(MenuItem item) => ((IInvokeProvider)new MenuItemAutomationPeer(item).GetPattern(PatternInterface.Invoke)).Invoke();
            void Apply() { Click(Control<Button>("ReportApplyButton")); FlushLayout(window); }
            void Clear() { Click(Control<Button>("ReportClearButton")); FlushLayout(window); }
            void Draft(string query = "", string selectedCategory = "Todas as categorias")
            {
                ((IValueProvider)new TextBoxAutomationPeer(search).GetPattern(PatternInterface.Value)).SetValue(query); category.SelectedItem = selectedCategory; FlushLayout(window);
            }
            ProfitRow[] Expected(string query = "", string selectedCategory = "Todas as categorias")
            {
                DateTimeOffset? from = vm.ReportPeriod switch { "Hoje" => new DateTimeOffset(today), "Últimos 7 dias" => new DateTimeOffset(today.AddDays(-6)), "Últimos 30 dias" => new DateTimeOffset(today.AddDays(-29)), _ => null };
                DateTimeOffset? until = from is null ? null : new DateTimeOffset(today.AddDays(1));
                var state = service.Snapshot;
                return state.Sales.Where(sale => !sale.Canceled && (from is null || sale.Date >= from) && (until is null || sale.Date < until)).SelectMany(sale => sale.Lines)
                    .GroupBy(line => line.ProductId).Select(group => new ProfitRow(group.Last().ProductName, group.Sum(line => line.Quantity), group.Sum(line => line.NetRevenue), group.Sum(line => Money.Round(line.Quantity * line.UnitCost)), group.Sum(line => line.Profit)) { ProductId = group.Key })
                    .Where(row => (selectedCategory == "Todas as categorias" || state.Products.Single(product => product.Id == row.ProductId).Category == selectedCategory)
                        && new[] { row.Name, state.Products.Single(product => product.Id == row.ProductId).Code, state.Products.Single(product => product.Id == row.ProductId).Category }.Any(text => text.Contains(query.Trim(), StringComparison.CurrentCultureIgnoreCase)))
                    .OrderByDescending(row => row.Profit).ThenBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(row => row.ProductId).ToArray();
            }
            void Result(ProfitRow[] expected)
            {
                if (!vm.ReportFilteredItems.Select(row => row.Id).SequenceEqual(expected.Select(row => row.ProductId)) || vm.ProfitRows.Count != expected.Length || vm.ReportFilteredCount != expected.Length
                    || !vm.ReportPageItems.Select(row => row.Id).SequenceEqual(expected.Skip((vm.ReportPageNumber - 1) * vm.ReportPageSize).Take(vm.ReportPageSize).Select(row => row.ProductId)) || grid.Items.Count != vm.ReportPageItems.Count)
                    throw new InvalidOperationException("O relatório precisa filtrar e paginar pelo identificador do produto, sem juntar cadastros de mesmo nome.");
                if (vm.ReportRevenue != expected.Sum(row => row.Revenue) || vm.ReportCost != expected.Sum(row => row.Cost) || vm.ReportProfit != expected.Sum(row => row.Profit)
                    || vm.ReportMargin != (expected.Sum(row => row.Revenue) == 0 ? "0%" : (expected.Sum(row => row.Profit) / expected.Sum(row => row.Revenue)).ToString("P1")))
                    throw new InvalidOperationException("Receita, custo, lucro e margem precisam usar todos os produtos aplicados, com custo histórico, desconto distribuído e sem taxas de entrega ou canceladas.");
                foreach (var expectedRow in expected)
                {
                    var row = vm.ReportFilteredItems.Single(item => item.Id == expectedRow.ProductId);
                    if (row.Name != expectedRow.Name || row.Quantity != expectedRow.Quantity || row.Revenue != expectedRow.Revenue || row.Cost != expectedRow.Cost || row.Profit != expectedRow.Profit || row.Margin != expectedRow.Margin || row.Image is null)
                        throw new InvalidOperationException("Cada linha deve preservar seus valores históricos e carregar a imagem por ID do produto.");
                }
                foreach (var pair in new Dictionary<string, string> { ["ReportRevenueValue"] = vm.ReportRevenue.ToString("C2"), ["ReportCostValue"] = vm.ReportCost.ToString("C2"), ["ReportProfitValue"] = vm.ReportProfit.ToString("C2"), ["ReportMarginValue"] = vm.ReportMargin })
                    if (Control<TextBlock>(pair.Key).Text != pair.Value) throw new InvalidOperationException("Os cartões renderizados precisam refletir os totais reais do conjunto aplicado.");
            }
            string stable = State(); Result(Expected());
            if (vm.ReportFilteredCount < 21 || vm.ReportPageCount < 3 || vm.ReportPageItems.Count != 10 || Control<Button>("ReportPreviousButton").IsEnabled || !Control<Button>("ReportNextButton").IsEnabled)
                throw new InvalidOperationException("O relatório inicia com dez produtos por página e resume o conjunto completo.");
            var reportZero = vm.ReportFilteredItems.Single(row => row.Id == products[2].Id);
            if (reportZero.Revenue != 0 || reportZero.Margin != 0 || reportZero.Profit >= 0) throw new InvalidOperationException("Receita zero deve ter margem zero sem ocultar o custo e o lucro negativo.");
            var repeated = vm.ReportFilteredItems.Where(row => row.Name == products[0].Name).ToArray();
            if (repeated.Length != 2 || repeated.Select(row => row.Id).Distinct().Count() != 2 || repeated.Select(row => row.Category).Distinct().Count() != 2)
                throw new InvalidOperationException("Dois produtos com o mesmo nome precisam manter resultados, foto e categoria próprios após renomear o cadastro.");
            Click(Control<Button>("ReportNextButton")); FlushLayout(window); var selected = vm.ReportPageItems[0]; grid.SelectedItem = selected; vm.Refresh(); FlushLayout(window);
            if (vm.ReportPageNumber != 2 || vm.SelectedReportRow?.Id != selected.Id) throw new InvalidOperationException("Atualizar deve preservar a página e a seleção por identificador.");
            Click(Control<Button>("ReportNextButton")); FlushLayout(window);
            if (vm.ReportCanNext || vm.SelectedReportRow is not null) throw new InvalidOperationException("A última página limita navegação e limpa seleção ausente.");
            vm.ReportNextPageCommand.Execute(null); if (vm.ReportPageNumber != vm.ReportPageCount) throw new InvalidOperationException("O comando não pode ultrapassar o final do relatório.");
            foreach (int size in new[] { 20, 50, 10 }) { pageSize.SelectedItem = size; FlushLayout(window); Result(Expected()); if (vm.ReportPageNumber != 1) throw new InvalidOperationException("Mudar tamanho da página deve reiniciar a consulta no início."); }
            foreach (string value in vm.ReportPeriodOptions) { period.SelectedItem = value; FlushLayout(window); Result(Expected()); }
            period.SelectedItem = "Últimos 30 dias"; FlushLayout(window); Clear();
            var applied = vm.ReportFilteredItems.Select(row => row.Id).ToArray(); Draft("R000", "Especial");
            if (!vm.ReportFilteredItems.Select(row => row.Id).SequenceEqual(applied)) throw new InvalidOperationException("Editar busca e categoria não deve alterar resultados antes de Aplicar.");
            vm.Refresh(); FlushLayout(window); Result(Expected()); Apply(); Result(Expected("R000", "Especial")); AssertSearchReadable(search);
            if (vm.ReportFilteredCount != 1 || vm.ReportFilteredItems[0].Id != products[0].Id || vm.ReportCost >= changed.Cost)
                throw new InvalidOperationException("Buscar código e categoria deve localizar o produto certo e usar o custo das vendas, sem o novo custo do cadastro.");
            Draft("PRODUTO HISTÓRICO REPETIDO"); Apply(); Result(Expected("PRODUTO HISTÓRICO REPETIDO"));
            Draft(selectedCategory: "Refrigerantes"); Apply(); Result(Expected(selectedCategory: "Refrigerantes")); Clear(); Result(Expected());
            void SortHeader(string member)
            {
                grid.BringIntoView(); FlushLayout(window); grid.ScrollIntoView(vm.ReportPageItems.First(), grid.Columns.Single(column => column.SortMemberPath == member)); FlushLayout(window);
                var header = Descendants<DataGridColumnHeader>(grid).Single(item => item.Column?.SortMemberPath == member);
                if (new DataGridColumnHeaderAutomationPeer(header).GetPattern(PatternInterface.Invoke) is IInvokeProvider invoke) invoke.Invoke();
                else
                {
                    var click = typeof(DataGridColumnHeader).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, Type.EmptyTypes, null) ?? throw new InvalidOperationException("Clique nativo do cabeçalho indisponível.");
                    click.Invoke(header, null);
                }
                FlushLayout(window);
            }
            SortHeader("Revenue"); var sorted = Expected().OrderBy(row => row.Revenue).ThenBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(row => row.ProductId).ToArray(); Result(sorted);
            Click(Control<Button>("ReportNextButton")); FlushLayout(window); Result(sorted);
            SortHeader("Revenue"); Result(Expected().OrderByDescending(row => row.Revenue).ThenBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(row => row.ProductId).ToArray());
            if (vm.ReportPageNumber != 1 || !vm.ReportSortDescending) throw new InvalidOperationException("Cabeçalhos devem ordenar todo o conjunto e reiniciar a página.");
            var csv = window.ReportCsvRows().ToArray();
            if (csv.Length != vm.ReportFilteredCount + 1 || !csv.Skip(1).Select(row => row[1]).SequenceEqual(vm.ReportFilteredItems.Select(row => row.Code))) throw new InvalidOperationException("CSV precisa exportar todos os resultados aplicados na ordem escolhida, incluindo outras páginas.");
            Draft("consulta não aplicada"); if (window.ReportCsvRows().Count() != csv.Length) throw new InvalidOperationException("Exportação deve ignorar a busca ainda não aplicada."); Clear();
            if (State() != stable) throw new InvalidOperationException("Filtros, ordenação, paginação e CSV não podem alterar o cadastro ou as vendas.");
            Draft("Produto inexistente " + Guid.NewGuid().ToString("N")); Apply(); Result([]);
            if (vm.ReportEmptyVisibility != "Visible" || Control<Button>("ReportExportButton").IsEnabled || vm.ReportCanNext || vm.ReportHasSelection)
                throw new InvalidOperationException("Sem resultados, deve aparecer estado vazio e ações dependentes devem ficar desativadas.");
            Capture(window, Path.Combine(output, "125-resultados-sem-registros.png")); Clear();
            var target = vm.ReportFilteredItems.Single(row => row.Id == products[0].Id);
            void OnPage(Guid id) { int index = vm.ReportFilteredItems.ToList().FindIndex(row => row.Id == id); vm.ReportPageNumber = index / vm.ReportPageSize + 1; FlushLayout(window); }
            OnPage(target.Id); var currentRow = vm.ReportPageItems.Single(row => row.Id == target.Id); grid.ScrollIntoView(currentRow, grid.Columns.Last()); FlushLayout(window);
            var more = Descendants<Button>(grid).Single(button => button.Name == "ReportMoreButton" && button.DataContext is ReportProductRow row && row.Id == target.Id);
            Click(more); FlushLayout(window); menu = more.ContextMenu;
            if (menu is null || !menu.IsOpen || menu.Items.OfType<MenuItem>().Count() != 2) throw new InvalidOperationException("Menu do relatório deve ter somente consulta de detalhes e histórico do produto.");
            CaptureElement(menu, Path.Combine(output, "121-menu-resultados.png"));
            void WithDialog<T>(Action invoke, Action<T> inspect) where T : Window
            {
                Exception? failure = null; bool handled = false; DateTime started = DateTime.UtcNow;
                var watcher = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
                watcher.Tick += (_, _) =>
                {
                    var dialog = Application.Current.Windows.OfType<T>().FirstOrDefault(candidate => candidate.IsVisible && ReferenceEquals(candidate.Owner, window));
                    if (dialog is not null) { watcher.Stop(); handled = true; try { FlushLayout(dialog); inspect(dialog); } catch (Exception ex) { failure = ex; dialog.Close(); } }
                    else if ((DateTime.UtcNow - started).TotalSeconds > 6) { watcher.Stop(); failure = new InvalidOperationException("A ação do relatório precisa abrir a janela correspondente."); }
                };
                try { watcher.Start(); invoke(); while (!handled && failure is null) FlushLayout(window); if (failure is not null) throw failure; FlushLayout(window); }
                finally { watcher.Stop(); }
            }
            vm.SelectedReportRow = vm.ReportPageItems.First(row => row.Id != target.Id);
            WithDialog<ReportProductDetailsWindow>(() => Invoke(menu.Items.OfType<MenuItem>().Single(item => Equals(item.Header, "Ver detalhes"))), dialog =>
            {
                if (dialog.RowForDisplay.Id != target.Id || dialog.RevenueLabel.Text != target.Revenue.ToString("C2") || dialog.CostLabel.Text != target.Cost.ToString("C2") || dialog.ProfitLabel.Text != target.Profit.ToString("C2") || dialog.MarginLabel.Text != target.Margin.ToString("P1"))
                    throw new InvalidOperationException("Detalhes devem consultar o produto da própria linha e os valores históricos do período.");
                Capture(dialog, Path.Combine(output, "122-detalhes-resultado-produto.png")); Click(dialog.CloseButton);
            });
            menu.IsOpen = false;
            using (var history = new WindowCloser(window.CreateReportHistoryDialog(target)))
            {
                var dialog = history.Window;
                Capture(dialog, Path.Combine(output, "123-historico-produto-relatorio.png"));
                if (dialog.SalesForDisplay.Count != 2 || dialog.SalesForDisplay.Sum(row => row.Revenue) != target.Revenue || dialog.SalesForDisplay.Sum(row => row.Cost) != target.Cost || dialog.SalesForDisplay.Sum(row => row.Profit) != target.Profit || dialog.SaleDetailsButton.IsEnabled)
                    throw new InvalidOperationException("Histórico do produto deve abrir as vendas reais do período e seus valores devem somar o resultado da linha.");
                dialog.SalesGrid.SelectedItem = dialog.SalesForDisplay[0]; FlushLayout(dialog); if (!dialog.SaleDetailsButton.IsEnabled) throw new InvalidOperationException("Selecionar uma venda no histórico permite consultar seus detalhes.");
            }
            if (State() != stable) throw new InvalidOperationException("Consultar detalhes e histórico não pode alterar vendas, estoque ou cadastro.");
            window.Width = 1200; window.Height = 760; Clear(); pageSize.SelectedItem = 50; FlushLayout(window); Result(Expected());
            var root = (FrameworkElement)window.Content;
            foreach (string name in new[] { "ReportPeriodBox", "ReportExportButton", "ReportSearchBox", "ReportCategoryBox", "ReportApplyButton", "ReportClearButton", "ReportPreviousButton", "ReportNextButton", "ReportPageSizeBox" })
            {
                var element = Control<FrameworkElement>(name); element.BringIntoView(); FlushLayout(window); var bounds = element.TransformToAncestor(root).TransformBounds(new Rect(element.RenderSize));
                if (!element.IsVisible || bounds.Left < -.5 || bounds.Top < -.5 || bounds.Right > root.ActualWidth + .5 || bounds.Bottom > root.ActualHeight + .5 || bounds.Width <= 0 || bounds.Height <= 0)
                    throw new InvalidOperationException($"O controle de resultados {name} deve ficar acessível na janela menor.");
            }
            var cards = Control<UniformGrid>("ReportSummaryCards"); cards.BringIntoView(); FlushLayout(window);
            foreach (var label in Descendants<TextBlock>(cards))
            {
                var bounds = label.TransformToAncestor(cards).TransformBounds(new Rect(label.RenderSize));
                var measured = new FormattedText(label.Text, CultureInfo.CurrentCulture, label.FlowDirection, new Typeface(label.FontFamily, label.FontStyle, label.FontWeight, label.FontStretch), label.FontSize, label.Foreground, VisualTreeHelper.GetDpi(label).PixelsPerDip);
                double contentHeight = Math.Max(0, label.DesiredSize.Height - label.Margin.Top - label.Margin.Bottom);
                if (bounds.Left < -.5 || bounds.Right > cards.ActualWidth + .5 || contentHeight > label.ActualHeight + .5 || label.TextWrapping == TextWrapping.NoWrap && measured.Width > label.ActualWidth + 1)
                    throw new InvalidOperationException($"Cartões de resultados devem mostrar títulos e valores completos. Texto: {label.Text}");
            }
            if (scroll.ExtentWidth > scroll.ViewportWidth + .5 || !ControlledScrolling.GetIsEnabled(scroll)) throw new InvalidOperationException("Painel de resultados deve caber na largura menor e usar rolagem controlada.");
            scroll.ScrollToTop(); FlushLayout(window); Capture(window, Path.Combine(output, "124-resultados-tela-menor.png"));
            void CheckWheel(FrameworkElement area, Func<FrameworkElement> source, bool inner)
            {
                var viewer = ControlledScrolling.GetScrollViewer(area) ?? throw new InvalidOperationException("A área de resultados precisa ser rolável.");
                if (viewer.ScrollableHeight < 30 || !ControlledScrolling.GetIsEnabled(area)) throw new InvalidOperationException("A área precisa permitir conferir gestos e limites.");
                double outer = scroll.VerticalOffset, step = SystemParameters.WheelScrollLines == 0 ? 0 : 22;
                void Wheel(int delta, bool flush = true) { var args = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta) { RoutedEvent = Mouse.PreviewMouseWheelEvent }; source().RaiseEvent(args); if (flush) FlushLayout(window); if (!args.Handled) throw new InvalidOperationException("Touchpad deve ser tratado na área de resultados acionada."); }
                void Offset(double expected) { if (Math.Abs(viewer.VerticalOffset - Math.Clamp(expected, 0, viewer.ScrollableHeight)) > .6 || inner && Math.Abs(scroll.VerticalOffset - outer) > .6 || State() != stable) throw new InvalidOperationException("Rolagem de resultados deve ser proporcional, respeitar limites e preservar dados e área externa."); }
                viewer.ScrollToVerticalOffset(25); FlushLayout(window); double before = viewer.VerticalOffset;
                Wheel(-20); Offset(before + step / 6); Wheel(20); Offset(before); for (int index = 0; index < 6; index++) Wheel(-20, false); FlushLayout(window); Offset(before + step);
                for (int index = 0; index < 6; index++) Wheel(20, false); FlushLayout(window); Offset(before);
                viewer.ScrollToTop(); FlushLayout(window); Wheel(120); Offset(0); viewer.ScrollToBottom(); FlushLayout(window); Wheel(-120); Offset(viewer.ScrollableHeight); viewer.ScrollToTop(); FlushLayout(window);
            }
            grid.BringIntoView(); FlushLayout(window); CheckWheel(grid, () => Descendants<TextBlock>(grid).First(block => block.IsVisible && block.DataContext is ReportProductRow), true);
            CheckWheel(scroll, () => scroll, false);
            service.Login("RELATORIO_OPERADOR", "1234"); vm.Refresh(); FlushLayout(window); stable = State();
            bool exportDenied = false, detailsDenied = false;
            try { window.ReportCsvRows().ToArray(); } catch (UnauthorizedAccessException) { exportDenied = true; }
            try { window.CreateReportDetailsDialog(target).Close(); } catch (UnauthorizedAccessException) { detailsDenied = true; }
            if (!exportDenied || !detailsDenied || scroll.IsEnabled || State() != stable) throw new InvalidOperationException("Resultados do depósito permanecem exclusivos do administrador, sem permitir exportação ou consulta por chamada antiga do operador.");
            File.WriteAllText(Path.Combine(output, "relatorios-smoke-ok.txt"), "Resultado por ID, incluindo nomes iguais e cadastro renomeado. Receita líquida, custo histórico, lucro, margem ponderada, receita zero, prejuízo e canceladas conferidos com vendas reais. Períodos e limites diários, busca/categoria aplicadas, drafts preservados, CSV completo e ordenação global/paginação/seleção conferidos. Detalhes e histórico somente consulta, estados vazios e permissões de administrador. Capturas normal/menor, títulos e valores sem cortes, controles acessíveis e touchpad proporcional em grade e página. Sem curvas fictícias ou alteração de dados reais.\n");
        }
        finally { if (menu is not null) menu.IsOpen = false; if (window.IsVisible) window.Close(); }
    }
    private sealed class WindowCloser(ReportProductHistoryWindow window) : IDisposable
    {
        internal ReportProductHistoryWindow Window => window;
        public void Dispose() { if (window.IsVisible) window.Close(); }
    }
}
