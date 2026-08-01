namespace AspectN.Evaluation;

internal static class NamePatternMatcher
{
    public static bool IsMatch(
        string pattern,
        string candidate,
        bool ignoreCase = false)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(candidate);
#else
        if (pattern is null) throw new ArgumentNullException(nameof(pattern));
        if (candidate is null) throw new ArgumentNullException(nameof(candidate));
#endif

        var patternIndex = 0;
        var candidateIndex = 0;
        var starIndex = -1;
        var retryCandidateIndex = -1;
        while (candidateIndex < candidate.Length)
        {
            if (patternIndex < pattern.Length &&
                pattern[patternIndex] != '*' &&
                CharactersEqual(
                    pattern[patternIndex],
                    candidate[candidateIndex],
                    ignoreCase))
            {
                patternIndex++;
                candidateIndex++;
                continue;
            }

            if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                starIndex = patternIndex++;
                retryCandidateIndex = candidateIndex;
                continue;
            }

            if (starIndex < 0) return false;
            patternIndex = starIndex + 1;
            candidateIndex = ++retryCandidateIndex;
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
        {
            patternIndex++;
        }

        return patternIndex == pattern.Length;
    }

    private static bool CharactersEqual(
        char left,
        char right,
        bool ignoreCase)
    {
        return left == right ||
               (ignoreCase &&
                char.ToUpperInvariant(left) == char.ToUpperInvariant(right));
    }
}
