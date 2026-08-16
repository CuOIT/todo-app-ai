using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using AiTaskTracker.Services;

namespace AiTaskTracker;

internal sealed class SettingsWindow : Window
{
    private readonly string _dataDirectory;
    private readonly string _mcpCommand;
    private readonly string _operationsPath;
    private readonly string _snapshotPath;
    private readonly LicenseStateStore _licenseStore;
    private readonly TextBlock _footerFeedbackText = new();
    private LicenseState _licenseState;

    public SettingsWindow(Window owner, string dataDirectory, string snapshotPath, string operationsPath, bool isPinned)
    {
        _dataDirectory = dataDirectory;
        _snapshotPath = snapshotPath;
        _operationsPath = operationsPath;
        _mcpCommand = "dotnet run --project AiTaskTracker.Mcp\\AiTaskTracker.Mcp.csproj --no-build";
        _licenseStore = new LicenseStateStore(dataDirectory);
        _licenseState = _licenseStore.Load();

        Owner = owner;
        Width = 980;
        Height = 700;
        MinWidth = 920;
        MinHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Title = "AI Task Tracker settings";
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
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = CreateHeader();
        Grid.SetRow(header, 0);
        layout.Children.Add(header);

        var scroll = new ScrollViewer
        {
            Margin = new Thickness(0, 18, 0, 0),
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = CreateSettingsContent(isPinned)
        };
        Grid.SetRow(scroll, 1);
        layout.Children.Add(scroll);

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
                Text = "\uE713",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 23,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        header.Children.Add(icon);

        var title = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        title.Children.Add(new TextBlock
        {
            Text = "Settings",
            Foreground = Brushes.White,
            FontSize = 22,
            FontWeight = FontWeights.SemiBold
        });
        title.Children.Add(new TextBlock
        {
            Text = "Workspace, AI agent access, app behavior, and purchase readiness.",
            Foreground = BrushFrom(161, 168, 181),
            FontSize = 13,
            Margin = new Thickness(0, 5, 0, 0)
        });
        title.Children.Add(new WrapPanel
        {
            Margin = new Thickness(0, 11, 0, 0),
            Children =
            {
                CreateMiniPill("Local workspace", BrushFrom(87, 209, 123)),
                CreateMiniPill("MCP stdio ready", BrushFrom(88, 166, 255)),
                CreateMiniPill($"{_licenseState.Edition} / {_licenseState.Status}", BrushFrom(167, 139, 250))
            }
        });
        Grid.SetColumn(title, 1);
        header.Children.Add(title);

        var closeButton = CreateIconButton("\uE711", "Close settings");
        closeButton.Click += (_, _) => Close();
        Grid.SetColumn(closeButton, 2);
        header.Children.Add(closeButton);

        return header;
    }

    private UIElement CreateSettingsContent(bool isPinned)
    {
        var content = new Grid
        {
            Margin = new Thickness(0, 0, 0, 18)
        };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var left = new StackPanel();
        left.Children.Add(CreateSectionTitle("Workspace"));
        left.Children.Add(CreatePathBlock("Data directory", _dataDirectory, "Stores snapshot, operation log, and local preferences."));
        left.Children.Add(CreatePathBlock("Snapshot JSON", _snapshotPath, "Current local task database used by desktop and MCP."));
        left.Children.Add(CreatePathBlock("Operation log", _operationsPath, "Audit trail for user and AI updates."));

        left.Children.Add(CreateSectionTitle("AI Agent Access"));
        left.Children.Add(CreateCommandBlock("MCP stdio command", _mcpCommand, "Use this in AI clients that can spawn a local stdio MCP server."));
        left.Children.Add(CreateFeatureGrid(
            ("Create/update tasks", "AI can create, edit, log, and update task status.", "\uE710", BrushFrom(87, 209, 123)),
            ("Local-first", "No cloud dependency for MVP tracking.", "\uE753", BrushFrom(88, 166, 255)),
            ("Audit visible", "Agent actions are recorded in the operation log.", "\uE9D5", BrushFrom(251, 191, 36))));
        Grid.SetColumn(left, 0);
        content.Children.Add(left);

        var right = new StackPanel();
        right.Children.Add(CreateSectionTitle("App Behavior"));
        right.Children.Add(CreateToggleCard("Keep window on top", isPinned, "Current session preference. Use the pin button in the header to change it."));
        right.Children.Add(CreateReadOnlySetting("Theme", "Dark workspace", "Optimized for dense task scanning and long AI sessions."));
        right.Children.Add(CreateReadOnlySetting("Storage mode", "Local JSON", "Prepared for future offline-first server sync."));

        right.Children.Add(CreateSectionTitle("Billing And IAP Readiness"));
        right.Children.Add(CreateBillingPanel());
        right.Children.Add(new Border { Height = 48, Background = Brushes.Transparent });
        Grid.SetColumn(right, 2);
        content.Children.Add(right);

        return content;
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

        var actions = new Grid();
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var utilities = new WrapPanel
        {
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(utilities, 0);
        actions.Children.Add(utilities);

        var openDataButton = CreateButton("Open data folder", BrushFrom(28, 32, 40), BrushFrom(52, 59, 73), Colors.White);
        openDataButton.Click += (_, _) => OpenFolder(_dataDirectory);
        utilities.Children.Add(openDataButton);

        var copyMcpButton = CreateButton("Copy MCP command", BrushFrom(23, 58, 97), BrushFrom(47, 128, 237), Colors.White);
        copyMcpButton.Margin = new Thickness(8, 0, 0, 0);
        copyMcpButton.Click += (_, _) => CopyMcpCommand();
        utilities.Children.Add(copyMcpButton);

        _footerFeedbackText.Text = "";
        _footerFeedbackText.Foreground = BrushFrom(138, 227, 157);
        _footerFeedbackText.FontSize = 12;
        _footerFeedbackText.FontWeight = FontWeights.SemiBold;
        _footerFeedbackText.VerticalAlignment = VerticalAlignment.Center;
        _footerFeedbackText.Margin = new Thickness(10, 0, 0, 0);
        utilities.Children.Add(_footerFeedbackText);

        var diagnosticsButton = CreateButton("Run diagnostics", BrushFrom(28, 32, 40), BrushFrom(52, 59, 73), Colors.White);
        diagnosticsButton.Margin = new Thickness(8, 0, 0, 0);
        diagnosticsButton.Click += (_, _) =>
        {
            var diagnosticsWindow = new DiagnosticsWindow(this, _dataDirectory, _snapshotPath, _operationsPath);
            diagnosticsWindow.ShowDialog();
        };
        utilities.Children.Add(diagnosticsButton);

        var backupButton = CreateButton("Export backup", BrushFrom(28, 32, 40), BrushFrom(52, 59, 73), Colors.White);
        backupButton.Margin = new Thickness(8, 0, 0, 0);
        backupButton.Click += (_, _) =>
        {
            var resultWindow = new BackupResultWindow(this, CreateBackupArchive());
            resultWindow.ShowDialog();
        };
        utilities.Children.Add(backupButton);

        var licenseReportButton = CreateButton("Export license report", BrushFrom(28, 32, 40), BrushFrom(52, 59, 73), Colors.White);
        licenseReportButton.Margin = new Thickness(8, 0, 0, 0);
        licenseReportButton.Click += (_, _) =>
        {
            var reportPath = _licenseStore.ExportReadinessReport(_licenseState);
            var resultWindow = new ReportResultWindow(this, "License report exported", "The local entitlement and IAP readiness contract were written to disk.", reportPath);
            resultWindow.ShowDialog();
        };
        utilities.Children.Add(licenseReportButton);

        var restoreEntitlementButton = CreateButton("Restore entitlement", BrushFrom(23, 58, 97), BrushFrom(47, 128, 237), Colors.White);
        restoreEntitlementButton.Margin = new Thickness(8, 0, 0, 0);
        restoreEntitlementButton.Click += (_, _) =>
        {
            _licenseState = _licenseStore.RestorePurchases();
            var reportPath = _licenseStore.ExportReadinessReport(_licenseState);
            var resultWindow = new ReportResultWindow(this, "Entitlement restored", "The local purchase restore contract completed and refreshed the readiness report.", reportPath);
            resultWindow.ShowDialog();
        };
        utilities.Children.Add(restoreEntitlementButton);

        var doneButton = CreateButton("Done", BrushFrom(47, 128, 237), BrushFrom(88, 166, 255), Colors.White);
        doneButton.MinWidth = 96;
        doneButton.Margin = new Thickness(12, 0, 0, 0);
        doneButton.Click += (_, _) => Close();
        Grid.SetColumn(doneButton, 1);
        actions.Children.Add(doneButton);

        footer.Child = actions;
        return footer;
    }

    private string CreateBackupArchive()
    {
        var backupDirectory = Path.Combine(_dataDirectory, "backups");
        Directory.CreateDirectory(backupDirectory);

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var backupPath = Path.Combine(backupDirectory, $"AiTaskTracker-backup-{timestamp}.zip");

        using var archive = ZipFile.Open(backupPath, ZipArchiveMode.Create);
        AddFileIfExists(archive, _snapshotPath, "snapshot.json");
        AddFileIfExists(archive, _operationsPath, "operations.jsonl");

        var preferencesPath = Path.Combine(_dataDirectory, "ui-preferences.json");
        AddFileIfExists(archive, preferencesPath, "ui-preferences.json");

        var licensePath = Path.Combine(_dataDirectory, "license-state.json");
        AddFileIfExists(archive, licensePath, "license-state.json");

        return backupPath;
    }

    private static void AddFileIfExists(ZipArchive archive, string sourcePath, string entryName)
    {
        if (File.Exists(sourcePath))
        {
            archive.CreateEntryFromFile(sourcePath, entryName, CompressionLevel.Optimal);
        }
    }

    private static UniformGrid CreateFeatureGrid(params (string title, string detail, string glyph, Brush accent)[] items)
    {
        var grid = new UniformGrid
        {
            Columns = 1,
            Margin = new Thickness(0, 0, 0, 8)
        };
        foreach (var item in items)
        {
            grid.Children.Add(CreateFeatureCard(item.title, item.detail, item.glyph, item.accent));
        }

        return grid;
    }

    private static Border CreateFeatureCard(string title, string detail, string glyph, Brush accent)
    {
        var card = new Border
        {
            Background = BrushFrom(12, 17, 25),
            BorderBrush = BrushFrom(38, 50, 67),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(13, 11, 13, 11),
            Margin = new Thickness(0, 0, 0, 8),
            Effect = new DropShadowEffect
            {
                BlurRadius = 10,
                ShadowDepth = 0,
                Opacity = 0.12,
                Color = Color.FromRgb(2, 6, 11)
            }
        };
        card.Child = new DockPanel
        {
            Children =
            {
                new TextBlock
                {
                    Text = glyph,
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    Foreground = accent,
                    FontSize = 15,
                    Margin = new Thickness(0, 1, 9, 0)
                },
                new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = title, Foreground = Brushes.White, FontSize = 12, FontWeight = FontWeights.SemiBold },
                        new TextBlock { Text = detail, Foreground = BrushFrom(113, 121, 135), FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) }
                    }
                }
            }
        };
        return card;
    }

    private Border CreateBillingPanel()
    {
        var card = new Border
        {
            Background = new LinearGradientBrush(
                Color.FromRgb(15, 21, 31),
                Color.FromRgb(10, 14, 21),
                90),
            BorderBrush = BrushFrom(45, 58, 79),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(15, 14, 15, 14),
            Margin = new Thickness(0, 0, 0, 8),
            Effect = new DropShadowEffect
            {
                BlurRadius = 14,
                ShadowDepth = 0,
                Opacity = 0.14,
                Color = Color.FromRgb(2, 6, 11)
            }
        };

        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel();
        text.Children.Add(new TextBlock
        {
            Text = _licenseState.Edition,
            Foreground = Brushes.White,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold
        });
        text.Children.Add(new TextBlock
        {
            Text = "Local entitlement is active for this device. Store purchase stays locked until a signed package or server-backed entitlement provider is connected.",
            Foreground = BrushFrom(161, 168, 181),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 16, 0)
        });
        text.Children.Add(new WrapPanel
        {
            Margin = new Thickness(0, 10, 0, 0),
            Children =
            {
                CreateMiniPill($"Status: {_licenseState.Status}", BrushFrom(87, 209, 123)),
                CreateMiniPill($"Source: {_licenseState.EntitlementSource}", BrushFrom(88, 166, 255)),
                CreateMiniPill($"Provider: {_licenseState.EntitlementProvider}", BrushFrom(167, 139, 250)),
                CreateMiniPill(_licenseState.PurchaseRestoreReady ? "Restore ready" : "Restore pending", _licenseState.PurchaseRestoreReady ? BrushFrom(87, 209, 123) : BrushFrom(251, 191, 36)),
                CreateMiniPill("Store signing pending", BrushFrom(251, 191, 36))
            }
        });
        text.Children.Add(new UniformGrid
        {
            Columns = 2,
            Margin = new Thickness(0, 12, 16, 0),
            Children =
            {
                CreateLicenseMetric("Machine", _licenseState.MachineFingerprint),
                CreateLicenseMetric("Product", _licenseState.StoreProductId),
                CreateLicenseMetric("Commercial", _licenseState.CommercialUseEnabled ? "Enabled" : "Pending"),
                CreateLicenseMetric("Restore", _licenseState.PurchaseRestoreReady ? "Ready" : "Pending")
            }
        });
        layout.Children.Add(text);

        var storeState = new Border
        {
            Width = 170,
            Background = BrushFrom(40, 33, 20),
            BorderBrush = BrushFrom(96, 72, 25),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = "\uE719",
                        FontFamily = new FontFamily("Segoe MDL2 Assets"),
                        Foreground = BrushFrom(251, 191, 36),
                        FontSize = 18,
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = "Store purchase locked",
                        Foreground = BrushFrom(255, 226, 168),
                        FontSize = 12,
                        FontWeight = FontWeights.SemiBold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        TextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 8, 0, 0)
                    },
                    new TextBlock
                    {
                        Text = "Ready for wiring, not live billing.",
                        Foreground = BrushFrom(199, 181, 132),
                        FontSize = 11,
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 6, 0, 0)
                    }
                }
            }
        };
        Grid.SetColumn(storeState, 1);
        layout.Children.Add(storeState);

        card.Child = layout;
        return card;
    }

    private static Border CreateLicenseMetric(string label, string value)
    {
        return new Border
        {
            Background = BrushFrom(10, 16, 24),
            BorderBrush = BrushFrom(35, 47, 64),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 8, 8),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = label.ToUpperInvariant(),
                        Foreground = BrushFrom(113, 121, 135),
                        FontSize = 9,
                        FontWeight = FontWeights.SemiBold
                    },
                    new TextBlock
                    {
                        Text = value,
                        Foreground = Brushes.White,
                        FontSize = 11,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 4, 0, 0)
                    }
                }
            }
        };
    }

    private static Border CreatePathBlock(string label, string value, string detail)
    {
        return CreateValueBlock(label, value, detail, canCopy: true);
    }

    private static Border CreateCommandBlock(string label, string value, string detail)
    {
        return CreateValueBlock(label, value, detail, canCopy: true);
    }

    private static Border CreateReadOnlySetting(string label, string value, string detail)
    {
        return CreateValueBlock(label, value, detail, canCopy: false);
    }

    private static Border CreateToggleCard(string label, bool isOn, string detail)
    {
        var block = CreateValueBlock(label, isOn ? "On" : "Off", detail, canCopy: false);
        block.BorderBrush = isOn ? BrushFrom(47, 128, 237) : BrushFrom(42, 47, 58);
        return block;
    }

    private static Border CreateValueBlock(string label, string value, string detail, bool canCopy)
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

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = label.ToUpperInvariant(),
            Foreground = BrushFrom(113, 121, 135),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold
        });
        stack.Children.Add(new TextBlock
        {
            Text = value,
            Foreground = Brushes.White,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 8, 0)
        });
        stack.Children.Add(new TextBlock
        {
            Text = detail,
            Foreground = BrushFrom(113, 121, 135),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 8, 0)
        });
        grid.Children.Add(stack);

        if (canCopy)
        {
            var copyButton = CreateIconButton("\uE8C8", $"Copy {label}");
            copyButton.Width = 28;
            copyButton.Height = 28;
            copyButton.Margin = new Thickness(10, 9, 0, 0);
            copyButton.Click += (_, _) => Clipboard.SetText(value);
            Grid.SetColumn(copyButton, 1);
            grid.Children.Add(copyButton);
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

    private static TextBlock CreateSectionTitle(string text)
    {
        return new TextBlock
        {
            Text = text.ToUpperInvariant(),
            Foreground = BrushFrom(161, 168, 181),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 12, 0, 8)
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

    private void CopyMcpCommand()
    {
        Clipboard.SetText(_mcpCommand);
        _footerFeedbackText.Text = "Copied";
        _footerFeedbackText.BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(120),
            AutoReverse = true,
            BeginTime = TimeSpan.FromSeconds(1.1)
        });
    }

    private static SolidColorBrush BrushFrom(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(Color.FromRgb(red, green, blue));
    }
}
