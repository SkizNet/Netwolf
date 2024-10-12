using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Internal;

using Netwolf.Attributes;
using Netwolf.BotFramework.Services;
using Netwolf.PluginFramework.Commands;
using Netwolf.PluginFramework.Context;

using System.Linq.Expressions;
using System.Reflection;

namespace Netwolf.BotFramework.Internal;

internal sealed class BotCommandThunk : ICommandHandler<BotCommandResult>
{
    public string Command { get; init; }

    public string? Privilege { get; init; }

    private Bot Bot { get; init; }

    private ObjectMethodExecutor Executor { get; init; }

    private List<ParameterConverter> ParameterConverters { get; init; } = [];

    private delegate bool Converter(string input, out object? output);

    private record ParameterConverter(ParameterInfo Parameter, Converter Converter);

    string ICommandHandler<BotCommandResult>.UnderlyingFullName => $"{Executor.MethodInfo.DeclaringType!.FullName}.{Executor.MethodInfo.Name}";

    internal BotCommandThunk(Bot bot, MethodInfo method, CommandAttribute attr)
    {
        Bot = bot;
        Command = attr.Name;
        Privilege = attr.Privilege;
        Executor = ObjectMethodExecutor.Create(method, bot.GetType().GetTypeInfo(), TypeHelper.GetParameterDefaultValues(method));

        var convertMethod = typeof(TypeHelper).GetMethod(nameof(TypeHelper.TryChangeType))
            ?? throw new InvalidOperationException("Unable to locate TypeHelper.TryChangeType. This is a bug in the Netwolf.BotFramework library.");

        foreach (var param in Executor.MethodParameters)
        {
            // CreateDelegate doesn't work here because TypeHelper.TryChangeType as T as an out param, so we need to build up a linq expression
            // that calls the function and performs appropriate type casts, and then compile that expression.
            var inParam = Expression.Parameter(typeof(string), "input");
            var converted = Expression.Variable(param.ParameterType, "c");
            var success = Expression.Variable(typeof(bool), "r");
            var outParam = Expression.Parameter(typeof(object).MakeByRefType(), "output");
            var lambda = Expression.Lambda<Converter>(
                Expression.Block(
                    [converted, success],
                    Expression.Assign(outParam, Expression.Constant(null)),
                    Expression.Assign(success, Expression.Call(convertMethod.MakeGenericMethod(param.ParameterType), inParam, converted)),
                    Expression.IfThen(Expression.IsTrue(success), Expression.Assign(outParam, Expression.Convert(converted, typeof(object)))),
                    success
                ),
                inParam,
                outParam);
            ParameterConverters.Add(new(param, lambda.Compile()));
        }
    }

    public async Task<BotCommandResult> ExecuteAsync(ICommand command, IContext sender, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BotCommandContext context = (BotCommandContext)sender;
        List<object?> args = [];
        object? value;

        foreach (var (param, conv) in ParameterConverters)
        {
            // TODO: split up FullLine in some fashion, ideally without tons of extra allocations
            // Need to change delegate type to take in a Span<char> as well
            args.Add(param.ParameterType switch
            {
                _ when param.ParameterType == typeof(BotCommandContext) => context,
                _ when param.ParameterType == typeof(CancellationToken) => cancellationToken,
                _ => conv(context.FullLine, out value) ? value : Executor.GetDefaultValueForParameter(args.Count)
            });
        }

        if (Executor.IsMethodAsync)
        {
            value = await Executor.ExecuteAsync(Bot, [.. args]);
        }
        else
        {
            value = Executor.Execute(Bot, [.. args]);
        }

        return new(value);
    }
}
