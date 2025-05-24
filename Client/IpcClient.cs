using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Client;

public class IpcClient
{
    public int SocketErrors { get; set; } = 0;
    public int IOErrors { get; set; } = 0;
    public int Port { get; set; } = 32000;
    public string Host { get; set; } = "localhost";

    static Shared.ValueStopwatch _vsw = Shared.ValueStopwatch.StartNew();

    public IpcClient(int port)
    {
        Port = port;
    }

    /// <summary>
    /// This could be changed to use one <see cref="TcpClient"/> for messages instead of
    /// making a new <see cref="TcpClient.ConnectAsync(string, int)"/> each time, but
    /// there is no memory leak with this technique and a <see cref="CancellationToken"/>
    /// could be added to the <see cref="TcpClient.ConnectAsync(string, int)"/> for timeouts.
    /// </summary>
    /// <param name="type">the type of message</param>
    /// <param name="payload">the data to send</param>
    /// <param name="secret">the shared security key</param>
    /// <param name="sender">if empty the local machine name is used</param>
    public async void SendMessage(string type, string payload, string secret, string sender)
    {
        string json = JsonSerializer.Serialize(new Shared.IpcMessage
        {
            Type = type ?? string.Empty,
            Payload = payload ?? string.Empty,
            Secret = Shared.SecurityHelper.GenerateSecureCode6(secret),
            Sender = string.IsNullOrEmpty(sender) ? Environment.MachineName : sender,
            //Time = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ") // JSON date-time format
            //Time = DateTime.UtcNow.ToString("o")                 // ISO 8601 format
        });

        using (var client = new TcpClient())
        {
            try
            {
                _vsw = Shared.ValueStopwatch.StartNew();

                // Connect to the server and write the message into the network stream.
                await client.ConnectAsync(Host, Port);
                using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };
                await writer.WriteLineAsync(json);
                
                Debug.WriteLine($"[INFO] Connect and write took {_vsw.GetElapsedFriendly()}");
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"⚠️ SocketException: {ex.Message}");
                if (++SocketErrors > 3)
                {
                    Console.WriteLine($"{Environment.NewLine}🚨 Socket errors exceeded limit - Exiting 🚨{Environment.NewLine}");
                    Thread.Sleep(2000);
                    Environment.Exit(1);
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine($"⚠️ IOException: {ex.Message}");
                if (++IOErrors > 3)
                {
                    Console.WriteLine($"{Environment.NewLine}🚨 I/O errors exceeded limit - Exiting 🚨{Environment.NewLine}");
                    Thread.Sleep(2000);
                    Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Exception: {ex.Message}");
            }
        }
    }
}
