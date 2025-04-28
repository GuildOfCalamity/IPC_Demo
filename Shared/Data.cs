using System;
using System.Text;

namespace Shared;

public class Data
{
    /// <summary>
    ///   Generates a technical gibberish sentence.
    /// </summary>
    public static string GenerateTechnicalGibberish(int wordCount)
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
