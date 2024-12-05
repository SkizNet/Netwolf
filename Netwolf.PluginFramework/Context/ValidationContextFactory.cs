// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using System.ComponentModel.DataAnnotations;

namespace Netwolf.PluginFramework.Context;

internal sealed class ValidationContextFactory : IValidationContextFactory
{
    private IServiceProvider ServiceProvider { get; init; }

    public ValidationContextFactory(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    public ValidationContext Create(object instance, IDictionary<object, object?>? items = null)
    {
        return new ValidationContext(instance, ServiceProvider, items);
    }
}
