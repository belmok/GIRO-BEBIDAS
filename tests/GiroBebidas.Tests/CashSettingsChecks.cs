using System.Text.Json;
using System.Text.Json.Nodes;
using GiroBebidas.Core;

namespace GiroBebidas.Tests;

public static class CashSettingsChecks
{
    public static void Run(string root, Action<bool, string> check)
    {
        string directory = Path.Combine(root, "configuracoes-caixa", Guid.NewGuid().ToString("N"));
        var service = new StoreService(new JsonStore(directory));
        service.Login("ADMIN", "ADMIN");

        void Reject<T>(Action action, string name) where T : Exception
        {
            bool rejected = false;
            try { action(); } catch (T) { rejected = true; }
            check(rejected, name);
        }

        string State() => JsonSerializer.Serialize(service.Snapshot, JsonStore.JsonOptions);
        void Save(decimal opening, bool autoPrint)
        {
            var settings = service.Snapshot.Settings;
            settings.CashOpeningDefault = opening;
            settings.AutoPrint = autoPrint;
            service.SaveSettings(settings);
        }

        check(service.Snapshot.Settings.CashOpeningDefault == 0, "Caixa inicia com fundo sugerido zero");
        string originalAddress = service.Snapshot.Settings.Address;
        Save(150.75m, true);
        check(service.Snapshot.CashSessions.Count == 0 && service.Snapshot.CashMovements.Count == 0,
            "Salvar configuração não abre o caixa nem registra movimentações");
        check(service.Snapshot.Settings.Address == originalAddress, "Configuração do caixa preserva os demais dados da loja");

        service = new StoreService(new JsonStore(directory));
        check(service.Snapshot.Settings.CashOpeningDefault == 150.75m && service.Snapshot.Settings.AutoPrint,
            "Fundo sugerido e impressão automática permanecem após reabrir os dados");
        service.Login("ADMIN", "ADMIN");
        string backup = Path.Combine(directory, "configuracoes-caixa-backup.json");
        service.ExportBackup(backup);
        Save(2.50m, false);
        var disabledReload = new StoreService(new JsonStore(directory));
        check(disabledReload.Snapshot.Settings.CashOpeningDefault == 2.50m && !disabledReload.Snapshot.Settings.AutoPrint,
            "Desativar impressão automática e alterar fundo também persiste após reabrir");
        service.RestoreBackup(backup);
        check(service.Snapshot.Settings.CashOpeningDefault == 150.75m && service.Snapshot.Settings.AutoPrint,
            "Backup restaura o fundo sugerido e a impressão automática");
        check(service.CurrentUserId is null, "Restaurar configurações pelo backup exige novo login");
        var restoredReload = new StoreService(new JsonStore(directory));
        check(restoredReload.Snapshot.Settings.CashOpeningDefault == 150.75m && restoredReload.Snapshot.Settings.AutoPrint,
            "Configurações restauradas permanecem gravadas após reiniciar");

        var legacy = JsonNode.Parse(File.ReadAllText(backup))!.AsObject();
        legacy["Settings"]!.AsObject().Remove("CashOpeningDefault");
        string legacyBackup = Path.Combine(directory, "backup-sem-fundo-sugerido.json");
        File.WriteAllText(legacyBackup, legacy.ToJsonString());
        service.Login("ADMIN", "ADMIN");
        service.RestoreBackup(legacyBackup);
        check(service.Snapshot.Settings.CashOpeningDefault == 0 && service.Snapshot.Settings.AutoPrint,
            "Backup antigo sem fundo sugerido recebe zero e preserva impressão automática");
        check(new JsonStore(directory).Snapshot().Settings.CashOpeningDefault == 0,
            "Fundo zero de backup antigo permanece após reabrir");
        service.Login("ADMIN", "ADMIN");
        Save(150.75m, true);

        (decimal Value, string FileName, string Label)[] invalidValues =
        [
            (-0.01m, "negativo", "negativo"),
            (150.751m, "tres-decimais", "com mais de duas casas decimais"),
            (1_000_000_000m, "acima-limite", "acima do limite monetário")
        ];
        foreach (var invalid in invalidValues)
        {
            string beforeFile = File.ReadAllText(service.Store.FilePath);
            string beforeState = State();
            var settings = service.Snapshot.Settings;
            settings.CashOpeningDefault = invalid.Value;
            settings.AutoPrint = false;
            Reject<InvalidOperationException>(() => service.SaveSettings(settings), $"Fundo sugerido {invalid.Label} é recusado");
            check(State() == beforeState && File.ReadAllText(service.Store.FilePath) == beforeFile,
                $"Configuração recusada por fundo {invalid.Label} preserva estado completo e arquivo");

            var malformed = JsonNode.Parse(File.ReadAllText(backup))!.AsObject();
            malformed["Settings"]!["CashOpeningDefault"] = invalid.Value;
            malformed["Settings"]!["AutoPrint"] = false;
            string invalidBackup = Path.Combine(directory, $"backup-fundo-{invalid.FileName}.json");
            File.WriteAllText(invalidBackup, malformed.ToJsonString());
            Reject<InvalidDataException>(() => service.RestoreBackup(invalidBackup), $"Backup com fundo {invalid.Label} é recusado");
            check(State() == beforeState && File.ReadAllText(service.Store.FilePath) == beforeFile && service.IsAdmin,
                $"Backup recusado por fundo {invalid.Label} preserva dados, arquivo e sessão");
        }

        Save(0, false);
        check(service.Snapshot.Settings.CashOpeningDefault == 0, "Fundo sugerido zero é aceito sem abrir caixa");
        Save(999_999_999m, true);
        check(new JsonStore(directory).Snapshot().Settings.CashOpeningDefault == 999_999_999m,
            "Limite monetário permitido do fundo sugerido é salvo sem arredondamento");
        Save(150.75m, true);
        CashSession opened = service.OpenCash(25);
        check(opened.OpeningAmount == 25 && service.Snapshot.CashSessions.Single().OpeningAmount == 25,
            "Abertura explícita de 25 reais prevalece sobre sugestão de 150,75");
        check(StoreService.SummarizeCash(service.Snapshot, opened.Id).Expected == 25,
            "Saldo do caixa usa o fundo informado na abertura");
        check(service.Snapshot.Settings.CashOpeningDefault == 150.75m,
            "Abrir caixa com outro valor preserva a sugestão configurada");
        Save(200, false);
        check(service.Snapshot.CashSessions.Single().OpeningAmount == 25
            && StoreService.SummarizeCash(service.Snapshot, opened.Id).Expected == 25
            && service.Snapshot.CashMovements.Count == 0,
            "Alterar configuração com caixa aberto preserva fundo e saldo sem criar movimentos");
        service.CloseCash(25, "Conferência do teste de configurações");

        service.SaveUser(new UserAccount { Name = "Operador de teste", Username = "CAIXA_CONFIG", Role = UserRole.Operador }, "1234");
        service.Login("CAIXA_CONFIG", "1234");
        string operatorFile = File.ReadAllText(service.Store.FilePath);
        string operatorState = State();
        var forbidden = service.Snapshot.Settings;
        forbidden.CashOpeningDefault = 300;
        forbidden.AutoPrint = true;
        Reject<UnauthorizedAccessException>(() => service.SaveSettings(forbidden), "Operador não altera fundo sugerido nem impressão automática");
        check(State() == operatorState && File.ReadAllText(service.Store.FilePath) == operatorFile,
            "Tentativa do operador preserva todas as configurações e o arquivo");
    }
}
