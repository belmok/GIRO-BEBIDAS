using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public static partial class SmokeRunner
{
    private static void CheckFiscalSettingsUi(string output)
    {
        var service = new StoreService(new JsonStore(Path.Combine(output, "dados-fiscal-configuracoes-" + Guid.NewGuid().ToString("N"))));
        service.Login("ADMIN", "ADMIN");
        var window = new MainWindow(service, false) { Width = 1480, Height = 940 };
        try
        {
            var vm = window.Vm;
            vm.Page = "Configurações";
            window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = -10000; window.Top = -10000;
            window.ShowActivated = false; window.ShowInTaskbar = false; window.Show(); FlushLayout(window);
            window.Width = 1480; window.Height = 940; FlushLayout(window);
            var panel = window.FiscalConfigurationPanel;
            string State() => JsonSerializer.Serialize(service.Snapshot, JsonStore.JsonOptions);
            static void Click(Button button) => ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            static void Set(TextBox input, string value) => ((IValueProvider)new TextBoxAutomationPeer(input).GetPattern(PatternInterface.Value)).SetValue(value);
            void Navigate(string page)
            {
                var item = vm.Navigation.Single(item => item.Key == page);
                window.MenuNavigation.ScrollIntoView(item); FlushLayout(window);
                var container = window.MenuNavigation.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem
                    ?? throw new InvalidOperationException("O item do menu precisa estar renderizado para a navegação nativa.");
                ((ISelectionItemProvider)new ListBoxItemAutomationPeer(item, new ListBoxAutomationPeer(window.MenuNavigation)).GetPattern(PatternInterface.SelectionItem)).Select(); FlushLayout(window);
                if (vm.Page != page || !Equals(window.MenuNavigation.SelectedItem, item))
                    throw new InvalidOperationException("Selecionar o menu deve abrir a página correspondente.");
            }
            void AssertPage(bool fiscal)
            {
                if (panel.IsVisible != fiscal || window.FiscalScroll.IsVisible != fiscal || window.SettingsScroll.IsVisible == fiscal ||
                    window.SettingsStoreCard.IsVisible == fiscal || window.SettingsBackupCard.IsVisible == fiscal ||
                    window.SettingsAuditCard.IsVisible == fiscal || window.SettingsSaveButton.IsVisible == fiscal)
                    throw new InvalidOperationException("Configurações e Cupom fiscal devem mostrar somente os cartões da página selecionada, sem sobreposição.");
                if (fiscal && (window.FiscalScroll.VerticalOffset > .5 || vm.PageTitle != "Configuração fiscal"))
                    throw new InvalidOperationException("Cupom fiscal deve abrir no início da sua própria página.");
                if (!fiscal && (window.SettingsScroll.VerticalOffset > .5 || vm.PageTitle != "Configurações"))
                    throw new InvalidOperationException("Configurações deve abrir no início dos dados do depósito.");
            }
            bool ancestorFound = false;
            for (DependencyObject? parent = panel; parent is not null; parent = VisualTreeHelper.GetParent(parent))
                if (ReferenceEquals(parent, window.FiscalScroll)) { ancestorFound = true; break; }
            if (!ancestorFound) throw new InvalidOperationException("Cupom fiscal precisa de sua própria área de rolagem.");
            AssertPage(false);

            void AssertChecks()
            {
                var expected = service.CheckFiscalConfiguration();
                if (!vm.FiscalChecks.SequenceEqual(expected))
                    throw new InvalidOperationException("A conferência exibida deve corresponder aos dados fiscais efetivamente salvos.");
                foreach (var check in expected)
                {
                    var rendered = Descendants<TextBlock>(panel).Where(text => text.IsVisible && text.DataContext is FiscalCheck row && row.Item == check.Item).ToArray();
                    var status = rendered.SingleOrDefault(text => text.Text == check.Status);
                    var description = rendered.SingleOrDefault(text => text.Text == check.Detail);
                    if (status is null || description is null || description.TextWrapping != TextWrapping.Wrap || status.ActualWidth <= 0 || description.ActualWidth <= 0)
                        throw new InvalidOperationException("Cada item fiscal precisa mostrar seu status real e uma descrição completa com quebra de linha.");
                    foreach (var text in new[] { status, description })
                    {
                        var bounds = text.TransformToAncestor(panel).TransformBounds(new Rect(text.RenderSize));
                        if (bounds.Left < -.5 || bounds.Right > panel.ActualWidth + .5 || text.DesiredSize.Height > text.ActualHeight + .5)
                            throw new InvalidOperationException("Os status e as descrições fiscais não podem sofrer recorte horizontal ou vertical.");
                    }
                }
                if (expected.Single(row => row.Item == "Emitente").Complete || !expected.Single(row => row.Item == "Série e numeração").Complete || expected.Single(row => row.Item == "Autorização SEFAZ").Complete)
                    throw new InvalidOperationException("O cadastro parcial continua pendente, a numeração válida fica conferida localmente e a autorização SEFAZ permanece pendente.");
            }

            string before = State();
            const string unsavedName = "Nome ainda em edição";
            Set(window.SettingsNameInput, unsavedName); FlushLayout(window);
            Navigate("Fiscal"); AssertPage(true);
            Capture(window, Path.Combine(output, "69-cupom-fiscal-inicio.png"));
            vm.Message = "";
            panel.FiscalCheckButton.BringIntoView(); FlushLayout(window); Click(panel.FiscalCheckButton); FlushLayout(window);
            AssertChecks();
            int completed = vm.FiscalChecks.Count(check => check.Complete), pending = vm.FiscalChecks.Count - completed;
            if (!vm.Message.Contains($"{completed} item", StringComparison.Ordinal) || !vm.Message.Contains($"{pending} pendência", StringComparison.Ordinal) || !vm.Message.Contains("SEFAZ", StringComparison.Ordinal))
                throw new InvalidOperationException("Atualizar conferência deve informar os totais reais de campos conferidos e pendências.");
            if (State() != before || window.SettingsNameInput.Text != unsavedName || vm.Snapshot.Settings.Name != unsavedName)
                throw new InvalidOperationException("Conferir os campos fiscais não pode gravar dados, consumir numeração ou apagar configurações ainda em edição.");
            Capture(window, Path.Combine(output, "70-cupom-fiscal-conferencia.png"));
            window.Width = 1200; window.Height = 760; FlushLayout(window);
            panel.ChecksCard.BringIntoView(); FlushLayout(window); AssertChecks();
            if (window.FiscalScroll.ExtentWidth > window.FiscalScroll.ViewportWidth + .5 || !ControlledScrolling.GetIsEnabled(window.FiscalScroll))
                throw new InvalidOperationException("A página fiscal precisa caber na largura menor e ter rolagem controlada.");
            Capture(window, Path.Combine(output, "71-cupom-fiscal-tela-menor.png"));
            foreach (var size in new[] { (Width: 1480d, Height: 940d), (Width: 1200d, Height: 760d) })
            {
                window.Width = size.Width; window.Height = size.Height; FlushLayout(window);
                window.FiscalScroll.ScrollToEnd(); FlushLayout(window);
                Navigate("Configurações"); AssertPage(false);
                if (State() != before || window.SettingsNameInput.Text != unsavedName)
                    throw new InvalidOperationException("Alternar as páginas deve preservar configurações gravadas e campos ainda em edição.");
                window.SettingsScroll.ScrollToEnd(); FlushLayout(window);
                Navigate("Fiscal"); AssertPage(true); AssertChecks();
                if (State() != before || vm.Snapshot.Settings.Name != unsavedName)
                    throw new InvalidOperationException("Entrar no fiscal não pode gravar configurações ou consumir numeração.");
            }
            Capture(window, Path.Combine(output, "71b-configuracao-fiscal-inicio-menor.png"));
            var viewer = window.FiscalScroll;
            if (viewer.ScrollableHeight <= 50) throw new InvalidOperationException("A página fiscal menor precisa permitir testar a rolagem.");
            void Wheel(int delta, bool flush = true)
            {
                var source = Descendants<TextBlock>(panel).First(text => text.IsVisible && text.ActualHeight > 0 &&
                    text.TranslatePoint(new Point(), viewer).Y >= 0 && text.TranslatePoint(new Point(), viewer).Y + text.ActualHeight <= viewer.ViewportHeight);
                var args = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta) { RoutedEvent = Mouse.PreviewMouseWheelEvent };
                source.RaiseEvent(args); if (flush) FlushLayout(window);
                if (!args.Handled) throw new InvalidOperationException("O gesto sobre o conteúdo fiscal precisa ser consumido pela área de rolagem.");
            }
            void Offset(double expected)
            {
                if (Math.Abs(viewer.VerticalOffset - Math.Clamp(expected, 0, viewer.ScrollableHeight)) > .6 || viewer.HorizontalOffset > .5 || State() != before)
                    throw new InvalidOperationException("A rolagem fiscal precisa ser proporcional e preservar os registros.");
            }
            double distance = SystemParameters.WheelScrollLines == 0 ? 0 : 22;
            viewer.ScrollToVerticalOffset(30); FlushLayout(window); double start = viewer.VerticalOffset;
            Wheel(-20); Offset(start + distance / 6); Wheel(20); Offset(start);
            for (int i = 0; i < 6; i++) Wheel(-20, false); FlushLayout(window); Offset(start + distance);
            for (int i = 0; i < 6; i++) Wheel(20, false); FlushLayout(window); Offset(start);
            viewer.ScrollToTop(); FlushLayout(window); Wheel(120); Offset(0);
            viewer.ScrollToEnd(); FlushLayout(window); Wheel(-120); Offset(viewer.ScrollableHeight);

            bool RunDialog(FormDialog dialog, Action<FormDialog> action)
            {
                dialog.Owner = null; dialog.WindowStartupLocation = WindowStartupLocation.Manual;
                dialog.Left = -10000; dialog.Top = -10000; dialog.ShowActivated = false; dialog.ShowInTaskbar = false;
                dialog.SizeToContent = SizeToContent.Manual; dialog.Height = Math.Min(650, dialog.MaxHeight);
                Exception? failure = null;
                var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
                timeout.Tick += (_, _) => { failure = new InvalidOperationException("O formulário fiscal precisa concluir ou cancelar a operação."); dialog.Close(); };
                dialog.Loaded += (_, _) => dialog.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
                {
                    try { FlushLayout(dialog); action(dialog); }
                    catch (Exception ex) { failure = ex; dialog.Close(); }
                }));
                try
                {
                    timeout.Start(); bool accepted = dialog.ShowDialog() == true;
                    if (failure is not null) throw failure;
                    return accepted;
                }
                finally { timeout.Stop(); if (dialog.IsVisible) dialog.Close(); }
            }

            before = State();
            foreach (bool issuer in new[] { true, false })
                foreach (int cancelChoice in Enumerable.Range(0, 3))
                {
                    ButtonAction(issuer ? panel.FiscalIssuerButton : panel.FiscalOptionsButton, dialog =>
                    {
                        Set(dialog.Field<TextBox>(issuer ? "legal" : "terminal"), "Alteração não confirmada");
                        if (cancelChoice == 0) Click(dialog.CancelButton);
                        else if (cancelChoice == 1) Click(dialog.CloseButton);
                        else dialog.RaiseEvent(KeyEvent(dialog, Key.Escape, Keyboard.PreviewKeyDownEvent));
                    });
                    if (State() != before || window.SettingsNameInput.Text != unsavedName || vm.Snapshot.Settings.Name != unsavedName)
                        throw new InvalidOperationException("Cancelar, X ou Esc nos botões fiscais deve preservar o cadastro, a numeração e as configurações ainda em edição.");
                }
            bool issuerSaved = RunDialog(panel.CreateIssuerDialog(), dialog =>
            {
                Set(dialog.Field<TextBox>("legal"), "Cadastro fiscal parcial de teste");
                dialog.FormScroll.ScrollToEnd(); FlushLayout(dialog); Click(dialog.SubmitButton);
            });
            vm.Refresh(); FlushLayout(window);
            if (!issuerSaved || service.Snapshot.Fiscal.Issuer.LegalName != "Cadastro fiscal parcial de teste")
                throw new InvalidOperationException("Salvar cadastro fiscal parcial deve persistir a razão social sem marcar o emitente como completo.");
            AssertChecks();

            foreach (var invalid in new[] { (Series: "1000", Next: "1"), (Series: "1", Next: "0"), (Series: "1,5", Next: "1"), (Series: "1", Next: "9999999999") })
            {
                before = State();
                bool accepted = RunDialog(panel.CreateOptionsDialog(), dialog =>
                {
                    Set(dialog.Field<TextBox>("series"), invalid.Series); Set(dialog.Field<TextBox>("next"), invalid.Next);
                    Click(dialog.SubmitButton); FlushLayout(dialog);
                    if (!dialog.IsVisible || State() != before || !Descendants<TextBlock>((FrameworkElement)dialog.Content).Any(text => text.IsVisible && text.Text.Contains("Série e próximo número devem ser inteiros", StringComparison.Ordinal)))
                        throw new InvalidOperationException("Parâmetros fiscais inválidos devem manter o formulário aberto, exibir o erro e preservar os dados.");
                    Click(dialog.CancelButton);
                });
                if (accepted || State() != before) throw new InvalidOperationException("A validação da numeração deve bloquear gravação fora dos limites.");
            }

            var productionBefore = JsonSerializer.Serialize(service.Snapshot.Fiscal.Production, JsonStore.JsonOptions);
            bool optionsSaved = RunDialog(panel.CreateOptionsDialog(), dialog =>
            {
                Set(dialog.Field<TextBox>("series"), "2"); Set(dialog.Field<TextBox>("next"), "15");
                Set(dialog.Field<TextBox>("terminal"), "CAIXA-QA"); Click(dialog.SubmitButton);
            });
            vm.Refresh(); FlushLayout(window);
            if (!optionsSaved || service.Snapshot.Fiscal.Homologation.Series != 2 || service.Snapshot.Fiscal.Homologation.NextNumber != 15 || service.Snapshot.Fiscal.Options.Terminal != "CAIXA-QA" || JsonSerializer.Serialize(service.Snapshot.Fiscal.Production, JsonStore.JsonOptions) != productionBefore)
                throw new InvalidOperationException("Salvar homologação deve preservar o perfil de produção.");
            var homologationBefore = JsonSerializer.Serialize(service.Snapshot.Fiscal.Homologation, JsonStore.JsonOptions);
            optionsSaved = RunDialog(panel.CreateOptionsDialog(), dialog =>
            {
                var environment = dialog.Field<ComboBox>("environment");
                environment.SelectedItem = environment.Items.Cast<FiscalChoice<FiscalEnvironment>>().Single(choice => choice.Value == FiscalEnvironment.Producao); FlushLayout(dialog);
                if (dialog.Field<TextBox>("series").Text != "1" || dialog.Field<TextBox>("next").Text != "1")
                    throw new InvalidOperationException("Trocar o ambiente deve carregar sua numeração separada.");
                Set(dialog.Field<TextBox>("series"), "3"); Set(dialog.Field<TextBox>("next"), "27"); Click(dialog.SubmitButton);
            });
            vm.Refresh(); FlushLayout(window); AssertChecks();
            if (!optionsSaved || service.Snapshot.Fiscal.Options.Environment != FiscalEnvironment.Producao || service.Snapshot.Fiscal.Production.Series != 3 || service.Snapshot.Fiscal.Production.NextNumber != 27 || JsonSerializer.Serialize(service.Snapshot.Fiscal.Homologation, JsonStore.JsonOptions) != homologationBefore)
                throw new InvalidOperationException("Salvar produção deve preservar a numeração de homologação.");
            var pdvPrinter = new PrinterProfile { Name = "Impressora padrão fictícia", QueueName = "Fila fictícia PDV" };
            var fiscalPrinter = new PrinterProfile { Name = "Impressora fiscal fictícia", QueueName = "Fila fictícia fiscal" };
            service.SavePrinter(pdvPrinter, true); service.SavePrinter(fiscalPrinter, false); vm.Refresh(); FlushLayout(window);
            void ButtonAction(Button button, Action<FormDialog> action, bool printerFocus = false)
            {
                Exception? failure = null; bool handled = false; DateTime started = DateTime.UtcNow;
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
                timer.Tick += (_, _) =>
                {
                    var dialog = window.OwnedWindows.OfType<FormDialog>().FirstOrDefault(candidate => candidate.IsVisible);
                    if (dialog is null)
                    {
                        if ((DateTime.UtcNow - started).TotalSeconds > 6) { timer.Stop(); failure = new InvalidOperationException("O botão fiscal precisa abrir seu formulário."); }
                        return;
                    }
                    timer.Stop(); handled = true;
                    try
                    {
                        FlushLayout(dialog);
                        if (printerFocus)
                        {
                            var printer = dialog.Field<ComboBox>("printer");
                            var bounds = printer.TransformToAncestor(dialog.FormScroll).TransformBounds(new Rect(printer.RenderSize));
                            if (!printer.IsKeyboardFocusWithin || bounds.Top < -.5 || bounds.Bottom > dialog.FormScroll.ViewportHeight + .5)
                                throw new InvalidOperationException("Configurar impressora deve colocar o campo de impressora visível e em foco.");
                        }
                        action(dialog);
                    }
                    catch (Exception ex) { failure = ex; dialog.Close(); }
                };
                try
                {
                    button.BringIntoView(); FlushLayout(window); timer.Start(); Click(button); FlushLayout(window);
                    if (failure is not null) throw failure;
                    if (!handled) throw new InvalidOperationException("O botão fiscal deve executar a ação solicitada.");
                }
                finally { timer.Stop(); }
            }
            Navigate("Configurações"); Set(window.SettingsNameInput, unsavedName); FlushLayout(window); Navigate("Fiscal");
            before = State();
            ButtonAction(panel.FiscalPrinterButton, dialog =>
            {
                var printer = dialog.Field<ComboBox>("printer");
                printer.SelectedItem = printer.Items.Cast<FiscalChoice<Guid?>>().Single(choice => choice.Value == fiscalPrinter.Id);
                Capture(dialog, Path.Combine(output, "72-impressora-fiscal-modal.png"));
                Click(dialog.CancelButton);
            }, true);
            if (State() != before || window.SettingsNameInput.Text != unsavedName || vm.Snapshot.Settings.Name != unsavedName)
                throw new InvalidOperationException("Cancelar a escolha da impressora fiscal deve preservar os dados e configurações ainda em edição.");
            ButtonAction(panel.FiscalPrinterButton, dialog =>
            {
                var printer = dialog.Field<ComboBox>("printer");
                printer.SelectedItem = printer.Items.Cast<FiscalChoice<Guid?>>().Single(choice => choice.Value == fiscalPrinter.Id);
                Click(dialog.SubmitButton);
            }, true);
            FlushLayout(window);
            if (service.Snapshot.Fiscal.Options.PrinterId != fiscalPrinter.Id || service.Snapshot.Settings.DefaultPrinterId != pdvPrinter.Id ||
                !Descendants<TextBlock>(panel).Any(text => text.IsVisible && text.Text == vm.FiscalPrinterLabel && text.Text.Contains(fiscalPrinter.Name, StringComparison.Ordinal)))
                throw new InvalidOperationException("O botão deve salvar e mostrar a impressora fiscal, preservando a impressora padrão do PDV.");
            Click(panel.FiscalCheckButton); FlushLayout(window); AssertChecks();
            if (!vm.FiscalChecks.Single(check => check.Item == "Impressora").Complete)
                throw new InvalidOperationException("Uma impressora cadastrada selecionada deve atualizar a conferência local.");
            before = State(); Click(panel.FiscalEquipmentButton); FlushLayout(window);
            if (vm.Page != "Equipamentos" || window.FiscalScroll.IsVisible || window.SettingsScroll.IsVisible || State() != before)
                throw new InvalidOperationException("O botão de equipamentos deve abrir sua página sem alterar os registros.");
            Navigate("Fiscal"); AssertPage(true);
            if (service.Snapshot.Fiscal.Options.PrinterId != fiscalPrinter.Id || service.Snapshot.Fiscal.Production.Series != 3 || service.Snapshot.Fiscal.Production.NextNumber != 27)
                throw new InvalidOperationException("Reabrir Cupom fiscal deve preservar impressora e numeração salvas.");
            before = State(); panel.FiscalProductsButton.BringIntoView(); FlushLayout(window); Click(panel.FiscalProductsButton); FlushLayout(window);
            if (vm.Page != "Estoque" || !window.StockProductsGrid.IsVisible || window.StockProductsGrid.Items.Count != vm.StockRows.Count || vm.StockRows.Count == 0 || State() != before)
                throw new InvalidOperationException("Revisar produtos deve abrir o estoque sem alterar configurações fiscais ou registros.");
            Capture(window, Path.Combine(output, "73-fiscal-revisar-produtos.png"));
            Navigate("Fiscal"); AssertPage(true);

            service.SaveUser(new() { Name = "Operador fiscal de teste", Username = "OPERADOR-FISCAL", Role = UserRole.Operador }, "1234");
            service.Login("OPERADOR-FISCAL", "1234");
            var restricted = new MainWindow(service, false) { Width = 1200, Height = 760 };
            try
            {
                restricted.WindowStartupLocation = WindowStartupLocation.Manual; restricted.Left = -10000; restricted.Top = -10000;
                restricted.ShowActivated = false; restricted.ShowInTaskbar = false; restricted.Show(); FlushLayout(restricted);
                if (restricted.Vm.Navigation.Any(item => item.Key is "Fiscal" or "Configurações"))
                    throw new InvalidOperationException("O menu de operador não deve oferecer acesso fiscal ou configurações administrativas.");
                restricted.Vm.Page = "Fiscal"; FlushLayout(restricted);
                if (restricted.FiscalConfigurationPanel.FiscalCheckButton.IsEnabled || restricted.FiscalConfigurationPanel.FiscalPrinterButton.IsEnabled || restricted.FiscalConfigurationPanel.FiscalEquipmentButton.IsEnabled || restricted.FiscalConfigurationPanel.FiscalProductsButton.IsEnabled)
                    throw new InvalidOperationException("Os controles fiscais devem ficar desativados para operador mesmo se a página for atribuída por código.");
                restricted.Vm.Page = "Configurações"; FlushLayout(restricted);
                if (restricted.SettingsSaveButton.IsEnabled || restricted.SettingsBackupButton.IsEnabled || restricted.SettingsRestoreButton.IsEnabled)
                    throw new InvalidOperationException("Os controles administrativos devem ficar desativados para operador.");
                before = State(); bool refused = false;
                try { service.CheckFiscalConfiguration(); } catch (UnauthorizedAccessException) { refused = true; }
                if (!refused || State() != before) throw new InvalidOperationException("O serviço deve proteger a conferência fiscal sem modificar dados do operador.");
                refused = false;
                try { service.SaveFiscalOptions(service.Snapshot.Fiscal.Options, 4, 50); } catch (UnauthorizedAccessException) { refused = true; }
                if (!refused || State() != before) throw new InvalidOperationException("O serviço deve impedir alterações fiscais pelo operador.");
            }
            finally { restricted.Vm.ClearCart(); if (restricted.IsVisible) restricted.Close(); }
            File.WriteAllText(Path.Combine(output, "fiscal-configuracoes-smoke-ok.txt"), "Configurações e Cupom fiscal separados: navegação nativa nos dois sentidos, sem cartões sobrepostos, nas larguras normal e menor. Edição não salva preservada ao alternar e conferir. Checklist corresponde aos dados sem gravar ou simular autorização SEFAZ. Status e descrições sem corte; touchpad proporcional, gestos consecutivos e limites. Cancelar, X e Esc preservam cadastro e numeração; cadastro parcial salvo e números inválidos recusados com erro visível. Homologação e produção preservadas separadamente. Botão Configurar impressora abre o campo visível e em foco; cancelamento preserva e confirmação salva impressora fiscal fictícia sem modificar a padrão do PDV ou imprimir. Equipamentos abre sua página. Operador sem acesso no menu, controles administrativos desativados e serviço protegido.\n");
        }
        finally { window.Vm.ClearCart(); if (window.IsVisible) window.Close(); }
    }
}
