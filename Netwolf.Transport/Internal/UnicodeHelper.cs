// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.Extensions;

using System.Globalization;
using System.Text;

namespace Netwolf.Transport.Internal;

/// <summary>
/// Implementation of TR14, revision 53 for Unicode 16.0.0
/// https://www.unicode.org/reports/tr14/tr14-53.html
/// </summary>
internal static partial class UnicodeHelper
{
    private static readonly Dictionary<(LineBreakClass Prev, LineBreakClass Cur), (LineBreakType? PrevType, LineBreakType? CurType, int? PrevRule, int? CurRule)> _mapping = new()
    {
        // LB1 Assign a line breaking class to each code point of the input. Resolve AI, CB, CJ, SA, SG, and XX into other line breaking classes depending on criteria outside the scope of this algorithm.
        // handled in Grapheme constructor
        // LB2 Never break at the start of text.
        { (LineBreakClass.Sot, LineBreakClass.Any), (LineBreakType.Forbidden, null, 200, null) },
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
        // handled implicitly because we iterate over grapheme clusters and only consider the first codepoint in the grapheme cluster for this table
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
        // (+ LB10 Treat any remaining combining mark or ZWJ as AL.)
        { (LineBreakClass.AL, LineBreakClass.NU), (LineBreakType.Forbidden, null, 2300, null) },
        { (LineBreakClass.CM, LineBreakClass.NU), (LineBreakType.Forbidden, null, 2300, null) },
        { (LineBreakClass.ZWJ, LineBreakClass.NU), (LineBreakType.Forbidden, null, 2300, null) },
        { (LineBreakClass.HL, LineBreakClass.NU), (LineBreakType.Forbidden, null, 2300, null) },
        { (LineBreakClass.NU, LineBreakClass.AL), (LineBreakType.Forbidden, null, 2300, null) },
        { (LineBreakClass.NU, LineBreakClass.CM), (LineBreakType.Forbidden, null, 2300, null) },
        { (LineBreakClass.NU, LineBreakClass.ZWJ), (LineBreakType.Forbidden, null, 2300, null) },
        { (LineBreakClass.NU, LineBreakClass.HL), (LineBreakType.Forbidden, null, 2300, null) },
        // LB23a Do not break between numeric prefixes and ideographs, or between ideographs and numeric postfixes.
        { (LineBreakClass.PR, LineBreakClass.ID), (LineBreakType.Forbidden, null, 2310, null) },
        { (LineBreakClass.PR, LineBreakClass.EB), (LineBreakType.Forbidden, null, 2310, null) },
        { (LineBreakClass.PR, LineBreakClass.EM), (LineBreakType.Forbidden, null, 2310, null) },
        { (LineBreakClass.ID, LineBreakClass.PO), (LineBreakType.Forbidden, null, 2310, null) },
        { (LineBreakClass.EB, LineBreakClass.PO), (LineBreakType.Forbidden, null, 2310, null) },
        { (LineBreakClass.EM, LineBreakClass.PO), (LineBreakType.Forbidden, null, 2310, null) },
        // LB24 Do not break between numeric prefix/postfix and letters, or between letters and prefix/postfix.
        // (+ LB10 Treat any remaining combining mark or ZWJ as AL.)
        { (LineBreakClass.PR, LineBreakClass.AL), (LineBreakType.Forbidden, null, 2400, null) },
        { (LineBreakClass.PR, LineBreakClass.CM), (LineBreakType.Forbidden, null, 2400, null) },
        { (LineBreakClass.PR, LineBreakClass.ZWJ), (LineBreakType.Forbidden, null, 2400, null) },
        { (LineBreakClass.PR, LineBreakClass.HL), (LineBreakType.Forbidden, null, 2400, null) },
        { (LineBreakClass.PO, LineBreakClass.AL), (LineBreakType.Forbidden, null, 2400, null) },
        { (LineBreakClass.PO, LineBreakClass.HL), (LineBreakType.Forbidden, null, 2400, null) },
        { (LineBreakClass.AL, LineBreakClass.PR), (LineBreakType.Forbidden, null, 2400, null) },
        { (LineBreakClass.CM, LineBreakClass.PR), (LineBreakType.Forbidden, null, 2400, null) },
        { (LineBreakClass.ZWJ, LineBreakClass.PR), (LineBreakType.Forbidden, null, 2400, null) },
        { (LineBreakClass.AL, LineBreakClass.PO), (LineBreakType.Forbidden, null, 2400, null) },
        { (LineBreakClass.CM, LineBreakClass.PO), (LineBreakType.Forbidden, null, 2400, null) },
        { (LineBreakClass.ZWJ, LineBreakClass.PO), (LineBreakType.Forbidden, null, 2400, null) },
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
        // (+ LB10 Treat any remaining combining mark or ZWJ as AL.)
        { (LineBreakClass.AL, LineBreakClass.AL), (LineBreakType.Forbidden, null, 2800, null) },
        { (LineBreakClass.AL, LineBreakClass.CM), (LineBreakType.Forbidden, null, 2800, null) },
        { (LineBreakClass.AL, LineBreakClass.ZWJ), (LineBreakType.Forbidden, null, 2800, null) },
        { (LineBreakClass.AL, LineBreakClass.HL), (LineBreakType.Forbidden, null, 2800, null) },
        { (LineBreakClass.CM, LineBreakClass.AL), (LineBreakType.Forbidden, null, 2800, null) },
        { (LineBreakClass.CM, LineBreakClass.CM), (LineBreakType.Forbidden, null, 2800, null) },
        { (LineBreakClass.CM, LineBreakClass.ZWJ), (LineBreakType.Forbidden, null, 2800, null) },
        { (LineBreakClass.CM, LineBreakClass.HL), (LineBreakType.Forbidden, null, 2800, null) },
        { (LineBreakClass.ZWJ, LineBreakClass.AL), (LineBreakType.Forbidden, null, 2800, null) },
        { (LineBreakClass.ZWJ, LineBreakClass.CM), (LineBreakType.Forbidden, null, 2800, null) },
        { (LineBreakClass.ZWJ, LineBreakClass.ZWJ), (LineBreakType.Forbidden, null, 2800, null) },
        { (LineBreakClass.ZWJ, LineBreakClass.HL), (LineBreakType.Forbidden, null, 2800, null) },
        { (LineBreakClass.HL, LineBreakClass.AL), (LineBreakType.Forbidden, null, 2800, null) },
        { (LineBreakClass.HL, LineBreakClass.CM), (LineBreakType.Forbidden, null, 2800, null) },
        { (LineBreakClass.HL, LineBreakClass.ZWJ), (LineBreakType.Forbidden, null, 2800, null) },
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
        // (+ LB10 Treat any remaining combining mark or ZWJ as AL.)
        { (LineBreakClass.IS, LineBreakClass.AL), (LineBreakType.Forbidden, null, 2900, null) },
        { (LineBreakClass.IS, LineBreakClass.CM), (LineBreakType.Forbidden, null, 2900, null) },
        { (LineBreakClass.IS, LineBreakClass.ZWJ), (LineBreakType.Forbidden, null, 2900, null) },
        { (LineBreakClass.IS, LineBreakClass.HL), (LineBreakType.Forbidden, null, 2900, null) },
        // LB30 Do not break between letters, numbers, or ordinary symbols and opening or closing parentheses.
        // Adjustments to accomodate the excluded set of East Asian characters are handled in SplitText()
        // (+ LB10 Treat any remaining combining mark or ZWJ as AL.)
        { (LineBreakClass.AL, LineBreakClass.OP), (LineBreakType.Forbidden, null, 3000, null) },
        { (LineBreakClass.CM, LineBreakClass.OP), (LineBreakType.Forbidden, null, 3000, null) },
        { (LineBreakClass.ZWJ, LineBreakClass.OP), (LineBreakType.Forbidden, null, 3000, null) },
        { (LineBreakClass.HL, LineBreakClass.OP), (LineBreakType.Forbidden, null, 3000, null) },
        { (LineBreakClass.NU, LineBreakClass.OP), (LineBreakType.Forbidden, null, 3000, null) },
        { (LineBreakClass.CP, LineBreakClass.AL), (LineBreakType.Forbidden, null, 3000, null) },
        { (LineBreakClass.CP, LineBreakClass.CM), (LineBreakType.Forbidden, null, 3000, null) },
        { (LineBreakClass.CP, LineBreakClass.ZWJ), (LineBreakType.Forbidden, null, 3000, null) },
        { (LineBreakClass.CP, LineBreakClass.HL), (LineBreakType.Forbidden, null, 3000, null) },
        { (LineBreakClass.CP, LineBreakClass.NU), (LineBreakType.Forbidden, null, 3000, null) },
        // LB30a Break between two regional indicator symbols if and only if there are an even number of regional indicators preceding the position of the break.
        // handled in SplitText() as this rule cannot be encoded in a pairwise lookup table
        // LB30b Do not break between an emoji base (or potential emoji) and an emoji modifier.
        // handled implicitly due to the nature of us iterating over grapheme clusters rather than individual codepoints
        // LB31 Break everywhere else.
        // handled in Grapheme constructor
    };

    /// <summary>
    /// Split text into multiple lines, where each line is no larger than maxLength bytes when encoded in UTF-8.
    /// This follows the Unicode line breaking algorithm at https://www.unicode.org/reports/tr14/#Algorithm
    /// </summary>
    /// <param name="text"></param>
    /// <param name="maxLength"></param>
    /// <returns></returns>
    internal static List<string> SplitText(string text, int maxLength)
    {
        List<Grapheme> graphemes = new()
        {
            new(String.Empty, LineBreakClass.Sot)
        };

        // iterate over grapheme clusters, using the first codepoint (aka Rune) in the cluster to determine line break class
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        var prev = graphemes[0];
        while (enumerator.MoveNext())
        {
            string grapheme = enumerator.GetTextElement();
            Grapheme cur = new(grapheme);

            // look up the (prev, cur), (prev, Any), and (Any, cur) pairs
            var (prevType, curType, prevRule, curRule) = _mapping[(prev.Class, cur.Class)];
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

            (prevType, curType, prevRule, curRule) = _mapping[(prev.Class, LineBreakClass.Any)];
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

            (prevType, curType, prevRule, curRule) = _mapping[(LineBreakClass.Any, cur.Class)];
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

            graphemes.Add(cur);
            prev = cur;
        }

        // handle rules that we couldn't handle above
        int state = 0;
        var lb12classes = new LineBreakClass[] { LineBreakClass.SP, LineBreakClass.BA, LineBreakClass.HY };
        var lb15aclasses = new LineBreakClass[] { LineBreakClass.Sot, LineBreakClass.BK, LineBreakClass.CR, LineBreakClass.LF, LineBreakClass.NL, LineBreakClass.OP, LineBreakClass.QU, LineBreakClass.GL, LineBreakClass.SP, LineBreakClass.ZW };
        var lb15bclasses = new LineBreakClass[] { LineBreakClass.SP, LineBreakClass.GL, LineBreakClass.WJ, LineBreakClass.CL, LineBreakClass.QU, LineBreakClass.CP, LineBreakClass.EX, LineBreakClass.IS, LineBreakClass.SY, LineBreakClass.BK, LineBreakClass.CR, LineBreakClass.LF, LineBreakClass.NL, LineBreakClass.ZW };
        var lb20aclasses = new LineBreakClass[] { LineBreakClass.Sot, LineBreakClass.BK, LineBreakClass.CR, LineBreakClass.LF, LineBreakClass.NL, LineBreakClass.SP, LineBreakClass.ZW, LineBreakClass.CB, LineBreakClass.GL };
        var eaWidths = new EastAsianWidth[] { EastAsianWidth.F, EastAsianWidth.W, EastAsianWidth.H };
        prev = graphemes[0];
        for (var i = 1; i < graphemes.Count; ++i)
        {
            var cur = graphemes[i];
            var next = (i < graphemes.Count - 1) ? graphemes[i + 1] : null;
            var next2 = (i < graphemes.Count - 2) ? graphemes[i + 2] : null;
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
                (0x25, LineBreakClass.SY) => 0x25,
                (0x25, LineBreakClass.IS) => 0x25,
                (0x2525, LineBreakClass.SY) => 0x25,
                (0x2525, LineBreakClass.IS) => 0x25,
                (0x25, LineBreakClass.NU) => 0x2525,
                (0x25, LineBreakClass.CL) => 0x2500,
                (0x25, LineBreakClass.CP) => 0x2500,
                (0x25, LineBreakClass.PO) => 0x2500,
                (0x25, LineBreakClass.PR) => 0x2500,
                (0x30, LineBreakClass.RI) => 0x3000,
                (_, LineBreakClass.ZW) => 0x08,
                (_, LineBreakClass.OP) => 0x14,
                (_, LineBreakClass.CL) => 0x16,
                (_, LineBreakClass.CP) => 0x16,
                (_, LineBreakClass.B2) => 0x17,
                (_, LineBreakClass.NU) => 0x25,
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

            // LB8 Break before any character following a zero-width space, even if one or more spaces intervene.
            if (low == 0x08 && cur.Rule > 800)
            {
                cur.Type = LineBreakType.Optional;
                cur.Rule = 800;
            }

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
            bool isEastAsian(Grapheme g) => eaWidths.Contains(GetEastAsianWidth(g.Rune));
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
                if ((next != null && !isEastAsian(next)) || prev.Class == LineBreakClass.Sot || !isEastAsian(prev))
                {
                    cur.Type = LineBreakType.Forbidden;
                    cur.Rule = 1910;
                }
            }

            // LB20a Do not break after a word-initial hyphen.
            if (cur.Rule > 2010 && lb20aclasses.Contains(prev.Class) && (cur.Class == LineBreakClass.HY || cur.Rune.Value == 0x2010) && next?.Class == LineBreakClass.AL)
            {
                cur.Type = LineBreakType.Forbidden;
                cur.Rule = 2010;
            }

            // LB21a Do not break after the hyphen in Hebrew + Hyphen + non-Hebrew.
            if (cur.Rule > 2110 && prev.Class == LineBreakClass.HL && next != null && next.Class != LineBreakClass.HL && (cur.Class == LineBreakClass.HY || (cur.Class == LineBreakClass.BA && !isEastAsian(cur))))
            {
                cur.Type = LineBreakType.Forbidden;
                cur.Rule = 2110;
            }

            // LB25 Do not break numbers
            if (high == 0x25)
            {
                var lb25prev = new LineBreakClass[] { LineBreakClass.PO, LineBreakClass.PR, LineBreakClass.NU };
                if (prev.Rule > 2500 && lb25prev.Contains(cur.Class))
                {
                    prev.Type = LineBreakType.Forbidden;
                    prev.Rule = 2500;
                }

                if (cur.Rule > 2500 && (cur.Class == LineBreakClass.CL || cur.Class == LineBreakClass.CP) && next != null && (next.Class == LineBreakClass.PO || next.Class == LineBreakClass.PR))
                {
                    cur.Type = LineBreakType.Forbidden;
                    cur.Rule = 2500;
                }
            }

            if (prev.Rule > 2500 && cur.Class == LineBreakClass.OP && (prev.Class == LineBreakClass.PO || prev.Class == LineBreakClass.PR))
            {
                if (next?.Class == LineBreakClass.NU || (next?.Class == LineBreakClass.IS && next2?.Class == LineBreakClass.NU))
                {
                    prev.Type = LineBreakType.Forbidden;
                    prev.Rule = 2500;
                }
            }

            // LB28a Do not break inside the orthographic syllables of Brahmic scripts.
            static bool isAkAsCirc(Grapheme g) => g.Class == LineBreakClass.AK || g.Class == LineBreakClass.AS || g.Rune.Value == 0x25cc;
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
                var width = GetEastAsianWidth(cur.Rune);
                if (eaWidths.Contains(width))
                {
                    prev.Type = LineBreakType.Optional;
                    prev.Rule = 3100;
                }
            }

            if (cur.Rule == 3000 && cur.Class == LineBreakClass.CP)
            {
                var width = GetEastAsianWidth(cur.Rune);
                if (eaWidths.Contains(width))
                {
                    cur.Type = LineBreakType.Optional;
                    cur.Rule = 3100;
                }
            }

            // LB30a Break between two regional indicator symbols if and only if there are an even number of regional indicators preceding the position of the break.
            if (high == 0x30 && prev.Rule > 3010)
            {
                prev.Type = LineBreakType.Forbidden;
                prev.Rule = 3010;
            }

            prev = cur;
        }

        // Add end of text signal (which simplifies a bit of logic below)
        graphemes.Add(new(String.Empty, LineBreakClass.Eot, LineBreakType.Mandatory, 300));

        List<string> lines = new();
        int currentLength = 0;
        int thresholdLength = Math.Min(0, maxLength - 24);
        var preThreshold = new StringBuilder();
        Grapheme? threshold = null;
        int thresholdIndex = 0;
        var postThreshold = new StringBuilder();

        // iteratively build up the line, breaking on mandatory breaks and keeping candidate sets otherwise
        // when we get close to maxLength (defined as >= maxlength - 24 bytes). If we need to inject a soft
        // split, we split on the character in the candidate set with the lowest rule number. If none of the
        // characters in the candidate set match that criteria, we inject a break regardless after the final
        // grapheme that fits. Because we injected an end of text marker above, we are guaranteed that this
        // list ends with a mandatory line break and as such all lines are properly accounted for without
        // requiring logic outside of the main loop.
        for (int i = 0; i < graphemes.Count; ++i)
        {
            var cur = graphemes[i];

            // is cur a hard break? We don't add hard breaks to the resultant string so these are effectively
            // 0-length control codes instead.
            if (cur.Type == LineBreakType.Mandatory)
            {
                // don't add an extra blank line at the end if we ended with a mandatory line break and then
                // found our end of text marker
                if (cur.Class != LineBreakClass.Eot || currentLength > 0)
                {
                    preThreshold.Append(postThreshold);
                    lines.Add(preThreshold.ToString());
                    preThreshold.Clear();
                    threshold = null;
                    postThreshold.Clear();
                    currentLength = 0;
                }

                continue;
            }

            // skip over other line break characters that are forbidden breaks (i.e. CR when followed by LF)
            if (cur.Class == LineBreakClass.CR)
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

                lines.Add(preThreshold.ToString());
                preThreshold.Clear();
                threshold = null;
                postThreshold.Clear();
                currentLength = 0;
                continue;
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

        return lines;
    }

    private static partial LineBreakClass GetLineBreakClass(Rune rune);

    private static partial EastAsianWidth GetEastAsianWidth(Rune rune);

    private class Grapheme
    {
        public Grapheme(string value, LineBreakClass? lineBreakClass = null)
            : this(value, lineBreakClass, LineBreakType.Optional, 3100) { }

        public Grapheme(string value, LineBreakClass? lineBreakClass, LineBreakType lineBreakType, int rule)
        {
            Rune = value.EnumerateRunes().FirstOrDefault();
            lineBreakClass ??= GetLineBreakClass(Rune);

            Value = value;
            Class = lineBreakClass switch
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
            Length = value.EncodeUtf8().Length;
        }

        public string Value { get; init; }

        public Rune Rune { get; init; }

        public int Length { get; init; }

        public LineBreakClass Class { get; init; }

        public LineBreakType Type { get; set; }

        public int Rule { get; set; }
    }

    /// <summary>
    /// All known line break classes from https://www.unicode.org/reports/tr14/#Definitions
    /// As an enum to avoid lots of string comparisons in SplitText()
    /// </summary>
    private enum LineBreakClass
    {
        XX,
        BK,
        CR,
        LF,
        CM,
        NL,
        SG,
        WJ,
        ZW,
        GL,
        SP,
        ZWJ,
        B2,
        BA,
        BB,
        HY,
        CB,
        CL,
        CP,
        EX,
        IN,
        NS,
        OP,
        QU,
        IS,
        NU,
        PO,
        PR,
        SY,
        AI,
        AK,
        AL,
        AP,
        AS,
        CJ,
        EB,
        EM,
        H2,
        H3,
        HL,
        ID,
        JL,
        JV,
        JT,
        RI,
        SA,
        VF,
        VI,
        // start of text marker
        Sot,
        // end of text marker
        Eot,
        // special marker for wildcard rules
        Any
    }

    private enum EastAsianWidth
    {
        // Fullwidth
        F,
        // Halfwidth
        H,
        // Wide
        W,
        // Narrow
        Na,
        // Ambiguous
        A,
        // Neutral
        N
    }

    private enum LineBreakType
    {
        Forbidden,
        Optional,
        Mandatory
    }
}
