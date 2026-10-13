using GiroBebidas.Core;

namespace GiroBebidas.App;

public sealed record EquipmentInventory(IReadOnlyList<string> PrinterQueues, IReadOnlyList<string> SerialPorts, bool PrintersChecked = true, bool PortsChecked = true)
{
    public static EquipmentInventory Unchecked { get; } = new([], [], false, false);
    public static EquipmentInventory Read()
    {
        List<string> printers = [], ports = []; bool printerChecked = true, portsChecked = true;
        try { printers = ReceiptPrinter.InstalledPrinters(); } catch { printerChecked = false; }
        try { ports = SerialScaleReader.Ports(); } catch { portsChecked = false; }
        return new(printers, ports, printerChecked, portsChecked);
    }
    public bool HasPrinter(string queue) => PrinterQueues.Any(name => name.Equals(queue, StringComparison.OrdinalIgnoreCase) || name.Split('\\').Last().Equals(queue, StringComparison.OrdinalIgnoreCase));
    public bool HasPort(string port) => SerialPorts.Contains(port, StringComparer.OrdinalIgnoreCase);
}
public sealed record PrinterDeviceRow(PrinterProfile Profile, bool IsDefault, string Status, string StatusTone)
{
    public Guid Id => Profile.Id;
    public string Name => Profile.Name;
    public string QueueName => Profile.QueueName;
    public string Paper => Profile.Paper;
    public string StatusHint => "A disponibilidade indica a fila detectada no Windows. Use Testar impressão para conferir o equipamento.";
}
public sealed record ScaleDeviceRow(ScaleProfile Profile, bool IsDefault, string Status, string StatusTone, string StatusHint)
{
    public Guid Id => Profile.Id;
    public string Name => Profile.Name;
    public string Port => Profile.Port;
    public int BaudRate => Profile.BaudRate;
    public string Protocol => Profile.Protocol;
    public static string Signature(ScaleProfile p) => $"{p.Port}|{p.BaudRate}|{p.Protocol}|{p.CommandHex}";
}
