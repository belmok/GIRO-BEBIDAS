using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Markup;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public partial class App : Application
{
    private Mutex? instance;
    public App() => UseBrazilianCulture();
    private static void UseBrazilianCulture()
    {
        var culture = CultureInfo.GetCultureInfo("pt-BR");
        CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture; CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
    protected override void OnStartup(StartupEventArgs e)
    {
        UseBrazilianCulture();
        base.OnStartup(e);
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("pt-BR")));
        string dataDir = Option(e.Args, "--data-dir") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GiroBebidas");
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(dataDir).ToUpperInvariant())))[..20];
        instance = new Mutex(true, $"Local\\GiroBebidas-{key}", out bool created);
        if (!created) { MessageBox.Show("O Giro Bebidas já está aberto neste computador.", "Giro Bebidas"); Shutdown(); return; }
        try
        {
            var store = new JsonStore(dataDir); var service = new StoreService(store);
            if (e.Args.Contains("--ui-smoke")) { SmokeRunner.Run(service, Option(e.Args, "--output") ?? Path.Combine(dataDir, "telas")); Shutdown(); return; }
            if (store.RecoveredFromBackup) MessageBox.Show("Os dados foram recuperados da última cópia de segurança. O arquivo anterior foi preservado na pasta de dados.", "Backup recuperado");
            while (true)
            {
                var login = new LoginWindow(service); MainWindow = login;
                if (login.ShowDialog() != true) break;
                var main = new MainWindow(service) { WindowState = WindowState.Maximized }; MainWindow = main; main.ShowDialog();
                if (!main.ReturnToLogin) break;
                service.Logout();
            }
            Shutdown();
        }
        catch (Exception ex)
        {
            if (e.Args.Contains("--ui-smoke")) { File.WriteAllText(Path.Combine(dataDir, "smoke-error.txt"), ex.ToString()); Shutdown(1); return; }
            MessageBox.Show($"Não foi possível iniciar o sistema.\n\n{ex.Message}", "Giro Bebidas", MessageBoxButton.OK, MessageBoxImage.Error); Shutdown(1);
        }
    }
    private static string? Option(string[] args, string key) { int index = Array.IndexOf(args, key); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }
    protected override void OnExit(ExitEventArgs e) { instance?.Dispose(); base.OnExit(e); }
}
