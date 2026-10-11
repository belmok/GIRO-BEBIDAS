using System.Text.RegularExpressions;

namespace GiroBebidas.Core;

public static class FiscalValidation
{
    public static readonly Dictionary<string, string> States = new()
    {
        ["AC"] = "12", ["AL"] = "27", ["AP"] = "16", ["AM"] = "13", ["BA"] = "29", ["CE"] = "23", ["DF"] = "53", ["ES"] = "32", ["GO"] = "52",
        ["MA"] = "21", ["MT"] = "51", ["MS"] = "50", ["MG"] = "31", ["PA"] = "15", ["PB"] = "25", ["PR"] = "41", ["PE"] = "26", ["PI"] = "22",
        ["RJ"] = "33", ["RN"] = "24", ["RS"] = "43", ["RO"] = "11", ["RR"] = "14", ["SC"] = "42", ["SP"] = "35", ["SE"] = "28", ["TO"] = "17"
    };
    public static readonly string[] CsosnCodes = ["101", "102", "103", "201", "202", "203", "300", "400", "500", "900"];
    public static readonly string[] CstCodes = ["00", "02", "10", "15", "20", "30", "40", "41", "50", "51", "53", "60", "61", "70", "90"];
    public static string NormalizeDocument(string value) => Regex.Replace(value.Trim().ToUpperInvariant(), "[. /-]", "");
    public static bool IsValidCnpj(string value)
    {
        string cnpj = NormalizeDocument(value);
        if (!Regex.IsMatch(cnpj, "^[A-Z0-9]{12}[0-9]{2}$") || cnpj.All(c => c == cnpj[0])) return false;
        // Receita Federal: character ASCII value minus 48, including letters in the twelve-position base.
        int Digit(string part)
        {
            int sum = 0, weight = 2;
            for (int i = part.Length - 1; i >= 0; i--) { sum += (part[i] - 48) * weight; weight = weight == 9 ? 2 : weight + 1; }
            int remainder = sum % 11; return remainder < 2 ? 0 : 11 - remainder;
        }
        return cnpj[12] - '0' == Digit(cnpj[..12]) && cnpj[13] - '0' == Digit(cnpj[..13]);
    }
    private static void Text(string value, int maximum, string label)
    {
        if (value.Length > maximum) throw new InvalidOperationException($"{label}: use até {maximum} caracteres.");
    }
    public static void ValidateIssuer(FiscalIssuer issuer)
    {
        if (issuer is null) throw new InvalidOperationException("Dados fiscais do emitente ausentes.");
        issuer.LegalName = issuer.LegalName.Trim(); issuer.TradeName = issuer.TradeName.Trim(); issuer.Cnpj = NormalizeDocument(issuer.Cnpj);
        issuer.StateRegistration = issuer.StateRegistration.Trim().ToUpperInvariant(); issuer.State = issuer.State.Trim().ToUpperInvariant();
        issuer.City = issuer.City.Trim(); issuer.CityCode = issuer.CityCode.Trim(); issuer.Street = issuer.Street.Trim(); issuer.Number = issuer.Number.Trim();
        issuer.Complement = issuer.Complement.Trim(); issuer.District = issuer.District.Trim(); issuer.PostalCode = NormalizeDocument(issuer.PostalCode); issuer.Phone = issuer.Phone.Trim();
        Text(issuer.LegalName, 120, "Razão social"); Text(issuer.TradeName, 120, "Nome fantasia"); Text(issuer.StateRegistration, 20, "Inscrição estadual");
        Text(issuer.City, 60, "Município"); Text(issuer.Street, 100, "Logradouro"); Text(issuer.Number, 20, "Número"); Text(issuer.Complement, 60, "Complemento"); Text(issuer.District, 60, "Bairro"); Text(issuer.Phone, 20, "Telefone");
        if (issuer.Cnpj.Length > 0 && !IsValidCnpj(issuer.Cnpj)) throw new InvalidOperationException("CNPJ inválido. Confira os 14 caracteres e os dígitos verificadores.");
        if (issuer.Regime is not null && !Enum.IsDefined(issuer.Regime.Value)) throw new InvalidOperationException("Regime tributário inválido.");
        if (issuer.State.Length > 0 && !States.ContainsKey(issuer.State)) throw new InvalidOperationException("Selecione uma UF válida.");
        if (issuer.PostalCode.Length > 0 && !Regex.IsMatch(issuer.PostalCode, "^[0-9]{8}$")) throw new InvalidOperationException("O CEP deve ter 8 dígitos.");
        if (issuer.CityCode.Length > 0 && (!Regex.IsMatch(issuer.CityCode, "^[0-9]{7}$") || (States.TryGetValue(issuer.State, out var prefix) && !issuer.CityCode.StartsWith(prefix))))
            throw new InvalidOperationException("Informe o código IBGE do município com 7 dígitos, correspondente à UF.");
    }
    public static bool IssuerComplete(FiscalIssuer issuer) => issuer.LegalName.Length > 0 && IsValidCnpj(issuer.Cnpj) && issuer.StateRegistration.Length > 0
        && issuer.Regime is not null && States.ContainsKey(issuer.State) && issuer.City.Length > 0 && Regex.IsMatch(issuer.CityCode, "^[0-9]{7}$")
        && issuer.Street.Length > 0 && issuer.Number.Length > 0 && issuer.District.Length > 0 && Regex.IsMatch(issuer.PostalCode, "^[0-9]{8}$");
    public static void ValidateNumbers(int series, long nextNumber)
    {
        if (series is < 0 or > 999) throw new InvalidOperationException("A série deve estar entre 0 e 999.");
        if (nextNumber is < 1 or > 999_999_999) throw new InvalidOperationException("O próximo número deve estar entre 1 e 999.999.999.");
    }
    public static void ValidateProduct(ProductTaxData tax)
    {
        if (tax is null) throw new InvalidOperationException("Cadastro fiscal do produto ausente.");
        tax.Ncm = NormalizeDocument(tax.Ncm); tax.Cest = NormalizeDocument(tax.Cest); tax.Cfop = tax.Cfop.Trim(); tax.Csosn = tax.Csosn.Trim(); tax.Cst = tax.Cst.Trim();
        if (tax.Ncm.Length > 0 && !Regex.IsMatch(tax.Ncm, "^[0-9]{8}$")) throw new InvalidOperationException("NCM deve ter 8 dígitos.");
        if (tax.Cest.Length > 0 && !Regex.IsMatch(tax.Cest, "^[0-9]{7}$")) throw new InvalidOperationException("CEST deve ter 7 dígitos.");
        if (tax.Cfop.Length > 0 && !Regex.IsMatch(tax.Cfop, "^[567][0-9]{3}$")) throw new InvalidOperationException("CFOP de saída deve ter 4 dígitos e começar com 5, 6 ou 7.");
        if (tax.Csosn.Length > 0 && !CsosnCodes.Contains(tax.Csosn)) throw new InvalidOperationException("CSOSN inválido.");
        if (tax.Cst.Length > 0 && !CstCodes.Contains(tax.Cst)) throw new InvalidOperationException("CST de ICMS inválido.");
        if (tax.Cst.Length > 0 && tax.Csosn.Length > 0) throw new InvalidOperationException("Informe CSOSN ou CST de ICMS conforme o regime da empresa.");
        if (tax.Origin is < 0 or > 8) throw new InvalidOperationException("Origem deve estar entre 0 e 8.");
    }
    public static bool ProductComplete(Product p, TaxRegime? regime) => p.Tax.Ncm.Length == 8 && p.Tax.Cfop.Length == 4 && p.Tax.Origin is not null
        && ((regime is TaxRegime.SimplesNacional or TaxRegime.MEI) ? p.Tax.Csosn.Length > 0 : p.Tax.Cst.Length > 0);
}
