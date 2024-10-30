using Microsoft.CodeAnalysis.CSharp.Syntax;

using Netwolf.Transport.Extensions;
using Netwolf.Transport.IRC;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
        return Enumerable.SequenceEqual(Casefold(a, caseMapping), Casefold(b, caseMapping));
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

    /// <summary>
    /// Return a casefolded version of the given string, suitable for storing as a lookup key,
    /// but *NOT* suitable for display.
    /// </summary>
    /// <param name="str">String to casefold</param>
    /// <param name="caseMapping"></param>
    /// <returns>Byte array of the casefolded string</returns>
    public static byte[] Casefold(string str, CaseMapping caseMapping)
    {
        ArgumentNullException.ThrowIfNull(str);
        var bytes = str.EncodeUtf8();
        byte upper = caseMapping switch
        {
            CaseMapping.Ascii => 122,
            CaseMapping.Rfc1459 => 126,
            CaseMapping.Rfc1459Strict => 125,
            // Treat unknown case mapping as Ascii
            _ => 122
        };

        for (int i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] >= 97 && bytes[i] <= upper)
            {
                bytes[i] -= 32;
            }
        }

        return bytes;
    }

    public static (string Nick, string Ident, string Host) SplitHostmask(string mask)
    {
        string[] p;
        string nick = string.Empty;
        string ident = string.Empty;
        string host = string.Empty;

        if (mask.Contains('!'))
        {
            p = mask.Split('!', 2);
            nick = p[0];
            mask = p[1];
        }
        else if (!mask.Contains('@'))
        {
            nick = mask;
        }

        if (mask.Contains('@'))
        {
            p = mask.Split('@', 2);
            ident = p[0];
            host = p[1];
        }

        return (nick, ident, host);
    }
}
