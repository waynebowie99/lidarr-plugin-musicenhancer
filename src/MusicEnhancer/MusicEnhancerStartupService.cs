using System;
using System.Linq;
using NLog;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Music;
using NzbDrone.Core.Notifications;

namespace NzbDrone.Core.Plugins;

/// <summary>
/// Runs a one-off scan of the existing library when Lidarr starts, if the
/// Music Enhancer connection has "Scan library on startup" enabled.
/// </summary>
public class MusicEnhancerStartupService : IHandleAsync<ApplicationStartedEvent>
{
    private readonly IArtistService _artistService;
    private readonly IAlbumService _albumService;
    private readonly IContentFilterService _contentFilterService;
    private readonly IMetadataEnhancerService _metadataEnhancerService;
    private readonly INotificationFactory _notificationFactory;
    private readonly Logger _logger;

    public MusicEnhancerStartupService(
        IArtistService artistService,
        IAlbumService albumService,
        IContentFilterService contentFilterService,
        IMetadataEnhancerService metadataEnhancerService,
        INotificationFactory notificationFactory,
        Logger logger)
    {
        _artistService = artistService ?? throw new ArgumentNullException(nameof(artistService));
        _albumService = albumService ?? throw new ArgumentNullException(nameof(albumService));
        _contentFilterService = contentFilterService ?? throw new ArgumentNullException(nameof(contentFilterService));
        _metadataEnhancerService = metadataEnhancerService ?? throw new ArgumentNullException(nameof(metadataEnhancerService));
        _notificationFactory = notificationFactory ?? throw new ArgumentNullException(nameof(notificationFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void HandleAsync(ApplicationStartedEvent message)
    {
        var definitions = _notificationFactory.All()
            .Where(d => string.Equals(d.Implementation, nameof(MusicEnhancer), StringComparison.OrdinalIgnoreCase) &&
                        d.Settings is MusicEnhancerSettings { ScanLibraryOnStartup: true })
            .ToList();

        if (definitions.Count == 0)
        {
            _logger.Debug("MusicEnhancer: No connection has startup scanning enabled");
            return;
        }

        if (definitions.Count > 1)
        {
            _logger.Warn("MusicEnhancer: Multiple connections have startup scanning enabled; skipping to avoid conflicting settings. Enable it on only one connection.");
            return;
        }

        var settings = (MusicEnhancerSettings)definitions[0].Settings;
        var options = settings.ToFilterOptions();
        var artistsScanned = 0;
        var albumsScanned = 0;

        _logger.Info("MusicEnhancer: Starting library scan on startup (action={0})", options.VersionFilterAction);

        try
        {
            foreach (var artist in _artistService.GetAllArtists())
            {
                if (!artist.Monitored)
                {
                    continue;
                }

                artistsScanned++;

                try
                {
                    foreach (var album in _albumService.GetAlbumsByArtist(artist.Id).Where(a => a.Monitored))
                    {
                        try
                        {
                            var filteredTrackIds = _contentFilterService.FilterContentForArtist(artist, album, options);
                            _metadataEnhancerService.EnhanceMetadataForArtist(artist, album, options, filteredTrackIds);
                            albumsScanned++;
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex, "MusicEnhancer: Startup processing failed for album '{0}'", album.Title);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "MusicEnhancer: Startup processing failed for artist '{0}'", artist.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "MusicEnhancer: Startup library scan failed");
            return;
        }

        _logger.Info("MusicEnhancer: Startup library scan complete ({0} artists, {1} albums)", artistsScanned, albumsScanned);
    }
}
