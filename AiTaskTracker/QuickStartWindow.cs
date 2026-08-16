using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace AiTaskTracker;

internal sealed class QuickStartWindow : Window
{
    private readonly string _mcpCommand;
    private readonly TextBlock _copyFeedbackText = new();

    public QuickStartWindow(Window owner)
    {
        _mcpCommand = "dotnet run --project AiTaskTracker.Mcp\\AiTaskTracker.Mcp.csproj --no-build";

        Owner = owner;
        Width = 780;
        Height = 500;
        MinWidth = 760;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Title = "AI Task Tracker quick start";
        Opacity = 0;

        var root = new Border
        {
            Background = new LinearGradientBrush(
                Color.FromRgb(18, 24, 36),
                Color.FromRgb(12, 15, 21),
                new Point(0, 0),
                new Point(1, 1)),
            BorderBrush = BrushFrom(51, 59, 73),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(22),
            Effect = new DropShadowEffect
            {
                BlurRadius = 34,
                ShadowDepth = 10,
                Opacity = 0.58,
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
                Text = "\uE8FD",
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
            Text = "Quick Start",
            Foreground = Brushes.White,
            FontSize = 22,
            FontWeight = FontWeights.SemiBold
        });
        title.Children.Add(new TextBlock
        {
            Text = "A compact workflow for keeping tasks alive while user and AI agents move fast.",
            Foreground = BrushFrom(161, 168, 181),
            FontSize = 13,
            Margin = new Thickness(0, 5, 0, 0)
        });
        title.Children.Add(new Border
        {
            Background = BrushFrom(17, 41, 29),
            BorderBrush = BrushFrom(36, 92, 56),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(7, 2, 7, 2),
            Margin = new Thickness(0, 10, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = "LOCAL-FIRST / AI-READY",
                Foreground = BrushFrom(138, 227, 157),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold
            }
        });
        Grid.SetColumn(title, 1);
        header.Children.Add(title);

        var closeButton = CreateIconButton("\uE711", "Close quick start");
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
        left.Children.Add(CreateSectionTitle("Daily Flow"));
        left.Children.Add(CreateStepCard("1", "Capture in context", "Use + Add Task at the bottom of a group or board column. New tasks inherit the section status so capture stays fast.", "\uE710", BrushFrom(87, 209, 123)));
        left.Children.Add(CreateStepCard("2", "Keep Today Focus honest", "Now, Blocked, Due, and Recent surface the tasks most likely to break your working memory.", "\uE9D9", BrushFrom(88, 166, 255)));
        left.Children.Add(CreateStepCard("3", "Open Task Info only when needed", "Click the info button to slide in notes, subtasks, logs, assignee, due date, and attachments without leaving the list.", "\uE946", BrushFrom(167, 139, 250)));
        Grid.SetColumn(left, 0);
        body.Children.Add(left);

        var right = new StackPanel();
        right.Children.Add(CreateSectionTitle("AI-Friendly Workflow"));
        right.Children.Add(CreateStepCard("4", "Board for status movement", "Switch to Board when you need drag/drop movement across TO-DO, IN-PROGRESS, DONE, and CLOSE.", "\uE8FD", BrushFrom(251, 191, 36)));
        right.Children.Add(CreateStepCard("5", "Let agents update tasks", "Configure MCP stdio in your AI client so agents can create, update, log, and query tasks while they work.", "\uE8EF", BrushFrom(45, 212, 191)));
        right.Children.Add(CreateCommandCard());
        Grid.SetColumn(right, 2);
        body.Children.Add(right);

        return body;
    }

    private UIElement CreateActions()
    {
        var actions = new DockPanel { Margin = new Thickness(0, 18, 0, 0), LastChildFill = false };

        var copyMcpButton = CreateButton("Copy MCP command", BrushFrom(23, 58, 97), BrushFrom(47, 128, 237), Colors.White);
        copyMcpButton.Click += (_, _) => CopyMcpCommand();
        DockPanel.SetDock(copyMcpButton, Dock.Left);
        actions.Children.Add(copyMcpButton);

        _copyFeedbackText.Text = "";
        _copyFeedbackText.Foreground = BrushFrom(138, 227, 157);
        _copyFeedbackText.FontSize = 12;
        _copyFeedbackText.FontWeight = FontWeights.SemiBold;
        _copyFeedbackText.VerticalAlignment = VerticalAlignment.Center;
        _copyFeedbackText.Margin = new Thickness(12, 0, 0, 0);
        DockPanel.SetDock(_copyFeedbackText, Dock.Left);
        actions.Children.Add(_copyFeedbackText);

        var doneButton = CreateButton("Start tracking", BrushFrom(47, 128, 237), BrushFrom(88, 166, 255), Colors.White);
        doneButton.Click += (_, _) => Close();
        DockPanel.SetDock(doneButton, Dock.Right);
        actions.Children.Add(doneButton);

        return actions;
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

    private static Border CreateStepCard(string number, string title, string detail, string glyph, Brush accent)
    {
        var card = new Border
        {
            Background = BrushFrom(15, 18, 24),
            BorderBrush = BrushFrom(42, 47, 58),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(13, 12, 13, 12),
            Margin = new Thickness(0, 0, 0, 10)
        };

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var badge = new Border
        {
            Width = 34,
            Height = 34,
            Background = BrushFrom(17, 22, 31),
            BorderBrush = accent,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Margin = new Thickness(0, 0, 11, 0),
            Child = new Grid
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = glyph,
                        FontFamily = new FontFamily("Segoe MDL2 Assets"),
                        Foreground = accent,
                        FontSize = 15,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = number,
                        Foreground = Brushes.White,
                        FontSize = 9,
                        FontWeight = FontWeights.SemiBold,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = new Thickness(0, 0, 5, 4)
                    }
                }
            }
        };
        row.Children.Add(badge);

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = Brushes.White,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold
        });
        stack.Children.Add(new TextBlock
        {
            Text = detail,
            Foreground = BrushFrom(161, 168, 181),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        });
        Grid.SetColumn(stack, 1);
        row.Children.Add(stack);

        card.Child = row;
        return card;
    }

    private Border CreateCommandCard()
    {
        var card = new Border
        {
            Background = BrushFrom(15, 18, 24),
            BorderBrush = BrushFrom(42, 47, 58),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(13, 12, 13, 12),
            Margin = new Thickness(0, 0, 0, 10)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = "MCP STDIO COMMAND",
            Foreground = BrushFrom(113, 121, 135),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold
        });
        stack.Children.Add(new TextBlock
        {
            Text = _mcpCommand,
            Foreground = Brushes.White,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 8, 0)
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Use this when your AI client supports spawning local stdio MCP servers.",
            Foreground = BrushFrom(113, 121, 135),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 8, 0)
        });
        grid.Children.Add(stack);

        var copyButton = CreateIconButton("\uE8C8", "Copy MCP command");
        copyButton.Width = 30;
        copyButton.Height = 30;
        copyButton.Margin = new Thickness(10, 8, 0, 0);
        copyButton.Click += (_, _) => CopyMcpCommand();
        Grid.SetColumn(copyButton, 1);
        grid.Children.Add(copyButton);

        card.Child = grid;
        return card;
    }

    private static Button CreateButton(string text, Brush background, Brush border, Color foreground)
    {
        return new Button
        {
            Content = text,
            MinWidth = 92,
            Height = 34,
            Padding = new Thickness(14, 0, 14, 0),
            Background = background,
            BorderBrush = border,
            BorderThickness = new Thickness(1),
            Foreground = new SolidColorBrush(foreground),
            Cursor = Cursors.Hand,
            FontWeight = FontWeights.SemiBold,
            Template = CreateButtonTemplate()
        };
    }

    private static Button CreateIconButton(string glyph, string automationName)
    {
        return new Button
        {
            Content = glyph,
            Width = 30,
            Height = 30,
            Padding = new Thickness(0),
            Background = BrushFrom(28, 32, 40),
            BorderBrush = BrushFrom(52, 59, 73),
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
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));

        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(content);

        var template = new ControlTemplate(typeof(Button))
        {
            VisualTree = border
        };
        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, BrushFrom(37, 42, 52), "ButtonChrome"));
        hoverTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, BrushFrom(88, 166, 255), "ButtonChrome"));
        template.Triggers.Add(hoverTrigger);

        var pressedTrigger = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
        pressedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, BrushFrom(16, 40, 65), "ButtonChrome"));
        template.Triggers.Add(pressedTrigger);

        var focusTrigger = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
        focusTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, BrushFrom(125, 183, 255), "ButtonChrome"));
        template.Triggers.Add(focusTrigger);

        return template;
    }

    private void CopyMcpCommand()
    {
        Clipboard.SetText(_mcpCommand);
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

    private static SolidColorBrush BrushFrom(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(Color.FromRgb(red, green, blue));
    }
}
