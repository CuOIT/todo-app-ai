using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace AiTaskTracker;

internal sealed class AboutReleaseWindow : Window
{
    private readonly string _dataDirectory;
    private readonly string _mcpCommand;
    private readonly string _operationsPath;
    private readonly string _releaseDirectory;
    private readonly string _repoRoot;
    private readonly string _snapshotPath;
    private readonly TextBlock _copyFeedbackText = new();

    public AboutReleaseWindow(Window owner, string dataDirectory, string snapshotPath, string operationsPath)
    {
        _dataDirectory = dataDirectory;
        _snapshotPath = snapshotPath;
        _operationsPath = operationsPath;
        _releaseDirectory = System.AppContext.BaseDirectory;
        _repoRoot = ResolveRepoRoot(_releaseDirectory);
        _mcpCommand = "dotnet run --project AiTaskTracker.Mcp\\AiTaskTracker.Mcp.csproj --no-build";

        Owner = owner;
        Width = 760;
        Height = 720;
        MinWidth = 720;
        MinHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Title = "AI Task Tracker release";
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
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = AddHeader();
        Grid.SetRow(header, 0);
        layout.Children.Add(header);

        var statusRail = new UniformGrid
        {
            Columns = 3,
            Margin = new Thickness(0, 18, 0, 14)
        };
        statusRail.Children.Add(CreateStatusChip("Version", VersionText(), BrushFrom(88, 166, 255)));
        statusRail.Children.Add(CreateStatusChip("Storage", "Local-first", BrushFrom(87, 209, 123)));
        statusRail.Children.Add(CreateStatusChip("Package", PackageStatusText(), PackageStatusBrush()));
        Grid.SetRow(statusRail, 1);
        layout.Children.Add(statusRail);

        var readiness = new UniformGrid
        {
            Columns = 3,
            Margin = new Thickness(0, 0, 0, 14)
        };
        readiness.Children.Add(CreateReadinessCard("Desktop", "Signed-dev local verification", "\uE930", BrushFrom(87, 209, 123)));
        readiness.Children.Add(CreateReadinessCard("MCP", "Local stdio server", "\uE8EF", BrushFrom(88, 166, 255)));
        readiness.Children.Add(CreateReadinessCard("Store", "Production trust pending", "\uE7BF", BrushFrom(251, 191, 36)));
        Grid.SetRow(readiness, 2);
        layout.Children.Add(readiness);

        var content = new StackPanel { Margin = new Thickness(0, 0, 4, 0) };
        content.Children.Add(CreateSectionTitle("Release Paths"));
        content.Children.Add(CreateInfoBlock("Data directory", _dataDirectory, canCopy: true));
        content.Children.Add(CreateInfoBlock("Portable app folder", _releaseDirectory, canCopy: true));
        content.Children.Add(CreateInfoBlock("Snapshot JSON", _snapshotPath, canCopy: true));
        content.Children.Add(CreateInfoBlock("Operation log", _operationsPath, canCopy: true));
        content.Children.Add(CreateSectionTitle("Release Center"));
        content.Children.Add(CreateInfoBlock("Distribution package", LatestArtifactPath("artifacts\\distribution", "AiTaskTracker-*-distribution-*.zip"), canCopy: true));
        content.Children.Add(CreateInfoBlock("Product readiness report", LatestArtifactPath("artifacts\\product-readiness", "product-readiness-*.md"), canCopy: true));
        content.Children.Add(CreateInfoBlock("Release readiness report", LatestArtifactPath("artifacts\\release-readiness", "release-readiness-*.md"), canCopy: true));
        content.Children.Add(CreateInfoBlock("Signing handoff", LatestArtifactPath("artifacts\\signing", "signing-handoff-*.md"), canCopy: true));
        content.Children.Add(CreateInfoBlock("Store assets", Path.Combine(_repoRoot, "artifacts\\store-assets"), canCopy: true));
        content.Children.Add(CreateSectionTitle("AI Agent Setup"));
        content.Children.Add(CreateInfoBlock("MCP stdio command", _mcpCommand, canCopy: true));
        content.Children.Add(CreateInfoBlock("Distribution note", "Signed-dev artifacts, distribution package, store assets, and readiness reports are available. Production release still needs a trusted signing chain or store-managed signing.", canCopy: false));

        var contentScroll = new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        ApplyDarkScrollBars(contentScroll);
        Grid.SetRow(contentScroll, 3);
        layout.Children.Add(contentScroll);

        var actions = AddActions();
        Grid.SetRow(actions, 4);
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

    private UIElement AddHeader()
    {
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var logo = new Border
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
                Text = "\uE73E",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 23,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Grid.SetColumn(logo, 0);
        header.Children.Add(logo);

        var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titleStack.Children.Add(new TextBlock
        {
            Text = "AI Task Tracker",
            Foreground = Brushes.White,
            FontSize = 22,
            FontWeight = FontWeights.SemiBold
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = "Local-first task tracking for people and AI agents.",
            Foreground = BrushFrom(161, 168, 181),
            FontSize = 13,
            Margin = new Thickness(0, 5, 0, 0)
        });
        titleStack.Children.Add(new WrapPanel
        {
            Margin = new Thickness(0, 11, 0, 0),
            Children =
            {
                CreateMiniPill("Release center", BrushFrom(88, 166, 255)),
                CreateMiniPill("Local-first", BrushFrom(87, 209, 123)),
                CreateMiniPill(PackageStatusText(), PackageStatusBrush())
            }
        });
        Grid.SetColumn(titleStack, 1);
        header.Children.Add(titleStack);

        var closeButton = CreateIconButton("\uE711", "Close");
        closeButton.Click += (_, _) => Close();
        Grid.SetColumn(closeButton, 2);
        header.Children.Add(closeButton);

        return header;
    }

    private UIElement AddActions()
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

        var openDataButton = CreateButton("Open data", BrushFrom(28, 32, 40), BrushFrom(52, 59, 73), Colors.White);
        openDataButton.Click += (_, _) => OpenFolder(_dataDirectory);
        DockPanel.SetDock(openDataButton, Dock.Left);
        actions.Children.Add(openDataButton);

        var openReleaseButton = CreateButton("Open app folder", BrushFrom(28, 32, 40), BrushFrom(52, 59, 73), Colors.White);
        openReleaseButton.Margin = new Thickness(8, 0, 0, 0);
        openReleaseButton.Click += (_, _) => OpenFolder(_releaseDirectory);
        DockPanel.SetDock(openReleaseButton, Dock.Left);
        actions.Children.Add(openReleaseButton);

        var copyCommandButton = CreateButton("Copy MCP command", BrushFrom(23, 58, 97), BrushFrom(47, 128, 237), Colors.White);
        copyCommandButton.Margin = new Thickness(8, 0, 0, 0);
        copyCommandButton.Click += (_, _) => CopyMcpCommand();
        DockPanel.SetDock(copyCommandButton, Dock.Left);
        actions.Children.Add(copyCommandButton);

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

    private void CopyMcpCommand()
    {
        Clipboard.SetText(_mcpCommand);
        _copyFeedbackText.Text = "Copied";
    }

    private static string VersionText()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "0.1.0";
        return $"v{version}";
    }

    private string PackageStatusText()
    {
        var manifestPath = Path.Combine(_releaseDirectory, "release-manifest.json");
        if (!File.Exists(manifestPath))
        {
            return "Local build";
        }

        var manifestText = File.ReadAllText(manifestPath);
        return manifestText.Contains("\"signed\": true", StringComparison.OrdinalIgnoreCase)
            ? "Signed-dev"
            : "Unsigned";
    }

    private Brush PackageStatusBrush()
    {
        return PackageStatusText() == "Signed-dev"
            ? BrushFrom(87, 209, 123)
            : BrushFrom(251, 191, 36);
    }

    private string LatestArtifactPath(string relativeDirectory, string filter)
    {
        var directory = Path.Combine(_repoRoot, relativeDirectory);
        if (!Directory.Exists(directory))
        {
            return $"Not generated: {directory}";
        }

        var file = Directory.GetFiles(directory, filter)
            .Select(path => new FileInfo(path))
            .OrderByDescending(fileInfo => fileInfo.LastWriteTimeUtc)
            .FirstOrDefault();
        return file?.FullName ?? $"Not generated: {directory}\\{filter}";
    }

    private static string ResolveRepoRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "artifacts")) &&
                Directory.Exists(Path.Combine(directory.FullName, "AiTaskTracker")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return startDirectory;
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

    private static TextBlock CreateSectionTitle(string text)
    {
        return new TextBlock
        {
            Text = text.ToUpperInvariant(),
            Foreground = BrushFrom(161, 168, 181),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 10, 0, 8)
        };
    }

    private static Border CreateReadinessCard(string title, string detail, string glyph, Brush accent)
    {
        var card = new Border
        {
            Background = BrushFrom(12, 17, 25),
            BorderBrush = BrushFrom(38, 50, 67),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(13, 11, 13, 11),
            Margin = new Thickness(0, 0, 8, 0),
            Effect = new DropShadowEffect
            {
                BlurRadius = 10,
                ShadowDepth = 0,
                Opacity = 0.12,
                Color = Color.FromRgb(2, 6, 11)
            }
        };

        var row = new DockPanel();
        row.Children.Add(new TextBlock
        {
            Text = glyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            Foreground = accent,
            FontSize = 16,
            Margin = new Thickness(0, 1, 9, 0)
        });
        row.Children.Add(new StackPanel
        {
            Children =
            {
                new TextBlock { Text = title, Foreground = Brushes.White, FontSize = 13, FontWeight = FontWeights.SemiBold },
                new TextBlock { Text = detail, Foreground = BrushFrom(113, 121, 135), FontSize = 11, Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap }
            }
        });
        card.Child = row;
        return card;
    }

    private static Border CreateStatusChip(string label, string value, Brush accent)
    {
        var chip = new Border
        {
            Background = BrushFrom(12, 17, 25),
            BorderBrush = BrushFrom(38, 50, 67),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(13, 10, 13, 10),
            Margin = new Thickness(0, 0, 8, 0),
            Effect = new DropShadowEffect
            {
                BlurRadius = 10,
                ShadowDepth = 0,
                Opacity = 0.10,
                Color = Color.FromRgb(2, 6, 11)
            }
        };
        chip.Child = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = label, Foreground = BrushFrom(113, 121, 135), FontSize = 11 },
                new TextBlock { Text = value, Foreground = accent, FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 3, 0, 0) }
            }
        };
        return chip;
    }

    private static Border CreateInfoBlock(string label, string value, bool canCopy)
    {
        var block = new Border
        {
            Background = BrushFrom(12, 17, 25),
            BorderBrush = BrushFrom(38, 50, 67),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 0, 0, 9),
            Effect = new DropShadowEffect
            {
                BlurRadius = 10,
                ShadowDepth = 0,
                Opacity = 0.10,
                Color = Color.FromRgb(2, 6, 11)
            }
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var textStack = new StackPanel();
        textStack.Children.Add(new TextBlock
        {
            Text = label.ToUpperInvariant(),
            Foreground = BrushFrom(113, 121, 135),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold
        });
        textStack.Children.Add(new TextBlock
        {
            Text = value,
            Foreground = Brushes.White,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 8, 0)
        });
        grid.Children.Add(textStack);

        if (canCopy)
        {
            var copyStack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var copyButton = CreateIconButton("\uE8C8", $"Copy {label}");
            copyButton.Width = 28;
            copyButton.Height = 28;
            copyButton.Margin = new Thickness(10, 7, 0, 0);
            var copiedText = new TextBlock
            {
                Text = string.Empty,
                Foreground = BrushFrom(87, 209, 123),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10, 4, 0, 0)
            };
            copyButton.Click += (_, _) =>
            {
                Clipboard.SetText(value);
                copiedText.Text = "Copied";
            };
            copyStack.Children.Add(copyButton);
            copyStack.Children.Add(copiedText);
            Grid.SetColumn(copyStack, 1);
            grid.Children.Add(copyStack);
        }

        block.Child = grid;
        return block;
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

    private static void ApplyDarkScrollBars(ScrollViewer scrollViewer)
    {
        scrollViewer.Resources.Add(SystemColors.ControlBrushKey, BrushFrom(13, 16, 22));
        scrollViewer.Resources.Add(SystemColors.ControlLightBrushKey, BrushFrom(31, 38, 50));
        scrollViewer.Resources.Add(SystemColors.ControlDarkBrushKey, BrushFrom(68, 78, 96));
        scrollViewer.Resources.Add(SystemColors.ControlDarkDarkBrushKey, BrushFrom(88, 100, 122));
        scrollViewer.Resources.Add(typeof(ScrollBar), CreateDarkScrollBarStyle());
    }

    private static Style CreateDarkScrollBarStyle()
    {
        var style = new Style(typeof(ScrollBar));
        style.Setters.Add(new Setter(WidthProperty, 10.0));
        style.Setters.Add(new Setter(MinWidthProperty, 10.0));
        style.Setters.Add(new Setter(BackgroundProperty, BrushFrom(13, 16, 22)));
        style.Setters.Add(new Setter(ForegroundProperty, BrushFrom(68, 78, 96)));
        style.Setters.Add(new Setter(BorderThicknessProperty, new Thickness(0)));
        return style;
    }

    private static SolidColorBrush BrushFrom(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(Color.FromRgb(red, green, blue));
    }
}
