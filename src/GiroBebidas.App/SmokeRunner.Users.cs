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
    private static void CheckUsersDashboard(MainWindow main, MainViewModel current, string output)
    {
        var service = new StoreService(new JsonStore(Path.Combine(output, "dados-usuarios-" + Guid.NewGuid().ToString("N"))));
        service.Login("ADMIN", "ADMIN");
        var dayana = new UserAccount { Name = "DAYANA", Username = "DAYANA", Role = UserRole.Operador };
        var inactive = new UserAccount { Name = "João da Silva", Username = "JOAO", Role = UserRole.Operador, Active = false };
        var manager = new UserAccount { Name = "Gestor de teste", Username = "GESTOR", Role = UserRole.Administrador };
        service.SaveUser(dayana, "1111"); service.SaveUser(inactive, "2222"); service.SaveUser(manager, "3333");
        var window = new MainWindow(service, false) { Width = 1480, Height = 940 };
        try
        {
            var vm = window.Vm; vm.Page = "Usuários";
            Capture(window, Path.Combine(output, "110-usuarios-e-acessos.png"));
            window.Width = 1480; window.Height = 940; FlushLayout(window);
            var panel = Descendants<UsersPanel>(window).Single();
            T Find<T>(string name) where T : FrameworkElement => (T)(panel.FindName(name) ?? throw new InvalidOperationException("Controle de usuários ausente: " + name));
            var search = Find<TextBox>("UserSearchInput"); var role = Find<ComboBox>("UserRoleSelector"); var status = Find<ComboBox>("UserStatusSelector");
            var grid = Find<DataGrid>("UsersGrid"); var scroll = Find<ScrollViewer>("UsersScroll");
            string State() => JsonSerializer.Serialize(service.Snapshot, JsonStore.JsonOptions);
            void SetSearch(string text) { ((IValueProvider)new TextBoxAutomationPeer(search).GetPattern(PatternInterface.Value)).SetValue(text); FlushLayout(window); }
            static void Click(Button button) => ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            static void Prepare(Window dialog) { dialog.Owner = null; dialog.WindowStartupLocation = WindowStartupLocation.Manual; dialog.Left = -10000; dialog.Top = -10000; dialog.ShowActivated = false; dialog.ShowInTaskbar = false; }
            void NativeForm(FormDialog dialog, Action<FormDialog> scenario, bool saved)
            {
                Prepare(dialog); Exception? error = null;
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
                timer.Tick += (_, _) => { error = new InvalidOperationException("O formulário de usuário precisa fechar após a escolha."); dialog.Close(); };
                dialog.Loaded += (_, _) => dialog.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
                {
                    try { scenario(dialog); }
                    catch (Exception ex) { error = ex; dialog.Close(); }
                }));
                try { timer.Start(); bool? result = dialog.ShowDialog(); if (error is not null) throw error; if ((result == true) != saved) throw new InvalidOperationException("Salvar e cancelar usuário precisam retornar o resultado correto."); }
                finally { timer.Stop(); if (dialog.IsVisible) dialog.Close(); }
            }
            void SetText(FormDialog dialog, string key, string value) => ((IValueProvider)new TextBoxAutomationPeer(dialog.Field<TextBox>(key)).GetPattern(PatternInterface.Value)).SetValue(value);
            void Password(FormDialog dialog, string value) { dialog.Field<PasswordBox>("password").Password = value; dialog.Field<PasswordBox>("confirm").Password = value; }
            void Reject(FormDialog dialog, string before, string message)
            {
                Click(dialog.SubmitButton); FlushLayout(dialog);
                if (!dialog.IsVisible || State() != before) throw new InvalidOperationException(message);
            }
            if (vm.UserTotalCount != 4 || vm.UserAdminCount != 2 || vm.UserOperatorCount != 2 || vm.UserActiveCount != 3 || grid.Items.Count != 4)
                throw new InvalidOperationException("As quatro métricas precisam refletir os usuários reais, incluindo contas inativas.");
            string before = State(); SetSearch("dayana");
            if (vm.UserRows.Count != 1 || vm.UserRows[0].Id != dayana.Id) throw new InvalidOperationException("A busca precisa localizar o usuário sem diferenciar maiúsculas.");
            AssertSearchReadable(search);
            SetSearch("joao"); if (vm.UserRows.Count != 1 || vm.UserRows[0].Id != inactive.Id) throw new InvalidOperationException("A busca deve localizar nomes com ou sem acentos.");
            SetSearch("operador"); if (vm.UserRows.Count != 2) throw new InvalidOperationException("A busca precisa incluir o perfil.");
            SetSearch(""); role.SelectedItem = "Administrador"; FlushLayout(window); if (vm.UserRows.Count != 2 || vm.UserRows.Any(row => !row.IsAdmin)) throw new InvalidOperationException("O filtro de perfil deve mostrar somente o perfil escolhido.");
            role.SelectedItem = "Todos os perfis"; status.SelectedItem = "Inativo"; FlushLayout(window);
            if (vm.UserRows.Count != 1 || vm.UserRows[0].Active || vm.UserTotalCount != 4) throw new InvalidOperationException("Filtrar inativos não pode alterar as métricas gerais.");
            status.SelectedItem = "Todas as situações"; SetSearch("nenhuma-conta-teste");
            if (grid.Items.Count != 0 || vm.UserEmptyVisibility != "Visible" || vm.SelectedUserRow is not null) throw new InvalidOperationException("Nenhum resultado precisa mostrar o estado vazio e limpar a seleção.");
            Capture(window, Path.Combine(output, "112-usuarios-busca-vazia.png")); SetSearch("");
            if (State() != before) throw new InvalidOperationException("Pesquisar e filtrar usuários não pode modificar dados.");
            vm.SelectedUserRow = vm.UserRows.Single(row => row.Id == dayana.Id);
            service.SaveUser(new() { Id = dayana.Id, Name = "DAYANA FREITAS", Username = dayana.Username, Role = dayana.Role, Active = true }, null); vm.Refresh(); FlushLayout(window);
            if (vm.SelectedUserRow?.Id != dayana.Id || vm.SelectedUserRow.Name != "DAYANA FREITAS" || vm.SelectedUser?.Id != dayana.Id) throw new InvalidOperationException("Atualizar usuários deve manter a seleção por ID e trazer o cadastro novo.");
            var stale = new UserAccount { Id = dayana.Id, Name = "Cadastro antigo", Username = "ANTIGO", Role = UserRole.Administrador, Active = false };
            var freshEditor = window.CreateUserEditorDialog(stale, true);
            try
            {
                if (freshEditor.Field<TextBox>("name").Text != "DAYANA FREITAS" || freshEditor.Field<TextBox>("username").Text != "DAYANA" || freshEditor.Field<ComboBox>("role").SelectedItem is not UserRole.Operador || freshEditor.Field<CheckBox>("active").IsChecked != true)
                    throw new InvalidOperationException("Editar a linha antiga precisa carregar o cadastro, o perfil e a situação atuais pelo ID.");
            }
            finally { freshEditor.Close(); }
            var freshState = window.CreateUserStateConfirmation(stale);
            try { if (freshState.TitleLabel.Text != "Desativar usuário" || !freshState.MessageLabel.Text.Contains("DAYANA FREITAS")) throw new InvalidOperationException("A confirmação deve usar a situação e o nome atuais."); }
            finally { freshState.Close(); }
            var freshDetails = window.CreateUserDetailsDialog(stale);
            try
            {
                var text = string.Join("\n", Descendants<TextBlock>(freshDetails).Select(block => block.Text));
                if (!freshDetails.HeaderSubtitleLabel.Text.Contains("DAYANA FREITAS") || text.Contains("Cadastro antigo")) throw new InvalidOperationException("Detalhes de uma linha antiga precisam consultar o cadastro atual.");
            }
            finally { freshDetails.Close(); }
            var absent = new UserAccount { Name = "Conta ausente", Username = "AUSENTE", Role = UserRole.Operador };
            before = State();
            foreach (var factory in new Func<Window>[] { () => window.CreateUserEditorDialog(absent, true), () => window.CreateUserDetailsDialog(absent), () => window.CreateUserStateConfirmation(absent), () => window.CreateUserPasswordResetDialog(absent) })
            {
                bool refused = false;
                try { var unexpected = factory(); unexpected.Close(); } catch (InvalidOperationException) { refused = true; }
                if (!refused || State() != before) throw new InvalidOperationException("Ações sobre um ID ausente precisam recusar a conta e jamais recriá-la.");
            }
            var draftSnapshot = vm.Snapshot; string draftName = vm.Snapshot.Settings.Name + " • rascunho"; vm.Snapshot.Settings.Name = draftName;
            Guid? selectionBeforeCancel = vm.SelectedUserRow?.Id; Exception? cancelError = null;
            window.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                FormDialog? opened = null;
                try
                {
                    opened = Application.Current.Windows.OfType<FormDialog>().Single(dialog => dialog.IsVisible && ReferenceEquals(dialog.Owner, window));
                    SetText(opened, "name", "Edição cancelada pelo painel"); Click(opened.CancelButton);
                }
                catch (Exception ex) { cancelError = ex; opened?.Close(); }
            }));
            window.ShowUserEditor(stale, true);
            if (cancelError is not null) throw cancelError;
            if (!ReferenceEquals(vm.Snapshot, draftSnapshot) || vm.Snapshot.Settings.Name != draftName || vm.SelectedUserRow?.Id != selectionBeforeCancel || State() != before)
                throw new InvalidOperationException("Cancelar pelo painel deve preservar o rascunho de configurações e a seleção sem atualizar o modelo.");
            vm.Snapshot.Settings.Name = service.Snapshot.Settings.Name;
            var more = Descendants<Button>(grid).Single(button => button.Name == "UserRowMoreButton" && button.DataContext is UserDashboardRow row && row.Id == dayana.Id);
            vm.SelectedUserRow = vm.UserRows.First(row => row.Id != dayana.Id); Click(more); FlushLayout(window);
            var menu = more.ContextMenu!;
            try
            {
                if (!menu.IsOpen || menu.DataContext is not UserDashboardRow row || row.Id != dayana.Id || vm.SelectedUserRow?.Id != dayana.Id || menu.Items.OfType<MenuItem>().Count() != 5 || menu.Items.OfType<MenuItem>().Any(item => item.Header?.ToString()?.Contains("Excluir") == true))
                    throw new InvalidOperationException("O menu precisa agir sobre a linha clicada e preservar o histórico, sem oferecer exclusão.");
                CaptureElement(menu, Path.Combine(output, "113-usuarios-menu-acoes.png"));
            }
            finally { menu.IsOpen = false; }
            window.Width = 1200; window.Height = 760; FlushLayout(window);
            Capture(window, Path.Combine(output, "111-usuarios-tela-menor.png"));
            var root = (FrameworkElement)window.Content;
            foreach (var control in new FrameworkElement[] { search, role, status, Find<Button>("UserEditSelectionButton"), Find<Button>("UserCreateButton") })
            {
                control.BringIntoView(); FlushLayout(window); var bounds = control.TransformToAncestor(root).TransformBounds(new Rect(control.RenderSize));
                if (!control.IsVisible || bounds.Left < -.5 || bounds.Right > root.ActualWidth + .5 || bounds.Top < -.5 || bounds.Bottom > root.ActualHeight + .5) throw new InvalidOperationException("Busca, filtros e botões precisam caber na janela menor.");
            }
            if (grid.Columns.Sum(column => column.ActualWidth) > grid.ActualWidth + .5) throw new InvalidOperationException("A tabela de usuários precisa caber na largura real da tela menor.");
            before = State();
            var details = window.CreateUserDetailsDialog(service.Snapshot.Users.Single(account => account.Id == dayana.Id));
            try
            {
                Prepare(details); Capture(details, Path.Combine(output, "114-detalhes-usuario.png"));
                var text = string.Join("\n", Descendants<TextBlock>(details).Select(block => block.Text));
                var stored = service.Snapshot.Users.Single(account => account.Id == dayana.Id);
                if (!text.Contains(stored.Username) || text.Contains(stored.PasswordHash) || text.Contains(stored.PasswordSalt)) throw new InvalidOperationException("Detalhes devem mostrar o cadastro sem credenciais.");
                details.RaiseEvent(KeyEvent(details, Key.Escape, Keyboard.PreviewKeyDownEvent)); FlushLayout(window);
                if (details.IsVisible || State() != before) throw new InvalidOperationException("Esc em detalhes deve preservar os registros.");
            }
            finally { if (details.IsVisible) details.Close(); }
            NativeForm(window.CreateUserEditorDialog(service.Snapshot.Users.Single(account => account.Id == dayana.Id), true), dialog =>
            {
                SetText(dialog, "name", "Alteração descartada"); Password(dialog, "9999"); Click(dialog.CancelButton);
            }, false);
            if (State() != before) throw new InvalidOperationException("Cancelar uma edição deve preservar cadastro e senha.");
            NativeForm(window.CreateUserEditorDialog(service.Snapshot.Users.Single(account => account.Id == dayana.Id), false, true), dialog =>
            {
                Capture(dialog, Path.Combine(output, "115-duplicar-usuario.png"));
                if (dialog.Field<TextBox>("username").Text.Length != 0 || dialog.Field<PasswordBox>("password").Password.Length != 0) throw new InvalidOperationException("Duplicar não pode copiar o login ou a senha anterior.");
                SetText(dialog, "username", "NOVA_DAYANA"); Reject(dialog, before, "Duplicar deve exigir uma nova senha.");
                Password(dialog, "4444"); SetText(dialog, "username", "DAYANA"); Reject(dialog, before, "Login duplicado deve recusar a nova conta.");
                SetText(dialog, "username", "NOVA_DAYANA"); Click(dialog.SubmitButton);
            }, true);
            var duplicated = service.Snapshot.Users.Single(account => account.Username == "NOVA_DAYANA");
            var original = service.Snapshot.Users.Single(account => account.Id == dayana.Id);
            if (duplicated.Id == original.Id || duplicated.LastLogin is not null || !Passwords.Verify(duplicated, "4444") || Passwords.Verify(duplicated, "1111") || !Passwords.Verify(original, "1111"))
                throw new InvalidOperationException("A conta duplicada deve ter identidade e senha novas sem alterar a original.");
            before = State();
            NativeForm(window.CreateUserPasswordResetDialog(original), dialog =>
            {
                Capture(dialog, Path.Combine(output, "116-redefinir-senha-usuario.png"));
                Password(dialog, "5555"); dialog.Field<PasswordBox>("confirm").Password = "diferente"; Reject(dialog, before, "Senhas diferentes devem recusar a redefinição.");
                Password(dialog, "5555"); Click(dialog.SubmitButton);
            }, true);
            var reset = service.Snapshot.Users.Single(account => account.Id == original.Id);
            if (!Passwords.Verify(reset, "5555") || Passwords.Verify(reset, "1111") || reset.LastLogin != original.LastLogin || reset.Name != original.Name || reset.Role != original.Role) throw new InvalidOperationException("Redefinir senha deve alterar apenas a credencial e preservar cadastro e acesso histórico.");
            before = State();
            var confirmation = window.CreateUserStateConfirmation(reset); Prepare(confirmation); Exception? confirmationError = null;
            confirmation.Loaded += (_, _) => confirmation.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                try
                {
                    Capture(confirmation, Path.Combine(output, "117-desativar-usuario-confirmacao.png"));
                    if (!confirmation.CancelButton.IsDefault || confirmation.ConfirmButton.IsDefault || !confirmation.NoticeText.Contains("preservados")) throw new InvalidOperationException("Cancelar deve ser a escolha padrão e a confirmação deve explicar a preservação do histórico.");
                    Click(confirmation.CancelButton);
                }
                catch (Exception ex) { confirmationError = ex; confirmation.Close(); }
            }));
            if (confirmation.ShowDialog() == true || confirmationError is not null || State() != before) throw confirmationError ?? new InvalidOperationException("Cancelar a desativação deve preservar acesso e histórico.");
            window.ChangeUserActive(reset, false); vm.Refresh(); FlushLayout(window);
            if (service.Snapshot.Users.Single(account => account.Id == dayana.Id).Active) throw new InvalidOperationException("Desativar precisa impedir novos acessos.");
            bool rejected = false; try { service.Login("DAYANA", "5555"); } catch (InvalidOperationException) { rejected = true; }
            if (!rejected) throw new InvalidOperationException("A conta inativa não pode entrar no sistema.");
            window.ChangeUserActive(service.Snapshot.Users.Single(account => account.Id == dayana.Id), true); service.Login("DAYANA", "5555");
            if (service.Snapshot.Users.Single(account => account.Id == dayana.Id).LastLogin is null) throw new InvalidOperationException("O último acesso precisa refletir um login real.");
            before = State(); rejected = false;
            try { window.CreateUserEditorDialog(original, true); } catch (UnauthorizedAccessException) { rejected = true; }
            if (!rejected) throw new InvalidOperationException("Operador não pode abrir edição de usuários.");
            rejected = false; try { window.ChangeUserActive(original, false); } catch (UnauthorizedAccessException) { rejected = true; }
            if (!rejected || State() != before) throw new InvalidOperationException("Operador não pode alterar contas.");
            vm.Refresh(); if (vm.UserRows.Count != 0 || vm.UserCanEditSelection) throw new InvalidOperationException("A gestão de contas deve ficar indisponível para o operador.");
            service.Login("ADMIN", "ADMIN"); vm.Refresh();
            var admin = service.CurrentUser!; before = State(); rejected = false;
            try { window.ChangeUserActive(admin, false); } catch (InvalidOperationException) { rejected = true; }
            if (!rejected || State() != before || vm.UserRows.Single(row => row.Id == admin.Id).CanChangeState) throw new InvalidOperationException("A própria conta ativa precisa ser protegida.");
            window.ChangeUserActive(service.Snapshot.Users.Single(account => account.Id == manager.Id), false); vm.Refresh(); before = State();
            NativeForm(window.CreateUserEditorDialog(service.CurrentUser!, true), dialog =>
            {
                dialog.Field<ComboBox>("role").SelectedItem = UserRole.Operador; Reject(dialog, before, "O último administrador ativo não pode perder seu perfil."); Click(dialog.CancelButton);
            }, false);
            for (int index = 0; index < 8; index++) service.SaveUser(new() { Name = $"Operador rolagem {index + 1}", Username = $"ROLAGEM_{index + 1}", Role = UserRole.Operador }, "6666");
            vm.Refresh(); grid.BringIntoView(); FlushLayout(window);
            var viewer = ControlledScrolling.GetScrollViewer(grid) ?? throw new InvalidOperationException("A tabela precisa de uma área própria de rolagem.");
            viewer.ScrollToVerticalOffset(25); FlushLayout(window); double tableBefore = viewer.VerticalOffset, pageBefore = scroll.VerticalOffset;
            var source = Descendants<TextBlock>(grid).First(block => block.IsVisible && block.DataContext is UserDashboardRow);
            var wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -20) { RoutedEvent = Mouse.PreviewMouseWheelEvent }; source.RaiseEvent(wheel); FlushLayout(window);
            double expected = SystemParameters.WheelScrollLines == 0 ? 0 : 22d / 6;
            if (!wheel.Handled || Math.Abs(viewer.VerticalOffset - tableBefore - expected) > .5 || Math.Abs(scroll.VerticalOffset - pageBefore) > .5) throw new InvalidOperationException("O touchpad deve mover apenas a tabela de usuários com sensibilidade reduzida.");
            File.WriteAllText(Path.Combine(output, "usuarios-smoke-ok.txt"), "Métricas reais, busca com acentos/perfil, filtros, estado vazio, seleção por ID, menu da linha correta, layout menor e rolagem própria conferidos. Fábricas consultam os dados atuais de linhas antigas e recusam IDs ausentes. Cancelar pelo painel preserva cadastro, credenciais, rascunho de configurações e seleção; duplicar exige login único e nova senha; redefinir protege cadastro/histórico. Ativar/desativar e login real conferidos. Operador, própria conta e último administrador ativo protegidos. Detalhes não expõem hash ou salt.\n");
        }
        finally { window.Vm.ClearCart(); if (window.IsVisible) window.Close(); }
    }
}
