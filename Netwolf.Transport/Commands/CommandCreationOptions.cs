// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.IRC;

namespace Netwolf.Transport.Commands;

public record CommandCreationOptions(
    int LineLen = 512,
    int ClientTagLen = 4096,
    int ServerTagLen = 8191,
    bool UseCPrivMsg = false,
    bool UseCNotice = false,
    bool UseDraftMultiLine = false,
    int MultiLineMaxLines = int.MaxValue,
    int MultiLineMaxBytes = int.MaxValue)
{
    public static CommandCreationOptions MakeOptions(INetworkInfo network)
    {
        bool multilineEnabled = network.TryGetEnabledCap("draft/multiline", out var multilineValue) && network.TryGetEnabledCap("batch", out _);
        Dictionary<string, int> multilineLimits = [];
        if (multilineValue != null)
        {
            // coerce invalid or missing values to -1, which will disable multiline if this happens to be on max-bytes or max-lines
            // (CommandFactory sanity checks the MaxLines and MaxBytes values; custom ICommandFactory implementations should implement their own checking as well)
            multilineLimits = multilineValue.Split(',').Select(t => t.Split('=')).ToDictionary(a => a[0], a => int.TryParse(a.ElementAtOrDefault(1), out int i) ? i : -1);
        }

        multilineLimits.TryAdd("max-bytes", int.MaxValue);
        multilineLimits.TryAdd("max-lines", int.MaxValue);

        return new CommandCreationOptions(
            LineLen: network.Limits.LineLength,
            ClientTagLen: network.Limits.ClientTagLength,
            ServerTagLen: network.Limits.ServerTagLength,
            UseCPrivMsg: network.TryGetISupport(ISupportToken.CPRIVMSG, out _),
            UseCNotice: network.TryGetISupport(ISupportToken.CNOTICE, out _),
            UseDraftMultiLine: multilineEnabled,
            MultiLineMaxLines: multilineLimits["max-lines"],
            MultiLineMaxBytes: multilineLimits["max-bytes"]);
    }
}
