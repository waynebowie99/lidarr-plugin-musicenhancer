namespace NzbDrone.Core.Plugins;

/// <summary>
/// Lidarr plugin entry point. Lidarr discovers this type to list the plugin
/// and check for updates from the repository.
/// </summary>
public sealed class MusicEnhancerPlugin : Plugin
{
    public const string DisplayName = "Music Enhancer";
    public const string RepositoryOwner = "waynebowie99";
    public const string RepositoryUrl = "https://github.com/waynebowie99/lidarr-plugin-musicenhancer";

    public override string Name => DisplayName;

    public override string Owner => RepositoryOwner;

    public override string GithubUrl => RepositoryUrl;
}
