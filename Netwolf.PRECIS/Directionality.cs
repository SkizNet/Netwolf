// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.PRECIS.Internal;

namespace Netwolf.PRECIS;

public static class Directionality
{
    /// <summary>
    /// Do not use any directionality rule.
    /// </summary>
    public static readonly Func<string, bool>? None = null;

    /// <summary>
    /// Applies the Bidi rule as defined in RFC 5893.
    /// </summary>
    public static readonly Func<string, bool> Bidi = input =>
    {
        if (input == "")
        {
            // empty strings cannot pass conditions 1, 3, or 6.
            return false;
        }

        var classes = input.EnumerateRunes().Select(UnicodeProperty.GetBidiClass).ToList();

        // The first character must be a character with Bidi property L, R,
        // or AL.  If it has the R or AL property, it is an RTL label; if it
        // has the L property, it is an LTR label.
        bool? isRtl = classes[0] switch
        {
            BidiClass.L => false,
            BidiClass.R => true,
            BidiClass.AL => true,
            _ => null
        };

        if (isRtl is null)
        {
            // first character is not L, R, or AL
            return false;
        }
        else if (isRtl == true)
        {
            // In an RTL label, only characters with the Bidi properties R, AL,
            // AN, EN, ES, CS, ET, ON, BN, or NSM are allowed.
            // This is represented by the AllowedInRtl bit flag.
            if (!classes.All(c => c.HasFlag(BidiClass.AllowedInRtl)))
            {
                return false;
            }

            // In an RTL label, the end of the label must be a character with
            // Bidi property R, AL, EN, or AN, followed by zero or more
            // characters with Bidi property NSM.
            // This is represented by the RtlFinisher bit flag.
            if (!classes.AsEnumerable().Reverse().SkipWhile(c => c == BidiClass.NSM).First().HasFlag(BidiClass.RtlFinisher))
            {
                return false;
            }

            // In an RTL label, if an EN is present, no AN may be present, and
            // vice versa.
            if (classes.Contains(BidiClass.EN) && classes.Contains(BidiClass.AN))
            {
                return false;
            }
        }
        else
        {
            // In an LTR label, only characters with the Bidi properties L, EN,
            // ES, CS, ET, ON, BN, or NSM are allowed.
            // This is represented by the AllowedInLtr bit flag.
            if (!classes.All(c => c.HasFlag(BidiClass.AllowedInLtr)))
            {
                return false;
            }

            // In an LTR label, the end of the label must be a character with
            // Bidi property L or EN, followed by zero or more characters with
            // Bidi property NSM.
            // This is represented by the LtrFinisher bit flag.
            if (!classes.AsEnumerable().Reverse().SkipWhile(c => c == BidiClass.NSM).First().HasFlag(BidiClass.LtrFinisher))
            {
                return false;
            }
        }

        // If we got here, all rules passed
        return true;
    };
}
