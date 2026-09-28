using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Carnac.Logic;

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

        public static void Show(Canvas layer, ClickHighlight highlight)
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
                Stroke = new SolidColorBrush(GetColor(highlight.ColorName)),
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
            fade.Completed += (sender, e) => layer.Children.Remove(ring);

            scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
            ring.BeginAnimation(UIElement.OpacityProperty, fade);
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
