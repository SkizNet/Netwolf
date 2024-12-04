// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

using Netwolf.PluginFramework.Commands;
using Netwolf.PluginFramework.Context;

namespace Netwolf.BotFramework.Services;

public class BotCommandContextFactory
{
    private IEnumerable<IAccountProvider> AccountProviders { get; init; }

    private IEnumerable<IPermissionProvider> PermissionProviders { get; init; }

    private IValidationContextFactory ValidationContextFactory { get; init; }

    // Internal because our account and permission providers come from keyed services, and the DI container doesn't support dependent services yet
    internal BotCommandContextFactory(
        IEnumerable<IAccountProvider> accountProviders,
        IEnumerable<IPermissionProvider> permissionProviders,
        IValidationContextFactory validationContextFactory)
    {
        AccountProviders = accountProviders;
        PermissionProviders = permissionProviders;
        ValidationContextFactory = validationContextFactory;
    }

    public async Task<BotCommandContext> CreateAsync(Bot bot, string target, ICommand command, string fullLine, CancellationToken cancellationToken)
    {
        if (command.Source == null || command.CommandType != CommandType.Bot)
        {
            throw new ArgumentException("Command must be a bot command and have a defined source", nameof(command));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var context = new BotCommandContext(bot, ValidationContextFactory, target, command, fullLine);

        // Populate account
        foreach (var accountProvider in AccountProviders)
        {
            var resolvedAccount = await accountProvider.GetAccountAsync(context, cancellationToken);
            if (resolvedAccount != null)
            {
                context.SenderAccount = resolvedAccount;
                context.AccountProvider = accountProvider.GetType();
                break;
            }
        }

        // Populate permissions
        foreach (var permissionProvider in PermissionProviders)
        {
            var curCount = context.SenderPermissions.Count;
            context.SenderPermissions.UnionWith(await permissionProvider.GetPermissionsAsync(context, cancellationToken));

            // only track PermissionProvider types that positively contributed to the sender's permission set
            if (context.SenderPermissions.Count > curCount)
            {
                context.PermissionProviders.Add(permissionProvider.GetType());
            }
        }

        return context;
    }
}
