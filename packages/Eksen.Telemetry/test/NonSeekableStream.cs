namespace Eksen.Telemetry.Tests;

/// <summary>
/// A forward-only stream that returns at most three bytes per read, like a chunked network body.
/// </summary>
public sealed class NonSeekableStream(byte[] payload) : Stream
{
    private readonly MemoryStream _inner = new(payload, writable: false);

    public override bool CanRead
    {
        get { return true; }
    }

    public override bool CanSeek
    {
        get { return false; }
    }

    public override bool CanWrite
    {
        get { return false; }
    }

    public override long Length
    {
        get { throw new NotSupportedException(); }
    }

    public override long Position
    {
        get { throw new NotSupportedException(); }
        set { throw new NotSupportedException(); }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return _inner.Read(buffer, offset, Math.Min(count, 3));
    }

    public override int Read(Span<byte> buffer)
    {
        return _inner.Read(buffer[..Math.Min(buffer.Length, 3)]);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return _inner.ReadAsync(buffer[..Math.Min(buffer.Length, 3)], cancellationToken);
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
