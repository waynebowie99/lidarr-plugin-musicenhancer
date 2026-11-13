using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("MusicEnhancer.Test")]

namespace NzbDrone.Core.Plugins;

/// <summary>
/// Exported Lidarr plugin root for MusicEnhancer. The assembly name remains
/// Lidarr.Plugin.MusicEnhancer via MusicEnhancer.csproj.
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
