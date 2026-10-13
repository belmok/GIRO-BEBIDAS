using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public static partial class SmokeRunner
{
    private static void CheckEquipmentUi(string output)
    {
        var service = new StoreService(new JsonStore(Path.Combine(output, "dados-equipamentos-" + Guid.NewGuid().ToString("N"))));
        service.Login("ADMIN", "ADMIN");
        var inventory = new EquipmentInventory(["Fila de teste 1", "Fila de teste 2"], ["COM3"]);
        var window = new MainWindow(service, false) { Width = 1480, Height = 940 };
        ContextMenu? openedMenu = null;
        try
        {
            int inventoryReads = 0, windowsOpens = 0;
            window.EquipmentInventoryReader = () => { inventoryReads++; return inventory; };
            window.EquipmentWindowsSettings = () => windowsOpens++;
            window.EquipmentPrinterTest = (_, _) => throw new InvalidOperationException("O teste precisa receber o equipamento escolhido.");
            window.EquipmentScaleRead = _ => throw new InvalidOperationException("A leitura precisa ser injetada no teste.");
            var vm = window.Vm; vm.SetEquipmentInventory(inventory); vm.Page = "Equipamentos";
            Capture(window, Path.Combine(output, "86-equipamentos-vazio.png"));
            window.Width = 1480; window.Height = 940; FlushLayout(window);
            string State() => JsonSerializer.Serialize(service.Snapshot, JsonStore.JsonOptions);
            T Control<T>(string name) where T : FrameworkElement => window.FindName(name) as T
                ?? throw new InvalidOperationException($"O controle de equipamentos {name} precisa existir.");
            var scroll = Control<ScrollViewer>("EquipmentScroll");
            var printers = Control<DataGrid>("PrintersGrid"); var scales = Control<DataGrid>("ScalesGrid");
            static void Click(Button button) => ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            static void Invoke(MenuItem item) => ((IInvokeProvider)new MenuItemAutomationPeer(item).GetPattern(PatternInterface.Invoke)).Invoke();
            static void Prepare(Window dialog)
            {
                dialog.Owner = null; dialog.WindowStartupLocation = WindowStartupLocation.Manual; dialog.Left = -10000; dialog.Top = -10000;
                dialog.ShowActivated = false; dialog.ShowInTaskbar = false;
            }
            void Bounds(Window owner, params FrameworkElement[] elements)
            {
                FlushLayout(owner); var root = (FrameworkElement)owner.Content;
                foreach (var element in elements)
                {
                    var bounds = element.TransformToAncestor(root).TransformBounds(new Rect(element.RenderSize));
                    if (!element.IsVisible || bounds.Width <= 0 || bounds.Height <= 0 || bounds.Left < -.5 || bounds.Top < -.5
                        || bounds.Right > root.ActualWidth + .5 || bounds.Bottom > root.ActualHeight + .5)
                        throw new InvalidOperationException($"O controle {element.Name} precisa ficar acessível sem cortes.");
                }
            }
            void Set(FormDialog dialog, string key, string value)
            {
                var input = dialog.Field<TextBox>(key); input.BringIntoView(); FlushLayout(dialog);
                ((IValueProvider)new TextBoxAutomationPeer(input).GetPattern(PatternInterface.Value)).SetValue(value); FlushLayout(dialog);
            }
            void Choose(FormDialog dialog, string key, object value)
            {
                var input = dialog.Field<ComboBox>(key); input.BringIntoView(); FlushLayout(dialog);
                if (!input.Items.Contains(value)) throw new InvalidOperationException("A escolha do equipamento precisa estar disponível no formulário.");
                input.SelectedItem = value; FlushLayout(dialog);
            }
            void Toggle(FormDialog dialog, bool value)
            {
                var input = dialog.Field<CheckBox>("default"); input.BringIntoView(); FlushLayout(dialog);
                if ((input.IsChecked == true) != value) ((IToggleProvider)new CheckBoxAutomationPeer(input).GetPattern(PatternInterface.Toggle)).Toggle();
                FlushLayout(dialog);
            }
            bool? RunModal(Window dialog, Action action)
            {
                Prepare(dialog); Exception? failure = null;
                var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
                timeout.Tick += (_, _) => { failure = new InvalidOperationException("O formulário de equipamento deve fechar após a escolha."); dialog.Close(); };
                dialog.Loaded += (_, _) => dialog.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
                {
                    try { FlushLayout(dialog); action(); } catch (Exception ex) { failure = ex; dialog.Close(); }
                }));
                try { timeout.Start(); bool? result = dialog.ShowDialog(); if (failure is not null) throw failure; return result; }
                finally { timeout.Stop(); if (dialog.IsVisible) dialog.Close(); }
            }
            void InvokeWithDialog<T>(Action invoke, Action<T> action) where T : Window
            {
                Exception? failure = null; bool handled = false; var started = DateTime.UtcNow;
                var watcher = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
                watcher.Tick += (_, _) =>
                {
                    var dialog = Application.Current.Windows.OfType<T>().FirstOrDefault(candidate => candidate.IsVisible && ReferenceEquals(candidate.Owner, window));
                    if (dialog is not null)
                    {
                        watcher.Stop(); handled = true;
                        try { FlushLayout(dialog); action(dialog); } catch (Exception ex) { failure = ex; dialog.Close(); }
                    }
                    else if ((DateTime.UtcNow - started).TotalSeconds > 6)
                    {
                        watcher.Stop(); failure = new InvalidOperationException("Acionar o menu precisa abrir a janela de equipamento esperada.");
                    }
                };
                try
                {
                    watcher.Start(); invoke(); FlushLayout(window);
                    while (!handled && failure is null) FlushLayout(window);
                    if (failure is not null) throw failure;
                    FlushLayout(window);
                }
                finally { watcher.Stop(); }
            }
            MenuItem[] OpenMenu(bool printer, Guid id)
            {
                if (openedMenu is not null) openedMenu.IsOpen = false;
                var grid = printer ? printers : scales;
                object row = printer ? vm.PrinterRows.Single(p => p.Id == id) : vm.ScaleRows.Single(p => p.Id == id);
                grid.BringIntoView(); grid.ScrollIntoView(row, grid.Columns.Last()); FlushLayout(window);
                var button = Descendants<Button>(grid).Single(button => button.ContextMenu is not null
                    && (printer ? button.DataContext is PrinterDeviceRow p && p.Id == id : button.DataContext is ScaleDeviceRow s && s.Id == id));
                Click(button); FlushLayout(window); openedMenu = button.ContextMenu;
                if (openedMenu is null || !openedMenu.IsOpen) throw new InvalidOperationException("O botão de ações deve abrir o menu do equipamento.");
                var actions = openedMenu.Items.OfType<MenuItem>().ToArray();
                if (actions.Length != 4 || actions.Any(item => !item.IsEnabled)) throw new InvalidOperationException("Administrador deve acessar padrão, teste, edição e remoção no menu.");
                return actions;
            }
            static MenuItem Action(MenuItem[] items, string text) => items.Single(item => item.Header?.ToString()?.Contains(text, StringComparison.OrdinalIgnoreCase) == true);
            string initialState = State();
            if (vm.PrinterRows.Count != 0 || vm.ScaleRows.Count != 0 || vm.PrintersEmpty != "Visible" || vm.ScalesEmpty != "Visible"
                || Control<Button>("EquipmentEditPrinter").IsEnabled || Control<Button>("EquipmentDeletePrinter").IsEnabled
                || Control<Button>("EquipmentEditScale").IsEnabled || Control<Button>("EquipmentDeleteScale").IsEnabled || Control<Button>("EquipmentTestScale").IsEnabled)
                throw new InvalidOperationException("Sem equipamentos, os estados vazios precisam aparecer e as ações de seleção devem ficar desativadas.");
            Click(Control<Button>("EquipmentWindowsPrinters")); FlushLayout(window);
            if (windowsOpens != 1 || State() != initialState) throw new InvalidOperationException("Abrir impressoras do Windows deve acionar somente a navegação injetada e preservar os dados.");

            var printer1 = new PrinterProfile(); var printer2 = new PrinterProfile();
            foreach (var (profile, name, queue, paper, makeDefault) in new[]
            {
                (printer1, "Frente de caixa", "Fila de teste 1", "80 mm", true),
                (printer2, "Cozinha", "Fila de teste 2", "58 mm", false)
            })
            {
                var dialog = window.CreatePrinterDialog(profile); string before = State();
                if (RunModal(dialog, () =>
                {
                    Bounds(dialog, dialog.HeaderLabel, dialog.CloseButton, dialog.CancelButton, dialog.SubmitButton);
                    Set(dialog, "name", ""); Click(dialog.SubmitButton); FlushLayout(dialog);
                    if (!dialog.IsVisible || State() != before) throw new InvalidOperationException("Nome vazio deve recusar o cadastro sem alterar dados.");
                    Set(dialog, "name", name); Choose(dialog, "queue", queue); Choose(dialog, "paper", paper); Toggle(dialog, makeDefault);
                    if (ReferenceEquals(profile, printer1)) Capture(dialog, Path.Combine(output, "90-cadastro-impressora.png"));
                    Click(dialog.SubmitButton);
                }) != true) throw new InvalidOperationException("Salvar impressora pelo formulário deve confirmar o cadastro.");
                vm.Refresh(); FlushLayout(window);
            }
            var scale1 = new ScaleProfile(); var scale2 = new ScaleProfile();
            foreach (var (profile, name, port, baud, protocol, makeDefault) in new[]
            {
                (scale1, "Balança do balcão", "COM3", 9600, "Texto em kg", true),
                (scale2, "Balança de reserva", "COM4", 19200, "Inteiro em gramas", false)
            })
            {
                var dialog = window.CreateScaleDialog(profile); string before = State();
                if (RunModal(dialog, () =>
                {
                    Set(dialog, "name", name); Set(dialog, "port", "COM0"); Click(dialog.SubmitButton); FlushLayout(dialog);
                    if (!dialog.IsVisible || State() != before) throw new InvalidOperationException("Porta inválida deve recusar o cadastro sem alterar os dados.");
                    Set(dialog, "port", port); Choose(dialog, "baud", baud); Choose(dialog, "protocol", protocol); Set(dialog, "command", "05 0D 0A"); Toggle(dialog, makeDefault);
                    Bounds(dialog, dialog.HeaderLabel, dialog.CloseButton, dialog.CancelButton, dialog.SubmitButton);
                    if (ReferenceEquals(profile, scale1)) Capture(dialog, Path.Combine(output, "91-cadastro-balanca.png"));
                    Click(dialog.SubmitButton);
                }) != true) throw new InvalidOperationException("Salvar balança pelo formulário deve confirmar o cadastro.");
                vm.Refresh(); FlushLayout(window);
            }
            if (service.Snapshot.Printers.Count != 2 || service.Snapshot.Scales.Count != 2 || service.Snapshot.Settings.DefaultPrinterId != printer1.Id
                || service.Snapshot.Settings.DefaultScaleId != scale1.Id || service.Snapshot.Scales.Any(p => p.CommandHex != "050D0A"))
                throw new InvalidOperationException("Formulários devem persistir cadastros e padrões sem duplicar registros.");
            if (vm.PrinterRows.Any(row => row.Status != "Disponível no Windows") || vm.ScaleRows.Single(p => p.Id == scale1.Id).Status != "Porta disponível"
                || vm.ScaleRows.Single(p => p.Id == scale2.Id).Status != "Porta não detectada" || vm.ScaleRows.Any(p => p.Status.Contains("Conectada", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Status deve distinguir fila disponível, porta detectada e leitura confirmada, sem simular conexão física.");
            Capture(window, Path.Combine(output, "87-equipamentos-cadastrados.png"));
            window.Width = 1200; window.Height = 760; FlushLayout(window);
            string shortListState = State();
            foreach (var grid in new[] { printers, scales })
            {
                grid.BringIntoView(); FlushLayout(window);
                var inner = ControlledScrolling.GetScrollViewer(grid) ?? throw new InvalidOperationException("A tabela de equipamentos precisa estar renderizada.");
                if (inner.ScrollableHeight > .5) throw new InvalidOperationException("Dois equipamentos devem caber na tabela sem rolagem interna.");
                scroll.ScrollToVerticalOffset(Math.Min(20, scroll.ScrollableHeight)); FlushLayout(window);
                double beforeScroll = scroll.VerticalOffset;
                var source = Descendants<TextBlock>(grid).First(block => block.IsVisible && (block.DataContext is PrinterDeviceRow || block.DataContext is ScaleDeviceRow));
                var wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -20) { RoutedEvent = Mouse.PreviewMouseWheelEvent };
                source.RaiseEvent(wheel); FlushLayout(window);
                double distance = SystemParameters.WheelScrollLines == 0 ? 0 : 22d / 6;
                if (!wheel.Handled || Math.Abs(scroll.VerticalOffset - Math.Clamp(beforeScroll + distance, 0, scroll.ScrollableHeight)) > .6
                    || inner.VerticalOffset > .5 || State() != shortListState)
                    throw new InvalidOperationException("Quando os equipamentos cabem na tabela, o touchpad deve mover a página externa, preservando dados e a tabela sem deslocamento.");
            }
            window.Width = 1480; window.Height = 940; scroll.ScrollToTop(); FlushLayout(window);
            service.SaveFiscalOptions(new() { PrinterId = printer1.Id }, 1, 1); vm.Refresh(); FlushLayout(window);
            var printerActions = OpenMenu(true, printer2.Id); CaptureElement(openedMenu!, Path.Combine(output, "88-menu-impressora.png"));
            vm.SelectedPrinterRow = vm.PrinterRows.Single(p => p.Id == printer1.Id);
            Invoke(Action(printerActions, "padrão")); FlushLayout(window);
            if (service.Snapshot.Settings.DefaultPrinterId != printer2.Id || service.Snapshot.Fiscal.Options.PrinterId != printer1.Id
                || new JsonStore(service.Store.DirectoryPath).Snapshot().Settings.DefaultPrinterId != printer2.Id || vm.SelectedPrinterRow?.Id != printer2.Id)
                throw new InvalidOperationException("Definir padrão no menu deve usar a linha acionada e preservar a impressora fiscal.");
            var scaleActions = OpenMenu(false, scale2.Id); vm.SelectedScaleRow = vm.ScaleRows.Single(p => p.Id == scale1.Id);
            Invoke(Action(scaleActions, "padrão")); FlushLayout(window);
            if (service.Snapshot.Settings.DefaultScaleId != scale2.Id || service.Snapshot.Settings.DefaultPrinterId != printer2.Id || vm.SelectedScaleRow?.Id != scale2.Id)
                throw new InvalidOperationException("Definir balança padrão deve usar a linha acionada e preservar a impressora padrão.");

            printerActions = OpenMenu(true, printer2.Id); vm.SelectedPrinterRow = vm.PrinterRows.Single(p => p.Id == printer1.Id);
            InvokeWithDialog<FormDialog>(() => Invoke(Action(printerActions, "Editar")), dialog =>
            {
                if (dialog.Value("name") != "Cozinha") throw new InvalidOperationException("Editar no menu precisa abrir o cadastro da linha acionada.");
                Set(dialog, "name", "Cozinha revisada"); Choose(dialog, "paper", "A4"); Click(dialog.SubmitButton);
            });
            if (service.Snapshot.Printers.Count != 2 || service.Snapshot.Printers.Single(p => p.Id == printer2.Id).Name != "Cozinha revisada"
                || service.Snapshot.Printers.Single(p => p.Id == printer1.Id).Name != "Frente de caixa")
                throw new InvalidOperationException("Editar pelo menu deve alterar somente a impressora acionada.");
            scaleActions = OpenMenu(false, scale2.Id); vm.SelectedScaleRow = vm.ScaleRows.Single(p => p.Id == scale1.Id);
            InvokeWithDialog<FormDialog>(() => Invoke(Action(scaleActions, "Editar")), dialog =>
            {
                if (dialog.Value("name") != "Balança de reserva") throw new InvalidOperationException("Editar no menu precisa abrir a balança acionada.");
                Set(dialog, "name", "Balança de reserva revisada"); Choose(dialog, "baud", 38400); Click(dialog.SubmitButton);
            });
            if (service.Snapshot.Scales.Count != 2 || service.Snapshot.Scales.Single(p => p.Id == scale2.Id).BaudRate != 38400
                || service.Snapshot.Scales.Single(p => p.Id == scale1.Id).BaudRate != 9600)
                throw new InvalidOperationException("Editar pelo menu deve alterar somente a balança acionada.");

            Guid? printedId = null; window.EquipmentPrinterTest = (_, profile) => printedId = profile.Id;
            string stableState = State(); printerActions = OpenMenu(true, printer2.Id); vm.SelectedPrinterRow = vm.PrinterRows.Single(p => p.Id == printer1.Id);
            Invoke(Action(printerActions, "Testar")); FlushLayout(window);
            if (printedId != printer2.Id || State() != stableState) throw new InvalidOperationException("Testar impressão deve usar a linha acionada sem alterar os registros.");
            var printProfile = service.Snapshot.Printers.Single(p => p.Id == printer2.Id);
            string printText = ReceiptPrinter.PrinterTestText(printProfile);
            if (!printText.Contains(printProfile.Name, StringComparison.Ordinal) || !printText.Contains(printProfile.QueueName, StringComparison.Ordinal)
                || !printText.Contains(printProfile.Paper, StringComparison.Ordinal) || !printText.Contains("não fiscal", StringComparison.Ordinal))
                throw new InvalidOperationException("A página de teste precisa identificar a impressora e o papel escolhidos e seu caráter não fiscal.");
            window.EquipmentPrinterTest = (_, _) => throw new InvalidOperationException("Falha de impressão isolada de teste.");
            printerActions = OpenMenu(true, printer2.Id); Invoke(Action(printerActions, "Testar")); FlushLayout(window);
            if (!vm.Message.Contains("Falha de impressão", StringComparison.Ordinal) || State() != stableState || vm.EquipmentBusy)
                throw new InvalidOperationException("Falha de impressão deve ser informada sem gravar dados nem deixar controles bloqueados.");

            var pendingRead = new TaskCompletionSource<decimal>(); Guid? readId = null;
            window.EquipmentScaleRead = profile => { readId = profile.Id; return pendingRead.Task; };
            scaleActions = OpenMenu(false, scale2.Id); vm.SelectedScaleRow = vm.ScaleRows.Single(p => p.Id == scale1.Id);
            Invoke(Action(scaleActions, "Testar")); FlushLayout(window);
            if (!vm.EquipmentBusy || readId != scale2.Id || Control<Button>("EquipmentTestScale").IsEnabled)
                throw new InvalidOperationException("Testar leitura deve usar a linha acionada e bloquear novos testes enquanto aguarda.");
            // Simulate a second click already queued before the disabled state was painted.
            Action(scaleActions, "Testar").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); FlushLayout(window);
            if (!vm.EquipmentBusy || pendingRead.Task.IsCompleted || State() != stableState)
                throw new InvalidOperationException("Uma segunda ação enquanto a leitura aguarda não pode liberar nem substituir o teste em andamento.");
            pendingRead.SetResult(1.250m); FlushLayout(window);
            if (vm.EquipmentBusy || vm.ScaleRows.Single(p => p.Id == scale2.Id).Status != "Leitura confirmada"
                || !vm.Message.Contains("1,250", StringComparison.Ordinal) || State() != stableState || vm.SelectedScaleRow?.Id != scale2.Id)
                throw new InvalidOperationException("Leitura concluída deve mostrar o peso e recuperar controles sem alterar dados de venda.");
            Capture(window, Path.Combine(output, "93-balanca-leitura-conferida.png"));
            window.EquipmentScaleRead = _ => Task.FromException<decimal>(new InvalidOperationException("Falha de leitura isolada de teste."));
            scaleActions = OpenMenu(false, scale2.Id); Invoke(Action(scaleActions, "Testar")); FlushLayout(window);
            if (vm.EquipmentBusy || vm.ScaleRows.Single(p => p.Id == scale2.Id).Status == "Leitura confirmada"
                || !vm.Message.Contains("Falha de leitura", StringComparison.Ordinal) || State() != stableState)
                throw new InvalidOperationException("Falha após leitura confirmada deve remover o status anterior e recuperar os controles.");
            inventory = new([], []); Click(Control<Button>("EquipmentRefreshButton")); FlushLayout(window);
            if (vm.PrinterRows.Any(p => p.Status != "Não encontrada") || vm.ScaleRows.Any(p => p.Status != "Porta não detectada") || State() != stableState
                || vm.SelectedPrinterRow?.Id != printer2.Id || vm.SelectedScaleRow?.Id != scale2.Id)
                throw new InvalidOperationException("Atualizar disponibilidade deve refletir a ausência de filas e portas sem modificar os cadastros.");
            inventory = new(["Fila de teste 1", "Fila de teste 2"], ["COM3"]); Click(Control<Button>("EquipmentRefreshButton")); FlushLayout(window);
            if (inventoryReads < 2 || vm.PrinterRows.Any(p => p.Status != "Disponível no Windows")) throw new InvalidOperationException("Atualizar deve consultar novamente o inventário injetado.");
            window.EquipmentInventoryReader = () => throw new InvalidOperationException("Inventário indisponível no teste isolado.");
            Click(Control<Button>("EquipmentRefreshButton")); FlushLayout(window);
            if (vm.PrinterRows.Any(p => p.Status != "Não verificada") || vm.ScaleRows.Any(p => p.Status != "Não verificada")
                || !vm.Message.Contains("Inventário indisponível", StringComparison.Ordinal) || State() != stableState)
                throw new InvalidOperationException("Falha ao consultar inventário deve mostrar status não verificado e o erro real, preservando os cadastros.");
            window.EquipmentInventoryReader = () => { inventoryReads++; return inventory; };
            Click(Control<Button>("EquipmentRefreshButton")); FlushLayout(window);

            foreach (bool printer in new[] { true, false })
                for (int choice = 0; choice < 3; choice++)
                {
                    var dialog = printer ? window.CreateDeletePrinterConfirmation(vm.PrinterRows.Single(p => p.Id == printer1.Id))
                        : window.CreateDeleteScaleConfirmation(vm.ScaleRows.Single(p => p.Id == scale1.Id));
                    stableState = State();
                    if (RunModal(dialog, () =>
                    {
                        Bounds(dialog, dialog.TitleLabel, dialog.MessageLabel, dialog.NoticeLabel, dialog.CancelButton, dialog.ConfirmButton, dialog.CloseButton);
                        if (!dialog.CancelButton.IsDefault || dialog.ConfirmButton.IsDefault) throw new InvalidOperationException("Cancelar deve ser a escolha padrão ao remover equipamentos.");
                        if (printer && choice == 0) Capture(dialog, Path.Combine(output, "92-confirmacao-remover-impressora.png"));
                        if (choice == 0) Click(dialog.CancelButton);
                        else if (choice == 1) Click(dialog.CloseButton);
                        else dialog.RaiseEvent(KeyEvent(dialog, Key.Escape, Keyboard.PreviewKeyDownEvent));
                    }) == true || State() != stableState) throw new InvalidOperationException("Cancelar, fechar ou Esc devem preservar os equipamentos e seus vínculos.");
                }
            printerActions = OpenMenu(true, printer1.Id); vm.SelectedPrinterRow = vm.PrinterRows.Single(p => p.Id == printer2.Id);
            InvokeWithDialog<ConfirmationWindow>(() => Invoke(Action(printerActions, "Remover")), dialog => Click(dialog.ConfirmButton));
            if (service.Snapshot.Printers.Any(p => p.Id == printer1.Id) || service.Snapshot.Fiscal.Options.PrinterId is not null
                || service.Snapshot.Settings.DefaultPrinterId != printer2.Id)
                throw new InvalidOperationException("Remover pelo menu deve excluir a impressora acionada, limpar seu vínculo fiscal e preservar o padrão diferente.");
            scaleActions = OpenMenu(false, scale2.Id); vm.SelectedScaleRow = vm.ScaleRows.Single(p => p.Id == scale1.Id);
            InvokeWithDialog<ConfirmationWindow>(() => Invoke(Action(scaleActions, "Remover")), dialog => Click(dialog.ConfirmButton));
            if (service.Snapshot.Scales.Any(p => p.Id == scale2.Id) || service.Snapshot.Settings.DefaultScaleId is not null || service.Snapshot.Scales.Count != 1)
                throw new InvalidOperationException("Remover balança padrão pelo menu deve usar a linha acionada e voltar para pesagem manual.");

            for (int index = 0; index < 12; index++)
            {
                service.SavePrinter(new() { Name = $"Impressora de teste {index:00}", QueueName = index % 2 == 0 ? "Fila de teste 1" : "Fila ausente" }, false);
                service.SaveScale(new() { Name = $"Balança de teste {index:00}", Port = $"COM{index + 10}" }, false);
            }
            vm.Refresh(); window.Width = 1200; window.Height = 760; FlushLayout(window);
            stableState = State();
            foreach (string name in new[] { "EquipmentRefreshButton", "EquipmentWindowsPrinters", "EquipmentEditPrinter", "EquipmentDeletePrinter", "EquipmentAddPrinter",
                "EquipmentTestScale", "EquipmentEditScale", "EquipmentDeleteScale", "EquipmentAddScale" })
            {
                var button = Control<Button>(name); button.BringIntoView(); FlushLayout(window); Bounds(window, button);
                foreach (var label in Descendants<TextBlock>(button))
                    if (label.DesiredSize.Width > label.ActualWidth + .5 || label.DesiredSize.Height > label.ActualHeight + .5)
                        throw new InvalidOperationException("Os textos dos botões de equipamentos não podem ficar cortados na janela menor.");
            }
            if (scroll.ExtentWidth > scroll.ViewportWidth + .5 || !ControlledScrolling.GetIsEnabled(scroll))
                throw new InvalidOperationException("Equipamentos precisa caber na largura menor e usar rolagem controlada.");
            scroll.ScrollToTop(); FlushLayout(window); Capture(window, Path.Combine(output, "89-equipamentos-tela-menor.png"));
            double step = SystemParameters.WheelScrollLines == 0 ? 0 : 22;
            void CheckWheel(FrameworkElement area, Func<FrameworkElement> source, bool inner)
            {
                var viewer = ControlledScrolling.GetScrollViewer(area) ?? throw new InvalidOperationException("A área de equipamentos precisa de rolagem.");
                if (!ControlledScrolling.GetIsEnabled(area) || viewer.ScrollableHeight < 55) throw new InvalidOperationException("A lista de equipamentos deve permitir conferir gestos e limites.");
                double outerBefore = scroll.VerticalOffset;
                void Wheel(int delta, bool flush = true)
                {
                    var args = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta) { RoutedEvent = Mouse.PreviewMouseWheelEvent };
                    source().RaiseEvent(args); if (flush) FlushLayout(window);
                    if (!args.Handled) throw new InvalidOperationException("O gesto do touchpad deve ser tratado na área de equipamentos.");
                }
                void Offset(double expected)
                {
                    if (Math.Abs(viewer.VerticalOffset - Math.Clamp(expected, 0, viewer.ScrollableHeight)) > .6 || State() != stableState
                        || inner && Math.Abs(scroll.VerticalOffset - outerBefore) > .6)
                        throw new InvalidOperationException("Rolagem reduzida deve ser proporcional, preservar dados e mover somente a área acionada.");
                }
                viewer.ScrollToVerticalOffset(30); FlushLayout(window); double start = viewer.VerticalOffset;
                Wheel(-20); Offset(start + step / 6); Wheel(20); Offset(start);
                for (int i = 0; i < 6; i++) Wheel(-20, false); FlushLayout(window); Offset(start + step);
                for (int i = 0; i < 6; i++) Wheel(20, false); FlushLayout(window); Offset(start);
                viewer.ScrollToTop(); FlushLayout(window); Wheel(120); Offset(0);
                viewer.ScrollToBottom(); FlushLayout(window); Wheel(-120); Offset(viewer.ScrollableHeight);
                viewer.ScrollToTop(); FlushLayout(window);
            }
            foreach (var grid in new[] { printers, scales })
            {
                grid.BringIntoView(); FlushLayout(window);
                CheckWheel(grid, () => Descendants<TextBlock>(grid).First(block => block.IsVisible && (block.DataContext is PrinterDeviceRow || block.DataContext is ScaleDeviceRow)), true);
            }
            CheckWheel(scroll, () => scroll, false);
            service.SaveUser(new() { Name = "Operador de equipamentos", Username = "EQUIPAMENTOS", Role = UserRole.Operador }, "1234");
            service.Login("EQUIPAMENTOS", "1234"); vm.Refresh(); vm.Page = "Equipamentos"; FlushLayout(window);
            vm.SelectedPrinterRow = vm.PrinterRows.First(); vm.SelectedScaleRow = vm.ScaleRows.First(); FlushLayout(window);
            if (vm.CanEditPrinter || vm.CanEditScale || vm.CanTestPrinter || vm.CanTestScale || Control<Button>("EquipmentAddPrinter").IsEnabled || Control<Button>("EquipmentAddScale").IsEnabled)
                throw new InvalidOperationException("Operador não pode alterar ou testar equipamentos administrativos.");
            stableState = State();
            foreach (bool printer in new[] { true, false })
            {
                bool rejected = false;
                try { (printer ? window.CreatePrinterDialog(new()) : window.CreateScaleDialog(new())).Close(); }
                catch (UnauthorizedAccessException) { rejected = true; }
                if (!rejected || State() != stableState) throw new InvalidOperationException("Mesmo com solicitação antiga, o operador não pode abrir cadastro administrativo de equipamentos.");
            }
            File.WriteAllText(Path.Combine(output, "equipamentos-smoke-ok.txt"), "Estados vazios e ações desativadas; cadastro e edição nativos de impressoras/balanças com validação de nome e porta. Menus atuam na linha acionada, preservando outras seleções e vínculo fiscal. Padrões persistidos. Inventário, teste de impressão e leitura injetados sem tocar hardware: sucesso, falhas, recuperação de controles e limpeza de leitura anterior. Cancelar/X/Esc preservam cadastros; remoção confirmada limpa apenas vínculos afetados. Janela menor, textos/botões e rolagem de pequenos deltas, gestos consecutivos e limites conferidos na página e grades. Permissões de operador conferidas.\n");
        }
        finally { if (openedMenu is not null) openedMenu.IsOpen = false; window.Vm.ClearCart(); if (window.IsVisible) window.Close(); }
    }
}
