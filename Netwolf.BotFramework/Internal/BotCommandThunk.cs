using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Internal;

using Netwolf.Attributes;
using Netwolf.BotFramework.Services;
using Netwolf.PluginFramework.Commands;
using Netwolf.PluginFramework.Context;

using System.ComponentModel.DataAnnotations;
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

    private ValidationContextFactory ValidationContextFactory { get; init; }

    private delegate bool Converter(string input, out object? output);

    private record ParameterConverter(ParameterInfo Parameter, Converter Converter);

    string ICommandHandler<BotCommandResult>.UnderlyingFullName => $"{Executor.MethodInfo.DeclaringType!.FullName}.{Executor.MethodInfo.Name}";

    internal BotCommandThunk(Bot bot, MethodInfo method, CommandAttribute attr, ValidationContextFactory validationContextFactory)
    {
        Bot = bot;
        Command = attr.Name;
        Privilege = attr.Privilege;
        ValidationContextFactory = validationContextFactory;
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
                    // internal variables used
                    [converted, success],
                    // function code; the value of the final expression is used as the return value
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

        string[] splitLine = context.FullLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int splitIndex = 0;

        foreach (var (param, conv) in ParameterConverters)
        {
            // Handle special types
            if (param.ParameterType == typeof(BotCommandContext))
            {
                args.Add(context);
                continue;
            }
            else if (param.ParameterType == typeof(CancellationToken))
            {
                args.Add(cancellationToken);
                continue;
            }
            // TODO: Add a RestAttribute to obtain the remainder of params as a string
            // TODO: Handle enumerables somehow by taking 0+ of a thing?

            // Handle regular types
            if (splitIndex < splitLine.Length && conv(splitLine[splitIndex], out value))
            {
                splitIndex++;
            }
            else if (param.GetCustomAttribute<RequiredAttribute>() is RequiredAttribute req)
            {
                // always throws ValidationException
                req.Validate(null, param.Name ?? $"_{args.Count + 1}");
                // never called, but needed to make the compiler happy that value is always initialized below
                continue;
            }
            else
            {
                value = Executor.GetDefaultValueForParameter(args.Count);
            }

            // TODO: figure out what asp.net passes here when validating params on actions to see if that's something we want to replicate or not
            var validationContext = ValidationContextFactory.Create(param);
            validationContext.MemberName = param.Name;
            validationContext.DisplayName = param.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? param.Name ?? $"_{args.Count + 1}";

            // throws ValidationException on validation failure
            Validator.ValidateValue(value, validationContext, param.GetCustomAttributes<ValidationAttribute>());

            // if we got here, the param is good
            args.Add(value);
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
