using System.IO.Compression;
using System.Text.Json;

namespace GiroBebidas.Core;

public class InstallManifest
{
    public string Product { get; set; } = "GiroBebidas";
    public string Version { get; set; } = typeof(Installation).Assembly.GetName().Version!.ToString(3);
    public List<string> Files { get; set; } = [];
}
public static class Installation
{
    public const string ManifestName = ".giro-instalacao.json";
    public static string ResolveInside(string root, string relative)
    {
        string resolvedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (Path.IsPathRooted(relative) || relative.Contains(':')) throw new InvalidDataException("O pacote contém um caminho não permitido.");
        string path = Path.GetFullPath(Path.Combine(resolvedRoot, relative));
        if (!path.StartsWith(resolvedRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("O pacote tenta acessar uma pasta fora da instalação.");
        string? walk = Path.GetDirectoryName(path);
        while (walk is not null && walk.StartsWith(resolvedRoot, StringComparison.OrdinalIgnoreCase))
        {
            if (Directory.Exists(walk) && (File.GetAttributes(walk) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Links de diretório não são permitidos na instalação.");
            walk = Path.GetDirectoryName(walk);
        }
        return path;
    }
    public static InstallManifest? ReadManifest(string target)
    {
        string file = Path.Combine(Path.GetFullPath(target), ManifestName);
        if (!File.Exists(file)) return null;
        var manifest = JsonSerializer.Deserialize<InstallManifest>(File.ReadAllText(file));
        if (manifest is null || manifest.Product != "GiroBebidas" || !manifest.Files.Contains("GiroBebidas.exe") || !manifest.Files.Contains("DesinstalarGiroBebidas.exe")) throw new InvalidDataException("Esta pasta não contém uma instalação válida do Giro Bebidas.");
        foreach (string item in manifest.Files) _ = ResolveInside(target, item);
        return manifest;
    }
    public static void Install(Stream package, string target, Action<int>? progress = null)
    {
        target = Path.GetFullPath(target);
        if (Path.GetPathRoot(target)?.TrimEnd('\\') == target.TrimEnd('\\')) throw new InvalidDataException("Escolha uma pasta específica para instalar.");
        if (Directory.Exists(target) && (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Não é permitido instalar em um link de diretório.");
        var previous = ReadManifest(target);
        string stage = Path.Combine(target, ".giro-pacote-" + Guid.NewGuid().ToString("N"));
        string rollback = Path.Combine(target, ".giro-anterior-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage); Directory.CreateDirectory(rollback);
        List<string> committed = []; List<string> backedUp = [];
        try
        {
            using var archive = new ZipArchive(package, ZipArchiveMode.Read, true);
            var files = archive.Entries.Where(e => e.Name.Length > 0).ToList();
            if (!files.Any(e => e.FullName == "GiroBebidas.exe") || !files.Any(e => e.FullName == "DesinstalarGiroBebidas.exe")) throw new InvalidDataException("Pacote incompleto.");
            long totalSize = files.Sum(e => e.Length);
            if (totalSize > 1_500_000_000) throw new InvalidDataException("O pacote excede o tamanho permitido.");
            var names = files.Select(e => e.FullName).ToList();
            if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count || names.Any(n => Path.GetFileName(n) == ManifestName)) throw new InvalidDataException("Pacote com arquivos duplicados ou reservados.");
            for (int i = 0; i < files.Count; i++)
            {
                var entry = files[i]; string destination = ResolveInside(stage, entry.FullName); string actual = ResolveInside(target, entry.FullName);
                if (File.Exists(actual) && (previous is null || !previous.Files.Contains(entry.FullName, StringComparer.OrdinalIgnoreCase))) throw new InvalidDataException("A pasta contém arquivos de outra origem. Escolha uma pasta vazia.");
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!); entry.ExtractToFile(destination); progress?.Invoke((i + 1) * 75 / files.Count);
            }
            foreach (var name in names)
            {
                string destination = ResolveInside(target, name); Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                if (File.Exists(destination)) { string old = ResolveInside(rollback, name); Directory.CreateDirectory(Path.GetDirectoryName(old)!); File.Move(destination, old); backedUp.Add(name); }
                File.Move(ResolveInside(stage, name), destination); committed.Add(name);
            }
            string manifest = Path.Combine(target, ManifestName); string pending = manifest + ".tmp";
            File.WriteAllText(pending, JsonSerializer.Serialize(new InstallManifest { Files = names }, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(pending, manifest, true); progress?.Invoke(100);
        }
        catch
        {
            foreach (string name in committed) File.Delete(ResolveInside(target, name));
            foreach (string name in backedUp) File.Move(ResolveInside(rollback, name), ResolveInside(target, name), true);
            throw;
        }
        finally
        {
            // Both absolute temporary paths are verified against the explicitly selected install folder.
            if (!stage.StartsWith(target.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase) || !rollback.StartsWith(target.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Pasta temporária inválida.");
            if (Directory.Exists(stage)) Directory.Delete(stage, true);
            if (Directory.Exists(rollback)) Directory.Delete(rollback, true);
        }
    }
    public static List<string> Uninstall(string target, string? runningExecutable = null)
    {
        target = Path.GetFullPath(target); var manifest = ReadManifest(target) ?? throw new InvalidDataException("Instalação não encontrada.");
        List<string> deferred = [];
        foreach (string name in manifest.Files)
        {
            string file = ResolveInside(target, name);
            if (runningExecutable is not null && file.Equals(Path.GetFullPath(runningExecutable), StringComparison.OrdinalIgnoreCase)) { deferred.Add(file); continue; }
            if (File.Exists(file)) File.Delete(file);
        }
        File.Delete(Path.Combine(target, ManifestName));
        // Only remove empty directories, preserving unrelated files and all user data.
        foreach (string subfolder in manifest.Files.Select(n => Path.GetDirectoryName(ResolveInside(target, n))!).Distinct().OrderByDescending(n => n.Length))
            if (Directory.Exists(subfolder) && !Directory.EnumerateFileSystemEntries(subfolder).Any()) Directory.Delete(subfolder);
        return deferred;
    }
}
