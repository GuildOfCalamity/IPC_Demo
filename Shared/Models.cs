using System;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shared;

public class IpcMessage
{
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string Time { get; set; } = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ"); // JSON date-time format
    public string Sender { get; set; } = Environment.MachineName;
    public string Secret { get; set; } = string.Empty; // HMAC key

    public override string ToString()
    {
        //return $"Type: {Type} ⇒  Sender: {Sender}  Time: {Time}{Environment.NewLine}Payload: {Payload}";
        
        // *NOTE* This exposes the "secret" in the JSON output string.
        return JsonSerializer.Serialize<IpcMessage>(this, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true,
        });
    }
}
