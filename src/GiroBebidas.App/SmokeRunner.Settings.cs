using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public static partial class SmokeRunner
{
    private static void CheckSettingsUi(string output)
    {
        string data = Path.Combine(output, "dados-configuracoes-" + Guid.NewGuid().ToString("N"));
        var store = new JsonStore(data); var service = new StoreService(store); service.Login("ADMIN", "ADMIN");
        var window = new MainWindow(service, false) { Width = 1480, Height = 940 };
        try
        {
            var vm = window.Vm; vm.Page = "Configurações";
            Capture(window, Path.Combine(output, "66-configuracoes-renovadas.png"));
            window.Width = 1480; window.Height = 940; FlushLayout(window);
            string State() => JsonSerializer.Serialize(service.Snapshot, JsonStore.JsonOptions);
            string before = State(); var settingsBefore = service.Snapshot.Settings;
            void Set(TextBox input, string value) { ((IValueProvider)new TextBoxAutomationPeer(input).GetPattern(PatternInterface.Value)).SetValue(value); FlushLayout(window); }
            void Toggle(CheckBox control) { ((IToggleProvider)new CheckBoxAutomationPeer(control).GetPattern(PatternInterface.Toggle)).Toggle(); FlushLayout(window); }
            void Save() { ((IInvokeProvider)new ButtonAutomationPeer(window.SettingsSaveButton).GetPattern(PatternInterface.Invoke)).Invoke(); FlushLayout(window); }
            Set(window.SettingsNameInput, "Giro Bebidas • Loja de teste"); Set(window.SettingsAddressInput, "Rua das Palmeiras, 175 • Centro");
            Set(window.SettingsPhoneInput, "(11) 99999-0000"); Set(window.SettingsDocumentInput, "Documento fictício");
            Set(window.SettingsFooterInput, "Obrigado pela preferência. Volte sempre!");
            Toggle(window.SettingsAutoPrintToggle); Toggle(window.SettingsDailyBackupToggle);
            if (State() != before) throw new InvalidOperationException("Editar configurações sem salvar não pode alterar os dados gravados.");
            window.SettingsFooterInput.Focus(); Set(window.SettingsFooterInput, "Mensagem final digitada com o campo ainda em foco."); Save();
            var saved = new JsonStore(data).Snapshot().Settings;
            if (saved.Name != "Giro Bebidas • Loja de teste" || saved.Address != "Rua das Palmeiras, 175 • Centro" || saved.Phone != "(11) 99999-0000" || saved.Document != "Documento fictício" ||
                saved.ReceiptFooter != "Mensagem final digitada com o campo ainda em foco." || saved.AutoPrint == settingsBefore.AutoPrint || saved.DailyBackup == settingsBefore.DailyBackup ||
                saved.CashOpeningDefault != settingsBefore.CashOpeningDefault || saved.GitHubUpdates.Repository != settingsBefore.GitHubUpdates.Repository || saved.GitHubUpdates.AutoCheck != settingsBefore.GitHubUpdates.AutoCheck)
                throw new InvalidOperationException("Salvar deve persistir os campos e interruptores, inclusive o último campo em foco, e preservar as demais configurações.");
            before = State(); Set(window.SettingsNameInput, ""); Save();
            if (State() != before) throw new InvalidOperationException("Nome vazio deve recusar o salvamento sem alterar os registros.");
            Set(window.SettingsNameInput, saved.Name); Save();
            window.Width = 1200; window.Height = 760; FlushLayout(window);
            Capture(window, Path.Combine(output, "67-configuracoes-tela-menor.png"));
            window.SettingsScroll.ScrollToEnd(); FlushLayout(window);
            var root = (FrameworkElement)window.Content;
            foreach (var control in new FrameworkElement[] { window.SettingsSaveButton, window.SettingsBackupButton, window.SettingsRestoreButton, window.SettingsDataFolderButton })
            {
                control.BringIntoView(); FlushLayout(window);
                var point = control.TransformToAncestor(root).TransformBounds(new Rect(control.RenderSize));
                if (!control.IsVisible || point.Left < -.5 || point.Right > root.ActualWidth + .5 || point.Top < -.5 || point.Bottom > root.ActualHeight + .5)
                    throw new InvalidOperationException("Salvar e os controles de backup devem permanecer acessíveis na janela menor.");
            }
            Capture(window, Path.Combine(output, "68-configuracoes-backup.png"));
            for (int i = 0; i < 12; i++) service.SaveSettings(service.Snapshot.Settings);
            vm.Refresh(); window.SettingsAuditGrid.BringIntoView(); FlushLayout(window);
            var auditViewer = ControlledScrolling.GetScrollViewer(window.SettingsAuditGrid) ?? throw new InvalidOperationException("A auditoria precisa de uma área de rolagem própria.");
            auditViewer.ScrollToVerticalOffset(20); FlushLayout(window);
            double auditBefore = auditViewer.VerticalOffset, pageBefore = window.SettingsScroll.VerticalOffset;
            var auditSource = Descendants<TextBlock>(window.SettingsAuditGrid).First(control => control.IsVisible && control.DataContext is AuditEntry);
            var wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -20) { RoutedEvent = Mouse.PreviewMouseWheelEvent };
            auditSource.RaiseEvent(wheel); FlushLayout(window);
            double distance = SystemParameters.WheelScrollLines == 0 ? 0 : 22d / 6;
            if (!wheel.Handled || Math.Abs(auditViewer.VerticalOffset - auditBefore - distance) > .5 || Math.Abs(window.SettingsScroll.VerticalOffset - pageBefore) > .5)
                throw new InvalidOperationException("Rolar a auditoria deve mover somente a lista interna com sensibilidade reduzida.");
            if (!window.SettingsRestoreButton.IsEnabled || !ControlledScrolling.GetIsEnabled(window.SettingsScroll)) throw new InvalidOperationException("Configurações precisam usar rolagem reduzida e permitir restauração com caixa fechado e venda vazia.");
            service.OpenCash(50); vm.Refresh(); FlushLayout(window);
            if (window.SettingsRestoreButton.IsEnabled) throw new InvalidOperationException("Restauração deve ficar desativada durante o caixa aberto.");
            string backup = Path.Combine(output, "backup-configuracoes.json"); service.ExportBackup(backup); before = State(); bool rejected = false;
            try { service.RestoreBackup(backup); } catch (InvalidOperationException) { rejected = true; }
            if (!rejected || State() != before) throw new InvalidOperationException("Restauração com caixa aberto deve preservar os dados.");
            service.CloseCash(50, "Teste isolado"); vm.Refresh(); vm.AddProduct(vm.Snapshot.Products[0], 1); FlushLayout(window);
            if (window.SettingsRestoreButton.IsEnabled) throw new InvalidOperationException("Restauração deve ficar desativada com produtos no carrinho.");
            vm.ClearCart(); vm.Refresh(); FlushLayout(window);
            service.ExportBackup(backup);
            var changed = service.Snapshot.Settings; changed.Name = "Nome após backup"; service.SaveSettings(changed);
            service.RestoreBackup(backup);
            if (service.CurrentUser is not null || service.Snapshot.Settings.Name != saved.Name || !Directory.EnumerateFiles(data, "antes-restauracao-*.json").Any())
                throw new InvalidOperationException("Restaurar deve recuperar as configurações, preservar uma cópia anterior e exigir novo login.");
            service.Login("ADMIN", "ADMIN"); service.SaveUser(new() { Name = "Operador de teste", Username = "OPERADOR", Role = UserRole.Operador }, "1234"); service.Login("OPERADOR", "1234");
            before = State(); rejected = false;
            try { service.SaveSettings(service.Snapshot.Settings); } catch (UnauthorizedAccessException) { rejected = true; }
            if (!rejected || State() != before) throw new InvalidOperationException("O operador não pode alterar as configurações do depósito.");
            File.WriteAllText(Path.Combine(output, "configuracoes-smoke-ok.txt"), "Campos e interruptores editados por automação nativa; edição sem salvar preserva dados. Salvar persiste inclusive o campo em foco e preserva opções de caixa e atualizações. Nome vazio recusado. Layout menor e rolagem conferidos. Backup exportado; restauração bloqueada com caixa aberto/carrinho, recuperação de configurações com cópia anterior e novo login. Operador impedido de salvar.\n");
        }
        finally { window.Vm.ClearCart(); if (window.IsVisible) window.Close(); }
    }
}
