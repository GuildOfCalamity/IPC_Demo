using System;
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

    public IpcClient(int port)
    {
        Port = port;
    }

    public async void SendMessage(string type, string payload, string secret)
    {
        string json = JsonSerializer.Serialize(new Shared.IpcMessage
        {
            Type = type ?? string.Empty,
            Payload = payload ?? string.Empty,
            Secret = Shared.SecurityHelper.GenerateSecureCode6(secret),
            //Time = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ") // JSON date-time format
            //Time = DateTime.UtcNow.ToString("o") // ISO 8601 format
        });

        using (var client = new TcpClient())
        {
            try
            {
                await client.ConnectAsync(Host, Port);
                using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };
                await writer.WriteLineAsync(json);
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"⚠️ SocketException: {ex.Message}");
                if (++SocketErrors > 3)
                {
                    Console.WriteLine($"{Environment.NewLine}🚨 Socket errors exceeded limit - Exiting 🚨{Environment.NewLine}");
                    Environment.Exit(1);
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine($"⚠️ IOException: {ex.Message}");
                if (++IOErrors > 3)
                {
                    Console.WriteLine($"{Environment.NewLine}🚨 I/O errors exceeded limit - Exiting 🚨{Environment.NewLine}");
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
