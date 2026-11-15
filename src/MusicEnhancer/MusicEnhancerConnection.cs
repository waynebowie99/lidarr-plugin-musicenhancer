using System;
using FluentValidation.Results;
using NLog;
using NzbDrone.Core.Notifications;

namespace NzbDrone.Core.Plugins;

/// <summary>
/// Lidarr "Connect" provider for Music Enhancer. Registering a notification
/// provider is what gives the plugin an entry under Settings -> Connect and a
/// UI for its settings. It runs the content filter after an album is imported.
/// </summary>
public class MusicEnhancer : NotificationBase<MusicEnhancerSettings>
{
    private readonly IContentFilterService _contentFilterService;
    private readonly IMetadataEnhancerService _metadataEnhancerService;
    private readonly Logger _logger;

    public MusicEnhancer(
        IContentFilterService contentFilterService,
        IMetadataEnhancerService metadataEnhancerService,
        Logger logger)
    {
        _contentFilterService = contentFilterService ?? throw new ArgumentNullException(nameof(contentFilterService));
        _metadataEnhancerService = metadataEnhancerService ?? throw new ArgumentNullException(nameof(metadataEnhancerService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override string Name => MusicEnhancerPlugin.DisplayName;

    public override string Link => MusicEnhancerPlugin.RepositoryUrl;

    public override ValidationResult Test()
    {
        var result = Settings.Validate();
        if (result.IsValid) _logger.Info("MusicEnhancer: Connection settings are valid");
        return result;
    }

    public override void OnReleaseImport(AlbumDownloadMessage message)
    {
        if (message?.Artist == null || message.Album == null)
        {
            _logger.Warn("MusicEnhancer: Release import message did not contain an artist and album; skipping");
            return;
        }

        try
        {
            var options = Settings.ToFilterOptions();

            _contentFilterService.FilterContentForArtist(message.Artist, message.Album, options);
            _metadataEnhancerService.EnhanceMetadataForArtist(message.Artist, message.Album, options);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "MusicEnhancer: Failed to process imported album '{0}'", message.Album.Title);
        }
    }
}
