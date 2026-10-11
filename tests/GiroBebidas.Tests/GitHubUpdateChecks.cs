using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GiroBebidas.Core;

namespace GiroBebidas.Tests;

public static class GitHubUpdateChecks
{
    public static async Task RunAsync(string root, Action<bool, string> check)
    {
        const string repository = "teste/GiroBebidas";
        const string tag = "v1.1.0";
        byte[] installer = Encoding.UTF8.GetBytes("Conteúdo fictício para conferir o download; não é executável.");
        string digest = Convert.ToHexString(SHA256.HashData(installer));
        byte[] checksum = Encoding.UTF8.GetBytes(digest + "  InstalarGiroBebidas.exe\r\n" + new string('0', 64) + "  GiroBebidas.exe\r\n");
        string AssetUrl(string name, string selectedRepository = repository) => $"https://github.com/{selectedRepository}/releases/download/{tag}/{name}";
        string Metadata(bool draft = false, bool prerelease = false, string releaseTag = tag, string? installerUrl = null,
            long? installerSize = null, bool omitInstaller = false, bool duplicateInstaller = false, long? checksumSize = null)
        {
            object Installer() => new { name = GitHubUpdateClient.InstallerName, state = "uploaded", size = installerSize ?? installer.Length, browser_download_url = installerUrl ?? AssetUrl(GitHubUpdateClient.InstallerName) };
            var assets = new List<object>();
            if (!omitInstaller) assets.Add(Installer());
            if (duplicateInstaller) assets.Add(Installer());
            assets.Add(new { name = GitHubUpdateClient.ChecksumName, state = "uploaded", size = checksumSize ?? checksum.Length, browser_download_url = AssetUrl(GitHubUpdateClient.ChecksumName) });
            return JsonSerializer.Serialize(new { tag_name = releaseTag, name = "Giro Bebidas 1.1.0", body = "Melhorias do aplicativo.", draft, prerelease, published_at = "2026-10-11T12:00:00Z", assets });
        }
        GitHubUpdateClient MetadataClient(string metadata) => new(new FakeHandler((request, token) => Task.FromResult(Json(metadata))));
        async Task RejectAsync<T>(Func<Task> action, string name) where T : Exception
        {
            bool rejected = false;
            try { await action(); } catch (T) { rejected = true; }
            check(rejected, name);
        }
        void Reject<T>(Action action, string name) where T : Exception
        {
            bool rejected = false;
            try { action(); } catch (T) { rejected = true; }
            check(rejected, name);
        }

        check(GitHubUpdateClient.NormalizeRepository(" https://github.com/teste/GiroBebidas.git/ ") == repository, "Atualizações aceitam URL pública do repositório e normalizam .git");
        check(GitHubUpdateClient.NormalizeRepository(repository) == repository && new GitHubUpdateSettings().AutoCheck == false, "Atualizações iniciam sem consultas automáticas ou repositório inventado");
        Reject<InvalidOperationException>(() => GitHubUpdateClient.NormalizeRepository("https://github.com.evil.test/teste/GiroBebidas"), "Configuração recusa domínio parecido com GitHub");
        Reject<InvalidOperationException>(() => GitHubUpdateClient.NormalizeRepository("https://pessoa:segredo@github.com/teste/GiroBebidas"), "Configuração recusa credenciais na URL");
        Reject<InvalidOperationException>(() => GitHubUpdateClient.NormalizeRepository("https://github.com/teste/GiroBebidas?token=nao-usar"), "Configuração recusa tokens e parâmetros na URL");
        Reject<InvalidOperationException>(() => GitHubUpdateClient.NormalizeRepository("teste/GiroBebidas/caminho"), "Configuração recusa caminho além do repositório");
        Reject<InvalidOperationException>(() => GitHubUpdateClient.NormalizeRepository("https://github.com/teste/GiroBebidas/../Outro"), "Configuração não aceita navegação de diretórios na URL");
        check(GitHubUpdateClient.TryParseReleaseVersion("v1.10.0+publicacao", out var parsed) && parsed == new Version(1, 10, 0), "Atualizações comparam versões numericamente e aceitam metadados estáveis");
        check(!GitHubUpdateClient.TryParseReleaseVersion("v1.2.0-beta.1", out _) && !GitHubUpdateClient.TryParseReleaseVersion("última", out _), "Atualizações recusam versões de teste e etiquetas sem numeração");

        bool correctHeaders = false;
        using var validClient = new GitHubUpdateClient(new FakeHandler((request, token) =>
        {
            correctHeaders = request.Method == HttpMethod.Get && request.RequestUri?.AbsoluteUri == $"https://api.github.com/repos/{repository}/releases/latest" &&
                request.Headers.UserAgent.Count > 0 && request.Headers.Accept.Any(x => x.MediaType == "application/vnd.github+json") && request.Headers.Authorization is null;
            return Task.FromResult(Json(Metadata()));
        }));
        var release = await validClient.CheckAsync(repository, new Version(1, 0, 0));
        check(correctHeaders && release is not null && release.Version == new Version(1, 1, 0) && release.PublishedAt is not null, "Consulta pública identifica a versão nova sem enviar credenciais");
        using (var client = MetadataClient(Metadata())) check(await client.CheckAsync(repository, new Version(1, 1)) is null, "Versão instalada equivalente não aparece como atualização");
        using (var client = MetadataClient(Metadata())) check(await client.CheckAsync(repository, new Version(1, 10, 0)) is null, "Consulta não oferece regressão de versão");
        using (var client = MetadataClient(Metadata())) check(await client.CheckAsync("TESTE/girobebidas", new Version(1, 0, 0)) is not null, "Repositório segue a comparação sem maiúsculas usada pelo GitHub");
        using (var client = MetadataClient(Metadata(draft: true))) check(await client.CheckAsync(repository, new Version(1, 0, 0)) is null, "Publicação em rascunho não é oferecida");
        using (var client = MetadataClient(Metadata(prerelease: true))) check(await client.CheckAsync(repository, new Version(1, 0, 0)) is null, "Publicação marcada como prévia não é oferecida");
        using (var client = MetadataClient(Metadata(releaseTag: "v1.2.0-beta"))) check(await client.CheckAsync(repository, new Version(1, 0, 0)) is null, "Etiqueta de prévia é ignorada mesmo sem marcação do GitHub");
        using (var client = MetadataClient(Metadata(omitInstaller: true))) await RejectAsync<InvalidDataException>(() => client.CheckAsync(repository, new Version(1, 0, 0)), "Publicação sem instalador é recusada");
        using (var client = MetadataClient(Metadata(duplicateInstaller: true))) await RejectAsync<InvalidDataException>(() => client.CheckAsync(repository, new Version(1, 0, 0)), "Publicação com dois instaladores de mesmo nome é recusada");
        using (var client = MetadataClient(Metadata(installerUrl: "https://github.com.evil.test/InstalarGiroBebidas.exe"))) await RejectAsync<InvalidDataException>(() => client.CheckAsync(repository, new Version(1, 0, 0)), "Ativo fora do GitHub oficial é recusado");
        using (var client = MetadataClient(Metadata(installerUrl: AssetUrl(GitHubUpdateClient.InstallerName, "outro/GiroBebidas")))) await RejectAsync<InvalidDataException>(() => client.CheckAsync(repository, new Version(1, 0, 0)), "Ativo de outro repositório é recusado");
        using (var client = MetadataClient(Metadata(installerSize: GitHubUpdateClient.MaximumInstallerBytes + 1))) await RejectAsync<InvalidDataException>(() => client.CheckAsync(repository, new Version(1, 0, 0)), "Instalador acima do limite não é oferecido");
        using (var client = MetadataClient(Metadata(checksumSize: GitHubUpdateClient.MaximumChecksumBytes + 1))) await RejectAsync<InvalidDataException>(() => client.CheckAsync(repository, new Version(1, 0, 0)), "Arquivo de hash acima do limite não é oferecido");
        using (var client = new GitHubUpdateClient(new FakeHandler((request, token) =>
        {
            var response = Json("{}"); response.Content.Headers.ContentLength = GitHubUpdateClient.MaximumMetadataBytes + 1; return Task.FromResult(response);
        }))) await RejectAsync<InvalidDataException>(() => client.CheckAsync(repository, new Version(1, 0, 0)), "Metadados com tamanho declarado excessivo são recusados");
        using (var client = new GitHubUpdateClient(new FakeHandler((request, token) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new UnknownLengthContent(new byte[GitHubUpdateClient.MaximumMetadataBytes + 1]) }))))
            await RejectAsync<InvalidDataException>(() => client.CheckAsync(repository, new Version(1, 0, 0)), "Metadados sem Content-Length também respeitam limite durante a leitura");
        using (var client = new GitHubUpdateClient(new FakeHandler((request, token) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)))))
            check(await client.CheckAsync(repository, new Version(1, 0, 0)) is null, "Repositório sem versão pública não exige token");

        check(GitHubUpdateClient.ParseInstallerChecksum("\uFEFF" + Encoding.UTF8.GetString(checksum)).Equals(digest, StringComparison.OrdinalIgnoreCase), "SHA-256 seleciona apenas o nome exato do instalador");
        Reject<InvalidDataException>(() => GitHubUpdateClient.ParseInstallerChecksum(digest + "  ../InstalarGiroBebidas.exe"), "Hash com caminho no nome não substitui o hash do instalador");
        Reject<InvalidDataException>(() => GitHubUpdateClient.ParseInstallerChecksum(digest + " *InstalarGiroBebidas.exe"), "Arquivo de hash exige o formato publicado de duas separações");
        Reject<InvalidDataException>(() => GitHubUpdateClient.ParseInstallerChecksum(digest + "  InstalarGiroBebidas.exe\n" + digest + "  InstalarGiroBebidas.exe"), "Entradas duplicadas no arquivo SHA-256 são recusadas");

        string updates = Path.Combine(root, "atualizacoes-github", "Updates");
        bool requestsWithoutCredentials = true;
        var reports = new List<GitHubDownloadProgress>();
        using (var client = new GitHubUpdateClient(new FakeHandler((request, token) =>
        {
            requestsWithoutCredentials &= request.Headers.Authorization is null && !request.Headers.Contains("Cookie");
            string uri = request.RequestUri!.AbsoluteUri;
            if (uri == AssetUrl(GitHubUpdateClient.ChecksumName)) return Task.FromResult(Redirect("https://release-assets.githubusercontent.com/github-production-release-asset/checksums?assinatura=teste"));
            if (uri == AssetUrl(GitHubUpdateClient.InstallerName)) return Task.FromResult(Redirect("https://objects.githubusercontent.com/github-production-release-asset/installer?assinatura=teste"));
            if (request.RequestUri.Host == "release-assets.githubusercontent.com") return Task.FromResult(Bytes(checksum));
            if (request.RequestUri.Host == "objects.githubusercontent.com") return Task.FromResult(Bytes(installer));
            throw new InvalidOperationException("Solicitação inesperada no teste.");
        })))
        {
            string downloaded = await client.DownloadAsync(release!, updates, new InlineProgress(reports.Add));
            check(File.ReadAllBytes(downloaded).SequenceEqual(installer) && requestsWithoutCredentials && reports.Last().Percentage == 100,
                "Download segue CDN oficial, valida SHA-256 e informa conclusão sem executar o arquivo");
            check(!Directory.GetFiles(updates, "*.download").Any(), "Download concluído remove o arquivo temporário");
        }
        string destination = Path.Combine(updates, "InstalarGiroBebidas-1.1.0.exe");
        File.WriteAllText(destination, "versão anterior preservada");
        using (var client = new GitHubUpdateClient(new FakeHandler((request, token) =>
            Task.FromResult(Bytes(request.RequestUri!.AbsolutePath.EndsWith(GitHubUpdateClient.ChecksumName) ? checksum : new byte[installer.Length])))))
        {
            await RejectAsync<InvalidDataException>(() => client.DownloadAsync(release!, updates), "Instalador com SHA-256 divergente é descartado");
            check(File.ReadAllText(destination) == "versão anterior preservada" && !Directory.GetFiles(updates, "*.download").Any(), "Falha de integridade preserva download anterior e limpa dados temporários");
        }
        using (var client = new GitHubUpdateClient(new FakeHandler((request, token) => Task.FromResult(Redirect("https://downloads.evil.test/instalador")))))
            await RejectAsync<InvalidDataException>(() => client.DownloadAsync(release!, updates), "Redirecionamento para site externo é bloqueado");
        using (var client = new GitHubUpdateClient(new FakeHandler((request, token) => Task.FromResult(Redirect("http://objects.githubusercontent.com/instalador")))))
            await RejectAsync<InvalidDataException>(() => client.DownloadAsync(release!, updates), "Redirecionamento sem HTTPS é bloqueado");
        using (var client = new GitHubUpdateClient(new FakeHandler((request, token) => Task.FromResult(Redirect("https://pessoa:senha@objects.githubusercontent.com/instalador")))))
            await RejectAsync<InvalidDataException>(() => client.DownloadAsync(release!, updates), "Redirecionamento com credenciais é bloqueado");
        using (var client = new GitHubUpdateClient(new FakeHandler((request, token) =>
        {
            HttpResponseMessage response = request.RequestUri!.AbsolutePath.EndsWith(GitHubUpdateClient.ChecksumName) ? Bytes(checksum) : new HttpResponseMessage(HttpStatusCode.OK) { Content = new UnknownLengthContent(installer[..^1]) };
            return Task.FromResult(response);
        }))) await RejectAsync<InvalidDataException>(() => client.DownloadAsync(release!, updates), "Resposta truncada não vira instalador disponível");
        using (var client = new GitHubUpdateClient(new FakeHandler((request, token) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new UnknownLengthContent(new byte[GitHubUpdateClient.MaximumChecksumBytes + 1]) }))))
            await RejectAsync<InvalidDataException>(() => client.DownloadAsync(release!, updates), "Arquivo de hash sem Content-Length respeita o limite de leitura");
        using (var client = new GitHubUpdateClient(new FakeHandler((request, token) =>
            Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith(GitHubUpdateClient.ChecksumName) ? Bytes(checksum) : new HttpResponseMessage(HttpStatusCode.OK) { Content = new UnknownLengthContent(new byte[installer.Length + 1]) }))))
            await RejectAsync<InvalidDataException>(() => client.DownloadAsync(release!, updates), "Download sem Content-Length não pode exceder o tamanho publicado");
        using (var canceled = new CancellationTokenSource())
        using (var client = new GitHubUpdateClient(new FakeHandler((request, token) => Task.FromResult(Bytes(request.RequestUri!.AbsolutePath.EndsWith(GitHubUpdateClient.ChecksumName) ? checksum : installer)))))
        {
            await RejectAsync<OperationCanceledException>(() => client.DownloadAsync(release!, updates, new InlineProgress(p => { if (p.BytesReceived > 0) canceled.Cancel(); }), canceled.Token), "Cancelamento de download é respeitado antes de disponibilizar o instalador");
            check(File.ReadAllText(destination) == "versão anterior preservada" && !Directory.GetFiles(updates, "*.download").Any(), "Cancelamento preserva instalador anterior e apaga download incompleto");
        }
        using (var canceled = new CancellationTokenSource())
        using (var client = MetadataClient(Metadata()))
        {
            canceled.Cancel();
            await RejectAsync<OperationCanceledException>(() => client.CheckAsync(repository, new Version(1, 0, 0), canceled.Token), "Cancelamento de consulta não é transformado em sucesso");
        }
    }

    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK) { Content = new StringContent(value, Encoding.UTF8, "application/json") };
    private static HttpResponseMessage Bytes(byte[] value) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(value) };
    private static HttpResponseMessage Redirect(string uri)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Redirect); response.Headers.Location = new Uri(uri); return response;
    }
    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); return respond(request, cancellationToken); }
    }
    private sealed class InlineProgress(Action<GitHubDownloadProgress> report) : IProgress<GitHubDownloadProgress>
    { public void Report(GitHubDownloadProgress value) => report(value); }
    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }
}
