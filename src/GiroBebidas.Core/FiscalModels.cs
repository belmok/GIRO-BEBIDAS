namespace GiroBebidas.Core;

public enum FiscalEnvironment { Homologacao = 2, Producao = 1 }
public enum TaxRegime { SimplesNacional = 1, SimplesExcessoSublimite = 2, RegimeNormal = 3, MEI = 4 }

public sealed class FiscalConfiguration
{
    public FiscalIssuer Issuer { get; set; } = new();
    public FiscalOptions Options { get; set; } = new();
    public FiscalEnvironmentProfile Homologation { get; set; } = new();
    public FiscalEnvironmentProfile Production { get; set; } = new();
    public FiscalCertificate Certificate { get; set; } = new();
    public FiscalEnvironmentProfile ActiveProfile => Options.Environment == FiscalEnvironment.Producao ? Production : Homologation;
    public string EnvironmentLabel => Options.Environment == FiscalEnvironment.Producao ? "Produção" : "Homologação • testes";
    public const string IntegrationStatus = "Emissão SEFAZ ainda não integrada. As vendas geram comprovante não fiscal.";
}
public sealed class FiscalIssuer
{
    public string LegalName { get; set; } = "";
    public string TradeName { get; set; } = "";
    public string Cnpj { get; set; } = "";
    public string StateRegistration { get; set; } = "";
    public TaxRegime? Regime { get; set; }
    public string State { get; set; } = "";
    public string City { get; set; } = "";
    public string CityCode { get; set; } = "";
    public string Street { get; set; } = "";
    public string Number { get; set; } = "";
    public string Complement { get; set; } = "";
    public string District { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string Phone { get; set; } = "";
}
public sealed class FiscalOptions
{
    public FiscalEnvironment Environment { get; set; } = FiscalEnvironment.Homologacao;
    public string Terminal { get; set; } = "CAIXA-01";
    public Guid? PrinterId { get; set; }
}
public sealed class FiscalEnvironmentProfile
{
    public int Series { get; set; } = 1;
    public long NextNumber { get; set; } = 1;
    public string CscId { get; set; } = "";
    public string ProtectedCsc { get; set; } = "";
}
public sealed class FiscalCertificate
{
    public string ProtectedPfx { get; set; } = "";
    public string ProtectedPassword { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string Thumbprint { get; set; } = "";
    public DateTimeOffset? NotBefore { get; set; }
    public DateTimeOffset? NotAfter { get; set; }
    public bool IsImported => ProtectedPfx.Length > 0;
}
public sealed class ProductTaxData
{
    public string Ncm { get; set; } = "";
    public string Cest { get; set; } = "";
    public string Cfop { get; set; } = "";
    public string Csosn { get; set; } = "";
    public string Cst { get; set; } = "";
    public int? Origin { get; set; }
}
public record FiscalCheck(string Item, bool Complete, string Detail)
{
    public string Status => Complete ? "Conferido localmente" : "Pendente";
}
