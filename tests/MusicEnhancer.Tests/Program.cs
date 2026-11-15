using System.Net;
using System.Reflection;
using System.Text;
using NLog;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Music;
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
        var fixtureAlbum = new Album { Title = "Album Name", Genres = new List<string> { "Rock", "Pop" } };
        var fixtureTrack = new Track { Title = "Do not overwrite", MediumNumber = 2, AbsoluteTrackNumber = 4 };
        fixtureTrack.ArtistMetadata = new ArtistMetadata { Name = "Other Artist", ForeignArtistId = "b0c9d75c-841e-4e8d-91d4-e20debfbc810" };
        fixtureTrack.AlbumRelease = new AlbumRelease { ReleaseDate = new DateTime(2001, 1, 1), Label = new List<string> { "A Label" } };
        Check(LibraryMetadataWriter.WriteIfMissing(path, fixtureArtist, fixtureAlbum, fixtureTrack, 120, new[] { "Composer Name" }, new[] { "happy", "uplifting" }), $"{ext} library tags written");
        using (var audio = TagLib.File.Create(path))
        {
            Check(audio.Tag.Title == "Original title" && audio.Tag.FirstPerformer == "Original artist", $"{ext} existing title and artist preserved");
            Check(audio.Tag.Album == "Album Name" && audio.Tag.FirstAlbumArtist == "Album Artist", $"{ext} album fields filled");
            Check(audio.Tag.Genres.Contains("Rock") && audio.Tag.BeatsPerMinute == 120 && audio.Tag.Composers.Contains("Composer Name"), $"{ext} genres, BPM and composer written");
            Check(audio.Tag.MusicBrainzTrackId == null, $"{ext} missing recording ID remains missing");
            Check(LibraryMetadataWriter.HasMood(audio), $"{ext} mood tag written");
        }
        Check(!LibraryMetadataWriter.WriteIfMissing(path, fixtureArtist, fixtureAlbum, fixtureTrack, 90, new[] { "Different Composer" }, new[] { "sad" }), $"{ext} repeat run preserves metadata");
        Check(LyricsSidecarWriter.WriteIfMissing(path, "[00:01.00]Test lyrics"), $"{ext} lyrics sidecar added");
        Check(!LyricsSidecarWriter.WriteIfMissing(path, "Other lyrics"), $"{ext} lyrics sidecar preserved");
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
    Check(LyricsSidecarWriter.HasLyrics(preservedPath) && !LyricsSidecarWriter.WriteIfMissing(preservedPath, "Replacement"), "embedded lyrics preserved");
    var preservedTrack = new Track { Title = "Other" };
    LibraryMetadataWriter.WriteIfMissing(preservedPath, new Artist { Name = "Artist" }, new Album { Genres = new List<string> { "Other Genre" } }, preservedTrack, 120, new[] { "Other Composer" });
    using (var audio = TagLib.File.Create(preservedPath))
    {
        Check(audio.Tag.FirstGenre == "My Genre" && audio.Tag.FirstComposer == "My Composer" && audio.Tag.BeatsPerMinute == 99 && audio.Tag.Lyrics == "My existing lyrics", "existing genre, credits, tempo and lyrics preserved");
    }

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
    var service = new MetadataEnhancerService(tracks, files, lookup, additional, LogManager.GetCurrentClassLogger());
    var settings = new MusicEnhancerSettings();
    var artist = new Artist();
    var album = new Album { Id = 3 };

    service.EnhanceMetadataForArtist(artist, album, settings.ToFilterOptions());
    Check(!AdvisoryTagWriter.HasRating(filePath), "metadata processing requires opt-in");
    settings.EnableMetadataProcessing = true;
    settings.EnableMetadataWriting = false;
    service.EnhanceMetadataForArtist(artist, album, settings.ToFilterOptions());
    Check(!AdvisoryTagWriter.HasRating(filePath), "disabled writes do nothing");
    settings.EnableMetadataWriting = true;
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
    Check(AdvisoryTagWriter.HasRating(filePath) && updates == 1 && trackFile.Size == new FileInfo(filePath).Length, "positive lookup writes and updates Lidarr file");
    service.EnhanceMetadataForArtist(artist, album, settings.ToFilterOptions());
    Check(updates == 1, "repeat run does not rewrite");
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
    album.Title = "Fixture Album";
    album.Genres = new List<string> { "Rock" };
    track.ArtistMetadata = new ArtistMetadata { Name = "Fixture Artist" };
    additional.Lyrics = "[00:01.00]Test";
    service.EnhanceMetadataForArtist(artist, album, settings.ToFilterOptions());
    Check(System.IO.File.Exists(Path.ChangeExtension(trackFile.Path, ".lrc")), "service adds sidecar independently of explicit tagging");
    using (var tagged = TagLib.File.Create(trackFile.Path))
    {
        Check(tagged.Tag.Album == "Fixture Album" && tagged.Tag.Genres.Contains("Rock"), "service fills library tags even when advisory exists");
    }

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
    Check(online.LookupSingleIsrc(lookupTrack) == "USUM71703861", "single MusicBrainz ISRC for BPM");

    var extraMock = new AdditionalLookupHandler();
    var extra = new AdditionalMetadataLookupService(LogManager.GetCurrentClassLogger(), new HttpClient(extraMock));
    Check(extra.LookupBpm("USUM71703861", 208000) == 115, "Deezer BPM rounded and matched by ISRC");
    Check(extra.LookupBpm("USUM71703861", 100000) == null, "BPM duration mismatch skipped");
    Check(extra.LookupLyrics("The Artist", "The Album", "Song", 208000) == "[00:01.00]Lyrics", "LRCLIB lyrics matched");
    Check(extra.LookupLyrics("The Artist", "The Album", "Song (Clean)", 208000) == null, "different lyric version skipped");
    var composerTrack = new Track { ForeignRecordingId = "a0c9d75c-841e-4e8d-91d4-e20debfbc810", Duration = 208000 };
    Check(extra.LookupComposers(composerTrack)?.SequenceEqual(new[] { "Composer Name" }) == true, "MusicBrainz composer credits only");
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
    string CopyFixture(string ext)
    {
        var target = Path.Combine(testDir, Guid.NewGuid() + "." + ext);
        System.IO.File.Copy(Path.Combine(AppContext.BaseDirectory, "fixtures", "sample." + ext), target);
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
    public string? LookupSingleIsrc(Track track) => null;
}

public class FakeAdditionalLookup : IAdditionalMetadataLookupService
{
    public string? Lyrics;
    public uint? LookupBpm(string? isrc, int durationMs) => null;
    public string? LookupLyrics(string? artist, string? album, string? title, int durationMs) => Lyrics;
    public string[]? LookupComposers(Track track) => null;
    public string[]? LookupMoods(Track track, string? apiKey) => null;
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
            : uri.Host == "musicbrainz.org" && uri.AbsolutePath.Contains("/recording/")
                ? "{\"id\":\"a0c9d75c-841e-4e8d-91d4-e20debfbc810\",\"length\":208000,\"relations\":[{\"type\":\"performance\",\"work\":{\"id\":\"b0c9d75c-841e-4e8d-91d4-e20debfbc810\"}}]}"
            : uri.Host == "musicbrainz.org"
                ? "{\"id\":\"b0c9d75c-841e-4e8d-91d4-e20debfbc810\",\"relations\":[{\"type\":\"composer\",\"artist\":{\"name\":\"Composer Name\"}},{\"type\":\"lyricist\",\"artist\":{\"name\":\"Not A Composer\"}}]}"
                : "{\"artistName\":\"The Artist\",\"albumName\":\"The Album\",\"trackName\":\"Song\",\"duration\":208,\"instrumental\":false,\"syncedLyrics\":\"[00:01.00]Lyrics\"}";
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
