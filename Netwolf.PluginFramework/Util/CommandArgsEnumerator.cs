// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections;

namespace Netwolf.PluginFramework.Util;

public sealed class CommandArgsEnumerator : ICommandEnumerator
{
    private readonly IReadOnlyList<string> _args;
    private int _index = -1;

    public string Current => _args[_index];

    public string Rest => string.Join(' ', _args.Skip(_index));

    object IEnumerator.Current => Current;

    public CommandArgsEnumerator(IReadOnlyList<string> args)
    {
        _args = args;
    }

    public void Dispose() { /* no-op */ }

    public bool MoveNext()
    {
        return ++_index < _args.Count;
    }

    public void MoveToEnd()
    {
        _index = _args.Count;
    }

    public void Reset()
    {
        _index = -1;
    }
}
