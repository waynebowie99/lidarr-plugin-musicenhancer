using System;
using System.Threading;

namespace NzbDrone.Core.Plugins;

internal static class MusicBrainzThrottle
{
    private static readonly object Gate = new();
    private static DateTime _nextRequest = DateTime.MinValue;

    public static void Wait()
    {
        lock (Gate)
        {
            var delay = _nextRequest - DateTime.UtcNow;
            if (delay > TimeSpan.Zero) Thread.Sleep(delay);
            _nextRequest = DateTime.UtcNow.AddMilliseconds(1100);
        }
    }
}
