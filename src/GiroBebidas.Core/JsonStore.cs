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
        if (result.SchemaVersion != 1 || result.Users.Count == 0 || !result.Users.Any(u => u.Active && u.Role == UserRole.Administrador))
            throw new InvalidDataException("Versão de dados incompatível ou administrador ausente.");
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
            Save(next);
            state = next;
            return result;
        }
    }
    private void Save(StoreState next)
    {
        string temp = FilePath + ".tmp";
        try
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(next, JsonOptions);
            using (FileStream stream = new(temp, FileMode.Create, FileAccess.Write, FileShare.None)) { stream.Write(bytes); stream.Flush(true); }
            if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak", true);
            else File.Move(temp, FilePath);
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
