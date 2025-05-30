using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.Foundation.Collections;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace IPC_Demo.Controls;

/// <summary>
/// <para>
///   The goal of this graphing control was to keep it as simple as possible 
///   for the user, the only requirement is a list of <see cref="double"/>s.
/// </para>
/// <para>
///   This originally started off using an <see cref="Ellipse"/> shape, but
///   I've changed it to a <see cref="Rectangle"/> shape to allow for full 
///   vertical length rendering of the data points.
/// </para>
/// </summary>
public sealed partial class PlotControl : UserControl
{
    #region [Backing Members]
    //ToolTip? _tooltip;
    int _msDelay = 0;
    bool _loaded = false;
    bool _isDrawing = false;
    bool _sizeSet = false;
    bool _measureOccurred = false;
    bool _warnIfNoElements = false;
    double _restingOpacity = 0.65;
    double _canvasMargin = 60;
    double _curtailMargin = 2.25;

    Storyboard? _opacityInStoryboard;
    Storyboard? _opacityOutStoryboard;
    TimeSpan _opacityDuration = TimeSpan.FromMilliseconds(600);

    List<GraphItem> _dataPoints = new();
    #endregion

    #region [Dependency Properties]
    /// <summary>
    ///   This is the property that triggers the plot graph for the <see cref="PlotControl"/>.
    /// </summary>
    public static readonly DependencyProperty PointSourceProperty = DependencyProperty.Register(
        nameof(PointSource),
        typeof(List<GraphItem>),
        typeof(PlotControl),
        new PropertyMetadata(null, OnPointsPropertyChanged));
    public List<GraphItem> PointSource
    {
        get => (List<GraphItem>)GetValue(PointSourceProperty);
        set => SetValue(PointSourceProperty, value);
    }
    static void OnPointsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (PlotControl)d;
        if (e.NewValue is List<GraphItem> points)
            control.PointsChanged(points);
    }
    void PointsChanged(List<GraphItem> points)
    {
        if (!_loaded && points is not null)
        {
            _dataPoints = points;
            return;
        }
        else if (!_loaded || points is null)
            return;

        if (_warnIfNoElements && points.Count == 0)
        {
            host.DispatcherQueue.TryEnqueue(() =>
            {
                tbTitle.Text = "There are no points to graph";
                if (tbSubTitle.Visibility == Visibility.Visible)
                    tbSubTitle.Text = "Make sure the control source is set and it contains elements";
            });
        }

        if (_msDelay > 0)
            DrawRectanglePlotDelayed(points, 0);
        else
            DrawRectanglePlot(points, 0);
    }

    /// <summary>
    ///   This is the main title property of the <see cref="PlotControl"/>.
    /// </summary>
    public static readonly DependencyProperty PlotTitleProperty = DependencyProperty.Register(
        nameof(PlotTitle),
        typeof(string),
        typeof(PlotControl),
        new PropertyMetadata(string.Empty, OnTitlePropertyChanged));
    public string PlotTitle
    {
        get => (string)GetValue(PlotTitleProperty);
        set => SetValue(PlotTitleProperty, value);
    }
    static void OnTitlePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (PlotControl)d;
        if (e.NewValue is string title)
            control.TitleChanged(title);
    }
    void TitleChanged(string title)
    {
        if (string.IsNullOrEmpty(title))
            return;

        tbTitle.Text = title;
    }

    /// <summary>
    ///   This is the secondary title property of the <see cref="PlotControl"/>.
    /// </summary>
    public static readonly DependencyProperty PlotSubTitleProperty = DependencyProperty.Register(
        nameof(PlotSubTitle),
        typeof(string),
        typeof(PlotControl),
        new PropertyMetadata(string.Empty, OnSubTitlePropertyChanged));
    public string PlotSubTitle
    {
        get => (string)GetValue(PlotSubTitleProperty);
        set => SetValue(PlotSubTitleProperty, value);
    }
    static void OnSubTitlePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (PlotControl)d;
        if (e.NewValue is string title)
            control.SubTitleChanged(title);
    }
    void SubTitleChanged(string title)
    {
        if (string.IsNullOrEmpty(title))
            return;

        tbSubTitle.Text = title;
        tbSubTitle.Visibility = Visibility.Visible;
    }

    /// <summary>
    ///   This is the property that determines if the points are drawn to the bottom instead of floating.
    /// </summary>
    public static readonly DependencyProperty PointGroundProperty = DependencyProperty.Register(
        nameof(PointGround),
        typeof(bool),
        typeof(PlotControl),
        new PropertyMetadata(false));
    public bool PointGround
    {
        get => (bool)GetValue(PointGroundProperty);
        set => SetValue(PointGroundProperty, value);
    }

    /// <summary>
    ///   This is the property that determines if the points are drawn to the bottom instead of floating.
    /// </summary>
    public static readonly DependencyProperty PointDelayMSProperty = DependencyProperty.Register(
        nameof(PointDelayMS),
        typeof(int),
        typeof(PlotControl),
        new PropertyMetadata(1, OnPointsDelayPropertyChanged));
    public int PointDelayMS
    {
        get => (int)GetValue(PointDelayMSProperty);
        set => SetValue(PointDelayMSProperty, value);
    }
    static void OnPointsDelayPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (PlotControl)d;
        if (e.NewValue is int delay)
            control.DelayChanged(delay);
    }
    void DelayChanged(int delay)
    {
        if (delay < 0)
            return;

        _msDelay = delay;
    }

    /// <summary>
    ///   This is the property that determines the fill brush color.
    /// </summary>
    public static readonly DependencyProperty PointBrushProperty = DependencyProperty.Register(
        nameof(PointBrush),
        typeof(Brush),
        typeof(PlotControl),
     new PropertyMetadata(null));
    public Brush PointBrush
    {
        get { return (Brush)GetValue(PointBrushProperty); }
        set { SetValue(PointBrushProperty, value); }
    }

    /// <summary>
    ///   This is the property that determines the fill brush color.
    /// </summary>
    public static readonly DependencyProperty PointBorderBrushProperty = DependencyProperty.Register(
        nameof(PointBorderBrush),
        typeof(Brush),
        typeof(PlotControl),
     new PropertyMetadata(null));
    public Brush PointBorderBrush
    {
        get { return (Brush)GetValue(PointBorderBrushProperty); }
        set { SetValue(PointBorderBrushProperty, value); }
    }

    /// <summary>
    ///   This is the property that determines the point size.
    /// </summary>
    public static readonly DependencyProperty PointRadiusProperty = DependencyProperty.Register(
        nameof(PointRadius),
        typeof(double),
        typeof(PlotControl),
        new PropertyMetadata(8d));
    public double PointRadius
    {
        get => (double)GetValue(PointRadiusProperty);
        set => SetValue(PointRadiusProperty, value);
    }

    /// <summary>
    ///   This is the property that determines the point size.
    /// </summary>
    public static readonly DependencyProperty PointStrokeThicknessProperty = DependencyProperty.Register(
        nameof(PointStrokeThickness),
        typeof(double),
        typeof(PlotControl),
        new PropertyMetadata(2d));
    public double PointStrokeThickness
    {
        get => (double)GetValue(PointStrokeThicknessProperty);
        set => SetValue(PointStrokeThicknessProperty, value);
    }

    /// <summary>
    ///   This is the property that determines the point size.
    /// </summary>
    public static readonly DependencyProperty PointCanvasMarginProperty = DependencyProperty.Register(
        nameof(PointCanvasMargin),
        typeof(double),
        typeof(PlotControl),
        new PropertyMetadata(60d, OnCanvasMarginPropertyChanged));
    public double PointCanvasMargin
    {
        get => (double)GetValue(PointCanvasMarginProperty);
        set => SetValue(PointCanvasMarginProperty, value);
    }
    static void OnCanvasMarginPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (PlotControl)d;
        if (e.NewValue is double margin)
            control.CanvasMarginChanged(margin);
    }
    void CanvasMarginChanged(double margin)
    {
        if (margin < 0)
            return;

        _canvasMargin = margin;
    }

    /// <summary>
    ///   This is the property that determines if the points are drawn to the bottom instead of floating.
    /// </summary>
    public static readonly DependencyProperty ShowTitleDividerProperty = DependencyProperty.Register(
        nameof(ShowTitleDivider),
        typeof(bool),
        typeof(PlotControl),
        new PropertyMetadata(false));
    public bool ShowTitleDivider
    {
        get => (bool)GetValue(ShowTitleDividerProperty);
        set => SetValue(ShowTitleDividerProperty, value);
    }
    #endregion

    #region [Constructors]
    public PlotControl()
    {
        this.InitializeComponent();

        //_tooltip = new ToolTip();
        this.Loaded += PlotControlOnLoaded;
        this.Unloaded += PlotControlOnUnloaded;
        this.SizeChanged += PlotControlOnSizeChanged;
    }

    public PlotControl(List<GraphItem> points) : this()
    {
        _dataPoints = points;
    }
    #endregion

    /// <summary>
    /// This only exists for setting the selected GraphItem directly through MainPage's ListView.
    /// </summary>
    public static Action<GraphItem>? GraphItemPlotTap { get; set; }

    protected override Windows.Foundation.Size MeasureOverride(Windows.Foundation.Size availableSize)
    {
        _measureOccurred = true;
        return base.MeasureOverride(availableSize);
    }

    /// <summary>
    /// Draws rectangle plot points on the canvas with a small delay between each render for effect.
    /// </summary>
    /// <remarks>
    /// If zero is given for the <paramref name="maxValue"/> the it will be calculated during 
    /// this method as the normalized graph offset.
    /// </remarks>
    public void DrawRectanglePlotDelayed(List<GraphItem> dataPoints, double maxValue)
    {
        // Clear and previous canvas plots
        cvsPlot.Children.Clear();

        if (dataPoints == null || dataPoints.Count == 0)
            return;

        if (!_sizeSet)
        {
            _sizeSet = true;
            cvsPlot.Width = this.ActualWidth - (_canvasMargin / _curtailMargin);
            cvsPlot.Height = this.ActualHeight - (_canvasMargin + PointRadius);
        }

        List<double> amounts = new();
        for (int i = 0; i < dataPoints.Count; i++)
        {
            amounts.Add(dataPoints[i].Amount);
        }

        // Define Canvas size (you can also get this from the actual Canvas dimensions)
        double canvasWidth = cvsPlot.Width;
        double canvasHeight = cvsPlot.Height;

        // Check for invalid Canvas size
        if (canvasWidth.IsInvalidOrZero() || canvasHeight.IsInvalidOrZero())
            throw new Exception("Invalid canvas size for plot control. Canvas width/height has no value.");

        // If no max was defined, find the maximum value in the data to normalize the y-axis
        if (maxValue <= 0)
            maxValue = amounts.Max();

        #region [Brush Colors]
        Brush? pointFill = null;
        Brush? pointStroke = null;

        if (PointBrush is null)
            pointFill = Extensions.CreateLinearGradientBrush(Colors.MidnightBlue, Colors.WhiteSmoke, Colors.DodgerBlue);
        else
            pointFill = PointBrush;

        if (PointBorderBrush is null)
            pointStroke = new SolidColorBrush(Colors.Gray);
        else
            pointStroke = PointBorderBrush;
        #endregion

        // Calculate spacing between points on the x-axis
        double xSpacing = canvasWidth / (dataPoints.Count + 1); // Add 1 to count for space on left and right of chart

        Task.Run(async () =>
        {
            _isDrawing = true;
            for (int i = 0; i < dataPoints.Count; i++)
            {
                if (!_loaded)
                    break;

                // Calculate X position based on index and spacing
                double x = (i + 1) * xSpacing; // Start plotting with an offset for readability
                if (x.IsInvalid())
                    x = 0;

                // Calculate Y position based on value, maximum value and canvas height
                // Invert y axis so that higher values are at the top.
                double y = canvasHeight - (amounts[i] / (double)maxValue) * canvasHeight;
                if (y.IsInvalid())
                    y = 0;

                // Any access to a Microsoft.UI.Xaml.Controls element must be done on the dispatcher.
                cvsPlot.DispatcherQueue.TryEnqueue(() =>
                {
                    // Create the Microsoft.UI.Xaml.Shapes
                    Rectangle rect = new();
                    rect.Width = PointRadius * 2;
                    if (PointGround)
                        rect.Height = canvasHeight - y;
                    else
                        rect.Height = PointRadius * 6;
                    rect.RadiusX = PointRadius / 3;
                    rect.RadiusY = PointRadius / 3;
                    rect.Fill = pointFill;
                    rect.Stroke = pointStroke;
                    rect.StrokeThickness = PointStrokeThickness;
                    rect.Opacity = _restingOpacity;
                    rect.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);

                    // Position the rect on the canvas
                    Canvas.SetLeft(rect, x - PointRadius); // Center rect horizontally

                    // If the rectangle height is zero, adjust the height to the
                    // stroke thickness instead of zero so the shape will be visible.
                    var rectHeight = y - PointRadius;
                    if (rectHeight >= (canvasHeight - PointRadius))
                    {
                        Debug.WriteLine($"[DEBUG] Point[{i}] Y coord is {rectHeight}");
                        rect.Height = PointStrokeThickness;
                        Canvas.SetTop(rect, canvasHeight - PointRadius - (PointStrokeThickness + 1)); // Center rect vertically
                    }
                    else
                        Canvas.SetTop(rect, rectHeight);   // Center rect vertically

                    // Attach tooltip data value
                    if (i < dataPoints.Count)
                        rect.Tag = dataPoints[i]; // Store the data value in the rect's Tag property
                    rect.PointerEntered += RectangleOnPointerEntered;
                    rect.PointerExited += RectangleOnPointerExited;
                    rect.Tapped += RectangleOnTapped;

                    //rect.Shadow = Extensions.GetResource<ThemeShadow>("CommandBarFlyoutOverflowShadow");
                    //rect.Translation = new System.Numerics.Vector3(0, 0, 32);

                    // Add the shape to the canvas
                    cvsPlot.Children.Add(rect);
                });

                // Add small delay for effect
                await Task.Delay(_msDelay);
            }
            _isDrawing = false;
        });
    }

    /// <summary>
    /// Draws rectangle plot points on the canvas with no delay between each render.
    /// </summary>
    /// <remarks>
    /// If zero is given for the <paramref name="maxValue"/> the it will be calculated during 
    /// this method as the normalized graph offset.
    /// </remarks>
    public void DrawRectanglePlot(List<GraphItem> dataPoints, double maxValue)
    {
        // Clear and previous canvas plots
        cvsPlot.Children.Clear();

        if (dataPoints == null || dataPoints.Count == 0)
            return;

        if (!_sizeSet)
        {
            _sizeSet = true;
            cvsPlot.Width = this.ActualWidth - (_canvasMargin / _curtailMargin);
            cvsPlot.Height = this.ActualHeight - (_canvasMargin + PointRadius);
        }

        List<double> amounts = new();
        for (int i = 0; i < dataPoints.Count; i++)
        {
            amounts.Add(dataPoints[i].Amount);
        }

        // Define Canvas size (you can also get this from the actual Canvas dimensions)
        double canvasWidth = cvsPlot.Width;
        double canvasHeight = cvsPlot.Height;

        // Check for invalid Canvas size
        if (canvasWidth.IsInvalidOrZero() || canvasHeight.IsInvalidOrZero())
            throw new Exception("Invalid canvas size for plot control. Canvas width/height has no value.");

        // If no max was defined, find the maximum value in the data to normalize the y-axis
        if (maxValue <= 0)
            maxValue = amounts.Max();

        #region [Brush Colors]
        Brush? pointFill = null;
        Brush? pointStroke = null;

        if (PointBrush is null)
            pointFill = Extensions.CreateLinearGradientBrush(Colors.MidnightBlue, Colors.WhiteSmoke, Colors.DodgerBlue);
        else
            pointFill = PointBrush;

        if (PointBorderBrush is null)
            pointStroke = new SolidColorBrush(Colors.Gray);
        else
            pointStroke = PointBorderBrush;
        #endregion

        // Calculate spacing between points on the x-axis
        double xSpacing = canvasWidth / (dataPoints.Count + 1); // Add 1 to count for space on left and right of chart

        _isDrawing = true;
        for (int i = 0; i < dataPoints.Count; i++)
        {
            if (!_loaded)
                break;

            // Calculate X position based on index and spacing
            double x = (i + 1) * xSpacing; // Start plotting with an offset for readability
            if (x.IsInvalid())
                x = 0;

            // Calculate Y position based on value, maximum value and canvas height
            // Invert y axis so that higher values are at the top.
            double y = canvasHeight - (amounts[i] / (double)maxValue) * canvasHeight;
            if (y.IsInvalid())
                y = 0;

            // Create the Microsoft.UI.Xaml.Shapes
            Rectangle rect = new();
            rect.Width = PointRadius * 2;
            if (PointGround)
                rect.Height = canvasHeight - y;
            else
                rect.Height = PointRadius * 6;
            rect.RadiusX = PointRadius / 3;
            rect.RadiusY = PointRadius / 3;
            rect.Fill = pointFill;
            rect.Stroke = pointStroke;
            rect.StrokeThickness = PointStrokeThickness;
            rect.Opacity = _restingOpacity;
            rect.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);

            // Position the rect on the canvas
            Canvas.SetLeft(rect, x - PointRadius); // Center rect horizontally

            // If the rectangle height is zero, adjust the height to the
            // stroke thickness instead of zero so the shape will be visible.
            var rectHeight = y - PointRadius;
            if (rectHeight >= (canvasHeight - PointRadius))
            {
                Debug.WriteLine($"[DEBUG] Point[{i}] Y coord is {rectHeight}");
                rect.Height = PointStrokeThickness;
                Canvas.SetTop(rect, canvasHeight - PointRadius - (PointStrokeThickness + 1)); // Center rect vertically
            }
            else
                Canvas.SetTop(rect, rectHeight);   // Center rect vertically

            // Attach tooltip data value
            if (i < dataPoints.Count)
                rect.Tag = dataPoints[i]; // Store the data value in the rect's Tag property
            rect.PointerEntered += RectangleOnPointerEntered;
            rect.PointerExited += RectangleOnPointerExited;
            rect.Tapped += RectangleOnTapped;

            //rect.Shadow = Extensions.GetResource<ThemeShadow>("CommandBarFlyoutOverflowShadow");
            //rect.Translation = new System.Numerics.Vector3(0, 0, 32);

            // Add the shape to the canvas
            cvsPlot.Children.Add(rect);
        }
        _isDrawing = false;

    }

    /// <summary>
    /// Draws circle plot points on the canvas.
    /// </summary>
    /// <remarks>
    /// If zero is given for the <paramref name="maxValue"/> the it will be calculated during 
    /// this method as the normalized graph offset.
    /// </remarks>
    public void DrawCirclePlot(List<int> dataPoints, int maxValue = 0)
    {
        // Clear any previous canvas plots
        cvsPlot.Children.Clear();

        if (dataPoints == null || dataPoints.Count == 0)
            return;

        if (!_sizeSet)
        {
            _sizeSet = true;
            cvsPlot.Width = this.ActualWidth - (_canvasMargin / _curtailMargin);
            cvsPlot.Height = this.ActualHeight - (_canvasMargin + PointRadius);
        }

        // Define Canvas size (you can also get this from the actual Canvas dimensions)
        double canvasWidth = cvsPlot.Width;
        double canvasHeight = cvsPlot.Height;

        // Check for invalid Canvas size
        if (canvasWidth.IsInvalidOrZero() || canvasHeight.IsInvalidOrZero())
        {
            Debug.WriteLine("[WARNING] Invalid canvas size. Canvas width/height has no value.");
            return;
        }

        // If no max was defined, find the maximum value in the data to normalize the y-axis
        if (maxValue <= 0)
            maxValue = dataPoints.Max();

        // Colored brush appearance
        var circleFill = Extensions.CreateLinearGradientBrush(Colors.WhiteSmoke, Colors.DodgerBlue, Colors.MidnightBlue);
        SolidColorBrush circleStroke = new SolidColorBrush(Colors.Gray);

        // Calculate spacing between points on the x-axis
        double xSpacing = canvasWidth / (dataPoints.Count + 1); // Add 1 to count for space on left and right of chart

        _isDrawing = true;

        // Draw the circles
        for (int i = 0; i < dataPoints.Count; i++)
        {
            if (!_loaded)
                break;

            // Calculate X position based on index and spacing
            double x = (i + 1) * xSpacing; // Start plotting with an offset for readability
            if (x.IsInvalid())
                x = 0;

            // Calculate Y position based on value, maximum value and canvas height
            // Invert y axis so that higher values are at the top.
            double y = canvasHeight - (dataPoints[i] / (double)maxValue) * canvasHeight;
            if (y.IsInvalid())
                y = 0;

            // Create the Microsoft.UI.Xaml.Shapes
            Ellipse circle = new Ellipse();
            circle.Width = PointRadius * 2;
            circle.Height = PointRadius * 2;
            circle.Fill = circleFill;
            circle.Stroke = circleStroke;
            circle.StrokeThickness = PointStrokeThickness;
            circle.Opacity = _restingOpacity;

            // Position the circle on the canvas
            Canvas.SetLeft(circle, x - PointRadius); // Center circle horizontally
            Canvas.SetTop(circle, y - PointRadius);   // Center circle vertically

            // Attach tooltip data value
            circle.Tag = dataPoints[i]; // Store the data value in the circle's Tag property
            //circle.PointerEntered += CircleOnPointerEntered;
            //circle.PointerExited += CircleOnPointerExited;

            // Add the shape to the canvas
            cvsPlot.Children.Add(circle);
        }
        _isDrawing = false;
    }

    #region [Events]
    /// <summary>
    /// This can be called many time on first render, so avoid setting the 
    /// control sizes multiple times or it may cause a layout cycle exception.
    /// </summary>
    void PlotControlOnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width.IsInvalidOrZero() || e.NewSize.Height.IsInvalidOrZero())
            return;

        //Debug.WriteLine($"[EVENT] PlotControl got size change: {e.NewSize.Width},{e.NewSize.Height}");

        if (cvsPlot.Width.IsInvalidOrZero() && cvsPlot.Height.IsInvalidOrZero())
        {
            cvsPlot.Width = e.NewSize.Width - (_canvasMargin / _curtailMargin);
            cvsPlot.Height = e.NewSize.Height - (_canvasMargin + PointRadius);
        }
    }

    void PlotControlOnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_loaded)
        {
            _loaded = true;
            cvsPlot.Margin = new Thickness(10);
            dividerBar.Visibility = ShowTitleDivider ? Visibility.Visible : Visibility.Collapsed;

            Debug.WriteLine($"[DEBUG] Measurement override occurred: {_measureOccurred}");

            // If we received data during constructor then plot it.
            if (_dataPoints.Count > 0)
            {
                // Allow some time for the control to render before plotting.
                Task.Run(async () => { await Task.Delay(300); }).ContinueWith(t =>
                {
                    host.DispatcherQueue.TryEnqueue(() =>
                    {
                        if (_msDelay > 0)
                            DrawRectanglePlotDelayed(_dataPoints, 0);
                        else
                            DrawRectanglePlot(_dataPoints, 0);
                    });
                });
            }
            else if (_warnIfNoElements) // empty data warning
            {
                host.DispatcherQueue.TryEnqueue(() =>
                {
                    tbTitle.Text = "There are no points to graph";
                    if (tbSubTitle.Visibility == Visibility.Visible)
                        tbSubTitle.Text = "Make sure the control source is set and it contains elements";
                });
            }
        }
    }

    void PlotControlOnUnloaded(object sender, RoutedEventArgs e) => _loaded = false;

    /// <summary>
    /// Runs opacity animation and shows tooltip when pointer enters.
    /// </summary>
    void RectangleOnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (!_loaded)
            return;

        try
        {
            var rect = (Rectangle)sender;
            if (rect is null)
                return;

            GeneralTransform transform = rect.TransformToVisual(this); // "cvsPlot", or "root" grid, or "this" if it's a Page/UserControl
            Windows.Foundation.Point position = transform.TransformPoint(new Windows.Foundation.Point(rect.Width / 2, rect.Height / 2)); // Center of the circle
            //Debug.WriteLine($"[INFO] Position data is X={position.X:N0}, Y={position.Y:N0}");

            var item = (GraphItem)rect.Tag;

            #region [Didn't work properly]
            //var tooltipExample = ToolTipService.GetToolTip(rect) as ToolTip;
            //_tooltip.Content = $"{item.Amount}";
            //_tooltip.PlacementTarget = rect;
            //_tooltip.PlacementRect = new Rect(position.X, position.Y, 100, 40);
            //_tooltip.Placement = PlacementMode.Mouse;
            //_tooltip.HorizontalOffset = _radius + 1;  // X offset from mouse
            //_tooltip.VerticalOffset = _radius + 1;    // Y offset from mouse
            //ToolTipService.SetToolTip(rect, _tooltip);
            //_tooltip.IsOpen = true;
            //_tooltip.IsEnabled = true;
            #endregion

            ttValue.Text = $"{item.Title}{Environment.NewLine}Count: {item.Amount}{Environment.NewLine}Score: {item.Score}";


            ttPlot.PlacementTarget = rect;
            // Setting the placement rectangle is important when using code-behind
            ttPlot.PlacementRect = new Windows.Foundation.Rect(position.X, position.Y, 280, 70);
            ttPlot.Placement = PlacementMode.Mouse;   // this behaves abnormally when not set to mouse
            ttPlot.HorizontalOffset = PointRadius <= 10 ? PointRadius : PointRadius / 2; // X offset from mouse
            ttPlot.VerticalOffset = PointRadius <= 10 ? PointRadius : PointRadius / 2;   // Y offset from mouse
            ttPlot.Visibility = Visibility.Visible;
            //Debug.WriteLine($"[INFO] TooltipWidth={ttPlot.ActualWidth:N0}  TooltipHeight={ttPlot.ActualHeight:N0}");

            #region [Fix for updating the Flyout content tooltip does not appear]
            /*
                Flyouts are disconnected popups (separate visual trees).
                ToolTips rely on PointerEntered ⇨ PointerMoved ⇨ PointerHover ⇨ ToolTip lifecycle.
                Replacing content resets internal hit-test tree, but mouse isn't re-entering physically.
            */
            ToolTipService.SetToolTip(host, ttPlot);
            #endregion

            #region [Animation]
            if (_opacityInStoryboard == null)
            {
                // Create the storyboard and animation only once
                _opacityInStoryboard = new Storyboard();
                DoubleAnimation opacityAnimation = new DoubleAnimation
                {
                    From = _restingOpacity,
                    To = 1.0,
                    EnableDependentAnimation = true,
                    EasingFunction = new QuadraticEase(),
                    Duration = new Duration(_opacityDuration),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                };
                Storyboard.SetTargetProperty(opacityAnimation, "Opacity");
                _opacityInStoryboard.Children.Add(opacityAnimation);
            }
            else
            {
                _opacityInStoryboard.Stop(); // Stop any previous animation
            }
            Storyboard.SetTarget(_opacityInStoryboard.Children[0], (Rectangle)sender); // Set the new target
            _opacityInStoryboard.Begin();
            #endregion
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] OnPointerEntered: {ex.Message}");
        }
    }

    /// <summary>
    /// Hide the tooltip when the pointer exits the plot point.
    /// </summary>
    void RectangleOnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (_isDrawing || !_loaded)
            return;

        ttPlot.Visibility = Visibility.Collapsed;

        #region [Animation]
        if (_opacityOutStoryboard == null)
        {
            // Create the storyboard and animation only once
            _opacityOutStoryboard = new Storyboard();
            DoubleAnimation opacityAnimation = new DoubleAnimation
            {
                From = 1.0, // From = ((Rectangle)sender).Opacity,
                To = _restingOpacity,
                EnableDependentAnimation = true,
                EasingFunction = new QuadraticEase(),
                Duration = new Duration(_opacityDuration),
                //RepeatBehavior = RepeatBehavior.Forever,
            };

            Storyboard.SetTargetProperty(opacityAnimation, "Opacity");
            _opacityOutStoryboard.Children.Add(opacityAnimation);
        }
        else
        {
            _opacityOutStoryboard.Stop(); // Stop any previous animation
        }
        Storyboard.SetTarget(_opacityOutStoryboard.Children[0], (Rectangle)sender); // Set the new target
        _opacityOutStoryboard.Begin();
        #endregion
    }

    /// <summary>
    /// Triggers event to auto-select the item in the <see cref="ListView"/> 
    /// control on the <see cref="MainPage"/>.
    /// </summary>
    void RectangleOnTapped(object sender, TappedRoutedEventArgs e)
    {
        var rect = (Rectangle)sender;
        if (rect is null)
            return;

        var item = (GraphItem)rect.Tag;
        if (item is null)
            return;

        // We need direct access to the ListView control for this effect to work.
        GraphItemPlotTap?.Invoke(item);

        // You could also create a static reference to the MainPage during the ctor and then call the public method.
        //if (MainPage.StaticRef is not null)
        //    MainPage.StaticRef.SetSelectedItem(item);

    }
    #endregion

    /// <summary>
    /// For use in graphing currency values.
    /// </summary>
    bool TryParseDollarAmount(string amount, out double value)
    {
        if (string.IsNullOrEmpty(amount))
        {
            value = 0;
            return false;
        }

        // Remove the dollar sign if present
        string cleanedAmount = amount.Replace(CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol, "").Trim();

        // Attempt to parse the cleaned amount
        return double.TryParse(cleanedAmount, System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands | System.Globalization.NumberStyles.AllowCurrencySymbol, System.Globalization.CultureInfo.CurrentCulture, out value);
        //return double.TryParse(cleanedAmount, NumberStyles.Currency, CultureInfo.InvariantCulture, out value);
    }
}