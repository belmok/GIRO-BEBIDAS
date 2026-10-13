using System.Globalization;
using System.Text;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public sealed record UserDashboardRow(UserAccount Profile, bool CanEdit, bool CanChangeState)
{
    public Guid Id => Profile.Id;
    public string Name => Profile.Name;
    public string Username => Profile.Username;
    public string RoleLabel => Profile.RoleLabel;
    public bool IsAdmin => Profile.Role == UserRole.Administrador;
    public bool Active => Profile.Active;
    public string StatusLabel => Profile.StatusLabel;
    public string Initials => string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(part => part[..1])).ToUpperInvariant();
    public string LastLoginLabel => Profile.LastLogin?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "Ainda não acessou";
    public DateTimeOffset? LastLogin => Profile.LastLogin;
    public string StateActionLabel => Active ? "Desativar usuário" : "Ativar usuário";
    public string StateActionHint => CanChangeState ? "O histórico desta conta será preservado." : "A própria conta e o último administrador ativo devem ser preservados.";
}

public sealed partial class MainViewModel
{
    private string userSearch = "", userRoleFilter = "Todos os perfis", userStatusFilter = "Todas as situações";
    private UserDashboardRow? selectedUserRow;
    public List<UserDashboardRow> UserRows { get; private set; } = [];
    public List<string> UserRoleOptions { get; } = ["Todos os perfis", "Administrador", "Operador"];
    public List<string> UserStatusOptions { get; } = ["Todas as situações", "Ativo", "Inativo"];
    public string UserSearch { get => userSearch; set { if (Set(ref userSearch, value ?? "")) RebuildUsers(); } }
    public string UserRoleFilter { get => userRoleFilter; set { if (Set(ref userRoleFilter, value ?? "Todos os perfis")) RebuildUsers(); } }
    public string UserStatusFilter { get => userStatusFilter; set { if (Set(ref userStatusFilter, value ?? "Todas as situações")) RebuildUsers(); } }
    public UserDashboardRow? SelectedUserRow
    {
        get => selectedUserRow;
        set
        {
            if (!Set(ref selectedUserRow, value)) return;
            SelectedUser = value?.Profile;
            Changed(nameof(UserCanEditSelection));
        }
    }
    public int UserTotalCount => Snapshot.Users.Count;
    public int UserAdminCount => Snapshot.Users.Count(user => user.Role == UserRole.Administrador);
    public int UserOperatorCount => Snapshot.Users.Count(user => user.Role == UserRole.Operador);
    public int UserActiveCount => Snapshot.Users.Count(user => user.Active);
    public string UserCountLabel => $"{UserRows.Count} usuário{(UserRows.Count == 1 ? " encontrado" : "s encontrados")}";
    public string UserEmptyVisibility => UserRows.Count == 0 ? "Visible" : "Collapsed";
    public bool UserCanEditSelection => IsAdmin && SelectedUserRow is not null;
    internal void RebuildUsers()
    {
        Guid? selectedId = SelectedUserRow?.Id ?? SelectedUser?.Id;
        string query = NormalizeUserSearch(UserSearch.Trim());
        int activeAdmins = Snapshot.Users.Count(user => user.Active && user.Role == UserRole.Administrador);
        UserRows = IsAdmin ? Snapshot.Users
            .Where(user => UserRoleFilter == "Todos os perfis" || user.RoleLabel == UserRoleFilter)
            .Where(user => UserStatusFilter == "Todas as situações" || user.StatusLabel == UserStatusFilter)
            .Where(user => query.Length == 0 || NormalizeUserSearch($"{user.Name} {user.Username} {user.RoleLabel}").Contains(query, StringComparison.Ordinal))
            .OrderBy(user => user.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(user => user.Username, StringComparer.OrdinalIgnoreCase)
            .Select(user => new UserDashboardRow(user, IsAdmin, IsAdmin && user.Id != Service.CurrentUserId && !(user.Active && user.Role == UserRole.Administrador && activeAdmins <= 1)))
            .ToList() : [];
        Changed(nameof(UserRows));
        SelectedUserRow = UserRows.FirstOrDefault(user => user.Id == selectedId);
        foreach (var name in new[] { nameof(UserTotalCount), nameof(UserAdminCount), nameof(UserOperatorCount), nameof(UserActiveCount), nameof(UserCountLabel), nameof(UserEmptyVisibility), nameof(UserCanEditSelection) }) Changed(name);
    }
    private static string NormalizeUserSearch(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        return string.Concat(normalized.Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)).Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }
}
