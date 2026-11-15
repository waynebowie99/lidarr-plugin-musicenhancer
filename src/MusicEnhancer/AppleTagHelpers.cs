using System;
using System.Collections.Generic;
using System.Linq;
using TagLib;
using TagLib.Mpeg4;

namespace NzbDrone.Core.Plugins;

internal static class AppleTagHelpers
{
    public static bool HasFreeform(AppleTag apple, params string[] aliases)
    {
        return MatchingBoxes(apple, aliases).Any();
    }

    public static IEnumerable<string> ReadFreeform(AppleTag apple, params string[] aliases)
    {
        return MatchingBoxes(apple, aliases).SelectMany(box => box.Children.OfType<AppleDataBox>())
            .Select(box => box.Text).Where(text => !string.IsNullOrWhiteSpace(text));
    }

    private static IEnumerable<AppleAnnotationBox> MatchingBoxes(AppleTag apple, string[] aliases)
    {
        foreach (var box in apple.OfType<AppleAnnotationBox>())
        {
            if (box.BoxType.ToString(StringType.Latin1) != "----") continue;
            var fields = box.Children.OfType<AppleAdditionalInfoBox>().ToList();
            var mean = fields.FirstOrDefault(x => x.BoxType.ToString(StringType.Latin1) == "mean")?.Text;
            var name = fields.FirstOrDefault(x => x.BoxType.ToString(StringType.Latin1) == "name")?.Text;
            if (string.Equals(mean, "com.apple.iTunes", StringComparison.OrdinalIgnoreCase) &&
                aliases.Any(alias => string.Equals(alias, name, StringComparison.OrdinalIgnoreCase))) yield return box;
        }
    }
}
