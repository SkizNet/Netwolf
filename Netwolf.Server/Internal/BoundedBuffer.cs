using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Internal;

internal class BoundedBuffer : IBufferWriter<byte>
{
    private readonly int _maxSize;
    private byte[] _buffer;
    private int _index;

    public int Capacity => _buffer.Length;
    public int FreeCapacity => _buffer.Length - _index;
    public int WrittenCount => _index;
    public Memory<byte> WrittenMemory => _buffer.AsMemory(0, _index);
    public Span<byte> WrittenSpan => _buffer.AsSpan(0, _index);

    internal BoundedBuffer(int initialSize, int maxSize)
    {
        if (initialSize > maxSize)
        {
            throw new ArgumentOutOfRangeException(nameof(initialSize), "Initial size cannot be larger than maximum size.");
        }

        if (maxSize == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSize), "Maximum size must be greater than zero.");
        }

        _maxSize = maxSize;
        _buffer = new byte[initialSize];
        _index = 0;
    }

    public void Advance(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be non-negative.");
        }

        if (count > FreeCapacity)
        {
            throw new InvalidOperationException("Cannot advance beyond the current buffer size.");
        }

        _index += count;
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        MaybeResize(sizeHint);
        return _buffer.AsMemory(_index);
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        MaybeResize(sizeHint);
        return _buffer.AsSpan(_index);
    }

    public void Reset(int newIndex = 0)
    {
        if (newIndex < 0 || newIndex > Capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(newIndex), "New index must be within the bounds of the buffer.");
        }

        _index = newIndex;
    }

    /// <summary>
    /// Securely erase and reset the buffer,
    /// for use when it contains sensitive data
    /// </summary>
    public void Erase()
    {
        _index = 0;
        CryptographicOperations.ZeroMemory(_buffer);
    }

    private void MaybeResize(int sizeHint)
    {
        if (sizeHint < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeHint), "Size hint must be non-negative.");
        }

        if (Capacity == _maxSize)
        {
            throw new InvalidOperationException("Cannot resize buffer beyond maximum size.");
        }

        if (sizeHint == 0)
        {
            sizeHint = 1;
        }

        if (sizeHint < FreeCapacity)
        {
            return;
        }

        var newSize = Math.Min(Math.Max(Capacity * 2, _index + sizeHint), _maxSize);
        Array.Resize(ref _buffer, newSize);
    }
}
