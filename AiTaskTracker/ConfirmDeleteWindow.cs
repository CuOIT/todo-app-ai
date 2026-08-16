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

internal sealed class ConfirmDeleteWindow : Window
{
    public ConfirmDeleteWindow(Window owner, string taskId, string taskTitle)
    {
        Owner = owner;
        Width = 448;
        Height = 306;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Title = "Delete task";
        Opacity = 0;

        var root = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(16, 18, 24)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(51, 59, 73)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(20),
            Effect = new DropShadowEffect
            {
                BlurRadius = 34,
                ShadowDepth = 10,
                Opacity = 0.52,
                Color = Colors.Black
            }
        };

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var headingGrid = new Grid();
        headingGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headingGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var headingRow = new StackPanel { Orientation = Orientation.Horizontal };
        var warningIcon = new Border
        {
            Width = 38,
            Height = 38,
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromRgb(69, 22, 28)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(127, 29, 29)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 12, 0),
            Child = new TextBlock
            {
                Text = "\uE74D",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113)),
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        var heading = new TextBlock
        {
            Text = "Delete task?",
            Foreground = Brushes.White,
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        headingRow.Children.Add(warningIcon);
        headingRow.Children.Add(heading);

        var closeButton = CreateIconButton("\uE711", "Keep task");
        closeButton.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };
        Grid.SetColumn(closeButton, 1);

        headingGrid.Children.Add(headingRow);
        headingGrid.Children.Add(closeButton);
        layout.Children.Add(headingGrid);

        var description = new TextBlock
        {
            Text = "The task will leave active views, while its audit history stays available for traceability.",
            Foreground = new SolidColorBrush(Color.FromRgb(161, 168, 181)),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 12, 0, 16)
        };
        Grid.SetRow(description, 1);
        layout.Children.Add(description);

        var taskSummary = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(13, 17, 24)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(40, 52, 69)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10, 12, 10)
        };

        var taskStack = new StackPanel();
        taskStack.Children.Add(new TextBlock
        {
            Text = "SELECTED TASK",
            Foreground = new SolidColorBrush(Color.FromRgb(113, 121, 135)),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 6)
        });
        taskStack.Children.Add(new TextBlock
        {
            Text = taskTitle,
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        taskStack.Children.Add(new TextBlock
        {
            Text = taskId,
            Foreground = new SolidColorBrush(Color.FromRgb(113, 121, 135)),
            FontSize = 12,
            Margin = new Thickness(0, 4, 0, 0)
        });
        taskSummary.Child = taskStack;
        Grid.SetRow(taskSummary, 2);
        layout.Children.Add(taskSummary);

        var helperText = new TextBlock
        {
            Text = "This is a soft delete. Press Esc or close this dialog to keep the task.",
            Foreground = new SolidColorBrush(Color.FromRgb(113, 121, 135)),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 12, 0, 0)
        };
        Grid.SetRow(helperText, 3);
        layout.Children.Add(helperText);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };

        var cancelButton = CreateButton(
            "Keep task",
            Color.FromRgb(28, 32, 40),
            Color.FromRgb(52, 59, 73),
            Colors.White,
            Color.FromRgb(37, 42, 52),
            Color.FromRgb(75, 85, 104),
            Color.FromRgb(20, 24, 31));
        cancelButton.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };

        var deleteButton = CreateButton(
            "Delete task",
            Color.FromRgb(185, 28, 28),
            Color.FromRgb(248, 113, 113),
            Colors.White,
            Color.FromRgb(220, 38, 38),
            Color.FromRgb(252, 165, 165),
            Color.FromRgb(127, 29, 29));
        deleteButton.Margin = new Thickness(8, 0, 0, 0);
        deleteButton.Click += (_, _) =>
        {
            DialogResult = true;
            Close();
        };

        actions.Children.Add(cancelButton);
        actions.Children.Add(deleteButton);
        Grid.SetRow(actions, 4);
        layout.Children.Add(actions);

        root.Child = layout;
        Content = root;

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        };

        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        };

        Loaded += (_, _) =>
        {
            cancelButton.Focus();
            BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(120),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        };
    }

    private static Button CreateButton(
        string text,
        Color background,
        Color border,
        Color foreground,
        Color hoverBackground,
        Color hoverBorder,
        Color pressedBackground)
    {
        return new Button
        {
            Content = text,
            MinWidth = 82,
            Height = 34,
            Padding = new Thickness(14, 0, 14, 0),
            Background = new SolidColorBrush(background),
            BorderBrush = new SolidColorBrush(border),
            BorderThickness = new Thickness(1),
            Foreground = new SolidColorBrush(foreground),
            Cursor = Cursors.Hand,
            FontWeight = FontWeights.SemiBold,
            Template = CreateButtonTemplate(hoverBackground, hoverBorder, pressedBackground)
        };
    }

    private static Button CreateIconButton(string icon, string toolTip)
    {
        return new Button
        {
            Content = icon,
            ToolTip = toolTip,
            Width = 32,
            Height = 32,
            MinHeight = 32,
            Padding = new Thickness(0),
            Margin = new Thickness(10, 0, 0, 0),
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 11,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(1),
            Foreground = new SolidColorBrush(Color.FromRgb(170, 178, 192)),
            Cursor = Cursors.Hand,
            Template = CreateButtonTemplate(
                Color.FromRgb(37, 42, 52),
                Color.FromRgb(75, 85, 104),
                Color.FromRgb(47, 54, 68),
                7)
        };
    }

    private static ControlTemplate CreateButtonTemplate(
        Color hoverBackground,
        Color hoverBorder,
        Color pressedBackground,
        double cornerRadius = 6)
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
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(cornerRadius));

        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(content);

        var template = new ControlTemplate(typeof(Button))
        {
            VisualTree = border
        };

        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(hoverBackground), "ButtonChrome"));
        hoverTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(hoverBorder), "ButtonChrome"));
        template.Triggers.Add(hoverTrigger);

        var pressedTrigger = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
        pressedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(pressedBackground), "ButtonChrome"));
        template.Triggers.Add(pressedTrigger);

        var focusTrigger = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
        focusTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(88, 166, 255)), "ButtonChrome"));
        template.Triggers.Add(focusTrigger);

        return template;
    }
}
