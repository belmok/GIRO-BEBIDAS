using System.Text.Json;
using System.Text.Json.Serialization;

namespace GiroBebidas.Core;

public sealed class JsonStore
{
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() }, IgnoreReadOnlyProperties = true };
    private readonly object gate = new();
    private StoreState state;
    public string DirectoryPath { get; }
    public string FilePath => Path.Combine(DirectoryPath, "giro-dados.json");
    public bool RecoveredFromBackup { get; private set; }

    public JsonStore(string directory)
    {
        DirectoryPath = Path.GetFullPath(directory);
        Directory.CreateDirectory(DirectoryPath);
        if (File.Exists(FilePath))
        {
            try { state = Read(FilePath); }
            catch (Exception ex) when (ex is JsonException or InvalidDataException)
            {
                string backup = FilePath + ".bak";
                if (!File.Exists(backup)) throw new InvalidDataException("Os dados não puderam ser lidos. Preserve o arquivo e restaure um backup.", ex);
                state = Read(backup);
                File.Copy(FilePath, Path.Combine(DirectoryPath, $"dados-corrompidos-{DateTime.Now:yyyyMMddHHmmssfff}.json"));
                File.Copy(backup, FilePath, true);
                RecoveredFromBackup = true;
            }
        }
        else { state = DemoData.Create(); Save(state); }
    }
    private static StoreState Read(string path)
    {
        StoreState result = JsonSerializer.Deserialize<StoreState>(File.ReadAllText(path), JsonOptions) ?? throw new InvalidDataException("Arquivo vazio.");
        if (result.SchemaVersion != 1 || result.Settings is null || result.Fiscal is null || result.Fiscal.Issuer is null || result.Fiscal.Options is null
            || result.Fiscal.Homologation is null || result.Fiscal.Production is null || result.Fiscal.Certificate is null || result.Users is null || result.Products is null || result.Customers is null
            || result.Sales is null || result.Orders is null || result.CashSessions is null || result.CashMovements is null || result.ReceivablePayments is null
            || result.StockMovements is null || result.Audit is null || result.Printers is null || result.Scales is null
            || result.Users.Count == 0 || !result.Users.Any(u => u is not null && u.Active && u.Role == UserRole.Administrador))
            throw new InvalidDataException("Versão de dados incompatível ou administrador ausente.");
        if (result.Users.Any(u => u is null || string.IsNullOrWhiteSpace(u.Username) || u.PasswordHash is null || u.PasswordSalt is null) || result.Products.Any(p => p is null || p.Tax is null) || result.Customers.Any(c => c is null)
            || result.Sales.Any(s => s is null || s.Lines is null) || result.Orders.Any(o => o is null || o.Lines is null))
            throw new InvalidDataException("O backup contém registros incompletos.");
        if (result.Users.Select(u => u.Id).Distinct().Count() != result.Users.Count || result.Users.Select(u => u.Username.ToUpperInvariant()).Distinct().Count() != result.Users.Count
            || result.Products.Select(p => p.Id).Distinct().Count() != result.Products.Count || result.Products.Any(p => p.Stock < 0)
            || result.CashSessions.Count(c => c.ClosedAt is null) > 1 || result.NextSaleNumber <= result.Sales.Select(s => s.Number).DefaultIfEmpty(0).Max()
            || result.NextOrderNumber <= result.Orders.Select(o => o.Number).DefaultIfEmpty(0).Max())
            throw new InvalidDataException("O backup contém identificadores, contadores ou saldos inconsistentes.");
        foreach (var user in result.Users)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(user.Username) || !Enum.IsDefined(user.Role) || user.PasswordIterations < 10_000 || user.PasswordIterations > 1_000_000
                    || Convert.FromBase64String(user.PasswordHash).Length != 32 || Convert.FromBase64String(user.PasswordSalt).Length < 16)
                    throw new InvalidDataException("Credenciais inválidas no backup.");
            }
            catch (FormatException ex) { throw new InvalidDataException("Credenciais inválidas no backup.", ex); }
        }
        return result;
    }
    private static StoreState Clone(StoreState value) => JsonSerializer.Deserialize<StoreState>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions)!;
    public StoreState Snapshot() { lock (gate) return Clone(state); }
    public T Update<T>(Func<StoreState, T> action)
    {
        lock (gate)
        {
            StoreState next = Clone(state);
            T result = action(next);
            StoreState committed = Clone(next);
            Save(committed);
            state = committed;
            return result;
        }
    }
    private void Save(StoreState next)
    {
        string temp = FilePath + ".tmp";
        try
        {
            if (next.Settings.DailyBackup && File.Exists(FilePath))
            {
                string backups = Path.Combine(DirectoryPath, "Backups"); Directory.CreateDirectory(backups);
                string daily = Path.Combine(backups, $"giro-{DateTime.Now:yyyy-MM-dd}.json");
                if (!File.Exists(daily)) File.Copy(FilePath, daily);
            }
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(next, JsonOptions);
            using (FileStream stream = new(temp, FileMode.Create, FileAccess.Write, FileShare.None)) { stream.Write(bytes); stream.Flush(true); }
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak", true);
                    else File.Move(temp, FilePath);
                    break;
                }
                catch (IOException) when (attempt < 6 && File.Exists(temp))
                {
                    // A sync client or antivirus may briefly lock the previous backup.
                    // Retry only while the pending file still exists; never repeat the business operation.
                    Thread.Sleep(Math.Min(800, 50 * (1 << attempt)));
                }
            }
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public void ExportBackup(string target) { lock (gate) File.Copy(FilePath, target, true); }
    public void RestoreBackup(string source)
    {
        lock (gate)
        {
            StoreState restored = Read(source);
            File.Copy(FilePath, Path.Combine(DirectoryPath, $"antes-restauracao-{DateTime.Now:yyyyMMddHHmmssfff}.json"));
            Save(restored);
            state = restored;
        }
    }
}
