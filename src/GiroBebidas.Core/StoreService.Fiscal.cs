using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace GiroBebidas.Core;

public sealed partial class StoreService
{
    public void SaveFiscalIssuer(FiscalIssuer issuer)
    {
        Store.Update(s => { var actor = Actor(s, true); FiscalValidation.ValidateIssuer(issuer); s.Fiscal.Issuer = issuer; Log(s, actor, "Dados do emitente fiscal atualizados"); return 0; });
    }
    public void SaveFiscalOptions(FiscalOptions options, int series, long nextNumber)
    {
        Store.Update(s =>
        {
            var actor = Actor(s, true);
            if (!Enum.IsDefined(options.Environment)) throw new InvalidOperationException("Ambiente fiscal inválido.");
            FiscalValidation.ValidateNumbers(series, nextNumber); options.Terminal = Required(options.Terminal, "a identificação do caixa", 40);
            if (options.PrinterId is not null && !s.Printers.Any(p => p.Id == options.PrinterId)) throw new InvalidOperationException("Impressora fiscal configurada não encontrada. Cadastre-a em Equipamentos.");
            s.Fiscal.Options = options; s.Fiscal.ActiveProfile.Series = series; s.Fiscal.ActiveProfile.NextNumber = nextNumber;
            Log(s, actor, "Ambiente, série e impressão fiscal atualizados"); return 0;
        });
    }
    public void SaveFiscalCsc(FiscalEnvironment environment, string identifier, string token)
    {
        Store.Update(s =>
        {
            var actor = Actor(s, true);
            if (!Enum.IsDefined(environment)) throw new InvalidOperationException("Ambiente fiscal inválido.");
            identifier = identifier.Trim();
            if (identifier.Length > 6 || !int.TryParse(identifier, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int id) || id is < 1 or > 999999)
                throw new InvalidOperationException("O identificador CSC deve ter até 6 dígitos, maior que zero.");
            token = token.Trim(); if (token.Length is < 1 or > 256 || token.Any(char.IsWhiteSpace)) throw new InvalidOperationException("Informe o token CSC sem espaços (até 256 caracteres).");
            var profile = environment == FiscalEnvironment.Producao ? s.Fiscal.Production : s.Fiscal.Homologation;
            profile.CscId = id.ToString("000000"); profile.ProtectedCsc = WindowsSecretProtector.ProtectString(token);
            Log(s, actor, environment == FiscalEnvironment.Producao ? "CSC de produção atualizado" : "CSC de homologação atualizado"); return 0;
        });
    }
    public void RemoveFiscalCsc(FiscalEnvironment environment)
    {
        Store.Update(s =>
        {
            var actor = Actor(s, true); if (!Enum.IsDefined(environment)) throw new InvalidOperationException("Ambiente fiscal inválido.");
            var profile = environment == FiscalEnvironment.Producao ? s.Fiscal.Production : s.Fiscal.Homologation;
            profile.CscId = ""; profile.ProtectedCsc = ""; Log(s, actor, "CSC removido da configuração fiscal"); return 0;
        });
    }
    public void ImportFiscalCertificate(byte[] pfx, string password)
    {
        Store.Update(s =>
        {
            var actor = Actor(s, true);
            if (pfx.Length is < 1 or > 2_000_000) throw new InvalidOperationException("Escolha um certificado A1 de até 2 MB.");
            X509Certificate2 certificate;
            try { certificate = new X509Certificate2(pfx, password, X509KeyStorageFlags.EphemeralKeySet); }
            catch (CryptographicException) { throw new InvalidOperationException("Não foi possível abrir o certificado. Confira o arquivo A1 e sua senha."); }
            using (certificate)
            {
                if (!certificate.HasPrivateKey) throw new InvalidOperationException("O certificado precisa conter a chave privada (A1 .pfx ou .p12).");
                if (DateTime.Now < certificate.NotBefore || DateTime.Now >= certificate.NotAfter) throw new InvalidOperationException("O certificado está vencido ou fora do período de validade.");
                s.Fiscal.Certificate = new()
                {
                    ProtectedPfx = WindowsSecretProtector.Protect(pfx), ProtectedPassword = WindowsSecretProtector.ProtectString(password),
                    Subject = certificate.Subject, Issuer = certificate.Issuer, Thumbprint = certificate.Thumbprint,
                    NotBefore = certificate.NotBefore, NotAfter = certificate.NotAfter
                };
                Log(s, actor, "Certificado A1 importado para a configuração fiscal"); return 0;
            }
        });
    }
    public void RemoveFiscalCertificate()
    {
        Store.Update(s => { var actor = Actor(s, true); s.Fiscal.Certificate = new(); Log(s, actor, "Certificado removido da configuração fiscal"); return 0; });
    }
    public List<FiscalCheck> CheckFiscalConfiguration()
    {
        var s = Snapshot; Actor(s, true); var fiscal = s.Fiscal; var profile = fiscal.ActiveProfile;
        bool readableCsc = false, readableCertificate = false;
        if (profile.ProtectedCsc.Length > 0)
        {
            try { readableCsc = WindowsSecretProtector.UnprotectString(profile.ProtectedCsc).Length > 0; } catch (InvalidOperationException) { }
        }
        if (fiscal.Certificate.IsImported)
        {
            byte[]? pfx = null;
            try
            {
                pfx = WindowsSecretProtector.Unprotect(fiscal.Certificate.ProtectedPfx);
                using var cert = new X509Certificate2(pfx, WindowsSecretProtector.UnprotectString(fiscal.Certificate.ProtectedPassword), X509KeyStorageFlags.EphemeralKeySet);
                readableCertificate = cert.HasPrivateKey && DateTime.Now >= cert.NotBefore && DateTime.Now < cert.NotAfter;
            }
            catch (Exception ex) when (ex is InvalidOperationException or CryptographicException) { }
            finally { if (pfx is not null) CryptographicOperations.ZeroMemory(pfx); }
        }
        int active = s.Products.Count(p => p.Active); int complete = s.Products.Count(p => p.Active && FiscalValidation.ProductComplete(p, fiscal.Issuer.Regime));
        return
        [
            new("Emitente", FiscalValidation.IssuerComplete(fiscal.Issuer), "Razão social, CNPJ, IE, CRT, UF, município/IBGE e endereço completo."),
            new("Série e numeração", profile.Series is >= 0 and <= 999 && profile.NextNumber is >= 1 and <= 999999999, $"{fiscal.EnvironmentLabel} • série {profile.Series} • próximo número {profile.NextNumber}. Não consumidos por vendas não fiscais."),
            new("Certificado A1", readableCertificate, readableCertificate ? "Arquivo com chave privada acessível e dentro da validade. Não confirma cadeia ICP-Brasil nem titularidade." : "Importe um A1 válido neste usuário do Windows. Após migrar de computador ou usuário, reimporte-o."),
            new("CSC do ambiente", readableCsc && profile.CscId.Length > 0, readableCsc ? $"Identificador {profile.CscId}. Token protegido. Validade junto à SEFAZ ainda precisa ser confirmada." : "Cadastre o identificador e token obtidos na SEFAZ para o ambiente selecionado."),
            new("Impressora", fiscal.Options.PrinterId is not null && s.Printers.Any(p => p.Id == fiscal.Options.PrinterId), "Escolha uma impressora cadastrada em Equipamentos. A impressão do DANFE exige documento autorizado."),
            new("Produtos", active > 0 && active == complete, $"{complete} de {active} produtos ativos com campos básicos preenchidos. Tributação e exigência de CEST dependem da operação."),
            new("Autorização SEFAZ", false, FiscalConfiguration.IntegrationStatus + " Credenciamento e integração do emissor ainda são necessários.")
        ];
    }
}
