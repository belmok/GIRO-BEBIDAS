using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public partial class UpdatesPanel : UserControl
{
    private readonly GitHubUpdateClient client = new();
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? operation;
    private GitHubReleaseUpdate? release;
    private bool initialized, busy, closed, repositoryHidden;
    private MainViewModel Vm => (MainViewModel)DataContext;
    private MainWindow Main => (MainWindow)Window.GetWindow(this);
    private static Version CurrentVersion => typeof(StoreService).Assembly.GetName().Version!;
    public UpdatesPanel() => InitializeComponent();

    private void Panel_Loaded(object sender, RoutedEventArgs e) => InitializeSettings();
    private void InitializeSettings()
    {
        if (initialized || DataContext is not MainViewModel) return;
        var settings = Vm.Service.Snapshot.Settings.GitHubUpdates;
        RepositoryBox.Text = settings.Repository; AutoCheckBox.IsChecked = settings.AutoCheck;
        CurrentVersionLabel.Text = $"Versão instalada: {CurrentVersion.ToString(3)}";
        initialized = true;
    }
    private void RequireAdmin()
    {
        if (!Vm.IsAdmin) throw new UnauthorizedAccessException("Esta ação exige um administrador.");
    }
    private void SaveSettings()
    {
        RequireAdmin();
        Vm.Service.SaveUpdateSettings(new() { Repository = RepositoryBox.Text, AutoCheck = AutoCheckBox.IsChecked == true });
        RepositoryBox.Text = Vm.Service.Snapshot.Settings.GitHubUpdates.Repository;
        Vm.Refresh(); release = null; InstallButton.IsEnabled = false; ReleaseLabel.Text = ""; NotesLabel.Text = "";
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try { SaveSettings(); StatusLabel.Text = "Configuração salva. Você pode consultar as versões agora."; }
        catch (Exception ex) { StatusLabel.Text = ex.Message; }
    }
    private void Repository_Changed(object sender, TextChangedEventArgs e)
    {
        UpdateRepositoryVisibility();
        if (!initialized) return;
        release = null; InstallButton.IsEnabled = false; ReleaseLabel.Text = ""; NotesLabel.Text = "";
    }
    private void ToggleRepositoryVisibility_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        repositoryHidden = !repositoryHidden;
        UpdateRepositoryVisibility();
        if (!repositoryHidden) { RepositoryBox.Focus(); RepositoryBox.CaretIndex = RepositoryBox.Text.Length; }
    }
    private void UpdateRepositoryVisibility()
    {
        if (RepositoryMask is null || RepositoryVisibilityButton is null) return;
        RepositoryBox.Visibility = repositoryHidden ? Visibility.Hidden : Visibility.Visible;
        RepositoryMask.Visibility = repositoryHidden ? Visibility.Visible : Visibility.Collapsed;
        RepositoryMask.Text = string.IsNullOrWhiteSpace(RepositoryBox.Text) ? "Nenhum repositório configurado" : "•••••••• / ••••••••••";
        RepositoryVisibilityIcon.Text = repositoryHidden ? "\uED1A" : "\uE890";
        RepositoryVisibilityLabel.Text = repositoryHidden ? "Mostrar" : "Ocultar";
        string action = repositoryHidden ? "Mostrar endereço do repositório" : "Ocultar endereço do repositório";
        RepositoryVisibilityButton.ToolTip = action;
        System.Windows.Automation.AutomationProperties.SetName(RepositoryVisibilityButton, action);
    }
    public async Task CheckOnStartupAsync()
    {
        InitializeSettings();
        var settings = Vm.Service.Snapshot.Settings.GitHubUpdates;
        if (Vm.IsAdmin && settings.AutoCheck && settings.Repository.Length > 0) await CheckAsync(false);
    }
    private async void Check_Click(object sender, RoutedEventArgs e)
    {
        try { SaveSettings(); await CheckAsync(true); }
        catch (Exception ex) { StatusLabel.Text = ex.Message; }
    }
    private void SetBusy(bool value, bool download = false)
    {
        busy = value; SaveButton.IsEnabled = CheckButton.IsEnabled = RepositoryBox.IsEnabled = AutoCheckBox.IsEnabled = RepositoryVisibilityButton.IsEnabled = !value;
        InstallButton.IsEnabled = !value && release is not null;
        CancelButton.Visibility = value && download ? Visibility.Visible : Visibility.Collapsed;
        DownloadProgress.Visibility = value && download ? Visibility.Visible : Visibility.Collapsed;
    }
    private async Task CheckAsync(bool manual)
    {
        if (busy || closed) return;
        SetBusy(true); release = null; ReleaseLabel.Text = ""; NotesLabel.Text = "";
        operation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        try
        {
            RequireAdmin(); StatusLabel.Text = "Consultando as publicações do GitHub…";
            release = await client.CheckAsync(Vm.Service.Snapshot.Settings.GitHubUpdates.Repository, CurrentVersion, operation.Token);
            if (release is null) StatusLabel.Text = "Não foi encontrada uma versão pública mais recente. O repositório pode ainda não ter publicações disponíveis.";
            else
            {
                ReleaseLabel.Text = $"Versão {release.Version.ToString(3)} disponível";
                NotesLabel.Text = release.Notes;
                StatusLabel.Text = $"Instalador: {release.InstallerAsset.Size / 1024d / 1024d:N1} MB. Confira as novidades e escolha quando instalar.";
                if (!manual) Vm.Notify?.Invoke($"Giro Bebidas {release.Version.ToString(3)} disponível. Abra Atualizações no menu.");
            }
        }
        catch (OperationCanceledException) { if (!closed) StatusLabel.Text = "Consulta cancelada."; }
        catch (Exception ex) { if (!closed) StatusLabel.Text = $"Não foi possível consultar: {ex.Message}"; }
        finally { operation.Dispose(); operation = null; if (!closed) SetBusy(false); }
    }
    private void EnsureReadyToInstall()
    {
        RequireAdmin();
        if (Vm.Cart.Count > 0) throw new InvalidOperationException("Finalize ou limpe a venda atual antes de atualizar.");
        if (Vm.Service.Snapshot.CashSessions.Any(c => c.ClosedAt is null)) throw new InvalidOperationException("Feche o caixa antes de instalar a atualização.");
        if (Installation.ReadManifest(AppContext.BaseDirectory) is null) throw new InvalidOperationException("Esta cópia é portátil. Instale o Giro Bebidas com o instalador para atualizar por esta tela.");
    }
    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (busy || release is null || closed) return;
        var selected = release;
        try { EnsureReadyToInstall(); }
        catch (Exception ex) { StatusLabel.Text = ex.Message; return; }
        SetBusy(true, true); DownloadProgress.Value = 0;
        operation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        try
        {
            StatusLabel.Text = "Baixando o instalador e conferindo sua integridade…";
            string folder = Path.Combine(Vm.Service.Store.DirectoryPath, "Atualizacoes");
            var progress = new Progress<GitHubDownloadProgress>(p =>
            {
                if (closed) return;
                DownloadProgress.Value = p.Percentage;
                StatusLabel.Text = $"Baixando: {p.Percentage:N0}% • {p.BytesReceived / 1024d / 1024d:N1} de {p.TotalBytes / 1024d / 1024d:N1} MB";
            });
            string installer = await client.DownloadAsync(selected, folder, progress, operation.Token);
            operation.Token.ThrowIfCancellationRequested(); EnsureReadyToInstall();
            string backups = Path.Combine(Vm.Service.Store.DirectoryPath, "Backups"); Directory.CreateDirectory(backups);
            string backup = Path.Combine(backups, $"antes-atualizacao-{DateTime.Now:yyyyMMdd-HHmmssfff}.json");
            Vm.Service.ExportBackup(backup);
            StatusLabel.Text = "Instalador conferido e backup criado. Pronto para instalar.";
            if (MessageBox.Show(Main, $"Atualização {selected.Version.ToString(3)} pronta.\n\nBackup criado em:\n{backup}\n\nFechar o PDV e abrir o instalador agora?", "Atualizar Giro Bebidas", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            EnsureReadyToInstall();
            var start = new ProcessStartInfo(installer) { UseShellExecute = true };
            start.ArgumentList.Add("--update-install-dir"); start.ArgumentList.Add(AppContext.BaseDirectory);
            Process.Start(start); Main.Close();
        }
        catch (OperationCanceledException) { if (!closed) StatusLabel.Text = "Download cancelado. Você pode tentar novamente."; }
        catch (Exception ex) { if (!closed) StatusLabel.Text = $"Não foi possível atualizar: {ex.Message}"; }
        finally { operation?.Dispose(); operation = null; if (!closed) SetBusy(false); }
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => operation?.Cancel();
    private void OpenReleases_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            RequireAdmin(); string repo = GitHubUpdateClient.NormalizeRepository(RepositoryBox.Text);
            if (repo.Length == 0) throw new InvalidOperationException("Informe o repositório para abrir suas publicações.");
            Process.Start(new ProcessStartInfo($"https://github.com/{repo}/releases") { UseShellExecute = true });
        }
        catch (Exception ex) { StatusLabel.Text = ex.Message; }
    }
    internal void CloseOperations()
    {
        if (closed) return;
        closed = true; lifetime.Cancel(); client.Dispose(); lifetime.Dispose();
    }
}
