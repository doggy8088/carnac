using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Carnac.Logic;
using Carnac.Logic.Overlay;

namespace Carnac.UI
{
    /// <summary>The ring drawn around a mouse click: it grows to its full size while it fades away.</summary>
    public static class ClickRing
    {
        // Clicks in quick succession each get a ring of their own, but not without limit
        const int MaximumRings = 32;
        const double StartScale = 0.3;
        const double StartOpacity = 0.9;
        const double StrokeThickness = 4;

        // frozen brushes, made once per colour: the UI thread is the only one that draws rings
        static readonly Dictionary<string, Brush> Brushes = new Dictionary<string, Brush>();

        public static void Show(Canvas layer, ClickHighlight highlight, Action onCompleted = null)
        {
            while (layer.Children.Count >= MaximumRings)
            {
                layer.Children.RemoveAt(0);
            }

            var scale = new ScaleTransform(StartScale, StartScale);
            var ring = new Ellipse
            {
                Width = highlight.Diameter,
                Height = highlight.Diameter,
                Stroke = GetBrush(highlight.ColorName),
                StrokeThickness = StrokeThickness,
                Opacity = StartOpacity,
                IsHitTestVisible = false,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = scale
            };
            Canvas.SetLeft(ring, highlight.Location.X - highlight.Diameter / 2.0);
            Canvas.SetTop(ring, highlight.Location.Y - highlight.Diameter / 2.0);
            layer.Children.Add(ring);

            var grow = new DoubleAnimation(StartScale, 1.0, highlight.Duration)
            {
                EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut }
            };
            var fade = new DoubleAnimation(StartOpacity, 0.0, highlight.Duration)
            {
                EasingFunction = new CircleEase { EasingMode = EasingMode.EaseIn }
            };
            fade.Completed += (sender, e) =>
            {
                layer.Children.Remove(ring);
                if (onCompleted != null)
                    onCompleted();
            };

            scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
            ring.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        public static void ShowAtScreenLocation(Visual referenceVisual, ClickHighlight highlight)
        {
            var source = PresentationSource.FromVisual(referenceVisual);
            var dpiScale = source != null && source.CompositionTarget != null
                ? source.CompositionTarget.TransformToDevice.M11
                : 1.0;
            if (dpiScale <= 0 || double.IsInfinity(dpiScale) || double.IsNaN(dpiScale))
                dpiScale = 1.0;

            var size = highlight.Diameter + StrokeThickness * 4;
            var canvas = new Canvas { Width = size, Height = size, IsHitTestVisible = false };
            var window = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                BorderBrush = System.Windows.Media.Brushes.Transparent,
                ShowInTaskbar = false,
                ShowActivated = false,
                Topmost = true,
                Focusable = false,
                IsHitTestVisible = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Width = size,
                Height = size,
                Content = canvas
            };
            window.SourceInitialized += (sender, e) =>
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                Win32Methods.ApplyOverlayWindowStyles(hwnd, false);
                var pixelSize = (int)Math.Ceiling(size * dpiScale);
                var pixelLeft = (int)Math.Round(highlight.Location.X - pixelSize / 2.0);
                var pixelTop = (int)Math.Round(highlight.Location.Y - pixelSize / 2.0);
                Win32Methods.SetWindowRect(hwnd, new PixelRect(pixelLeft, pixelTop, pixelSize, pixelSize));
            };
            window.Show();
            Show(
                canvas,
                new ClickHighlight(new OverlayLocation(size / 2.0, size / 2.0), highlight.ColorName, highlight.Diameter, highlight.Duration),
                () =>
                {
                    try { window.Close(); } catch { }
                });
        }

        static Brush GetBrush(string colorName)
        {
            Brush brush;
            if (!Brushes.TryGetValue(colorName, out brush))
            {
                brush = new SolidColorBrush(GetColor(colorName));
                brush.Freeze();
                Brushes[colorName] = brush;
            }

            return brush;
        }

        static Color GetColor(string name)
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(name);
            }
            catch (Exception)
            {
                // the name has been checked against the known colors, this is only a safety net
                return Colors.OrangeRed;
            }
        }
    }
}
