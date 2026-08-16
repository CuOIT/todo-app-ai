using System;
using System.IO;
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

internal sealed class DiagnosticsWindow : Window
{
    private readonly string _dataDirectory;
    private readonly string _mcpCommand;
    private readonly string _operationsPath;
    private readonly string _report;
    private readonly string _snapshotPath;
    private readonly TextBlock _copyFeedbackText = new();

    public DiagnosticsWindow(Window owner, string dataDirectory, string snapshotPath, string operationsPath)
    {
        _dataDirectory = dataDirectory;
        _snapshotPath = snapshotPath;
        _operationsPath = operationsPath;
        _mcpCommand = "dotnet run --project AiTaskTracker.Mcp\\AiTaskTracker.Mcp.csproj --no-build";
        _report = BuildReport();

        Owner = owner;
        Width = 680;
        Height = 528;
        MinWidth = 660;
        MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Title = "AI Task Tracker diagnostics";
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

        var body = CreateBody();
        Grid.SetRow(body, 1);
        layout.Children.Add(body);

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
        Loaded += (_, _) => BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(130),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
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
            Text = "Diagnostics",
            Foreground = Brushes.White,
            FontSize = 22,
            FontWeight = FontWeights.SemiBold
        });
        title.Children.Add(new TextBlock
        {
            Text = "Storage, audit log, package, and MCP readiness for troubleshooting.",
            Foreground = BrushFrom(161, 168, 181),
            FontSize = 13,
            Margin = new Thickness(0, 5, 0, 0)
        });
        title.Children.Add(new WrapPanel
        {
            Margin = new Thickness(0, 11, 0, 0),
            Children =
            {
                CreateMiniPill("Storage checks", BrushFrom(87, 209, 123)),
                CreateMiniPill("Runtime report", BrushFrom(88, 166, 255)),
                CreateMiniPill("MCP ready", BrushFrom(167, 139, 250))
            }
        });
        Grid.SetColumn(title, 1);
        header.Children.Add(title);

        var closeButton = CreateIconButton("\uE711", "Close diagnostics");
        closeButton.Click += (_, _) => Close();
        Grid.SetColumn(closeButton, 2);
        header.Children.Add(closeButton);

        return header;
    }

    private UIElement CreateBody()
    {
        var body = new Grid { Margin = new Thickness(0, 18, 0, 0) };
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var left = new StackPanel();
        left.Children.Add(CreateSectionTitle("Health Checks"));
        left.Children.Add(CreateHealthCard("Data directory", Directory.Exists(_dataDirectory), _dataDirectory));
        left.Children.Add(CreateHealthCard("Snapshot JSON", File.Exists(_snapshotPath), DescribeFile(_snapshotPath)));
        left.Children.Add(CreateHealthCard("Operation log", File.Exists(_operationsPath), DescribeFile(_operationsPath)));
        Grid.SetColumn(left, 0);
        body.Children.Add(left);

        var right = new StackPanel();
        right.Children.Add(CreateSectionTitle("Runtime"));
        right.Children.Add(CreateValueCard("App folder", System.AppContext.BaseDirectory));
        right.Children.Add(CreateValueCard(".NET runtime", Environment.Version.ToString()));
        right.Children.Add(CreateValueCard("MCP command", _mcpCommand));
        Grid.SetColumn(right, 2);
        body.Children.Add(right);

        return body;
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

        var copyReportButton = CreateButton("Copy diagnostic report", BrushFrom(23, 58, 97), BrushFrom(47, 128, 237), Colors.White);
        copyReportButton.Click += (_, _) => CopyReport();
        DockPanel.SetDock(copyReportButton, Dock.Left);
        actions.Children.Add(copyReportButton);

        _copyFeedbackText.Text = "";
        _copyFeedbackText.Foreground = BrushFrom(138, 227, 157);
        _copyFeedbackText.FontSize = 12;
        _copyFeedbackText.FontWeight = FontWeights.SemiBold;
        _copyFeedbackText.VerticalAlignment = VerticalAlignment.Center;
        _copyFeedbackText.Margin = new Thickness(12, 0, 0, 0);
        DockPanel.SetDock(_copyFeedbackText, Dock.Left);
        actions.Children.Add(_copyFeedbackText);

        var doneButton = CreateButton("Done", BrushFrom(47, 128, 237), BrushFrom(88, 166, 255), Colors.White);
        doneButton.Click += (_, _) => Close();
        DockPanel.SetDock(doneButton, Dock.Right);
        actions.Children.Add(doneButton);

        footer.Child = actions;
        return footer;
    }

    private Border CreateHealthCard(string title, bool isHealthy, string detail)
    {
        var accent = isHealthy ? BrushFrom(87, 209, 123) : BrushFrom(255, 107, 107);
        var card = new Border
        {
            Background = BrushFrom(12, 17, 25),
            BorderBrush = isHealthy ? BrushFrom(38, 50, 67) : BrushFrom(100, 40, 47),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 0, 0, 10),
            Effect = new DropShadowEffect
            {
                BlurRadius = 10,
                ShadowDepth = 0,
                Opacity = 0.10,
                Color = Color.FromRgb(2, 6, 11)
            }
        };
        card.Child = new DockPanel
        {
            Children =
            {
                new TextBlock
                {
                    Text = isHealthy ? "\uE930" : "\uE783",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    Foreground = accent,
                    FontSize = 15,
                    Margin = new Thickness(0, 2, 10, 0)
                },
                new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = title, Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.SemiBold },
                        new TextBlock { Text = isHealthy ? "Ready" : "Needs attention", Foreground = accent, FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 3, 0, 0) },
                        new TextBlock { Text = detail, Foreground = BrushFrom(161, 168, 181), FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 0) }
                    }
                }
            }
        };
        return card;
    }

    private static Border CreateValueCard(string title, string value)
    {
        var card = new Border
        {
            Background = BrushFrom(12, 17, 25),
            BorderBrush = BrushFrom(38, 50, 67),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 0, 0, 10),
            Effect = new DropShadowEffect
            {
                BlurRadius = 10,
                ShadowDepth = 0,
                Opacity = 0.10,
                Color = Color.FromRgb(2, 6, 11)
            }
        };
        card.Child = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = title.ToUpperInvariant(), Foreground = BrushFrom(113, 121, 135), FontSize = 10, FontWeight = FontWeights.SemiBold },
                new TextBlock { Text = value, Foreground = Brushes.White, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 0) }
            }
        };
        return card;
    }

    private string BuildReport()
    {
        var report = new StringBuilder();
        report.AppendLine("AI Task Tracker diagnostics");
        report.AppendLine($"Generated: {DateTimeOffset.Now:O}");
        report.AppendLine($"Data directory: {_dataDirectory}");
        report.AppendLine($"Data directory exists: {Directory.Exists(_dataDirectory)}");
        report.AppendLine($"Snapshot: {DescribeFile(_snapshotPath)}");
        report.AppendLine($"Operation log: {DescribeFile(_operationsPath)}");
        report.AppendLine($"App folder: {System.AppContext.BaseDirectory}");
        report.AppendLine($".NET runtime: {Environment.Version}");
        report.AppendLine($"MCP command: {_mcpCommand}");
        return report.ToString();
    }

    private static string DescribeFile(string path)
    {
        if (!File.Exists(path))
        {
            return $"{path} (missing)";
        }

        var info = new FileInfo(path);
        return $"{path} ({info.Length:N0} bytes, modified {info.LastWriteTime:yyyy-MM-dd HH:mm})";
    }

    private static TextBlock CreateSectionTitle(string text)
    {
        return new TextBlock
        {
            Text = text.ToUpperInvariant(),
            Foreground = BrushFrom(161, 168, 181),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        };
    }

    private static Border CreateMiniPill(string text, Brush accent)
    {
        return new Border
        {
            Background = BrushFrom(11, 18, 28),
            BorderBrush = accent,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 6, 6),
            Child = new TextBlock
            {
                Text = text,
                Foreground = accent,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold
            }
        };
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

    private static SolidColorBrush BrushFrom(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(Color.FromRgb(red, green, blue));
    }

    private void CopyReport()
    {
        Clipboard.SetText(_report);
        _copyFeedbackText.Text = "Copied";
        _copyFeedbackText.BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(120),
            AutoReverse = true,
            BeginTime = TimeSpan.FromSeconds(1.1)
        });
    }
}
