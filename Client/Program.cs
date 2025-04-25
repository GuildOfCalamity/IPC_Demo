using System.Text;

namespace Client;

public class Program
{
    #region [Properties]
    static bool _codeSent = false;
    static IpcClient? _ipc = null;
    static int Port { get; set; } = 32000; // default port
    static string Secret { get; set; } = "HeavyMetal";
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

        Console.WriteLine("• Starting 10 minute IPC test…");
        Thread.Sleep(2000);

        // Run a 10 minute test
        for (int ipc = 1; ipc < 401; ipc++)
        {
            if (_ipc != null)
            {
                Console.WriteLine($"📨 Sending IPC data #{ipc} to listener at {DateTime.Now.ToLongTimeString()}");
                _ipc.SendMessage("data", GenerateTechnicalSentence(Random.Shared.Next(5,16)), Secret);
            }
            else
            {
                Console.WriteLine("• Creating IPC client…");
                _ipc = new IpcClient(Port);
            }
            Thread.Sleep(1500);
        }
    }

    /// <summary>
    ///   Generates technical gibberish.
    /// </summary>
    static string GenerateTechnicalSentence(int wordCount)
    {
        string[] table = { "a", "server", "or", "workstation", "PC", "is", "technological", "technology", "power",
        "system", "used", "for", "diagnosing", "and", "analyzing", "data", "for", "reporting", "to", "on", "ethernet",
        "user", "monitor", "display", "interaction", "electric", "batteries", "along", "with", "some", "over", "heatsink",
        "under", "memory", "once", "in", "while", "special", "object", "can be", "found", "inside", "the", "blueprint",
        "CAT5", "CAT6", "TTL", "HD", "SSD", "USB", "CDROM", "NVMe", "GPU", "RAM", "NIC", "RAID", "SQL", "API", "XML", "JSON",
        "website", "at", "cluster", "fiber-optic", "floppy-disk", "media", "storage", "Windows", "operating", "root",
        "database", "access", "denied", "granted", "file", "files", "folder", "folders", "directory", "path", "surface-mount",
        "registry", "policy", "wire", "wires", "serial", "parallel", "bus", "fast", "slow", "speed", "bits", "DSL",
        "bytes", "voltage", "current", "resistance", "wattage", "circuit", "inspection", "measurement", "continuity",
        "diagram", "specifications", "robotics", "telecommunication", "applied", "internet", "science", "code",
        "password", "username", "wireless", "digital", "headset", "programming", "framework", "enabled", "disabled",
        "timer", "information", "keyboard", "mouse", "printer", "peripheral", "binary", "hexadecimal", "network",
        "router", "mainframe", "host", "client", "software", "version", "format", "upload", "download", "login",
        "logout", "embedded", "barcode", "driver", "image", "document", "flow", "layout", "uses", "configuration" };
        string word = string.Empty;
        StringBuilder builder = new StringBuilder();
        // Select a random word from the array until word count is satisfied.
        for (int i = 0; i < wordCount; i++)
        {
            string tmp = table[Random.Shared.Next(table.Length)];
            if (wordCount < table.Length)
                while (word.Equals(tmp) || builder.ToString().Contains(tmp)) { tmp = table[Random.Shared.Next(table.Length)]; }
            else
                while (word.Equals(tmp)) { tmp = table[Random.Shared.Next(table.Length)]; }
            builder.Append(tmp).Append(' ');
            word = tmp;
        }
        string sentence = builder.ToString().Trim() + ".";
        // Set the first letter of the first word in the sentence to uppercase.
        sentence = char.ToUpper(sentence[0]) + sentence.Substring(1);
        return sentence;
    }
}
