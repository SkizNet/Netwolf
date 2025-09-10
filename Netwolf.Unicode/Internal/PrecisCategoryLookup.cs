// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Unicode.PRECIS;

using System.Globalization;
using System.Text;

namespace Netwolf.Unicode.Internal;

internal static class PrecisCategoryLookup
{
    private static readonly int[] Exceptions = [
        0x00B7, 0x00DF, 0x0375, 0x03C2, 0x05F3, 0x05F4, 0x0640, 0x0660,
        0x0669, 0x06F0, 0x06F1, 0x06F2, 0x06F3, 0x06F4, 0x06F5, 0x06F6,
        0x0661, 0x0662, 0x0663, 0x0664, 0x0665, 0x0666, 0x0667, 0x0668,
        0x06F7, 0x06F8, 0x06F9, 0x06FD, 0x06FE, 0x07FA, 0x0F0B, 0x3007,
        0x302E, 0x302F, 0x3031, 0x3032, 0x3033, 0x3034, 0x3035, 0x303B,
        0x30FB
        ];

    private static readonly int[] BackwardsCompatible = [];

    private static readonly UnicodeCategory[] LetterDigits = [
        UnicodeCategory.LowercaseLetter,
        UnicodeCategory.UppercaseLetter,
        UnicodeCategory.OtherLetter,
        UnicodeCategory.DecimalDigitNumber,
        UnicodeCategory.ModifierLetter,
        UnicodeCategory.NonSpacingMark,
        UnicodeCategory.SpacingCombiningMark,
    ];

    private static readonly UnicodeCategory[] OtherLetterDigits = [
        UnicodeCategory.TitlecaseLetter,
        UnicodeCategory.LetterNumber,
        UnicodeCategory.OtherNumber,
        UnicodeCategory.EnclosingMark,
    ];

    private static readonly UnicodeCategory[] Symbols = [
        UnicodeCategory.MathSymbol,
        UnicodeCategory.CurrencySymbol,
        UnicodeCategory.ModifierSymbol,
        UnicodeCategory.OtherSymbol,
    ];

    private static readonly UnicodeCategory[] Punctuation = [
        UnicodeCategory.ConnectorPunctuation,
        UnicodeCategory.DashPunctuation,
        UnicodeCategory.OpenPunctuation,
        UnicodeCategory.ClosePunctuation,
        UnicodeCategory.InitialQuotePunctuation,
        UnicodeCategory.FinalQuotePunctuation,
        UnicodeCategory.OtherPunctuation,
    ];

    internal static PrecisCategories GetCategory(Rune rune)
    {
        var cp = rune.Value;
        var unicodeCategory = Rune.GetUnicodeCategory(rune);

        if (Exceptions.Contains(cp))
        {
            return PrecisCategories.Exceptions | ExceptionLookup(cp);
        }
        else if (BackwardsCompatible.Contains(cp))
        {
            // right now nothing is in the BackwardCompatible list so just return DISALLOWED
            return PrecisCategories.BackwardCompatible | PrecisCategories.DISALLOWED;
        }
        else if (unicodeCategory == UnicodeCategory.OtherNotAssigned && UnicodeProperty.IsNoncharacterCodePoint(rune))
        {
            return PrecisCategories.Unassigned | PrecisCategories.UNASSIGNED;
        }
        else if (cp >= 0x0021 && cp <= 0x007E)
        {
            return PrecisCategories.Ascii7 | PrecisCategories.PVALID;
        }
        else if (UnicodeProperty.IsJoinControl(rune))
        {
            return PrecisCategories.JoinControl | PrecisCategories.CONTEXTJ;
        }
        else if ((UnicodeProperty.GetHangulSyllableType(rune) & (HangulSyllableType.L | HangulSyllableType.V | HangulSyllableType.T)) != 0)
        {
            return PrecisCategories.OldHangulJamo | PrecisCategories.DISALLOWED;
        }
        else if (UnicodeProperty.IsDefaultIgnorableCodePoint(rune) || UnicodeProperty.IsNoncharacterCodePoint(rune))
        {
            return PrecisCategories.PrecisIgnorableProperties | PrecisCategories.DISALLOWED;
        }
        // It's unclear if the RFC meant General_Category(cp) is in {Cc} when it said that Control(cp) = True,
        // since Control(cp) is not defined anywhere in Unicode.
        else if (unicodeCategory == UnicodeCategory.Control)
        {
            return PrecisCategories.Controls | PrecisCategories.DISALLOWED;
        }
        else if (rune.ToString().Normalize(NormalizationForm.FormKC) != rune.ToString())
        {
            return PrecisCategories.HasCompat | PrecisCategories.ID_DIS | PrecisCategories.FREE_PVAL;
        }
        else if (LetterDigits.Contains(unicodeCategory))
        {
            return PrecisCategories.LetterDigits | PrecisCategories.PVALID;
        }
        else if (OtherLetterDigits.Contains(unicodeCategory))
        {
            return PrecisCategories.OtherLetterDigits | PrecisCategories.ID_DIS | PrecisCategories.FREE_PVAL;
        }
        else if (unicodeCategory == UnicodeCategory.SpaceSeparator)
        {
            return PrecisCategories.Spaces | PrecisCategories.ID_DIS | PrecisCategories.FREE_PVAL;
        }
        else if (Symbols.Contains(unicodeCategory))
        {
            return PrecisCategories.Symbols | PrecisCategories.ID_DIS | PrecisCategories.FREE_PVAL;
        }
        else if (Punctuation.Contains(unicodeCategory))
        {
            return PrecisCategories.Punctuation | PrecisCategories.ID_DIS | PrecisCategories.FREE_PVAL;
        }
        else
        {
            return PrecisCategories.DISALLOWED;
        }
    }

    private static PrecisCategories ExceptionLookup(int cp) => cp switch
    {
        0x00DF or 0x03C2 or 0x06FD or 0x06FE or 0x0F0B or 0x3007 => PrecisCategories.PVALID,
        0x00B7 or 0x0375 or 0x05F3 or 0x05F4 or 0x30FB => PrecisCategories.CONTEXTO,
        >= 0x0660 and <= 0x0669 => PrecisCategories.CONTEXTO,
        >= 0x06F0 and <= 0x06F9 => PrecisCategories.CONTEXTO,
        0x0640 or 0x07FA or 0x302E or 0x302F or 0x303B => PrecisCategories.DISALLOWED,
        >= 0x3031 and <= 0x3035 => PrecisCategories.DISALLOWED,
        _ => throw new ArgumentException("Code point is not in Exceptions category", nameof(cp))
    };
}
