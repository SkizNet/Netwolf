// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections;

namespace Netwolf.PluginFramework.Util;

public sealed class CommandStringEnumerator : ICommandEnumerator
{
    private readonly string _args;
    private int _start = -1;
    private int _end = -1;

    public string Current => _args[_start.._end];

    public string Rest => _args[_start..];

    object IEnumerator.Current => Current;

    public CommandStringEnumerator(string args)
    {
        _args = args;
    }

    public void Dispose() { /* no-op */ }

    public bool MoveNext()
    {
        do
        {
            _start = _end + 1;
            if (_start >= _args.Length)
            {
                return false;
            }

            _end = _args.IndexOf(' ', _start);
            if (_end == -1)
            {
                _end = _args.Length;
            }
        } while (_start == _end);

        return true;
    }

    public void MoveToEnd()
    {
        _start = _args.Length;
        _end = _args.Length;
    }

    public void Reset()
    {
        _start = -1;
        _end = -1;
    }
}
