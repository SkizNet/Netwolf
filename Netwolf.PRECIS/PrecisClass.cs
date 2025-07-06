// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.PRECIS;

public sealed record PrecisClass(
    PrecisCategories Valid,
    PrecisCategories ContextualRuleRequired,
    PrecisCategories Disallowed,
    bool AllowUnassigned)
{
    public static readonly PrecisClass IdentifierClass = new(
        Valid: PrecisCategories.PVALID,
        ContextualRuleRequired: PrecisCategories.CONTEXTJ | PrecisCategories.CONTEXTO,
        Disallowed: PrecisCategories.DISALLOWED | PrecisCategories.ID_DIS,
        AllowUnassigned: false
        );

    public static readonly PrecisClass FreeformClass = new(
        Valid: PrecisCategories.PVALID | PrecisCategories.FREE_PVAL,
        ContextualRuleRequired: PrecisCategories.CONTEXTJ | PrecisCategories.CONTEXTO,
        Disallowed: PrecisCategories.DISALLOWED,
        AllowUnassigned: false
        );
}
