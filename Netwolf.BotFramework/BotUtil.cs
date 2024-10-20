using Netwolf.Transport.IRC;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework;

/// <summary>
/// Utility class for hostmask and casemapping-aware string comparisons
/// </summary>
public static class BotUtil
{
    /// <summary>
    /// Check if the two strings are equal case-insensitively according to the provided
    /// <paramref name="caseMapping"/>.
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <param name="caseMapping"></param>
    /// <returns></returns>
    public static bool IrcEquals(string? a, string? b, CaseMapping caseMapping)
    {
        if (a == null && b == null)
        {
            return true;
        }

        if (a == null || b == null)
        {
            return false;
        }

        // compare byte-by-byte since none of the currently-supported casemappings are Unicode-aware
        // and the ircds similarly use algorithms that transform bytewise via lookup tables
        var u8a = Encoding.UTF8.GetBytes(a);
        var u8b = Encoding.UTF8.GetBytes(b);

        if (u8a.Length != u8b.Length)
        {
            return false;
        }

        byte upper = caseMapping switch
        {
            CaseMapping.Ascii => 122,
            CaseMapping.Rfc1459 => 126,
            CaseMapping.Rfc1459Strict => 125,
            // Treat unknown case mapping as Ascii
            _ => 122
        };
        
        for (int i = 0; i < u8a.Length; i++)
        {
            var x = (u8a[i] >= 97 && u8a[i] <= upper) ? (u8a[i] - 32) : u8a[i];
            var y = (u8b[i] >= 97 && u8b[i] <= upper) ? (u8b[i] - 32) : u8b[i];

            if (x != y)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Check if a comma-separated list of values case-insensitively contains a given item given a particular <paramref name="caseMapping"/>.
    /// </summary>
    /// <param name="list"></param>
    /// <param name="item"></param>
    /// <param name="caseMapping"></param>
    /// <returns></returns>
    public static bool CommaListContains(string list, string item, CaseMapping caseMapping)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(item);

        return list.Split(',').Any(i => IrcEquals(i, item, caseMapping));
    }
}
