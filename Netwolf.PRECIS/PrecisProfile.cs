// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Text;

namespace Netwolf.PRECIS;

public sealed record PrecisProfile(
    PrecisClass BaseClass,
    Func<string, string>? WidthMappingRule,
    Func<string, string>? AdditionalMappingRule,
    Func<string, string>? CaseMappingRule,
    NormalizationForm NormalizationRule,
    Func<string, bool>? DirectionalityRule,
    PrecisRules Preparation,
    PrecisRules Enforcement,
    PrecisRules Comparison)
{
    public static readonly PrecisProfile UsernameCaseMapped = new(
        PrecisClass.IdentifierClass,
        WidthMapping.Decompose,
        AdditionalMapping.None,
        CaseMapping.ToLowerCase,
        NormalizationForm.FormC,
        Directionality.Bidi,
        PrecisRules.WidthMapping | PrecisRules.Behavioral,
        PrecisRules.All | PrecisRules.ForbidEmpty,
        PrecisRules.All | PrecisRules.ForbidEmpty);

    public static readonly PrecisProfile UsernameCasePreserved = new(
        PrecisClass.IdentifierClass,
        WidthMapping.Decompose,
        AdditionalMapping.None,
        CaseMapping.None,
        NormalizationForm.FormC,
        Directionality.Bidi,
        PrecisRules.WidthMapping | PrecisRules.Behavioral,
        PrecisRules.All | PrecisRules.ForbidEmpty,
        PrecisRules.All | PrecisRules.ForbidEmpty);

    public static readonly PrecisProfile OpaqueString = new(
        PrecisClass.FreeformClass,
        WidthMapping.None,
        AdditionalMapping.MapSpaces,
        CaseMapping.None,
        NormalizationForm.FormC,
        Directionality.None,
        PrecisRules.Behavioral,
        PrecisRules.All,
        PrecisRules.All);

    public static readonly PrecisProfile Nickname = new(
        PrecisClass.FreeformClass,
        WidthMapping.None,
        AdditionalMapping.MapAndCollapseSpaces,
        CaseMapping.ToLowerCase,
        NormalizationForm.FormKC,
        Directionality.None,
        PrecisRules.Behavioral,
        PrecisRules.AdditionalMapping | PrecisRules.Normalization | PrecisRules.Behavioral | PrecisRules.ForbidEmpty,
        PrecisRules.All | PrecisRules.ForbidEmpty);
}
