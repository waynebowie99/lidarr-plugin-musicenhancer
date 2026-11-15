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
    var service = new MetadataEnhancerService(tracks, files, lookup, LogManager.GetCurrentClassLogger());
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
    if (Environment.GetEnvironmentVariable("MUSICENHANCER_LIVE_TEST") == "1")
    {
        var live = new ExplicitLookupService(LogManager.GetCurrentClassLogger());
        Check(live.Lookup(new Track { ForeignRecordingId = "398bf241-7d57-4993-860b-1f9ef496c97f", Duration = 177000 }) == AdvisoryRating.Explicit,
            "live MusicBrainz explicit disambiguation");
        Check(live.Lookup(new Track { ForeignRecordingId = "2463ba13-6f03-4d0a-a0e9-e15939d828bc", Duration = 207959 }) == null,
            "live Deezer non-explicit is not a clean edit");
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
