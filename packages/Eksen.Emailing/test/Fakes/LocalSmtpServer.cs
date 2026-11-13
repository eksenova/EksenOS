using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Eksen.Emailing.Tests.Fakes;

public sealed class LocalSmtpServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _loop;
    public ConcurrentQueue<string> Messages { get; } = new();
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
    private readonly string? _recipientRejection;

    /// <param name="recipientRejection">
    /// When set, every <c>RCPT TO</c> is answered with this reply, for example
    /// <c>550 5.1.1 &lt;person@example.test&gt;: Recipient address rejected</c>.
    /// </param>
    public LocalSmtpServer(string? recipientRejection = null)
    {
        _recipientRejection = recipientRejection;
        _listener.Start();
        _loop = ListenAsync();
    }
    private async Task ListenAsync()
    {
        try
        {
            while (!_cancellation.IsCancellationRequested)
            {
                using var connection = await _listener.AcceptTcpClientAsync(_cancellation.Token);
                try
                {
                    await ServeAsync(connection);
                }
                catch (IOException) when (!_cancellation.IsCancellationRequested)
                {
                    // The client dropped the connection (SmtpClient does after a rejected command).
                }
            }
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) { }
        catch (SocketException) when (_cancellation.IsCancellationRequested) { }
    }

    private async Task ServeAsync(TcpClient connection)
    {
        await using var network = connection.GetStream();
        using var reader = new StreamReader(network, Encoding.UTF8, leaveOpen: true);
        await using var writer = new StreamWriter(network, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };
        await writer.WriteLineAsync("220 fixture.local ESMTP");
        while (await reader.ReadLineAsync(_cancellation.Token) is { } line)
        {
            if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase)) { await writer.WriteLineAsync("250 fixture.local"); continue; }
            if (line.Equals("DATA", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("354 End with dot");
                var message = new StringBuilder();
                while (await reader.ReadLineAsync(_cancellation.Token) is { } body && body != ".") message.AppendLine(body);
                Messages.Enqueue(message.ToString());
                await writer.WriteLineAsync("250 Message accepted"); continue;
            }
            if (_recipientRejection is not null && line.StartsWith("RCPT TO", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync(_recipientRejection); continue;
            }
            if (line.Equals("QUIT", StringComparison.OrdinalIgnoreCase)) { await writer.WriteLineAsync("221 Goodbye"); break; }
            await writer.WriteLineAsync("250 OK");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cancellation.Cancel(); _listener.Stop(); await _loop; _cancellation.Dispose();
    }
}
