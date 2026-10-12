using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using GiroBebidas.Core;
using Microsoft.Win32;

namespace GiroBebidas.App;

public record FiscalChoice<T>(T Value, string Label);
public partial class FiscalPanel : UserControl
{
    public FiscalPanel() => InitializeComponent();
    private MainWindow Main => (MainWindow)Window.GetWindow(this);
    private MainViewModel Vm => (MainViewModel)DataContext;
    private void Run(Action action, string? success = null)
    {
        try
        {
            if (!Vm.IsAdmin) throw new UnauthorizedAccessException("Esta ação exige um administrador.");
            action(); Vm.Refresh(); if (success is not null) Vm.Notify?.Invoke(success);
        }
        catch (Exception ex) { Vm.Notify?.Invoke(ex.Message); }
    }
    private static readonly FiscalChoice<FiscalEnvironment>[] Environments =
        [new(FiscalEnvironment.Homologacao, "Homologação • testes"), new(FiscalEnvironment.Producao, "Produção")];
    private static readonly FiscalChoice<TaxRegime?>[] Regimes =
        [new(null, "Selecione o regime"), new(TaxRegime.SimplesNacional, "1 • Simples Nacional"), new(TaxRegime.SimplesExcessoSublimite, "2 • Simples: excesso de sublimite"), new(TaxRegime.RegimeNormal, "3 • Regime normal"), new(TaxRegime.MEI, "4 • MEI")];
    internal FormDialog CreateIssuerDialog()
    {
        var p = Vm.Snapshot.Fiscal.Issuer;
        var d = new FormDialog(Main, "Empresa emitente", "Informe os dados reais do estabelecimento. Você pode salvar o cadastro incompleto e conferir as pendências depois.");
        d.Text("legal", "Razão social", p.LegalName); d.Text("trade", "Nome fantasia", p.TradeName);
        d.Text("cnpj", "CNPJ • aceita formato numérico ou alfanumérico", p.Cnpj); d.Text("ie", "Inscrição estadual", p.StateRegistration);
        d.Choice("crt", "Regime tributário • CRT", Regimes, Regimes.First(r => r.Value == p.Regime), nameof(FiscalChoice<TaxRegime?>.Label));
        var states = new[] { "Selecione a UF" }.Concat(FiscalValidation.States.Keys.Order()).ToArray();
        d.Choice("state", "UF", states, p.State.Length > 0 ? p.State : states[0]);
        d.Text("city", "Município", p.City); d.Text("ibge", "Código IBGE do município • 7 dígitos", p.CityCode);
        d.Text("street", "Logradouro", p.Street); d.Text("number", "Número", p.Number); d.Text("complement", "Complemento", p.Complement);
        d.Text("district", "Bairro", p.District); d.Text("cep", "CEP", p.PostalCode); d.Text("phone", "Telefone", p.Phone);
        d.Submit = f => Vm.Service.SaveFiscalIssuer(new()
        {
            LegalName = f.Value("legal"), TradeName = f.Value("trade"), Cnpj = f.Value("cnpj"), StateRegistration = f.Value("ie"),
            Regime = f.Selected<FiscalChoice<TaxRegime?>>("crt").Value, State = f.Value("state") == states[0] ? "" : f.Value("state"),
            City = f.Value("city"), CityCode = f.Value("ibge"), Street = f.Value("street"), Number = f.Value("number"), Complement = f.Value("complement"), District = f.Value("district"), PostalCode = f.Value("cep"), Phone = f.Value("phone")
        });
        return d;
    }
    private void Issuer_Click(object sender, RoutedEventArgs e) => Run(() => { if (CreateIssuerDialog().ShowDialog() == true) Vm.Notify?.Invoke("Dados fiscais da empresa salvos."); });
    internal FormDialog CreateOptionsDialog()
    {
        var fiscal = Vm.Snapshot.Fiscal;
        var d = new FormDialog(Main, "Configurar NFC-e", "Modelo 65. Homologação e produção têm série, próximo número e CSC separados. A numeração configurada só será usada por um emissor integrado.");
        var environment = d.Choice("environment", "Ambiente", Environments, Environments.First(p => p.Value == fiscal.Options.Environment), nameof(FiscalChoice<FiscalEnvironment>.Label));
        var series = d.Number("series", "Série", fiscal.ActiveProfile.Series); var next = d.Number("next", "Próximo número fiscal", fiscal.ActiveProfile.NextNumber);
        environment.SelectionChanged += (_, _) =>
        {
            if (environment.SelectedItem is FiscalChoice<FiscalEnvironment> choice)
            {
                var profile = choice.Value == FiscalEnvironment.Producao ? fiscal.Production : fiscal.Homologation;
                series.Text = profile.Series.ToString(); next.Text = profile.NextNumber.ToString();
            }
        };
        d.Text("terminal", "Identificação do caixa", fiscal.Options.Terminal);
        var printers = new List<FiscalChoice<Guid?>> { new(null, "Selecionar depois") };
        printers.AddRange(Vm.Printers.Select(p => new FiscalChoice<Guid?>(p.Id, $"{p.Name} • {p.Paper}")));
        d.Choice("printer", "Impressora do documento fiscal", printers, printers.FirstOrDefault(p => p.Value == fiscal.Options.PrinterId) ?? printers[0], nameof(FiscalChoice<Guid?>.Label));
        d.Info("Cadastre a impressora e o papel em Equipamentos. A impressora escolhida aqui fica reservada para o documento fiscal; o comprovante atual usa a impressora padrão do PDV.");
        d.Submit = f =>
        {
            decimal number = f.Decimal("next"), serie = f.Decimal("series");
            if (number != decimal.Truncate(number) || serie != decimal.Truncate(serie) || serie is < 0 or > 999 || number is < 1 or > 999999999)
                throw new InvalidOperationException("Série e próximo número devem ser inteiros dentro dos limites indicados: série 0–999 e número 1–999.999.999.");
            Vm.Service.SaveFiscalOptions(new() { Environment = f.Selected<FiscalChoice<FiscalEnvironment>>("environment").Value, Terminal = f.Value("terminal"), PrinterId = f.Selected<FiscalChoice<Guid?>>("printer").Value }, (int)serie, (long)number);
        };
        return d;
    }
    private void Options_Click(object sender, RoutedEventArgs e) => Run(() => { if (CreateOptionsDialog().ShowDialog() == true) Vm.Notify?.Invoke("Parâmetros da NFC-e salvos."); });
    private void Csc_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        var fiscal = Vm.Snapshot.Fiscal;
        var d = new FormDialog(Main, "Código de segurança • CSC", "Use o identificador e o token fornecidos pela SEFAZ. O token existente não é exibido; salvar substitui apenas o CSC do ambiente escolhido.");
        var environment = d.Choice("environment", "Ambiente do CSC", Environments, Environments.First(p => p.Value == fiscal.Options.Environment), nameof(FiscalChoice<FiscalEnvironment>.Label));
        var identifier = d.Text("id", "Identificador CSC • até 6 dígitos", fiscal.ActiveProfile.CscId); d.Password("token", "Token CSC").MaxLength = 256;
        environment.SelectionChanged += (_, _) => { if (environment.SelectedItem is FiscalChoice<FiscalEnvironment> choice) identifier.Text = (choice.Value == FiscalEnvironment.Producao ? fiscal.Production : fiscal.Homologation).CscId; };
        d.Submit = f => Vm.Service.SaveFiscalCsc(f.Selected<FiscalChoice<FiscalEnvironment>>("environment").Value, f.Value("id"), f.Value("token"));
        if (d.ShowDialog() == true) Vm.Notify?.Invoke("CSC protegido e salvo no ambiente escolhido.");
    });
    private void Certificate_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        var pick = new OpenFileDialog { Title = "Importar certificado digital A1", Filter = "Certificado A1|*.pfx;*.p12" };
        if (pick.ShowDialog(Main) != true) return;
        if (new FileInfo(pick.FileName).Length > 2_000_000) throw new InvalidOperationException("Escolha um certificado A1 de até 2 MB.");
        var d = new FormDialog(Main, "Importar certificado A1", "Arquivo, chave privada e senha serão protegidos pelo Windows. Para outro computador ou usuário do Windows, será necessário importar novamente.", "Importar");
        d.Info(Path.GetFileName(pick.FileName)); d.Password("password", "Senha do certificado A1");
        d.Submit = f => { byte[] bytes = File.ReadAllBytes(pick.FileName); try { Vm.Service.ImportFiscalCertificate(bytes, f.Value("password")); } finally { CryptographicOperations.ZeroMemory(bytes); } };
        if (d.ShowDialog() == true) Vm.Notify?.Invoke("Certificado A1 importado e protegido.");
    });
    private void RemoveCertificate_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        if (MessageBox.Show(Main, "Remover o certificado da configuração? O arquivo original não será alterado.", "Giro Bebidas", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) Vm.Service.RemoveFiscalCertificate();
    });
    private void RemoveCsc_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        if (MessageBox.Show(Main, $"Remover o CSC do ambiente {Vm.Snapshot.Fiscal.EnvironmentLabel}?", "Giro Bebidas", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) Vm.Service.RemoveFiscalCsc(Vm.Snapshot.Fiscal.Options.Environment);
    });
    internal void ShowChecks() { UpdateLayout(); ChecksCard.BringIntoView(); }
    private void Check_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!Vm.IsAdmin) throw new UnauthorizedAccessException("Esta ação exige um administrador.");
            Vm.BuildFiscalChecks();
            Vm.Notify?.Invoke("Conferência local atualizada. A autorização SEFAZ continua pendente de integração.");
            ShowChecks();
        }
        catch (Exception ex) { Vm.Notify?.Invoke(ex.Message); }
    }
}
