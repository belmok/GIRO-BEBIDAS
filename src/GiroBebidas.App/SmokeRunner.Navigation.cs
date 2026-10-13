using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public static partial class SmokeRunner
{
    private static void CheckNavigationUi(string output)
    {
        var service = new StoreService(new JsonStore(Path.Combine(output, "dados-navegacao-" + Guid.NewGuid().ToString("N"))));
        service.Login("ADMIN", "ADMIN");
        service.SaveUser(new() { Name = "Operador do balcão", Username = "NAVEGACAO", Role = UserRole.Operador }, "1234");
        string[] adminKeys = ["PDV", "Vendas", "Pedidos", "Estoque", "Clientes", "Caixa", "Fiscal", "Relatórios", "Configurações", "Usuários", "Equipamentos", "Atualizações"];
        string[] operatorKeys = ["PDV", "Vendas", "Pedidos", "Clientes", "Caixa"];
        string[] adminGroups = ["VENDAS E OPERAÇÃO", "FISCAL", "GESTÃO", "CONFIGURAÇÕES"];
        foreach (bool admin in new[] { true, false })
        {
            service.Login(admin ? "ADMIN" : "NAVEGACAO", admin ? "ADMIN" : "1234");
            var window = new MainWindow(service, false) { Width = 1480, Height = 940 };
            window.EquipmentInventoryReader = () => new EquipmentInventory([], []);
            try
            {
                var vm = window.Vm;
                window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = -10000; window.Top = -10000;
                window.ShowActivated = false; window.ShowInTaskbar = false; window.Show(); FlushLayout(window);
                window.Width = 1480; window.Height = 940; FlushLayout(window);
                var account = window.FindName("SidebarAccountPanel") as FrameworkElement
                    ?? throw new InvalidOperationException("O usuário precisa de um painel fixo no rodapé lateral.");
                var root = (FrameworkElement)window.Content;
                string State() => JsonSerializer.Serialize(service.Snapshot, JsonStore.JsonOptions);
                string before = State();
                vm.AddProduct(vm.Snapshot.Products[0], 2); vm.AddProduct(vm.Snapshot.Products[1], 1); vm.Discount = 1; vm.DeliveryFee = 2;
                decimal total = vm.Total; var quantities = vm.Cart.Select(line => (line.Product.Id, line.Quantity)).ToArray();
                void Preserved()
                {
                    if (State() != before || vm.Total != total || !vm.Cart.Select(line => (line.Product.Id, line.Quantity)).SequenceEqual(quantities))
                        throw new InvalidOperationException("Navegar e rolar o menu devem preservar dados, itens e valores da venda em andamento.");
                }
                void AssertText(TextBlock text)
                {
                    if (text.ActualWidth <= 0 || text.ActualHeight <= 0 || text.DesiredSize.Height > text.ActualHeight + .5)
                        throw new InvalidOperationException("Títulos e nomes dos grupos precisam ficar legíveis sem corte vertical.");
                    var formatted = new FormattedText(text.Text, CultureInfo.CurrentUICulture, text.FlowDirection,
                        new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch), text.FontSize, text.Foreground,
                        VisualTreeHelper.GetDpi(text).PixelsPerDip);
                    if (text.TextWrapping == TextWrapping.NoWrap && formatted.WidthIncludingTrailingWhitespace > text.ActualWidth + 1)
                        throw new InvalidOperationException($"O título de navegação {text.Text} precisa caber por inteiro na lateral.");
                }
                ListBoxItem Container(NavItem item) => window.MenuNavigation.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem
                    ?? Descendants<ListBoxItem>(window.MenuNavigation).Single(container => Equals(container.Content, item));
                void AssertSelected(NavItem item)
                {
                    FlushLayout(window); var container = Container(item);
                    if (vm.Page != item.Key || !Equals(window.MenuNavigation.SelectedItem, item) || !container.IsSelected)
                        throw new InvalidOperationException("A seleção do grupo deve ficar sincronizada com a página aberta.");
                    var bounds = container.TransformToAncestor(window.MenuScroll).TransformBounds(new Rect(container.RenderSize));
                    if (!container.IsVisible || bounds.Left < -.5 || bounds.Right > window.MenuScroll.ViewportWidth + .5
                        || bounds.Top < -.5 || bounds.Bottom > window.MenuScroll.ViewportHeight + .5)
                        throw new InvalidOperationException($"Selecionar {item.Title} precisa revelar sua linha inteira na área rolável do menu.");
                    AssertText(Descendants<TextBlock>(container).Single(block => block.Text == item.Title));
                    if (!Descendants<TextBlock>(root).Any(block => block.IsVisible && block.Text == vm.PageTitle))
                        throw new InvalidOperationException("Navegar deve mostrar o título correto da página existente.");
                    Preserved();
                }
                void Select(NavItem item)
                {
                    var peer = new ListBoxItemAutomationPeer(item, new ListBoxAutomationPeer(window.MenuNavigation));
                    ((ISelectionItemProvider)peer.GetPattern(PatternInterface.SelectionItem)).Select();
                    AssertSelected(item);
                }
                void AssertGroups()
                {
                    var expectedKeys = admin ? adminKeys : operatorKeys;
                    var expectedGroups = admin ? adminGroups : adminGroups.Take(1).ToArray();
                    if (!vm.Navigation.Select(item => item.Key).SequenceEqual(expectedKeys)
                        || window.MenuNavigation.Items.Count != expectedKeys.Length || vm.Navigation.Any(item => string.IsNullOrWhiteSpace(item.Group)))
                        throw new InvalidOperationException("O menu agrupado precisa preservar todos os destinos existentes e suas permissões, na ordem definida.");
                    var view = CollectionViewSource.GetDefaultView(window.MenuNavigation.ItemsSource);
                    var groups = view.Groups?.Cast<CollectionViewGroup>().ToArray() ?? [];
                    if (groups.Length != expectedGroups.Length || groups.Any(group => group.ItemCount == 0)
                        || !groups.Select(group => group.Name.ToString()!.ToUpperInvariant()).SequenceEqual(expectedGroups))
                        throw new InvalidOperationException("Somente grupos com destinos permitidos devem aparecer, sem seções vazias no operador.");
                    if (!groups.SelectMany(group => group.Items.Cast<NavItem>()).Select(item => item.Key).SequenceEqual(expectedKeys))
                        throw new InvalidOperationException("O agrupamento deve preservar a ordem e os destinos das páginas.");
                    foreach (var group in groups)
                    {
                        var label = Descendants<TextBlock>(window.MenuNavigation).Single(block => block.DataContext is CollectionViewGroup data
                            && Equals(data.Name, group.Name) && string.Equals(block.Text, group.Name.ToString(), StringComparison.OrdinalIgnoreCase));
                        AssertText(label);
                    }
                    if (!admin && (service.IsAdmin || vm.Navigation.Any(item => !operatorKeys.Contains(item.Key))))
                        throw new InvalidOperationException("O operador deve conservar somente vendas, pedidos, clientes, caixa e frente de caixa.");
                }
                Rect AccountBounds()
                {
                    FlushLayout(window); var bounds = account.TransformToAncestor(root).TransformBounds(new Rect(account.RenderSize));
                    var menuBounds = window.MenuScroll.TransformToAncestor(root).TransformBounds(new Rect(window.MenuScroll.RenderSize));
                    if (!account.IsVisible || bounds.Width <= 0 || bounds.Height <= 0 || bounds.Left < -.5 || bounds.Top < -.5
                        || bounds.Right > root.ActualWidth + .5 || bounds.Bottom > root.ActualHeight + .5 || menuBounds.Bottom > bounds.Top + .5)
                        throw new InvalidOperationException("O painel do usuário deve ficar visível e separado da área que rola.");
                    if (!Descendants<TextBlock>(account).Any(block => block.IsVisible && block.Text == vm.OperatorName)
                        || !Descendants<TextBlock>(account).Any(block => block.IsVisible && block.Text == vm.OperatorRole))
                        throw new InvalidOperationException("Nome e perfil do usuário devem permanecer visíveis no rodapé lateral.");
                    return bounds;
                }
                void AssertFooterVersion()
                {
                    var version = window.FindName("FooterVersionLabel") as TextBlock
                        ?? throw new InvalidOperationException("A versão instalada precisa aparecer no rodapé inferior.");
                    var status = window.FindName("LocalDataIndicator") as FrameworkElement
                        ?? throw new InvalidOperationException("O rodapé precisa preservar o indicador de dados locais.");
                    var shortcuts = window.FindName("FooterShortcuts") as FrameworkElement
                        ?? throw new InvalidOperationException("O rodapé precisa preservar os atalhos de teclado.");
                    var sidebar = window.FindName("Sidebar") as FrameworkElement
                        ?? throw new InvalidOperationException("A lateral deve manter sua identificação.");
                    if (version.Text != vm.VersionLabel || !version.IsVisible
                        || Descendants<TextBlock>(sidebar).Any(block => block.Text == vm.VersionLabel || block.Text == "SEMPRE POR PERTO" || block.Text == "Seu negócio em movimento."))
                        throw new InvalidOperationException("A versão deve ficar no rodapé inferior e o cartão promocional deve ser removido por inteiro da lateral.");
                    foreach (var element in new[] { status, shortcuts, version })
                    {
                        var bounds = element.TransformToAncestor(root).TransformBounds(new Rect(element.RenderSize));
                        if (!element.IsVisible || bounds.Width <= 0 || bounds.Height <= 0 || bounds.Left < -.5 || bounds.Top < -.5
                            || bounds.Right > root.ActualWidth + .5 || bounds.Bottom > root.ActualHeight + .5)
                            throw new InvalidOperationException("Versão, dados locais e atalhos devem permanecer dentro da janela menor.");
                    }
                    var versionBounds = version.TransformToAncestor(root).TransformBounds(new Rect(version.RenderSize));
                    var shortcutsBounds = shortcuts.TransformToAncestor(root).TransformBounds(new Rect(shortcuts.RenderSize));
                    if (versionBounds.Right > shortcutsBounds.Left - 4 || versionBounds.IntersectsWith(shortcutsBounds))
                        throw new InvalidOperationException("A versão e os atalhos de teclado não podem se sobrepor no rodapé.");
                    AssertText(version);
                    foreach (var label in Descendants<TextBlock>(status).Concat(Descendants<TextBlock>(shortcuts))) AssertText(label);
                }
                foreach (var size in new[] { (Width: 1480d, Height: 940d), (Width: 1200d, Height: 760d) })
                {
                    window.Width = size.Width; window.Height = size.Height; FlushLayout(window); AssertGroups(); AssertFooterVersion();
                    window.MenuScroll.ScrollToTop(); FlushLayout(window); var accountTop = AccountBounds();
                    foreach (var item in vm.Navigation) Select(item);
                    foreach (var item in vm.Navigation.AsEnumerable().Reverse())
                    {
                        vm.Page = item.Key; AssertSelected(item);
                    }
                    var last = vm.Navigation.Last(); vm.Page = last.Key; AssertSelected(last);
                    vm.Search = "texto de pesquisa preservado";
                    var f2 = KeyEvent(window, Key.F2, Keyboard.PreviewKeyDownEvent); window.RaiseEvent(f2); FlushLayout(window);
                    var pdv = vm.Navigation.Single(item => item.Key == "PDV"); AssertSelected(pdv);
                    if (!f2.Handled || !window.ProductSearch.IsKeyboardFocusWithin || window.ProductSearch.SelectedText != vm.Search)
                        throw new InvalidOperationException("F2 deve revelar Frente de caixa no menu e focalizar a busca com o texto selecionado.");
                    vm.Search = ""; FlushLayout(window); Preserved();
                    window.MenuScroll.ScrollToEnd(); FlushLayout(window); var accountBottom = AccountBounds();
                    if (Math.Abs(accountBottom.Top - accountTop.Top) > .5 || Math.Abs(accountBottom.Left - accountTop.Left) > .5)
                        throw new InvalidOperationException("Rolar o menu não pode deslocar o usuário fixo no rodapé.");

                    var viewer = window.MenuScroll;
                    if (admin && size.Width == 1200 && viewer.ScrollableHeight < 25)
                        throw new InvalidOperationException("A janela menor precisa manter todos os grupos acessíveis por rolagem.");
                    double step = SystemParameters.WheelScrollLines == 0 ? 0 : 22;
                    FrameworkElement WheelSource() => Descendants<TextBlock>(window.MenuNavigation).First(block => block.DataContext is NavItem item && block.Text == item.Title && block.IsVisible
                        && block.TransformToAncestor(viewer).TransformBounds(new Rect(block.RenderSize)) is var bounds
                        && bounds.Top >= 0 && bounds.Bottom <= viewer.ViewportHeight);
                    void Wheel(int delta, bool flush = true)
                    {
                        var args = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta) { RoutedEvent = Mouse.PreviewMouseWheelEvent };
                        WheelSource().RaiseEvent(args); if (flush) FlushLayout(window);
                        if (!args.Handled) throw new InvalidOperationException("O menu deve consumir os gestos do touchpad sobre seus próprios títulos.");
                    }
                    void Offset(double expected)
                    {
                        if (Math.Abs(viewer.VerticalOffset - Math.Clamp(expected, 0, viewer.ScrollableHeight)) > .6 || !Equals(window.MenuNavigation.SelectedItem, pdv))
                            throw new InvalidOperationException("Rolagem do menu deve ser proporcional e manter a seleção da página.");
                        AccountBounds(); Preserved();
                    }
                    viewer.ScrollToVerticalOffset(Math.Min(30, viewer.ScrollableHeight / 2)); FlushLayout(window); double start = viewer.VerticalOffset;
                    Wheel(-20); Offset(start + step / 6); Wheel(20); Offset(start);
                    for (int index = 0; index < 6; index++) Wheel(-20, false); FlushLayout(window); Offset(start + step);
                    for (int index = 0; index < 6; index++) Wheel(20, false); FlushLayout(window); Offset(start);
                    viewer.ScrollToTop(); FlushLayout(window); Wheel(120); Offset(0);
                    viewer.ScrollToEnd(); FlushLayout(window); Wheel(-120); Offset(viewer.ScrollableHeight);
                    viewer.ScrollToTop(); FlushLayout(window);
                    if (admin) Capture(window, Path.Combine(output, size.Width == 1480 ? "94-menu-agrupado.png" : "95-menu-agrupado-tela-menor.png"));
                    else if (size.Width == 1200) Capture(window, Path.Combine(output, "96-menu-operador.png"));
                }
            }
            finally { window.Vm.ClearCart(); if (window.IsVisible) window.Close(); }
        }
        File.WriteAllText(Path.Combine(output, "navegacao-smoke-ok.txt"), "Menu agrupado: todos os 12 destinos administrativos e os 5 destinos operacionais acessíveis por seleção nativa e navegação programática. Quatro grupos no administrador e somente Vendas e operação no operador, sem grupos vazios ou destinos adicionais. Títulos e seleção revelados integralmente ao navegar. F2 sincroniza menu e página, focaliza a busca e seleciona o texto. Rodapé com usuário/perfil permanece fixo na janela normal e menor. Cartão promocional removido; versão transferida para o rodapé inferior, sem sobreposição com dados locais ou atalhos. Touchpad sobre títulos conferido com deltas pequenos, gestos consecutivos e limites, preservando seleção, dados e valores da venda.\n");
    }
}
