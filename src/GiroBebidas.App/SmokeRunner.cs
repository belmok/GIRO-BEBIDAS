using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public static class SmokeRunner
{
    public static void Run(StoreService service, string output)
    {
        Directory.CreateDirectory(output);
        var login = new LoginWindow(service); Capture(login, Path.Combine(output, "01-login.png")); login.Close();
        service.Login("ADMIN", "ADMIN");
        if (!service.Snapshot.Customers.Any()) service.SaveCustomer(new() { Name = "Marina Souza", Phone = "(11) 99999-0000", Address = "Rua dos Ipês, 85 • Centro", CreditLimit = 500 });
        if (!service.Snapshot.CashSessions.Any()) service.OpenCash(250);
        if (service.Snapshot.Users.Count == 1) service.SaveUser(new() { Name = "João • balcão", Username = "JOAO", Role = UserRole.Operador }, "1234");
        var window = new MainWindow(service, false); var vm = window.Vm;
        vm.AddProduct(vm.Snapshot.Products[0], 12); vm.AddProduct(vm.Snapshot.Products[4], 2); vm.AddProduct(vm.Snapshot.Products[12], 1);
        vm.CartCustomer = vm.CustomerOptions.Last(); vm.Discount = 2;
        if (!service.Snapshot.Orders.Any()) service.CreateOrder([new(vm.Snapshot.Products[2].Id, 6)], service.Snapshot.Customers[0].Id, true, "Rua dos Ipês, 85 • Centro", "Entregar após 18h", 0, 5);
        if (!service.Snapshot.Sales.Any()) service.CompleteSale(new([new(vm.Snapshot.Products[0].Id, 2)], 0, 0, null, PaymentMethod.Pix, 9));
        vm.Refresh(); Capture(window, Path.Combine(output, "02-pdv.png"));
        int index = 3;
        foreach (var page in new[] { "Estoque", "Clientes", "Pedidos", "Caixa", "Vendas", "Relatórios", "Usuários", "Equipamentos", "Configurações" })
        {
            vm.Page = page; Capture(window, Path.Combine(output, $"{index++:00}-{page.ToLowerInvariant()}.png"));
        }
        string samplePhoto = Path.Combine(output, "foto-de-teste.png");
        var photoVisual = new DrawingVisual(); using (var context = photoVisual.RenderOpen()) context.DrawImage(BeverageArt.Create(vm.Snapshot.Products[0]), new Rect(0, 0, 160, 140));
        var photoBitmap = new RenderTargetBitmap(160, 140, 96, 96, PixelFormats.Pbgra32); photoBitmap.Render(photoVisual);
        var photoEncoder = new PngBitmapEncoder(); photoEncoder.Frames.Add(BitmapFrame.Create(photoBitmap)); using (var stream = File.Create(samplePhoto)) photoEncoder.Save(stream);
        var photoProduct = vm.Snapshot.Products[0]; photoProduct.PhotoBase64 = PhotoTools.Import(samplePhoto); photoProduct.Description = "Cerveja pilsen em lata de 350 ml. Ideal para servir gelada.";
        service.SaveProduct(photoProduct); vm.Refresh();
        if (PhotoTools.Decode(service.Snapshot.Products[0].PhotoBase64) is null) throw new InvalidOperationException("Falha na importação da foto.");
        var photoLine = vm.Cart.Single(l => l.Product.Id == photoProduct.Id);
        decimal photoCartTotal = vm.Total;
        vm.Cart[vm.Cart.IndexOf(photoLine)] = new CartLine(vm.Snapshot.Products.Single(p => p.Id == photoProduct.Id), photoLine.Quantity);
        if (vm.Cart.Single(l => l.Product.Id == photoProduct.Id).Image is not BitmapSource || vm.Total != photoCartTotal)
            throw new InvalidOperationException("A foto persistida precisa aparecer no item do carrinho sem alterar seu total.");
        vm.Refresh();
        service.ExportBackup(Path.Combine(output, "backup-com-foto.json"));
        var form = window.CreateProductDialog(photoProduct, false);
        // An unshown owner cannot be assigned to a visible window; clear it for offscreen QA.
        form.Owner = null; form.SizeToContent = SizeToContent.Manual; form.Height = Math.Min(800, form.MaxHeight); Capture(form, Path.Combine(output, "12-cadastro-produto.png"));
        form.ScrollToField("origin"); Capture(form, Path.Combine(output, "19-dados-fiscais-produto.png")); form.Close();
        vm.Page = "Fiscal"; Capture(window, Path.Combine(output, "16-configuracao-fiscal.png"));
        window.FiscalConfigurationPanel.ShowChecks(); Capture(window, Path.Combine(output, "20-conferencia-fiscal.png"));
        var issuerForm = window.FiscalConfigurationPanel.CreateIssuerDialog(); issuerForm.Owner = null; issuerForm.SizeToContent = SizeToContent.Manual; issuerForm.Height = Math.Min(800, issuerForm.MaxHeight);
        Capture(issuerForm, Path.Combine(output, "17-empresa-emitente.png")); issuerForm.Close();
        var fiscalForm = window.FiscalConfigurationPanel.CreateOptionsDialog(); fiscalForm.Owner = null; fiscalForm.SizeToContent = SizeToContent.Manual; fiscalForm.Height = Math.Min(730, fiscalForm.MaxHeight);
        Capture(fiscalForm, Path.Combine(output, "18-parametros-nfce.png")); fiscalForm.Close();
        if (service.Snapshot.Fiscal.Options.Environment != FiscalEnvironment.Homologacao || vm.FiscalChecks.Single(c => c.Item == "Autorização SEFAZ").Complete)
            throw new InvalidOperationException("A configuração inicial não pode simular emissão fiscal autorizada.");
        vm.Page = "Atualizações"; Capture(window, Path.Combine(output, "21-atualizacoes.png"));
        var updates = window.UpdatesConfigurationPanel;
        string configuredRepository = updates.RepositoryBox.Text;
        updates.RepositoryVisibilityButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout();
        if (updates.RepositoryBox.Visibility != Visibility.Hidden || updates.RepositoryMask.Visibility != Visibility.Visible || (!string.IsNullOrEmpty(configuredRepository) && updates.RepositoryMask.Text.Contains(configuredRepository, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("O endereço do repositório precisa ficar oculto na tela.");
        Capture(window, Path.Combine(output, "22-atualizacoes-repositorio-oculto.png"));
        updates.RepositoryVisibilityButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout();
        if (updates.RepositoryBox.Visibility != Visibility.Visible || updates.RepositoryBox.Text != configuredRepository)
            throw new InvalidOperationException("Mostrar o repositório deve preservar o endereço configurado.");
        bool automaticCheck = updates.AutoCheckBox.IsChecked == true;
        var togglePeer = new CheckBoxAutomationPeer(updates.AutoCheckBox);
        var toggleProvider = (IToggleProvider)togglePeer.GetPattern(PatternInterface.Toggle);
        toggleProvider.Toggle();
        if (updates.AutoCheckBox.IsChecked == automaticCheck) throw new InvalidOperationException("O interruptor de atualizações não alternou a opção.");
        toggleProvider.Toggle();
        if (updates.AutoCheckBox.IsChecked != automaticCheck) throw new InvalidOperationException("O interruptor não preservou a configuração ao retornar ao estado inicial.");
        window.Width = 1200; window.Height = 760; vm.Page = "PDV"; Capture(window, Path.Combine(output, "15-pdv-tela-menor.png"));
        if (window.CartItemsViewport.ActualHeight < 230) throw new InvalidOperationException("O carrinho precisa manter espaço suficiente para os produtos na tela menor.");
        MainWindow.ScrollMenu(window.MenuScroll, -20, 3); window.UpdateLayout();
        if (window.MenuScroll.ScrollableHeight > 0 && window.MenuScroll.VerticalOffset <= 0) throw new InvalidOperationException("A rolagem fracionária do menu não respondeu ao gesto do touchpad.");
        MainWindow.ScrollMenu(window.MenuScroll, 120, 3); window.UpdateLayout();
        vm.Page = "Atualizações"; Capture(window, Path.Combine(output, "23-atualizacoes-tela-menor.png"));
        updates.UpdatesScroll.ScrollToEnd(); window.UpdateLayout();
        var installPosition = updates.InstallButton.TranslatePoint(new Point(0, 0), updates.UpdatesScroll);
        if (installPosition.X < 0 || installPosition.X + updates.InstallButton.ActualWidth > updates.UpdatesScroll.ActualWidth || installPosition.Y < 0 || installPosition.Y + updates.InstallButton.ActualHeight > updates.UpdatesScroll.ActualHeight)
            throw new InvalidOperationException("O botão de atualização precisa ficar acessível na janela menor.");
        updates.AutoCheckBox.IsChecked = false;
        Capture(window, Path.Combine(output, "24-atualizacoes-interruptor-desativado.png"));
        updates.AutoCheckBox.IsChecked = automaticCheck;
        var switchSamples = new StackPanel { Margin = new Thickness(20) };
        switchSamples.Children.Add(new CheckBox { Content = "Opção ativada", IsChecked = true });
        switchSamples.Children.Add(new CheckBox { Content = "Opção desativada", IsChecked = false });
        var switchWindow = new Window { Width = 370, Height = 210, Content = switchSamples, Background = Brushes.White };
        Capture(switchWindow, Path.Combine(output, "25-interruptores.png")); switchWindow.Close();
        CheckProductUi(window, output);
        CheckCartAmounts(window);
        CheckCashStatus(output);
        CheckStockUi(window, output);
        vm.ClearCart(); window.Close();
        File.WriteAllText(Path.Combine(output, "smoke-ok.txt"), "Login, PDV e dez módulos renderizados. Cadastro completo de produto e campos fiscais renderizados. Configuração fiscal, empresa e parâmetros NFC-e renderizados. Foto importada, persistida, exibida no carrinho e exportada no backup. Layout menor renderizado. Painel de atualizações e interruptores renderizados. Repositório ocultado e reexibido sem alteração; interruptor alternado por automação; botão de atualização acessível na janela menor. Pesquisa nativa por nome, código e código de barras validada em grade e lista; texto e cursor sem recorte; Enter adiciona o produto correto e limpa a pesquisa; F2 seleciona e focaliza a busca. Categoria, estado sem resultados e layout dos produtos em 1200 x 760 conferidos. Alternar grade/lista preserva o total; pedidos reservados bloqueiam alterações no carrinho. Desconto e entrega confirmados antes da perda de foco; texto inválido bloqueado e corrigido. Caixa fechado vermelho e aberto verde validados em dados isolados. Menu do caixa acionado por automação, atualizado sem reabrir e fechado por Esc ou clique fora; histórico acessível e configurações restritas ao administrador. Formulário de configurações do caixa renderizado; fundo sugerido salvo sem alterar o saldo e adotado na próxima abertura pelo operador.\n");
    }
    private static void CheckStockUi(MainWindow window, string output)
    {
        var vm = window.Vm;
        string originalPage = vm.Page, originalSearch = vm.StockSearch, originalCategory = vm.StockCategory,
            originalSort = vm.StockSort, originalMessage = vm.Message;
        bool originalDescending = vm.StockSortDescending;
        Guid? originalSelected = vm.SelectedStock?.Product.Id;
        double originalWidth = window.Width, originalHeight = window.Height;
        var searchProvider = (IValueProvider)new TextBoxAutomationPeer(window.StockSearchInput).GetPattern(PatternInterface.Value);
        ContextMenu? openedMenu = null;
        FormDialog? duplicate = null, details = null;
        Window? history = null;
        var unused = new Product { Name = "Produto de teste do estoque", Code = "UI-STOCK-" + Guid.NewGuid().ToString("N")[..6].Replace('0', 'G'), Price = 10, Stock = 0, MinimumStock = 0, Category = "Sem álcool especial" };
        vm.Service.SaveProduct(unused);
        var probe = vm.Service.Snapshot.Products.Single(p => p.Code == "005" && p.Active && !p.IsWeighed);
        var photoProduct = vm.Service.Snapshot.Products.First(p => p.PhotoBase64.Length > 0);
        // Round-trip only the isolated smoke fixture to render both movement signs.
        vm.Service.AdjustStock(probe.Id, 1, "Conferência visual de entrada de estoque");
        vm.Service.AdjustStock(probe.Id, -1, "Conferência visual de saída de estoque");
        vm.Refresh();

        void Search(string query) { searchProvider.SetValue(query); FlushLayout(window); }
        void ResetFilters()
        {
            window.StockCategoryFilter.SelectedItem = "Todas as categorias";
            window.StockSortFilter.SelectedItem = "Nome do produto";
            vm.StockSortDescending = false; Search("");
        }
        void AssertBounds(FrameworkElement element)
        {
            var content = (FrameworkElement)window.Content;
            var position = element.TranslatePoint(new Point(), content);
            if (!element.IsVisible || element.ActualWidth <= 0 || element.ActualHeight <= 0 || position.X < -0.5 || position.Y < -0.5 ||
                position.X + element.ActualWidth > content.ActualWidth + 0.5 || position.Y + element.ActualHeight > content.ActualHeight + 0.5)
                throw new InvalidOperationException($"O controle de estoque {element.Name} precisa permanecer visível e inteiro na janela menor.");
        }
        Button MoreButton(Guid productId)
        {
            var row = vm.StockRows.Single(r => r.Product.Id == productId);
            window.StockProductsGrid.ScrollIntoView(row, window.StockProductsGrid.Columns.Last()); FlushLayout(window);
            return Descendants<Button>(window.StockProductsGrid).Single(b => b.Name == "StockMoreButton" && b.DataContext is StockRow r && r.Product.Id == productId);
        }
        MenuItem[] OpenMenu(Guid productId, bool canDelete)
        {
            openedMenu?.SetCurrentValue(ContextMenu.IsOpenProperty, false);
            var button = MoreButton(productId);
            ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            FlushLayout(window);
            openedMenu = button.ContextMenu ?? throw new InvalidOperationException("Cada produto precisa ter seu menu de ações.");
            openedMenu.UpdateLayout();
            var items = openedMenu.Items.OfType<MenuItem>().ToArray();
            var row = (StockRow)button.DataContext;
            if (!openedMenu.IsOpen || !ReferenceEquals(openedMenu.DataContext, row) || Math.Abs(openedMenu.ActualWidth - 260) > 0.5 || items.Length != 4 ||
                items[0].Header?.ToString() != "Ver detalhes" || items[1].Header?.ToString() != "Duplicar produto" ||
                items[2].Header?.ToString() != row.ToggleActiveLabel || items[3].Header?.ToString() != "Excluir produto" ||
                row.CanDelete != canDelete || items[3].IsEnabled != canDelete || items[3].ToolTip?.ToString() != row.DeleteHint)
                throw new InvalidOperationException("O menu do produto precisa carregar quatro ações, estado ativo e permissão de exclusão do produto clicado.");
            foreach (var item in items)
            {
                var position = item.TranslatePoint(new Point(), openedMenu);
                if (item.ActualWidth <= 0 || item.ActualHeight <= 0 || position.X < -0.5 || position.Y < -0.5 ||
                    position.X + item.ActualWidth > openedMenu.ActualWidth + 0.5 || position.Y + item.ActualHeight > openedMenu.ActualHeight + 0.5)
                    throw new InvalidOperationException("As ações do menu do produto precisam ser legíveis e não sofrer recorte.");
            }
            return items;
        }

        try
        {
            window.Width = 1480; window.Height = 940; vm.Page = "Estoque"; vm.Message = ""; ResetFilters();
            var photoRow = vm.StockRows.Single(r => r.Product.Id == photoProduct.Id);
            window.StockProductsGrid.ScrollIntoView(photoRow, window.StockProductsGrid.Columns[0]); FlushLayout(window);
            if (photoRow.Image is not BitmapSource || !Descendants<Image>(window.StockProductsGrid).Any(i => i.DataContext is StockRow r && r.Product.Id == photoProduct.Id && ReferenceEquals(i.Source, r.Image)))
                throw new InvalidOperationException("A foto persistida precisa aparecer na coluna do produto em estoque.");
            Capture(window, Path.Combine(output, "35-estoque-novo.png"));

            foreach (string query in new[] { probe.Name, probe.Code, probe.Barcode })
            {
                Search(query);
                if (string.IsNullOrEmpty(query) || vm.StockSearch != query || window.StockSearchInput.Text != query || vm.StockRows.Count != 1 ||
                    vm.StockRows[0].Product.Id != probe.Id || window.StockProductsGrid.Items.Count != 1)
                    throw new InvalidOperationException($"A pesquisa de estoque por {query} precisa encontrar somente o produto correspondente.");
                AssertSearchReadable(window.StockSearchInput);
            }
            Search(probe.Category);
            if (vm.StockRows.Count == 0 || vm.StockRows.Any(r => r.Category != probe.Category))
                throw new InvalidOperationException("A busca de estoque precisa reconhecer a categoria do produto.");
            Search("");
            window.StockCategoryFilter.SelectedItem = probe.Category; FlushLayout(window);
            if (vm.StockCategory != probe.Category || vm.StockRows.Count == 0 || vm.StockRows.Any(r => r.Category != probe.Category))
                throw new InvalidOperationException("A lista de categorias precisa aplicar o filtro ao estoque.");
            window.StockCategoryFilter.SelectedItem = "Todas as categorias"; FlushLayout(window);
            if (vm.StockCategory != "Todas as categorias" || vm.StockRows.Count != vm.Snapshot.Products.Count)
                throw new InvalidOperationException("Todas as categorias precisa restaurar a lista completa do estoque.");

            foreach (string sort in new[] { "Preço de venda", "Nome do produto" })
            {
                window.StockSortFilter.SelectedItem = sort; vm.StockSortDescending = false; FlushLayout(window);
                if (vm.StockSort != sort || (sort == "Preço de venda"
                    ? !vm.StockRows.Select(r => r.Price).SequenceEqual(vm.StockRows.Select(r => r.Price).OrderBy(p => p))
                    : !vm.StockRows.Select(r => r.Name).SequenceEqual(vm.StockRows.Select(r => r.Name).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase))))
                    throw new InvalidOperationException("A seleção de ordenação precisa organizar os valores em ordem crescente.");
                ((IInvokeProvider)new ButtonAutomationPeer(window.StockSortDirectionButton).GetPattern(PatternInterface.Invoke)).Invoke(); FlushLayout(window);
                if (!vm.StockSortDescending || (sort == "Preço de venda"
                    ? !vm.StockRows.Select(r => r.Price).SequenceEqual(vm.StockRows.Select(r => r.Price).OrderByDescending(p => p))
                    : !vm.StockRows.Select(r => r.Name).SequenceEqual(vm.StockRows.Select(r => r.Name).OrderByDescending(n => n, StringComparer.CurrentCultureIgnoreCase))))
                    throw new InvalidOperationException("O botão de inverter ordem precisa ordenar nomes e preços em ordem decrescente.");
            }
            ResetFilters();
            if (vm.RecentStockMovements.Count > 5 || window.LatestStockMovementsGrid.Items.Count != vm.RecentStockMovements.Count ||
                vm.RecentStockMovements.Any(m => (m.Sign > 0 && !m.QuantityLabel.StartsWith('+')) || (m.Sign < 0 && !m.QuantityLabel.StartsWith('-'))))
                throw new InvalidOperationException("Últimas movimentações precisa limitar a lista a cinco registros e identificar entradas e saídas pelo sinal.");
            foreach (int sign in new[] { 1, -1 })
            {
                Color color = (Color)ColorConverter.ConvertFromString(sign > 0 ? "#187B52" : "#B4483E");
                if (!Descendants<TextBlock>(window.LatestStockMovementsGrid).Any(t => t.DataContext is StockMovementRow m && m.Sign == sign &&
                    t.Text == m.QuantityLabel && t.Foreground is SolidColorBrush brush && brush.Color == color))
                    throw new InvalidOperationException("Entradas devem aparecer em verde e saídas em vermelho nas movimentações recentes.");
            }

            window.Width = 1200; window.Height = 760; Search(probe.Name); AssertSearchReadable(window.StockSearchInput);
            foreach (var element in new FrameworkElement[] { window.StockSearchFrame, window.StockCategoryFilter, window.StockSortFilter, window.StockSortDirectionButton,
                window.StockAdjustButton, window.StockEditSelectedButton, window.StockNewProductButton, window.AllStockMovementsButton }) AssertBounds(element);
            Search(""); window.StockProductsGrid.ScrollIntoView(vm.StockRows.First(), window.StockProductsGrid.Columns[0]); FlushLayout(window);
            Capture(window, Path.Combine(output, "36-estoque-menor.png"));
            window.Width = 1480; window.Height = 940; FlushLayout(window);

            OpenMenu(photoProduct.Id, false); CaptureElement(openedMenu!, Path.Combine(output, "37-produto-acoes.png")); openedMenu!.IsOpen = false;
            var reserved = vm.StockRows.First(r => r.Reserved > 0);
            OpenMenu(reserved.Product.Id, false); openedMenu!.IsOpen = false;
            var actions = OpenMenu(unused.Id, true);
            if (actions[2].Header?.ToString() != "Desativar produto") throw new InvalidOperationException("Produto ativo precisa permitir desativação no menu.");
            ((IInvokeProvider)new MenuItemAutomationPeer(actions[2]).GetPattern(PatternInterface.Invoke)).Invoke(); FlushLayout(window);
            if (openedMenu!.IsOpen || vm.Snapshot.Products.Single(p => p.Id == unused.Id).Active || vm.StockRows.Single(r => r.Product.Id == unused.Id).Status != "Inativo" ||
                new JsonStore(vm.Service.Store.DirectoryPath).Snapshot().Products.Single(p => p.Id == unused.Id).Active)
                throw new InvalidOperationException("Desativar no menu precisa fechar as ações, persistir a mudança e exibir Inativo.");
            actions = OpenMenu(unused.Id, true);
            if (actions[2].Header?.ToString() != "Ativar produto") throw new InvalidOperationException("Produto inativo precisa permitir ativação no menu.");
            ((IInvokeProvider)new MenuItemAutomationPeer(actions[2]).GetPattern(PatternInterface.Invoke)).Invoke(); FlushLayout(window);
            if (openedMenu!.IsOpen || !vm.Snapshot.Products.Single(p => p.Id == unused.Id).Active || vm.StockRows.Single(r => r.Product.Id == unused.Id).Status != "Sem estoque")
                throw new InvalidOperationException("Ativar no menu precisa restaurar o cadastro e mostrar seu estoque atual.");

            foreach (string category in new[] { unused.Category, "cervejas" })
            {
                var customProduct = vm.Service.Snapshot.Products.Single(p => p.Id == unused.Id);
                customProduct.Category = category; vm.Service.SaveProduct(customProduct); vm.Refresh();
                var customEdit = window.CreateProductDialog(customProduct, true); customEdit.Owner = null;
                try
                {
                    if (customEdit.Value("category") != category)
                        throw new InvalidOperationException($"Editar precisa carregar a categoria exata '{category}', incluindo categorias personalizadas e variações de maiúsculas.");
                    customEdit.Submit?.Invoke(customEdit);
                    if (vm.Service.Snapshot.Products.Single(p => p.Id == unused.Id).Category != category)
                        throw new InvalidOperationException($"Salvar a edição precisa preservar a categoria '{category}' sem substituí-la por uma opção padrão.");
                }
                finally { customEdit.Close(); }

                var customCopy = window.CreateDuplicateProductDialog(vm.Service.Snapshot.Products.Single(p => p.Id == unused.Id)); customCopy.Owner = null;
                try
                {
                    int countBeforeCustomCopy = vm.Service.Snapshot.Products.Count;
                    string customCopyCode = customCopy.Value("code");
                    if (customCopy.Value("category") != category || customCopy.Decimal("stock") != 0 || customCopy.Value("barcode") != "" ||
                        vm.Service.Snapshot.Products.Any(p => p.Code == customCopyCode))
                        throw new InvalidOperationException($"Duplicar precisa preparar um cadastro independente com a categoria exata '{category}'.");
                    customCopy.Submit?.Invoke(customCopy);
                    var customSaved = vm.Service.Snapshot.Products.Single(p => p.Code == customCopyCode);
                    if (vm.Service.Snapshot.Products.Count != countBeforeCustomCopy + 1 || customSaved.Id == unused.Id || customSaved.Category != category ||
                        vm.Service.Snapshot.Products.Single(p => p.Id == unused.Id).Category != category)
                        throw new InvalidOperationException($"Salvar a duplicação precisa manter a categoria '{category}' na cópia e na origem.");
                }
                finally { customCopy.Close(); }
            }
            var restoredCustomProduct = vm.Service.Snapshot.Products.Single(p => p.Id == unused.Id);
            restoredCustomProduct.Category = unused.Category; vm.Service.SaveProduct(restoredCustomProduct); vm.Refresh();

            string sourceBefore = System.Text.Json.JsonSerializer.Serialize(photoProduct, JsonStore.JsonOptions);
            int countBefore = vm.Service.Snapshot.Products.Count;
            duplicate = window.CreateDuplicateProductDialog(photoProduct); duplicate.Owner = null; duplicate.SizeToContent = SizeToContent.Manual; duplicate.Height = Math.Min(800, duplicate.MaxHeight);
            string duplicateCode = duplicate.Value("code");
            if (duplicate.Decimal("stock") != 0 || duplicate.Value("barcode") != "" || duplicate.Value("name") != photoProduct.Name + " (cópia)" ||
                duplicateCode == photoProduct.Code || vm.Service.Snapshot.Products.Any(p => p.Code == duplicateCode) ||
                duplicate.Decimal("price") != photoProduct.Price || duplicate.Value("description") != photoProduct.Description ||
                duplicate.Value("ncm") != photoProduct.Tax.Ncm || duplicate.Value("cest") != photoProduct.Tax.Cest || duplicate.Value("cfop") != photoProduct.Tax.Cfop ||
                vm.Service.Snapshot.Products.Count != countBefore || System.Text.Json.JsonSerializer.Serialize(photoProduct, JsonStore.JsonOptions) != sourceBefore)
                throw new InvalidOperationException("Duplicar precisa preparar uma cópia independente, com código novo, estoque zero, sem código de barras e sem alterar o cadastro original.");
            Capture(duplicate, Path.Combine(output, "38-duplicar-produto.png"));
            if (!Descendants<Image>((FrameworkElement)duplicate.Content).Any(i => i.Source is BitmapSource))
                throw new InvalidOperationException("A duplicação precisa preservar a foto do produto.");
            duplicate.Submit?.Invoke(duplicate);
            var savedCopy = vm.Service.Snapshot.Products.Single(p => p.Code == duplicateCode);
            if (vm.Service.Snapshot.Products.Count != countBefore + 1 || savedCopy.Id == photoProduct.Id || savedCopy.Stock != 0 || savedCopy.Barcode != "" ||
                savedCopy.PhotoBase64 != photoProduct.PhotoBase64 || System.Text.Json.JsonSerializer.Serialize(savedCopy.Tax, JsonStore.JsonOptions) != System.Text.Json.JsonSerializer.Serialize(photoProduct.Tax, JsonStore.JsonOptions) ||
                System.Text.Json.JsonSerializer.Serialize(vm.Service.Snapshot.Products.Single(p => p.Id == photoProduct.Id), JsonStore.JsonOptions) != sourceBefore)
                throw new InvalidOperationException("Salvar a duplicação precisa criar outro produto com foto e dados fiscais preservados, sem mudar a origem.");
            duplicate.Close(); duplicate = null; vm.Refresh();

            details = window.CreateStockDetailsDialog(photoProduct); details.Owner = null; details.SizeToContent = SizeToContent.Manual; details.Height = Math.Min(740, details.MaxHeight);
            Capture(details, Path.Combine(output, "39-detalhes-produto.png"));
            var detailText = string.Join("\n", Descendants<TextBlock>((FrameworkElement)details.Content).Select(t => t.Text));
            if (!detailText.Contains(photoProduct.Name, StringComparison.Ordinal) || !detailText.Contains("Código: " + photoProduct.Code, StringComparison.Ordinal) ||
                !detailText.Contains("Reservado:", StringComparison.Ordinal) || !detailText.Contains("NCM:", StringComparison.Ordinal) ||
                !Descendants<Image>((FrameworkElement)details.Content).Any(i => i.Source is BitmapSource))
                throw new InvalidOperationException("Detalhes precisa apresentar foto, identificação, reserva e dados fiscais do produto.");
            details.Close(); details = null;
            history = window.CreateStockMovementsWindow(); history.Owner = null; Capture(history, Path.Combine(output, "40-movimentacoes-estoque.png"));
            var historyTable = Descendants<DataGrid>((FrameworkElement)history.Content).Single();
            if (historyTable.Items.Count != vm.Service.Snapshot.StockMovements.Count || historyTable.Items.OfType<StockMovementRow>().Count() != historyTable.Items.Count)
                throw new InvalidOperationException("Ver todas precisa apresentar o histórico completo das movimentações de estoque.");
            history.Close(); history = null;
        }
        finally
        {
            if (openedMenu is not null) openedMenu.IsOpen = false;
            duplicate?.Close(); details?.Close(); history?.Close();
            if (vm.Service.Snapshot.Products.Any(p => p.Id == unused.Id && !p.Active)) vm.Service.SetProductActive(unused.Id, true);
            vm.StockSearch = originalSearch; vm.StockCategory = originalCategory; vm.StockSort = originalSort; vm.StockSortDescending = originalDescending;
            vm.Refresh(); vm.SelectedStock = vm.StockRows.FirstOrDefault(r => r.Product.Id == originalSelected);
            vm.Page = originalPage; vm.Message = originalMessage; window.Width = originalWidth; window.Height = originalHeight; FlushLayout(window);
        }
        File.AppendAllText(Path.Combine(output, "estoque-smoke-ok.txt"), "Busca nativa por nome, código, barras e categoria; filtros e ordenação nos dois sentidos; fotos e movimentações com sinais e cores; layout 1200 x 760; menus por linha com exclusão protegida; ativação e desativação persistidas; edição e duplicação salvas preservando categoria personalizada e variante minúscula; cópia independente com foto e tributação; detalhes e histórico completo conferidos.\n");
    }
    private static void CheckProductUi(MainWindow window, string output)
    {
        var vm = window.Vm;
        var originalCart = vm.Cart.Select(l => (Line: l, Quantity: l.Quantity)).ToArray();
        var originalCustomer = vm.CartCustomer?.Id;
        decimal originalDiscount = vm.Discount, originalDelivery = vm.DeliveryFee, originalTotal = vm.Total;
        string originalPage = vm.Page, originalSearch = vm.Search, originalCategory = vm.Category, originalMessage = vm.Message;
        bool originalView = vm.IsProductsList;
        double originalWidth = window.Width, originalHeight = window.Height;
        var valueProvider = (IValueProvider)new TextBoxAutomationPeer(window.ProductSearch).GetPattern(PatternInterface.Value);
        var probe = vm.Snapshot.Products.Single(p => p.Code == "005" && p.Active && !p.IsWeighed);
        void Search(string text) { valueProvider.SetValue(text); FlushLayout(window); }
        void ShowView(bool list)
        {
            decimal totalBefore = vm.Total;
            var button = list ? window.ProductListButton : window.ProductGridButton;
            ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            FlushLayout(window);
            if (vm.IsProductsList != list || vm.Total != totalBefore ||
                window.ProductGridItems.Visibility != (list ? Visibility.Collapsed : Visibility.Visible) ||
                window.ProductListItems.Visibility != (list ? Visibility.Visible : Visibility.Collapsed) ||
                vm.GridViewVisibility != (list ? "Collapsed" : "Visible") || vm.ListViewVisibility != (list ? "Visible" : "Collapsed"))
                throw new InvalidOperationException("Os controles de grade e lista precisam alternar a visualização preservando o total.");
        }
        try
        {
            window.Width = 1480; window.Height = 940; vm.Page = "PDV";
            vm.Category = "Todos"; Search("");
            if (!Descendants<Image>(window.CartItemsScrollViewer).Any(i => i.DataContext is CartLine l && l.Image is BitmapSource && ReferenceEquals(i.Source, l.Image)))
                throw new InvalidOperationException("A miniatura da foto precisa estar vinculada à imagem do produto no carrinho.");
            window.CategoryFilters.SelectedItem = vm.CategoryOptions.Single(c => c.Name == probe.Category); FlushLayout(window);
            if (vm.Category != probe.Category || vm.Tiles.Count == 0 || vm.Tiles.Any(t => t.Product.Category != probe.Category))
                throw new InvalidOperationException("Selecionar uma categoria precisa filtrar os produtos correspondentes.");
            window.CategoryFilters.SelectedItem = vm.CategoryOptions.Single(c => c.Name == "Todos"); FlushLayout(window);
            if (vm.Category != "Todos") throw new InvalidOperationException("A categoria Todos precisa restaurar o filtro.");
            foreach (bool list in new[] { false, true })
            {
                ShowView(list);
                foreach (string query in new[] { probe.Name, probe.Code, probe.Barcode })
                {
                    Search(query);
                    var items = list ? window.ProductListItems : window.ProductGridItems;
                    if (vm.Search != query || window.ProductSearch.Text != query || vm.Tiles.Count != 1 || vm.Tiles[0].Product.Id != probe.Id ||
                        items.Items.Count != 1 || items.Items[0] is not ProductTile tile || tile.Product.Id != probe.Id ||
                        window.ProductEmptyState.Visibility != Visibility.Collapsed)
                        throw new InvalidOperationException($"A pesquisa nativa por {query} precisa exibir o produto correto em {(list ? "lista" : "grade")}.");
                    AssertSearchReadable(window.ProductSearch);
                    AssertProductBounds(window, items);
                    if (!list && query == probe.Name) Capture(window, Path.Combine(output, "26-grade-busca.png"));
                    var quantities = vm.Cart.ToDictionary(l => l.Product.Id, l => l.Quantity);
                    var enter = KeyEvent(window, Key.Enter, Keyboard.KeyDownEvent);
                    window.ProductSearch.RaiseEvent(enter); FlushLayout(window);
                    if (!enter.Handled || vm.Search != "" || window.ProductSearch.Text != "" ||
                        vm.Cart.Single(l => l.Product.Id == probe.Id).Quantity != quantities.GetValueOrDefault(probe.Id) + 1 ||
                        vm.Cart.Any(l => l.Product.Id != probe.Id && (!quantities.TryGetValue(l.Product.Id, out decimal q) || l.Quantity != q)))
                        throw new InvalidOperationException("Enter precisa adicionar somente o produto pesquisado e limpar a busca.");
                    foreach (var line in vm.Cart.ToArray())
                    {
                        if (quantities.TryGetValue(line.Product.Id, out decimal quantity)) line.Quantity = quantity;
                        else vm.Cart.Remove(line);
                    }
                    vm.Refresh(); vm.Message = originalMessage; FlushLayout(window);
                    if (vm.Total != originalTotal) throw new InvalidOperationException("A pesquisa não pode alterar o total após restaurar as quantidades.");
                }
                if (list) { AssertProductBounds(window, window.ProductListItems); Capture(window, Path.Combine(output, "27-produtos-lista.png")); }
            }
            Search(probe.Barcode);
            FocusManager.SetFocusedElement(FocusManager.GetFocusScope(window.ProductSearch), window.ProductListButton);
            var f2 = KeyEvent(window, Key.F2, Keyboard.PreviewKeyDownEvent);
            window.RaiseEvent(f2); FlushLayout(window);
            if (!f2.Handled || vm.Page != "PDV" || window.ProductSearch.SelectionStart != 0 || window.ProductSearch.SelectionLength != probe.Barcode.Length ||
                (!window.ProductSearch.IsKeyboardFocused && FocusManager.GetFocusedElement(FocusManager.GetFocusScope(window.ProductSearch)) != window.ProductSearch))
                throw new InvalidOperationException("F2 precisa focalizar a busca e selecionar seu texto, inclusive na janela de QA sem ativação.");
            Search("produto-inexistente-" + Guid.NewGuid().ToString("N"));
            if (vm.Tiles.Count != 0 || window.ProductListItems.Items.Count != 0 || window.ProductGridItems.Items.Count != 0 || window.ProductEmptyState.Visibility != Visibility.Visible)
                throw new InvalidOperationException("Uma pesquisa sem correspondência precisa mostrar o estado sem resultados.");
            Capture(window, Path.Combine(output, "28-busca-sem-resultados.png"));
            window.Width = 1200; window.Height = 760; Search("");
            foreach (bool list in new[] { false, true })
            {
                ShowView(list);
                AssertProductBounds(window, list ? window.ProductListItems : window.ProductGridItems);
            }
            Search(probe.Name); AssertSearchReadable(window.ProductSearch);
            var searchPosition = window.SearchFrame.TranslatePoint(new Point(), (FrameworkElement)window.Content);
            if (searchPosition.X < 0 || searchPosition.Y < 0 || searchPosition.X + window.SearchFrame.ActualWidth > ((FrameworkElement)window.Content).ActualWidth + 0.5)
                throw new InvalidOperationException("A pesquisa precisa permanecer dentro da janela de 1200 x 760.");
            Capture(window, Path.Combine(output, "29-busca-tela-menor.png"));
            Search("");
            var order = vm.Snapshot.Orders.First(o => o.IsOpen && o.Lines.Count > 0);
            vm.LoadOrder(order); FlushLayout(window);
            var reservedQuantities = vm.Cart.ToDictionary(l => l.Product.Id, l => l.Quantity);
            foreach (bool list in new[] { false, true })
            {
                ShowView(list);
                var items = list ? window.ProductListItems : window.ProductGridItems;
                if (vm.CanEditCart || vm.ActiveOrderId != order.Id || vm.Total != order.Total || vm.AddProductCommand.CanExecute(vm.Tiles.First()) ||
                    vm.Cart.Any(l => vm.IncrementCommand.CanExecute(l) || vm.DecrementCommand.CanExecute(l) || vm.RemoveCommand.CanExecute(l) || l.Quantity != reservedQuantities[l.Product.Id]) ||
                    Descendants<Button>(items).Any(b => b.DataContext is ProductTile && b.IsEnabled) ||
                    Descendants<Button>(window.CartItemsScrollViewer).Any(b => b.Command is not null && b.IsEnabled))
                    throw new InvalidOperationException("Um pedido reservado precisa preservar quantidades, preços e total, com os comandos de edição desativados nas duas visualizações.");
            }
        }
        finally
        {
            vm.ClearCart();
            foreach (var saved in originalCart) { saved.Line.Quantity = saved.Quantity; vm.Cart.Add(saved.Line); }
            vm.Discount = originalDiscount; vm.DeliveryFee = originalDelivery; vm.Refresh();
            vm.CartCustomer = vm.CustomerOptions.FirstOrDefault(c => c.Id == originalCustomer);
            vm.Category = originalCategory; vm.Search = originalSearch; vm.IsProductsList = originalView; vm.Page = originalPage; vm.Message = originalMessage;
            window.Width = originalWidth; window.Height = originalHeight; FlushLayout(window);
        }
    }
    private static void CheckCartAmounts(MainWindow window)
    {
        var vm = window.Vm;
        decimal originalDiscount = vm.Discount, originalDelivery = vm.DeliveryFee, originalTotal = vm.Total;
        string originalPage = vm.Page;
        var discountProvider = (IValueProvider)new TextBoxAutomationPeer(window.CartDiscountInput).GetPattern(PatternInterface.Value);
        var deliveryProvider = (IValueProvider)new TextBoxAutomationPeer(window.CartDeliveryInput).GetPattern(PatternInterface.Value);
        try
        {
            vm.Page = "PDV"; FlushLayout(window);
            discountProvider.SetValue("3,50"); deliveryProvider.SetValue("4,00"); FlushLayout(window);
            if (vm.Discount != originalDiscount || vm.DeliveryFee != originalDelivery || vm.Total != originalTotal)
                throw new InvalidOperationException("Desconto e entrega devem permanecer pendentes enquanto o usuário não confirma os campos ou perde o foco.");
            window.CommitCartAmounts(); FlushLayout(window);
            if (vm.Discount != 3.5m || vm.DeliveryFee != 4m || vm.Total != vm.Subtotal - 3.5m + 4m)
                throw new InvalidOperationException("Confirmar os campos precisa aplicar desconto e entrega digitados, inclusive antes da perda de foco.");
            foreach (var (input, provider, validValue) in new[]
            {
                (window.CartDiscountInput, discountProvider, "3,50"),
                (window.CartDeliveryInput, deliveryProvider, "4,00")
            })
            {
                provider.SetValue("dado inválido");
                bool rejected = false;
                try { window.CommitCartAmounts(); }
                catch (InvalidOperationException) { rejected = true; }
                if (!rejected || !Validation.GetHasError(input))
                    throw new InvalidOperationException("Texto inválido em desconto ou entrega precisa bloquear a confirmação da venda.");
                provider.SetValue(validValue); window.CommitCartAmounts(); FlushLayout(window);
                if (Validation.GetHasError(input) || vm.Discount != 3.5m || vm.DeliveryFee != 4m || vm.Total != vm.Subtotal - 3.5m + 4m)
                    throw new InvalidOperationException("Corrigir o valor deve remover o erro de validação e preservar o total correto.");
            }
        }
        finally
        {
            vm.Discount = originalDiscount; vm.DeliveryFee = originalDelivery;
            window.CartDiscountInput.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            window.CartDeliveryInput.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            vm.Page = originalPage; FlushLayout(window);
        }
    }
    private static void CheckCashStatus(string output)
    {
        // A separate store keeps cash opening/closing checks out of the main fixture's history.
        var cashService = new StoreService(new JsonStore(Path.Combine(output, "dados-caixa-teste-" + Guid.NewGuid().ToString("N"))));
        cashService.Login("ADMIN", "ADMIN"); cashService.OpenCash(250);
        var cashWindow = new MainWindow(cashService, false) { Width = 1200, Height = 760 };
        try
        {
            Capture(cashWindow, Path.Combine(output, "31-caixa-aberto.png"));
            AssertCashStatus(cashWindow, true);
            OpenCashMenu(cashWindow, true, true);
            CaptureElement(cashWindow.CashStatusMenu, Path.Combine(output, "33-menu-caixa-aberto.png"));
            EscapeCashMenu(cashWindow);
            OpenCashMenu(cashWindow, true, true);
            cashWindow.CashStatusMenu.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownOutsideCapturedElementEvent });
            FlushLayout(cashWindow);
            if (cashWindow.CashStatusMenu.IsOpen) throw new InvalidOperationException("Um clique fora do menu precisa fechar as opções do caixa.");
            decimal expected = cashWindow.Vm.CashSummary?.Expected ?? throw new InvalidOperationException("O caixa de teste precisa estar aberto.");
            cashService.CloseCash(expected, "Conferência automática da aparência do caixa.");
            cashWindow.Vm.Refresh(); FlushLayout(cashWindow); AssertCashStatus(cashWindow, false);
            Capture(cashWindow, Path.Combine(output, "30-caixa-fechado.png"));
            OpenCashMenu(cashWindow, false, true);
            CaptureElement(cashWindow.CashStatusMenu, Path.Combine(output, "32-menu-caixa-fechado.png"));
            cashService.OpenCash(250); cashWindow.Vm.Refresh(); FlushLayout(cashWindow); AssertCashStatus(cashWindow, true);
            AssertCashMenu(cashWindow, true, true);
            InvokeCashHistory(cashWindow);
            cashWindow.Vm.Page = "PDV";
            Capture(cashWindow, Path.Combine(output, "31-caixa-aberto.png"));
            var settings = cashWindow.CreateCashSettingsDialog();
            try
            {
                settings.Owner = null; settings.SizeToContent = SizeToContent.Manual; settings.Height = Math.Min(530, settings.MaxHeight);
                Capture(settings, Path.Combine(output, "34-configuracoes-caixa.png"));
                if (settings.Decimal("opening") != cashWindow.Vm.Snapshot.Settings.CashOpeningDefault ||
                    settings.Checked("autoprint") != cashWindow.Vm.Snapshot.Settings.AutoPrint)
                    throw new InvalidOperationException("As configurações do caixa precisam carregar o fundo de troco e a impressão automática atuais.");
                var openingInput = Descendants<TextBox>((FrameworkElement)settings.Content).Single();
                ((IValueProvider)new TextBoxAutomationPeer(openingInput).GetPattern(PatternInterface.Value)).SetValue("123,45");
                FlushLayout(settings); AssertSearchReadable(openingInput);
                if (settings.Decimal("opening") != 123.45m) throw new InvalidOperationException("O fundo de troco precisa aceitar valores em reais com vírgula decimal.");
                decimal cashBeforeSettings = cashWindow.Vm.CashSummary?.Expected ?? throw new InvalidOperationException("O caixa precisa estar aberto durante a configuração do fundo sugerido.");
                settings.Submit?.Invoke(settings); cashWindow.Vm.Refresh(); FlushLayout(cashWindow);
                if (cashWindow.Vm.Snapshot.Settings.CashOpeningDefault != 123.45m || cashWindow.Vm.CashSummary?.Expected != cashBeforeSettings)
                    throw new InvalidOperationException("Salvar o fundo de troco sugerido precisa persistir o valor sem movimentar o dinheiro do caixa aberto.");
            }
            finally { settings.Close(); }
            cashService.SaveUser(new() { Name = "Operador de teste", Username = "OPERADOR", Role = UserRole.Operador }, "1234");
            cashService.Login("OPERADOR", "1234");
            var operatorWindow = new MainWindow(cashService, false)
            {
                Width = 1200, Height = 760, WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -10000, Top = -10000, ShowInTaskbar = false, ShowActivated = false
            };
            try
            {
                operatorWindow.Show(); FlushLayout(operatorWindow);
                OpenCashMenu(operatorWindow, true, false); InvokeCashHistory(operatorWindow);
                bool settingsRejected = false;
                try { operatorWindow.CreateCashSettingsDialog().Close(); }
                catch (UnauthorizedAccessException) { settingsRejected = true; }
                if (!settingsRejected) throw new InvalidOperationException("O operador não pode acessar as configurações do caixa, mesmo fora do menu.");
                cashService.CloseCash(operatorWindow.Vm.CashSummary?.Expected ?? throw new InvalidOperationException("O caixa do operador precisa estar aberto."), "Conferência do menu do operador.");
                operatorWindow.Vm.Refresh(); FlushLayout(operatorWindow); AssertCashStatus(operatorWindow, false);
                OpenCashMenu(operatorWindow, false, false); EscapeCashMenu(operatorWindow);
                var openingDialog = operatorWindow.CreateOpenCashDialog();
                try
                {
                    if (openingDialog.Decimal("opening") != 123.45m)
                        throw new InvalidOperationException("A próxima abertura do caixa precisa sugerir o fundo configurado pelo administrador, inclusive para o operador.");
                }
                finally { openingDialog.Close(); }
                cashService.OpenCash(250); operatorWindow.Vm.Refresh(); FlushLayout(operatorWindow); AssertCashStatus(operatorWindow, true);
            }
            finally { operatorWindow.CashStatusMenu.IsOpen = false; operatorWindow.Vm.ClearCart(); operatorWindow.Close(); }
        }
        finally { cashWindow.CashStatusMenu.IsOpen = false; cashWindow.Vm.ClearCart(); cashWindow.Close(); }
    }
    private static void OpenCashMenu(MainWindow window, bool open, bool admin)
    {
        ((IInvokeProvider)new ButtonAutomationPeer(window.CashStatusButton).GetPattern(PatternInterface.Invoke)).Invoke();
        FlushLayout(window); window.CashStatusMenu.UpdateLayout();
        AssertCashMenu(window, open, admin);
    }
    private static void AssertCashMenu(MainWindow window, bool open, bool admin)
    {
        string expectedAction = open ? "Fechar caixa" : "Abrir caixa";
        var menu = window.CashStatusMenu;
        // ContextMenu inherits Popup's StaysOpen=true metadata; test dismissal behavior instead.
        if (!menu.IsOpen || !ReferenceEquals(menu.DataContext, window.Vm) || window.Vm.CashActionLabel != expectedAction ||
            window.CashPrimaryAction.Header?.ToString() != expectedAction || !window.CashPrimaryAction.IsEnabled || !window.CashHistoryItem.IsEnabled ||
            window.CashSettingsItem.IsEnabled != admin || window.Vm.IsAdmin != admin || menu.ActualWidth <= 0 || menu.ActualHeight <= 0)
            throw new InvalidOperationException($"O menu do caixa precisa mostrar a ação do estado atual, permitir o histórico e limitar as configurações ao administrador. IsOpen={menu.IsOpen}; StaysOpen={menu.StaysOpen}; DataContextSame={ReferenceEquals(menu.DataContext, window.Vm)}; ActionLabel='{window.Vm.CashActionLabel}'; Header='{window.CashPrimaryAction.Header}'; ExpectedAction='{expectedAction}'; PrimaryEnabled={window.CashPrimaryAction.IsEnabled}; HistoryEnabled={window.CashHistoryItem.IsEnabled}; SettingsEnabled={window.CashSettingsItem.IsEnabled}; IsAdmin={window.Vm.IsAdmin}; ExpectedAdmin={admin}; ActualWidth={menu.ActualWidth:0.###}; ActualHeight={menu.ActualHeight:0.###}.");
        foreach (var item in new[] { window.CashPrimaryAction, window.CashHistoryItem, window.CashSettingsItem })
        {
            var position = item.TranslatePoint(new Point(), menu);
            if (item.ActualWidth <= 0 || item.ActualHeight <= 0 || position.X < -0.5 || position.Y < -0.5 ||
                position.X + item.ActualWidth > menu.ActualWidth + 0.5 || position.Y + item.ActualHeight > menu.ActualHeight + 0.5)
                throw new InvalidOperationException($"As ações do menu do caixa precisam permanecer visíveis sem recorte. Item='{item.Header}'; X={position.X:0.###}; Y={position.Y:0.###}; Width={item.ActualWidth:0.###}; Height={item.ActualHeight:0.###}; MenuWidth={menu.ActualWidth:0.###}; MenuHeight={menu.ActualHeight:0.###}.");
        }
    }
    private static void EscapeCashMenu(MainWindow window)
    {
        var menu = window.CashStatusMenu;
        var escape = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(menu) ?? throw new InvalidOperationException("O menu do caixa precisa estar renderizado."), Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.KeyDownEvent };
        menu.RaiseEvent(escape); FlushLayout(window);
        if (!escape.Handled || menu.IsOpen) throw new InvalidOperationException("Esc precisa fechar o menu do caixa.");
    }
    private static void InvokeCashHistory(MainWindow window)
    {
        ((IInvokeProvider)new MenuItemAutomationPeer(window.CashHistoryItem).GetPattern(PatternInterface.Invoke)).Invoke();
        FlushLayout(window);
        if (window.CashStatusMenu.IsOpen || window.Vm.Page != "Caixa" || !window.CashHistoryGrid.IsVisible ||
            window.CashHistoryGrid.Items.Count != window.Vm.CashSessions.Count || window.CashHistoryGrid.Items.Count == 0)
            throw new InvalidOperationException("Ver histórico precisa fechar o menu e mostrar os caixas registrados, inclusive para operadores.");
    }
    private static void CaptureElement(FrameworkElement element, string path)
    {
        element.UpdateLayout(); element.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        int width = (int)Math.Ceiling(element.ActualWidth), height = (int)Math.Ceiling(element.ActualHeight);
        if (width <= 0 || height <= 0) throw new InvalidOperationException("O menu precisa ter tamanho visível antes da captura.");
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(path); encoder.Save(stream);
    }
    private static void AssertCashStatus(MainWindow window, bool open)
    {
        Brush? dotBrush = ((DependencyObject)window.CashStatusDot) switch
        {
            Border border => border.Background,
            System.Windows.Shapes.Shape shape => shape.Fill,
            _ => null
        };
        string background = open ? "#E9F5EB" : "#FFF4F1", ink = open ? "#187B52" : "#B4483E", borderColor = open ? "#D4EAD9" : "#F0D5CF";
        static bool Matches(Brush? brush, string hex) => brush is SolidColorBrush solid && solid.Color == (Color)ColorConverter.ConvertFromString(hex);
        if (window.Vm.CashIsOpen != open || window.CashStatusLabel.Text != (open ? "Caixa aberto" : "Caixa fechado") ||
            !Matches(window.CashStatusBadge.Background, background) || !Matches(window.CashStatusBadge.BorderBrush, borderColor) ||
            !Matches(window.CashStatusLabel.Foreground, ink) || !Matches(dotBrush, ink))
            throw new InvalidOperationException("O indicador de caixa precisa ficar verde ao abrir e vermelho ao fechar, incluindo fundo, borda, texto e ponto.");
    }
    private static KeyEventArgs KeyEvent(Window window, Key key, RoutedEvent routedEvent)
        => new(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window) ?? throw new InvalidOperationException("A janela de QA precisa estar renderizada."), Environment.TickCount, key) { RoutedEvent = routedEvent };
    private static void FlushLayout(Window window)
    {
        window.UpdateLayout(); window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); window.UpdateLayout();
    }
    private static void AssertSearchReadable(TextBox search)
    {
        search.ApplyTemplate();
        if (search.Template.FindName("PART_ContentHost", search) is not ScrollViewer host || search.ActualWidth < 100 ||
            host.ViewportHeight + 0.5 < search.FontSize * search.FontFamily.LineSpacing || host.ViewportWidth <= 0 ||
            search.Foreground is not SolidColorBrush ink || ink.Color.A == 0)
            throw new InvalidOperationException("O campo de pesquisa precisa reservar espaço para uma linha de texto legível.");
        foreach (int index in new[] { 0, search.Text.Length - 1 })
        {
            var character = search.GetRectFromCharacterIndex(index, true);
            if (character.IsEmpty || character.Height <= 0 || character.Top < -0.5 || character.Bottom > search.ActualHeight + 0.5 ||
                character.Left < -0.5 || character.Right > search.ActualWidth + 0.5)
                throw new InvalidOperationException("O texto e o cursor da pesquisa não podem ficar recortados.");
        }
    }
    private static void AssertProductBounds(MainWindow window, ItemsControl items)
    {
        var buttons = Descendants<Button>(items).Where(b => b.DataContext is ProductTile).ToArray();
        if (items.Items.Count != window.Vm.Tiles.Count || buttons.Length != items.Items.Count || buttons.Length == 0 ||
            buttons.Length > window.Vm.Snapshot.Products.Count(p => p.Active) || window.ProductViewport.ViewportWidth <= 0 ||
            !double.IsFinite(window.Vm.ProductCardWidth) || window.Vm.ProductCardWidth <= 0 || window.Vm.ProductCardWidth > window.ProductViewport.ViewportWidth + 0.5)
            throw new InvalidOperationException("Os produtos renderizados precisam corresponder ao filtro e caber na área disponível.");
        foreach (var button in buttons)
        {
            var position = button.TranslatePoint(new Point(), window.ProductViewport);
            if (button.ActualWidth <= 0 || position.X < -0.5 || position.X + button.ActualWidth > window.ProductViewport.ViewportWidth + 0.5)
                throw new InvalidOperationException("Os cartões e as linhas dos produtos não podem sofrer recorte horizontal.");
        }
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
    private static void Capture(Window window, string path)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = -10000; window.Top = -10000; window.ShowInTaskbar = false; window.ShowActivated = false;
        if (!window.IsVisible) window.Show();
        window.UpdateLayout(); window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        var content = (FrameworkElement)window.Content;
        var backdrop = new DrawingVisual(); using (var context = backdrop.RenderOpen()) context.DrawRectangle(window.Background, null, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
        var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(backdrop); bitmap.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(path); encoder.Save(stream);
    }
}
