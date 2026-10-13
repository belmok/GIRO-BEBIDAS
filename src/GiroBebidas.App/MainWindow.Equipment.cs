using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using GiroBebidas.Core;

namespace GiroBebidas.App;

public partial class MainWindow
{
    internal Func<EquipmentInventory> EquipmentInventoryReader { get; set; } = EquipmentInventory.Read;
    internal Func<ScaleProfile, Task<decimal>> EquipmentScaleRead { get; set; } = SerialScaleReader.ReadAsync;
    internal Action<Window, PrinterProfile> EquipmentPrinterTest { get; set; } = ReceiptPrinter.PrintTest;
    internal Action EquipmentWindowsSettings { get; set; } = () => Process.Start(new ProcessStartInfo("ms-settings:printers") { UseShellExecute = true });
    private void RequireEquipmentAdmin()
    {
        if (!Vm.Service.IsAdmin) throw new UnauthorizedAccessException("Esta ação exige um administrador.");
        if (Vm.EquipmentBusy) throw new InvalidOperationException("Aguarde o teste da balança terminar.");
    }
    private bool RefreshEquipmentInventory()
    {
        if (!Vm.IsAdmin) return false;
        try
        {
            var inventory = EquipmentInventoryReader(); Vm.SetEquipmentInventory(inventory);
            if (inventory.PrintersChecked && inventory.PortsChecked) return true;
            Notify("Não foi possível conferir todas as filas e portas do Windows. A disponibilidade não verificada aparece em cinza."); return false;
        }
        catch (Exception ex) { Vm.SetEquipmentInventory(EquipmentInventory.Unchecked); Notify("Não foi possível conferir os equipamentos: " + ex.Message); return false; }
    }
    private void RefreshEquipment_Click(object sender, RoutedEventArgs e)
    {
        try { RequireEquipmentAdmin(); if (RefreshEquipmentInventory()) Notify("Filas de impressão e portas seriais conferidas no Windows. Use os testes para verificar os equipamentos."); }
        catch (Exception ex) { Notify(ex.Message); }
    }
    private void RunEquipment(Func<bool> action, string? success = null)
    {
        try { RequireEquipmentAdmin(); if (!action()) return; Vm.Refresh(); RefreshEquipmentInventory(); if (success is not null) Notify(success); }
        catch (Exception ex) { Notify(ex.Message); }
    }
    private static void CloseDeviceMenu(object sender)
    {
        if (sender is MenuItem item && ItemsControl.ItemsControlFromItemContainer(item) is ContextMenu menu) menu.IsOpen = false;
    }
    private PrinterDeviceRow PrinterRow(object sender)
    {
        Guid id = sender is FrameworkElement { DataContext: PrinterDeviceRow row } ? row.Id : Require(Vm.SelectedPrinterRow, "uma impressora").Id;
        var profile = Vm.Service.Snapshot.Printers.FirstOrDefault(p => p.Id == id) ?? throw new InvalidOperationException("Esta impressora não está mais cadastrada.");
        var current = Vm.PrinterRows.FirstOrDefault(p => p.Id == id) ?? new(profile, false, "Não verificada", "Neutral");
        Vm.SelectedPrinterRow = current; CloseDeviceMenu(sender); return current with { Profile = profile };
    }
    private ScaleDeviceRow ScaleRow(object sender)
    {
        Guid id = sender is FrameworkElement { DataContext: ScaleDeviceRow row } ? row.Id : Require(Vm.SelectedScaleRow, "uma balança").Id;
        var profile = Vm.Service.Snapshot.Scales.FirstOrDefault(p => p.Id == id) ?? throw new InvalidOperationException("Esta balança não está mais cadastrada.");
        var current = Vm.ScaleRows.FirstOrDefault(p => p.Id == id) ?? new(profile, false, "Não verificada", "Neutral", "");
        Vm.SelectedScaleRow = current; CloseDeviceMenu(sender); return current with { Profile = profile };
    }
    private void PrinterRowMenu_Click(object sender, RoutedEventArgs e) => OpenDeviceMenu(sender, true);
    private void ScaleRowMenu_Click(object sender, RoutedEventArgs e) => OpenDeviceMenu(sender, false);
    private void OpenDeviceMenu(object sender, bool printer)
    {
        try
        {
            RequireEquipmentAdmin(); if (printer) _ = PrinterRow(sender); else _ = ScaleRow(sender);
            var button = (Button)sender; var menu = button.ContextMenu ?? throw new InvalidOperationException("Menu indisponível.");
            menu.PlacementTarget = button; menu.Placement = PlacementMode.Bottom; menu.HorizontalOffset = button.ActualWidth - menu.Width;
            menu.VerticalOffset = 4; menu.IsOpen = true;
        }
        catch (Exception ex) { Notify(ex.Message); }
    }
    internal FormDialog CreatePrinterDialog(PrinterProfile profile)
    {
        RequireEquipmentAdmin();
        var inventory = EquipmentInventoryReader(); var printers = inventory.PrinterQueues.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        bool existing = Vm.Service.Snapshot.Printers.Any(p => p.Id == profile.Id);
        if (printers.Count == 0 && !existing) throw new InvalidOperationException("Nenhuma impressora foi encontrada. Adicione a impressora nas configurações do Windows e tente novamente.");
        if (existing && !printers.Contains(profile.QueueName, StringComparer.OrdinalIgnoreCase)) printers.Add(profile.QueueName);
        var dialog = new FormDialog(this, existing ? "Editar impressora" : "Cadastrar impressora", "Escolha a fila instalada no Windows e o formato do papel. O padrão do PDV é separado da impressora fiscal.");
        dialog.Text("name", "Nome para identificar", profile.Name);
        dialog.Choice("queue", "Impressora instalada no Windows", printers, profile.QueueName);
        dialog.Choice("paper", "Formato do cupom", new[] { "58 mm", "80 mm", "A4" }, profile.Paper);
        dialog.Check("default", "Usar como impressora padrão do PDV", Vm.Snapshot.Settings.DefaultPrinterId == profile.Id || Vm.Printers.Count == 0);
        if (!inventory.HasPrinter(profile.QueueName) && existing) dialog.Info("A fila cadastrada não foi detectada no Windows. Você pode atualizar o cadastro ou conferir a instalação em Impressoras do Windows.");
        dialog.Submit = form => Vm.Service.SavePrinter(new() { Id = profile.Id, Name = form.Value("name"), QueueName = form.Value("queue"), Paper = form.Value("paper") }, form.Checked("default"));
        return dialog;
    }
    private void NewPrinter_Click(object sender, RoutedEventArgs e) => RunEquipment(() => CreatePrinterDialog(new()).ShowDialog() == true, "Impressora cadastrada.");
    private void EditPrinter_Click(object sender, RoutedEventArgs e) => RunEquipment(() => CreatePrinterDialog(PrinterRow(sender).Profile).ShowDialog() == true, "Impressora atualizada.");
    internal ConfirmationWindow CreateDeletePrinterConfirmation(PrinterDeviceRow row) => new(this, "Remover impressora", $"Remover {row.Name} do cadastro do PDV?", "Remover")
    { NoticeText = "O driver do Windows permanece instalado. Se essa impressora for o padrão do PDV ou da configuração fiscal, será necessário escolher outra." };
    private void DeletePrinter_Click(object sender, RoutedEventArgs e) => RunEquipment(() =>
    {
        var row = PrinterRow(sender); if (CreateDeletePrinterConfirmation(row).ShowDialog() != true) return false;
        Vm.Service.DeleteDevice(row.Id, true); return true;
    }, "Impressora removida do PDV.");
    private void DefaultPrinter_Click(object sender, RoutedEventArgs e) => RunEquipment(() => { Vm.Service.SetDefaultPrinter(PrinterRow(sender).Id); return true; }, "Impressora padrão do PDV definida.");
    private void WindowsPrinters_Click(object sender, RoutedEventArgs e)
    {
        try { RequireEquipmentAdmin(); EquipmentWindowsSettings(); } catch (Exception ex) { Notify(ex.Message); }
    }
    private void TestPrinter_Click(object sender, RoutedEventArgs e)
    {
        try { RequireEquipmentAdmin(); var row = PrinterRow(sender); EquipmentPrinterTest(this, row.Profile); Notify($"Página de teste enviada para {row.Name}. Confira a impressão no equipamento."); }
        catch (Exception ex) { Notify(ex.Message); }
    }
    internal FormDialog CreateScaleDialog(ScaleProfile profile)
    {
        RequireEquipmentAdmin(); bool existing = Vm.Service.Snapshot.Scales.Any(p => p.Id == profile.Id);
        var dialog = new FormDialog(this, existing ? "Editar balança" : "Cadastrar balança", "Conexão serial 8N1. Confira os parâmetros no manual. USB precisa disponibilizar uma porta COM compatível.");
        dialog.Text("name", "Nome para identificar", profile.Name);
        dialog.Info("Portas detectadas: " + string.Join(", ", EquipmentInventoryReader().SerialPorts.DefaultIfEmpty("nenhuma")));
        dialog.Text("port", "Porta serial", profile.Port);
        dialog.Choice("baud", "Velocidade (baud)", new[] { 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200 }, profile.BaudRate);
        dialog.Choice("protocol", "Formato de leitura", new[] { "Texto em kg", "Inteiro em gramas" }, profile.Protocol);
        dialog.Text("command", "Comando de solicitação em hexadecimal • opcional", profile.CommandHex);
        dialog.Check("default", "Usar como balança padrão", Vm.Snapshot.Settings.DefaultScaleId == profile.Id || Vm.Scales.Count == 0);
        dialog.Submit = form => Vm.Service.SaveScale(new() { Id = profile.Id, Name = form.Value("name"), Port = form.Value("port"), BaudRate = form.Selected<int>("baud"), Protocol = form.Value("protocol"), CommandHex = form.Value("command") }, form.Checked("default"));
        return dialog;
    }
    private void NewScale_Click(object sender, RoutedEventArgs e) => RunEquipment(() => CreateScaleDialog(new()).ShowDialog() == true, "Balança cadastrada. Use Testar leitura para conferir o equipamento.");
    private void EditScale_Click(object sender, RoutedEventArgs e) => RunEquipment(() => CreateScaleDialog(ScaleRow(sender).Profile).ShowDialog() == true, "Balança atualizada.");
    internal ConfirmationWindow CreateDeleteScaleConfirmation(ScaleDeviceRow row) => new(this, "Remover balança", $"Remover {row.Name} do cadastro do PDV?", "Remover")
    { NoticeText = "A porta e os drivers do Windows permanecem disponíveis. Se essa for a balança padrão, a pesagem volta a ser manual." };
    private void DeleteScale_Click(object sender, RoutedEventArgs e) => RunEquipment(() =>
    {
        var row = ScaleRow(sender); if (CreateDeleteScaleConfirmation(row).ShowDialog() != true) return false;
        Vm.Service.DeleteDevice(row.Id, false); Vm.ClearScaleReading(row.Id); return true;
    }, "Balança removida do PDV.");
    private void DefaultScale_Click(object sender, RoutedEventArgs e) => RunEquipment(() => { Vm.Service.SetDefaultScale(ScaleRow(sender).Id); return true; }, "Balança padrão definida.");
    private async void TestScale_Click(object sender, RoutedEventArgs e)
    {
        ScaleProfile? profile = null; bool started = false;
        try
        {
            RequireEquipmentAdmin(); profile = ScaleRow(sender).Profile; Vm.EquipmentBusy = true; started = true;
            decimal weight = await EquipmentScaleRead(profile);
            if (weight <= 0 || weight > 1_000_000) throw new InvalidOperationException("A balança deve indicar um peso positivo dentro do limite de leitura.");
            Vm.MarkScaleReading(profile, weight);
            Notify($"Leitura de {profile.Name}: {weight:0.000} kg.");
        }
        catch (Exception ex) { if (profile is not null) Vm.ClearScaleReading(profile.Id); Notify(ex.Message); }
        finally { if (started) Vm.EquipmentBusy = false; }
    }
}
