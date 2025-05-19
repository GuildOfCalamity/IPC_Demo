using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml.Controls;

namespace IPC_Demo;

public class TabItemViewModel
{
    public string Header { get; set; } = "New Connection";
    public string Sender { get; set; } = string.Empty;
    public IconSource Icon { get; set; } = new SymbolIconSource { Symbol = Symbol.World };
    public ObservableCollection<ApplicationMessage> Messages { get; set; } = new();
}
