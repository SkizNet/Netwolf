// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.PluginFramework.Commands;
using Netwolf.Transport.Extensions;
using Netwolf.Transport.State;

using System.Diagnostics.CodeAnalysis;


namespace Netwolf.Transport.IRC;

/// <summary>
/// Utility class for hostmask and casemapping-aware string comparisons
/// </summary>
public static class IrcUtil
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
    public static string Casefold(string str, CaseMapping caseMapping)
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

        return bytes.DecodeUtf8();
    }

    /// <summary>
    /// Splits an arbitrary hostmask into nick, ident, and host components.
    /// Missing components (when receiving only a nickname, only an ident@host,
    /// or only a hostname/servername) will be returned as empty strings.
    /// <para/>
    /// Supported formats:
    /// <list type="bullet">
    /// <item>nick</item>
    /// <item>ident@host</item>
    /// <item>nick!ident@host</item>
    /// <item>host (must contain <c>.</c>'s to be recognized as a bare host)</item>
    /// </list>
    /// </summary>
    /// <param name="mask"></param>
    /// <returns></returns>
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
            if (mask.Contains('.'))
            {
                host = mask;
            }
            else
            {
                nick = mask;
            }
        }

        if (mask.Contains('@'))
        {
            p = mask.Split('@', 2);
            ident = p[0];
            host = p[1];
        }

        return (nick, ident, host);
    }

    public static bool TryExtractUserFromSource(ICommand command, INetworkInfo networkInfo, [NotNullWhen(true)] out UserRecord? user)
    {
        user = null;
        if (command.Source == null)
        {
            return false;
        }

        var (nick, _, _) = SplitHostmask(command.Source);
        if (string.IsNullOrEmpty(nick) || nick.Contains('.'))
        {
            // if nick contains '.' then it's probably a server name, not a user
            return false;
        }

        user = networkInfo.GetUserByNick(nick);
        return user != null;
    }
}
