using System;

namespace IPC_Demo;

public class ApplicationMessage : ICloneable
{
    public ModuleId Module { get; set; }
    public string? MessageText { get; set; }
    //[JsonIgnore]
    public Type? MessageType { get; set; }
    public object? MessagePayload { get; set; }
    public DateTime MessageTime { get; set; } = DateTime.Now;
    public object Clone() => this.MemberwiseClone();
    public override string ToString() => $"{Module} => {MessageText} => {MessageTime}";
}

public enum MessageLevel
{
    Debug = 0,
    Information = 1,
    Important = 2,
    Warning = 3,
    Error = 4
}

public enum ModuleId
{
    None = 0,
    App = 1,
    MainWindow = 2,
    MainPage = 3,
    IPC_Client = 4,
    IPC_Passed = 5,
    IPC_Failed = 6,
}
