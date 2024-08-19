using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Netwolf.Transport.MFA;

public class MfaTotp : IMfaMechanism
{
    public string Name => "TOTP";

    public string ScramName => "totp";

    public delegate Task<string> TokenRetrievalCallback(int outputSize, int stepSize, HashAlgorithmName hashAlgorithm);

    /// <summary>
    /// Number of digits to output for the TOTP code.
    /// </summary>
    public int OutputSize { get; set; } = 6;

    /// <summary>
    /// Duration of a step, in seconds.
    /// </summary>
    public int StepSize { get; set; } = 30;

    /// <summary>
    /// Hash algorithm used to generate the TOTP code.
    /// </summary>
    public HashAlgorithmName HashAlgorithm { get; set; } = HashAlgorithmName.SHA1;

    /// <summary>
    /// Number of past steps we will accept in addition to the current step when validating.
    /// </summary>
    public int BackwardStepSkew { get; set; } = 1;

    /// <summary>
    /// Number of future steps we will accept in addition to the current step when validating.
    /// </summary>
    public int ForwardStepSkew { get; set; } = 1;

    /// <summary>
    /// Initial UNIX timestamp from which we started generating tokens.
    /// Generally it's fine to leave this at 0, otherwise this should be set to the
    /// initial enrollment time for the given TOTP code. If this timestamp is not
    /// a multiple of <see cref="StepSize"/>.
    /// </summary>
    public long InitialTime { get; set; } = 0;

    /// <summary>
    /// UNIX timestamp representing the earliest step we will accept when validating.
    /// </summary>
    public long ReplayToken { get; set; } = 0;

    object? IMfaMechanism.ReplayToken => ReplayToken;

    private byte[]? SecretKey { get; init; }

    private TokenRetrievalCallback? Callback { get; init; }

    private readonly int[] _powersOf10Cache =
    [
        1,
        10,
        100,
        1000,
        10000,
        100000,
        1000000,
        10000000,
        100000000,
        1000000000,
    ];

    /// <summary>
    /// Create a new TOTP MFA instance with a known secret key.
    /// This is primarily useful server-side (when validating tokens) or in unit testing.
    /// Client implementations likely will not want to directly store the key, but instead
    /// prompt the user for the value or interface with an HSM via the
    /// <see cref="MfaTotp(TokenRetrievalCallback)"/> constructor.
    /// The key is stored in memory without any protections.
    /// </summary>
    /// <param name="secretKey">Key to use</param>
    public MfaTotp(byte[] secretKey)
    {
        SecretKey = secretKey;
    }

    /// <summary>
    /// Create a new TOTP MFA instance where the callback is used to retrieve the
    /// token value. This constructor cannot be used for token validation, only generation.
    /// </summary>
    /// <param name="callback"></param>
    public MfaTotp(TokenRetrievalCallback callback)
    {
        Callback = callback;
    }

    public async Task<string> GetTokenAsync(byte[]? challenge = null)
    {
        if (Callback != null)
        {
            return await Callback(OutputSize, StepSize, HashAlgorithm);
        }

        if (SecretKey == null)
        {
            throw new InvalidOperationException("Missing secret key or token callback for TOTP code generation");
        }

        return GenerateToken(GetCurrentStep()).ToString();
    }

    public Task<bool> ValidateAsync(string token, byte[]? challenge = null)
    {
        if (SecretKey == null)
        {
            throw new InvalidOperationException("Secret key is required to validate TOTP codes");
        }

        if (token.Length != OutputSize || !Int32.TryParse(token, out int providedToken))
        {
            return Task.FromResult(false);
        }

        var now = GetCurrentStep();
        var notBefore = GetStep(ReplayToken);
        for (var step = now - BackwardStepSkew; step <= now + ForwardStepSkew; ++step)
        {
            if (step < notBefore)
            {
                continue;
            }

            if (providedToken == GenerateToken(step))
            {
                ReplayToken = (step + 1) * StepSize + InitialTime;
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    private long GetCurrentStep()
    {
        return GetStep(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    private long GetStep(long timestamp)
    {
        return (timestamp - InitialTime) / StepSize;
    }

    private int GenerateToken(long counter)
    {
        if (SecretKey == null)
        {
            throw new InvalidOperationException("Secret key is required to generate TOTP codes");
        }

        // int.MaxValue is expressed as 9 digits, hence that particular cap
        if (OutputSize < 6 || OutputSize > 9)
        {
            throw new InvalidOperationException("Output size must be at least 6 and at most 9 digits");
        }

        Func<byte[], byte[], byte[]> hmac = HashAlgorithm.Name switch
        {
            "SHA1" => HMACSHA1.HashData,
            "SHA256" => HMACSHA256.HashData,
            "SHA512" => HMACSHA512.HashData,
            _ => throw new InvalidOperationException("Unsupported hash algorithm for TOTP")
        };

        // Step 1: Generate an HMAC value
        byte[] counterBytes = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counterBytes, counter);
        var HS = hmac(SecretKey, counterBytes);

        // Step 2: Generate a 4-byte string (Dynamic Truncation)
        var offset = HS[^1] & 0x0f;
        HS[offset] &= 0x7f; // clear the top bit to ensure the number is read as a positive int
        var P = new ReadOnlySpan<byte>(HS, offset, 4);

        // Step 3: Compute an HOTP value
        var S = BinaryPrimitives.ReadInt32BigEndian(P);
        return S % _powersOf10Cache[OutputSize];
    }
}
