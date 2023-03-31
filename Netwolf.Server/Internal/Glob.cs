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
    /// Construct a new Glob
    /// </summary>
    /// <param name="patterns">Patterns containing * and ? wildcards</param>
    internal Glob(params string[] patterns)
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
                _ = sb.Append(new string('?', m.Value.Count(c => c == '?')));
                _ = sb.Append(m.Value.Contains('*') ? "*" : String.Empty);
                return sb.ToString();
            });

            // normalize pattern
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
            }

            Automota.MarkAccepting(next);
        }

        Automota.Compile();
    }

    /// <summary>
    /// Determine if this Glob matches the text.
    /// </summary>
    /// <param name="text">Text to match, must be </param>
    /// <returns></returns>
    internal bool IsMatch(string text)
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
}
