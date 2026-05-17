using System;
using System.Collections.Generic;
using System.Linq;

namespace DisableMods.Core;

public static class ModKeyMatcher
{
    private static readonly char[] Separators = { ',', ';', ' ', '\r', '\n', '\t' };

    public static IReadOnlyList<string> ParsePatterns(string rawPatterns)
    {
        return (rawPatterns ?? string.Empty)
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(Normalize)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct()
            .ToList();
    }

    public static string Normalize(string value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant();
    }

    public static bool Matches(IEnumerable<string> patterns, string packageId)
    {
        var normalizedPackageId = Normalize(packageId);
        return patterns.Any(pattern => Matches(pattern, normalizedPackageId));
    }

    public static bool Matches(string pattern, string packageId)
    {
        pattern = Normalize(pattern);
        packageId = Normalize(packageId);

        if (pattern == packageId || pattern == "*")
        {
            return true;
        }

        var wildcardIndex = pattern.IndexOf('*');
        if (wildcardIndex < 0)
        {
            return false;
        }

        var currentIndex = 0;
        var firstSegment = true;
        foreach (var segment in pattern.Split(new[] { '*' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var segmentIndex = packageId.IndexOf(segment, currentIndex, StringComparison.Ordinal);
            if (segmentIndex < 0)
            {
                return false;
            }

            if (firstSegment && wildcardIndex > 0 && segmentIndex != 0)
            {
                return false;
            }

            currentIndex = segmentIndex + segment.Length;
            firstSegment = false;
        }

        return pattern.EndsWith("*", StringComparison.Ordinal) || currentIndex == packageId.Length;
    }
}
