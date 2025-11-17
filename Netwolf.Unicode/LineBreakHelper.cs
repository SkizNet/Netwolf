// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Unicode.Internal;

using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Netwolf.Unicode;

/// <summary>
/// Implementation of TR14, revision 55 for Unicode 17.0.0
/// https://www.unicode.org/reports/tr14/tr14-55.html
/// </summary>
public static class LineBreakHelper
{
    private static readonly Dictionary<(LineBreakClass Prev, LineBreakClass Cur), (LineBreakType? PrevType, LineBreakType? CurType, int? PrevRule, int? CurRule)> _mapping = new()
    {
        // LB1 Assign a line breaking class to each code point of the input. Resolve AI, CB, CJ, SA, SG, and XX into other line breaking classes depending on criteria outside the scope of this algorithm.
        // handled in Grapheme constructor
        // LB2 Never break at the start of text.
        { (LineBreakClass.StartOfText, LineBreakClass.Any), (LineBreakType.Forbidden, null, 200, null) },
        // LB3 Always break at the end of text.
        // handled by injecting a new value at the end with a mandatory break
        // LB4 Always break after hard line breaks.
        { (LineBreakClass.Any, LineBreakClass.BK), (LineBreakType.Forbidden, LineBreakType.Mandatory, 600, 400) },
        // LB5 Treat CR followed by LF, as well as CR, LF, and NL as hard line breaks.
        // LB6 Do not break before hard line breaks.
        { (LineBreakClass.CR, LineBreakClass.LF), (LineBreakType.Forbidden, null, 500, null) },
        { (LineBreakClass.Any, LineBreakClass.CR), (LineBreakType.Forbidden, LineBreakType.Mandatory, 600, 501) },
        { (LineBreakClass.Any, LineBreakClass.LF), (LineBreakType.Forbidden, LineBreakType.Mandatory, 600, 501) },
        { (LineBreakClass.Any, LineBreakClass.NL), (LineBreakType.Forbidden, LineBreakType.Mandatory, 600, 501) },
        // LB7 Do not break before spaces or zero width space.
        // Also LB18 Break after spaces needs to go here to prevent duplicate key errors
        { (LineBreakClass.Any, LineBreakClass.SP), (LineBreakType.Forbidden, LineBreakType.Optional, 700, 1800) },
        { (LineBreakClass.Any, LineBreakClass.ZW), (LineBreakType.Forbidden, null, 700, null) },
        // LB8 Break before any character following a zero-width space, even if one or more spaces intervene.
        // handled in SplitText() as this rule cannot be encoded in a pairwise lookup table
        // LB8a Do not break after a zero width joiner.
        { (LineBreakClass.Any, LineBreakClass.ZWJ), (null, LineBreakType.Forbidden, null, 810) },
        // LB9 Do not break a combining character sequence; treat it as if it has the line breaking class of the base character in all of the following rules. Treat ZWJ as if it were CM.
        // handled in SplitText() as this rule cannot be encoded in a pairwise lookup table
        // LB10 Treat any remaining combining mark or ZWJ as AL.
        // directly encoded as additional options in all rules where AL appears further down
        // LB11 Do not break before or after Word joiner and related characters.
        { (LineBreakClass.Any, LineBreakClass.WJ), (LineBreakType.Forbidden, LineBreakType.Forbidden, 1100, 1100) },
        // LB12 Do not break after NBSP and related characters.
        { (LineBreakClass.Any, LineBreakClass.GL), (null, LineBreakType.Forbidden, null, 1200) },
        // LB12a Do not break before NBSP and related characters, except after spaces and hyphens.
        // handled in SplitText() to avoid bloating the table with an extra 40-something rules that encode all options except for the excluded set
        // LB13 Do not break before ‘]’ or ‘!’ or ‘/’, even after spaces.
        { (LineBreakClass.Any, LineBreakClass.CL), (LineBreakType.Forbidden, null, 1300, null) },
        { (LineBreakClass.Any, LineBreakClass.CP), (LineBreakType.Forbidden, null, 1300, null) },
        { (LineBreakClass.Any, LineBreakClass.EX), (LineBreakType.Forbidden, null, 1300, null) },
        { (LineBreakClass.Any, LineBreakClass.SY), (LineBreakType.Forbidden, null, 1300, null) },
        // LB14 Do not break after ‘[’, even after spaces.
        // handled in SplitText() as this rule cannot be encoded in a pairwise lookup table
        // LB15a Do not break after an unresolved initial punctuation that lies at the start of the line, after a space, after opening punctuation, or after an unresolved quotation mark, even after spaces.
        // handled in SplitText() as this rule cannot be encoded in a pairwise lookup table
        // LB15b Do not break before an unresolved final punctuation that lies at the end of the line, before a space, before a prohibited break, or before an unresolved quotation mark, even after spaces.
        // handled in SplitText() as this rule cannot be encoded in a pairwise lookup table
        // LB15c Break before a decimal mark that follows a space, for instance, in ‘subtract .5’.
        // handled in SplitText() as this rule cannot be encoded in a pairwise lookup table
        // LB15d Otherwise, do not break before ‘;’, ‘,’, or ‘.’, even after spaces.
        { (LineBreakClass.Any, LineBreakClass.IS), (LineBreakType.Forbidden, null, 1540, null) },
        // LB16 Do not break between closing punctuation and a nonstarter (lb=NS), even with intervening spaces.
        // handled in SplitText() as this rule cannot be encoded in a pairwise lookup table
        // LB17 Do not break within ‘——’, even with intervening spaces.
        // handled in SplitText() as this rule cannot be encoded in a pairwise lookup table
        // LB18 Break after spaces.
        // handled above since (Any, SP) was in use for LB7
        // LB19 Do not break before non-initial unresolved quotation marks, such as ‘ ” ’ or ‘ " ’, nor after non-final unresolved quotation marks, such as ‘ “ ’ or ‘ " ’.
        // handled in SplitText() as this rule cannot be encoded in a pairwise lookup table
        // LB19a Unless surrounded by East Asian characters, do not break either side of any unresolved quotation marks.
        // handled in SplitText() as this rule cannot be encoded in a pairwise lookup table
        // LB20 Break before and after unresolved CB.
        { (LineBreakClass.Any, LineBreakClass.CB), (LineBreakType.Optional, LineBreakType.Optional, 2000, 2000) },
        // LB20a Do not break after a word-initial hyphen.
        // handled in SplitText() as this rule cannot be encoded in a pairwise lookup table
        // LB21 Do not break before hyphen-minus, other hyphens, fixed-width spaces, small kana, and other non-starters, or after acute accents.
        { (LineBreakClass.Any, LineBreakClass.BA), (LineBreakType.Forbidden, null, 2100, null) },
        { (LineBreakClass.Any, LineBreakClass.HH), (LineBreakType.Forbidden, null, 2100, null) },
        { (LineBreakClass.Any, LineBreakClass.HY), (LineBreakType.Forbidden, null, 2100, null) },
        { (LineBreakClass.Any, LineBreakClass.NS), (LineBreakType.Forbidden, null, 2100, null) },
        { (LineBreakClass.Any, LineBreakClass.BB), (null, LineBreakType.Forbidden, null, 2100) },
        // LB21a Do not break after the hyphen in Hebrew + Hyphen + non-Hebrew.
        // handled in SplitText() as this rule cannot be encoded in a pairwise lookup table
        // LB21b Do not break between Solidus and Hebrew letters.
        { (LineBreakClass.SY, LineBreakClass.HL), (LineBreakType.Forbidden, null, 2120, null) },
        // LB22 Do not break before ellipses.
        { (LineBreakClass.Any, LineBreakClass.IN), (LineBreakType.Forbidden, null, 2200, null) },
        // LB23 Do not break between digits and letters.
        { (LineBreakClass.AL, LineBreakClass.NU), (LineBreakType.Forbidden, null, 2300, null) },
        { (LineBreakClass.HL, LineBreakClass.NU), (LineBreakType.Forbidden, null, 2300, null) },
        { (LineBreakClass.NU, LineBreakClass.AL), (LineBreakType.Forbidden, null, 2300, null) },
        { (LineBreakClass.NU, LineBreakClass.HL), (LineBreakType.Forbidden, null, 2300, null) },
        // LB23a Do not break between numeric prefixes and ideographs, or between ideographs and numeric postfixes.
        { (LineBreakClass.PR, LineBreakClass.ID), (LineBreakType.Forbidden, null, 2310, null) },
        { (LineBreakClass.PR, LineBreakClass.EB), (LineBreakType.Forbidden, null, 2310, null) },
        { (LineBreakClass.PR, LineBreakClass.EM), (LineBreakType.Forbidden, null, 2310, null) },
        { (LineBreakClass.ID, LineBreakClass.PO), (LineBreakType.Forbidden, null, 2310, null) },
        { (LineBreakClass.EB, LineBreakClass.PO), (LineBreakType.Forbidden, null, 2310, null) },
        { (LineBreakClass.EM, LineBreakClass.PO), (LineBreakType.Forbidden, null, 2310, null) },
        // LB24 Do not break between numeric prefix/postfix and letters, or between letters and prefix/postfix.
        { (LineBreakClass.PR, LineBreakClass.AL), (LineBreakType.Forbidden, null, 2400, null) },
        { (LineBreakClass.PR, LineBreakClass.HL), (LineBreakType.Forbidden, null, 2400, null) },
        { (LineBreakClass.PO, LineBreakClass.AL), (LineBreakType.Forbidden, null, 2400, null) },
        { (LineBreakClass.PO, LineBreakClass.HL), (LineBreakType.Forbidden, null, 2400, null) },
        { (LineBreakClass.AL, LineBreakClass.PR), (LineBreakType.Forbidden, null, 2400, null) },
        { (LineBreakClass.AL, LineBreakClass.PO), (LineBreakType.Forbidden, null, 2400, null) },
        { (LineBreakClass.HL, LineBreakClass.PR), (LineBreakType.Forbidden, null, 2400, null) },
        { (LineBreakClass.HL, LineBreakClass.PO), (LineBreakType.Forbidden, null, 2400, null) },
        // LB25 Do not break numbers
        // Additional rules handled in SplitText() as they cannot be encoded in a pairwise lookup table
        { (LineBreakClass.PO, LineBreakClass.NU), (LineBreakType.Forbidden, null, 2500, null) },
        { (LineBreakClass.PR, LineBreakClass.NU), (LineBreakType.Forbidden, null, 2500, null) },
        { (LineBreakClass.HY, LineBreakClass.NU), (LineBreakType.Forbidden, null, 2500, null) },
        { (LineBreakClass.IS, LineBreakClass.NU), (LineBreakType.Forbidden, null, 2500, null) },
        // LB26 Do not break a Korean syllable.
        { (LineBreakClass.JL, LineBreakClass.JL), (LineBreakType.Forbidden, null, 2600, null) },
        { (LineBreakClass.JL, LineBreakClass.JV), (LineBreakType.Forbidden, null, 2600, null) },
        { (LineBreakClass.JL, LineBreakClass.H2), (LineBreakType.Forbidden, null, 2600, null) },
        { (LineBreakClass.JL, LineBreakClass.H3), (LineBreakType.Forbidden, null, 2600, null) },
        { (LineBreakClass.JV, LineBreakClass.JV), (LineBreakType.Forbidden, null, 2600, null) },
        { (LineBreakClass.JV, LineBreakClass.JT), (LineBreakType.Forbidden, null, 2600, null) },
        { (LineBreakClass.H2, LineBreakClass.JV), (LineBreakType.Forbidden, null, 2600, null) },
        { (LineBreakClass.H2, LineBreakClass.JT), (LineBreakType.Forbidden, null, 2600, null) },
        { (LineBreakClass.JT, LineBreakClass.JT), (LineBreakType.Forbidden, null, 2600, null) },
        { (LineBreakClass.H3, LineBreakClass.JT), (LineBreakType.Forbidden, null, 2600, null) },
        // LB27 Treat a Korean Syllable Block the same as ID.
        { (LineBreakClass.JL, LineBreakClass.PO), (LineBreakType.Forbidden, null, 2700, null) },
        { (LineBreakClass.JV, LineBreakClass.PO), (LineBreakType.Forbidden, null, 2700, null) },
        { (LineBreakClass.JT, LineBreakClass.PO), (LineBreakType.Forbidden, null, 2700, null) },
        { (LineBreakClass.H2, LineBreakClass.PO), (LineBreakType.Forbidden, null, 2700, null) },
        { (LineBreakClass.H3, LineBreakClass.PO), (LineBreakType.Forbidden, null, 2700, null) },
        { (LineBreakClass.PR, LineBreakClass.JL), (LineBreakType.Forbidden, null, 2700, null) },
        { (LineBreakClass.PR, LineBreakClass.JV), (LineBreakType.Forbidden, null, 2700, null) },
        { (LineBreakClass.PR, LineBreakClass.JT), (LineBreakType.Forbidden, null, 2700, null) },
        { (LineBreakClass.PR, LineBreakClass.H2), (LineBreakType.Forbidden, null, 2700, null) },
        { (LineBreakClass.PR, LineBreakClass.H3), (LineBreakType.Forbidden, null, 2700, null) },
        // LB28 Do not break between alphabetics (“at”).
        { (LineBreakClass.AL, LineBreakClass.AL), (LineBreakType.Forbidden, null, 2800, null) },
        { (LineBreakClass.AL, LineBreakClass.HL), (LineBreakType.Forbidden, null, 2800, null) },
        { (LineBreakClass.HL, LineBreakClass.AL), (LineBreakType.Forbidden, null, 2800, null) },
        { (LineBreakClass.HL, LineBreakClass.HL), (LineBreakType.Forbidden, null, 2800, null) },
        // LB28a Do not break inside the orthographic syllables of Brahmic scripts.
        // Adjustments to accomodate U+25CC DOTTED CIRCLE as well as rules that cannot be encoded in a pairwise lookup table are handled in SplitText()
        { (LineBreakClass.AP, LineBreakClass.AK), (LineBreakType.Forbidden, null, 2810, null) },
        { (LineBreakClass.AP, LineBreakClass.AS), (LineBreakType.Forbidden, null, 2810, null) },
        { (LineBreakClass.AK, LineBreakClass.VF), (LineBreakType.Forbidden, null, 2810, null) },
        { (LineBreakClass.AS, LineBreakClass.VF), (LineBreakType.Forbidden, null, 2810, null) },
        { (LineBreakClass.AK, LineBreakClass.VI), (LineBreakType.Forbidden, null, 2810, null) },
        { (LineBreakClass.AS, LineBreakClass.VI), (LineBreakType.Forbidden, null, 2810, null) },
        // LB29 Do not break between numeric punctuation and alphabetics (“e.g.”).
        { (LineBreakClass.IS, LineBreakClass.AL), (LineBreakType.Forbidden, null, 2900, null) },
        { (LineBreakClass.IS, LineBreakClass.HL), (LineBreakType.Forbidden, null, 2900, null) },
        // LB30 Do not break between letters, numbers, or ordinary symbols and opening or closing parentheses.
        // Adjustments to accomodate the excluded set of East Asian characters are handled in SplitText()
        { (LineBreakClass.AL, LineBreakClass.OP), (LineBreakType.Forbidden, null, 3000, null) },
        { (LineBreakClass.HL, LineBreakClass.OP), (LineBreakType.Forbidden, null, 3000, null) },
        { (LineBreakClass.NU, LineBreakClass.OP), (LineBreakType.Forbidden, null, 3000, null) },
        { (LineBreakClass.CP, LineBreakClass.AL), (LineBreakType.Forbidden, null, 3000, null) },
        { (LineBreakClass.CP, LineBreakClass.HL), (LineBreakType.Forbidden, null, 3000, null) },
        { (LineBreakClass.CP, LineBreakClass.NU), (LineBreakType.Forbidden, null, 3000, null) },
        // LB30a Break between two regional indicator symbols if and only if there are an even number of regional indicators preceding the position of the break.
        // handled in SplitText() as this rule cannot be encoded in a pairwise lookup table
        // LB30b Do not break between an emoji base (or potential emoji) and an emoji modifier.
        // partially handled in SplitText()
        { (LineBreakClass.EB, LineBreakClass.EM), (LineBreakType.Forbidden, null, 3020, null) },
        // LB31 Break everywhere else.
        // handled in Grapheme constructor
    };

    /// <summary>
    /// Split text into multiple lines, where each line is no larger than maxLength bytes when encoded in UTF-8.
    /// This follows the Unicode line breaking algorithm at https://www.unicode.org/reports/tr14/#Algorithm
    /// </summary>
    /// <param name="text"></param>
    /// <param name="maxLength"></param>
    /// <returns>An iterable of the broken-up lines along with whether the line ends with a hard break or a soft break (wrapping)</returns>
    public static IEnumerable<LineBreakRecord> SplitText(string text, int maxLength)
    {
        return SplitText(text, maxLength, SplitTextOptions.None);
    }

    private static void AdjustRules(CodePoint prev, CodePoint cur)
    {
        // look up the (prev, cur), (prev, Any), and (Any, cur) pairs
        var (prevType, curType, prevRule, curRule) = _mapping.GetValueOrDefault((prev.Class, cur.Class));
        if ((prevRule ?? 99999) < prev.Rule)
        {
            prev.Type = prevType!.Value;
            prev.Rule = prevRule!.Value;
        }

        if ((curRule ?? 99999) < cur.Rule)
        {
            cur.Type = curType!.Value;
            cur.Rule = curRule!.Value;
        }

        (prevType, curType, prevRule, curRule) = _mapping.GetValueOrDefault((prev.Class, LineBreakClass.Any));
        if ((prevRule ?? 99999) < prev.Rule)
        {
            prev.Type = prevType!.Value;
            prev.Rule = prevRule!.Value;
        }

        if ((curRule ?? 99999) < cur.Rule)
        {
            cur.Type = curType!.Value;
            cur.Rule = curRule!.Value;
        }

        (prevType, curType, prevRule, curRule) = _mapping.GetValueOrDefault((LineBreakClass.Any, cur.Class));
        if ((prevRule ?? 99999) < prev.Rule)
        {
            prev.Type = prevType!.Value;
            prev.Rule = prevRule!.Value;
        }

        if ((curRule ?? 99999) < cur.Rule)
        {
            cur.Type = curType!.Value;
            cur.Rule = curRule!.Value;
        }
    }

    /// <summary>
    /// Split text into multiple lines, where each line is no larger than maxLength bytes when encoded in UTF-8.
    /// This follows the Unicode line breaking algorithm at https://www.unicode.org/reports/tr14/#Algorithm
    /// </summary>
    /// <param name="text"></param>
    /// <param name="maxLength"></param>
    /// <param name="options">Options to modify the algorithm result</param>
    /// <returns>An iterable of the broken-up lines along with whether the line ends with a hard break or a soft break (wrapping)</returns>
    public static IEnumerable<LineBreakRecord> SplitText(string text, int maxLength, SplitTextOptions options)
    {
        List<CodePoint> codePoints =
        [
            new(null, LineBreakClass.StartOfText)
        ];

        var lb9classes = new LineBreakClass[] { LineBreakClass.SOT, LineBreakClass.BK, LineBreakClass.CR, LineBreakClass.LF, LineBreakClass.NL, LineBreakClass.SP, LineBreakClass.ZW };

        // iterate over codepoints to determine line break class
        var enumerator = text.EnumerateRunes();
        var prev = codePoints[0];
        while (enumerator.MoveNext())
        {
            CodePoint cur = new(enumerator.Current);

            // Do an initial set of pairwise lookups, and then handle LB9 and LB10
            // LB9 and LB10 can adjust the classes, so we do another set of lookups afterwards
            AdjustRules(prev, cur);

            // LB9 Do not break a combining character sequence; treat it as if it has the line breaking class of the base character in all of the following rules. Treat ZWJ as if it were CM.
            if (prev.Rule > 900 && !lb9classes.Contains(prev.Class) && (cur.Class == LineBreakClass.CM || cur.Class == LineBreakClass.ZWJ))
            {
                cur.OverrideClass = prev.Class;
                cur.OverrideRune = prev.Rune;
                prev.Rule = 900;
                prev.Type = LineBreakType.Forbidden;
                cur.Rule = 3100;
                cur.Type = LineBreakType.Optional;
            }

            // LB10 Treat any remaining combining mark or ZWJ as AL.
            if (cur.Class == LineBreakClass.CM || cur.Class == LineBreakClass.ZWJ)
            {
                cur.OverrideClass = LineBreakClass.AL;
                cur.OverrideRune = new('A');
                if (prev.Rule > 1000)
                {
                    prev.Rule = 3100;
                    prev.Type = LineBreakType.Optional;
                }
                
                if (cur.Rule > 1000)
                {
                    cur.Rule = 3100;
                    cur.Type = LineBreakType.Optional;
                }
            }

            // if LB9 or LB10 modified the class, do another lookup
            if (prev.OverrideClass.HasValue || cur.OverrideClass.HasValue)
            {
                AdjustRules(prev, cur);
            }

            codePoints.Add(cur);
            prev = cur;
        }

        // handle rules that we couldn't handle above
        LineBreakClass[] lb12classes = [LineBreakClass.SP, LineBreakClass.BA, LineBreakClass.HY, LineBreakClass.HH];
        LineBreakClass[] lb15aclasses = [LineBreakClass.StartOfText, LineBreakClass.BK, LineBreakClass.CR, LineBreakClass.LF, LineBreakClass.NL, LineBreakClass.OP, LineBreakClass.QU, LineBreakClass.GL, LineBreakClass.SP, LineBreakClass.ZW];
        LineBreakClass[] lb15bclasses = [LineBreakClass.SP, LineBreakClass.GL, LineBreakClass.WJ, LineBreakClass.CL, LineBreakClass.QU, LineBreakClass.CP, LineBreakClass.EX, LineBreakClass.IS, LineBreakClass.SY, LineBreakClass.BK, LineBreakClass.CR, LineBreakClass.LF, LineBreakClass.NL, LineBreakClass.ZW];
        LineBreakClass[] lb20aclasses = [LineBreakClass.StartOfText, LineBreakClass.BK, LineBreakClass.CR, LineBreakClass.LF, LineBreakClass.NL, LineBreakClass.SP, LineBreakClass.ZW, LineBreakClass.CB, LineBreakClass.GL];
        LineBreakClass[] lb25classes = [LineBreakClass.PO, LineBreakClass.PR];
        EastAsianWidth[] eaWidths = [EastAsianWidth.F, EastAsianWidth.W, EastAsianWidth.H];

        // for the first loop we only handle rules before LB9, as these rules require examining every character
        int state = 0;
        for (var i = 1; i < codePoints.Count; ++i)
        {
            var cur = codePoints[i];
            state = (state, cur.Class) switch
            {
                (0x08, LineBreakClass.SP) => 0x08,
                (_, LineBreakClass.ZW) => 0x08,
                (_, _) => 0
            };

            // LB8 Break before any character following a zero-width space, even if one or more spaces intervene.
            if (state == 0x08 && cur.Rule > 800)
            {
                cur.Type = LineBreakType.Optional;
                cur.Rule = 800;
            }
        }

        // for the second loop we skip over every character marked by LB9 as if they didn't exist
        var filtered = codePoints.Where(c => c.Rule != 900).ToList();
        state = 0;
        prev = filtered[0];
        for (var i = 1; i < filtered.Count; ++i)
        {
            var cur = filtered[i];
            var next = (i < filtered.Count - 1) ? filtered[i + 1] : null;
            var next2 = (i < filtered.Count - 2) ? filtered[i + 2] : null;
            state = (state, cur.Class) switch
            {
                (0x08, LineBreakClass.SP) => 0x08,
                (0x14, LineBreakClass.SP) => 0x14,
                (0x15, LineBreakClass.SP) => 0x15,
                (0x16, LineBreakClass.SP) => 0x16,
                (0x16, LineBreakClass.NS) => 0x1600,
                (0x17, LineBreakClass.SP) => 0x17,
                (0x1700, LineBreakClass.SP) => 0x17,
                (0x17, LineBreakClass.B2) => 0x1700,
                (0x1700, LineBreakClass.B2) => 0x1700,
                (0x2525, LineBreakClass.SY) => 0x2525,
                (0x2525, LineBreakClass.IS) => 0x2525,
                (0x2525, LineBreakClass.CL) => 0x2500,
                (0x2525, LineBreakClass.CP) => 0x2500,
                (0x30, LineBreakClass.RI) => 0x3000,
                (_, LineBreakClass.ZW) => 0x08,
                (_, LineBreakClass.OP) => 0x14,
                (_, LineBreakClass.CL) => 0x16,
                (_, LineBreakClass.CP) => 0x16,
                (_, LineBreakClass.B2) => 0x17,
                (_, LineBreakClass.NU) => 0x2525,
                (_, LineBreakClass.RI) => 0x30,
                (_, _) => 0
            };

            // handle state modifications that require more logic
            if (lb15aclasses.Contains(prev.Class) && cur.Class == LineBreakClass.QU && Rune.GetUnicodeCategory(cur.Rune) == UnicodeCategory.InitialQuotePunctuation)
            {
                state = 0x15;
            }

            byte low = unchecked((byte)(state & 0xff));
            byte high = unchecked((byte)(state >> 8));

            // LB12a Do not break before NBSP and related characters, except after spaces and hyphens.
            if (cur.Class == LineBreakClass.GL && prev.Rule > 1210 && !lb12classes.Contains(prev.Class))
            {
                prev.Type = LineBreakType.Forbidden;
                prev.Rule = 1210;
            }

            // LB14 Do not break after ‘[’, even after spaces.
            if (low == 0x14 && cur.Rule > 1400)
            {
                cur.Type = LineBreakType.Forbidden;
                cur.Rule = 1400;
            }

            // LB15a Do not break after an unresolved initial punctuation that lies at the start of the line, after a space, after opening punctuation, or after an unresolved quotation mark, even after spaces.
            if (low == 0x15 && cur.Rule > 1510)
            {
                cur.Type = LineBreakType.Forbidden;
                cur.Rule = 1510;
            }

            // LB15b Do not break before an unresolved final punctuation that lies at the end of the line, before a space, before a prohibited break, or before an unresolved quotation mark, even after spaces.
            if (prev.Rule > 1520 && cur.Class == LineBreakClass.QU && Rune.GetUnicodeCategory(cur.Rune) == UnicodeCategory.FinalQuotePunctuation && (next == null || lb15bclasses.Contains(next.Class)))
            {
                prev.Type = LineBreakType.Forbidden;
                prev.Rule = 1520;
            }

            // LB15c Break before a decimal mark that follows a space, for instance, in ‘subtract .5’.
            if (prev.Rule > 1530 && prev.Class == LineBreakClass.SP && cur.Class == LineBreakClass.IS && next?.Class == LineBreakClass.NU)
            {
                prev.Type = LineBreakType.Optional;
                prev.Rule = 1530;
            }

            // LB16 Do not break between closing punctuation and a nonstarter (lb=NS), even with intervening spaces.
            if (high == 0x16 && prev.Rule > 1600)
            {
                prev.Type = LineBreakType.Forbidden;
                prev.Rule = 1600;
            }

            // LB17 Do not break within ‘——’, even with intervening spaces.
            if (high == 0x17 && prev.Rule > 1700)
            {
                prev.Type = LineBreakType.Forbidden;
                prev.Rule = 1700;
            }

            // LB19 Do not break before non-initial unresolved quotation marks, such as ‘ ” ’ or ‘ " ’, nor after non-final unresolved quotation marks, such as ‘ “ ’ or ‘ " ’.
            if (prev.Rule > 1900 && cur.Class == LineBreakClass.QU && Rune.GetUnicodeCategory(cur.Rune) != UnicodeCategory.InitialQuotePunctuation)
            {
                prev.Type = LineBreakType.Forbidden;
                prev.Rule = 1900;
            }

            if (cur.Rule > 1900 && cur.Class == LineBreakClass.QU && Rune.GetUnicodeCategory(cur.Rune) != UnicodeCategory.FinalQuotePunctuation)
            {
                cur.Type = LineBreakType.Forbidden;
                cur.Rule = 1900;
            }

            // LB19a Unless surrounded by East Asian characters, do not break either side of any unresolved quotation marks.
            bool isEastAsian(CodePoint c) => eaWidths.Contains(UnicodeProperty.GetEastAsianWidth(c.Rune));
            if (prev.Rule > 1910 && cur.Class == LineBreakClass.QU)
            {
                if (!isEastAsian(prev) || next == null || !isEastAsian(next))
                {
                    prev.Type = LineBreakType.Forbidden;
                    prev.Rule = 1910;
                }
            }

            if (cur.Rule > 1910 && cur.Class == LineBreakClass.QU)
            {
                if ((next != null && !isEastAsian(next)) || prev.Class == LineBreakClass.StartOfText || !isEastAsian(prev))
                {
                    cur.Type = LineBreakType.Forbidden;
                    cur.Rule = 1910;
                }
            }

            // LB20a Do not break after a word-initial hyphen.
            if (cur.Rule > 2010 && lb20aclasses.Contains(prev.Class) && (cur.Class == LineBreakClass.HY || cur.Class == LineBreakClass.HH) && (next?.Class == LineBreakClass.AL || next?.Class == LineBreakClass.HL))
            {
                cur.Type = LineBreakType.Forbidden;
                cur.Rule = 2010;
            }

            // LB21a Do not break after the hyphen in Hebrew + Hyphen + non-Hebrew.
            if (cur.Rule > 2110 && prev.Class == LineBreakClass.HL && next != null && next.Class != LineBreakClass.HL && (cur.Class == LineBreakClass.HY || cur.Class == LineBreakClass.HH))
            {
                cur.Type = LineBreakType.Forbidden;
                cur.Rule = 2110;
            }

            // LB25 Do not break numbers
            if (cur.Rule > 2500 && low == 0x25 && next?.Class == LineBreakClass.NU)
            {
                cur.Type = LineBreakType.Forbidden;
                cur.Rule = 2500;
            }

            if (cur.Rule > 2500 && high == 0x25 && next != null && lb25classes.Contains(next.Class))
            {
                cur.Type = LineBreakType.Forbidden;
                cur.Rule = 2500;
            }

            if (prev.Rule > 2500 && lb25classes.Contains(prev.Class) && cur.Class == LineBreakClass.OP && (next?.Class == LineBreakClass.NU || (next?.Class == LineBreakClass.IS && next2?.Class == LineBreakClass.NU)))
            {
                prev.Type = LineBreakType.Forbidden;
                prev.Rule = 2500;
            }

            // LB28a Do not break inside the orthographic syllables of Brahmic scripts.
            static bool isAkAsCirc(CodePoint c) => c.Class == LineBreakClass.AK || c.Class == LineBreakClass.AS || c.Rune.Value == 0x25cc;
            if (prev.Rule > 2810)
            {
                if ((prev.Class == LineBreakClass.AP && isAkAsCirc(cur))
                    || (isAkAsCirc(prev) && (cur.Class == LineBreakClass.VF || cur.Class == LineBreakClass.VI))
                    || (isAkAsCirc(prev) && isAkAsCirc(cur) && next?.Class == LineBreakClass.VF))
                {
                    prev.Type = LineBreakType.Forbidden;
                    prev.Rule = 2810;
                }
            }

            if (cur.Rule > 2810 && isAkAsCirc(prev) && cur.Class == LineBreakClass.VI && (next?.Class == LineBreakClass.AK || next?.Rune.Value == 0x25cc))
            {
                cur.Type = LineBreakType.Forbidden;
                cur.Rule = 2810;
            }

            // LB30 Do not break between letters, numbers, or ordinary symbols and opening or closing parentheses.
            if (prev.Rule == 3000 && cur.Class == LineBreakClass.OP)
            {
                var width = UnicodeProperty.GetEastAsianWidth(cur.Rune);
                if (eaWidths.Contains(width))
                {
                    prev.Type = LineBreakType.Optional;
                    prev.Rule = 3100;
                }
            }

            if (prev.Rule == 3000 && prev.Class == LineBreakClass.CP)
            {
                var width = UnicodeProperty.GetEastAsianWidth(prev.Rune);
                if (eaWidths.Contains(width))
                {
                    prev.Type = LineBreakType.Optional;
                    prev.Rule = 3100;
                }
            }

            // LB30a Break between two regional indicator symbols if and only if there are an even number of regional indicators preceding the position of the break.
            if (high == 0x30 && prev.Rule > 3010)
            {
                prev.Type = LineBreakType.Forbidden;
                prev.Rule = 3010;
            }

            // LB30b Do not break between an emoji base (or potential emoji) and an emoji modifier.
            if (prev.Rule > 3020 && cur.Class == LineBreakClass.EM && Rune.GetUnicodeCategory(prev.Rune) == UnicodeCategory.OtherNotAssigned && UnicodeProperty.IsExtendedPictographic(prev.Rune))
            {
                prev.Type = LineBreakType.Forbidden;
                prev.Rule = 3020;
            }

            prev = cur;
        }

        // Add end of text signal (which simplifies a bit of logic below)
        codePoints.Add(new(null, LineBreakClass.EndOfText, LineBreakType.Mandatory, 300));

        int currentLength = 0;
        int thresholdLength = Math.Min(0, maxLength - 24);
        var preThreshold = new StringBuilder();
        CodePoint? threshold = null;
        int thresholdIndex = 0;
        var postThreshold = new StringBuilder();

        // iteratively build up the line, breaking on mandatory breaks and keeping candidate sets otherwise
        // when we get close to maxLength (defined as >= maxlength - 24 bytes). If we need to inject a soft
        // split, we split on the character in the candidate set with the lowest rule number. If none of the
        // characters in the candidate set match that criteria, we inject a break regardless after the final
        // grapheme that fits. Because we injected an end of text marker above, we are guaranteed that this
        // list ends with a mandatory line break and as such all lines are properly accounted for without
        // requiring logic outside of the main loop.
        for (int i = 1; i < codePoints.Count; ++i)
        {
            var cur = codePoints[i];

            // is cur a hard break? We don't add hard breaks to the resultant string so these are effectively
            // 0-length control codes instead.
            if (cur.Type == LineBreakType.Mandatory)
            {
                if (cur.Class != LineBreakClass.EndOfText && options.HasFlag(SplitTextOptions.IncludeBreakCharacters))
                {
                    if (threshold == null)
                    {
                        preThreshold.Append(cur.Value);
                    }
                    else
                    {
                        postThreshold.Append(cur.Value);
                    }
                }

                // don't add an extra blank line at the end if we ended with a mandatory line break and then
                // found our end of text marker
                if (cur.Class != LineBreakClass.EndOfText || currentLength > 0)
                {
                    preThreshold.Append(postThreshold);
                    yield return new(preThreshold.ToString(), true);
                    preThreshold.Clear();
                    threshold = null;
                    postThreshold.Clear();
                    currentLength = 0;
                }

                continue;
            }

            // skip over other line break characters that are forbidden breaks (i.e. CR when followed by LF)
            if (!options.HasFlag(SplitTextOptions.IncludeBreakCharacters) && cur.Class == LineBreakClass.CR && cur.Type == LineBreakType.Forbidden)
            {
                continue;
            }

            if (currentLength + cur.Length > maxLength)
            {
                // need to split this line
                if (threshold != null)
                {
                    // will be incremented on continue, so we specify thresholdIndex here to examine the character
                    // after threshold in the next loop iteration
                    i = thresholdIndex;
                }

                if (preThreshold.Length > 0 && (!options.HasFlag(SplitTextOptions.AllowOverflow) || threshold != null))
                {
                    yield return new(preThreshold.ToString(), false);
                    preThreshold.Clear();
                    threshold = null;
                    postThreshold.Clear();
                    currentLength = 0;
                    continue;
                }
            }

            currentLength += cur.Length;

            if (threshold == null)
            {
                preThreshold.Append(cur.Value);
            }
            else
            {
                postThreshold.Append(cur.Value);
            }

            if (currentLength > thresholdLength && cur.Type == LineBreakType.Optional && cur.Rule < (threshold?.Rule ?? 99999))
            {
                // found a new threshold grapheme
                preThreshold.Append(postThreshold);
                postThreshold.Clear();
                threshold = cur;
                thresholdIndex = i;
            }
        }
    }

    [DebuggerDisplay("U+{RuneDebugDisplay,nq} {ClassDebugDisplay,nq} {Type} [{Rule}]")]
    private class CodePoint
    {
        public CodePoint(Rune? value, LineBreakClass? lineBreakClass = null)
            : this(value, lineBreakClass, LineBreakType.Optional, 3100) { }

        public CodePoint(Rune? value, LineBreakClass? lineBreakClass, LineBreakType lineBreakType, int rule)
        {
            OriginalRune = value ?? default;
            lineBreakClass ??= UnicodeProperty.GetLineBreakClass(Rune);

            Value = Rune.ToString();
            OriginalClass = lineBreakClass switch
            {
                LineBreakClass.AI => LineBreakClass.AL,
                LineBreakClass.SG => LineBreakClass.AL,
                LineBreakClass.XX => LineBreakClass.AL,
                LineBreakClass.SA => Rune.GetUnicodeCategory(Rune) switch
                {
                    UnicodeCategory.NonSpacingMark => LineBreakClass.CM,
                    UnicodeCategory.SpacingCombiningMark => LineBreakClass.CM,
                    _ => LineBreakClass.AL
                },
                LineBreakClass.CJ => LineBreakClass.NS,
                _ => lineBreakClass.Value
            };

            Type = lineBreakType;
            Rule = rule;
            Length = Value.EncodeUtf8().Length;
        }

        private string RuneDebugDisplay => OriginalRune.Value.ToString("X4");

        private string ClassDebugDisplay => OverrideClass != null ? $"{OverrideClass}* ({OriginalClass})" : OriginalClass.ToString();

        public string Value { get; init; }

        private Rune OriginalRune { get; init; }

        public Rune? OverrideRune { get; set; }

        public Rune Rune => OverrideRune ?? OriginalRune;

        public int Length { get; init; }

        private LineBreakClass OriginalClass { get; init; }

        public LineBreakClass? OverrideClass { get; set; }

        public LineBreakClass Class => OverrideClass ?? OriginalClass;

        public LineBreakType Type { get; set; }

        public int Rule { get; set; }
    }
}
