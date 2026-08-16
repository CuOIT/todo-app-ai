using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace AiTaskTracker;

internal sealed class ErrorReportWindow : Window
{
    private readonly string _report;
    private readonly TextBlock _copyFeedbackText = new();

    public ErrorReportWindow(Window? owner, Exception exception)
    {
        _report = BuildReport(exception);

        if (owner is { IsVisible: true })
        {
            Owner = owner;
        }
        Width = 620;
        Height = 460;
        MinWidth = 600;
        MinHeight = 420;
        WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = owner is null;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Title = "AI Task Tracker error";
        Opacity = 0;

        var root = new Border
        {
            Background = new LinearGradientBrush(
                Color.FromRgb(22, 17, 23),
                Color.FromRgb(10, 13, 19),
                90),
            BorderBrush = BrushFrom(116, 45, 54),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(22),
            Effect = new DropShadowEffect
            {
                BlurRadius = 34,
                ShadowDepth = 10,
                Opacity = 0.62,
                Color = Colors.Black
            }
        };

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = CreateHeader();
        Grid.SetRow(header, 0);
        layout.Children.Add(header);

        var reportBox = new TextBox
        {
            Text = _report,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Background = BrushFrom(12, 17, 25),
            BorderThickness = new Thickness(0),
            Foreground = Brushes.White,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            Padding = new Thickness(11),
            SelectionBrush = BrushFrom(41, 77, 122)
        };
        reportBox.SetValue(TextBlock.LineHeightProperty, 17d);
        var reportSurface = new Border
        {
            Background = BrushFrom(12, 17, 25),
            BorderBrush = BrushFrom(59, 44, 56),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Margin = new Thickness(0, 18, 0, 0),
            ClipToBounds = true,
            Effect = new DropShadowEffect
            {
                BlurRadius = 10,
                ShadowDepth = 0,
                Opacity = 0.10,
                Color = Color.FromRgb(2, 6, 11)
            },
            Child = reportBox
        };
        Grid.SetRow(reportSurface, 1);
        layout.Children.Add(reportSurface);

        var actions = CreateActions();
        Grid.SetRow(actions, 2);
        layout.Children.Add(actions);

        root.Child = layout;
        Content = root;

        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        };
        Loaded += (_, _) =>
        {
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(120))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        };
    }

    private UIElement CreateHeader()
    {
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new Border
        {
            Width = 52,
            Height = 52,
            Background = BrushFrom(90, 31, 36),
            BorderBrush = BrushFrom(255, 107, 107),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Margin = new Thickness(0, 0, 14, 0),
            Child = new TextBlock
            {
                Text = "\uE783",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 22,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        header.Children.Add(icon);

        var title = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        title.Children.Add(new TextBlock
        {
            Text = "Something went wrong",
            Foreground = Brushes.White,
            FontSize = 22,
            FontWeight = FontWeights.SemiBold
        });
        title.Children.Add(new TextBlock
        {
            Text = "The app caught the error so you can copy a report before continuing.",
            Foreground = BrushFrom(161, 168, 181),
            FontSize = 13,
            Margin = new Thickness(0, 5, 0, 0)
        });
        Grid.SetColumn(title, 1);
        header.Children.Add(title);

        var closeButton = CreateIconButton("\uE711", "Close error report");
        closeButton.Click += (_, _) => Close();
        Grid.SetColumn(closeButton, 2);
        header.Children.Add(closeButton);

        return header;
    }

    private UIElement CreateActions()
    {
        var footer = new Border
        {
            Background = BrushFrom(15, 13, 20),
            BorderBrush = BrushFrom(75, 45, 55),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(11),
            Margin = new Thickness(0, 18, 0, 0)
        };

        var actions = new DockPanel { LastChildFill = false };

        var copyButton = CreateButton("Copy error report", BrushFrom(90, 31, 36), BrushFrom(255, 107, 107), Colors.White);
        copyButton.Click += (_, _) => CopyReport();
        DockPanel.SetDock(copyButton, Dock.Left);
        actions.Children.Add(copyButton);

        _copyFeedbackText.Text = string.Empty;
        _copyFeedbackText.Foreground = BrushFrom(255, 183, 183);
        _copyFeedbackText.FontSize = 12;
        _copyFeedbackText.FontWeight = FontWeights.SemiBold;
        _copyFeedbackText.VerticalAlignment = VerticalAlignment.Center;
        _copyFeedbackText.Margin = new Thickness(10, 0, 0, 0);
        DockPanel.SetDock(_copyFeedbackText, Dock.Left);
        actions.Children.Add(_copyFeedbackText);

        var doneButton = CreateButton("Close", BrushFrom(47, 128, 237), BrushFrom(88, 166, 255), Colors.White);
        doneButton.Click += (_, _) => Close();
        DockPanel.SetDock(doneButton, Dock.Right);
        actions.Children.Add(doneButton);

        footer.Child = actions;
        return footer;
    }

    private void CopyReport()
    {
        Clipboard.SetText(_report);
        _copyFeedbackText.Text = "Copied";
    }

    private static string BuildReport(Exception exception)
    {
        var report = new StringBuilder();
        report.AppendLine("AI Task Tracker error report");
        report.AppendLine($"Generated: {DateTimeOffset.Now:O}");
        report.AppendLine($"Type: {exception.GetType().FullName}");
        report.AppendLine($"Message: {exception.Message}");
        report.AppendLine();
        report.AppendLine(exception.ToString());
        return report.ToString();
    }

    private static Button CreateButton(string text, Brush background, Brush border, Color foreground)
    {
        return new Button
        {
            Content = text,
            MinWidth = 92,
            Height = 36,
            Padding = new Thickness(14, 0, 14, 0),
            Background = background,
            BorderBrush = border,
            BorderThickness = new Thickness(1),
            Foreground = new SolidColorBrush(foreground),
            Cursor = Cursors.Hand,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            Template = CreateButtonTemplate()
        };
    }

    private static Button CreateIconButton(string glyph, string automationName)
    {
        return new Button
        {
            Content = glyph,
            Width = 32,
            Height = 32,
            Padding = new Thickness(0),
            Background = BrushFrom(26, 20, 29),
            BorderBrush = BrushFrom(75, 45, 55),
            BorderThickness = new Thickness(1),
            Foreground = Brushes.White,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            Cursor = Cursors.Hand,
            ToolTip = automationName,
            Template = CreateButtonTemplate()
        };
    }

    private static ControlTemplate CreateButtonTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "ButtonChrome";
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
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));

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
                new Setter(UIElement.OpacityProperty, 0.92)
            }
        });
        template.Triggers.Add(new Trigger
        {
            Property = Button.IsPressedProperty,
            Value = true,
            Setters =
            {
                new Setter(UIElement.OpacityProperty, 0.78)
            }
        });
        template.Triggers.Add(new Trigger
        {
            Property = Button.IsEnabledProperty,
            Value = false,
            Setters =
            {
                new Setter(UIElement.OpacityProperty, 0.45)
            }
        });

        return template;
    }

    private static SolidColorBrush BrushFrom(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(Color.FromRgb(red, green, blue));
    }
}
