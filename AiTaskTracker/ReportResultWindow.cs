using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace AiTaskTracker;

internal sealed class ReportResultWindow : Window
{
    private readonly string _detail;
    private readonly string _path;
    private readonly string _title;
    private readonly TextBlock _copyFeedbackText = new();

    public ReportResultWindow(Window owner, string title, string detail, string path)
    {
        _title = title;
        _detail = detail;
        _path = path;

        Owner = owner;
        Width = 560;
        Height = 306;
        MinWidth = 540;
        MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Title = title;
        Opacity = 0;

        var root = new Border
        {
            Background = new LinearGradientBrush(
                Color.FromRgb(17, 20, 28),
                Color.FromRgb(10, 13, 19),
                90),
            BorderBrush = BrushFrom(55, 65, 84),
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
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = CreateHeader();
        Grid.SetRow(header, 0);
        layout.Children.Add(header);

        var content = CreateContent();
        Grid.SetRow(content, 1);
        layout.Children.Add(content);

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
            Background = BrushFrom(23, 58, 97),
            BorderBrush = BrushFrom(88, 166, 255),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Margin = new Thickness(0, 0, 14, 0),
            Child = new TextBlock
            {
                Text = "\uE9D5",
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
            Text = _title,
            Foreground = Brushes.White,
            FontSize = 22,
            FontWeight = FontWeights.SemiBold
        });
        title.Children.Add(new TextBlock
        {
            Text = _detail,
            Foreground = BrushFrom(161, 168, 181),
            FontSize = 13,
            Margin = new Thickness(0, 5, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });
        Grid.SetColumn(title, 1);
        header.Children.Add(title);

        var closeButton = CreateIconButton("\uE711", "Close result");
        closeButton.Click += (_, _) => Close();
        Grid.SetColumn(closeButton, 2);
        header.Children.Add(closeButton);

        return header;
    }

    private UIElement CreateContent()
    {
        var block = new Border
        {
            Background = BrushFrom(12, 17, 25),
            BorderBrush = BrushFrom(38, 50, 67),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 18, 0, 0),
            Effect = new DropShadowEffect
            {
                BlurRadius = 10,
                ShadowDepth = 0,
                Opacity = 0.10,
                Color = Color.FromRgb(2, 6, 11)
            }
        };
        block.Child = new StackPanel
        {
            Children =
            {
                new TextBlock
                {
                    Text = "REPORT PATH",
                    Foreground = BrushFrom(113, 121, 135),
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold
                },
                new TextBlock
                {
                    Text = _path,
                    Foreground = Brushes.White,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 6, 0, 0)
                }
            }
        };
        return block;
    }

    private UIElement CreateActions()
    {
        var footer = new Border
        {
            Background = BrushFrom(11, 15, 22),
            BorderBrush = BrushFrom(38, 50, 67),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(11),
            Margin = new Thickness(0, 18, 0, 0)
        };

        var actions = new DockPanel { LastChildFill = false };

        var openButton = CreateButton("Open folder", BrushFrom(28, 32, 40), BrushFrom(52, 59, 73), Colors.White);
        openButton.Click += (_, _) => OpenFolder(Path.GetDirectoryName(_path) ?? _path);
        DockPanel.SetDock(openButton, Dock.Left);
        actions.Children.Add(openButton);

        var copyButton = CreateButton("Copy path", BrushFrom(23, 58, 97), BrushFrom(47, 128, 237), Colors.White);
        copyButton.Margin = new Thickness(8, 0, 0, 0);
        copyButton.Click += (_, _) => CopyReportPath();
        DockPanel.SetDock(copyButton, Dock.Left);
        actions.Children.Add(copyButton);

        _copyFeedbackText.Text = string.Empty;
        _copyFeedbackText.Foreground = BrushFrom(87, 209, 123);
        _copyFeedbackText.FontSize = 12;
        _copyFeedbackText.FontWeight = FontWeights.SemiBold;
        _copyFeedbackText.VerticalAlignment = VerticalAlignment.Center;
        _copyFeedbackText.Margin = new Thickness(10, 0, 0, 0);
        DockPanel.SetDock(_copyFeedbackText, Dock.Left);
        actions.Children.Add(_copyFeedbackText);

        var doneButton = CreateButton("Done", BrushFrom(47, 128, 237), BrushFrom(88, 166, 255), Colors.White);
        doneButton.Click += (_, _) => Close();
        DockPanel.SetDock(doneButton, Dock.Right);
        actions.Children.Add(doneButton);

        footer.Child = actions;
        return footer;
    }

    private void CopyReportPath()
    {
        Clipboard.SetText(_path);
        _copyFeedbackText.Text = "Copied";
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
            Background = BrushFrom(20, 26, 36),
            BorderBrush = BrushFrom(47, 60, 79),
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

    private static void OpenFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private static SolidColorBrush BrushFrom(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(Color.FromRgb(red, green, blue));
    }
}
