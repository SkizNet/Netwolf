using Netwolf.Server.Commands;
using Netwolf.Server.Internal;
using Netwolf.Transport.Commands;
using Netwolf.Transport.Extensions;

using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Sasl;

public class Plain : ISaslMechanismProvider
{
    public string Name => "PLAIN";

    public ISaslState InitializeForUser(User client)
    {
        throw new NotImplementedException();
    }

    private class State : ISaslState
    {
        // Should be a multiple of 400
        private const int MAX_SIZE = 1200;

        public string Name => "PLAIN";
        public bool Completed { get; private set; }
        public bool Errored { get; private set; }

        private User Client { get; set; }
        private int MessageCount { get; set; }
        private BoundedBuffer Buffer { get; init; } = new(400, MAX_SIZE);

        public State(User client)
        {
            Client = client;
        }

        public Task<ICommandResponse> ProcessClientCommandAsync(ICommand command, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            MessageCount++;
            if (MessageCount == 1)
            {
                return Task.FromResult<ICommandResponse>(
                    new CommandResponse(Client, null, "AUTHENTICATE", "+"));
            }

            if (command.Args[0] != "+")
            {
                if (Buffer.WrittenCount >= MAX_SIZE)
                {
                    // they're sending way too much data, reject them
                    Completed = true;
                    Errored = true;
                    return Task.FromResult<ICommandResponse>(
                        new NumericResponse(Client, Numeric.ERR_SASLFAIL));
                }

                Encoding.UTF8.GetBytes(command.Args[0], Buffer);
            }

            if (command.Args[0].Length == 400)
            {
                // we're expecting additional data from the client
                return Task.FromResult<ICommandResponse>(new EmptyResponse());
            }

            // client finished sending data, decode and process it
            if (Base64.DecodeFromUtf8InPlace(Buffer.WrittenSpan, out var bytesWritten) == OperationStatus.InvalidData)
            {
                // invalid base64
                Completed = true;
                Errored = true;
                return Task.FromResult<ICommandResponse>(
                    new NumericResponse(Client, Numeric.ERR_SASLFAIL));
            }

            Buffer.Reset(bytesWritten);
            // split into spans for authzid, authcid, and password
            // normalization of the strings (i.e. SASLPrep) is handled at a lower level, to ensure
            // that all authentication mechanisms use the same normalized strings
            ReadOnlySpan<byte> decoded = Buffer.WrittenSpan;
            int i = 0;
            Range authzid = Range.All,
                authcid = Range.All,
                password = Range.All;

            foreach (var range in decoded.Split((byte)0))
            {
                switch (i)
                {
                    case 0:
                        authzid = range;
                        break;
                    case 1:
                        authcid = range;
                        break;
                    case 2:
                        password = range;
                        break;
                    default:
                        // too many fields, invalid
                        Completed = true;
                        Errored = true;
                        return Task.FromResult<ICommandResponse>(
                            new NumericResponse(Client, Numeric.ERR_SASLFAIL));
                }

                i++;
            }

            if (i < 3)
            {
                // not enough fields, invalid
                Completed = true;
                Errored = true;
                return Task.FromResult<ICommandResponse>(
                    new NumericResponse(Client, Numeric.ERR_SASLFAIL));
            }

            // if authzid is empty, use authcid instead
            if (decoded[0] == 0)
            {
                authzid = authcid;
            }
        }
    }
}
