using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace IPC_Demo;
/*

 Example usage (server)
-----------------------------------------------
var server = new IpcHelper(port: 32000);
server.MessageReceived += (msg) => Console.WriteLine($"📨 Received: {msg}");
server.JsonMessageReceived += (jmsg) => Console.WriteLine($"📨 JSON: {jmsg}");
server.ErrorOccurred += (err) => Console.WriteLine($"⚠ Server: {err.Message}");
server.Start();
// Call server.Stop() or server.Dispose() to shut down


 Sending message from external app (client)
-----------------------------------------------
string json = JsonSerializer.Serialize(new IpcMessage { Type = "keycode", Payload = "12345678" });
using (var client = new TcpClient())
{
   await client.ConnectAsync("localhost", 32000);
   using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };
   await writer.WriteLineAsync(json);
}
*/
public class IpcHelper : IDisposable
{
    private readonly TcpListener _listener;
    private CancellationTokenSource? _cts;
    Dictionary<EndPoint, DateTime> _endpoints;

    public int Port { get; }

    /// <summary>
    /// Raw data event
    /// </summary>
    public event Action<string>? MessageReceived;

    /// <summary>
    /// Formatted data event
    /// </summary>
    public event Action<Shared.IpcMessage>? JsonMessageReceived;

    /// <summary>
    /// Exception event
    /// </summary>
    public event Action<Exception>? ErrorOccurred;

    public IpcHelper(int port = 32000)
    {
        Port = port;
        _endpoints = new Dictionary<EndPoint, DateTime>();
        _listener = new TcpListener(IPAddress.Loopback, Port);
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _listener.Start();
        Task.Run(() => AcceptClientsAsync(_cts.Token));

    }

    /// <summary>
    /// This could also be re-purposed as a white-list.
    /// </summary>
    public Dictionary<EndPoint, DateTime> GetConnectionHistory()
    {
        return _endpoints;
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener.Stop();
    }

    async Task AcceptClientsAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken); // blocking call
                if (client != null)
                {
                    if (_endpoints.ContainsKey(client.Client.LocalEndPoint!))
                        Debug.WriteLine($"🔔 Client already connected at remote endpoint {Extensions.FormatEndPoint(client.Client.RemoteEndPoint)}");
                    else
                        _endpoints.Add(client.Client.LocalEndPoint!, DateTime.Now);

                    // Process the inbound connection data
                    _ = Task.Run(() => HandleClientAsync(client, cancellationToken));
                }
            }
        }
        catch (OperationCanceledException) { /* token signaled */ }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex);
        }
    }

    async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        try
        {
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            if (!cancellationToken.IsCancellationRequested && client.Connected)
            {
                string? line = await reader.ReadLineAsync(cancellationToken); // non-blocking read
                if (line != null)
                {
                    MessageReceived?.Invoke(line); // Fire event with raw data
                    try
                    {
                        var obj = JsonSerializer.Deserialize<Shared.IpcMessage>(line);
                        if (obj != null)
                            JsonMessageReceived?.Invoke(obj);
                    }
                    catch (JsonException jsonEx)
                    {
                        ErrorOccurred?.Invoke(jsonEx);
                    }
                }
                else
                    ErrorOccurred?.Invoke(new Exception("Client sent no data."));
            }
            else
            {
                ErrorOccurred?.Invoke(new Exception("Client is not connected or cancellation was requested."));
            }
        }
        catch (OperationCanceledException) { /* token signaled */ }
        catch (IOException ioex) when (ioex.InnerException is SocketException) { /* client disconnected */ }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex);
        }
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }

    ~IpcHelper() => Dispose();
}
