using System.Net;
using System.Reflection;
using System.Text;
using NLog;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Music;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Plugins;
using TagLib;
using TagLib.Id3v2;
using TagLib.Mpeg4;
using TagLib.Ogg;

if (Environment.GetEnvironmentVariable("MUSICENHANCER_LIVE_TEST") == "1")
{
    var config = new NLog.Config.LoggingConfiguration();
    config.AddRule(LogLevel.Debug, LogLevel.Fatal, new NLog.Targets.ConsoleTarget("console") { Layout = "${level} ${message} ${exception:format=tostring}" });
    LogManager.Configuration = config;
}

var testDir = Path.Combine(Path.GetTempPath(), "music-enhancer-test-" + Guid.NewGuid());
Directory.CreateDirectory(testDir);
try
{
    foreach (var ext in new[] { "mp3", "flac", "ogg", "opus", "m4a" })
    {
        foreach (var value in new[] { 1, 2 })
        {
            var path = CopyFixture(ext);
            Check(!AdvisoryTagWriter.HasRating(path), $"{ext} starts unrated");
            Check(AdvisoryTagWriter.WriteIfMissing(path, value), $"{ext} written");
            Check(AdvisoryTagWriter.HasRating(path), $"{ext} has rating");
            Check(AdvisoryTagWriter.IsKnownClean(path) == (value == 2), $"{ext} clean advisory detection");
            Check(!AdvisoryTagWriter.WriteIfMissing(path, value == 1 ? 2 : 1), $"{ext} existing rating preserved");
            using var audio = TagLib.File.Create(path);
            Check(audio.Tag.Title == "Original title" && audio.Tag.FirstPerformer == "Original artist", $"{ext} other metadata retained");
            var stored = ext switch
            {
                "mp3" => UserTextInformationFrame.Get((TagLib.Id3v2.Tag)audio.GetTag(TagTypes.Id3v2), "ITUNESADVISORY", false).Text[0],
                "m4a" => ((AppleTag)audio.GetTag(TagTypes.Apple)).DataBoxes(ByteVector.FromString("rtng", StringType.Latin1)).Single().Data[0].ToString(),
                _ => ((XiphComment)audio.GetTag(TagTypes.Xiph)).GetFirstField("ITUNESADVISORY")
            };
            Check(stored == (ext == "m4a" && value == 1 ? "4" : value.ToString()), $"{ext} expected rating {value}, got {stored}");
        }
    }

    var existingPath = CopyFixture("mp3");
    using (var audio = TagLib.File.Create(existingPath))
    {
        var id3 = (TagLib.Id3v2.Tag)audio.GetTag(TagTypes.Id3v2, true);
        UserTextInformationFrame.Get(id3, "explicit", true).Text = new[] { "0" };
        audio.Save();
    }
    Check(AdvisoryTagWriter.HasRating(existingPath) && !AdvisoryTagWriter.WriteIfMissing(existingPath, 1), "existing lower-case explicit field preserved");

    foreach (var ext in new[] { "mp3", "flac", "ogg", "opus", "m4a" })
    {
        var path = CopyFixture(ext);
        var fixtureArtist = new Artist { Name = "Album Artist", ForeignArtistId = "a0c9d75c-841e-4e8d-91d4-e20debfbc810" };
        var fixtureAlbum = new Album { Title = "Album Name", Genres = new List<string> { "Rock", "Pop" }, ReleaseDate = new DateTime(1998, 1, 1) };
        fixtureAlbum.SecondaryTypes.Add(SecondaryAlbumType.Compilation);
        var fixtureTrack = new Track { Title = "Do not overwrite", MediumNumber = 2, AbsoluteTrackNumber = 4 };
        fixtureTrack.ArtistMetadata = new ArtistMetadata { Name = "Other Artist", ForeignArtistId = "b0c9d75c-841e-4e8d-91d4-e20debfbc810" };
        var fixtureRelease = new AlbumRelease { ReleaseDate = new DateTime(2001, 1, 1), Label = new List<string> { "A Label" }, Disambiguation = "Deluxe", Country = new List<string> { "US" }, Media = new List<Medium> { new() { Number = 2, Format = "CD", Name = "Disc Two" } } };
        fixtureRelease.Tracks = new List<Track> { fixtureTrack, new() { MediumNumber = 2 } };
        fixtureTrack.AlbumRelease = fixtureRelease;
        var recording = new RecordingMetadata { Work = "The Work", CommunityTags = new[] { "rock", "happy" } };
        Check(LibraryMetadataWriter.WriteIfMissing(path, fixtureArtist, fixtureAlbum, fixtureTrack, 120, new[] { "Composer Name" }, new[] { "happy", "uplifting" }, recording), $"{ext} library tags written");
        using (var audio = TagLib.File.Create(path))
        {
            Check(audio.Tag.Title == "Original title" && audio.Tag.FirstPerformer == "Original artist", $"{ext} existing title and artist preserved");
            Check(audio.Tag.Album == "Album Name" && audio.Tag.FirstAlbumArtist == "Album Artist", $"{ext} album fields filled");
            Check(audio.Tag.Genres.Contains("Rock") && audio.Tag.BeatsPerMinute == 120 && audio.Tag.Composers.Contains("Composer Name"), $"{ext} genres, BPM and composer written");
            Check(audio.Tag.MusicBrainzTrackId == null, $"{ext} missing recording ID remains missing");
            Check(LibraryMetadataWriter.HasMood(audio), $"{ext} mood tag written");
            var extraFields = new SymfoniumFieldWriter(audio, "." + ext);
            Check(extraFields.Exists(new[] { "MEDIA" }, "TMED"), $"{ext} media tag written");
            Check(extraFields.Exists(new[] { "DISCSUBTITLE" }, "TSST"), $"{ext} disc subtitle written");
            Check(extraFields.Exists(new[] { "ORIGINALDATE" }, "TDOR", id3Fallback: "TORY"), $"{ext} original year written");
            Check(extraFields.Exists(new[] { "WORK" }, "TIT1"), $"{ext} work written");
            Check(extraFields.Exists(new[] { "TRACK_TAGS" }), $"{ext} track tags written");
            Check(extraFields.Exists(new[] { "ALBUMVERSION" }), $"{ext} album version written");
            Check(audio.Tag.TrackCount == 2, $"{ext} per-disc track count written");
            if (ext == "mp3")
            {
                var id3 = (TagLib.Id3v2.Tag)audio.GetTag(TagTypes.Id3v2);
                string Text(string id) => id3.GetFrames<TextInformationFrame>(ByteVector.FromString(id, StringType.Latin1)).Single().Text[0];
                Check(Text("TMED") == "CD" && Text("TSST") == "Disc Two" && Text("TIT1") == "The Work", "ID3 standard fields have correct values");
                Check(UserTextInformationFrame.Get(id3, "COUNTRY", false).Text[0] == "US" && id3.IsCompilation, "ID3 country and compilation");
            }
            else if (ext == "m4a")
            {
                var apple = (AppleTag)audio.GetTag(TagTypes.Apple);
                Check(apple.GetDashBox("com.apple.iTunes", "MEDIA") == "CD" && apple.GetDashBox("com.apple.iTunes", "WORK") == "The Work", "Apple freeform fields have correct values");
                Check(apple.GetDashBox("com.apple.iTunes", "RELEASECOUNTRY") == "US" && apple.IsCompilation, "Apple country and compilation");
            }
            else
            {
                var xiph = (XiphComment)audio.GetTag(TagTypes.Xiph);
                Check(xiph.GetFirstField("MEDIA") == "CD" && xiph.GetFirstField("WORK") == "The Work" && xiph.GetFirstField("ORIGINALDATE") == "1998", $"{ext} fields have correct values");
                Check(xiph.GetFirstField("RELEASECOUNTRY") == "US" && xiph.GetFirstField("COMPILATION") == "1", $"{ext} country and compilation");
            }
        }
        Check(!LibraryMetadataWriter.WriteIfMissing(path, fixtureArtist, fixtureAlbum, fixtureTrack, 90, new[] { "Different Composer" }, new[] { "sad" }, recording), $"{ext} repeat run preserves metadata");
        Check(LyricsSidecarWriter.WriteIfMissing(path, "[00:01.00]Test lyrics"), $"{ext} lyrics sidecar added");
        Check(!LyricsSidecarWriter.WriteIfMissing(path, "Other lyrics"), $"{ext} lyrics sidecar preserved");
    }

    foreach (var ext in new[] { "mp3", "flac", "ogg", "opus", "m4a" })
    {
        var path = CopyFixture(ext);
        using (var audio = TagLib.File.Create(path))
        {
            audio.Tag.Performers = Array.Empty<string>();
            audio.Save();
        }
        var metadata = new RecordingMetadata { Artists = new[] { "First Artist", "Second Artist" }, ArtistSortNames = new[] { "Artist, First", "Artist, Second" } };
        LibraryMetadataWriter.WriteIfMissing(path, new Artist(), new Album(), new Track(), recording: metadata);
        using var tagged = TagLib.File.Create(path);
        Check(tagged.Tag.Performers.SequenceEqual(metadata.Artists), $"{ext} separate recording artists round trip");
    }

    var mismatchPath = CopyFixture("flac");
    using (var audio = TagLib.File.Create(mismatchPath))
    {
        audio.Tag.Album = "Different Album";
        audio.Save();
    }
    var mismatchedTrack = new Track { Title = "Different Track", ForeignRecordingId = "a0c9d75c-841e-4e8d-91d4-e20debfbc810" };
    var mismatchedAlbum = new Album { Title = "Album", ForeignAlbumId = "b0c9d75c-841e-4e8d-91d4-e20debfbc810" };
    LibraryMetadataWriter.WriteIfMissing(mismatchPath, new Artist(), mismatchedAlbum, mismatchedTrack);
    using (var audio = TagLib.File.Create(mismatchPath))
    {
        Check(string.IsNullOrEmpty(audio.Tag.MusicBrainzTrackId) && string.IsNullOrEmpty(audio.Tag.MusicBrainzReleaseGroupId), "do not attach IDs to conflicting existing titles");
    }

    var preservedPath = CopyFixture("flac");
    using (var audio = TagLib.File.Create(preservedPath))
    {
        audio.Tag.Genres = new[] { "My Genre" };
        audio.Tag.Composers = new[] { "My Composer" };
        audio.Tag.BeatsPerMinute = 99;
        audio.Tag.Lyrics = "My existing lyrics";
        audio.Save();
    }

    var appleExisting = CopyFixture("m4a");
    using (var audio = TagLib.File.Create(appleExisting))
    {
        var apple = (AppleTag)audio.GetTag(TagTypes.Apple, true);
        apple.SetDashBox("com.apple.iTunes", "mood", "Existing Mood");
        apple.SetDashBox("com.apple.iTunes", "albumversion", "Existing Edition");
        apple.SetDashBox("com.apple.iTunes", "itunesadvisory", "0");
        apple.SetDashBox("com.apple.iTunes", "lyrics", "Existing Lyrics");
        audio.Save();
    }
    using (var audio = TagLib.File.Create(appleExisting))
    {
        Check(LibraryMetadataWriter.HasMood(audio), "case-insensitive Apple mood preserved");
        Check(new SymfoniumFieldWriter(audio, ".m4a").Exists(new[] { "ALBUMVERSION" }), "case-insensitive Apple edition preserved");
    }
    Check(AdvisoryTagWriter.HasRating(appleExisting) && LyricsSidecarWriter.HasLyrics(appleExisting), "case-insensitive Apple advisory and lyrics preserved");
    Check(LyricsSidecarWriter.HasLyrics(preservedPath) && !LyricsSidecarWriter.WriteIfMissing(preservedPath, "Replacement"), "embedded lyrics preserved");
    var preservedTrack = new Track { Title = "Other" };
    LibraryMetadataWriter.WriteIfMissing(preservedPath, new Artist { Name = "Artist" }, new Album { Genres = new List<string> { "Other Genre" } }, preservedTrack, 120, new[] { "Other Composer" });
    using (var audio = TagLib.File.Create(preservedPath))
    {
        Check(audio.Tag.FirstGenre == "My Genre" && audio.Tag.FirstComposer == "My Composer" && audio.Tag.BeatsPerMinute == 99 && audio.Tag.Lyrics == "My existing lyrics", "existing genre, credits, tempo and lyrics preserved");
    }

    foreach (var title in new[] { "Alive", "Deliver Me", "Credit", "Flip", "Song - Live and Let Die", "Song (Remastered 2011)", "Song (Original Version)", "Cleanin' Out My Closet" })
        Check(VersionMatcher.MatchVersion(new Track { Title = title }) == null, $"ordinary title not filtered: {title}");
    foreach (var (title, type) in new[] { ("Song (Radio Edit)", VersionType.Edit), ("Song (Clean Edit)", VersionType.Clean), ("Song (Live at Wembley)", VersionType.Live), ("Song - Remix", VersionType.Remix), ("Album Commentary", VersionType.Commentary), ("Song (Extended Mix)", VersionType.Version) })
        Check(VersionMatcher.MatchVersion(new Track { Title = title })?.VersionType == type, $"version marker detected: {title}");

    var filterTracks = new List<Track>
    {
        new() { Id = 1, TrackFileId = 101, Title = "Song (Remix)", Duration = 180000 },
        new() { Id = 2, TrackFileId = 102, Title = "Song (Radio Edit)", Duration = 180000 },
        new() { Id = 3, TrackFileId = 103, Title = "Song (Live)", Duration = 180000 },
        new() { Id = 4, TrackFileId = 104, Title = "Song (Clean)", Duration = 180000 },
        new() { Id = 5, TrackFileId = 105, Title = "Alive", Duration = 180000 }
    };
    var deleted = new List<int>();
    var unmonitored = 0;
    var sharedFile = 0;
    var filterPaths = filterTracks.ToDictionary(t => t.TrackFileId, t => TrackFixture(t.Title));
    var filterTrackService = Stub<ITrackService>.Create((method, args) => method.Name switch
    {
        "GetTracksByAlbum" => filterTracks,
        "GetTracksByFileId" => LinkedTracks(args),
        _ => throw new Exception(method.Name)
    });
    var filterFiles = Stub<IMediaFileService>.Create((method, args) => method.Name == "Get" ? FilterFile(args) : throw new Exception(method.Name));
    var deleter = Stub<IDeleteMediaFiles>.Create((method, args) => Delete(args));
    var albums = Stub<IAlbumService>.Create((method, args) => Unmonitor());
    var filter = new ContentFilterService(LogManager.GetCurrentClassLogger(), filterTrackService, filterFiles, deleter, albums);
    var filterArtist = new Artist { Path = testDir };
    if (OperatingSystem.IsLinux())
    {
        var link = Path.Combine(testDir, "linked-audio.mp3");
        System.IO.File.CreateSymbolicLink(link, filterPaths[101]);
        Check(!MusicFileIdentity.IsWithinArtistFolder(filterArtist, link), "symlinked files are not treated as safe writes");
    }
    var filterSettings = new MusicEnhancerSettings();
    Check(!filterSettings.DeleteFilesAfterUnmonitor, "file deletion defaults off for new settings");
    var oldSettings = System.Text.Json.JsonSerializer.Deserialize<MusicEnhancerSettings>("{\"DeleteFilesAfterUnmonitor\":true,\"DiscogsApiSecret\":\"saved-value\"}")!;
    Check(oldSettings.DeleteFilesAfterUnmonitor && oldSettings.DiscogsApiSecret == "saved-value" && !oldSettings.EnableRecordingMetadataLookup, "existing settings and legacy secret retained");
    var reviewCandidates = filter.FilterContentForArtist(filterArtist, new Album { Id = 4 }, filterSettings.ToFilterOptions());
    Check(reviewCandidates.SetEquals(new[] { 1, 2, 3 }), "filter returns only selected unwanted track IDs");
    Check(deleted.Count == 0 && unmonitored == 0, "flag-only never deletes or unmonitors");
    filterSettings.VersionFilterAction = VersionFilterAction.Unmonitor;
    filter.FilterContentForArtist(filterArtist, new Album { Id = 4 }, filterSettings.ToFilterOptions());
    Check(deleted.Count == 0 && unmonitored == 0, "mixed album retained when deletion disabled");
    filterSettings.DeleteFilesAfterUnmonitor = true;
    sharedFile = 102;
    filter.FilterContentForArtist(filterArtist, new Album { Id = 4 }, filterSettings.ToFilterOptions());
    Check(deleted.SequenceEqual(new[] { 101, 103 }) && unmonitored == 0, "only matched unshared files deleted even when majority match");
    deleted.Clear();
    filterSettings.VersionFilterAction = (VersionFilterAction)999;
    Check(!filterSettings.Validate().IsValid, "invalid filter action fails validation");
    filter.FilterContentForArtist(filterArtist, new Album { Id = 4 }, filterSettings.ToFilterOptions());
    Check(deleted.Count == 0 && unmonitored == 0, "invalid action is non-destructive");
    filterSettings.VersionFilterAction = VersionFilterAction.Unmonitor;
    filterTracks.Clear();
    filterTracks.Add(new Track { Id = 6, TrackFileId = 106, Title = "Commentary", Duration = 1000 });
    filterTracks.Add(new Track { Id = 7, TrackFileId = 0, Title = "Undownloaded original", Duration = 180000 });
    filterPaths[106] = TrackFixture("Commentary");
    filter.FilterContentForArtist(filterArtist, new Album { Id = 4 }, filterSettings.ToFilterOptions());
    Check(deleted.SequenceEqual(new[] { 106 }) && unmonitored == 0, "short commentary detected and undownloaded track prevents album unmonitor");
    deleted.Clear();
    filterTracks.RemoveAt(1);
    filter.FilterContentForArtist(filterArtist, new Album { Id = 4 }, filterSettings.ToFilterOptions());
    Check(deleted.SequenceEqual(new[] { 106 }) && unmonitored == 1, "all-matched album can be unmonitored");
    deleted.Clear();
    using (var audio = TagLib.File.Create(filterPaths[106]))
    {
        audio.Tag.MusicBrainzTrackId = "a0c9d75c-841e-4e8d-91d4-e20debfbc810";
        audio.Save();
    }
    filterTracks[0].ForeignRecordingId = "b0c9d75c-841e-4e8d-91d4-e20debfbc810";
    filter.FilterContentForArtist(filterArtist, new Album { Id = 4 }, filterSettings.ToFilterOptions());
    Check(deleted.Count == 0 && unmonitored == 1, "recording ID conflict prevents deletion and unmonitoring");
    filterPaths[106] = TrackFixture("Song (Radio Edit)");
    Check(AdvisoryTagWriter.WriteIfMissing(filterPaths[106], 2), "clean advisory fixture created");
    filterTracks[0].ForeignRecordingId = null!;
    filterTracks[0].Title = "Song (Radio Edit)";
    filterTracks[0].Duration = 180000;
    filter.FilterContentForArtist(filterArtist, new Album { Id = 4 }, filterSettings.ToFilterOptions());
    Check(deleted.Count == 0 && unmonitored == 1, "clean advisory protects edit when keep-clean is enabled");
    filterSettings.KeepCleanVersions = false;
    filter.FilterContentForArtist(filterArtist, new Album { Id = 4 }, filterSettings.ToFilterOptions());
    Check(deleted.SequenceEqual(new[] { 106 }) && unmonitored == 2, "clean advisory can be filtered only when explicitly disabled");
    deleted.Clear();
    filterSettings.KeepCleanVersions = true;
    filterPaths[106] = Path.Combine(testDir, "missing.mp3");
    filter.FilterContentForArtist(filterArtist, new Album { Id = 4 }, filterSettings.ToFilterOptions());
    Check(deleted.Count == 0 && unmonitored == 2, "unreadable clean advisory prevents destructive action");
    filterPaths[106] = TrackFixture("Ordinary Song");
    filter.FilterContentForArtist(filterArtist, new Album { Id = 4 }, filterSettings.ToFilterOptions());
    Check(deleted.Count == 0 && unmonitored == 2, "unverified file title prevents destructive action");
    filterPaths[106] = TrackFixture("Different Song (Radio Edit)");
    filter.FilterContentForArtist(filterArtist, new Album { Id = 4 }, filterSettings.ToFilterOptions());
    Check(deleted.Count == 0 && unmonitored == 2, "same version marker on a different title is not proof of identity");
    using (var audio = TagLib.File.Create(filterPaths[106]))
    {
        audio.Tag.MusicBrainzTrackId = "a0c9d75c-841e-4e8d-91d4-e20debfbc810";
        audio.Save();
    }
    filterTracks[0].ForeignRecordingId = "a0c9d75c-841e-4e8d-91d4-e20debfbc810";
    filter.FilterContentForArtist(filterArtist, new Album { Id = 4 }, filterSettings.ToFilterOptions());
    Check(deleted.SequenceEqual(new[] { 106 }) && unmonitored == 3, "matching embedded recording ID verifies version");
    deleted.Clear();
    filterTracks[0].ForeignRecordingId = null!;
    filterPaths[106] = TrackFixture("Song (Radio Edit)");
    filterArtist.Path = Path.Combine(testDir, "other-artist");
    filter.FilterContentForArtist(filterArtist, new Album { Id = 4 }, filterSettings.ToFilterOptions());
    Check(deleted.Count == 0 && unmonitored == 3, "files outside artist folder prevent destructive action");
    filterArtist.Path = testDir;
    filterSettings.ReleaseTypesToFilter = "Compilation";
    var compilation = new Album { Id = 4, AlbumType = "Album" };
    compilation.SecondaryTypes.Add(SecondaryAlbumType.Compilation);
    filter.FilterContentForArtist(filterArtist, compilation, filterSettings.ToFilterOptions());
    Check(deleted.SequenceEqual(new[] { 106 }) && unmonitored == 4, "secondary compilation type can be selected");

    var startupDefinitions = new List<NotificationDefinition>
    {
        new() { Implementation = "MusicEnhancer", Settings = new MusicEnhancerSettings() },
        new() { Implementation = "MusicEnhancer", Settings = new MusicEnhancerSettings { ScanLibraryOnStartup = true } }
    };
    var startupAlbums = new List<Album> { new() { Id = 10, Monitored = true }, new() { Id = 11, Monitored = true } };
    var startupFilterCount = 0;
    var startupMetadataCount = 0;
    var factory = Stub<INotificationFactory>.Create((method, args) => method.Name == "All" ? startupDefinitions : throw new Exception(method.Name));
    var startupArtists = Stub<IArtistService>.Create((method, args) => method.Name == "GetAllArtists" ? new List<Artist> { new() { Monitored = true } } : throw new Exception(method.Name));
    var startupAlbumService = Stub<IAlbumService>.Create((method, args) => method.Name == "GetAlbumsByArtist" ? startupAlbums : throw new Exception(method.Name));
    var startupFilter = Stub<IContentFilterService>.Create((method, args) => StartupFilter(args));
    var startupMetadata = Stub<IMetadataEnhancerService>.Create((method, args) => StartupMetadata());
    var startup = new MusicEnhancerStartupService(startupArtists, startupAlbumService, startupFilter, startupMetadata, factory, LogManager.GetCurrentClassLogger());
    startup.HandleAsync(new ApplicationStartedEvent());
    Check(startupFilterCount == 2 && startupMetadataCount == 1, "startup selects enabled connection and continues after an album error");
    startupDefinitions.Add(new NotificationDefinition { Implementation = "MusicEnhancer", Settings = new MusicEnhancerSettings { ScanLibraryOnStartup = true } });
    startup.HandleAsync(new ApplicationStartedEvent());
    Check(startupFilterCount == 2, "startup refuses conflicting enabled connections");

    var filePath = CopyFixture("flac");
    var track = new Track { Id = 7, TrackFileId = 12, Title = "Song", Explicit = true };
    var trackFile = new TrackFile { Id = 12, Path = filePath };
    var shared = false;
    var updates = 0;
    var tracks = Stub<ITrackService>.Create((method, args) => method.Name switch
    {
        "GetTracksByAlbum" => new List<Track> { track },
        "GetTracksByFileId" => shared ? new List<Track> { track, new Track { Id = 8 } } : new List<Track> { track },
        _ => throw new Exception(method.Name)
    });
    var files = Stub<IMediaFileService>.Create((method, args) => method.Name switch
    {
        "Get" => trackFile,
        "Update" => Update(),
        _ => throw new Exception(method.Name)
    });
    var lookup = new FakeLookup();
    var additional = new FakeAdditionalLookup();
    var fileNotifications = 0;
    var watcher = Stub<IRootFolderWatchingService>.Create((method, args) => Watch());
    var service = new MetadataEnhancerService(tracks, files, lookup, additional, watcher, LogManager.GetCurrentClassLogger());
    var settings = new MusicEnhancerSettings();
    var artist = new Artist { Path = testDir };
    var album = new Album { Id = 3 };

    service.EnhanceMetadataForArtist(artist, album, settings.ToFilterOptions());
    Check(!AdvisoryTagWriter.HasRating(filePath), "metadata processing requires opt-in");
    settings.EnableMetadataProcessing = true;
    settings.EnableMetadataWriting = false;
    service.EnhanceMetadataForArtist(artist, album, settings.ToFilterOptions());
    Check(!AdvisoryTagWriter.HasRating(filePath), "disabled writes do nothing");
    settings.EnableMetadataWriting = true;
    settings.EnableLibraryTagging = true;
    settings.EnableRecordingMetadataLookup = true;
    settings.EnableOnlineExplicitLookup = true;
    service.EnhanceMetadataForArtist(artist, album, settings.ToFilterOptions(), new HashSet<int> { track.Id });
    Check(lookup.Calls == 0 && additional.TotalCalls == 0 && !AdvisoryTagWriter.HasRating(filePath), "filtered tracks are excluded before all metadata lookups");
    settings.EnableLibraryTagging = false;
    settings.EnableRecordingMetadataLookup = false;
    settings.EnableOnlineExplicitLookup = false;
    shared = true;
    service.EnhanceMetadataForArtist(artist, album, settings.ToFilterOptions());
    Check(!AdvisoryTagWriter.HasRating(filePath), "shared file skipped");
    shared = false;
    track.Title = "Song (Clean)";
    service.EnhanceMetadataForArtist(artist, album, settings.ToFilterOptions());
    Check(!AdvisoryTagWriter.HasRating(filePath), "conflicting clean/explicit skipped");
    track.Explicit = false;
    track.Title = "Song";
    service.EnhanceMetadataForArtist(artist, album, settings.ToFilterOptions());
    Check(!AdvisoryTagWriter.HasRating(filePath) && lookup.Calls == 0, "unknown and online off untouched");
    settings.EnableOnlineExplicitLookup = true;
    lookup.Result = null;
    service.EnhanceMetadataForArtist(artist, album, settings.ToFilterOptions());
    Check(!AdvisoryTagWriter.HasRating(filePath), "unknown does not mean clean");
    lookup.Result = AdvisoryRating.Explicit;
    service.EnhanceMetadataForArtist(artist, album, settings.ToFilterOptions());
    Check(AdvisoryTagWriter.HasRating(filePath) && updates == 1 && fileNotifications == 1 && trackFile.Size == new FileInfo(filePath).Length, "positive lookup writes and updates Lidarr file and watcher");
    service.EnhanceMetadataForArtist(artist, album, settings.ToFilterOptions());
    Check(updates == 1 && fileNotifications == 1, "repeat run does not rewrite or notify watcher");
    trackFile.Path = CopyFixture("ogg");
    track.Title = "Song (Clean)";
    track.Explicit = false;
    service.EnhanceMetadataForArtist(artist, album, settings.ToFilterOptions());
    using (var taggedClean = TagLib.File.Create(trackFile.Path))
    {
        Check(((XiphComment)taggedClean.GetTag(TagTypes.Xiph)).GetFirstField("ITUNESADVISORY") == "2", "clean title writes clean rating");
    }
    settings.EnableExplicitTagging = false;
    settings.EnableLibraryTagging = true;
    settings.EnableLyricsLookup = true;
    settings.EnableComposerLookup = true;
    album.Title = "Fixture Album";
    album.Genres = new List<string> { "Rock" };
    track.ArtistMetadata = new ArtistMetadata { Name = "Fixture Artist" };
    additional.Lyrics = "[00:01.00]Test";
    service.EnhanceMetadataForArtist(artist, album, settings.ToFilterOptions());
    Check(System.IO.File.Exists(Path.ChangeExtension(trackFile.Path, ".lrc")), "service adds sidecar independently of explicit tagging");
    Check(additional.MaximumConcurrent >= 2, "independent lyrics and MusicBrainz lookups run concurrently");
    using (var tagged = TagLib.File.Create(trackFile.Path))
    {
        Check(tagged.Tag.Album == "Fixture Album" && tagged.Tag.Genres.Contains("Rock"), "service fills library tags even when advisory exists");
    }
    settings.EnableRecordingMetadataLookup = true;
    var beforeRecordingMetadata = additional.RecordingCalls;
    additional.Recording = new RecordingMetadata { Work = "The Work", CommunityTags = new[] { "rock" } };
    service.EnhanceMetadataForArtist(artist, album, settings.ToFilterOptions());
    using (var tagged = TagLib.File.Create(trackFile.Path))
    {
        Check(additional.RecordingCalls == beforeRecordingMetadata + 1 && ((XiphComment)tagged.GetTag(TagTypes.Xiph)).GetFirstField("WORK") == "The Work", "service writes optional recording metadata");
    }
    trackFile.Path = CopyFixture("flac");
    using (var audio = TagLib.File.Create(trackFile.Path))
    {
        audio.Tag.MusicBrainzTrackId = "a0c9d75c-841e-4e8d-91d4-e20debfbc810";
        audio.Save();
    }
    track.ForeignRecordingId = "b0c9d75c-841e-4e8d-91d4-e20debfbc810";
    track.Explicit = true;
    settings.EnableExplicitTagging = true;
    var beforeConflict = updates;
    service.EnhanceMetadataForArtist(artist, album, settings.ToFilterOptions());
    Check(updates == beforeConflict && !AdvisoryTagWriter.HasRating(trackFile.Path), "recording ID conflict prevents metadata changes");

    track.ForeignRecordingId = null!;
    track.Title = "Complete Song";
    track.Explicit = false;
    album.Title = "Complete Album";
    track.ArtistMetadata = new ArtistMetadata { Name = "Fixture Artist" };
    trackFile.Path = CopyFixture("flac");
    using (var audio = TagLib.File.Create(trackFile.Path))
    {
        audio.Tag.Title = track.Title;
        audio.Tag.Album = album.Title;
        audio.Tag.Performers = new[] { "Fixture Artist" };
        audio.Tag.AlbumArtists = new[] { "Fixture Artist" };
        audio.Tag.Genres = new[] { "Rock" };
        audio.Tag.BeatsPerMinute = 120;
        audio.Tag.Composers = new[] { "Composer" };
        audio.Tag.Lyrics = "Existing lyrics";
        var xiph = (XiphComment)audio.GetTag(TagTypes.Xiph, true);
        xiph.SetField("MOOD", "happy");
        xiph.SetField("WORK", "Complete Work");
        xiph.SetField("TRACK_TAGS", "rock");
        audio.Save();
    }
    AdvisoryTagWriter.WriteIfMissing(trackFile.Path, 1);
    settings.EnableLibraryTagging = true;
    settings.EnableRecordingMetadataLookup = true;
    settings.EnableBpmLookup = true;
    settings.EnableComposerLookup = true;
    settings.EnableMoodLookup = true;
    settings.EnableLyricsLookup = true;
    settings.EnableExplicitTagging = true;
    settings.EnableOnlineExplicitLookup = true;
    var beforeFullMetadata = additional.TotalCalls;
    var beforeFullExplicit = lookup.Calls;
    service.EnhanceMetadataForArtist(artist, album, settings.ToFilterOptions());
    Check(additional.TotalCalls == beforeFullMetadata && lookup.Calls == beforeFullExplicit,
        "complete metadata, lyrics and advisory trigger no online lookups");

    var mock = new LookupHandler();
    var online = new ExplicitLookupService(LogManager.GetCurrentClassLogger(), new HttpClient(mock));
    var lookupTrack = new Track { ForeignRecordingId = "a0c9d75c-841e-4e8d-91d4-e20debfbc810", Duration = 208000 };
    Check(online.Lookup(lookupTrack) == AdvisoryRating.Explicit && mock.Calls == 2, "MusicBrainz ISRC to Deezer explicit");
    Check(online.Lookup(lookupTrack) == AdvisoryRating.Explicit && mock.Calls == 2, "lookup cache");
    mock.Conflict = true;
    lookupTrack.ForeignRecordingId = "b0c9d75c-841e-4e8d-91d4-e20debfbc810";
    Check(online.Lookup(lookupTrack) == null, "conflicting ISRC results skipped");
    mock.Conflict = false;
    mock.Note = "clean";
    lookupTrack.ForeignRecordingId = "c0c9d75c-841e-4e8d-91d4-e20debfbc810";
    Check(online.Lookup(lookupTrack) == AdvisoryRating.Clean, "MusicBrainz clean disambiguation");
    lookupTrack.ForeignRecordingId = "d0c9d75c-841e-4e8d-91d4-e20debfbc810";
    lookupTrack.Duration = 100000;
    Check(online.Lookup(lookupTrack) == null, "MusicBrainz duration mismatch rejected");
    lookupTrack.Duration = 208000;
    lookupTrack.ForeignRecordingId = "e0c9d75c-841e-4e8d-91d4-e20debfbc810";
    Check(online.Lookup(lookupTrack, new RecordingMetadata { Isrcs = new[] { "USUM71703861" } }) == AdvisoryRating.Explicit, "known ISRC avoids duplicate MusicBrainz request");

    var extraMock = new AdditionalLookupHandler();
    var extra = new AdditionalMetadataLookupService(LogManager.GetCurrentClassLogger(), new HttpClient(extraMock));
    Check(extra.LookupBpm("USUM71703861", 208000) == 115, "Deezer BPM rounded and matched by ISRC");
    Check(extra.LookupBpm("USUM71703861", 100000) == null, "BPM duration mismatch skipped");
    Check(extra.LookupLyrics("The Artist", "The Album", "Song", 208000) == "[00:01.00]Lyrics", "LRCLIB lyrics matched");
    Check(extra.LookupLyrics("The Artist", "The Album", "Song (Clean)", 208000) == null, "different lyric version skipped");
    var composerTrack = new Track { ForeignRecordingId = "a0c9d75c-841e-4e8d-91d4-e20debfbc810", Duration = 208000 };
    var recordingLookup = extra.LookupRecordingMetadata(composerTrack);
    Check(recordingLookup?.Artists?.SequenceEqual(new[] { "First Artist", "Second Artist" }) == true && recordingLookup.Work == "The Work" &&
          recordingLookup.Composers?.SequenceEqual(new[] { "Composer Name" }) == true && recordingLookup.Isrcs?.SequenceEqual(new[] { "USUM71703861" }) == true,
          "MusicBrainz recording credits, composer, ISRC and work");
    Check(recordingLookup?.CommunityTags?.SequenceEqual(new[] { "rock", "happy" }) == true && recordingLookup.Moods?.SequenceEqual(new[] { "happy" }) == true, "MusicBrainz positive community tags and moods");
    composerTrack.Duration = 100000;
    Check(extra.LookupRecordingMetadata(composerTrack) == null, "recording metadata duration mismatch skipped");
    composerTrack.Duration = 208000;
    Check(extra.LookupMoods(composerTrack, "test-key")?.SequenceEqual(new[] { "happy", "calm" }) == true, "Last.fm mood whitelist");
    Check(extra.LookupMoods(composerTrack, null) == null, "mood lookup requires API key");
    if (Environment.GetEnvironmentVariable("MUSICENHANCER_LIVE_TEST") == "1")
    {
        var live = new ExplicitLookupService(LogManager.GetCurrentClassLogger());
        Check(live.Lookup(new Track { ForeignRecordingId = "398bf241-7d57-4993-860b-1f9ef496c97f", Duration = 177000 }) == AdvisoryRating.Explicit,
            "live MusicBrainz explicit disambiguation");
        Check(live.Lookup(new Track { ForeignRecordingId = "2463ba13-6f03-4d0a-a0e9-e15939d828bc", Duration = 207959 }) == null,
            "live Deezer non-explicit is not a clean edit");
        var liveExtra = new AdditionalMetadataLookupService(LogManager.GetCurrentClassLogger());
        Check(liveExtra.LookupBpm("USUM71703861", 207959) == 115, "live Deezer BPM");
        Check(!string.IsNullOrEmpty(liveExtra.LookupLyrics("Borislav Slavov", "Baldur's Gate 3 (Original Game Soundtrack)", "I Want to Live", 233000)), "live LRCLIB lyrics");
    }
    Console.WriteLine("All metadata tests passed.");

    object? Update() { updates++; return null; }
    TrackFile FilterFile(object?[]? args)
    {
        var id = (int)args![0]!;
        return new TrackFile { Id = id, Path = filterPaths.GetValueOrDefault(id, "") };
    }
    object? Watch() { fileNotifications++; return null; }
    List<Track> LinkedTracks(object?[]? args)
    {
        var id = (int)args![0]!;
        return filterTracks.Where(t => t.TrackFileId == id).Concat(id == sharedFile ? new[] { new Track { Id = 99 } } : Array.Empty<Track>()).ToList();
    }
    object? Delete(object?[]? args) { deleted.Add(((TrackFile)args![1]!).Id); return null; }
    object? Unmonitor() { unmonitored++; return null; }
    object? StartupFilter(object?[]? args)
    {
        startupFilterCount++;
        if (((Album)args![1]!).Id == 10) throw new InvalidOperationException("synthetic album failure");
        return new HashSet<int> { 100 };
    }
    object? StartupMetadata() { startupMetadataCount++; return null; }
    string CopyFixture(string ext)
    {
        var target = Path.Combine(testDir, Guid.NewGuid() + "." + ext);
        System.IO.File.Copy(Path.Combine(AppContext.BaseDirectory, "fixtures", "sample." + ext), target);
        return target;
    }
    string TrackFixture(string title)
    {
        var target = CopyFixture("mp3");
        using var audio = TagLib.File.Create(target);
        audio.Tag.Title = title;
        audio.Save();
        return target;
    }
}
finally
{
    Directory.Delete(testDir, true);
}

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception("FAILED: " + message);
}

public class Stub<T> : DispatchProxy where T : class
{
    private Func<MethodInfo, object?[]?, object?> _invoke = null!;
    public static T Create(Func<MethodInfo, object?[]?, object?> invoke)
    {
        var proxy = Create<T, Stub<T>>();
        ((Stub<T>)(object)proxy)._invoke = invoke;
        return proxy;
    }
    protected override object? Invoke(MethodInfo? method, object?[]? args) => _invoke(method!, args);
}

public class FakeLookup : IExplicitLookupService
{
    public AdvisoryRating? Result;
    public int Calls;
    public AdvisoryRating? Lookup(Track track) { Calls++; return Result; }
    public AdvisoryRating? Lookup(Track track, RecordingMetadata? metadata, bool recordingLookupAttempted = false) { Calls++; return metadata?.AdvisoryRating ?? Result; }
}

public class FakeAdditionalLookup : IAdditionalMetadataLookupService
{
    public string? Lyrics;
    public RecordingMetadata? Recording;
    public int RecordingCalls;
    public int TotalCalls;
    private int _active;
    private int _maximumConcurrent;
    public int MaximumConcurrent => System.Threading.Volatile.Read(ref _maximumConcurrent);
    private void Probe()
    {
        System.Threading.Interlocked.Increment(ref TotalCalls);
        var active = System.Threading.Interlocked.Increment(ref _active);
        int observed;
        while (active > (observed = System.Threading.Volatile.Read(ref _maximumConcurrent)) &&
               System.Threading.Interlocked.CompareExchange(ref _maximumConcurrent, active, observed) != observed) { }
        System.Threading.Thread.Sleep(40);
        System.Threading.Interlocked.Decrement(ref _active);
    }
    public uint? LookupBpm(string? isrc, int durationMs) { Probe(); return null; }
    public string? LookupLyrics(string? artist, string? album, string? title, int durationMs) { Probe(); return Lyrics; }
    public string[]? LookupMoods(Track track, string? apiKey) { Probe(); return null; }
    public RecordingMetadata? LookupRecordingMetadata(Track track) { Probe(); System.Threading.Interlocked.Increment(ref RecordingCalls); return Recording; }
}

public class LookupHandler : HttpMessageHandler
{
    public int Calls;
    public bool Conflict;
    public string Note = "";
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        var url = request.RequestUri!.ToString();
        var id = request.RequestUri!.AbsolutePath.Split('/').Last();
        var body = url.Contains("musicbrainz.org")
            ? $"{{\"id\":\"{id}\",\"length\":208000,\"disambiguation\":\"{Note}\",\"isrcs\":[\"USUM71703861\"{(Conflict ? ",\"USUM71703862\"" : "")}]}}"
            : url.EndsWith("USUM71703862")
                ? "{\"isrc\":\"USUM71703862\",\"duration\":208,\"explicit_lyrics\":false}"
                : "{\"isrc\":\"USUM71703861\",\"duration\":208,\"explicit_lyrics\":true}";
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}

public class AdditionalLookupHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        var body = uri.Host == "ws.audioscrobbler.com"
            ? "{\"toptags\":{\"tag\":[{\"name\":\"rock\"},{\"name\":\"Happy\"},{\"name\":\"calm\"}]}}"
            : uri.Host == "api.deezer.com"
            ? "{\"isrc\":\"USUM71703861\",\"duration\":208,\"bpm\":114.84}"
            : uri.Host == "musicbrainz.org" && uri.AbsolutePath.Contains("/recording/") && uri.Query.Contains("tags")
                ? "{\"id\":\"a0c9d75c-841e-4e8d-91d4-e20debfbc810\",\"length\":208000,\"isrcs\":[\"USUM71703861\"],\"artist-credit\":[{\"name\":\"First Artist\",\"artist\":{\"sort-name\":\"Artist, First\"}},{\"name\":\"Second Artist\",\"artist\":{\"sort-name\":\"Artist, Second\"}}],\"relations\":[{\"type\":\"performance\",\"work\":{\"id\":\"b0c9d75c-841e-4e8d-91d4-e20debfbc810\",\"title\":\"The Work\",\"relations\":[{\"type\":\"composer\",\"artist\":{\"name\":\"Composer Name\"}}]}}],\"tags\":[{\"name\":\"rock\",\"count\":5},{\"name\":\"happy\",\"count\":1},{\"name\":\"ignored\",\"count\":0},{\"name\":\"bad;tag\",\"count\":2}]}"
            : uri.Host == "musicbrainz.org" && uri.AbsolutePath.Contains("/recording/")
                ? "{\"id\":\"a0c9d75c-841e-4e8d-91d4-e20debfbc810\",\"length\":208000,\"relations\":[{\"type\":\"performance\",\"work\":{\"id\":\"b0c9d75c-841e-4e8d-91d4-e20debfbc810\"}}]}"
            : uri.Host == "musicbrainz.org"
                ? "{\"id\":\"b0c9d75c-841e-4e8d-91d4-e20debfbc810\",\"relations\":[{\"type\":\"composer\",\"artist\":{\"name\":\"Composer Name\"}},{\"type\":\"lyricist\",\"artist\":{\"name\":\"Not A Composer\"}}]}"
                : "{\"artistName\":\"The Artist\",\"albumName\":\"The Album\",\"trackName\":\"Song\",\"duration\":208,\"instrumental\":false,\"syncedLyrics\":\"[00:01.00]Lyrics\"}";
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
