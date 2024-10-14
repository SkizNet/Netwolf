using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework.Internal;

internal sealed class ValidationContextFactory
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
