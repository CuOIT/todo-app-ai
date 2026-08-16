using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace AiTaskTracker;

public sealed class FloatingToggleWindow : Window
{
    private readonly Button _toggleButton;
    private readonly Border _stateBadge;
    private readonly TextBlock _stateGlyph;
    private readonly Action _togglePopup;
    private Point? _dragStart;
    private bool _isDragging;

    public FloatingToggleWindow(Action togglePopup)
    {
        _togglePopup = togglePopup;

        Width = 60;
        Height = 60;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Title = "Task Toggle";
        Icon = Application.Current.TryFindResource("AppIcon") as ImageSource;

        Left = SystemParameters.WorkArea.Right - Width - 18;
        Top = SystemParameters.WorkArea.Bottom - Height - 18;

        _stateGlyph = new TextBlock
        {
            Text = "\u2212",
            FontFamily = new FontFamily("Segoe UI Symbol"),
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _stateBadge = new Border
        {
            Width = 18,
            Height = 18,
            CornerRadius = new CornerRadius(9),
            Background = new SolidColorBrush(Color.FromRgb(39, 49, 65)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(77, 91, 114)),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Child = _stateGlyph
        };

        var logo = new Image
        {
            Source = Application.Current.TryFindResource("AppIcon") as ImageSource,
            Width = 25,
            Height = 25,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var buttonContent = new Grid
        {
            Margin = new Thickness(8),
            Children = { logo, _stateBadge }
        };

        _toggleButton = new Button
        {
            Content = buttonContent,
            Width = 48,
            Height = 48,
            MinHeight = 0,
            Padding = new Thickness(0),
            Cursor = Cursors.Hand,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromRgb(20, 25, 35)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(53, 65, 84)),
            BorderThickness = new Thickness(1),
            ToolTip = "Show AI Task Tracker",
            Template = CreateButtonTemplate(),
            Effect = new DropShadowEffect
            {
                BlurRadius = 20,
                ShadowDepth = 5,
                Opacity = 0.5,
                Color = Colors.Black
            }
        };
        AutomationProperties.SetName(_toggleButton, "Show AI Task Tracker");

        _toggleButton.Click += (_, _) =>
        {
            if (!_isDragging)
            {
                _togglePopup();
            }
        };
        _toggleButton.PreviewMouseLeftButtonDown += (_, e) =>
        {
            _dragStart = e.GetPosition(this);
            _isDragging = false;
        };
        _toggleButton.PreviewMouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed || _dragStart is null)
            {
                return;
            }

            var current = e.GetPosition(this);
            var movedFarEnough =
                Math.Abs(current.X - _dragStart.Value.X) >= SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(current.Y - _dragStart.Value.Y) >= SystemParameters.MinimumVerticalDragDistance;

            if (!movedFarEnough)
            {
                return;
            }

            _isDragging = true;
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                _isDragging = false;
            }
        };
        _toggleButton.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (_isDragging)
            {
                e.Handled = true;
            }
            _dragStart = null;
        };

        Content = new Grid
        {
            Children = { _toggleButton }
        };
    }

    public void SetPopupState(bool isVisible)
    {
        _stateGlyph.Text = isVisible ? "\u2212" : "+";
        _toggleButton.ToolTip = isVisible ? "Minimize AI Task Tracker" : "Show AI Task Tracker";
        AutomationProperties.SetName(_toggleButton, isVisible ? "Minimize AI Task Tracker" : "Show AI Task Tracker");
        _toggleButton.Background = isVisible
            ? new SolidColorBrush(Color.FromRgb(20, 25, 35))
            : new SolidColorBrush(Color.FromRgb(37, 99, 235));
        _toggleButton.BorderBrush = isVisible
            ? new SolidColorBrush(Color.FromRgb(53, 65, 84))
            : new SolidColorBrush(Color.FromRgb(88, 166, 255));
        _stateBadge.Background = isVisible
            ? new SolidColorBrush(Color.FromRgb(39, 49, 65))
            : new SolidColorBrush(Color.FromRgb(13, 71, 161));
        _stateBadge.BorderBrush = isVisible
            ? new SolidColorBrush(Color.FromRgb(77, 91, 114))
            : new SolidColorBrush(Color.FromRgb(147, 197, 253));
    }

    private static ControlTemplate CreateButtonTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetBinding(Border.BackgroundProperty, new Binding(nameof(Button.Background))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        border.SetBinding(Border.BorderBrushProperty, new Binding(nameof(Button.BorderBrush))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        border.SetBinding(Border.BorderThicknessProperty, new Binding(nameof(Button.BorderThickness))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(14));

        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(content);

        var template = new ControlTemplate(typeof(Button))
        {
            VisualTree = border
        };
        template.Triggers.Add(new Trigger
        {
            Property = Button.IsMouseOverProperty,
            Value = true,
            Setters =
            {
                new Setter(Button.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(118, 181, 255)))
            }
        });
        template.Triggers.Add(new Trigger
        {
            Property = Button.IsPressedProperty,
            Value = true,
            Setters =
            {
                new Setter(Button.OpacityProperty, 0.82)
            }
        });
        return template;
    }
}
