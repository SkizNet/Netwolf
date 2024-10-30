// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Netwolf.BotFramework.Exceptions;

/// <summary>
/// Represents an operation that failed with an error Numeric code
/// </summary>
public class NumericException : Exception
{
    public int Numeric { get; init; }

    public NumericException(int numeric, string message) : base(message)
    {
        Numeric = numeric;
    }
}
