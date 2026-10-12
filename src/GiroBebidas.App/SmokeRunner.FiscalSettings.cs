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
            Capture(window, Path.Combine(output, "69-configuracoes-fiscal-inicio.png"));
            window.Width = 1480; window.Height = 940; FlushLayout(window);
            var panel = window.FiscalConfigurationPanel;
            string State() => JsonSerializer.Serialize(service.Snapshot, JsonStore.JsonOptions);
            static void Click(Button button) => ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            static void Set(TextBox input, string value) => ((IValueProvider)new TextBoxAutomationPeer(input).GetPattern(PatternInterface.Value)).SetValue(value);
            bool ancestorFound = false;
            for (DependencyObject? parent = panel; parent is not null; parent = VisualTreeHelper.GetParent(parent))
                if (ReferenceEquals(parent, window.SettingsScroll)) { ancestorFound = true; break; }
            if (!ancestorFound || !panel.IsVisible || vm.FiscalChecks.Count == 0)
                throw new InvalidOperationException("A configuração fiscal e sua conferência devem integrar a mesma tela de Configurações.");

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
            panel.FiscalCheckButton.BringIntoView(); FlushLayout(window); Click(panel.FiscalCheckButton); FlushLayout(window);
            AssertChecks();
            if (State() != before || window.SettingsNameInput.Text != unsavedName || vm.Snapshot.Settings.Name != unsavedName)
                throw new InvalidOperationException("Conferir os campos fiscais não pode gravar dados, consumir numeração ou apagar configurações ainda em edição.");
            Capture(window, Path.Combine(output, "70-configuracoes-conferencia-fiscal.png"));
            window.Width = 1200; window.Height = 760; FlushLayout(window);
            panel.ChecksCard.BringIntoView(); FlushLayout(window); AssertChecks();
            if (window.SettingsScroll.ExtentWidth > window.SettingsScroll.ViewportWidth + .5 || !ControlledScrolling.GetIsEnabled(window.SettingsScroll))
                throw new InvalidOperationException("A seção fiscal precisa caber na largura menor e usar a rolagem controlada da página.");
            Capture(window, Path.Combine(output, "71-configuracoes-fiscal-tela-menor.png"));
            vm.Page = "PDV"; FlushLayout(window); vm.Page = "Fiscal"; FlushLayout(window);
            var panelPosition = panel.TranslatePoint(new Point(), window.SettingsScroll);
            if (!panel.IsVisible || panelPosition.Y > window.SettingsScroll.ViewportHeight || panelPosition.Y + panel.ActualHeight < 0)
                throw new InvalidOperationException("O atalho Fiscal deve abrir a seção na mesma área de Configurações.");
            vm.Page = "Configurações"; FlushLayout(window);
            if (window.SettingsScroll.VerticalOffset > .5 || !window.SettingsSaveButton.IsVisible)
                throw new InvalidOperationException("O atalho Configurações deve retornar aos dados do depósito no início da página.");

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
                    bool accepted = RunDialog(issuer ? panel.CreateIssuerDialog() : panel.CreateOptionsDialog(), dialog =>
                    {
                        Set(dialog.Field<TextBox>(issuer ? "legal" : "terminal"), "Alteração não confirmada");
                        if (cancelChoice == 0) Click(dialog.CancelButton);
                        else if (cancelChoice == 1) Click(dialog.CloseButton);
                        else dialog.RaiseEvent(KeyEvent(dialog, Key.Escape, Keyboard.PreviewKeyDownEvent));
                    });
                    if (accepted || State() != before) throw new InvalidOperationException("Cancelar, X ou Esc em um formulário fiscal deve preservar o cadastro e a numeração.");
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
            File.WriteAllText(Path.Combine(output, "fiscal-configuracoes-smoke-ok.txt"), "Fiscal integrado à mesma página de Configurações, com atalhos preservados e retorno ao topo. Conferência nativa corresponde aos campos salvos, sem gravar dados ou simular autorização SEFAZ. Status e descrições cabem na largura menor; rolagem controlada. Cancelar, X e Esc preservam cadastro e numeração. Cadastro parcial salvo com pendências. Numeração inválida recusada com erro visível. Homologação e produção editadas separadamente, preservando o outro perfil.\n");
        }
        finally { window.Vm.ClearCart(); if (window.IsVisible) window.Close(); }
    }
}
