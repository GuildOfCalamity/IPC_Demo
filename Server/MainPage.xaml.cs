using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace IPC_Demo;

/// <summary>
/// This is the server project, the client project can be found at "..\Client\Client.csproj".
/// </summary>
public sealed partial class MainPage : Page, INotifyPropertyChanged
{
    #region [Main Props]
    /// <summary>
    /// Server and client must share this secret.
    /// Don't hard-code this as I have, this is just an example demo.
    /// </summary>
    string _secret { get; set; } = "9hOfBy7beK0x3zX4";

    IpcHelper? ipcServer = null;
    bool _loaded = false;
    bool _toggle = false;
    bool _randomAsset = false;
    bool _realTimePlot = true;
    bool _localMachineOnly = false;
    bool _verboseRejection = true;
    bool _redrawNeeded = true;
    int _maxMessages = 50;
    readonly string _historyHeader = "Connection Activity";
    readonly ObservableCollection<ApplicationMessage>? _tab1Messages;
    public event PropertyChangedEventHandler? PropertyChanged;
    DispatcherQueueTimer? _updateGraphTimer = null;

    static Shared.ValueStopwatch _vsw = Shared.ValueStopwatch.StartNew();
    public ObservableCollection<TabItemViewModel> Connections { get; set; } = new();

    bool _isBusy = false;
    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; NotifyPropertyChanged(nameof(IsBusy)); }
    }

    string _footerText = string.Empty;
    public string FooterText
    {
        get => _footerText;
        set { _footerText = value; NotifyPropertyChanged(nameof(FooterText)); }
    }

    #region [Plot Control]
    List<GraphItem> _points = new();
    public List<GraphItem> Points
    {
        get => _points;
        set { _points = value; NotifyPropertyChanged(nameof(Points)); }
    }

    double _pointSize = 12;
    public double PointSize
    {
        get => _pointSize;
        set { _pointSize = value; NotifyPropertyChanged(nameof(PointSize)); }
    }
    #endregion

    public void NotifyPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
    {
        if (string.IsNullOrEmpty(propertyName)) { return; }
        // Confirm that we're on the UI thread in the event that DependencyProperty is changed under forked thread.
        this.DispatcherQueue.InvokeOnUI(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
    }
    #endregion

    #region [Dragging Props]
    bool isMoving = false;
    int windowStartX = 0;
    int initialPointerX = 0;
    int windowStartY = 0;
    int initialPointerY = 0;
    #endregion

    public MainPage()
    {
        _vsw = Shared.ValueStopwatch.StartNew();

        if (App.Profile != null && App.Profile.logging)
        {
            Shared.Logger.SetLoggerFileName(App.GetCurrentAssemblyName()!);
            Shared.Logger.SetLoggerFolderPath(AppDomain.CurrentDomain.BaseDirectory);
            _maxMessages = App.Profile.maxMessages > 0 ? App.Profile.maxMessages : 50;
        }

        this.InitializeComponent();
        this.Loaded += IpcPageOnLoaded;
        this.Unloaded += IpcPageOnUnloaded;
    }

    #region [Page Events]

    void IpcPageOnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_loaded && this.Content != null)
        {
            #region [Fetch previous messages]
            if (App.Profile != null && App.Profile.trackMessages)
            {
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
            }
            #endregion

            #region [Setup IPC and events]
            ipcServer = new IpcHelper(port: 32000);
            ipcServer.MessageReceived += IpcServer_MessageReceived;
            ipcServer.JsonMessageReceived += IpcServer_JsonMessageReceived;
            ipcServer.ErrorOccurred += IpcServer_ErrorOccurred;
            ipcServer.Start();
            #endregion

            if (_realTimePlot)
            {
                _updateGraphTimer = this.DispatcherQueue.CreateTimer();
                _updateGraphTimer.Interval = TimeSpan.FromSeconds(10);
                _updateGraphTimer.Tick += UpdateGraphTimerOnTick;
                _updateGraphTimer.Start();
            }
        }

        var workingCode = Shared.SecurityHelper.GenerateSecureCode6(_secret);

        #region [LED image toggle]
        string? asset = string.Empty;
        if (_randomAsset)
        {
            int attempts = 30;
            while (--attempts > 0 && string.IsNullOrEmpty(asset) && (!asset.Contains("LED_") && !asset.Contains("Bulb_")))
            {
                asset = Path.GetFileName(GetRandomAsset(Path.Combine(AppContext.BaseDirectory, "Assets")));
                Debug.WriteLine($"[INFO] Found asset '{asset}'");
            }
        }
        else
            asset = "Bulb54_off.png";
        
        if (_randomAsset)
            InitializeVisualCompositionLayers(asset: asset.Substring(0, asset.IndexOf("_")), width: 61, height: 61);
        else
            InitializeVisualCompositionLayers(asset: asset.Substring(0, asset.IndexOf("_")), width: 161, height: 161);

        UpdateInfoBar($"Secure code for the next {Extensions.MinutesRemainingInCurrentHour()} minutes will be {workingCode}    📷 {asset.Substring(0, asset.IndexOf("_"))}", MessageLevel.Information);
        #endregion

        #region [Pulsing Test]
        if (layer3.Visibility == Visibility.Visible)
        {
            float pulseWidth = 91;
            float pulseHeight = 91;
            PreloadVisualFrames(layer3, $"ms-appx:///Assets/LED65_alt.png", _visuals, Windows.UI.Color.FromArgb(255, 13, 210, 255), new System.Numerics.Vector3(0.5f, 0.5f, 0f), pulseWidth, pulseHeight, 0.9f);
            // Auto-adjust the grid layer margins, they must match for the effect to be seamless.
            layer3.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
            {
                layer3.HorizontalAlignment = HorizontalAlignment.Center;
                layer3.VerticalAlignment = VerticalAlignment.Center;
                layer3.Margin = new Thickness(-1 * pulseWidth, -1 * pulseHeight, 0, 0);
                layer3.Margin = new Thickness(-1 * pulseWidth, -1 * pulseHeight, 0, 0);
            });
            _ = Task.Run(async () =>
            {
                bool linear = false;
                await Task.Delay(500);

                // Animate the LED image using the Compositor
                while (!App.IsClosing)
                {
                    for (int i = 0; i < _visuals.Count; i++)
                    {
                        layer3.DispatcherQueue?.TryEnqueue(() => 
                        { 
                            if (i < _visuals.Count) // "i" is sometimes 10?
                                SetVisualChild(layer3, _visuals[i]); 
                        });

                        if (linear)
                            await Task.Delay(52);
                        else
                        {
                            int slope = (int)Extensions.EaseInQuadratic(i * 10);
                            var delay = (120 - Math.Min(119, slope)) + 18;
                            await Task.Delay(delay);
                        }
                    }
                    for (int i = _visuals.Count - 1; i > -1; i--)
                    {
                        layer3.DispatcherQueue?.TryEnqueue(() => 
                        {
                            if (i < _visuals.Count) // "i" is sometimes 10?
                                SetVisualChild(layer3, _visuals[i]); 
                        });

                        if (linear)
                            await Task.Delay(52);
                        else
                        {
                            int slope = (int)Extensions.EaseInQuadratic(i * 10);
                            var delay = (120 - Math.Min(119, slope)) + 18;
                            await Task.Delay(delay);
                        }
                    }
                }
            });
        }
        #endregion

        _loaded = true;

        if (App.Profile != null && App.Profile.logging)
            Shared.Logger.Log($"{nameof(MainPage)} load took {_vsw.GetElapsedFriendly()}", "Statistics");
        else
            Debug.WriteLine($"[DEBUG] {nameof(MainPage)} load took {_vsw.GetElapsedFriendly()}");
    }

    void IpcPageOnUnloaded(object sender, RoutedEventArgs e)
    {
        ipcServer?.Stop();
        _updateGraphTimer?.Stop();

        if (App.Profile != null)
        {
            if (App.Profile.trackMessages)
            {
                // TODO: rework saving of messages for all connections
                if (_tab1Messages != null && _tab1Messages.Count > 1)
                    App.MessageLog?.SaveData(_tab1Messages.ToList());
            }

            if (App.Profile.logging)
            {
                Shared.Logger.Log($"Application instance ran for {_vsw.GetElapsedFriendly()}", "Statistics");
                Shared.Logger.ConfirmLogIsFlushed(2000);
            }
        }
    }

    void TabViewItemOnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is TabViewItem tab && tab.DataContext is TabItemViewModel tvm)
        {
            Debug.WriteLine($"[OnLoaded Event] ViewModel header: {tvm.Header}");

            //PropertyChangedEventHandler? handler = (_, args) => 
            //{
            //    Debug.WriteLine($"📢 Examining property '{args.PropertyName}'");
            //    if (args.PropertyName == nameof(tvm.ActivityScore) && tvm.ActivityScore > 0)
            //    {
            //        _ = this.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
            //        {
            //            VisualStateManager.GoToState(tab, "Active", true);
            //        });
            //    }
            //};
            //tvm.PropertyChanged -= handler; // remove any existing subscriptions
            //tvm.PropertyChanged += handler;
        }
    }
    #endregion

    #region [IPC Events]
    /// <summary>
    /// Server error event
    /// </summary>
    /// <param name="err"><see cref="Exception"/></param>
    void IpcServer_ErrorOccurred(Exception err)
    {
        string msg = $"Server Error: {err.Message}";

        if (_loaded)
        {
            UpdateInfoBar(msg, MessageLevel.Error);
            SetVisualChild(layer2, _visualErr); // We're using the Compositor to swap the image, instead of the Image.Visibility trick.
        }
        else
        {
            Debug.WriteLine($"[ERROR] {msg}");
        }

        if (App.Profile != null && App.Profile.logging)
            Shared.Logger.Log(msg, "IpcServer");
    }

    /// <summary>
    /// Common message received event. Will be used to handle connection history and other tabs.
    /// </summary>
    /// <param name="msg">raw, unformatted message data</param>
    void IpcServer_MessageReceived(string msg)
    {
        // Deserialize the raw message
        var obj = JsonSerializer.Deserialize<Shared.IpcMessage>(msg);
        if (obj == null)
            return;

        if (!Shared.SecurityHelper.VerifySecureCode6(obj.Secret, _secret))
        {
            // Ignore messages that don't match our secret code.
            // Will get reported in our JsonMessageReceived event if verbose rejection flag is true.
            Debug.WriteLine($"[DEBUG] Ignoring bad PIN from '{obj.Sender}'");
            return;
        }

        _redrawNeeded = true;
        this.DispatcherQueue.TryEnqueue(() =>
        {
            #region [Connection History]
            if (!Connections.Any(Connections =>
                Connections.Header.Equals(_historyHeader, StringComparison.OrdinalIgnoreCase)))
            {
                Debug.WriteLine($"[DEBUG] Creating connection history tab");
                var histo = new TabItemViewModel
                {
                    Header = _historyHeader,
                    Sender = obj.Sender,
                    Icon = new SymbolIconSource { Symbol = Symbol.ZeroBars },
                };
                Connections.Add(histo);
            }

            var tvm = Connections.FirstOrDefault(Connections => Connections.Header.Equals(_historyHeader, StringComparison.OrdinalIgnoreCase));
            if (tvm != null)
            {
                var dict = ipcServer?.GetConnectionHistory();
                foreach (var item in dict)
                {
                    var conMsg = new ApplicationMessage
                    {
                        Module = ModuleId.IPC_Client,
                        MessagePayload = $"{item.Key}",
                        MessageText = $"💻 {Extensions.FormatEndPoint(item.Key)}    ⌚ {item.Value.ToJsonFriendlyFormat()}    ⏱️ {obj.Time}",
                        MessageType = typeof(Shared.IpcMessage),
                        MessageTime = DateTime.Now /* item.Value */,
                    };
                    tvm?.Messages?.Insert(0, conMsg);
                    if (tvm?.Messages?.Count > _maxMessages)
                        tvm?.Messages?.RemoveAt(_maxMessages);
                }

                if (Connections.Count == 1) // only do this on the first tab creation event
                    tvConnections.SelectedItem = tvm; // force focus to new tab
            }
            #endregion
        });
    }

    /// <summary>
    /// JSON message received event (dynamic tab creation)
    /// </summary>
    /// <param name="jmsg"><see cref="IpcMessage"/></param>
    void IpcServer_JsonMessageReceived(Shared.IpcMessage jmsg)
    {
        if (_loaded && jmsg != null)
        {
            string senderCompare = string.Empty;
            if (_localMachineOnly)
                senderCompare = Environment.MachineName;
            else
                senderCompare = jmsg.Sender;

            // Check if it's a message from us by us (uncomment the MachineName check to enable two-factor)
            if (!string.IsNullOrEmpty(jmsg.Sender) /* && jmsg.Sender == Environment.MachineName */)
            {
                #region [LED toggle]
                if (_toggle)
                {
                    _toggle = false;
                    SetVisualChild(layer2, _visualOff); // We're using the Compositor to swap the image, instead of the Image.Visibility trick.
                }
                else
                {
                    _toggle = true;
                    var test = Random.Shared.NextDouble();
                    if (test >= 0.85)
                        SetVisualChild(layer2, _visualErr);
                    else if (test >= 0.65)
                        SetVisualChild(layer2, _visualWrn);
                    else if (test >= 0.45)
                        SetVisualChild(layer2, _visualAlt);
                    else
                        SetVisualChild(layer2, _visualOn);
                }
                #endregion

                // Security check
                if (Shared.SecurityHelper.VerifySecureCode6(jmsg.Secret, _secret))
                {
                    var appMsg = new ApplicationMessage
                    {
                        Module = ModuleId.IPC_Passed,
                        MessagePayload = jmsg,
                        MessageText = $"{jmsg}",
                        MessageType = typeof(Shared.IpcMessage),
                        MessageTime = jmsg.Time.ParseJsonDateTime(),
                    };

                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        // Does the connection already exist?
                        if (!Connections.Any(Connections => Connections.Header.Equals(senderCompare, StringComparison.OrdinalIgnoreCase)))
                        {
                            UpdateInfoBar($"Creating new tab for '{senderCompare}'", MessageLevel.Important);
                            var tvm = new TabItemViewModel
                            {
                                Header = $"{senderCompare}",
                                Sender = jmsg.Sender,
                                DecaySeconds = 15, // you'll want to adjust this for stress test scenarios
                                Icon = SymbolIconHelper.GetRandomIcon(),
                            };
                            Connections.Add(tvm);

                            if (Connections.Count == 1) // only do this on the first tab creation event
                                tvConnections.SelectedItem = tvm; // force focus to new tab

                            tvm.Messages.Insert(0, appMsg);

                            // Update the footer (subtract one for history tab)
                            FooterText = $"Connections: {Connections.Count - 1}";

                            /**
                             **  TODO: Check for old connections and remove them, or too many tabs being created.
                             **/
                        }
                        else
                        {
                            var existing = Connections.FirstOrDefault(Connections => Connections.Header.Equals(senderCompare, StringComparison.OrdinalIgnoreCase));
                            if (existing != null)
                            {
                                existing?.Messages?.Insert(0, appMsg);
                                if (existing?.Messages?.Count > _maxMessages)
                                    existing?.Messages?.RemoveAt(_maxMessages);

                                // Modify the color on the tab who's most active
                                if (App.Profile != null && App.Profile.highlightMostActive)
                                {
                                    existing?.RegisterActivity();
                                    CheckForMoreActiveClientWide(existing, App.Profile.activityThreshold);
                                }
                            }
                            else
                                UpdateInfoBar($"⚠️ Failed to locate client tab for sender '{jmsg.Sender}'", MessageLevel.Warning);
                        }
                    });
                }
                else if (_verboseRejection) // failed security check
                {
                    /** 
                     **  You'll want to add some form of protection to prevent intentional/malicious hammering.
                     **/
                    if (App.Profile != null && App.Profile.logging)
                        Shared.Logger.Log($"Failed security check for sender '{jmsg.Sender}'", "Security");

                    // Add to our connection history tab (we may want to create a rejects tab in the future)
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        UpdateInfoBar($"⚠️ Failed security check for sender '{jmsg.Sender}'", MessageLevel.Error);

                        var tvm = Connections.FirstOrDefault(Connections => Connections.Header.Equals(_historyHeader, StringComparison.OrdinalIgnoreCase));
                        if (tvm != null)
                        {
                            var conMsg = new ApplicationMessage
                            {
                                Module = ModuleId.IPC_Failed,
                                MessagePayload = $"{jmsg}",
                                MessageText = $"💻 {jmsg.Sender}    🔒 {jmsg.Secret}    🚨 BAD PIN!",
                                MessageType = typeof(Shared.IpcMessage),
                                MessageTime = DateTime.Now,
                            };
                            tvm?.Messages?.Insert(0, conMsg);
                            if (tvm?.Messages?.Count > _maxMessages)
                                tvm?.Messages?.RemoveAt(_maxMessages);
                        }
                    });

                    SetVisualChild(layer2, _visualErr); // We're using the Compositor to swap the image, instead of the Image.Visibility trick.
                }
            }
            else
            {
                if (App.Profile != null && App.Profile.logging)
                    Shared.Logger.Log($"Disallowed/Unknown sender '{jmsg.Sender}'", "Security");
                else
                    Debug.WriteLine($"⚠ Disallowed/Unknown sender '{jmsg.Sender}'");
            }
        }
        else
            Debug.WriteLine($"JSON 📨 {jmsg}");
    }
    #endregion

    #region [MenuFlyout Events]
    void OnDisconnectClick(object sender, RoutedEventArgs e)
    {
        if (GetSelectedTabContext(sender) is TabItemViewModel vm)
            Debug.WriteLine($"[EVENT] Disconnect {vm.Header}");
    }

    void OnPingClick(object sender, RoutedEventArgs e)
    {
        if (GetSelectedTabContext(sender) is TabItemViewModel vm)
            Debug.WriteLine($"[EVENT] Ping {vm.Header}");
    }

    void OnCloseTabClick(object sender, RoutedEventArgs e)
    {
        if (GetSelectedTabContext(sender) is TabItemViewModel vm && !vm.Header.Contains(_historyHeader))
            Connections.Remove(vm);
    }

    /// <summary>
    /// Not needed, but this is where we could add a custom drag-n-drop handler.
    /// </summary>
    void MyTabView_TabDragCompleted(TabView sender, TabViewTabDragCompletedEventArgs args)
    {
        // Force UI collection to sync after drag
        if (sender.TabItemsSource is ObservableCollection<TabItemViewModel> collection)
        {
            var reordered = sender.TabItems
                .Cast<TabViewItem>()
                .Select(tab => tab.DataContext as TabItemViewModel)
                .Where(vm => vm != null)
                .ToList();

            collection.Clear();

            foreach (var item in reordered!)
            {
                if (item != null)
                    collection.Add(item);
            }
        }
    }

    /// <summary>
    /// <see cref="Flyout"/> opened event for graphing.
    /// Data will be populated when the flyout is opened.
    /// </summary>
    /// <remarks>
    /// For a typical random distribution, the connections on the graph should all eventually 
    /// equal <see cref="MainPage._maxMessages"/>, if the simulation is allowed to run long enough.
    /// </remarks>
    public void GraphFlyoutOpened(object sender, object e)
    {
        if (!_redrawNeeded || App.IsClosing)
            return;

        UpdatePlotPoints();
    }

    void UpdateGraphTimerOnTick(DispatcherQueueTimer sender, object args)
    {
        if (!_redrawNeeded || App.IsClosing)
            return;

        UpdatePlotPoints();
    }

    void UpdatePlotPoints()
    {
        try
        {
            _redrawNeeded = false;

            if (Connections.Count == 0)
                return;

            Points.Clear();

            int groupCount = 0;

            // Select connections with at least one message and then order by header.
            var orderedList = Connections
                .Where(e => e.Messages.Count > 0)
                .OrderBy(e => e.Header)
                .ToList();

            foreach (var item in orderedList)
            {
                if (string.IsNullOrEmpty(item.Header) || item.Header.Contains(_historyHeader))
                    continue;

                groupCount++;

                //foreach (var msg in item.Messages)
                //{
                //    if (msg.MessageTime.IsOlderThanDays(2d))
                //        continue;
                //}

                //Points.Add(new GraphItem { Title = item.Header, Amount = item.ActivityScore });
                Points.Add(new GraphItem { Title = item.Header, Score = item.ActivityScore, Amount = item.Messages.Count });
            }

            #region [Point size auto-adjust]
            switch (Points.Count)
            {
                case int count when count > 100: PointSize = 3; break;
                case int count when count > 75: PointSize = 4; break;
                case int count when count > 50: PointSize = 6; break;
                case int count when count > 25: PointSize = 8; break;
                case int count when count > 15: PointSize = 10; break;
                case int count when count > 10: PointSize = 15; break;
                case int count when count > 5: PointSize = 18; break;
                default: PointSize = 22; break;
            }
            #endregion

            // Trigger redraw
            pcConnections.PointSource = null;
            pcConnections.PointSource = Points;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] Failed to update graph points: {ex.Message}");
        }
    }

    #endregion

    #region [Image toggle using Compositor]
    Microsoft.UI.Composition.SpriteVisual? _visualOn = null;  // green lighting
    Microsoft.UI.Composition.SpriteVisual? _visualAlt = null; // blue lighting
    Microsoft.UI.Composition.SpriteVisual? _visualWrn = null; // yellow lighting
    Microsoft.UI.Composition.SpriteVisual? _visualErr = null; // red lighting
    Microsoft.UI.Composition.SpriteVisual? _visualOff = null; // no color
    List<Microsoft.UI.Composition.SpriteVisual> _visuals = new(); // testing varying glow strengths

    /// <summary>
    /// We're using the Compositor to swap the image, instead of the Image.Visibility trick.
    /// You could just employ one grid control, but then you'd have to layer visuals with 
    /// multiple calls. This will accomplish the same effect, but with less code-behind.
    /// </summary>
    void InitializeVisualCompositionLayers(string asset = "LED18", float width = 60, float height = 60, float opacity = 0.9f)
    {
        LoadVisualComposition(layer1, $"ms-appx:///Assets/{asset}_off.png", out _visualOff, Microsoft.UI.Colors.Transparent, new System.Numerics.Vector3(0.5f, 0.5f, 0f), width, height, opacity);
        LoadVisualComposition(layer2, $"ms-appx:///Assets/{asset}_on.png", out _visualOn, Windows.UI.Color.FromArgb(255, 0, 245, 0), new System.Numerics.Vector3(0.5f, 0.5f, 0f), width, height, opacity);
        LoadVisualComposition(layer2, $"ms-appx:///Assets/{asset}_wrn.png", out _visualWrn, Windows.UI.Color.FromArgb(255, 255, 223, 14), new System.Numerics.Vector3(0.5f, 0.5f, 0f), width, height, opacity);
        LoadVisualComposition(layer2, $"ms-appx:///Assets/{asset}_err.png", out _visualErr, Windows.UI.Color.FromArgb(255, 245, 0, 0), new System.Numerics.Vector3(0.5f, 0.5f, 0f), width, height, opacity);
        LoadVisualComposition(layer2, $"ms-appx:///Assets/{asset}_alt.png", out _visualAlt, Windows.UI.Color.FromArgb(255, 13, 210, 255), new System.Numerics.Vector3(0.5f, 0.5f, 0f), width, height, opacity);
        SetVisualChild(layer1, _visualOff); // off image always visible

        // Auto-adjust the grid layer margins, they must match for the effect to be seamless.
        layer1.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
        {
            layer1.HorizontalAlignment = layer2.HorizontalAlignment = HorizontalAlignment.Right;
            layer1.VerticalAlignment = layer2.VerticalAlignment = VerticalAlignment.Top;
            if (_randomAsset)
            {
                layer1.Margin = new Thickness(0, -1 * (width * 0.1), width + (width * 0.2), 0);
                layer2.Margin = new Thickness(0, -1 * (width * 0.1), width + (width * 0.2), 0);
            }
            else
            {
                layer1.Margin = new Thickness(0, -1 * (width * 0.3), width + (width * 1.2), 0);
                layer2.Margin = new Thickness(0, -1 * (width * 0.3), width + (width * 1.2), 0);
            }
        });
    }

    /// <summary>
    /// Can be used to toggle the <see cref="Microsoft.UI.Composition.SpriteVisual"/> on or off.
    /// </summary>
    /// <param name="state"><c>true</c> for visible, <c>false</c> for invisible</param>
    public void ToggleVisual(bool state, Microsoft.UI.Composition.SpriteVisual? visual)
    {
        if (visual == null || App.IsClosing)
            return;

        visual.IsVisible = state;
    }

    /// <summary>
    /// Applies the given <paramref name="visual"/> to the <see cref="FrameworkElement"/> <paramref name="fe"/>.
    /// </summary>
    /// <remarks>The <paramref name="visual"/> can be any <see cref="Microsoft.UI.Composition.ContainerVisual"/></remarks>
    public void SetVisualChild(FrameworkElement fe, Microsoft.UI.Composition.SpriteVisual? visual)
    {
        if (fe == null || App.IsClosing)
            return;

        // We may be under a forked thread when called, so just to be safe we'll enqueue on FrameworkElement's dispatcher.
        fe.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
        {
            // Remove any existing reference
            ElementCompositionPreview.SetElementChildVisual(fe, null);

            // Apply new visual
            ElementCompositionPreview.SetElementChildVisual(fe, visual);
        });
    }

    /// <summary>
    /// Applies the given <paramref name="layer1"/>, then <paramref name="layer2"/> to the <see cref="FrameworkElement"/> <paramref name="fe"/>.
    /// <code>
    ///    SetLayeredVisuals(rootGrid, _visualOff, _visualOn);
    /// </code>
    /// </summary>
    /// <remarks>The layers will be applied in the order <b>layer1</b> then <b>layer2</b>.</remarks>
    public void SetLayeredVisuals(FrameworkElement fe, Microsoft.UI.Composition.SpriteVisual? layer1, Microsoft.UI.Composition.SpriteVisual? layer2)
    {
        if (fe == null || App.IsClosing)
            return;

        var compositor = ElementCompositionPreview.GetElementVisual(fe).Compositor;
        if (compositor == null)
            return;

        Microsoft.UI.Composition.ContainerVisual? containerVisual = compositor.CreateContainerVisual();

        // Add visual layers to the container's visual collection
        containerVisual.Children.InsertAtTop(layer1);
        containerVisual.Children.InsertAtTop(layer2);

        // Remove any existing reference
        ElementCompositionPreview.SetElementChildVisual(fe, null);

        // Apply new layered visual
        ElementCompositionPreview.SetElementChildVisual(fe, containerVisual);
    }

    /// <summary>
    /// Applies the given <paramref name="layers"/> to the <see cref="FrameworkElement"/> <paramref name="fe"/>.
    /// <code>
    ///    SetLayeredVisuals(rootGrid, new SpriteVisual[] { _visualOff, _visualErr, _visualWrn, _visualAlt, _visualOn });
    /// </code>
    /// </summary>
    /// <param name="layers">array of <see cref="Microsoft.UI.Composition.SpriteVisual"/>s</param>
    public void SetLayeredVisuals(FrameworkElement fe, params Microsoft.UI.Composition.SpriteVisual[] layers)
    {
        if (fe == null || App.IsClosing)
            return;

        var compositor = ElementCompositionPreview.GetElementVisual(fe).Compositor;
        if (compositor == null)
            return;

        Microsoft.UI.Composition.ContainerVisual? containerVisual = compositor.CreateContainerVisual();

        // Add each visual layer to the container's visual collection
        layers.ForEach(sv => { containerVisual.Children.InsertAtTop(sv); });

        // Remove any existing reference
        ElementCompositionPreview.SetElementChildVisual(fe, null);

        // Apply new layered visual
        ElementCompositionPreview.SetElementChildVisual(fe, containerVisual);
    }

    /// <summary>
    /// Applies the given <paramref name="layer1"/>, then <paramref name="layer2"/> to the <see cref="FrameworkElement"/> <paramref name="fe"/>.
    /// <code>
    ///    SetLayeredAbove(rootGrid, _visualOff, _visualOn);
    /// </code>
    /// </summary>
    /// <remarks>The layers will be applied in the order <b>layer1</b> then <b>layer2</b>.</remarks>
    public void SetLayeredAbove(FrameworkElement fe, Microsoft.UI.Composition.SpriteVisual? layer1, Microsoft.UI.Composition.SpriteVisual? layer2)
    {
        if (fe == null || App.IsClosing)
            return;

        var compositor = ElementCompositionPreview.GetElementVisual(fe).Compositor;
        if (compositor == null)
            return;

        Microsoft.UI.Composition.ContainerVisual? containerVisual = compositor.CreateContainerVisual();

        // Add visual layers to the container's visual collection
        containerVisual.Children.InsertAbove(layer2, layer1);

        // Remove any existing reference
        ElementCompositionPreview.SetElementChildVisual(fe, null);

        // Apply new layered visual
        ElementCompositionPreview.SetElementChildVisual(fe, containerVisual);
    }

    /// <summary>
    /// Applies the given <paramref name="layer2"/>, then <paramref name="layer1"/> to the <see cref="FrameworkElement"/> <paramref name="fe"/>.
    /// <code>
    ///    SetLayeredBelow(rootGrid, _visualOff, _visualOn);
    /// </code>
    /// </summary>
    /// <remarks>The layers will be applied in the order <b>layer1</b> then <b>layer2</b>.</remarks>
    public void SetLayeredBelow(FrameworkElement fe, Microsoft.UI.Composition.SpriteVisual? layer1, Microsoft.UI.Composition.SpriteVisual? layer2)
    {
        if (fe == null || App.IsClosing)
            return;

        var compositor = ElementCompositionPreview.GetElementVisual(fe).Compositor;
        if (compositor == null)
            return;

        Microsoft.UI.Composition.ContainerVisual? containerVisual = compositor.CreateContainerVisual();

        // Add visual layers to the container's visual collection
        containerVisual.Children.InsertBelow(layer2, layer1);

        // Remove any existing reference
        ElementCompositionPreview.SetElementChildVisual(fe, null);

        // Apply new layered visual
        ElementCompositionPreview.SetElementChildVisual(fe, containerVisual);
    }

    /// <summary>
    /// Removes the <see cref="Microsoft.UI.Composition.SpriteVisual"/> from the <see cref="FrameworkElement"/> <paramref name="fe"/>.
    /// </summary>
    /// <param name="fe"></param>
    public void ClearVisualComposition(FrameworkElement fe)
    {
        if (fe == null || App.IsClosing)
            return;

        ElementCompositionPreview.SetElementChildVisual(fe, null);
    }

    /// <summary>
    /// This will lay down the <paramref name="uriAsset"/> image on the <see cref="FrameworkElement"/> using the <see cref="Microsoft.UI.Composition.Compositor"/>.
    /// If <paramref name="glowColor"/> is not <see cref="Microsoft.UI.Colors.Transparent"/>, a drop shadow will be applied.
    /// </summary>
    public void LoadVisualComposition(FrameworkElement fe, string uriAsset, out Microsoft.UI.Composition.SpriteVisual visual, Windows.UI.Color glowColor, System.Numerics.Vector3 offsetAdjustment, float width = 0, float height = 0, float opacity = 1)
    {
        Microsoft.UI.Xaml.Media.LoadedImageSurface? surface;
        var targetVisual = ElementCompositionPreview.GetElementVisual(fe);
        Microsoft.UI.Composition.Compositor? compositor = targetVisual.Compositor;

        try
        {
            // Create LoadedImageSurface from appx URI
            if (!string.IsNullOrEmpty(uriAsset))
                surface = LoadedImageSurface.StartLoadFromUri(new Uri(uriAsset));
            else
                surface = LoadedImageSurface.StartLoadFromUri(new Uri($"ms-appx:///Assets/LED8_on.png"));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠ {MethodBase.GetCurrentMethod()?.Name}: {ex.Message}");
            surface = null;
        }

        // Create a CompositionBrush and set the surface
        var brush = compositor.CreateSurfaceBrush();
        brush.Surface = surface;

        // Create a visual sized to match the Image control
        visual = compositor.CreateSpriteVisual();
        if (visual == null) { return; }

        if ((width == 0 || height == 0) && fe.ActualSize != System.Numerics.Vector2.Zero)
        {
            visual.Size = new System.Numerics.Vector2((float)fe.ActualWidth, (float)fe.ActualHeight);
            //targetVisual.CenterPoint = new System.Numerics.Vector3(fe.ActualSize.X / 2f, fe.ActualSize.Y / 2f, 0f);
        }
        else
        {
            visual.Size = new System.Numerics.Vector2(width, height);
        }

        visual.RelativeOffsetAdjustment = offsetAdjustment; // = new System.Numerics.Vector3(0.93f, 0.001f, 0f); // ⇦ Top-right corner
        visual.Brush = brush;
        visual.Opacity = opacity;

        if (glowColor.A > 0x00 && glowColor != Microsoft.UI.Colors.Transparent)
        {
            // Create drop shadow (this is noticeable on a CompositionSurfaceBrush, but not on a CompositionColorBrush).
            Microsoft.UI.Composition.DropShadow shadow = compositor.CreateDropShadow();
            shadow.Opacity = 0.85f;
            shadow.Color = glowColor;
            shadow.BlurRadius = 30f;
            shadow.Offset = new System.Numerics.Vector3(0, 1, -1);
            // Specify mask policy for shadow.
            shadow.SourcePolicy = Microsoft.UI.Composition.CompositionDropShadowSourcePolicy.InheritFromVisualContent;

            // Associate shadow with visual.
            visual.Shadow = shadow;
        }

        //brush.Scale = new System.Numerics.Vector2 { X = 0.75f, Y = 0.75f };
        brush.Stretch = Microsoft.UI.Composition.CompositionStretch.Uniform;
        brush.BitmapInterpolationMode = Microsoft.UI.Composition.CompositionBitmapInterpolationMode.Linear;
        
        visual.IsVisible = true; // can be used to toggle the image on/off

        // Set the visual onto the Image control (we'll do this later during state change)
        //ElementCompositionPreview.SetElementChildVisual(fe, visual);
    }

    /// <summary>
    /// This will populate the <paramref name="visuals"/> with drop shadows using the <see cref="Microsoft.UI.Composition.Compositor"/>.
    /// If <paramref name="glowColor"/> is not <see cref="Microsoft.UI.Colors.Transparent"/>, a drop shadow will be applied.
    /// </summary>
    public void PreloadVisualFrames(FrameworkElement fe, string uriAsset, List<Microsoft.UI.Composition.SpriteVisual> visuals, Windows.UI.Color glowColor, System.Numerics.Vector3 offsetAdjustment, float width = 0, float height = 0, float opacity = 1f)
    {
        if (visuals.Count > 0)
            visuals.Clear();

        Microsoft.UI.Xaml.Media.LoadedImageSurface? surface;
        var targetVisual = ElementCompositionPreview.GetElementVisual(fe);
        Microsoft.UI.Composition.Compositor? compositor = targetVisual.Compositor;

        try
        {
            if (!string.IsNullOrEmpty(uriAsset))
                surface = LoadedImageSurface.StartLoadFromUri(new Uri(uriAsset));
            else
                surface = LoadedImageSurface.StartLoadFromUri(new Uri($"ms-appx:///Assets/LED8_on.png"));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠ {MethodBase.GetCurrentMethod()?.Name}: {ex.Message}");
            surface = null;
        }

        // Create a CompositionBrush and set the surface
        var brush = compositor.CreateSurfaceBrush();
        brush.Surface = surface;

        // Create visuals with ascending amounts of glow
        for (int i = 1; i < 11; i++)
        {
            // Create a visual sized to match the Image control
            var visual = compositor.CreateSpriteVisual();
            if (visual == null) { return; }
            if ((width == 0 || height == 0) && fe.ActualSize != System.Numerics.Vector2.Zero)
            {
                visual.Size = new System.Numerics.Vector2((float)fe.ActualWidth, (float)fe.ActualHeight);
                //targetVisual.CenterPoint = new System.Numerics.Vector3(fe.ActualSize.X / 2f, fe.ActualSize.Y / 2f, 0f);
            }
            else
            {
                visual.Size = new System.Numerics.Vector2(width, height);
            }
            visual.RelativeOffsetAdjustment = offsetAdjustment; // = new System.Numerics.Vector3(0.93f, 0.001f, 0f); // ⇦ Top-right corner
            visual.Brush = brush;
            visual.Opacity = opacity;

            if (glowColor.A > 0x00 && glowColor != Microsoft.UI.Colors.Transparent)
            {
                // Create drop shadow (this is noticeable on a CompositionSurfaceBrush, but not on a CompositionColorBrush).
                Microsoft.UI.Composition.DropShadow shadow = compositor.CreateDropShadow();
                shadow.Opacity = 0.9f;
                shadow.Color = glowColor;
                shadow.BlurRadius = (float)i * 3.1f;
                shadow.Offset = new System.Numerics.Vector3(0, 0, -1);
                // Specify mask policy for shadow.
                shadow.SourcePolicy = Microsoft.UI.Composition.CompositionDropShadowSourcePolicy.InheritFromVisualContent;
                // Associate shadow with visual.
                visual.Shadow = shadow;
            }

            //brush.Scale = new System.Numerics.Vector2 { X = 0.75f, Y = 0.75f };
            brush.Stretch = Microsoft.UI.Composition.CompositionStretch.Uniform;
            brush.BitmapInterpolationMode = Microsoft.UI.Composition.CompositionBitmapInterpolationMode.Linear;
            visual.IsVisible = true; // can be used to toggle the image on/off

            // Add the visual to our list
            visuals.Add(visual);
        }
    }

    public static void StackVisualsTest(UIElement targetElement)
    {
        Microsoft.UI.Composition.Compositor? compositor = ElementCompositionPreview.GetElementVisual(targetElement).Compositor;
        if (compositor is null)
            return;

        Microsoft.UI.Composition.ContainerVisual? containerVisual = compositor.CreateContainerVisual();

        // Create 1st layer child visual
        Microsoft.UI.Composition.SpriteVisual layer1 = compositor.CreateSpriteVisual();
        layer1.Size = new System.Numerics.Vector2(100, 100);
        layer1.Brush = compositor.CreateColorBrush(Microsoft.UI.Colors.Red);
        layer1.Opacity = 0.8f;

        // Create 2nd layer child visual
        Microsoft.UI.Composition.SpriteVisual layer2 = compositor.CreateSpriteVisual();
        layer2.Size = new System.Numerics.Vector2(100, 100);
        layer2.Brush = compositor.CreateColorBrush(Microsoft.UI.Colors.Blue);
        layer2.Opacity = 0.8f;
        layer2.Offset = new System.Numerics.Vector3(50, 50, 0); // Offset to stack

        // Add visual layers to the container
        containerVisual.Children.InsertAtTop(layer1);
        containerVisual.Children.InsertAtTop(layer2);

        // Set the container visual as the child visual of the target element
        ElementCompositionPreview.SetElementChildVisual(targetElement, containerVisual);
    }
    #endregion

    #region [Miscellaneous]
    /// <summary>
    /// Every time a message is received, we check all tabs to see which is most active.
    /// </summary>
    /// <param name="triggeringVM">the view model that triggered this check</param>
    /// <param name="threshold">the activity score to compare against</param>
    void CheckForMoreActiveClientWide(TabItemViewModel? triggeringVM, int threshold = 3)
    {
        if (triggeringVM == null || App.IsClosing)
            return;

        // Get the current most active connection
        var list = Connections.OrderByDescending(vm => vm.ActivityScore);
        foreach (var mostActive in list)
        {
            if (mostActive.Header.Contains(_historyHeader))
                continue; // skip the history tab, it won't be relevant here since we don't call RegisterActivity()

            // Heat-map the most active connection
            if (mostActive.ActivityScore >= threshold)
            {
                if (mostActive.ActivityScore < threshold * 4)
                {
                    Debug.WriteLine($"📢 Modifying {mostActive.Header}'s brush. Score={mostActive.ActivityScore}");
                    #region [Adjusting colors for most active]
                    mostActive.ToggleColor = mostActive.ToggleColor.CreateLighterRed(0.1f);
                    //mostActive.FontColor = mostActive.FontColor.CreateLighterBlue(0.1f);
                    //mostActive.FontColor = mostActive.ToggleColor.CreateContrastingBrush(0.3f);
                    #endregion
                }
                else
                {
                    Debug.WriteLine($"📢 Skipping {mostActive.Header}'s brush change. Score={mostActive.ActivityScore}");
                }
            }

            // Run a decay cycle on each tab model
            mostActive.DecayActivity();
        }
    }

    /// <summary>
    /// Every time a message is received, we check only the triggering tabs to see if it's the most active client.
    /// </summary>
    /// <param name="triggeringVM">the view model that triggered this check</param>
    /// <param name="threshold">the activity score to compare against</param>
    void CheckForMoreActiveClientSlim(TabItemViewModel? triggeringVM, int threshold = 3)
    {
        if (triggeringVM == null || App.IsClosing)
            return;

        // Get the current most active connection
        var mostActive = Connections.OrderByDescending(vm => vm.ActivityScore).FirstOrDefault();

        //if (mostActive == triggeringVM && mostActive != tvConnections.SelectedItem && mostActive.ActivityScore >= threshold)
        //{
        //    // Set focus to the most active connection
        //    tvConnections.SelectedItem = mostActive;
        //}

        // Heat-map the most active connection
        if (mostActive == triggeringVM && mostActive.ActivityScore >= threshold)
        {
            if (triggeringVM.ActivityScore < threshold * 4)
            {
                Debug.WriteLine($"📢 Modifying {triggeringVM.Header}'s brush. Score={triggeringVM.ActivityScore}");
                // Just toggle color for the most active connection
                triggeringVM.ToggleColor = triggeringVM.ToggleColor.CreateLighterBlue(); //new SolidColorBrush(Microsoft.UI.Colors.Orchid);
            }
            else
            {
                Debug.WriteLine($"📢 Skipping {triggeringVM.Header}'s brush change. Score={triggeringVM.ActivityScore}");
            }
        }

        // Run a decay cycle on all tab models
        var list = Connections.OrderByDescending(vm => vm.ActivityScore);
        foreach (var vm in list)
        {
            vm.DecayActivity();
        }
    }

    TabItemViewModel? GetSelectedTabContext(object sender)
    {
        if (sender is FrameworkElement fe && fe.DataContext is TabItemViewModel tvm1)
            return tvm1;
        else if (sender is TabViewItem tvi && tvi.DataContext is TabItemViewModel tvm2)
            return tvm2;

        return null;
    }

    void UpdateInfoBar(string msg, MessageLevel level = MessageLevel.Information)
    {
        if (App.IsClosing || this.Content == null)
            return;

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

    string GetRandomAsset(string assetsFolderPath)
    {
        if (!Directory.Exists(assetsFolderPath))
            return string.Empty;

        var pngFiles = Directory.EnumerateFiles(assetsFolderPath, "*.png", SearchOption.TopDirectoryOnly).ToList();

        if (pngFiles.Count == 0)
            return string.Empty;

        return pngFiles[Random.Shared.Next(pngFiles.Count)];
    }
    #endregion

    #region [Dragging]
    void UIElement_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        ((UIElement)sender).CapturePointer(e.Pointer);
        var currentPoint = e.GetCurrentPoint((UIElement)sender);
        if (currentPoint.Properties.IsLeftButtonPressed && MainWindow.appW != null)
        {
            ((UIElement)sender).CapturePointer(e.Pointer);
            windowStartX = MainWindow.appW.Position.X;
            windowStartY = MainWindow.appW.Position.Y;
            Windows.Graphics.PointInt32 pt;
            NativeMethods.GetCursorPos(out pt); // user32.dll
            initialPointerX = pt.X;
            initialPointerY = pt.Y;
            isMoving = true;
        }
        //else if (currentPoint.Properties.IsRightButtonPressed)
        //{
        //    if (Content is not null && Content.XamlRoot is not null)
        //    {
        //        FlyoutShowOptions options = new FlyoutShowOptions();
        //        options.ShowMode = FlyoutShowMode.Standard;
        //        options.Position = new Windows.Foundation.Point((int)currentPoint.Position.X, (int)currentPoint.Position.Y);
        //        if (!TitlebarMenuFlyout.IsOpen && !App.IsClosing)
        //            TitlebarMenuFlyout.ShowAt(Content, options);
        //    }
        //}
        else if (currentPoint.Properties.IsMiddleButtonPressed)
        {
            e.Handled = true;
            Application.Current.Exit();
        }
    }

    void UIElement_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        (sender as UIElement)?.ReleasePointerCapture(e.Pointer);
        isMoving = false;
    }

    void UIElement_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var currentPoint = e.GetCurrentPoint((UIElement)sender);
        if (currentPoint.Properties.IsLeftButtonPressed)
        {
            Windows.Graphics.PointInt32 pt;
            NativeMethods.GetCursorPos(out pt);
            if (isMoving && MainWindow.appW != null)
                MainWindow.appW.Move(new Windows.Graphics.PointInt32(windowStartX + (pt.X - initialPointerX), windowStartY + (pt.Y - initialPointerY)));
        }
    }
    #endregion
}
