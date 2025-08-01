﻿// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Netwolf.BotFramework.Services;
using Netwolf.PluginFramework.Commands;
using Netwolf.Transport.Commands;
using Netwolf.Transport.Events;
using Netwolf.Transport.IRC;

namespace Netwolf.BotFramework;

/// <summary>
/// Data class to work around the lack of dependent services in the .NET DI container.
/// This class encapsulates all services needed by the top-level <see cref="Bot"/> class
/// so that user-defind subclasses do not need adjustment in the event we change which
/// services the top-level class depends upon.
/// <para />
/// This class is an opaque PDO passed into the Bot constructor and contains no
/// user-accessible data. It is marked public purely so that it can be referenced
/// in user-defined assemblies which create Bot subclasses.
/// </summary>
public sealed class BotCreationData
{
    internal string BotName { get; init; }
    internal ILogger<Bot> Logger { get; init; }
    internal IOptionsMonitor<BotOptions> OptionsMonitor { get; init; }
    internal INetworkFactory NetworkFactory { get; init; }
    internal NetworkEvents NetworkEvents { get; init; }
    internal ICommandDispatcher<BotCommandResult> CommandDispatcher { get; init; }
    internal ICommandFactory CommandFactory { get; init; }
    internal BotCommandContextFactory BotCommandContextFactory { get; init; }
    internal IEnumerable<ICapProvider> CapProviders { get; init; }

    // for unit testing
    internal bool EnableCommandOptimization { get; set; } = true;
    internal bool ForceCommandOptimization { get; set; } = false;

    internal BotCreationData(
        string botName,
        ILogger<Bot> logger,
        IOptionsMonitor<BotOptions> options,
        INetworkFactory networkFactory,
        NetworkEvents networkEvents,
        ICommandDispatcher<BotCommandResult> commandDispatcher,
        ICommandFactory commandFactory,
        BotCommandContextFactory botCommandContextFactory,
        IEnumerable<ICapProvider> capProviders)
    {
        ArgumentNullException.ThrowIfNull(botName, nameof(botName));

        BotName = botName;
        Logger = logger;
        OptionsMonitor = options;
        NetworkEvents = networkEvents;
        NetworkFactory = networkFactory;
        CommandDispatcher = commandDispatcher;
        CommandFactory = commandFactory;
        BotCommandContextFactory = botCommandContextFactory;
        CapProviders = capProviders;
    }
}
