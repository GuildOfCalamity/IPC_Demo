using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace IPC_Demo;

/// <summary>
/// This is the server project, the client project can be found at "..\Client\Client.csproj".
/// </summary>
public sealed partial class MainPage : Page, INotifyPropertyChanged
{
    #region [Properties]
    int _total = 0;
    bool _loaded = false;
    string _secret = "HeavyMetal";
    readonly int _maxMessages = 50;
    readonly ObservableCollection<ApplicationMessage>? _tab1Messages;
    readonly ObservableCollection<ApplicationMessage>? _tab2Messages;
    readonly ObservableCollection<ApplicationMessage>? _tab3Messages;
    public event PropertyChangedEventHandler? PropertyChanged;
    IpcHelper? ipcServer = null;
    
    bool _isBusy = false;
    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; NotifyPropertyChanged(nameof(IsBusy)); }
    }

    public void NotifyPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
    {
        if (string.IsNullOrEmpty(propertyName)) { return; }
        // Confirm that we're on the UI thread in the event that DependencyProperty is changed under forked thread.
        this.DispatcherQueue.InvokeOnUI(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
    }
    #endregion

    public MainPage()
    {
        this.InitializeComponent();
        this.Loaded += IpcPageOnLoaded;
        this.Unloaded += IpcPageOnUnloaded;
        try
        {
            // We could change this to dynamically create a tab when a new client
            // connection occurs. But for now, we'll just use the first 3 tabs.
            _tab1Messages = new();
            Binding binding1 = new Binding { Mode = BindingMode.OneWay, Source = _tab1Messages };
            BindingOperations.SetBinding(lvTab1Messages, ListView.ItemsSourceProperty, binding1);

            _tab2Messages = new();
            Binding binding2 = new Binding { Mode = BindingMode.OneWay, Source = _tab2Messages };
            BindingOperations.SetBinding(lvTab2Messages, ListView.ItemsSourceProperty, binding2);

            _tab3Messages = new();
            Binding binding3 = new Binding { Mode = BindingMode.OneWay, Source = _tab3Messages };
            BindingOperations.SetBinding(lvTab3Messages, ListView.ItemsSourceProperty, binding3);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠ {MethodBase.GetCurrentMethod()?.DeclaringType?.Name}(Error): {ex.Message}");
        }
    }

    #region [Page Events]
    void IpcPageOnUnloaded(object sender, RoutedEventArgs e)
    {
        ipcServer?.Stop();
        if (_tab1Messages != null && _tab1Messages.Count > 1)
            App.MessageLog?.SaveData(_tab1Messages.ToList());
    }
    void IpcPageOnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_loaded && this.Content != null)
        {
            #region [Fetch previous messages]
            try
            {
                int count = 0;
                var prevMsgs = App.MessageLog?.GetData();
                if (prevMsgs is not null)
                {
                    foreach (var msg in prevMsgs)
                    {
                        // Load messages as long as we aren't exceeding the limit and they're fresh.
                        if (++count < _maxMessages && !msg.MessageTime.IsOlderThanDays(2d))
                            _tab1Messages.Add(msg);
                    }
                    Debug.WriteLine($"[INFO] {count} previous messages loaded");
                }
            }
            catch (Exception) { UpdateInfoBar($"Failed to load previous message list.", MessageLevel.Warning); }
            #endregion

            #region [Setup IPC and events]
            ipcServer = new IpcHelper(port: 32000);

            // --= Message received event =--
            ipcServer.MessageReceived += (msg) =>
            {
                Debug.WriteLine($"Received 📨 {msg}");
                this.DispatcherQueue.TryEnqueue(() => 
                {
                    /** Tab 3 (connection history) **/

                    if (_tab3Messages.Count == 0)
                        tvi3.Header = $"Connections";
                    else if (_tab3Messages.Count > _maxMessages)
                        _tab3Messages.RemoveAt(_maxMessages);

                    var obj = JsonSerializer.Deserialize<Shared.IpcMessage>(msg);
                    if (obj != null)
                    {
                        var dict = ipcServer.GetConnectionHistory();
                        foreach (var item in dict)
                        {
                            var conMsg = new ApplicationMessage
                            {
                                Module = ModuleId.IPC_Client,
                                MessagePayload = $"{item.Key}",
                                MessageText = $"💻 {item.Key}    ⌚ {item.Value}    ⏱️ {obj.Time}",
                                MessageType = typeof(Shared.IpcMessage),
                                MessageTime = item.Value,
                            };
                            //if (_tab3Messages.Count != dict.Count)
                            _tab3Messages?.Insert(0, conMsg);
                        }
                    }
                });
            };

            // --= JSON message received event =--
            ipcServer.JsonMessageReceived += (jmsg) =>
            {
                if (_loaded && jmsg != null)
                {
                    // Check if it's a message from us by us.
                    if (!string.IsNullOrEmpty(jmsg.Sender) && jmsg.Sender == Environment.MachineName)
                    {
                        // Security check
                        if (Shared.SecurityHelper.VerifySecureCode(jmsg.Secret, _secret))
                        {
                            this.DispatcherQueue.TryEnqueue(() =>
                            {
                                /** Tab 1 (message details) **/

                                tsf.Text = $"Received msg #{++_total}";

                                if (_tab1Messages.Count == 0)
                                    tvi1.Header = $"{jmsg.Sender}";
                                else if (_tab1Messages.Count > _maxMessages)
                                    _tab1Messages.RemoveAt(_maxMessages);

                                var appMsg = new ApplicationMessage
                                {
                                    Module = ModuleId.IPC_Passed,
                                    MessagePayload = jmsg,
                                    MessageText = $"{jmsg}",
                                    MessageType = typeof(Shared.IpcMessage),
                                    MessageTime = jmsg.Time.ParseJsonDateTime(),
                                };
                                _tab1Messages?.Insert(0, appMsg);
                            });
                        }
                        else
                        {
                            this.DispatcherQueue.TryEnqueue(() =>
                            {
                                /** Tab 2 (failed security check) **/

                                if (_tab2Messages.Count == 0)
                                    tvi2.Header = $"{jmsg.Sender}";
                                else if (_tab2Messages.Count > _maxMessages)
                                    _tab2Messages.RemoveAt(_maxMessages);

                                var appMsg = new ApplicationMessage
                                {
                                    Module = ModuleId.IPC_Failed,
                                    MessagePayload = jmsg,
                                    MessageText = $"{jmsg}",
                                    MessageType = typeof(Shared.IpcMessage),
                                    MessageTime = jmsg.Time.ParseJsonDateTime(),
                                };
                                _tab2Messages?.Insert(0, appMsg);
                            });
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"⚠ Disallowed/Unknown sender '{jmsg.Sender}'");
                    }
                }
                else
                    Debug.WriteLine($"JSON 📨 {jmsg}");
            };

            // --= Server error event =--
            ipcServer.ErrorOccurred += (err) =>
            {
                Debug.WriteLine($"⚠ Server: {err.Message}");
            };
            ipcServer.Start();
            #endregion
        }
        _loaded = true;

        var workingCode = Shared.SecurityHelper.GenerateSecureCode(_secret);
        UpdateInfoBar($"Secure code for the next hour will be {workingCode}", MessageLevel.Information);
    }
    #endregion

    void UpdateInfoBar(string msg, MessageLevel level = MessageLevel.Information)
    {
        if (App.IsClosing || infoBar == null)
            return;

        //DispatcherQueue.InvokeOnUI(() => { tbMessages.Text = msg; });

        _ = infoBar.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
        {
            switch (level)
            {
                case MessageLevel.Debug:
                    {
                        infoBar.IsOpen = true;
                        infoBar.Message = msg;
                        infoBar.Severity = InfoBarSeverity.Informational;
                        break;
                    }
                case MessageLevel.Information:
                    {
                        infoBar.IsOpen = true;
                        infoBar.Message = msg;
                        infoBar.Severity = InfoBarSeverity.Informational;
                        break;
                    }
                case MessageLevel.Important:
                    {
                        infoBar.IsOpen = true;
                        infoBar.Message = msg;
                        infoBar.Severity = InfoBarSeverity.Success;
                        break;
                    }
                case MessageLevel.Warning:
                    {
                        infoBar.IsOpen = true;
                        infoBar.Message = msg;
                        infoBar.Severity = InfoBarSeverity.Warning;
                        break;
                    }
                case MessageLevel.Error:
                    {
                        infoBar.IsOpen = true;
                        infoBar.Message = msg;
                        infoBar.Severity = InfoBarSeverity.Error;
                        break;
                    }
            }
        });
    }
}
