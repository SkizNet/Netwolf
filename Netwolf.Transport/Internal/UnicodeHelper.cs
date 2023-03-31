using System.Globalization;
using System.Text;

namespace Netwolf.Transport.Internal;

internal static partial class UnicodeHelper
{
    internal static readonly UTF8Encoding Strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    internal static readonly UTF8Encoding Lax = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

    internal static byte[] EncodeUtf8(this string source, bool strict = true)
    {
        return (strict ? Strict : Lax).GetBytes(source);
    }

    internal static string DecodeUtf8(this ReadOnlySpan<byte> source, bool strict = true)
    {
        return (strict ? Strict : Lax).GetString(source);
    }

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
        // LB13 Do not break before ‘]’ or ‘!’ or ‘;’ or ‘/’, even after spaces.
        { (LineBreakClass.Any, LineBreakClass.CL), (LineBreakType.Forbidden, null, 1300, null) },
        { (LineBreakClass.Any, LineBreakClass.CP), (LineBreakType.Forbidden, null, 1300, null) },
        { (LineBreakClass.Any, LineBreakClass.EX), (LineBreakType.Forbidden, null, 1300, null) },
        { (LineBreakClass.Any, LineBreakClass.IS), (LineBreakType.Forbidden, null, 1300, null) },
        { (LineBreakClass.Any, LineBreakClass.SY), (LineBreakType.Forbidden, null, 1300, null) },
        // LB14 Do not break after ‘[’, even after spaces.
        // handled in SplitText() as this rule cannot be encoded in a pairwise lookup table
        // LB15 Do not break within ‘”[’, even with intervening spaces.
        // handled in SplitText() as this rule cannot be encoded in a pairwise lookup table
        // LB16 Do not break between closing punctuation and a nonstarter (lb=NS), even with intervening spaces.
        // handled in SplitText() as this rule cannot be encoded in a pairwise lookup table
        // LB17 Do not break within ‘——’, even with intervening spaces.
        // handled in SplitText() as this rule cannot be encoded in a pairwise lookup table
        // LB18 Break after spaces.
        // handled above since (Any, SP) was in use for LB7
        // LB19 Do not break before or after quotation marks, such as ‘ ” ’.
        { (LineBreakClass.Any, LineBreakClass.QU), (LineBreakType.Forbidden, LineBreakType.Forbidden, 1900, 1900) },
        // LB20 Break before and after unresolved CB.
        { (LineBreakClass.Any, LineBreakClass.CB), (LineBreakType.Optional, LineBreakType.Optional, 2000, 2000) },
        // LB21 Do not break before hyphen-minus, other hyphens, fixed-width spaces, small kana, and other non-starters, or after acute accents.
        { (LineBreakClass.Any, LineBreakClass.BA), (LineBreakType.Forbidden, null, 2100, null) },
        { (LineBreakClass.Any, LineBreakClass.HY), (LineBreakType.Forbidden, null, 2100, null) },
        { (LineBreakClass.Any, LineBreakClass.NS), (LineBreakType.Forbidden, null, 2100, null) },
        { (LineBreakClass.Any, LineBreakClass.BB), (null, LineBreakType.Forbidden, null, 2100) },
        // LB21a Don't break after Hebrew + Hyphen.
        { (LineBreakClass.HL, LineBreakClass.HY), (null, LineBreakType.Forbidden, null, 2110) },
        { (LineBreakClass.HL, LineBreakClass.BA), (null, LineBreakType.Forbidden, null, 2110) },
        // LB21b Don’t break between Solidus and Hebrew letters.
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
        // LB25 Do not break between the following pairs of classes relevant to numbers
        { (LineBreakClass.CL, LineBreakClass.PO), (LineBreakType.Forbidden, null, 2500, null) },
        { (LineBreakClass.CP, LineBreakClass.PO), (LineBreakType.Forbidden, null, 2500, null) },
        { (LineBreakClass.CL, LineBreakClass.PR), (LineBreakType.Forbidden, null, 2500, null) },
        { (LineBreakClass.CP, LineBreakClass.PR), (LineBreakType.Forbidden, null, 2500, null) },
        { (LineBreakClass.NU, LineBreakClass.PO), (LineBreakType.Forbidden, null, 2500, null) },
        { (LineBreakClass.NU, LineBreakClass.PR), (LineBreakType.Forbidden, null, 2500, null) },
        { (LineBreakClass.PO, LineBreakClass.OP), (LineBreakType.Forbidden, null, 2500, null) },
        { (LineBreakClass.PO, LineBreakClass.NU), (LineBreakType.Forbidden, null, 2500, null) },
        { (LineBreakClass.PR, LineBreakClass.OP), (LineBreakType.Forbidden, null, 2500, null) },
        { (LineBreakClass.PR, LineBreakClass.NU), (LineBreakType.Forbidden, null, 2500, null) },
        { (LineBreakClass.HY, LineBreakClass.NU), (LineBreakType.Forbidden, null, 2500, null) },
        { (LineBreakClass.IS, LineBreakClass.NU), (LineBreakType.Forbidden, null, 2500, null) },
        { (LineBreakClass.NU, LineBreakClass.NU), (LineBreakType.Forbidden, null, 2500, null) },
        { (LineBreakClass.SY, LineBreakClass.NU), (LineBreakType.Forbidden, null, 2500, null) },
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
        var eaWidths = new EastAsianWidth[] { EastAsianWidth.F, EastAsianWidth.W, EastAsianWidth.H };
        prev = graphemes[0];
        foreach (var cur in graphemes)
        {
            state = (state, cur.Class) switch
            {
                (0x08, LineBreakClass.SP) => 0x08,
                (0x14, LineBreakClass.SP) => 0x14,
                (0x1500, LineBreakClass.SP) => 0x1500,
                (0x1500, LineBreakClass.OP) => 0x1514,
                (0x1514, LineBreakClass.SP) => 0x14,
                (0x16, LineBreakClass.SP) => 0x16,
                (0x16, LineBreakClass.NS) => 0x1600,
                (0x17, LineBreakClass.SP) => 0x17,
                (0x1700, LineBreakClass.SP) => 0x17,
                (0x17, LineBreakClass.B2) => 0x1700,
                (0x1700, LineBreakClass.B2) => 0x1700,
                (0x30, LineBreakClass.RI) => 0x3000,
                (_, LineBreakClass.ZW) => 0x08,
                (_, LineBreakClass.OP) => 0x14,
                (_, LineBreakClass.QU) => 0x1500,
                (_, LineBreakClass.CL) => 0x16,
                (_, LineBreakClass.CP) => 0x16,
                (_, LineBreakClass.B2) => 0x17,
                (_, LineBreakClass.RI) => 0x30,
                (_, _) => 0
            };

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

            // LB15 Do not break within ‘”[’, even with intervening spaces.
            if (high == 0x15 && prev.Rule > 1500)
            {
                prev.Type = LineBreakType.Forbidden;
                prev.Rule = 1500;
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
                    _ = preThreshold.Append(postThreshold);
                    lines.Add(preThreshold.ToString());
                    _ = preThreshold.Clear();
                    threshold = null;
                    _ = postThreshold.Clear();
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
                _ = preThreshold.Clear();
                threshold = null;
                _ = postThreshold.Clear();
                currentLength = 0;
                continue;
            }

            currentLength += cur.Length;

            _ = threshold == null ? preThreshold.Append(cur.Value) : postThreshold.Append(cur.Value);

            if (currentLength > thresholdLength && cur.Type == LineBreakType.Optional && cur.Rule < (threshold?.Rule ?? 99999))
            {
                // found a new threshold grapheme
                _ = preThreshold.Append(postThreshold);
                _ = postThreshold.Clear();
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
                LineBreakClass.CJ => LineBreakClass.AL,
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
        AL,
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
