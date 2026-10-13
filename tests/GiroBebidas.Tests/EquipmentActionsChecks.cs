using System.Text.Json;
using GiroBebidas.Core;

namespace GiroBebidas.Tests;

public static class EquipmentActionsChecks
{
    public static void Run(string root, Action<bool, string> check)
    {
        string directory = Path.Combine(root, "acoes-equipamentos", Guid.NewGuid().ToString("N"));
        var service = new StoreService(new JsonStore(directory));
        service.Login("ADMIN", "ADMIN");
        static string Data<T>(T value) => JsonSerializer.Serialize(value, JsonStore.JsonOptions);
        string State() => Data(service.Snapshot);
        void RejectAtomic<T>(Action action, string name) where T : Exception
        {
            string beforeState = State(), beforeFile = File.ReadAllText(service.Store.FilePath);
            bool rejected = false;
            try { action(); } catch (T) { rejected = true; }
            check(rejected, name);
            check(State() == beforeState && File.ReadAllText(service.Store.FilePath) == beforeFile,
                name + ": cadastro, configurações, auditoria e arquivo preservados");
        }

        check(service.Snapshot.Printers.Count == 0 && service.Snapshot.Scales.Count == 0
            && service.Snapshot.Settings.DefaultPrinterId is null && service.Snapshot.Settings.DefaultScaleId is null,
            "Equipamentos iniciam vazios, sem impressora padrão e com pesagem manual");
        var printer1 = new PrinterProfile { Name = " Frente de caixa ", QueueName = " Fila de teste 1 ", Paper = "80 mm" };
        var printer2 = new PrinterProfile { Name = "Cozinha", QueueName = "Fila de teste 2", Paper = "58 mm" };
        var printer3 = new PrinterProfile { Name = new string('P', 80), QueueName = new string('F', 180), Paper = "A4" };
        service.SavePrinter(printer1, false); service.SavePrinter(printer2, false); service.SavePrinter(printer3, false);
        check(service.Snapshot.Printers.Count == 3 && service.Snapshot.Settings.DefaultPrinterId == printer1.Id,
            "Primeira impressora torna-se padrão e novos cadastros preservam a escolha");
        check(service.Snapshot.Printers.Single(p => p.Id == printer1.Id).Name == "Frente de caixa"
            && service.Snapshot.Printers.Single(p => p.Id == printer1.Id).QueueName == "Fila de teste 1",
            "Cadastro normaliza os espaços do nome e da fila da impressora");
        check(service.Snapshot.Printers.Select(p => p.Paper).SequenceEqual(new[] { "80 mm", "58 mm", "A4" })
            && service.Snapshot.Printers.Single(p => p.Id == printer3.Id).Name.Length == 80
            && service.Snapshot.Printers.Single(p => p.Id == printer3.Id).QueueName.Length == 180,
            "Papéis 58 mm, 80 mm e A4 e limites de nome e fila são aceitos");

        var scale1 = new ScaleProfile { Name = " Balança do balcão ", Port = " com3 ", CommandHex = "05 0d 0a" };
        var scale2 = new ScaleProfile { Name = "Balança de reserva", Port = "COM4", Protocol = "Inteiro em gramas", BaudRate = 115200 };
        var scale3 = new ScaleProfile { Name = new string('B', 80), Port = "COM9999", BaudRate = 1200, CommandHex = string.Concat(Enumerable.Repeat("05", 64)) };
        service.SaveScale(scale1, false); service.SaveScale(scale2, false); service.SaveScale(scale3, false);
        check(service.Snapshot.Scales.Count == 3 && service.Snapshot.Settings.DefaultScaleId == scale1.Id,
            "Primeira balança torna-se padrão e novos cadastros preservam a escolha");
        var normalizedScale = service.Snapshot.Scales.Single(p => p.Id == scale1.Id);
        check(normalizedScale.Name == "Balança do balcão" && normalizedScale.Port == "COM3" && normalizedScale.CommandHex == "050d0a",
            "Cadastro normaliza nome, porta COM e espaços do comando serial");
        check(service.Snapshot.Scales.Single(p => p.Id == scale2.Id).Protocol == "Inteiro em gramas"
            && service.Snapshot.Scales.Single(p => p.Id == scale2.Id).CommandHex == ""
            && service.Snapshot.Scales.Single(p => p.Id == scale3.Id).CommandHex.Length == 128,
            "Protocolos suportados, comando vazio e comando de 64 bytes são aceitos");

        var settings = service.Snapshot.Settings;
        settings.Name = "Depósito de testes de equipamentos"; settings.AutoPrint = true; settings.CashOpeningDefault = 125.75m;
        service.SaveSettings(settings);
        service.SaveFiscalOptions(new FiscalOptions { Environment = FiscalEnvironment.Producao, PrinterId = printer1.Id }, 7, 124);
        var beforePrinterDefault = service.Snapshot;
        service.SetDefaultPrinter(printer2.Id);
        var afterPrinterDefault = service.Snapshot;
        check(afterPrinterDefault.Settings.DefaultPrinterId == printer2.Id,
            "Definir impressora padrão seleciona o cadastro escolhido");
        check(afterPrinterDefault.Audit.Count == beforePrinterDefault.Audit.Count + 1
            && afterPrinterDefault.Audit[^1].UserName == "ADMIN" && afterPrinterDefault.Audit[^1].Action.Contains(printer2.Name),
            "Definir impressora padrão registra administrador e equipamento na auditoria");
        beforePrinterDefault.Settings.DefaultPrinterId = printer2.Id; beforePrinterDefault.Audit.Add(afterPrinterDefault.Audit[^1]);
        check(Data(beforePrinterDefault) == Data(afterPrinterDefault),
            "Definir impressora padrão preserva vínculo fiscal, balança, loja e todos os demais dados");

        var beforeScaleDefault = service.Snapshot;
        service.SetDefaultScale(scale2.Id);
        var afterScaleDefault = service.Snapshot;
        check(afterScaleDefault.Settings.DefaultScaleId == scale2.Id,
            "Definir balança padrão seleciona o cadastro escolhido");
        check(afterScaleDefault.Audit.Count == beforeScaleDefault.Audit.Count + 1
            && afterScaleDefault.Audit[^1].UserName == "ADMIN" && afterScaleDefault.Audit[^1].Action.Contains(scale2.Name),
            "Definir balança padrão registra administrador e equipamento na auditoria");
        beforeScaleDefault.Settings.DefaultScaleId = scale2.Id; beforeScaleDefault.Audit.Add(afterScaleDefault.Audit[^1]);
        check(Data(beforeScaleDefault) == Data(afterScaleDefault),
            "Definir balança padrão preserva impressora, vínculo fiscal, loja e todos os demais dados");

        var editedPrinter = service.Snapshot.Printers.Single(p => p.Id == printer2.Id);
        editedPrinter.Name = "Cozinha revisada"; editedPrinter.QueueName = "Fila revisada"; editedPrinter.Paper = "A4";
        service.SavePrinter(editedPrinter, false);
        check(service.Snapshot.Printers.Count == 3 && Data(service.Snapshot.Printers.Single(p => p.Id == printer2.Id)) == Data(editedPrinter)
            && service.Snapshot.Settings.DefaultPrinterId == printer2.Id && service.Snapshot.Fiscal.Options.PrinterId == printer1.Id,
            "Editar impressora substitui somente seu cadastro e preserva padrão e vínculo fiscal");
        var editedScale = service.Snapshot.Scales.Single(p => p.Id == scale2.Id);
        editedScale.Name = "Balança revisada"; editedScale.Port = "COM9"; editedScale.BaudRate = 38400; editedScale.Protocol = "Texto em kg"; editedScale.CommandHex = "0D0A";
        service.SaveScale(editedScale, false);
        check(service.Snapshot.Scales.Count == 3 && Data(service.Snapshot.Scales.Single(p => p.Id == scale2.Id)) == Data(editedScale)
            && service.Snapshot.Settings.DefaultScaleId == scale2.Id && service.Snapshot.Settings.DefaultPrinterId == printer2.Id,
            "Editar balança substitui somente seu cadastro e preserva os equipamentos padrão");

        (string Label, Action<PrinterProfile> Change)[] invalidPrinters =
        [
            ("nome vazio", p => p.Name = " "),
            ("nome acima de 80 caracteres", p => p.Name = new string('P', 81)),
            ("fila vazia", p => p.QueueName = " "),
            ("fila acima de 180 caracteres", p => p.QueueName = new string('F', 181)),
            ("papel incompatível", p => p.Paper = "100 mm")
        ];
        foreach (var invalid in invalidPrinters)
        {
            var invalidProfile = service.Snapshot.Printers.Single(p => p.Id == printer2.Id); invalid.Change(invalidProfile);
            RejectAtomic<InvalidOperationException>(() => service.SavePrinter(invalidProfile, true), $"Impressora com {invalid.Label} é recusada");
        }
        (string Label, Action<ScaleProfile> Change)[] invalidScales =
        [
            ("nome vazio", p => p.Name = " "),
            ("nome acima de 80 caracteres", p => p.Name = new string('B', 81)),
            ("porta COM0", p => p.Port = "COM0"),
            ("porta acima de COM9999", p => p.Port = "COM10000"),
            ("porta incompatível", p => p.Port = "USB1"),
            ("velocidade incompatível", p => p.BaudRate = 12345),
            ("protocolo incompatível", p => p.Protocol = "Protocolo não suportado"),
            ("comando com quantidade ímpar de caracteres", p => p.CommandHex = "0D0"),
            ("comando não hexadecimal", p => p.CommandHex = "GG"),
            ("comando acima de 64 bytes", p => p.CommandHex = string.Concat(Enumerable.Repeat("05", 65)))
        ];
        foreach (var invalid in invalidScales)
        {
            var invalidProfile = service.Snapshot.Scales.Single(p => p.Id == scale2.Id); invalid.Change(invalidProfile);
            RejectAtomic<InvalidOperationException>(() => service.SaveScale(invalidProfile, true), $"Balança com {invalid.Label} é recusada");
        }
        Guid missing = Guid.NewGuid();
        RejectAtomic<InvalidOperationException>(() => service.SetDefaultPrinter(missing), "Impressora inexistente não pode ser padrão");
        RejectAtomic<InvalidOperationException>(() => service.SetDefaultScale(missing), "Balança inexistente não pode ser padrão");
        RejectAtomic<InvalidOperationException>(() => service.SetDefaultPrinter(scale1.Id), "Cadastro de balança não pode ser definido como impressora padrão");
        RejectAtomic<InvalidOperationException>(() => service.SetDefaultScale(printer1.Id), "Cadastro de impressora não pode ser definido como balança padrão");
        RejectAtomic<InvalidOperationException>(() => service.DeleteDevice(missing, true), "Remover impressora inexistente é recusado");
        RejectAtomic<InvalidOperationException>(() => service.DeleteDevice(missing, false), "Remover balança inexistente é recusado");
        RejectAtomic<InvalidOperationException>(() => service.DeleteDevice(scale1.Id, true), "Remover balança como impressora é recusado");
        RejectAtomic<InvalidOperationException>(() => service.DeleteDevice(printer1.Id, false), "Remover impressora como balança é recusado");

        string persistedState = State();
        check(Data(new JsonStore(directory).Snapshot()) == persistedState,
            "Cadastros, padrões, vínculos fiscais e ajustes editados permanecem após reabrir os dados");
        string backup = Path.Combine(directory, "backup-equipamentos.json"); service.ExportBackup(backup);
        service.SetDefaultPrinter(printer1.Id); service.SetDefaultScale(scale1.Id);
        service.DeleteDevice(printer3.Id, true); service.DeleteDevice(scale3.Id, false);
        service.RestoreBackup(backup);
        check(State() == persistedState && Data(new JsonStore(directory).Snapshot()) == persistedState,
            "Backup restaura todos os equipamentos, padrões e vínculo fiscal sem perda de configurações");
        check(service.CurrentUserId is null && Directory.GetFiles(directory, "antes-restauracao-*.json").Length > 0,
            "Restaurar backup de equipamentos encerra a sessão e preserva uma cópia anterior");
        service.Login("ADMIN", "ADMIN");

        var beforeDeletePrinter = service.Snapshot;
        service.DeleteDevice(printer3.Id, true);
        var afterDeletePrinter = service.Snapshot;
        beforeDeletePrinter.Printers.RemoveAll(p => p.Id == printer3.Id); beforeDeletePrinter.Audit.Add(afterDeletePrinter.Audit[^1]);
        check(afterDeletePrinter.Audit.Count == beforeDeletePrinter.Audit.Count && Data(beforeDeletePrinter) == Data(afterDeletePrinter),
            "Remover impressora não padrão preserva os demais cadastros, padrões e vínculo fiscal");
        var beforeDeleteScale = service.Snapshot;
        service.DeleteDevice(scale3.Id, false);
        var afterDeleteScale = service.Snapshot;
        beforeDeleteScale.Scales.RemoveAll(p => p.Id == scale3.Id); beforeDeleteScale.Audit.Add(afterDeleteScale.Audit[^1]);
        check(afterDeleteScale.Audit.Count == beforeDeleteScale.Audit.Count && Data(beforeDeleteScale) == Data(afterDeleteScale),
            "Remover balança não padrão preserva os demais cadastros, padrões e vínculo fiscal");
        service.DeleteDevice(printer1.Id, true);
        check(service.Snapshot.Fiscal.Options.PrinterId is null && service.Snapshot.Settings.DefaultPrinterId == printer2.Id
            && service.Snapshot.Printers.Count == 1 && service.Snapshot.Scales.Count == 2,
            "Remover impressora fiscal limpa somente seu vínculo e preserva a impressora padrão diferente");
        service.SaveFiscalOptions(new FiscalOptions { Environment = FiscalEnvironment.Producao, PrinterId = printer2.Id }, 7, 124);
        service.DeleteDevice(printer2.Id, true);
        check(service.Snapshot.Settings.DefaultPrinterId is null && service.Snapshot.Fiscal.Options.PrinterId is null
            && service.Snapshot.Printers.Count == 0 && service.Snapshot.Settings.DefaultScaleId == scale2.Id,
            "Remover impressora padrão fiscal limpa ambos os vínculos e preserva a balança padrão");
        service.DeleteDevice(scale1.Id, false);
        check(service.Snapshot.Settings.DefaultScaleId == scale2.Id && service.Snapshot.Scales.Count == 1,
            "Remover balança não padrão preserva a balança escolhida");
        service.DeleteDevice(scale2.Id, false);
        check(service.Snapshot.Settings.DefaultScaleId is null && service.Snapshot.Scales.Count == 0,
            "Remover a balança padrão volta para pesagem manual");
        check(Data(new JsonStore(directory).Snapshot()) == State(), "Remoções e limpeza dos vínculos permanecem após reabrir os dados");

        service.SavePrinter(printer1, true); service.SaveScale(scale1, true);
        service.SaveUser(new UserAccount { Name = "Operador de equipamentos", Username = "EQUIPAMENTOS_OPERADOR", Role = UserRole.Operador }, "1234");
        var restricted = new StoreService(service.Store); restricted.Login("EQUIPAMENTOS_OPERADOR", "1234");
        RejectAtomic<UnauthorizedAccessException>(() => restricted.SavePrinter(new() { Name = "Outra", QueueName = "Outra fila" }, true), "Operador não cadastra impressora");
        RejectAtomic<UnauthorizedAccessException>(() => restricted.SaveScale(new() { Name = "Outra balança", Port = "COM2" }, true), "Operador não cadastra balança");
        RejectAtomic<UnauthorizedAccessException>(() => restricted.SavePrinter(printer1, false), "Operador não edita impressora");
        RejectAtomic<UnauthorizedAccessException>(() => restricted.SaveScale(scale1, false), "Operador não edita balança");
        RejectAtomic<UnauthorizedAccessException>(() => restricted.SetDefaultPrinter(printer1.Id), "Operador não define impressora padrão");
        RejectAtomic<UnauthorizedAccessException>(() => restricted.SetDefaultScale(scale1.Id), "Operador não define balança padrão");
        RejectAtomic<UnauthorizedAccessException>(() => restricted.DeleteDevice(printer1.Id, true), "Operador não remove impressora");
        RejectAtomic<UnauthorizedAccessException>(() => restricted.DeleteDevice(scale1.Id, false), "Operador não remove balança");
        RejectAtomic<UnauthorizedAccessException>(() => restricted.RestoreBackup(backup), "Operador não restaura backup de equipamentos");
        check(restricted.CurrentUser?.Role == UserRole.Operador && service.IsAdmin,
            "Ações recusadas mantêm as sessões e os papéis de administrador e operador");
        restricted.Logout();
        RejectAtomic<UnauthorizedAccessException>(() => restricted.SetDefaultPrinter(printer1.Id), "Sessão encerrada não define impressora padrão");
        RejectAtomic<UnauthorizedAccessException>(() => restricted.SetDefaultScale(scale1.Id), "Sessão encerrada não define balança padrão");
    }
}
