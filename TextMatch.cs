namespace AIFileButler;

internal static class TextMatch
{
    /// <summary>True if <paramref name="term"/> occurs in <paramref name="hay"/>
    /// bounded by non-letters on both sides — so "visa" matches "visa" and
    /// "visa_2024.pdf" but NOT "revisable", and a rule "cat" no longer fires on
    /// "certificate". Only letters count as word characters, so digits, spaces,
    /// underscores and punctuation all act as boundaries. Caller lower-cases both.</summary>
    public static bool ContainsWord(string hay, string term)
    {
        if (string.IsNullOrEmpty(term)) return false;
        int idx = 0;
        while ((idx = hay.IndexOf(term, idx, System.StringComparison.Ordinal)) >= 0)
        {
            bool leftOk = idx == 0 || !char.IsLetter(hay[idx - 1]);
            int end = idx + term.Length;
            bool rightOk = end >= hay.Length || !char.IsLetter(hay[end]);
            if (leftOk && rightOk) return true;
            idx++;
        }
        return false;
    }
}
