// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Internal;

using Netwolf.Attributes;
using Netwolf.BotFramework.Services;
using Netwolf.Generator.Attributes;
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

    private delegate bool Converter(ReadOnlySpan<char> input, out object? output);

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
            // CreateDelegate doesn't work here because TypeHelper.TryChangeType has T as an out param, so we need to build up a linq expression
            // that calls the function and performs appropriate type casts, and then compile that expression.
            var convType = (param.ParameterType.IsSZArray ? param.ParameterType.GetElementType() : param.ParameterType)
                ?? throw new InvalidOperationException("Unsupported array type in bot command signature");

            var inParam = Expression.Parameter(typeof(ReadOnlySpan<char>), "input");
            var converted = Expression.Variable(convType, "c");
            var success = Expression.Variable(typeof(bool), "r");
            var outParam = Expression.Parameter(typeof(object).MakeByRefType(), "output");
            var lambda = Expression.Lambda<Converter>(
                Expression.Block(
                    // internal variables used
                    [converted, success],
                    // function code; the value of the final expression is used as the return value
                    Expression.Assign(outParam, Expression.Constant(null)),
                    Expression.Assign(success, Expression.Call(convertMethod.MakeGenericMethod(convType), inParam, converted)),
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

        var lineSpan = context.RawArgs.AsSpan();
        int lineLength = context.RawArgs.Length;
        var splitEnumerator = lineSpan.Split(' ');

        static bool advance(ref MemoryExtensions.SpanSplitEnumerator<char> enumerator, int length)
        {
            while (enumerator.MoveNext())
            {
                var (_, len) = enumerator.Current.GetOffsetAndLength(length);
                if (len == 0)
                {
                    continue;
                }

                return true;
            }

            return false;
        };

        foreach (var (param, conv) in ParameterConverters)
        {
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
            else if (param.GetCustomAttribute<CommandNameAttribute>() != null)
            {
                if (conv(command.Verb, out value))
                {
                    args.Add(value);
                    continue;
                }
                else
                {
                    throw new InvalidOperationException("The parameter with CommandNameAttribute needs to be a string or convertible from string");
                }
            }
            else if (param.GetCustomAttribute<RestAttribute>() != null && advance(ref splitEnumerator, lineLength))
            {
                if (!conv(lineSpan[new Range(splitEnumerator.Current.Start, Index.End)], out value))
                {
                    throw new InvalidOperationException("The parameter with RestAttribute needs to be a string or convertible from string");
                }

                while (advance(ref splitEnumerator, lineLength)) { /* consume the remainder of the parameters too */ }

                // no continue here as we want the default validation logic below
            }
            else if (param.ParameterType.IsSZArray)
            {
                // make a copy of the enumerator since we grab as many args as we can for the array but stop once conversion fails
                // however if we advance to the point that conversion fails, that arg gets skipped over because it'll advance again during the next iteration
                var copy = splitEnumerator;
                var listType = typeof(List<>).MakeGenericType(param.ParameterType.GetElementType()!);
                dynamic arrayBuilder = Activator.CreateInstance(listType)!;

                while (advance(ref copy, lineLength) && conv(lineSpan[copy.Current], out value))
                {
                    arrayBuilder.Add((dynamic?)value);
                    advance(ref splitEnumerator, lineLength);
                }

                // convert the List<whatever> to whatever[] since that's what param expects
                value = arrayBuilder.ToArray();
            }
            else if (advance(ref splitEnumerator, lineLength) && conv(lineSpan[splitEnumerator.Current], out value))
            {
                // nothing to do in this block; value was set as part of conv()
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
