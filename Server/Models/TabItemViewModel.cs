using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace IPC_Demo;

public class TabItemViewModel : INotifyPropertyChanged
{
    static SolidColorBrush baseBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(28, 95, 178, 242)); // Microsoft.UI.Colors.DimGray
    public string Header { get; set; } = "New Connection";
    public string Sender { get; set; } = string.Empty;
    public IconSource Icon { get; set; } = new SymbolIconSource { Symbol = Symbol.World };
    public ObservableCollection<ApplicationMessage> Messages { get; set; } = new();

    /// <summary>
    /// We need the INotifyPropertyChanged for this to work during runtime.
    /// </summary>
    private SolidColorBrush _toggleColor = baseBrush;
    public SolidColorBrush ToggleColor
    {
        get => _toggleColor;
        set { _toggleColor = value; OnPropertyChanged(); }
    }

    #region [Activity Tracking]
    public DateTime LastActivity { get; set; } = DateTime.MinValue;
    public int ActivityScore { get; set; } = 0;
    public void RegisterActivity()
    {
        LastActivity = DateTime.Now;
        ActivityScore++;
    }
    public void DecayActivity()
    {
        if ((DateTime.Now - LastActivity).TotalSeconds >= 5)
        {
            ActivityScore = 0;
            ToggleColor = baseBrush;
        }
    }
    #endregion

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
