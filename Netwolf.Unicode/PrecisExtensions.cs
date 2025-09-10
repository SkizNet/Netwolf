// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Unicode.Internal;
using Netwolf.Unicode.PRECIS;

namespace Netwolf.Unicode;

public static class PrecisExtensions
{
    /// <summary>
    /// Implementation of RFC 8264 PRECIS string preparation.
    /// </summary>
    /// <param name="source">String to prepare.</param>
    /// <param name="profile">Profile to apply to <paramref name="source"/>.</param>
    /// <returns>The prepared string or <c>null</c> if the string fails preparation according to the specified profile.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> or <paramref name="profile"/> is null.</exception>
    public static string? Prepare(this string source, PrecisProfile profile)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(profile);
        return ApplyRules(source, profile, profile.Preparation);
    }

    /// <summary>
    /// Implementation of RFC 8264 PRECIS string enforcement.
    /// </summary>
    /// <param name="source">String to enforce.</param>
    /// <param name="profile">Profile to apply to <paramref name="source"/>.</param>
    /// <returns>The enforced string or <c>null</c> if the string fails enforcement according to the specified profile.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> or <paramref name="profile"/> is null.</exception>
    public static string? Enforce(this string source, PrecisProfile profile)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(profile);
        return ApplyRules(source, profile, profile.Enforcement);
    }

    /// <summary>
    /// Implementation of RFC 8264 PRECIS string comparison.
    /// </summary>
    /// <param name="source">First string to compare.</param>
    /// <param name="other">Second string to compare.</param>
    /// <param name="profile">Profile to apply to the strings.</param>
    /// <returns>True if the strings compare equally, false if they fail comparison rules, are different, or <paramref name="other"/> is null.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> or <paramref name="profile"/> is null.</exception>
    public static bool Equals(this string source, string? other, PrecisProfile profile)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(profile);
        if (other == null)
        {
            return false;
        }

        var left = ApplyRules(source, profile, profile.Comparison);
        var right = ApplyRules(other, profile, profile.Comparison);
        if (left is null || right is null)
        {
            return false;
        }

        return left == right;
    }

    private static string? ApplyRules(string input, PrecisProfile profile, PrecisRules rules)
    {
        string prev = input;
        string? cur = input;

        // RFC 8264 indicates in Section 7 (Order of Operations) that we should apply
        // the rules multiple times until the string stabilizes, or until we have applied it
        // a total of 4 times.
        for (int i = 0; i < 4; i++)
        {
            if (rules.HasFlag(PrecisRules.WidthMapping) && profile.WidthMappingRule != null)
            {
                cur = profile.WidthMappingRule(cur);
                if (cur is null)
                {
                    return null;
                }
            }

            if (rules.HasFlag(PrecisRules.AdditionalMapping) && profile.AdditionalMappingRule != null)
            {
                cur = profile.AdditionalMappingRule(cur);
                if (cur is null)
                {
                    return null;
                }
            }

            if (rules.HasFlag(PrecisRules.CaseMapping) && profile.CaseMappingRule != null)
            {
                cur = profile.CaseMappingRule(cur);
                if (cur is null)
                {
                    return null;
                }
            }

            if (rules.HasFlag(PrecisRules.Normalization))
            {
                try
                {
                    cur = cur.Normalize(profile.NormalizationRule);
                }
                catch (ArgumentException)
                {
                    return null;
                }
            }

            if (rules.HasFlag(PrecisRules.Directionality) && profile.DirectionalityRule != null)
            {
                if (!profile.DirectionalityRule(cur))
                {
                    return null;
                }
            }

            if (rules.HasFlag(PrecisRules.Behavioral))
            {
                var runes = cur.EnumerateRunes().ToList();
                for (var j = 0; j < runes.Count; j++)
                {
                    var rune = runes[j];
                    var category = PrecisCategoryLookup.GetCategory(rune);
                    if (category.HasFlag(PrecisCategories.UNASSIGNED) && !profile.BaseClass.AllowUnassigned)
                    {
                        return null;
                    }
                    else if ((category & profile.BaseClass.Disallowed) != 0)
                    {
                        return null;
                    }
                    else if ((category & profile.BaseClass.Valid) != 0)
                    {
                        continue;
                    }
                    else if ((category & profile.BaseClass.ContextualRuleRequired) != 0)
                    {
                        // handle registered contextual rules
                        // using Unicode 12.0 registry, as it hasn't been updated for newer versions
                        // https://www.iana.org/assignments/idna-tables-12.0.0/idna-tables-12.0.0.xhtml#idna-tables-context
                        switch (rune.Value)
                        {
                            case 0x200C:
                                if (j == 0)
                                {
                                    return null;
                                }

                                if (UnicodeProperty.GetCombiningClass(runes[j - 1]) == CombiningClass.Virama)
                                {
                                    // Valid
                                    break;
                                }

                                for (int k = j - 1; k >= 0; k--)
                                {
                                    if (UnicodeProperty.GetJoiningType(runes[k]) == JoiningType.Transparent)
                                    {
                                        continue;
                                    }
                                    else if ((UnicodeProperty.GetJoiningType(runes[k]) & (JoiningType.LeftJoining | JoiningType.DualJoining)) != 0)
                                    {
                                        // Valid
                                        break;
                                    }
                                    else
                                    {
                                        // Invalid
                                        return null;
                                    }
                                }

                                if (j == runes.Count - 1)
                                {
                                    // When checking joining types, we require a character after us
                                    return null;
                                }

                                for (int k = j + 1; k < runes.Count; k++)
                                {
                                    if (UnicodeProperty.GetJoiningType(runes[k]) == JoiningType.Transparent)
                                    {
                                        continue;
                                    }
                                    else if ((UnicodeProperty.GetJoiningType(runes[k]) & (JoiningType.RightJoining | JoiningType.DualJoining)) != 0)
                                    {
                                        // Valid
                                        break;
                                    }
                                    else
                                    {
                                        // Invalid
                                        return null;
                                    }
                                }

                                // Getting here implies the "regex" passed so this is a valid character in this position
                                break;
                            case 0x200D:
                                if (j == 0)
                                {
                                    return null;
                                }

                                if (UnicodeProperty.GetCombiningClass(runes[j - 1]) == CombiningClass.Virama)
                                {
                                    // Valid
                                    break;
                                }

                                // Else: Invalid
                                return null;
                            case 0x00B7:
                                if (j == 0 || j == runes.Count - 1)
                                {
                                    return null;
                                }

                                if (runes[j - 1].Value == 0x006C && runes[j + 1].Value == 0x006C)
                                {
                                    // Valid
                                    break;
                                }

                                // Else: Invalid
                                return null;
                            case 0x0375:
                                if (j == runes.Count - 1)
                                {
                                    return null;
                                }

                                if (UnicodeProperty.GetScript(runes[j + 1]) == Script.Greek)
                                {
                                    // Valid
                                    break;
                                }

                                // Else: Invalid
                                return null;
                            case 0x05F3:
                            case 0x05F4:
                                if (j == 0)
                                {
                                    return null;
                                }

                                if (UnicodeProperty.GetScript(runes[j - 1]) == Script.Hebrew)
                                {
                                    // Valid
                                    break;
                                }

                                // Else: Invalid
                                return null;
                            case 0x30FB:
                                if (!runes.Select(UnicodeProperty.GetScript).Any(s => s == Script.Hiragana || s == Script.Katakana || s == Script.Han))
                                {
                                    return null;
                                }

                                // Valid
                                break;
                            case >= 0x0660 and <= 0x0669:
                                if (runes.Select(r => r.Value).Any(i => i >= 0x06F0 && i <= 0x06F9))
                                {
                                    return null;
                                }

                                // Valid
                                break;
                            case >= 0x06F0 and <= 0x06F9:
                                if (runes.Select(r => r.Value).Any(i => i >= 0x0660 && i <= 0x0669))
                                {
                                    return null;
                                }

                                // Valid
                                break;
                            default:
                                // No contextual rule exists for this codepoint
                                return null;
                        }
                    }
                    // shouldn't ever get here, but treat remaining runes as unassigned since we weren't told
                    // they were explicitly valid, needing contextual rules, or disallowed
                    else if (!profile.BaseClass.AllowUnassigned)
                    {
                        return null;
                    }
                }
            }

            if (rules.HasFlag(PrecisRules.ForbidEmpty) && cur.Length == 0)
            {
                return null;
            }

            // check if we're stable
            if (prev == cur)
            {
                return cur;
            }

            prev = cur;
        }

        // we didn't stabilize
        return null;
    }
}
