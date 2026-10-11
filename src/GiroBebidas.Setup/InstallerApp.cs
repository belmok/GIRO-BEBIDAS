using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using GiroBebidas.Core;

namespace GiroBebidas.Setup;

public static class InstallerApp
{
    internal static string ProductVersion => typeof(InstallerApp).Assembly.GetName().Version!.ToString(3);
#if UNINSTALLER
    public static readonly bool UninstallMode = true;
#else
    public static readonly bool UninstallMode = false;
#endif
    [STAThread]
    public static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
        if (args.Length >= 2 && args[0] is "--verify-package" or "--verify-uninstall")
        {
            try
            {
                if (args[0] == "--verify-package") { using var package = Package(); Installation.Install(package, args[1]); File.WriteAllText(Path.Combine(args[1], "verificacao-ok.txt"), "Pacote extraído e manifesto conferido."); }
                else { Installation.Uninstall(args[1]); File.WriteAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1]))!, "desinstalacao-ok.txt"), "Arquivos do aplicativo removidos. Arquivos adicionais preservados."); }
                return 0;
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1]))!, "instalacao-erro.txt"), ex.ToString()); return 1; }
        }
        var app = new Application(); var window = new InstallerWindow();
        if (args.Length >= 2 && args[0] == "--ui-smoke")
        {
            window.Left = -10000; window.Top = -10000; window.WindowStartupLocation = WindowStartupLocation.Manual; window.ShowInTaskbar = false; window.ShowActivated = false;
            window.Loaded += (_, _) =>
            {
                try { window.UpdateLayout(); var bmp = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bmp.Render(window); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bmp)); using var output = File.Create(args[1]); png.Save(output); window.Close(); }
                catch (Exception ex) { File.WriteAllText(args[1] + ".error.txt", ex.ToString()); app.Shutdown(1); }
            };
        }
        return app.Run(window);
    }
    public static Stream Package() => Assembly.GetExecutingAssembly().GetManifestResourceStream("GiroBebidas.Payload.zip") ?? throw new InvalidOperationException("Este arquivo não contém o pacote de instalação. Execute o instalador gerado na pasta de distribuição.");
}

public sealed class InstallerWindow : Window
{
    private readonly TextBox target;
    private readonly CheckBox desktop, deleteData;
    private readonly TextBlock status;
    private readonly ProgressBar progress;
    private readonly Button primary;
    private bool busy, complete;
    public InstallerWindow()
    {
        bool uninstall = InstallerApp.UninstallMode;
        Title = uninstall ? "Desinstalar Giro Bebidas" : "Instalar Giro Bebidas";
        Width = 760; Height = 620; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = new FontFamily("Segoe UI"); FontSize = 14; Background = Brush("#F7F9F4"); Foreground = Brush("#18372A");
        var root = new Grid(); root.RowDefinitions.Add(new() { Height = new GridLength(127) }); root.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) }); root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var banner = new Border { Background = Brush("#112D25"), Padding = new Thickness(32, 24, 32, 24) };
        var brand = new StackPanel(); brand.Children.Add(new TextBlock { Text = "GIRO  BEBIDAS", Foreground = Brush("#C8EEBC"), FontSize = 28, FontWeight = FontWeights.Bold }); brand.Children.Add(new TextBlock { Text = "SEU DEPÓSITO. TUDO NO SEU RITMO.", Foreground = Brush("#8AB297"), FontSize = 10, Margin = new Thickness(0, 10, 0, 0) }); banner.Child = brand; root.Children.Add(banner);
        var body = new StackPanel { Margin = new Thickness(32, 25, 32, 0) }; Grid.SetRow(body, 1); root.Children.Add(body);
        body.Children.Add(new TextBlock { Text = uninstall ? "Remover o aplicativo" : "Tudo pronto para começar", FontSize = 25, FontWeight = FontWeights.SemiBold });
        body.Children.Add(new TextBlock { Text = uninstall ? "Seus dados serão preservados, a menos que você escolha excluí-los abaixo." : "Instale o PDV com vendas, estoque, clientes e caixa no seu Windows.", FontSize = 13, Foreground = Brush("#788B74"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 9, 0, 20) });
        body.Children.Add(new TextBlock { Text = "Pasta do aplicativo", FontWeight = FontWeights.SemiBold, FontSize = 12, Margin = new Thickness(0, 0, 0, 7) });
        string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "GiroBebidas");
        if (uninstall && File.Exists(Path.Combine(AppContext.BaseDirectory, Installation.ManifestName))) defaultPath = AppContext.BaseDirectory;
        var folderRow = new DockPanel(); var browse = Button("Escolher", false); browse.Margin = new Thickness(10, 0, 0, 0); DockPanel.SetDock(browse, Dock.Right); folderRow.Children.Add(browse);
        target = new TextBox { Text = defaultPath, Padding = new Thickness(10, 10, 10, 10), BorderBrush = Brush("#D8E4D2"), Background = Brushes.White }; folderRow.Children.Add(target); body.Children.Add(folderRow);
        browse.Click += (_, _) => { var pick = new OpenFolderDialog { Title = "Escolher pasta do Giro Bebidas", InitialDirectory = Directory.Exists(target.Text) ? target.Text : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) }; if (pick.ShowDialog(this) == true) target.Text = uninstall ? pick.FolderName : Path.Combine(pick.FolderName, "GiroBebidas"); };
        desktop = new CheckBox { Content = "Criar atalho na área de trabalho", IsChecked = true, Margin = new Thickness(0, 17, 0, 0), Visibility = uninstall ? Visibility.Collapsed : Visibility.Visible }; body.Children.Add(desktop);
        deleteData = new CheckBox { Content = "Excluir também todos os dados, fotos e backups locais", IsChecked = false, Margin = new Thickness(0, 17, 0, 0), Visibility = uninstall ? Visibility.Visible : Visibility.Collapsed }; body.Children.Add(deleteData);
        body.Children.Add(new TextBlock { Text = uninstall ? "A desinstalação remove apenas os arquivos registrados pelo instalador." : "Primeiro acesso: usuário ADMIN • senha ADMIN\nAplicativo independente: não exige instalação prévia do .NET.", Foreground = Brush("#6F8965"), FontSize = 12, Margin = new Thickness(0, 20, 0, 0), TextWrapping = TextWrapping.Wrap });
        progress = new ProgressBar { Height = 7, Minimum = 0, Maximum = 100, Foreground = Brush("#247C4E"), Margin = new Thickness(0, 22, 0, 10), Visibility = Visibility.Hidden }; body.Children.Add(progress);
        status = new TextBlock { Text = $"Versão {InstallerApp.ProductVersion} • Windows 10 / 11 • 64 bits", Foreground = Brush("#7D9272"), FontSize = 12, TextWrapping = TextWrapping.Wrap }; body.Children.Add(status);
        var footer = new DockPanel { Margin = new Thickness(32, 13, 32, 27) }; Grid.SetRow(footer, 2); root.Children.Add(footer);
        primary = Button(uninstall ? "Desinstalar" : "Instalar agora", true); primary.MinWidth = 157; DockPanel.SetDock(primary, Dock.Right); footer.Children.Add(primary); primary.Click += Primary_Click;
        var cancel = Button("Fechar", false); cancel.HorizontalAlignment = HorizontalAlignment.Left; cancel.Click += (_, _) => { if (!busy) Close(); }; footer.Children.Add(cancel); Content = root;
        Closing += (_, e) => { if (busy) e.Cancel = true; };
    }
    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color));
    private static Button Button(string label, bool primary)
    {
        var button = new Button { Content = label, Padding = new Thickness(18, 11, 18, 11), Background = Brush(primary ? "#187B52" : "#FFFFFF"), Foreground = primary ? Brushes.White : Brush("#36543D"), BorderBrush = Brush("#DAE4D2"), FontWeight = FontWeights.SemiBold, Cursor = System.Windows.Input.Cursors.Hand };
        var template = new ControlTemplate(typeof(Button)); var border = new FrameworkElementFactory(typeof(Border)); border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8)); border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(Control.Background)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent }); border.SetValue(Border.PaddingProperty, button.Padding); var content = new FrameworkElementFactory(typeof(ContentPresenter)); content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center); border.AppendChild(content); template.VisualTree = border; button.Template = template; return button;
    }
    private async void Primary_Click(object sender, RoutedEventArgs e)
    {
        if (complete) { if (!InstallerApp.UninstallMode) Process.Start(new ProcessStartInfo(Path.Combine(target.Text, "GiroBebidas.exe")) { UseShellExecute = true }); Close(); return; }
        bool removeData = deleteData.IsChecked == true, createDesktop = desktop.IsChecked == true;
        string path;
        try { path = Path.GetFullPath(target.Text.Trim()); EnsureAppClosed(path); }
        catch (Exception ex) { status.Text = ex.Message; return; }
        if (InstallerApp.UninstallMode && MessageBox.Show(this, removeData ? "Excluir o aplicativo E TODOS OS DADOS do depósito? Esta exclusão é permanente." : "Desinstalar o aplicativo e preservar os dados?", "Giro Bebidas", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        busy = true; primary.IsEnabled = false; target.IsEnabled = false; progress.Visibility = Visibility.Visible;
        try
        {
            status.Text = InstallerApp.UninstallMode ? "Removendo os arquivos do aplicativo..." : "Instalando os arquivos do aplicativo...";
            if (InstallerApp.UninstallMode)
            {
                var deferred = await Task.Run(() => Installation.Uninstall(path, Environment.ProcessPath));
                WindowsRegistration.Remove(path);
                if (removeData)
                {
                    string data = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GiroBebidas"));
                    string expected = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)).TrimEnd('\\') + "\\GiroBebidas";
                    if (!data.Equals(expected, StringComparison.OrdinalIgnoreCase) || (Directory.Exists(data) && (File.GetAttributes(data) & FileAttributes.ReparsePoint) != 0)) throw new InvalidDataException("Pasta de dados inválida.");
                    if (Directory.Exists(data)) Directory.Delete(data, true);
                }
                if (deferred.Count > 0) WindowsRegistration.ScheduleCleanup(path, deferred);
                status.Text = removeData ? "Aplicativo e dados removidos." : "Aplicativo removido. Os dados e backups foram preservados.";
            }
            else
            {
                using var package = InstallerApp.Package();
                await Task.Run(() => Installation.Install(package, path, value => Dispatcher.Invoke(() => progress.Value = value)));
                WindowsRegistration.Add(path, createDesktop); status.Text = "Instalação concluída. Seu depósito está pronto para começar.";
            }
            progress.Value = 100; complete = true; primary.Content = InstallerApp.UninstallMode ? "Concluir" : "Abrir Giro Bebidas";
        }
        catch (Exception ex) { status.Text = ex.Message; target.IsEnabled = true; }
        finally { busy = false; primary.IsEnabled = true; }
    }
    private static void EnsureAppClosed(string target)
    {
        foreach (var process in Process.GetProcessesByName("GiroBebidas"))
            using (process)
                if (!process.HasExited) throw new InvalidOperationException("Feche o Giro Bebidas antes de instalar ou desinstalar.");
    }
}

public static class WindowsRegistration
{
    private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\GiroBebidas";
    private static string Menu => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Giro Bebidas");
    public static void Add(string target, bool desktop)
    {
        Directory.CreateDirectory(Menu); Shortcut(Path.Combine(Menu, "Giro Bebidas.lnk"), Path.Combine(target, "GiroBebidas.exe"));
        Shortcut(Path.Combine(Menu, "Desinstalar Giro Bebidas.lnk"), Path.Combine(target, "DesinstalarGiroBebidas.exe"));
        if (desktop) Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Giro Bebidas.lnk"), Path.Combine(target, "GiroBebidas.exe"));
        using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
        key.SetValue("DisplayName", "Giro Bebidas"); key.SetValue("DisplayVersion", InstallerApp.ProductVersion); key.SetValue("Publisher", "Giro Bebidas"); key.SetValue("InstallLocation", target);
        key.SetValue("DisplayIcon", Path.Combine(target, "GiroBebidas.exe")); key.SetValue("UninstallString", "\"" + Path.Combine(target, "DesinstalarGiroBebidas.exe") + "\"");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord); key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }
    private static void Shortcut(string file, string executable)
    {
        Type type = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("O Windows não disponibilizou o serviço de atalhos.");
        dynamic shell = Activator.CreateInstance(type)!; dynamic shortcut = shell.CreateShortcut(file);
        try { shortcut.TargetPath = executable; shortcut.WorkingDirectory = Path.GetDirectoryName(executable); shortcut.Description = "Giro Bebidas • PDV"; shortcut.Save(); }
        finally { Marshal.FinalReleaseComObject(shortcut); Marshal.FinalReleaseComObject(shell); }
    }
    public static void Remove(string target)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
        if (key?.GetValue("InstallLocation")?.ToString()?.Equals(target, StringComparison.OrdinalIgnoreCase) == true) Registry.CurrentUser.DeleteSubKeyTree(RegistryPath, false);
        foreach (string file in new[] { Path.Combine(Menu, "Giro Bebidas.lnk"), Path.Combine(Menu, "Desinstalar Giro Bebidas.lnk"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Giro Bebidas.lnk") })
            if (File.Exists(file)) File.Delete(file);
        if (Directory.Exists(Menu) && !Directory.EnumerateFileSystemEntries(Menu).Any()) Directory.Delete(Menu);
    }
    public static void ScheduleCleanup(string target, List<string> files)
    {
        target = Path.GetFullPath(target);
        foreach (string file in files) if (!Path.GetFullPath(file).StartsWith(target.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Arquivo de remoção fora da instalação.");
        string script = Path.Combine(Path.GetTempPath(), "giro-remover-" + Guid.NewGuid().ToString("N") + ".ps1");
        string Quote(string path) => "'" + path.Replace("'", "''") + "'";
        var body = new StringBuilder($"Wait-Process -Id {Environment.ProcessId} -ErrorAction SilentlyContinue\r\n");
        foreach (string file in files) body.AppendLine($"Remove-Item -LiteralPath {Quote(file)} -Force -ErrorAction SilentlyContinue");
        body.AppendLine($"Remove-Item -LiteralPath {Quote(target)} -ErrorAction SilentlyContinue"); body.AppendLine($"Remove-Item -LiteralPath {Quote(script)} -Force -ErrorAction SilentlyContinue");
        File.WriteAllText(script, body.ToString(), new UTF8Encoding(true));
        var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        foreach (string arg in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script }) start.ArgumentList.Add(arg);
        Process.Start(start);
    }
}
