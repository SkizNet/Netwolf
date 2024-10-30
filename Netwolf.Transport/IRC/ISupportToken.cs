// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.Transport.IRC;

/// <summary>
/// Strongly-typed value type that refers to an ISUPPORT token.
/// Using the pre-existing static members is preferred, however arbitrary ISUPPORT
/// tokens can be created by passing along a string; no transformation or validation
/// is performed so you will usually want to ensure it is all-uppercase.
/// </summary>
/// <param name="Name"></param>
public readonly record struct ISupportToken(string Name)
{
    public static readonly ISupportToken AWAYLEN = new("AWAYLEN");
    public static readonly ISupportToken CASEMAPPING = new("CASEMAPPING");
    public static readonly ISupportToken CHANLIMIT = new("CHANLIMIT");
    public static readonly ISupportToken CHANMODES = new("CHANMODES");
    public static readonly ISupportToken CHANNELLEN = new("CHANNELLEN");
    public static readonly ISupportToken CHANTYPES = new("CHANTYPES");
    public static readonly ISupportToken CNOTICE = new("CNOTICE");
    public static readonly ISupportToken CPRIVMSG = new("CPRIVMSG");
    public static readonly ISupportToken ELIST = new("ELIST");
    public static readonly ISupportToken EXCEPTS = new("EXCEPTS");
    public static readonly ISupportToken EXTBAN = new("EXTBAN");
    public static readonly ISupportToken HOSTLEN = new("HOSTLEN");
    public static readonly ISupportToken INVEX = new("INVEX");
    public static readonly ISupportToken KICKLEN = new("KICKLEN");
    public static readonly ISupportToken MAXLIST = new("MAXLIST");
    public static readonly ISupportToken MODES = new("MODES");
    public static readonly ISupportToken MONITOR = new("MONITOR");
    public static readonly ISupportToken NAMELEN = new("NAMELEN");
    public static readonly ISupportToken NETWORK = new("NETWORK");
    public static readonly ISupportToken NICKLEN = new("NICKLEN");
    public static readonly ISupportToken PREFIX = new("PREFIX");
    public static readonly ISupportToken SAFELIST = new("SAFELIST");
    public static readonly ISupportToken SILENCE = new("SILENCE");
    public static readonly ISupportToken STATUSMSG = new("STATUSMSG");
    public static readonly ISupportToken TARGMAX = new("TARGMAX");
    public static readonly ISupportToken TOPICLEN = new("TOPICLEN");
    public static readonly ISupportToken UTF8ONLY = new("UTF8ONLY");
    public static readonly ISupportToken USERLEN = new("USERLEN");
    public static readonly ISupportToken WHOX = new("WHOX");

    public static implicit operator string(ISupportToken t) => t.Name;
    public override string ToString() => Name;
}
