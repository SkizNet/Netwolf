using Netwolf.BotFramework.Attributes;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework.CommandParser;

/// <summary>
/// Class that wraps a MethodInfo to contain pre-processed data regarding the parameters it takes.
/// </summary>
internal class CommandDescriptor
{
    /// <summary>
    /// Wrapped command method
    /// </summary>
    internal MethodInvoker Method { get; init; }

    /// <summary>
    /// Class instance if invoking a class method
    /// </summary>
    internal object? Instance { get; init; }

    /// <summary>
    /// Whether the <seealso cref="Method"/> is async and needs to be awaited.
    /// </summary>
    internal bool IsAsync { get; init; }

    /// <summary>
    /// Arguments to be passed to the <see cref="Method"/>
    /// </summary>
    internal ImmutableList<IArgumentDescriptor> Args { get; init; }

    internal CommandDescriptor(MethodInfo method, CommandAttribute attribute, object? instance)
    {
        if (attribute.Name == null)
        {
            throw new ArgumentException("CommandAttribute not updated with inherited name. This is a bug in the BotFramework library.", nameof(attribute));
        }

        Instance = instance;
        IsAsync = method.ReturnType.IsAssignableTo(typeof(Task));

        // This is also checked at design/compile time via the Netwolf.Analyzers package for better dev experience
        if (!IsAsync && method.ReturnType != typeof(void))
        {
            throw new InvalidOperationException("Bot command methods must return either void or Task");
        }

        Method = MethodInvoker.Create(method);

        // get ArgumentDescriptors going for all of the args
        if (method.ContainsGenericParameters)
        {
            throw new InvalidOperationException("Bot command methods must not contain unassigned generic parameters.");
        }

        Args = method.GetParameters().Select(p =>
        {
            var descriptorType = typeof(ArgumentDescriptor<>).MakeGenericType(p.ParameterType);
            return (IArgumentDescriptor)Activator.CreateInstance(descriptorType, p)!;
        }).ToImmutableList();
    }

    public Task InvokeAsync(string[] args, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // parse args
        List<object?> parsedArgs = [];
        ReadOnlySpan<string> rest = new(args);
        foreach (var argDescriptor in Args)
        {
            if (argDescriptor.TryParse(ref rest, out var parsed))
            {
                parsedArgs.Add(parsed);
            }
            else
            {
                // conversion failed and the arg is optional
                parsedArgs.Add(null);
            }
        }

        // invoke Method; will return either null or something that can be cast to Task
        var value = Method.Invoke(Instance, [.. parsedArgs]);

        // if we're async then pass along the returned task so the caller can await it
        if (IsAsync && value != null)
        {
            return (Task)value;
        }

        // otherwise the invocation was synchronous and completed, so mark this as done too
        return Task.CompletedTask;
    }
}
