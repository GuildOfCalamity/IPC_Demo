using System.Diagnostics;
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
        int msSleep = 1500;
        int totalCycles = 400;

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

        Console.WriteLine($"🔔 Starting {ToReadableTime(totalCycles * msSleep)} IPC test…");
        Thread.Sleep(2500);

        // Run a 10 minute test
        for (int ipc = 0; ipc < totalCycles + 1; ipc++)
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
            Thread.Sleep(msSleep);
        }

        Console.WriteLine("🔔 Test complete");
        Thread.Sleep(1000);
    }

    /// <summary>
    /// Display a readable sentence as to when the time will happen.
    /// e.g. "8 minutes 0 milliseconds"
    /// </summary>
    /// <param name="milliseconds">integer value</param>
    /// <returns>human friendly format</returns>
    static string ToReadableTime(int milliseconds)
    {
        if (milliseconds < 0)
            throw new ArgumentException("Milliseconds cannot be negative.");

        TimeSpan timeSpan = TimeSpan.FromMilliseconds(milliseconds);

        if (timeSpan.TotalHours >= 1)
        {
            return string.Format("{0:0} hour{1} {2:0} minute{3}",
                timeSpan.Hours, timeSpan.Hours == 1 ? "" : "s",
                timeSpan.Minutes, timeSpan.Minutes == 1 ? "" : "s");
        }
        else if (timeSpan.TotalMinutes >= 1)
        {
            return string.Format("{0:0} minute{1} {2:0} second{3}",
                timeSpan.Minutes, timeSpan.Minutes == 1 ? "" : "s",
                timeSpan.Seconds, timeSpan.Seconds == 1 ? "" : "s");
        }
        else
        {
            return string.Format("{0:0} second{1} {2:0} millisecond{3}",
                timeSpan.Seconds, timeSpan.Seconds == 1 ? "" : "s",
                timeSpan.Milliseconds, timeSpan.Milliseconds == 1 ? "" : "s");
        }
    }
}
