using System.Text;

namespace Client;

public class Program
{
    #region [Properties]
    static IpcClient? _ipc = null;

    /// <summary>
    /// Default port number for the IPC connection.
    /// </summary>
    static int Port { get; set; } = 32000;

    /// <summary>
    /// Server and client must share this secret.
    /// Don't hard-code this as I have, this is just an example demo.
    /// </summary>
    static string Secret { get; set; } = "9hOfBy7beK0x3zX4";
    #endregion

    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        // Check if we were passed a port option
        if (args.Length > 0)
        {
            if (int.TryParse(args[0], out int portOption))
            {
                Console.WriteLine($"🔔 Port set to {args[0]}");
                Port = portOption;
            }
            else
            {
                Console.WriteLine($"⚠️ Invalid port option '{args[0]}', using default port {Port}");
            }
        }

        Console.WriteLine("🔔 Starting 10 minute IPC test…");
        Thread.Sleep(2000);

        // Run a 10 minute test
        for (int ipc = 0; ipc < 401; ipc++)
        {
            if (_ipc != null)
            {
                Console.WriteLine($"📨 Sending IPC data #{ipc} to listener at {DateTime.Now.ToLongTimeString()}");
                _ipc.SendMessage("data", Shared.Data.GenerateTechnicalGibberish(Random.Shared.Next(5,16)), Secret);
            }
            else
            {
                Console.WriteLine("🔔 Creating IPC client…");
                _ipc = new IpcClient(Port);
            }
            Thread.Sleep(1500);
        }
    }
}
