using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

using Shared;

namespace IPC_Demo;

/// <summary>
/// This is the server project, the client project can be found at "..\Client\Client.csproj".
/// </summary>
public sealed partial class MainPage : Page, INotifyPropertyChanged
{
    #region [Properties]
    /// <summary>
    /// Server and client must share this secret.
    /// Don't hard-code this as I have, this is just an example demo.
    /// </summary>
    string _secret { get; set; } = "9hOfBy7beK0x3zX4";

    IpcHelper? ipcServer = null;
    int _total = 0;
    bool _loaded = false;
    bool _toggle = false;
    readonly int _maxMessages = 50;
    readonly ObservableCollection<ApplicationMessage>? _tab1Messages;
    readonly ObservableCollection<ApplicationMessage>? _tab2Messages;
    readonly ObservableCollection<ApplicationMessage>? _tab3Messages;
    public event PropertyChangedEventHandler? PropertyChanged;
    
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

    #region [Dragging Props]
    bool isMoving = false;
    int windowStartX = 0;
    int initialPointerX = 0;
    int windowStartY = 0;
    int initialPointerY = 0;
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
            ipcServer.MessageReceived += IpcServer_MessageReceived;
            ipcServer.JsonMessageReceived += IpcServer_JsonMessageReceived;
            ipcServer.ErrorOccurred += IpcServer_ErrorOccurred;
            ipcServer.Start();
            #endregion

        }
        _loaded = true;

        var workingCode = Shared.SecurityHelper.GenerateSecureCode6(_secret);
        UpdateInfoBar($"Secure code for the next {Extensions.MinutesRemainingInCurrentHour()} minutes will be {workingCode}", MessageLevel.Information);

        gridLED.Visibility = Visibility.Collapsed;
        // We're using the Compositor to swap the image, instead of the Image.Visibility trick.
        float width = 48; float height = 48;
        LoadVisualComposition(root, "ms-appx:///Assets/LED8_off.png", out _visualOff, Microsoft.UI.Colors.Transparent, width, height);
        LoadVisualComposition(root, "ms-appx:///Assets/LED8_on.png", out _visualOn, Windows.UI.Color.FromArgb(255, 0, 245, 0), width, height);
        LoadVisualComposition(root, "ms-appx:///Assets/LED8_err.png", out _visualErr, Windows.UI.Color.FromArgb(255, 245, 0, 0), width, height);
    }

    /// <summary>
    /// Server error event
    /// </summary>
    /// <param name="err"><see cref="Exception"/></param>
    void IpcServer_ErrorOccurred(Exception err)
    {
        if (_loaded)
        {
            UpdateInfoBar($"Server Error: {err.Message}", MessageLevel.Error);
            imgLED_err.DispatcherQueue.TryEnqueue(() =>
            {
                //comp_on.Visibility = Visibility.Collapsed;
                //comp_err.Visibility = Visibility.Visible;

                // We're using the Compositor to swap the image, instead of the Image.Visibility trick.
                SetVisualChild(root, _visualErr);
            });
        }
        else
            Debug.WriteLine($"⚠ Server Error: {err.Message}");
    }

    /// <summary>
    /// JSON message received event
    /// </summary>
    /// <param name="jmsg"><see cref="IpcMessage"/></param>
    void IpcServer_JsonMessageReceived(IpcMessage jmsg)
    {
        if (_loaded && jmsg != null)
        {
            // Check if it's a message from us by us.
            if (!string.IsNullOrEmpty(jmsg.Sender) && jmsg.Sender == Environment.MachineName)
            {
                #region [LED toggle]
                if (_toggle)
                {
                    _toggle = false;
                    imgLED_on.DispatcherQueue.TryEnqueue(() =>
                    {
                        /*
                         NOTE: Setting the source causes a flicker.
                         img.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/LED1_off.png"));
                        */

                        //comp_on.Visibility = Visibility.Collapsed;
                        // We're using the Compositor to swap the image, instead of the Image.Visibility trick.
                        SetVisualChild(root, _visualOff);
                    });
                }
                else
                {
                    _toggle = true;
                    imgLED_on.DispatcherQueue.TryEnqueue(() =>
                    {
                        /*
                         NOTE: Setting the source causes a flicker.
                         img.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/LED1_on.png"));
                        */

                        //comp_on.Visibility = Visibility.Visible;
                        // We're using the Compositor to swap the image, instead of the Image.Visibility trick.
                        SetVisualChild(root, _visualOn);
                    });
                }
                #endregion

                // Security check
                if (Shared.SecurityHelper.VerifySecureCode6(jmsg.Secret, _secret))
                {
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        /** Tab 1 (message details) **/

                        tsf.Text = $"Received msg #{++_total}";

                        if (tvi1.Header != null && tvi1.Header is string hdr && hdr.Equals("Available", StringComparison.OrdinalIgnoreCase))
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

                        /* 
                         NOTE: Setting the source causes a flicker.
                         img.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/LED1_err.png"));
                        */

                        //comp_on.Visibility = Visibility.Collapsed;
                        //comp_err.Visibility = Visibility.Visible;
                        SetVisualChild(root, _visualErr);

                        if (tvi2.Header != null && tvi2.Header is string hdr && hdr.Equals("Available", StringComparison.OrdinalIgnoreCase))
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
    }

    /// <summary>
    /// Message received event
    /// </summary>
    /// <param name="msg">raw message data</param>
    void IpcServer_MessageReceived(string msg)
    {
        Debug.WriteLine($"Received 📨 {msg}");
        this.DispatcherQueue.TryEnqueue(() =>
        {
            /** Tab 3 (connection history) **/

            if (tvi3.Header != null && tvi3.Header is string hdr && hdr.Equals("Available", StringComparison.OrdinalIgnoreCase))
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

    }
    #endregion

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

    #region [Image toggle using Compositor]

    // NOTE: This was an experiment to see if we could use the Compositor to swap-in/swap-out the LED image states.
    // It works, but the setup is more complicated than just setting the <CompShadow> control's visibility.

    Microsoft.UI.Composition.SpriteVisual? _visualOn = null;
    Microsoft.UI.Composition.SpriteVisual? _visualOff = null;
    Microsoft.UI.Composition.SpriteVisual? _visualErr = null;

    /// <summary>
    /// Can be used to toggle the <see cref="Microsoft.UI.Composition.SpriteVisual"/> on or off.
    /// </summary>
    /// <param name="state"></param>
    public void ToggleVisual(bool state, Microsoft.UI.Composition.SpriteVisual? visual)
    {
        if (visual == null)
            return;

        visual.IsVisible = state;
    }

    public void SetVisualChild(FrameworkElement fe, Microsoft.UI.Composition.SpriteVisual? visual)
    {
        if (fe == null)
            return;

        ElementCompositionPreview.SetElementChildVisual(fe, visual);
    }

    /// <summary>
    /// This will lay down the <paramref name="uriAsset"/> image on the <see cref="FrameworkElement"/> using the Compositor.
    /// </summary>
    public void LoadVisualComposition(FrameworkElement fe, string uriAsset, out Microsoft.UI.Composition.SpriteVisual visual, Windows.UI.Color shadowColor, float width = 0, float height = 0)
    {
        Microsoft.UI.Xaml.Media.LoadedImageSurface? surface;
        var targetVisual = ElementCompositionPreview.GetElementVisual(fe);
        Microsoft.UI.Composition.Compositor? compositor = targetVisual.Compositor;

        // Create LoadedImageSurface from appx URI
        if (!string.IsNullOrEmpty(uriAsset))
            surface = LoadedImageSurface.StartLoadFromUri(new Uri(uriAsset));
        else
            surface = LoadedImageSurface.StartLoadFromUri(new Uri($"ms-appx:///Assets/LED8_on.png"));

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

        // Top-right corner
        visual.RelativeOffsetAdjustment = new System.Numerics.Vector3(0.93f, 0.0f, 0f); // center the image

        visual.Brush = brush;

        if (shadowColor != Microsoft.UI.Colors.Transparent)
        {
            // Create drop shadow (this is noticeable on a CompositionSurfaceBrush, but not on a CompositionColorBrush).
            Microsoft.UI.Composition.DropShadow shadow = compositor.CreateDropShadow();
            shadow.Opacity = 0.8f;
            shadow.Color = shadowColor;
            shadow.BlurRadius = 20f;
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

        // Set the visual onto the Image control
        //ElementCompositionPreview.SetElementChildVisual(fe, visual);
    }

    public void ClearVisualComposition(FrameworkElement fe)
    {
        if (fe == null)
            return;

        ElementCompositionPreview.SetElementChildVisual(fe, null);
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
