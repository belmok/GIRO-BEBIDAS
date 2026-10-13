using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public partial class MainWindow
{
    private void RequireUserAdministrator()
    {
        if (!Vm.Service.IsAdmin) throw new UnauthorizedAccessException("Esta ação exige um administrador.");
    }
    private UserAccount CurrentUserForAction(Guid id)
    {
        RequireUserAdministrator();
        return Vm.Service.Snapshot.Users.FirstOrDefault(account => account.Id == id)
            ?? throw new InvalidOperationException("Este usuário não está mais disponível. Atualize a lista e selecione a conta novamente.");
    }
    internal FormDialog CreateUserEditorDialog(UserAccount source, bool editing, bool duplicate = false)
    {
        RequireUserAdministrator();
        if (editing || duplicate) source = CurrentUserForAction(source.Id);
        var dialog = new FormDialog(this, duplicate ? "Duplicar usuário" : editing ? "Editar usuário" : "Criar usuário", duplicate ? "Crie uma conta independente com outro usuário e uma nova senha." : "Use uma conta individual para cada pessoa.");
        dialog.Text("name", "Nome", duplicate ? source.Name + " (cópia)" : source.Name).MaxLength = 80;
        dialog.Text("username", "Usuário • único no sistema", duplicate ? "" : source.Username).MaxLength = 30;
        dialog.Choice("role", "Perfil", Enum.GetValues<UserRole>(), source.Role);
        dialog.Password("password", editing && !duplicate ? "Nova senha • deixe vazio para manter" : "Senha (mínimo de 4 caracteres)");
        dialog.Password("confirm", "Confirmar senha");
        dialog.Check("active", "Usuário ativo", duplicate || source.Active);
        dialog.Info("Administrador: acesso completo. Operador: vendas, pedidos, clientes, fiados e caixa. A senha atual nunca é exibida.");
        dialog.Submit = form =>
        {
            RequireUserAdministrator();
            if (editing && !duplicate) _ = CurrentUserForAction(source.Id);
            if (form.Value("password") != form.Value("confirm")) throw new InvalidOperationException("As senhas não conferem.");
            Vm.Service.SaveUser(new UserAccount
            {
                Id = editing && !duplicate ? source.Id : Guid.NewGuid(), Name = form.Value("name"), Username = form.Value("username"),
                Role = form.Selected<UserRole>("role"), Active = form.Checked("active")
            }, form.Value("password"));
        };
        return dialog;
    }
    internal void ShowUserEditor(UserAccount source, bool editing, bool duplicate = false)
    {
        try
        {
            if (CreateUserEditorDialog(source, editing, duplicate).ShowDialog() != true) return;
            Vm.Refresh(); Notify(duplicate ? "Nova conta criada com senha independente." : "Usuário salvo.");
            if (editing && !duplicate && source.Id == Vm.Service.CurrentUserId) { ReturnToLogin = true; Close(); }
        }
        catch (Exception ex) { Notify(ex.Message); }
    }
    internal FormDialog CreateUserPasswordResetDialog(UserAccount user)
    {
        user = CurrentUserForAction(user.Id);
        var dialog = new FormDialog(this, "Redefinir senha", "Informe uma nova senha para esta conta. A senha atual não é exibida.", "Redefinir senha");
        dialog.Summary("Conta selecionada", [("Nome", user.Name), ("Usuário", user.Username)]);
        dialog.Password("password", "Nova senha (mínimo de 4 caracteres)"); dialog.Password("confirm", "Confirmar nova senha");
        dialog.Submit = form =>
        {
            RequireUserAdministrator();
            if (form.Value("password") != form.Value("confirm")) throw new InvalidOperationException("As senhas não conferem.");
            if (string.IsNullOrEmpty(form.Value("password"))) throw new InvalidOperationException("Informe a nova senha.");
            var current = CurrentUserForAction(user.Id);
            Vm.Service.SaveUser(new() { Id = current.Id, Name = current.Name, Username = current.Username, Role = current.Role, Active = current.Active }, form.Value("password"));
        };
        return dialog;
    }
    internal void ShowUserPasswordReset(UserAccount user)
    {
        try
        {
            if (CreateUserPasswordResetDialog(user).ShowDialog() != true) return;
            Vm.Refresh(); Notify("Senha redefinida.");
            if (user.Id == Vm.Service.CurrentUserId) { ReturnToLogin = true; Close(); }
        }
        catch (Exception ex) { Notify(ex.Message); }
    }
    internal ConfirmationWindow CreateUserStateConfirmation(UserAccount user)
    {
        user = CurrentUserForAction(user.Id);
        return BuildUserStateConfirmation(user, !user.Active);
    }
    private ConfirmationWindow BuildUserStateConfirmation(UserAccount user, bool activating)
    {
        return new ConfirmationWindow(this, activating ? "Ativar usuário" : "Desativar usuário", $"{(activating ? "Permitir novamente o acesso de" : "Impedir novos acessos de")} {user.Name} ({user.Username})?", activating ? "Ativar usuário" : "Desativar usuário")
        { NoticeText = "Vendas, pedidos e o histórico desta conta serão preservados." };
    }
    internal void ChangeUserActive(UserAccount user, bool active)
    {
        var current = CurrentUserForAction(user.Id);
        Vm.Service.SaveUser(new() { Id = current.Id, Name = current.Name, Username = current.Username, Role = current.Role, Active = active }, null);
    }
    internal void ShowUserStateChange(UserAccount user)
    {
        try
        {
            var current = CurrentUserForAction(user.Id); bool activating = !current.Active;
            if (BuildUserStateConfirmation(current, activating).ShowDialog() != true) return;
            ChangeUserActive(current, activating); Vm.Refresh(); Notify(activating ? "Usuário ativado." : "Usuário desativado.");
        }
        catch (Exception ex) { Notify(ex.Message); }
    }
    internal DetailWindow CreateUserDetailsDialog(UserAccount user)
    {
        user = CurrentUserForAction(user.Id);
        var dialog = new DetailWindow(this, "Detalhes do usuário", $"{user.Name} • {user.Username}", "\uE77B") { Height = Math.Min(630, SystemParameters.WorkArea.Height - 32) };
        var lines = new StackPanel();
        foreach (var item in new[] { ("Nome", user.Name), ("Usuário", user.Username), ("Perfil", user.RoleLabel), ("Situação", user.StatusLabel), ("Último acesso", user.LastLogin?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "Ainda não acessou") })
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            grid.ColumnDefinitions.Add(new() { Width = new GridLength(135) }); grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            grid.Children.Add(new TextBlock { Text = item.Item1, FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(83, 109, 96)) });
            var value = new TextBlock { Text = item.Item2, FontSize = 14, Foreground = new SolidColorBrush(Color.FromRgb(30, 56, 43)), TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold };
            Grid.SetColumn(value, 1); grid.Children.Add(value); lines.Children.Add(grid);
        }
        dialog.AddSection("Cadastro e acesso", lines);
        dialog.AddSection("Permissões", new TextBlock { Text = user.Role == UserRole.Administrador ? "Acesso completo à administração do depósito." : "Acesso a vendas, pedidos, clientes, fiados e caixa.", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(53, 83, 70)) });
        return dialog;
    }
    internal void ShowUserDetails(UserAccount user)
    {
        try { CreateUserDetailsDialog(user).ShowDialog(); }
        catch (Exception ex) { Notify(ex.Message); }
    }
}
