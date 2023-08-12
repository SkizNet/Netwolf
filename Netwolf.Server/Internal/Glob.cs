using System.Text;
using System.Text.RegularExpressions;

namespace Netwolf.Server.Internal;

/// <summary>
/// Pattern matching based on an NFA.
/// Supports * and ? wildcards, with no ability to escape.
/// </summary>
internal partial class Glob
{
    [GeneratedRegex("[?*]+")]
    private static partial Regex GlobRegex();

    private readonly NFA Automota = new();

    /// <summary>
    /// Retrieve a cached Glob, or construct a new one if no cached one is found
    /// </summary>
    /// <param name="patterns">A disjunction of patterns containing * and ? wildcards</param>
    /// <returns>A Glob instance</returns>
    internal static Glob For(params string[] patterns)
    {
        // TODO: implement cache
        return new Glob(patterns);
    }

    /// <summary>
    /// Construct a new Glob
    /// </summary>
    /// <param name="patterns">Patterns containing * and ? wildcards</param>
    private Glob(params string[] patterns)
    {
        int cur = 0;
        int next = 0;
        foreach (string p in patterns)
        {
            // rearrange pathological patterns containing repeated *? sequences
            // to move all ?s to the front followed by at most one *
            string pattern = GlobRegex().Replace(p, m =>
            {
                var sb = new StringBuilder();
                sb.Append(new string('?', m.Value.Count(c => c == '?')));
                sb.Append(m.Value.Contains('*') ? "*" : String.Empty);
                return sb.ToString();
            });

            // unicode normalization of pattern
            pattern = pattern.Normalize();

            // build an NFA to process the pattern
            cur = Automota.NewState();
            Automota.AddEpsilon(0, cur);

            foreach (char c in pattern)
            {
                next = Automota.NewState();

                switch (c)
                {
                    case '*':
                        Automota.AddAny(cur, cur);
                        Automota.AddEpsilon(cur, next);
                        break;
                    case '?':
                        Automota.AddAny(cur, next);
                        break;
                    default:
                        Automota.AddTransition(cur, c, next);
                        break;
                }

                cur = next;
            }

            Automota.MarkAccepting(cur);
        }

        Automota.Compile();
    }

    /// <summary>
    /// Determine if this Glob matches the text.
    /// </summary>
    /// <param name="text">Text to match</param>
    /// <returns></returns>
    public bool IsMatch(string text)
    {
        try
        {
            text = text.Normalize();
        }
        catch (ArgumentException)
        {
            // swallow exception and work on the raw un-normalized text
        }

        return Automota.Parse(text);
    }

    /// <summary>
    /// Determine if this Glob matches at least one of the specified texts.
    /// </summary>
    /// <param name="texts">Text strings to match</param>
    /// <returns></returns>
    public bool MatchAny(params string[] texts)
    {
        foreach (var text in texts)
        {
            if (IsMatch(text))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determine if this Glob matches all of the specified texts.
    /// </summary>
    /// <param name="texts">Text strings to match</param>
    /// <returns></returns>
    public bool MatchAll(params string[] texts)
    {
        foreach (var text in texts)
        {
            if (!IsMatch(text))
            {
                return false;
            }
        }

        return true;
    }
}
