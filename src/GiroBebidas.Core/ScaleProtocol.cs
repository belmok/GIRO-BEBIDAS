using System.Globalization;
using System.Text.RegularExpressions;

namespace GiroBebidas.Core;

public static class ScaleProtocol
{
    public static decimal Parse(string frame, string protocol)
    {
        if (frame.Contains("US", StringComparison.OrdinalIgnoreCase) || frame.Contains("OL", StringComparison.OrdinalIgnoreCase) || frame.Contains("unstable", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A balança indica peso instável ou sobrecarga.");
        string clean = new(frame.Where(c => !char.IsControl(c)).ToArray());
        string pattern = protocol == "Inteiro em gramas" ? @"(?<![\d.,])[-+]?\d{1,9}(?![\d.,])" : @"[-+]?\d+[.,]\d{1,3}(?!\d)";
        var matches = Regex.Matches(clean, pattern);
        if (matches.Count != 1) throw new InvalidOperationException("A leitura não corresponde ao protocolo selecionado. Confira o manual da balança.");
        if (!decimal.TryParse(matches[0].Value.Replace(',', '.'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal value)) throw new InvalidOperationException("Peso inválido.");
        if (protocol == "Inteiro em gramas") value /= 1000;
        if (value <= 0 || value > 1_000_000) throw new InvalidOperationException("A balança deve indicar um peso positivo.");
        return value;
    }
}
