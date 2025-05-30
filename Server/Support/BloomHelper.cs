using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;

using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;

namespace IPC_Demo;

public static class BloomHelper
{
    static readonly Windows.UI.Color _defaultColor = Windows.UI.Color.FromArgb(190, 255, 255, 255); // white with slight opacity

    /// <summary>
    ///   A down-n-dirty bloom effect for any <see cref="UIElement"/> that has a <see cref="Panel"/> parent.
    ///   This defeats any existing animations the control has because ExpressionAnimations are created 
    ///   to facilitate the bloom effect via the control's <see cref="Microsoft.UI.Composition.VisualCollection"/>.
    ///   Animations may still occur if they are internally performed by the control (e.g. a custom control).
    /// </summary>
    /// <remarks>
    ///   This can be applied multiple times to the <see cref="UIElement"/> for a stronger effect.
    ///   Should never be applied more than one parent level up, since this will make offset adjustments
    ///   to the <see cref="Microsoft.UI.Composition.LayerVisual"/>, this could result in undesired behavior.
    /// </remarks>
    public static void AddBloom(UIElement? element, UIElement? parent, Windows.UI.Color color, Vector3 offset, float blurRadius = 10)
    {
        if (element == null || parent == null)
        {
            Debug.WriteLine($"[WARNING] AddBloom: One (or more) UIElement is null, cannot continue.");
            return;
        }

        if (color == Microsoft.UI.Colors.Transparent)
            color = _defaultColor;

        // We're making a copy of the parent and then applying the bloom effect to its sibling.
        var visual = ElementCompositionPreview.GetElementVisual(element);
        visual.Opacity = 0;
        var compositor = visual.Compositor;

        var sizeBind = compositor.CreateExpressionAnimation("visual.Size");
        sizeBind.SetReferenceParameter("visual", visual);

        var offsetBind = compositor.CreateExpressionAnimation("visual.Offset");
        offsetBind.SetReferenceParameter("visual", visual);

        var rVisual = compositor.CreateRedirectVisual(visual);
        rVisual.StartAnimation("Size", sizeBind);

        var lVisual = compositor.CreateLayerVisual();
        lVisual.StartAnimation("Size", sizeBind);
        lVisual.StartAnimation("Offset", offsetBind);

        lVisual.Children.InsertAtTop(rVisual);

        var shadow = compositor.CreateDropShadow();
        shadow.BlurRadius = blurRadius;
        shadow.Color = color;
        shadow.Offset = offset;
        shadow.SourcePolicy = Microsoft.UI.Composition.CompositionDropShadowSourcePolicy.InheritFromVisualContent;

        // Set the LayerVisual's shadow and opacity.
        lVisual.Shadow = shadow;
        lVisual.Opacity = (float)element.Opacity;

        var parentContainerVisual = ElementCompositionPreview.GetElementChildVisual(parent) as Microsoft.UI.Composition.ContainerVisual;
        // Create a visual if no parent container visual exists.
        if (parentContainerVisual == null)
        {
            parentContainerVisual = compositor.CreateContainerVisual();
            parentContainerVisual.RelativeSizeAdjustment = Vector2.One;
            ElementCompositionPreview.SetElementChildVisual(parent, parentContainerVisual);
        }
        // Insert the visual at the top of the collection.
        parentContainerVisual.Children.InsertAtTop(lVisual);
    }
    public static void AddBloom(UIElement? element, UIElement? parent, float blurRadius = 10) => AddBloom(element, parent, _defaultColor, Vector3.Zero, blurRadius);
    public static void AddBloom(UIElement? element, UIElement? parent, Windows.UI.Color color, float blurRadius = 10) => AddBloom(element, parent, color, Vector3.Zero, blurRadius);

    /// <summary>
    ///   Removes the bloom effect from the specified <see cref="UIElement"/>. If <paramref name="layerVisual"/> 
    ///   is null, all <see cref="Microsoft.UI.Composition.Visual"/>s will be removed from the parent container.
    /// </summary>
    public static void RemoveBloom(UIElement? element, UIElement? parent, Microsoft.UI.Composition.LayerVisual? layerVisual)
    {
        if (element == null || parent == null)
            return;

        var visual = ElementCompositionPreview.GetElementVisual(element);
        visual.Opacity = (float)element.Opacity;
        var parentContainerVisual = ElementCompositionPreview.GetElementChildVisual(parent) as Microsoft.UI.Composition.ContainerVisual;
        if (parentContainerVisual != null)
        {
            // Remove the given visual or remove all visuals.
            if (layerVisual is not null)
            {
                parentContainerVisual.Children.Remove(layerVisual);
            }
            else
            {
                //var visuals = parentContainerVisual.Children.ToList();
                //foreach (var vis in visuals)
                //{
                //    parentContainerVisual.Children.Remove(vis);
                //}
                parentContainerVisual.Children.RemoveAll();
            }
        }
    }

    /// <summary>
    ///   Removes all child <see cref="Microsoft.UI.Composition.Visual"/>s from the given <see cref="UIElement"/>.
    /// </summary>
    /// <param name="element"><see cref="UIElement"/></param>
    public static void RemoveAllChildVisuals(UIElement element)
    {
        var visual = ElementCompositionPreview.GetElementChildVisual(element) as ContainerVisual;
        if (visual != null)
            visual.Children.RemoveAll();
    }

    /// <summary>
    ///   Finds the nearest <see cref="Panel"/> parent and removes any child <see cref="Microsoft.UI.Composition.Visual"/>s.
    /// </summary>
    /// <param name="childElement"><see cref="UIElement"/></param>
    public static void RemoveAllParentVisuals(UIElement childElement)
    {
        var parentPanel = FindParentPanel(childElement);
        if (parentPanel != null)
        {
            var visual = ElementCompositionPreview.GetElementChildVisual(parentPanel) as ContainerVisual;
            if (visual != null)
                visual.Children.RemoveAll();
        }
    }

    /// <summary>
    ///   Finds the nearest <see cref="Panel"/> parent of the given <see cref="UIElement"/>.
    /// </summary>
    /// <param name="element"><see cref="UIElement"/></param>
    /// <returns>Parent <see cref="Panel"/> if successful, otherwise <see cref="null"/>.</returns>
    public static Panel? FindParentPanel(UIElement element)
    {
        if (element == null)
            return null;

        DependencyObject parent = element;
        while (parent != null)
        {
            parent = VisualTreeHelper.GetParent(parent);
            // Not including Primitives here, e.g. "LoopingSelectorPanel".
            if (parent is StackPanel panel) { return panel; }
            else if (parent is Grid grid) { return grid; }
            else if (parent is Canvas cnvs) { return cnvs; }
            else if (parent is ItemsStackPanel ispnl) { return ispnl; }
            else if (parent is ItemsWrapGrid iwgrd) { return iwgrd; }
            else if (parent is RelativePanel rpanel) { return rpanel; }
            else if (parent is SwapChainPanel scpnl) { return scpnl; }
            else if (parent is SwapChainBackgroundPanel scbpnl) { return scbpnl; }
            else if (parent is VariableSizedWrapGrid vswgrd) { return vswgrd; }
            else if (parent is VirtualizingPanel vpnl) { return vpnl; }
            else if (parent is VirtualizingStackPanel vspnl) { return vspnl; }
            else if (parent is WrapGrid wgrd) { return wgrd; }
        }
        return null; // not found
    }
}
