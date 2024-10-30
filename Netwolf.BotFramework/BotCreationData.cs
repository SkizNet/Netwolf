using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Netwolf.BotFramework.Internal;
using Netwolf.BotFramework.Services;
using Netwolf.PluginFramework.Commands;
using Netwolf.Transport.IRC;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    internal ICommandDispatcher<BotCommandResult> CommandDispatcher { get; init; }
    internal ICommandFactory CommandFactory { get; init; }
    internal BotCommandContextFactory BotCommandContextFactory { get; init; }
    internal IEnumerable<ICapProvider> CapProviders { get; init; }
    internal ValidationContextFactory ValidationContextFactory { get; init; }
    internal ChannelRecordLookup ChannelRecordLookup { get; init; }
    internal UserRecordLookup UserRecordLookup { get; init; }

    // for unit testing
    internal bool EnableCommandOptimization { get; set; } = true;

    internal BotCreationData(
        string botName,
        ILogger<Bot> logger,
        IOptionsMonitor<BotOptions> options,
        INetworkFactory networkFactory,
        ICommandDispatcher<BotCommandResult> commandDispatcher,
        ICommandFactory commandFactory,
        BotCommandContextFactory botCommandContextFactory,
        IEnumerable<ICapProvider> capProviders,
        ValidationContextFactory validationContextFactory,
        ChannelRecordLookup channelRecordLookup,
        UserRecordLookup userRecordLookup)
    {
        ArgumentNullException.ThrowIfNull(botName, nameof(botName));

        BotName = botName;
        Logger = logger;
        OptionsMonitor = options;
        NetworkFactory = networkFactory;
        CommandDispatcher = commandDispatcher;
        CommandFactory = commandFactory;
        BotCommandContextFactory = botCommandContextFactory;
        CapProviders = capProviders;
        ValidationContextFactory = validationContextFactory;
        ChannelRecordLookup = channelRecordLookup;
        UserRecordLookup = userRecordLookup;
    }
}
