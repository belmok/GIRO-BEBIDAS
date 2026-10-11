using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GiroBebidas.Core;

public sealed class GitHubUpdateSettings
{
    public string Repository { get; set; } = "";
    public bool AutoCheck { get; set; }
}

public sealed record GitHubReleaseAsset(string Name, Uri DownloadUrl, long Size);
public sealed record GitHubReleaseUpdate(string Repository, Version Version, string Tag, string Name,
    string Notes, Uri ReleaseUrl, DateTimeOffset? PublishedAt, GitHubReleaseAsset InstallerAsset,
    GitHubReleaseAsset ChecksumAsset);
public sealed record GitHubDownloadProgress(long BytesReceived, long TotalBytes)
{
    public double Percentage => TotalBytes <= 0 ? 0 : Math.Min(100, BytesReceived * 100.0 / TotalBytes);
}

/// <summary>Reads public releases and downloads a verified installer. It never starts a process.</summary>
public sealed class GitHubUpdateClient : IDisposable
{
    public const string InstallerName = "InstalarGiroBebidas.exe";
    public const string ChecksumName = "SHA256.txt";
    public const long MaximumInstallerBytes = 512L * 1024 * 1024;
    public const int MaximumMetadataBytes = 2 * 1024 * 1024;
    public const int MaximumChecksumBytes = 256 * 1024;
    private readonly HttpClient client;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly string[] AssetHosts =
        ["objects.githubusercontent.com", "release-assets.githubusercontent.com", "github-releases.githubusercontent.com"];

    // An injectable transport allows deterministic checks without contacting a real repository.
    public GitHubUpdateClient(HttpMessageHandler? handler = null)
    {
        handler ??= new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseDefaultCredentials = false };
        client = new HttpClient(handler, true) { Timeout = Timeout.InfiniteTimeSpan };
    }

    public static string NormalizeRepository(string? repository)
    {
        string value = (repository ?? "").Trim();
        if (value.Length == 0) return "";
        if (value.Contains("://", StringComparison.Ordinal))
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || !IsHttps(uri) ||
                !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
                uri.Query.Length > 0 || uri.Fragment.Length > 0 || value.Contains('%'))
                throw new InvalidOperationException("Informe o repositório como usuário/projeto ou uma URL HTTPS do GitHub.");
            // Keep the original path so dot segments cannot silently select another repository.
            var source = Regex.Match(value, @"^https://github\.com/(?<path>[^?#]*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!source.Success) throw new InvalidOperationException("Informe uma URL HTTPS simples do repositório no GitHub.");
            value = source.Groups["path"].Value.Trim('/');
        }
        if (value.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) value = value[..^4];
        var parts = value.Split('/');
        if (parts.Length != 2 || !Regex.IsMatch(parts[0], @"^[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?$", RegexOptions.CultureInvariant) ||
            !Regex.IsMatch(parts[1], @"^[A-Za-z0-9_.-]{1,100}$", RegexOptions.CultureInvariant) || parts[1] is "." or "..")
            throw new InvalidOperationException("Informe o repositório como usuário/projeto, sem espaços ou caminhos adicionais.");
        return parts[0] + "/" + parts[1];
    }

    public static bool TryParseReleaseVersion(string? tag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(tag)) return false;
        string value = tag;
        if (value[0] is 'v' or 'V') value = value[1..];
        // Stable tags only. Build metadata may be present but does not change precedence.
        if (!Regex.IsMatch(value, @"^[0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?(?:\+[A-Za-z0-9.-]+)?$", RegexOptions.CultureInvariant)) return false;
        int metadata = value.IndexOf('+');
        if (metadata >= 0) value = value[..metadata];
        if (!Version.TryParse(value, out var parsed)) return false;
        version = parsed;
        return true;
    }

    public async Task<GitHubReleaseUpdate?> CheckAsync(string repository, Version currentVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentVersion);
        string normalized = NormalizeRepository(repository);
        if (normalized.Length == 0) throw new InvalidOperationException("Configure o repositório público do GitHub para consultar atualizações.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            using var request = Request(new Uri($"https://api.github.com/repos/{normalized}/releases/latest"), true);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound) return null; // Also covers private repositories, which are intentionally unsupported.
            EnsureSuccess(response);
            EnsureResponseUri(response, request.RequestUri!);
            byte[] data = await ReadBoundedAsync(response.Content, MaximumMetadataBytes, timeout.Token).ConfigureAwait(false);
            using var document = JsonDocument.Parse(data);
            var release = document.RootElement;
            if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean()) return null;
            string tag = release.GetProperty("tag_name").GetString() ?? "";
            if (!TryParseReleaseVersion(tag, out var version) || Comparable(version) <= Comparable(currentVersion)) return null;
            var assets = release.GetProperty("assets").EnumerateArray().ToArray();
            var installer = ReadAsset(assets, normalized, tag, InstallerName, MaximumInstallerBytes);
            var checksum = ReadAsset(assets, normalized, tag, ChecksumName, MaximumChecksumBytes);
            string name = Text(release, "name", 200);
            string notes = Text(release, "body", 16000);
            DateTimeOffset? published = null;
            if (release.TryGetProperty("published_at", out var publishedProperty) && publishedProperty.ValueKind == JsonValueKind.String &&
                publishedProperty.TryGetDateTimeOffset(out var parsedDate)) published = parsedDate;
            var releaseUrl = new Uri($"https://github.com/{normalized}/releases/tag/{Uri.EscapeDataString(tag)}");
            return new(normalized, version, tag, name, notes, releaseUrl, published, installer, checksum);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new TimeoutException("A consulta ao GitHub demorou demais. Tente novamente quando a conexão estiver disponível."); }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or FormatException or InvalidOperationException)
        { throw new InvalidDataException("O GitHub retornou uma publicação com dados inválidos.", ex); }
    }

    public async Task<string> DownloadAsync(GitHubReleaseUpdate release, string trustedUpdatesDirectory,
        IProgress<GitHubDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(release);
        ValidateRelease(release);
        string directory = Path.GetFullPath(trustedUpdatesDirectory);
        if (directory.Equals(Path.GetPathRoot(directory), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Escolha a pasta de atualizações do aplicativo.");
        if (Directory.Exists(directory) && (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("A pasta de atualizações não pode ser um atalho de diretório.");
        string destination = Path.Combine(directory, $"InstalarGiroBebidas-{release.Version}.exe");
        if (Directory.Exists(destination) || (File.Exists(destination) && (File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0))
            throw new InvalidOperationException("O destino do instalador não é um arquivo regular.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        string? temporary = null;
        try
        {
            using var checksumResponse = await AssetResponseAsync(release.ChecksumAsset.DownloadUrl, timeout.Token).ConfigureAwait(false);
            byte[] checksumBytes = await ReadBoundedAsync(checksumResponse.Content, MaximumChecksumBytes, timeout.Token).ConfigureAwait(false);
            if (checksumBytes.LongLength != release.ChecksumAsset.Size) throw new InvalidDataException("O arquivo de verificação tem um tamanho diferente da publicação.");
            string expected = ParseInstallerChecksum(StrictUtf8.GetString(checksumBytes));
            using var response = await AssetResponseAsync(release.InstallerAsset.DownloadUrl, timeout.Token).ConfigureAwait(false);
            if (response.Content.Headers.ContentLength is long declared && declared != release.InstallerAsset.Size)
                throw new InvalidDataException("O instalador tem um tamanho diferente da publicação.");
            timeout.Token.ThrowIfCancellationRequested();
            Directory.CreateDirectory(directory);
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("A pasta de atualizações não pode ser um atalho de diretório.");
            temporary = Path.Combine(directory, $"instalador-{Guid.NewGuid():N}.download");
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var input = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous))
            {
                byte[] buffer = new byte[65536];
                long received = 0;
                progress?.Report(new(0, release.InstallerAsset.Size));
                int count;
                while ((count = await input.ReadAsync(buffer, timeout.Token).ConfigureAwait(false)) != 0)
                {
                    received += count;
                    if (received > release.InstallerAsset.Size || received > MaximumInstallerBytes)
                        throw new InvalidDataException("O download excedeu o tamanho esperado do instalador.");
                    hash.AppendData(buffer, 0, count);
                    await output.WriteAsync(buffer.AsMemory(0, count), timeout.Token).ConfigureAwait(false);
                    progress?.Report(new(received, release.InstallerAsset.Size));
                }
                if (received != release.InstallerAsset.Size) throw new InvalidDataException("O download do instalador ficou incompleto.");
                await output.FlushAsync(timeout.Token).ConfigureAwait(false);
            }
            string actual = Convert.ToHexString(hash.GetHashAndReset());
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expected), Convert.FromHexString(actual)))
                throw new InvalidDataException("A verificação SHA-256 falhou. O instalador foi descartado; consulte uma nova publicação.");
            timeout.Token.ThrowIfCancellationRequested();
            File.Move(temporary, destination, true);
            temporary = null;
            return destination;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new TimeoutException("O download demorou demais. Tente novamente quando a conexão estiver disponível."); }
        catch (DecoderFallbackException ex)
        { throw new InvalidDataException("O arquivo SHA256.txt não é um texto UTF-8 válido.", ex); }
        finally
        {
            if (temporary is not null)
            {
                try { File.Delete(temporary); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
    }

    public static string ParseInstallerChecksum(string text)
    {
        string? result = null;
        foreach (string source in text.TrimStart('\uFEFF').Split('\n'))
        {
            string line = source.TrimEnd('\r');
            if (!Regex.IsMatch(line, @"^[A-Fa-f0-9]{64}  InstalarGiroBebidas\.exe$", RegexOptions.CultureInvariant)) continue;
            if (result is not null) throw new InvalidDataException("SHA256.txt contém mais de uma entrada para o instalador.");
            result = line[..64];
        }
        return result ?? throw new InvalidDataException("SHA256.txt não contém o SHA-256 exato de InstalarGiroBebidas.exe.");
    }

    private static Version Comparable(Version version) => new(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision));
    private static bool IsHttps(Uri uri) => uri.IsAbsoluteUri && uri.Scheme == Uri.UriSchemeHttps && uri.Port == 443 && uri.UserInfo.Length == 0;
    private static string Text(JsonElement element, string name, int maximum)
    {
        if (!element.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null) return "";
        string value = property.GetString() ?? "";
        return value.Length > maximum ? value[..maximum] : value;
    }
    private static GitHubReleaseAsset ReadAsset(JsonElement[] assets, string repository, string tag, string name, long maximum)
    {
        var matches = assets.Where(asset => asset.GetProperty("name").GetString() == name).ToArray();
        if (matches.Length != 1) throw new InvalidDataException($"A publicação precisa conter exatamente um arquivo {name}.");
        var asset = matches[0];
        long size = asset.GetProperty("size").GetInt64();
        if (size <= 0 || size > maximum) throw new InvalidDataException($"O tamanho de {name} excede os limites de atualização.");
        if (!Uri.TryCreate(asset.GetProperty("browser_download_url").GetString(), UriKind.Absolute, out var uri))
            throw new InvalidDataException($"O endereço de {name} é inválido.");
        ValidateAssetUri(uri, repository, tag, name);
        if (asset.TryGetProperty("state", out var state) && state.GetString() != "uploaded")
            throw new InvalidDataException($"O arquivo {name} ainda não está disponível.");
        return new(name, uri, size);
    }
    private static void ValidateRelease(GitHubReleaseUpdate release)
    {
        string repository = NormalizeRepository(release.Repository);
        if (repository.Length == 0 || !TryParseReleaseVersion(release.Tag, out var version) || Comparable(version) != Comparable(release.Version))
            throw new InvalidDataException("A versão de atualização é inválida.");
        ValidateAssetUri(release.InstallerAsset.DownloadUrl, repository, release.Tag, InstallerName);
        ValidateAssetUri(release.ChecksumAsset.DownloadUrl, repository, release.Tag, ChecksumName);
        if (release.InstallerAsset.Name != InstallerName || release.ChecksumAsset.Name != ChecksumName ||
            release.InstallerAsset.Size <= 0 || release.InstallerAsset.Size > MaximumInstallerBytes ||
            release.ChecksumAsset.Size <= 0 || release.ChecksumAsset.Size > MaximumChecksumBytes)
            throw new InvalidDataException("Os arquivos de atualização são inválidos.");
    }
    private static void ValidateAssetUri(Uri uri, string repository, string tag, string name)
    {
        string prefix = "/" + repository + "/";
        if (!IsHttps(uri) || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
            !uri.AbsolutePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !Uri.UnescapeDataString(uri.AbsolutePath[prefix.Length..]).Equals($"releases/download/{tag}/{name}", StringComparison.Ordinal))
            throw new InvalidDataException("O arquivo de atualização precisa pertencer à publicação do repositório configurado no GitHub.");
    }
    private static HttpRequestMessage Request(Uri uri, bool metadata)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("GiroBebidas-Updater/1.0");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(metadata ? "application/vnd.github+json" : "application/octet-stream"));
        if (metadata) request.Headers.Add("X-GitHub-Api-Version", "2026-03-10");
        return request;
    }
    private async Task<HttpResponseMessage> AssetResponseAsync(Uri source, CancellationToken token)
    {
        Uri current = source;
        for (int redirects = 0; redirects <= 4; redirects++)
        {
            if (!IsHttps(current) || (current.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
                    ? current != source : !AssetHosts.Contains(current.Host, StringComparer.OrdinalIgnoreCase)))
                throw new InvalidDataException("O download foi redirecionado para um endereço não permitido.");
            using var request = Request(current, false);
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            try
            {
                EnsureResponseUri(response, current);
                if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
                {
                    Uri? location = response.Headers.Location;
                    if (location is null) throw new InvalidDataException("O GitHub retornou um redirecionamento sem endereço.");
                    current = location.IsAbsoluteUri ? location : new Uri(current, location);
                    response.Dispose();
                    continue;
                }
                EnsureSuccess(response);
                return response;
            }
            catch { response.Dispose(); throw; }
        }
        throw new InvalidDataException("O download excedeu o limite de redirecionamentos.");
    }
    private static void EnsureResponseUri(HttpResponseMessage response, Uri expected)
    {
        // Also catches transports supplied by callers that silently follow redirects.
        if (response.RequestMessage?.RequestUri is Uri actual && actual != expected)
            throw new InvalidDataException("O endereço da resposta não corresponde à solicitação de atualização.");
    }
    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.OK) return;
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            throw new HttpRequestException("O GitHub limitou as consultas. Aguarde alguns minutos antes de tentar novamente.", null, response.StatusCode);
        throw new HttpRequestException("Não foi possível obter a atualização pública no GitHub.", null, response.StatusCode);
    }
    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, int maximum, CancellationToken token)
    {
        if (content.Headers.ContentLength is long length && length > maximum) throw new InvalidDataException("O arquivo de atualização excedeu o tamanho permitido.");
        await using var stream = await content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var output = new MemoryStream();
        byte[] buffer = new byte[16384];
        int count;
        while ((count = await stream.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
        {
            if (output.Length + count > maximum) throw new InvalidDataException("O arquivo de atualização excedeu o tamanho permitido.");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }
    public void Dispose() => client.Dispose();
}
