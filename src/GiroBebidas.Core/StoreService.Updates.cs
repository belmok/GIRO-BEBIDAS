namespace GiroBebidas.Core;

public sealed partial class StoreService
{
    internal static GitHubUpdateSettings ValidateUpdateSettings(GitHubUpdateSettings? settings)
    {
        if (settings is null) throw new InvalidOperationException("A configuração de atualizações está incompleta.");
        string repository = GitHubUpdateClient.NormalizeRepository(settings.Repository);
        return new() { Repository = repository, AutoCheck = repository.Length > 0 && settings.AutoCheck };
    }

    public void SaveUpdateSettings(GitHubUpdateSettings settings)
    {
        Store.Update(s =>
        {
            var actor = Actor(s, true);
            s.Settings.GitHubUpdates = ValidateUpdateSettings(settings);
            Log(s, actor, "Configuração de atualizações salva");
            return 0;
        });
    }
}
