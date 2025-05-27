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
    /// The sender's name.
    /// </summary>
    static string Sender { get; set; } = Environment.MachineName;

    /// <summary>
    /// Stress test flag.
    /// </summary>
    static bool StressTest { get; set; } = false;

    /// <summary>
    /// For simulating faulty security PIN.
    /// </summary>
    static bool RandomFaultyPIN { get; set; } = false;

    /// <summary>
    /// Server and client must share this secret.
    /// Don't hard-code this as I have, this is just an example demo.
    /// </summary>
    static string Secret { get; set; } = "9hOfBy7beK0x3zX4";
    #endregion

    static void Main(string[] args)
    {
        int msSleep = 980;
        int totalCycles = 500;

        Console.OutputEncoding = Encoding.UTF8;

        if (Debugger.IsAttached)
        {
            Console.WriteLine($"🔔 Debugger detected, enabling additional test modes (may still be overridden with switches)");
            StressTest = RandomFaultyPIN = true;
        }

        #region [Argument checks]
        // Check if we were passed a port option
        if (args.Length > 0)
        {
            if (int.TryParse(args[0], out int portOption))
            {
                Console.WriteLine($"🔔 Port set to {args[0]}");
                Port = portOption;
            }
            else
                Console.WriteLine($"⚠️ Invalid port option '{args[0]}', using default port {Port}");
        }

        // Check if we were passed a sender option
        if (args.Length > 1)
        {
            if (!string.IsNullOrEmpty(args[1]))
            {
                Console.WriteLine($"🔔 Sender set to '{args[1]}'");
                Sender = args[1];
            }
            else
                Console.WriteLine($"⚠️ Invalid sender option '{args[1]}', using default sender");
        }

        // Check if we were passed a stress test option
        if (args.Length > 2)
        {
            Console.WriteLine($"🔔 Stress test is enabled");
            StressTest = true;
        }
        else
        {
            Console.WriteLine($"🔔 Stress test is disabled");
        }

        // Check if we were passed a faulty PIN test option
        if (args.Length > 3)
        {
            Console.WriteLine($"🔔 Faulty PIN test is enabled");
            RandomFaultyPIN = true;
        }
        else
        {
            Console.WriteLine($"🔔 Faulty PIN test is disabled");
        }
        #endregion

        Console.WriteLine($"🔔 Starting {ToReadableTime(totalCycles * msSleep)} IPC test…");
        Thread.Sleep(2500);

        // Run a 10 minute test
        for (int ipc = 0; ipc < totalCycles + 1; ipc++)
        {
            if (_ipc != null)
            {
                // For stress-testing purposes we can append a random char to the header so
                // it appears like more than one application is connecting to our server.
                string name = string.Empty;
                if (StressTest)
                    name = $"{Sender}{AppendRandom()}";
                else
                    name = $"{Sender}";

                if (RandomFaultyPIN && ipc > 10 && Random.Shared.Next(101) >= 95) // 5% chance of PIN fail
                {
                    Console.WriteLine($"📨 Sending IPC data #{ipc} with faulty secret from '{name}' to listener at {DateTime.Now.ToLongTimeString()}");
                    _ipc.SendMessage("data", Shared.Data.GenerateTechnicalGibberish(Random.Shared.Next(5, 16)), $"{Secret}{AppendRandom()}", $"{name}");
                }
                else
                {
                    Console.WriteLine($"📨 Sending IPC data #{ipc} from '{name}' to listener at {DateTime.Now.ToLongTimeString()}");
                    _ipc.SendMessage("data", Shared.Data.GenerateTechnicalGibberish(Random.Shared.Next(5, 16)), Secret, $"{name}");
                }
            }
            else
            {
                Console.WriteLine("🔔 Creating IPC client…");
                _ipc = new IpcClient(Port);
            }
            Thread.Sleep(msSleep);
        }

        Console.WriteLine("🔔 Test complete");
        Thread.Sleep(1500);
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

    /// <summary>
    /// Helper for stress-test. 
    /// Used to fool the message handler into thinking we have multiple clients.
    /// </summary>
    /// <returns>random letter</returns>
    static string AppendRandom()
    {
        const string idChars = "abcdefghijklmnopqrstuvwxyz";
        char[] charArray = idChars.Distinct().ToArray();
        return $"{charArray[Random.Shared.Next() % charArray.Length]}";
    }
}
