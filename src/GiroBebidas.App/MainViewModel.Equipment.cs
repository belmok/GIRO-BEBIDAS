using GiroBebidas.Core;

namespace GiroBebidas.App;

public sealed partial class MainViewModel
{
    private EquipmentInventory equipmentInventory = EquipmentInventory.Unchecked;
    private readonly Dictionary<Guid, (string Signature, decimal Weight, DateTime At)> scaleReadings = [];
    public List<PrinterDeviceRow> PrinterRows { get; private set; } = [];
    public List<ScaleDeviceRow> ScaleRows { get; private set; } = [];
    public string PrintersEmpty => PrinterRows.Count == 0 ? "Visible" : "Collapsed";
    public string ScalesEmpty => ScaleRows.Count == 0 ? "Visible" : "Collapsed";
    public double PrinterRowsHeight => Math.Clamp(PrinterRows.Count, 2, 3) * 58 + 40;
    public double ScaleRowsHeight => Math.Clamp(ScaleRows.Count, 2, 3) * 58 + 40;
    private PrinterDeviceRow? selectedPrinterRow;
    private ScaleDeviceRow? selectedScaleRow;
    public PrinterDeviceRow? SelectedPrinterRow { get => selectedPrinterRow; set { if (Set(ref selectedPrinterRow, value)) { SelectedPrinter = value?.Profile; EquipmentSelectionChanged(); } } }
    public ScaleDeviceRow? SelectedScaleRow { get => selectedScaleRow; set { if (Set(ref selectedScaleRow, value)) { SelectedScale = value?.Profile; EquipmentSelectionChanged(); } } }
    private bool equipmentBusy;
    public bool EquipmentBusy { get => equipmentBusy; set { if (Set(ref equipmentBusy, value)) EquipmentSelectionChanged(); } }
    public bool CanEditPrinter => IsAdmin && SelectedPrinterRow is not null && !EquipmentBusy;
    public bool CanEditScale => IsAdmin && SelectedScaleRow is not null && !EquipmentBusy;
    public bool CanTestPrinter => CanEditPrinter;
    public bool CanTestScale => CanEditScale;
    internal void SetEquipmentInventory(EquipmentInventory inventory) { equipmentInventory = inventory; BuildEquipmentRows(); }
    internal void MarkScaleReading(ScaleProfile profile, decimal value)
    {
        scaleReadings[profile.Id] = (ScaleDeviceRow.Signature(profile), value, DateTime.Now); BuildEquipmentRows();
    }
    internal void ClearScaleReading(Guid id) { scaleReadings.Remove(id); BuildEquipmentRows(); }
    private void EquipmentSelectionChanged()
    {
        foreach (string name in new[] { nameof(CanEditPrinter), nameof(CanEditScale), nameof(CanTestPrinter), nameof(CanTestScale) }) Changed(name);
    }
    private void BuildEquipmentRows()
    {
        Guid? printerId = SelectedPrinterRow?.Id, scaleId = SelectedScaleRow?.Id;
        PrinterRows = Printers.Select(p => new PrinterDeviceRow(p, Snapshot.Settings.DefaultPrinterId == p.Id,
            !equipmentInventory.PrintersChecked ? "Não verificada" : equipmentInventory.HasPrinter(p.QueueName) ? "Disponível no Windows" : "Não encontrada",
            !equipmentInventory.PrintersChecked ? "Neutral" : equipmentInventory.HasPrinter(p.QueueName) ? "Success" : "Warning")).ToList();
        ScaleRows = Scales.Select(p =>
        {
            bool read = scaleReadings.TryGetValue(p.Id, out var result) && result.Signature == ScaleDeviceRow.Signature(p);
            return new ScaleDeviceRow(p, Snapshot.Settings.DefaultScaleId == p.Id,
                read ? "Leitura confirmada" : !equipmentInventory.PortsChecked ? "Não verificada" : equipmentInventory.HasPort(p.Port) ? "Porta disponível" : "Porta não detectada",
                read || equipmentInventory.PortsChecked && equipmentInventory.HasPort(p.Port) ? "Success" : equipmentInventory.PortsChecked ? "Warning" : "Neutral",
                read ? $"Último teste: {result.At:HH:mm} • {result.Weight:0.000} kg. A leitura é confirmada no momento do teste." : "A presença da porta não confirma a conexão da balança. Use Testar leitura.");
        }).ToList();
        foreach (string name in new[] { nameof(PrinterRows), nameof(ScaleRows), nameof(PrintersEmpty), nameof(ScalesEmpty), nameof(PrinterRowsHeight), nameof(ScaleRowsHeight) }) Changed(name);
        SelectedPrinterRow = PrinterRows.FirstOrDefault(p => p.Id == printerId); SelectedScaleRow = ScaleRows.FirstOrDefault(p => p.Id == scaleId);
        EquipmentSelectionChanged();
    }
}
