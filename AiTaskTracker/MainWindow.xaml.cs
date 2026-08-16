using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AiTaskTracker.Domain;
using AiTaskTracker.Services;

namespace AiTaskTracker;

public sealed class StatusDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is string status ? TaskStatuses.ToDisplayName(status) : "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString()?.Trim().ToLowerInvariant().Replace(" ", "_") ?? TaskStatuses.Backlog;
    }
}

public sealed class PriorityDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is string priority
            ? priority.Trim().ToLowerInvariant() switch
            {
                "emergen" or "urgent" or "emergency" => "Emergency",
                "high" => "High",
                "med" or "medium" => "Medium",
                "low" => "Low",
                _ => "Medium"
            }
            : "Medium";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString()?.Trim().ToLowerInvariant() switch
        {
            "emergency" => "emergen",
            "high" => "high",
            "medium" => "med",
            "low" => "low",
            _ => "med"
        };
    }
}

public sealed class DateTimeOffsetDateConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is DateTimeOffset date ? date.LocalDateTime.Date : null;
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is DateTime date ? new DateTimeOffset(date.Date) : null;
    }
}

public sealed class AssigneeInitialsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var assignee = value?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(assignee))
        {
            return "UA";
        }

        var parts = assignee
            .Split(new[] { ' ', '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(part => char.ToUpperInvariant(part[0]));
        return string.Concat(parts);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public partial class MainWindow : Window
{
    private const string UserActorName = "User";
    private const string UserActorType = "user";
    private const double TaskInfoDrawerWidth = 390;
    private const double FocusCompactThreshold = 1180;
    private const double FocusCollapseThreshold = 1500;

    private readonly ObservableCollection<TaskItem> _tasks = new();
    private readonly ObservableCollection<TaskItem> _todoKanbanTasks = new();
    private readonly ObservableCollection<TaskItem> _inProgressKanbanTasks = new();
    private readonly ObservableCollection<TaskItem> _doneKanbanTasks = new();
    private readonly ObservableCollection<TaskItem> _closeKanbanTasks = new();
    private readonly TaskStore _store = new();
    private readonly UiPreferencesStore _preferencesStore;
    private readonly FloatingToggleWindow _floatingToggleWindow;
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(2.6) };
    private readonly Dictionary<string, Dictionary<string, object?>> _lastAuditState = new();
    private readonly ICollectionView _tasksView;

    private TaskSnapshot _snapshot = new();
    private DateTime _lastSnapshotWriteUtc = DateTime.MinValue;
    private bool _isLoading;
    private bool _isSaving;
    private bool _isApplyingTaskChange;
    private bool _isClosing;
    private bool _isUiReady;
    private bool _isTaskInfoOpen;
    private bool _isRefreshingTaskInfo;
    private bool _hasTaskInfoUnsavedChanges;
    private WindowState _windowStateBeforeMinimize = WindowState.Normal;
    private string? _openTaskInfoTaskId;
    private Grid? _activeQuickAddEditor;
    private Button? _activeQuickAddTrigger;
    private TextBox? _activeQuickAddInput;
    private Point? _kanbanDragStart;
    private DateTime? _detailDueDate;
    private DateTime _detailDueCalendarMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private Popup? _inlineDuePopup;
    private UniformGrid? _inlineDueCalendarGrid;
    private TextBlock? _inlineDueMonthText;
    private TaskItem? _inlineDueTask;
    private DateTime _inlineDueCalendarMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    public MainWindow()
    {
        InitializeComponent();

        _preferencesStore = new UiPreferencesStore(_store.DataDirectory);
        var preferences = _preferencesStore.Load();
        ApplyUiPreferences(preferences);

        _tasksView = CollectionViewSource.GetDefaultView(_tasks);
        ConfigureTasksView();
        SearchInput.TextChanged += SearchInput_TextChanged;
        StatusFilterInput.SelectionChanged += StatusFilterInput_SelectionChanged;
        StatusFilterInput.SelectedIndex = 0;
        TasksGrid.ItemsSource = _tasksView;
        TodoKanbanList.ItemsSource = _todoKanbanTasks;
        InProgressKanbanList.ItemsSource = _inProgressKanbanTasks;
        DoneKanbanList.ItemsSource = _doneKanbanTasks;
        CloseKanbanList.ItemsSource = _closeKanbanTasks;
        StoragePathText.Text = _store.DataDirectory;
        TodayDateText.Text = DateTime.Now.ToString("dddd, MMMM d", CultureInfo.CurrentCulture);

        LoadSnapshot(preserveSelection: false);

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _refreshTimer.Tick += (_, _) => RefreshFromDiskIfNeeded();
        _refreshTimer.Start();
        _toastTimer.Tick += (_, _) => HideToast();

        _floatingToggleWindow = new FloatingToggleWindow(TogglePopupVisibility);
        _floatingToggleWindow.Show();
        _floatingToggleWindow.SetPopupState(true);

        StateChanged += (_, _) =>
        {
            SyncFloatingToggleState();
            UpdateWindowChromeState();
        };
        IsVisibleChanged += (_, _) => SyncFloatingToggleState();
        SizeChanged += (_, _) => UpdateResponsivePanels();
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        Loaded += (_, _) =>
        {
            if (preferences.IsMaximized)
            {
                WindowState = WindowState.Maximized;
            }

            UpdateWindowChromeState();
        };

        ViewModeInput.SelectedIndex = preferences.ViewModeIndex == 1 ? 1 : 0;
        _isUiReady = true;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && FindParent<Button>(source) is not null)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleWindowMaximized();
            e.Handled = true;
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // The mouse can be released while Windows starts the drag operation.
            }
        }
    }

    private void MinimizeWindowButton_Click(object sender, RoutedEventArgs e)
    {
        _windowStateBeforeMinimize = WindowState == WindowState.Maximized
            ? WindowState.Maximized
            : WindowState.Normal;
        WindowState = WindowState.Minimized;
    }

    private void MaximizeWindowButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleWindowMaximized();
    }

    private void CloseWindowButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleWindowMaximized()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void UpdateWindowChromeState()
    {
        if (MaximizeWindowButton is null)
        {
            return;
        }

        var isMaximized = WindowState == WindowState.Maximized;
        MaximizeWindowButton.Content = isMaximized ? "\u2750" : "\u25A1";
        MaximizeWindowButton.ToolTip = isMaximized ? "Restore" : "Maximize";
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var controlPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

        if (controlPressed && e.Key is Key.F or Key.K)
        {
            SearchInput.Focus();
            SearchInput.SelectAll();
            e.Handled = true;
            return;
        }

        if (controlPressed && e.Key == Key.N)
        {
            var searchTextBeforeShortcut = SearchInput.Text;
            if (_activeQuickAddInput is not null)
            {
                _activeQuickAddInput.Focus();
                _activeQuickAddInput.SelectAll();
            }
            else
            {
                OpenDefaultQuickAdd();
            }

            Dispatcher.BeginInvoke(() =>
            {
                if (!string.Equals(SearchInput.Text, searchTextBeforeShortcut, StringComparison.Ordinal))
                {
                    SearchInput.Text = searchTextBeforeShortcut;
                    SearchInput.CaretIndex = SearchInput.Text.Length;
                }

                _activeQuickAddInput?.Focus();
            }, DispatcherPriority.Background);

            e.Handled = true;
            return;
        }

        if (controlPressed && e.Key == Key.S && _isTaskInfoOpen)
        {
            SaveDetailsButton_Click(sender, e);
            Keyboard.ClearFocus();
            e.Handled = true;
            return;
        }

        if (controlPressed && e.Key is Key.D1 or Key.NumPad1)
        {
            ViewModeInput.SelectedIndex = 0;
            e.Handled = true;
            return;
        }

        if (controlPressed && e.Key is Key.D2 or Key.NumPad2)
        {
            ViewModeInput.SelectedIndex = 1;
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Escape)
        {
            return;
        }

        if (_activeQuickAddEditor is not null)
        {
            CloseActiveQuickAdd();
            e.Handled = true;
        }
        else if (_isTaskInfoOpen)
        {
            CloseTaskInfoPanel();
            e.Handled = true;
        }
    }

    private void OpenDefaultQuickAdd()
    {
        var root = KanbanPanel.Visibility == Visibility.Visible
            ? (DependencyObject)KanbanPanel
            : TasksGrid;
        var trigger = FindVisualDescendants<Button>(root)
            .FirstOrDefault(button =>
                button.Name == "QuickAddTrigger" &&
                button.Visibility == Visibility.Visible &&
                button.IsEnabled);

        if (trigger is not null)
        {
            trigger.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, trigger));
        }
        else if (FirstTaskCreatePanel.Visibility == Visibility.Visible)
        {
            FirstTaskTitleInput.Focus();
        }
    }

    private void LoadSnapshot(bool preserveSelection)
    {
        var selectedId = preserveSelection ? SelectedTask()?.Id : null;

        _isLoading = true;
        try
        {
            _snapshot = _store.Load();
            _tasks.Clear();
            _lastAuditState.Clear();

            foreach (var task in _store.ActiveTasks(_snapshot))
            {
                RegisterTask(task);
                _tasks.Add(task);
            }

            _lastSnapshotWriteUtc = File.Exists(_store.SnapshotPath)
                ? File.GetLastWriteTimeUtc(_store.SnapshotPath)
                : DateTime.MinValue;

            if (selectedId is not null)
            {
                TasksGrid.SelectedItem = _tasks.FirstOrDefault(task => task.Id == selectedId);
            }

            RefreshFocusBoard();
            _tasksView.Refresh();
            RefreshKanbanBoard();
            RefreshSelectedTaskPanel();
            UpdateFilterResult();
            UpdateStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not load local tasks.\n\n{ex.Message}", "Load error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void RegisterTask(TaskItem task)
    {
        task.PropertyChanged += Task_PropertyChanged;
        _lastAuditState[task.Id] = TaskStore.ToAuditDictionary(task);
    }

    private void RefreshFromDiskIfNeeded()
    {
        if (_isSaving || _isLoading || !File.Exists(_store.SnapshotPath))
        {
            return;
        }

        var currentWriteUtc = File.GetLastWriteTimeUtc(_store.SnapshotPath);
        if (currentWriteUtc <= _lastSnapshotWriteUtc)
        {
            return;
        }

        LoadSnapshot(preserveSelection: true);
        SetStatus("Refreshed local task data.");
    }

    private void AddTaskInlineButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string statusToken } trigger || trigger.Parent is not Grid host)
        {
            return;
        }

        var editor = host.Children.OfType<Grid>().FirstOrDefault(child => child.Name == "QuickAddEditor");
        var input = editor?.Children.OfType<TextBox>().FirstOrDefault(child => child.Name == "QuickAddTitleInput");
        if (editor is null || input is null)
        {
            return;
        }

        CloseActiveQuickAdd();
        _activeQuickAddTrigger = trigger;
        _activeQuickAddEditor = editor;
        _activeQuickAddInput = input;
        input.Tag = statusToken;
        input.Clear();
        input.BorderBrush = new SolidColorBrush(Color.FromRgb(49, 87, 126));
        trigger.Visibility = Visibility.Collapsed;
        editor.Visibility = Visibility.Visible;
        var status = StatusFromInlineToken(statusToken);
        SetStatus($"Capturing a {TaskStatuses.ToDisplayName(status)} task. Press Enter to save or Esc to cancel.");
        Dispatcher.BeginInvoke(() => input.Focus(), DispatcherPriority.Input);
        e.Handled = true;
    }

    private void InlineTaskTitle_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox input)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            CommitInlineTask(input);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseActiveQuickAdd();
            e.Handled = true;
        }
    }

    private void ConfirmInlineTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Parent: Grid editor })
        {
            var input = editor.Children.OfType<TextBox>().FirstOrDefault(child => child.Name == "QuickAddTitleInput");
            if (input is not null)
            {
                CommitInlineTask(input);
            }
        }

        e.Handled = true;
    }

    private void CancelInlineTaskButton_Click(object sender, RoutedEventArgs e)
    {
        CloseActiveQuickAdd();
        e.Handled = true;
    }

    private void CommitInlineTask(TextBox input)
    {
        var title = input.Text.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            input.BorderBrush = new SolidColorBrush(Color.FromRgb(248, 113, 113));
            SetStatus("Task title is required before capture can be saved.");
            input.Focus();
            return;
        }

        var status = StatusFromInlineToken(input.Tag as string ?? "");
        CloseActiveQuickAdd();
        AddTaskForStatus(status, title);
    }

    private void CloseActiveQuickAdd()
    {
        if (_activeQuickAddInput is not null)
        {
            _activeQuickAddInput.Clear();
        }

        if (_activeQuickAddEditor is not null)
        {
            _activeQuickAddEditor.Visibility = Visibility.Collapsed;
        }

        if (_activeQuickAddTrigger is not null)
        {
            _activeQuickAddTrigger.Visibility = Visibility.Visible;
        }

        _activeQuickAddEditor = null;
        _activeQuickAddTrigger = null;
        _activeQuickAddInput = null;
    }

    private void AddTaskForStatus(string status, string title)
    {
        var task = _store.CreateTask(
            _snapshot,
            title,
            UserActorName,
            UserActorType,
            "medium",
            status,
            status is TaskStatuses.Done or TaskStatuses.Cancelled ? 100 : 0);

        RegisterTask(task);
        _tasks.Insert(0, task);
        RefreshTaskViews();
        TasksGrid.SelectedItem = task;
        RefreshFocusBoard();
        SetStatus($"Created task {task.ShortId}.");
    }

    private static string StatusFromInlineToken(string token)
    {
        if (TaskStatuses.ClickUpLite.Contains(token, StringComparer.OrdinalIgnoreCase))
        {
            return token.ToLowerInvariant();
        }

        if (token is "todo" or "in_progress" or "done" or "close")
        {
            return TaskStatuses.FromKanbanGroup(token);
        }

        return StatusFromDisplayName(token);
    }

    private static string StatusFromDisplayName(string statusDisplay)
    {
        return statusDisplay.Trim().ToUpperInvariant() switch
        {
            "TO DO" => TaskStatuses.Backlog,
            "READY" => TaskStatuses.Ready,
            "IN PROGRESS" => TaskStatuses.InProgress,
            "BLOCKED" => TaskStatuses.Blocked,
            "REVIEW" => TaskStatuses.Review,
            "DONE" => TaskStatuses.Done,
            "CLOSE" => TaskStatuses.Cancelled,
            _ => TaskStatuses.Backlog
        };
    }

    private void TasksGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshSelectedTaskPanel();
    }

    private void FocusList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox listBox || listBox.SelectedItem is not TaskItem task)
        {
            return;
        }

        TasksGrid.SelectedItem = _tasks.FirstOrDefault(item => item.Id == task.Id);

        foreach (var other in new[] { NowList, BlockedList, DueList, RecentList })
        {
            if (!ReferenceEquals(other, listBox))
            {
                other.SelectedItem = null;
            }
        }
    }

    private void FocusTask_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ToggleTaskInfo(sender);
        e.Handled = true;
    }

    private void KanbanList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox listBox || listBox.SelectedItem is not TaskItem task)
        {
            return;
        }

        TasksGrid.SelectedItem = _tasks.FirstOrDefault(item => item.Id == task.Id);
        RefreshSelectedTaskPanel();

        foreach (var other in new[] { TodoKanbanList, InProgressKanbanList, DoneKanbanList, CloseKanbanList })
        {
            if (!ReferenceEquals(other, listBox))
            {
                other.SelectedItem = null;
            }
        }
    }

    private void KanbanCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _kanbanDragStart = e.GetPosition(this);
    }

    private void KanbanCard_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _kanbanDragStart = null;
    }

    private void KanbanCard_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _kanbanDragStart is not Point start)
        {
            return;
        }

        var current = e.GetPosition(this);
        if (Math.Abs(current.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var task = sender switch
        {
            ListBoxItem item when item.DataContext is TaskItem itemTask => itemTask,
            DependencyObject dependencyObject => FindParent<ListBoxItem>(dependencyObject)?.DataContext as TaskItem,
            _ => null
        };

        if (task is null)
        {
            return;
        }

        _kanbanDragStart = null;
        DragDrop.DoDragDrop((DependencyObject)sender, task, DragDropEffects.Move);
    }

    private void KanbanList_DragEnter(object sender, DragEventArgs e)
    {
        if (sender is ListBox listBox && e.Data.GetDataPresent(typeof(TaskItem)))
        {
            SetKanbanDropTarget(listBox, true);
        }
    }

    private void KanbanList_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(TaskItem)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void KanbanList_DragLeave(object sender, DragEventArgs e)
    {
        ResetKanbanDropTarget(sender as ListBox);
    }

    private void KanbanList_Drop(object sender, DragEventArgs e)
    {
        ResetKanbanDropTarget(sender as ListBox);
        if (sender is not ListBox listBox ||
            listBox.Tag is not string kanbanGroup ||
            e.Data.GetData(typeof(TaskItem)) is not TaskItem task)
        {
            return;
        }

        MoveTaskToStatus(task, TaskStatuses.FromKanbanGroup(kanbanGroup));
    }

    private void ResetKanbanDropTarget(ListBox? listBox)
    {
        if (listBox is null)
        {
            return;
        }

        SetKanbanDropTarget(listBox, false);
    }

    private void SetKanbanDropTarget(ListBox listBox, bool isActive)
    {
        listBox.Background = isActive ? (Brush)FindResource("KanbanDropBrush") : Brushes.Transparent;
        listBox.BorderBrush = isActive ? (Brush)FindResource("BlueAccentBrush") : Brushes.Transparent;
        listBox.BorderThickness = isActive ? new Thickness(1) : new Thickness(0);

        if (DropOverlayForList(listBox) is Border overlay)
        {
            overlay.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private Border? DropOverlayForList(ListBox listBox)
    {
        return listBox.Name switch
        {
            nameof(TodoKanbanList) => TodoDropOverlay,
            nameof(InProgressKanbanList) => InProgressDropOverlay,
            nameof(DoneKanbanList) => DoneDropOverlay,
            nameof(CloseKanbanList) => CloseDropOverlay,
            _ => null
        };
    }

    private void OpenTaskInfoButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleTaskInfo(sender);
        e.Handled = true;
    }

    private void OpenTaskInfoButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ToggleTaskInfo(sender);
        e.Handled = true;
    }

    private void TaskActionsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement button)
        {
            return;
        }

        var task = ResolveTaskFromSender(sender);
        var menuOwner = button.ContextMenu is not null ? button : FindParentWithContextMenu(button);
        if (task is null || menuOwner?.ContextMenu is null)
        {
            return;
        }

        menuOwner.ContextMenu.DataContext = task;
        menuOwner.ContextMenu.PlacementTarget = menuOwner;
        menuOwner.ContextMenu.Placement = ReferenceEquals(menuOwner, button)
            ? PlacementMode.Left
            : PlacementMode.Bottom;
        menuOwner.ContextMenu.IsOpen = true;
        e.Handled = true;
    }

    private void MoveTaskMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string nextStatus, DataContext: TaskItem menuTask })
        {
            var task = _tasks.FirstOrDefault(item => item.Id == menuTask.Id);
            if (task is not null)
            {
                MoveTaskToStatus(task, nextStatus);
            }
        }

        e.Handled = true;
    }

    private void OpenTaskInfoMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ToggleTaskInfo(sender);
        e.Handled = true;
    }

    private void DeleteTaskMenuItem_Click(object sender, RoutedEventArgs e)
    {
        DeleteTaskFromSender(sender);
        e.Handled = true;
    }

    private void MoveTaskToStatus(TaskItem task, string nextStatus)
    {
        nextStatus = string.IsNullOrWhiteSpace(nextStatus)
            ? TaskStatuses.Backlog
            : nextStatus.Trim().ToLowerInvariant();

        if (task.Status == nextStatus)
        {
            SetStatus($"{task.ShortId} is already {TaskStatuses.ToDisplayName(nextStatus)}.");
            return;
        }

        _isApplyingTaskChange = true;
        try
        {
            task.Status = nextStatus;
            if (nextStatus == TaskStatuses.Done || nextStatus == TaskStatuses.Cancelled)
            {
                task.ProgressPercent = 100;
            }
            else if (task.ProgressPercent >= 100)
            {
                task.ProgressPercent = 95;
            }
        }
        finally
        {
            _isApplyingTaskChange = false;
        }

        SaveTaskChange(task, new[] { "status", "progress_percent" });
        RefreshTaskViews();
        RefreshFocusBoard();
        RefreshSelectedTaskPanel();
        SetStatus($"Moved {task.ShortId} to {TaskStatuses.ToDisplayName(nextStatus)}.");
    }

    private void ToggleTaskInfo(object sender)
    {
        var selectedTask = ResolveTaskFromSender(sender);
        if (selectedTask is null)
        {
            return;
        }

        if (_isTaskInfoOpen && _openTaskInfoTaskId == selectedTask.Id)
        {
            CloseTaskInfoPanel();
            return;
        }

        if (_isTaskInfoOpen && !TrySaveTaskInfoChangesBeforeClose())
        {
            return;
        }

        TasksGrid.SelectedItem = selectedTask;
        RefreshSelectedTaskPanel();
        _openTaskInfoTaskId = selectedTask.Id;
        OpenTaskInfoPanel();
    }

    private TaskItem? ResolveTaskFromSender(object sender)
    {
        if (sender is not DependencyObject dependencyObject)
        {
            return null;
        }

        if (sender is FrameworkElement { DataContext: TaskItem directTask })
        {
            return _tasks.FirstOrDefault(item => item.Id == directTask.Id);
        }

        var dataGridRowTask = FindParent<DataGridRow>(dependencyObject)?.DataContext as TaskItem;
        if (dataGridRowTask is not null)
        {
            return _tasks.FirstOrDefault(item => item.Id == dataGridRowTask.Id);
        }

        var listBoxItemTask = FindParent<ListBoxItem>(dependencyObject)?.DataContext as TaskItem;
        if (listBoxItemTask is not null)
        {
            return _tasks.FirstOrDefault(item => item.Id == listBoxItemTask.Id);
        }

        return null;
    }

    private void CloseTaskInfoButton_Click(object sender, RoutedEventArgs e)
    {
        CloseTaskInfoPanel();
        e.Handled = true;
    }

    private void DeleteTaskButton_Click(object sender, RoutedEventArgs e)
    {
        DeleteTaskFromSender(sender);
        e.Handled = true;
    }

    private void DeleteTaskButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DeleteTaskFromSender(sender);
        e.Handled = true;
    }

    private void DeleteTaskFromSender(object sender)
    {
        var task = ResolveTaskFromSender(sender);
        if (task is null)
        {
            return;
        }

        DeleteTask(task);
    }

    private void CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
    }

    private void RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
    {
    }

    private void Task_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoading || _isApplyingTaskChange || sender is not TaskItem task || task.IsDeleted)
        {
            return;
        }

        if (e.PropertyName is nameof(TaskItem.UpdatedAt)
            or nameof(TaskItem.UpdatedBy)
            or nameof(TaskItem.UpdatedAtLocal)
            or nameof(TaskItem.FocusBadges)
            or nameof(TaskItem.KanbanStatus)
            or nameof(TaskItem.StatusDisplay)
            or nameof(TaskItem.StatusOrder)
            or nameof(TaskItem.IsDone)
            or nameof(TaskItem.DueDateLocal)
            or nameof(TaskItem.DueDateText))
        {
            return;
        }

        _isApplyingTaskChange = true;
        try
        {
            if (e.PropertyName == nameof(TaskItem.IsDone))
            {
                task.Status = task.IsDone ? TaskStatuses.Done : TaskStatuses.InProgress;
                task.ProgressPercent = task.IsDone ? 100 : Math.Min(task.ProgressPercent, 95);
            }

            SaveTaskChange(task, new[] { ToSnakeCase(e.PropertyName ?? "task") });
            RefreshTaskViews();
            RefreshFocusBoard();
            RefreshSelectedTaskPanel();
        }
        finally
        {
            _isApplyingTaskChange = false;
        }
    }

    private void SaveDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        var task = SelectedTask();
        if (task is null)
        {
            SetStatus("Select a task before saving details.");
            return;
        }

        var changed = new List<string>();
        if (string.IsNullOrWhiteSpace(SelectedTaskTitleInput.Text))
        {
            SetStatus("Task title is required before details can be saved.");
            SelectedTaskTitleInput.Focus();
            SelectedTaskTitleInput.SelectAll();
            return;
        }

        _isApplyingTaskChange = true;
        try
        {
            ApplyTextChange(() => task.Title, value => task.Title = value, SelectedTaskTitleInput.Text, "title", changed);
            ApplyTextChange(() => task.Notes, value => task.Notes = value, NotesInput.Text, "notes", changed);
            ApplyTextChange(() => task.ProjectId, value => task.ProjectId = value, ProjectInput.Text, "project_id", changed);
            ApplyTextChange(() => task.ListId, value => task.ListId = value, ListInput.Text, "list_id", changed);
            ApplyTextChange(() => task.Assignee, value => task.Assignee = value, AssigneeInput.Text, "assignee", changed);
            ApplyTextChange(() => task.Estimate, value => task.Estimate = value, EstimateInput.Text, "estimate", changed);

            var nextStatus = DetailStatusInput.SelectedItem?.ToString() ?? task.Status;
            if (!string.Equals(task.Status, nextStatus, StringComparison.OrdinalIgnoreCase))
            {
                task.Status = nextStatus;
                changed.Add("status");
            }

            var nextPriority = DetailPriorityInput.SelectedItem?.ToString() ?? task.Priority;
            if (!string.Equals(task.Priority, nextPriority, StringComparison.OrdinalIgnoreCase))
            {
                task.Priority = nextPriority;
                changed.Add("priority");
            }

            var nextProgress = (int)Math.Round(DetailProgressInput.Value);
            if (task.ProgressPercent != nextProgress)
            {
                task.ProgressPercent = nextProgress;
                changed.Add("progress_percent");
            }

            var tags = TagsInput.Text.Trim();
            if (!string.Equals(task.TagsText, tags, StringComparison.Ordinal))
            {
                task.TagsText = tags;
                changed.Add("tags");
            }

            DateTimeOffset? dueDate = _detailDueDate is DateTime selectedDueDate
                ? new DateTimeOffset(selectedDueDate.Date)
                : null;
            if (task.DueDate != dueDate)
            {
                task.DueDate = dueDate;
                changed.Add("due_date");
            }

            var blockedBy = SplitCsv(BlockedByInput.Text);
            if (!task.BlockedByTaskIds.SequenceEqual(blockedBy, StringComparer.OrdinalIgnoreCase))
            {
                task.BlockedByTaskIds.Clear();
                foreach (var id in blockedBy)
                {
                    task.BlockedByTaskIds.Add(id);
                }
                changed.Add("blocked_by_task_ids");
            }
        }
        finally
        {
            _isApplyingTaskChange = false;
        }

        SaveTaskChange(task, changed);
        RefreshTaskViews();
        RefreshFocusBoard();
        RefreshSelectedTaskPanel();
        SetTaskInfoDirty(false);
        SetStatus($"Saved details for {task.ShortId}.");
    }

    private void TaskInfoField_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isTaskInfoOpen ||
            _isRefreshingTaskInfo ||
            _isLoading ||
            _isApplyingTaskChange ||
            SelectedTask() is null)
        {
            return;
        }

        SetTaskInfoDirty(true);
    }

    private void SetTaskInfoDirty(bool isDirty)
    {
        _hasTaskInfoUnsavedChanges = isDirty;

        if (SaveDetailsButton is not null)
        {
            SaveDetailsButton.IsEnabled = isDirty && SelectedTask() is not null;
        }

        if (TaskInfoSaveStateText is null || TaskInfoSaveStateDot is null)
        {
            return;
        }

        TaskInfoSaveStateText.Text = isDirty ? "Unsaved" : "Saved";
        TaskInfoSaveStateText.Foreground = new SolidColorBrush(isDirty
            ? Color.FromRgb(255, 226, 168)
            : Color.FromRgb(138, 227, 157));
        TaskInfoSaveStateDot.Fill = new SolidColorBrush(isDirty
            ? Color.FromRgb(245, 158, 11)
            : Color.FromRgb(87, 209, 123));
    }

    private void SelectedTaskTitleInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        SaveDetailsButton_Click(sender, e);
        Keyboard.ClearFocus();
        e.Handled = true;
    }

    private void DetailProgressInput_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DetailProgressValueText is not null)
        {
            DetailProgressValueText.Text = $"{(int)Math.Round(e.NewValue)}%";
        }

        TaskInfoField_Changed(sender, e);
    }

    private void DetailDueCalendarButton_Click(object sender, RoutedEventArgs e)
    {
        OpenDetailDuePopup();
        e.Handled = true;
    }

    private void DetailDueHost_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        OpenDetailDuePopup();
        e.Handled = true;
    }

    private void OpenDetailDuePopup()
    {
        if (_detailDueDate is DateTime selectedDate)
        {
            _detailDueCalendarMonth = new DateTime(selectedDate.Year, selectedDate.Month, 1);
        }
        else
        {
            var today = DateTime.Today;
            _detailDueCalendarMonth = new DateTime(today.Year, today.Month, 1);
        }

        RenderDetailDueCalendar();
        DetailDuePopup.IsOpen = true;
    }

    private void DetailDuePreviousMonth_Click(object sender, RoutedEventArgs e)
    {
        _detailDueCalendarMonth = _detailDueCalendarMonth.AddMonths(-1);
        RenderDetailDueCalendar();
        e.Handled = true;
    }

    private void DetailDueNextMonth_Click(object sender, RoutedEventArgs e)
    {
        _detailDueCalendarMonth = _detailDueCalendarMonth.AddMonths(1);
        RenderDetailDueCalendar();
        e.Handled = true;
    }

    private void DetailDueToday_Click(object sender, RoutedEventArgs e)
    {
        SetDetailDueDate(DateTime.Today);
        DetailDuePopup.IsOpen = false;
        e.Handled = true;
    }

    private void DetailDueClear_Click(object sender, RoutedEventArgs e)
    {
        SetDetailDueDate(null);
        DetailDuePopup.IsOpen = false;
        e.Handled = true;
    }

    private void DetailCalendarDay_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DateTime date })
        {
            SetDetailDueDate(date);
            DetailDuePopup.IsOpen = false;
        }

        e.Handled = true;
    }

    private void SetDetailDueDate(DateTime? date)
    {
        _detailDueDate = date?.Date;
        if (_detailDueDate is DateTime dueDate)
        {
            DetailDueDateText.Text = dueDate.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
            DetailDueDateText.Foreground = new SolidColorBrush(Color.FromRgb(244, 247, 251));
            _detailDueCalendarMonth = new DateTime(dueDate.Year, dueDate.Month, 1);
        }
        else
        {
            DetailDueDateText.Text = "Select a date";
            DetailDueDateText.Foreground = new SolidColorBrush(Color.FromRgb(113, 121, 135));
        }

        TaskInfoField_Changed(DetailDueDateText, new RoutedEventArgs());
    }

    private void RenderDetailDueCalendar()
    {
        DetailDueMonthText.Text = _detailDueCalendarMonth.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        DetailDueCalendarGrid.Children.Clear();

        foreach (var label in new[] { "S", "M", "T", "W", "T", "F", "S" })
        {
            DetailDueCalendarGrid.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = new SolidColorBrush(Color.FromRgb(113, 121, 135)),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            });
        }

        var firstOfMonth = _detailDueCalendarMonth;
        var firstVisibleDate = firstOfMonth.AddDays(-(int)firstOfMonth.DayOfWeek);
        for (var offset = 0; offset < 42; offset++)
        {
            var date = firstVisibleDate.AddDays(offset);
            DetailDueCalendarGrid.Children.Add(CreateDetailDueDayButton(date, date.Month == firstOfMonth.Month));
        }
    }

    private Button CreateDetailDueDayButton(DateTime date, bool isCurrentMonth)
    {
        var isSelected = _detailDueDate?.Date == date.Date;
        var isToday = DateTime.Today == date.Date;
        var button = new Button
        {
            Content = date.Day.ToString(CultureInfo.InvariantCulture),
            Tag = date.Date,
            Width = 28,
            MinHeight = 26,
            Padding = new Thickness(0),
            Margin = new Thickness(2),
            FontSize = 11,
            FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal,
            Background = new SolidColorBrush(isSelected ? Color.FromRgb(47, 128, 237) : Color.FromRgb(17, 19, 24)),
            BorderBrush = new SolidColorBrush(isSelected || isToday ? Color.FromRgb(88, 166, 255) : Color.FromRgb(42, 47, 58)),
            Foreground = new SolidColorBrush(isCurrentMonth ? Color.FromRgb(244, 247, 251) : Color.FromRgb(113, 121, 135)),
            Opacity = isCurrentMonth ? 1 : 0.55
        };
        button.Click += DetailCalendarDay_Click;
        return button;
    }

    private void InlineDueDateButton_Click(object sender, RoutedEventArgs e)
    {
        if (ResolveTaskFromSender(sender) is not { } task || sender is not FrameworkElement placementTarget)
        {
            return;
        }

        _inlineDueTask = task;
        var selectedDate = task.DueDate?.LocalDateTime.Date;
        _inlineDueCalendarMonth = selectedDate is DateTime dueDate
            ? new DateTime(dueDate.Year, dueDate.Month, 1)
            : new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        EnsureInlineDuePopup();
        _inlineDuePopup!.PlacementTarget = placementTarget;
        _inlineDuePopup.HorizontalOffset = -132;
        _inlineDuePopup.VerticalOffset = 4;
        RenderInlineDueCalendar();
        _inlineDuePopup.IsOpen = true;
        e.Handled = true;
    }

    private void EnsureInlineDuePopup()
    {
        if (_inlineDuePopup is not null)
        {
            return;
        }

        _inlineDueMonthText = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeights.SemiBold
        };
        _inlineDueCalendarGrid = new UniformGrid { Columns = 7 };

        var previous = CreateCalendarNavButton("\uE76B", "Previous month", (_, e) =>
        {
            _inlineDueCalendarMonth = _inlineDueCalendarMonth.AddMonths(-1);
            RenderInlineDueCalendar();
            e.Handled = true;
        });
        var next = CreateCalendarNavButton("\uE76C", "Next month", (_, e) =>
        {
            _inlineDueCalendarMonth = _inlineDueCalendarMonth.AddMonths(1);
            RenderInlineDueCalendar();
            e.Handled = true;
        });

        var header = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        Grid.SetColumn(previous, 0);
        Grid.SetColumn(_inlineDueMonthText, 1);
        Grid.SetColumn(next, 2);
        header.Children.Add(previous);
        header.Children.Add(_inlineDueMonthText);
        header.Children.Add(next);

        var clear = new Button { Content = "Clear", Margin = new Thickness(0) };
        clear.Click += (_, e) =>
        {
            ApplyInlineDueDate(null);
            e.Handled = true;
        };

        var today = new Button { Content = "Today", Margin = new Thickness(8, 0, 0, 0) };
        today.Click += (_, e) =>
        {
            ApplyInlineDueDate(DateTime.Today);
            e.Handled = true;
        };

        var footer = new DockPanel { LastChildFill = false };
        DockPanel.SetDock(clear, Dock.Left);
        DockPanel.SetDock(today, Dock.Right);
        footer.Children.Add(clear);
        footer.Children.Add(today);

        var panel = new StackPanel();
        panel.Children.Add(header);
        panel.Children.Add(_inlineDueCalendarGrid);
        panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(37, 42, 51)), Margin = new Thickness(0, 8, 0, 8) });
        panel.Children.Add(footer);

        _inlineDuePopup = new Popup
        {
            Placement = PlacementMode.Bottom,
            AllowsTransparency = true,
            StaysOpen = false,
            PopupAnimation = PopupAnimation.Fade,
            Child = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(16, 20, 27)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(47, 128, 237)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10),
                Width = 258,
                Child = panel
            }
        };
    }

    private static Button CreateCalendarNavButton(string icon, string toolTip, RoutedEventHandler click)
    {
        var button = new Button
        {
            Content = icon,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            Width = 28,
            MinHeight = 28,
            Padding = new Thickness(0),
            Margin = new Thickness(0),
            ToolTip = toolTip
        };
        button.Click += click;
        return button;
    }

    private void RenderInlineDueCalendar()
    {
        if (_inlineDueCalendarGrid is null || _inlineDueMonthText is null)
        {
            return;
        }

        _inlineDueMonthText.Text = _inlineDueCalendarMonth.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        _inlineDueCalendarGrid.Children.Clear();
        foreach (var label in new[] { "S", "M", "T", "W", "T", "F", "S" })
        {
            _inlineDueCalendarGrid.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = new SolidColorBrush(Color.FromRgb(113, 121, 135)),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            });
        }

        var firstOfMonth = _inlineDueCalendarMonth;
        var firstVisibleDate = firstOfMonth.AddDays(-(int)firstOfMonth.DayOfWeek);
        for (var offset = 0; offset < 42; offset++)
        {
            var date = firstVisibleDate.AddDays(offset);
            _inlineDueCalendarGrid.Children.Add(CreateInlineDueDayButton(date, date.Month == firstOfMonth.Month));
        }
    }

    private Button CreateInlineDueDayButton(DateTime date, bool isCurrentMonth)
    {
        var selectedDate = _inlineDueTask?.DueDate?.LocalDateTime.Date;
        var isSelected = selectedDate == date.Date;
        var isToday = DateTime.Today == date.Date;
        var button = new Button
        {
            Content = date.Day.ToString(CultureInfo.InvariantCulture),
            Tag = date.Date,
            Width = 28,
            MinHeight = 26,
            Padding = new Thickness(0),
            Margin = new Thickness(2),
            FontSize = 11,
            FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal,
            Background = new SolidColorBrush(isSelected ? Color.FromRgb(47, 128, 237) : Color.FromRgb(17, 19, 24)),
            BorderBrush = new SolidColorBrush(isSelected || isToday ? Color.FromRgb(88, 166, 255) : Color.FromRgb(42, 47, 58)),
            Foreground = new SolidColorBrush(isCurrentMonth ? Color.FromRgb(244, 247, 251) : Color.FromRgb(113, 121, 135)),
            Opacity = isCurrentMonth ? 1 : 0.55
        };
        button.Click += (_, e) =>
        {
            ApplyInlineDueDate(date);
            e.Handled = true;
        };
        return button;
    }

    private void ApplyInlineDueDate(DateTime? date)
    {
        if (_inlineDueTask is not { } task)
        {
            return;
        }

        DateTimeOffset? nextDueDate = date is DateTime selectedDate
            ? new DateTimeOffset(selectedDate.Date)
            : null;
        if (task.DueDate == nextDueDate)
        {
            _inlineDuePopup?.SetCurrentValue(Popup.IsOpenProperty, false);
            return;
        }

        _isApplyingTaskChange = true;
        try
        {
            task.DueDate = nextDueDate;
        }
        finally
        {
            _isApplyingTaskChange = false;
        }

        SaveTaskChange(task, new[] { "due_date" });
        RefreshTaskViews();
        RefreshFocusBoard();
        RefreshSelectedTaskPanel();
        SetStatus(nextDueDate is null ? $"Cleared due date for {task.ShortId}." : $"Set {task.ShortId} due date.");
        _inlineDuePopup?.SetCurrentValue(Popup.IsOpenProperty, false);
    }

    private void AddSubtaskButton_Click(object sender, RoutedEventArgs e)
    {
        var task = SelectedTask();
        if (task is null)
        {
            SetStatus("Select a task before adding a subtask.");
            return;
        }

        var title = SubtaskInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            SetStatus("Subtask title is required.");
            return;
        }

        _store.AddSubtask(_snapshot, task, title, UserActorName, UserActorType);
        _lastAuditState[task.Id] = TaskStore.ToAuditDictionary(task);
        SubtaskInput.Clear();
        MarkSnapshotSaved();
        RefreshSelectedTaskPanel();
        RefreshFocusBoard();
        RefreshTaskViews();
        SetStatus($"Added subtask to {task.ShortId}.");
    }

    private void SubtaskInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        AddSubtaskButton_Click(sender, e);
        e.Handled = true;
    }

    private void AddAttachmentButton_Click(object sender, RoutedEventArgs e)
    {
        var task = SelectedTask();
        if (task is null)
        {
            SetStatus("Select a task before adding an attachment.");
            return;
        }

        var target = AttachmentInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(target))
        {
            SetStatus("Attachment URL or file path is required.");
            return;
        }

        _store.AddAttachment(_snapshot, task, target, AttachmentTitleInput.Text, "", UserActorName, UserActorType);
        _lastAuditState[task.Id] = TaskStore.ToAuditDictionary(task);
        AttachmentInput.Clear();
        AttachmentTitleInput.Clear();
        MarkSnapshotSaved();
        RefreshSelectedTaskPanel();
        RefreshTaskViews();
        SetStatus($"Attached reference to {task.ShortId}.");
    }

    private void AddLogButton_Click(object sender, RoutedEventArgs e)
    {
        var task = SelectedTask();
        if (task is null)
        {
            SetStatus("Select a task before adding a log.");
            return;
        }

        var message = LogInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            SetStatus("Log message is required.");
            return;
        }

        _store.AddLog(_snapshot, task, message, UserActorName, UserActorType);
        _lastAuditState[task.Id] = TaskStore.ToAuditDictionary(task);
        LogInput.Clear();
        MarkSnapshotSaved();
        RefreshSelectedTaskPanel();
        RefreshFocusBoard();
        RefreshTaskViews();
        SetStatus($"Added log to {task.ShortId}.");
    }

    private void LogInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            return;
        }

        AddLogButton_Click(sender, e);
        e.Handled = true;
    }

    private void MarkDoneButton_Click(object sender, RoutedEventArgs e)
    {
        var task = SelectedTask();
        if (task is null)
        {
            SetStatus("Select a task before marking done.");
            return;
        }

        _isApplyingTaskChange = true;
        task.IsDone = true;
        _isApplyingTaskChange = false;
        SaveTaskChange(task, new[] { "status", "progress_percent" });
        RefreshTaskViews();
        RefreshFocusBoard();
        RefreshSelectedTaskPanel();
        SetStatus($"Completed {task.ShortId}.");
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var task = SelectedTask();
        if (task is null)
        {
            SetStatus("Select a task before deleting.");
            return;
        }

        DeleteTask(task);
    }

    private void DeleteTask(TaskItem task)
    {
        var dialog = new ConfirmDeleteWindow(this, task.ShortId, task.Title);
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _store.SoftDeleteTask(_snapshot, task, UserActorName, UserActorType);
        _lastAuditState.Remove(task.Id);
        _tasks.Remove(task);
        if (_openTaskInfoTaskId == task.Id)
        {
            CloseTaskInfoPanel(saveUnsavedChanges: false);
        }
        MarkSnapshotSaved();
        RefreshTaskViews();
        RefreshFocusBoard();
        RefreshSelectedTaskPanel();
        SetStatus($"Deleted {task.ShortId}.");
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        Topmost = sender is ToggleButton toggle
            ? toggle.IsChecked == true
            : !Topmost;
        PinButton.IsChecked = Topmost;
        PinButton.ToolTip = Topmost ? "Stop keeping window on top" : "Keep window on top";
        SetStatus(Topmost ? "Window is now always on top." : "Window pin disabled.");
        SaveUiPreferences();
    }

    private void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        var aboutWindow = new AboutReleaseWindow(
            this,
            _store.DataDirectory,
            _store.SnapshotPath,
            _store.OperationsPath);
        aboutWindow.ShowDialog();
    }

    private void QuickStartButton_Click(object sender, RoutedEventArgs e)
    {
        var quickStartWindow = new QuickStartWindow(this);
        quickStartWindow.ShowDialog();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(
            this,
            _store.DataDirectory,
            _store.SnapshotPath,
            _store.OperationsPath,
            Topmost);
        settingsWindow.ShowDialog();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        LoadSnapshot(preserveSelection: true);
        SetStatus("Manual refresh complete.");
    }

    private void SearchInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        ClearSearchButton.Visibility = string.IsNullOrWhiteSpace(SearchInput.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;
        ApplyTaskFilter();
    }

    private void StatusFilterInput_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyTaskFilter();
    }

    private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
        SearchInput.Clear();
        SearchInput.Focus();
    }

    private void ClearTaskFiltersButton_Click(object sender, RoutedEventArgs e)
    {
        SearchInput.Clear();
        StatusFilterInput.SelectedIndex = 0;
        ApplyTaskFilter();
    }

    private void FirstTaskTitleInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        CreateFirstTask();
        e.Handled = true;
    }

    private void CreateFirstTaskButton_Click(object sender, RoutedEventArgs e)
    {
        CreateFirstTask();
    }

    private void CreateFirstTask()
    {
        var title = FirstTaskTitleInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            FirstTaskTitleInput.BorderBrush = new SolidColorBrush(Color.FromRgb(248, 113, 113));
            SetStatus("Add a title to start tracking your first task.");
            FirstTaskTitleInput.Focus();
            return;
        }

        FirstTaskTitleInput.Clear();
        FirstTaskTitleInput.BorderBrush = new SolidColorBrush(Color.FromRgb(49, 87, 126));
        AddTaskForStatus(TaskStatuses.Backlog, title);
    }

    private void ApplyTaskFilter()
    {
        _tasksView.Refresh();
        RefreshKanbanBoard();
        UpdateFilterResult();
        ResetKanbanScroll();
    }

    private bool MatchesCurrentFilter(TaskItem task)
    {
        var selectedStatus = (StatusFilterInput.SelectedItem as ComboBoxItem)?.Tag as string ?? "all";
        if (!string.Equals(selectedStatus, "all", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(task.Status, selectedStatus, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var query = SearchInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return new[]
        {
            task.Title,
            task.Notes,
            task.ProjectId,
            task.ListId,
            task.Assignee,
            task.TagsText,
            task.StatusDisplay,
            task.Priority,
            task.Estimate
        }.Any(value => value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true);
    }

    private void UpdateFilterResult()
    {
        var selectedStatus = (StatusFilterInput.SelectedItem as ComboBoxItem)?.Tag as string ?? "all";
        var filterActive = !string.IsNullOrWhiteSpace(SearchInput.Text) || selectedStatus != "all";
        var visibleCount = _tasks.Count(MatchesCurrentFilter);
        var hasVisibleTasks = visibleCount > 0;
        FilterResultText.Visibility = filterActive ? Visibility.Visible : Visibility.Collapsed;
        FilterResultText.Text = filterActive ? $"{visibleCount} shown" : "";

        TaskEmptyStatePanel.Visibility = hasVisibleTasks ? Visibility.Collapsed : Visibility.Visible;
        ClearTaskFiltersButton.Visibility = filterActive ? Visibility.Visible : Visibility.Collapsed;
        FirstTaskCreatePanel.Visibility = filterActive ? Visibility.Collapsed : Visibility.Visible;
        FirstTaskBenefitPanel.Visibility = filterActive ? Visibility.Collapsed : Visibility.Visible;
        FirstTaskHintText.Visibility = filterActive ? Visibility.Collapsed : Visibility.Visible;
        TaskEmptyStateTitle.Text = filterActive ? "No tasks match" : "No tasks yet";
        TaskEmptyStateText.Text = filterActive
            ? "Try another search or status."
            : "Start with one task. The workspace will turn it into focus, board movement, logs, and AI-readable context.";

        TasksGrid.Visibility = hasVisibleTasks && ViewModeInput.SelectedIndex == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        KanbanPanel.Visibility = hasVisibleTasks && ViewModeInput.SelectedIndex == 1
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ViewModeInput_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModeInput is null || TasksGrid is null || KanbanPanel is null || ViewModeInput.SelectedIndex < 0)
        {
            return;
        }

        CloseActiveQuickAdd();

        if (ViewModeInput.SelectedIndex == 1)
        {
            TasksGrid.Visibility = Visibility.Collapsed;
            KanbanPanel.Visibility = Visibility.Visible;
            RefreshKanbanBoard();
            ResetKanbanScroll();
        }
        else
        {
            KanbanPanel.Visibility = Visibility.Collapsed;
            TasksGrid.Visibility = Visibility.Visible;
            _tasksView.Refresh();
        }

        UpdateViewModeButtons();
        UpdateFilterResult();
        SaveUiPreferences();
    }

    private void ResetKanbanScroll()
    {
        if (KanbanPanel is null || ViewModeInput?.SelectedIndex != 1)
        {
            return;
        }

        Dispatcher.BeginInvoke(() => KanbanPanel.ScrollToLeftEnd(), DispatcherPriority.ContextIdle);
    }

    private void ViewModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string tag } || !int.TryParse(tag, out var selectedIndex))
        {
            return;
        }

        ViewModeInput.SelectedIndex = selectedIndex;
        UpdateViewModeButtons();
    }

    private void UpdateViewModeButtons()
    {
        if (ClickUpViewButton is null || KanbanViewButton is null || ViewModeInput is null)
        {
            return;
        }

        var listActive = ViewModeInput.SelectedIndex != 1;
        ClickUpViewButton.Background = new SolidColorBrush(listActive ? Color.FromRgb(47, 128, 237) : Colors.Transparent);
        ClickUpViewButton.BorderBrush = new SolidColorBrush(listActive ? Color.FromRgb(88, 166, 255) : Colors.Transparent);
        ClickUpViewButton.Foreground = new SolidColorBrush(listActive ? Colors.White : Color.FromRgb(161, 168, 181));
        KanbanViewButton.Background = new SolidColorBrush(!listActive ? Color.FromRgb(47, 128, 237) : Colors.Transparent);
        KanbanViewButton.BorderBrush = new SolidColorBrush(!listActive ? Color.FromRgb(88, 166, 255) : Colors.Transparent);
        KanbanViewButton.Foreground = new SolidColorBrush(!listActive ? Colors.White : Color.FromRgb(161, 168, 181));
    }

    private void TogglePopupVisibility()
    {
        if (_isClosing)
        {
            return;
        }

        if (IsVisible && WindowState != WindowState.Minimized)
        {
            _windowStateBeforeMinimize = WindowState == WindowState.Maximized
                ? WindowState.Maximized
                : WindowState.Normal;
            WindowState = WindowState.Minimized;
            _floatingToggleWindow.SetPopupState(false);
            return;
        }

        Show();
        WindowState = _windowStateBeforeMinimize;
        Activate();
        _floatingToggleWindow.SetPopupState(true);
    }

    private void SyncFloatingToggleState()
    {
        if (_isClosing)
        {
            return;
        }

        if (WindowState == WindowState.Maximized)
        {
            _floatingToggleWindow.Hide();
            return;
        }

        if (!_floatingToggleWindow.IsVisible)
        {
            _floatingToggleWindow.Show();
        }

        _floatingToggleWindow.SetPopupState(IsVisible && WindowState != WindowState.Minimized);
    }

    protected override void OnClosed(EventArgs e)
    {
        _isClosing = true;
        SaveUiPreferences();
        _refreshTimer.Stop();
        _floatingToggleWindow.Close();
        base.OnClosed(e);
        Application.Current.Shutdown();
    }

    private void ApplyUiPreferences(UiPreferences preferences)
    {
        Width = Math.Max(MinWidth, preferences.WindowWidth);
        Height = Math.Max(MinHeight, preferences.WindowHeight);
        Topmost = preferences.IsAlwaysOnTop;
        PinButton.IsChecked = Topmost;
        PinButton.ToolTip = Topmost ? "Stop keeping window on top" : "Keep window on top";
    }

    private void SaveUiPreferences()
    {
        if (!_isUiReady)
        {
            return;
        }

        var bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, Width, Height)
            : RestoreBounds;

        try
        {
            _preferencesStore.Save(new UiPreferences
            {
                WindowWidth = Math.Max(MinWidth, bounds.Width),
                WindowHeight = Math.Max(MinHeight, bounds.Height),
                IsMaximized = WindowState == WindowState.Maximized,
                IsAlwaysOnTop = Topmost,
                ViewModeIndex = ViewModeInput.SelectedIndex == 1 ? 1 : 0
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetStatus("Could not save window preferences.");
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        EnableDarkTitleBar();
    }

    private void EnableDarkTitleBar()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var enabled = 1;
        DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);

    private TaskItem? SelectedTask()
    {
        return TasksGrid.SelectedItem as TaskItem;
    }

    private void RefreshSelectedTaskPanel()
    {
        _isRefreshingTaskInfo = true;
        try
        {
            var task = SelectedTask();
            if (task is null)
            {
                SelectedTaskTitleInput.Text = "No task selected";
                SelectedTaskTitleInput.IsEnabled = false;
                SelectedTaskText.Text = "No task selected";
                SelectedTaskIdText.Text = "--";
                SelectedTaskStatusText.Text = "--";
                SelectedTaskPriorityText.Text = "--";
                ApplySelectedTaskPriorityStyle("low");
                SelectedTaskProgressText.Text = "0%";
                SelectedTaskHeaderProgressBar.Value = 0;
                MarkDoneButtonLabel.Text = "Mark done";
                MarkDoneButton.IsEnabled = false;
                DetailStatusInput.SelectedItem = TaskStatuses.Backlog;
                DetailPriorityInput.SelectedItem = "med";
                DetailProgressInput.Value = 0;
                DetailProgressValueText.Text = "0%";
                SubtaskCountText.Text = "0";
                AttachmentCountText.Text = "0";
                LogCountText.Text = "0";
                NotesInput.Clear();
                ProjectInput.Clear();
                ListInput.Clear();
                AssigneeInput.Clear();
                EstimateInput.Clear();
                TagsInput.Clear();
                SetDetailDueDate(null);
                BlockedByInput.Clear();
                SubtaskList.ItemsSource = null;
                AttachmentList.ItemsSource = null;
                LogList.ItemsSource = null;
                SetTaskInfoDirty(false);
                return;
            }

            SelectedTaskTitleInput.IsEnabled = true;
            SelectedTaskTitleInput.Text = task.Title;
            SelectedTaskIdText.Text = task.ShortId;
            SelectedTaskStatusText.Text = task.StatusDisplay;
            SelectedTaskPriorityText.Text = PriorityDisplay(task.Priority);
            ApplySelectedTaskPriorityStyle(task.Priority);
            SelectedTaskProgressText.Text = $"{task.ProgressPercent}%";
            SelectedTaskHeaderProgressBar.Value = task.ProgressPercent;
            var isDone = string.Equals(task.Status, TaskStatuses.Done, StringComparison.OrdinalIgnoreCase);
            MarkDoneButtonLabel.Text = isDone ? "Done" : "Mark done";
            MarkDoneButton.IsEnabled = !isDone;
            DetailStatusInput.SelectedItem = task.Status;
            DetailPriorityInput.SelectedItem = task.Priority;
            DetailProgressInput.Value = task.ProgressPercent;
            DetailProgressValueText.Text = $"{task.ProgressPercent}%";
            SelectedTaskText.Text = $"Updated {task.UpdatedAtLocal} by {task.UpdatedBy}";
            SubtaskCountText.Text = task.Subtasks.Count.ToString();
            AttachmentCountText.Text = task.Attachments.Count.ToString();
            LogCountText.Text = task.Logs.Count.ToString();
            NotesInput.Text = task.Notes;
            ProjectInput.Text = task.ProjectId;
            ListInput.Text = task.ListId;
            AssigneeInput.Text = task.Assignee;
            EstimateInput.Text = task.Estimate;
            TagsInput.Text = task.TagsText;
            SetDetailDueDate(task.DueDate?.LocalDateTime.Date);
            BlockedByInput.Text = string.Join(", ", task.BlockedByTaskIds);
            SubtaskList.ItemsSource = task.Subtasks;
            AttachmentList.ItemsSource = task.Attachments;
            LogList.ItemsSource = task.Logs;
            SetTaskInfoDirty(false);
        }
        finally
        {
            _isRefreshingTaskInfo = false;
        }
    }

    private void ApplySelectedTaskPriorityStyle(string priority)
    {
        var normalized = string.IsNullOrWhiteSpace(priority) ? "med" : priority.Trim().ToLowerInvariant();
        (string background, string flag, string text) colors = normalized switch
        {
            "emergen" or "urgent" => ("#5A1F24", "#FF6B6B", "#FFD1D1"),
            "high" => ("#573A12", "#F59E0B", "#FFE2A8"),
            "med" or "medium" => ("#143B42", "#2DD4BF", "#B8FFF5"),
            _ => ("#202631", "#8B93A3", "#A1A8B5")
        };

        SelectedTaskPriorityBadge.Background = (Brush)new BrushConverter().ConvertFromString(colors.background)!;
        SelectedTaskPriorityFlag.Foreground = (Brush)new BrushConverter().ConvertFromString(colors.flag)!;
        SelectedTaskPriorityText.Foreground = (Brush)new BrushConverter().ConvertFromString(colors.text)!;
    }

    private static string PriorityDisplay(string priority)
    {
        return priority.Trim().ToLowerInvariant() switch
        {
            "emergen" or "urgent" or "emergency" => "Emergency",
            "high" => "High",
            "med" or "medium" => "Medium",
            "low" => "Low",
            _ => "Medium"
        };
    }

    private void OpenTaskInfoPanel()
    {
        if (_isTaskInfoOpen)
        {
            return;
        }

        _isTaskInfoOpen = true;
        UpdateResponsivePanels();
        TaskInfoPanel.Visibility = Visibility.Visible;
        TaskInfoSpacerColumn.Width = new GridLength(14);
        SetCompactTaskColumns(true);
        TaskInfoPanel.Width = 0;
        TaskInfoPanel.Opacity = 0;
        AnimateTaskInfoPanel(TaskInfoDrawerWidth, 1);
    }

    private bool TrySaveTaskInfoChangesBeforeClose()
    {
        if (!_hasTaskInfoUnsavedChanges)
        {
            return true;
        }

        SaveDetailsButton_Click(this, new RoutedEventArgs());
        if (!_hasTaskInfoUnsavedChanges)
        {
            return true;
        }

        SetStatus("Save the current task details before closing Task Info.");
        return false;
    }

    private void CloseTaskInfoPanel(bool saveUnsavedChanges = true)
    {
        if (!_isTaskInfoOpen && TaskInfoPanel.Visibility != Visibility.Visible)
        {
            return;
        }

        if (saveUnsavedChanges && !TrySaveTaskInfoChangesBeforeClose())
        {
            return;
        }

        _isTaskInfoOpen = false;
        _openTaskInfoTaskId = null;
        SetTaskInfoDirty(false);
        var width = TaskInfoPanel.ActualWidth > 0 ? TaskInfoPanel.ActualWidth : TaskInfoDrawerWidth;
        TaskInfoPanel.Width = width;

        var widthAnimation = new DoubleAnimation
        {
            From = width,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
            FillBehavior = FillBehavior.Stop
        };
        var opacityAnimation = new DoubleAnimation
        {
            From = TaskInfoPanel.Opacity,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(140),
            FillBehavior = FillBehavior.Stop
        };

        widthAnimation.Completed += (_, _) =>
        {
            TaskInfoPanel.Width = 0;
            TaskInfoPanel.Opacity = 0;
            TaskInfoPanel.Visibility = Visibility.Collapsed;
            TaskInfoSpacerColumn.Width = new GridLength(0);
            SetCompactTaskColumns(false);
            UpdateResponsivePanels();
        };

        TaskInfoPanel.BeginAnimation(FrameworkElement.WidthProperty, widthAnimation);
        TaskInfoPanel.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
    }

    private void SetCompactTaskColumns(bool compact)
    {
        if (AssigneeColumn is null || DueDateColumn is null || CommentsColumn is null)
        {
            return;
        }

        AssigneeColumn.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        DueDateColumn.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        CommentsColumn.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateResponsivePanels()
    {
        if (FocusPanel is null || FocusColumn is null || FocusSpacerColumn is null)
        {
            return;
        }

        var collapseFocus = ActualWidth < FocusCompactThreshold ||
                            (_isTaskInfoOpen && ActualWidth < FocusCollapseThreshold);
        FocusPanel.Visibility = collapseFocus ? Visibility.Collapsed : Visibility.Visible;
        FocusColumn.Width = collapseFocus ? new GridLength(0) : new GridLength(300);
        FocusSpacerColumn.Width = collapseFocus ? new GridLength(0) : new GridLength(14);
    }

    private void AnimateTaskInfoPanel(double targetWidth, double targetOpacity)
    {
        var widthAnimation = new DoubleAnimation
        {
            From = TaskInfoPanel.ActualWidth,
            To = targetWidth,
            Duration = TimeSpan.FromMilliseconds(210),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
            FillBehavior = FillBehavior.Stop
        };
        var opacityAnimation = new DoubleAnimation
        {
            From = TaskInfoPanel.Opacity,
            To = targetOpacity,
            Duration = TimeSpan.FromMilliseconds(160),
            FillBehavior = FillBehavior.Stop
        };

        widthAnimation.Completed += (_, _) =>
        {
            TaskInfoPanel.Width = targetWidth;
            TaskInfoPanel.Opacity = targetOpacity;
        };

        TaskInfoPanel.BeginAnimation(FrameworkElement.WidthProperty, widthAnimation);
        TaskInfoPanel.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
    }

    private void RefreshFocusBoard()
    {
        var focus = _store.BuildFocus(_snapshot);
        NowList.ItemsSource = focus.Now;
        BlockedList.ItemsSource = focus.Blocked;
        DueList.ItemsSource = focus.Due;
        RecentList.ItemsSource = focus.Recent;
        NowHeader.Text = $"Now ({focus.Now.Count})";
        BlockedHeader.Text = $"Blocked ({focus.Blocked.Count})";
        DueHeader.Text = $"Due ({focus.Due.Count})";
        RecentHeader.Text = $"Recent ({focus.Recent.Count})";
        UpdateDashboardMetrics();
    }

    private void ConfigureTasksView()
    {
        _tasksView.Filter = item => item is TaskItem task && MatchesCurrentFilter(task);
        _tasksView.GroupDescriptions.Clear();
        _tasksView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(TaskItem.StatusDisplay)));
        _tasksView.SortDescriptions.Clear();
        _tasksView.SortDescriptions.Add(new SortDescription(nameof(TaskItem.StatusOrder), ListSortDirection.Ascending));
        _tasksView.SortDescriptions.Add(new SortDescription(nameof(TaskItem.UpdatedAt), ListSortDirection.Descending));
    }

    private void RefreshTaskViews()
    {
        _tasksView.Refresh();
        RefreshKanbanBoard();
        UpdateFilterResult();
    }

    private void RefreshKanbanBoard()
    {
        _todoKanbanTasks.Clear();
        _inProgressKanbanTasks.Clear();
        _doneKanbanTasks.Clear();
        _closeKanbanTasks.Clear();

        foreach (var task in _tasks.Where(MatchesCurrentFilter).OrderByDescending(task => task.UpdatedAt))
        {
            switch (task.KanbanStatus)
            {
                case "todo":
                    _todoKanbanTasks.Add(task);
                    break;
                case "in_progress":
                    _inProgressKanbanTasks.Add(task);
                    break;
                case "done":
                    _doneKanbanTasks.Add(task);
                    break;
                case "close":
                    _closeKanbanTasks.Add(task);
                    break;
            }
        }

        TodoKanbanCount.Text = _todoKanbanTasks.Count.ToString(CultureInfo.InvariantCulture);
        InProgressKanbanCount.Text = _inProgressKanbanTasks.Count.ToString(CultureInfo.InvariantCulture);
        DoneKanbanCount.Text = _doneKanbanTasks.Count.ToString(CultureInfo.InvariantCulture);
        CloseKanbanCount.Text = _closeKanbanTasks.Count.ToString(CultureInfo.InvariantCulture);
    }

    private void SaveTaskChange(TaskItem task, IEnumerable<string> changedFields)
    {
        var fields = changedFields.Where(field => !string.IsNullOrWhiteSpace(field)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (fields.Count == 0)
        {
            return;
        }

        var before = _lastAuditState.TryGetValue(task.Id, out var state)
            ? state
            : new Dictionary<string, object?>();

        _isSaving = true;
        try
        {
            _store.UpdateTask(_snapshot, task, UserActorName, UserActorType, fields, before);
            _lastAuditState[task.Id] = TaskStore.ToAuditDictionary(task);
            MarkSnapshotSaved();
            UpdateStatus();
        }
        finally
        {
            _isSaving = false;
        }
    }

    private void MarkSnapshotSaved()
    {
        _lastSnapshotWriteUtc = File.Exists(_store.SnapshotPath)
            ? File.GetLastWriteTimeUtc(_store.SnapshotPath)
            : DateTime.MinValue;
    }

    private void UpdateStatus()
    {
        UpdateFooterStatus("Workspace ready for user and AI updates.");
        UpdateDashboardMetrics();
    }

    private void SetStatus(string message)
    {
        UpdateFooterStatus(message);
        ShowToast(message);
        UpdateDashboardMetrics();
    }

    private void UpdateFooterStatus(string message)
    {
        StatusText.Text = message;
        FooterTaskCountText.Text = $"{_tasks.Count} tasks";
        FooterSaveStateText.Text = "Saved locally";
        FooterUpdatedText.Text = $"Updated {DateTime.Now:HH:mm}";
    }

    private void ShowToast(string message)
    {
        if (string.IsNullOrWhiteSpace(message) ||
            message.Equals("Workspace ready for user and AI updates.", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ToastMessageText.Text = message;
        ToastHost.Visibility = Visibility.Visible;
        _toastTimer.Stop();

        ToastHost.BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            From = ToastHost.Opacity,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(140),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        ToastTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
        {
            From = 10,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(160),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });

        _toastTimer.Start();
    }

    private void HideToast()
    {
        _toastTimer.Stop();
        var fade = new DoubleAnimation
        {
            From = ToastHost.Opacity,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
            FillBehavior = FillBehavior.Stop
        };
        fade.Completed += (_, _) =>
        {
            ToastHost.Opacity = 0;
            ToastHost.Visibility = Visibility.Collapsed;
            ToastTransform.Y = 10;
        };

        ToastHost.BeginAnimation(OpacityProperty, fade);
        ToastTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
        {
            From = 0,
            To = 8,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
            FillBehavior = FillBehavior.Stop
        });
    }

    private void UpdateDashboardMetrics()
    {
        var today = DateTimeOffset.Now.Date;
        var dueSoonCount = _tasks.Count(task => task.DueDate is not null && task.DueDate.Value.Date <= today.AddDays(2));
        var inProgressCount = _tasks.Count(task => task.Status == TaskStatuses.InProgress || task.Status == TaskStatuses.Review);
        var completedCount = _tasks.Count(task => task.Status == TaskStatuses.Done || task.Status == TaskStatuses.Cancelled);

        ActiveTasksMetric.Text = _tasks.Count.ToString();
        InProgressMetric.Text = inProgressCount.ToString();
        DueSoonMetric.Text = dueSoonCount.ToString();
        CompletedMetric.Text = completedCount.ToString();
    }

    private static void ApplyTextChange(Func<string> current, Action<string> apply, string next, string fieldName, List<string> changed)
    {
        var normalized = next.Trim();
        if (string.Equals(current(), normalized, StringComparison.Ordinal))
        {
            return;
        }

        apply(normalized);
        changed.Add(fieldName);
    }

    private static string ComboValue(ComboBox comboBox, string fallback)
    {
        return (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? fallback;
    }

    private static int ClampProgress(string value)
    {
        return int.TryParse(value, out var parsed) ? Math.Clamp(parsed, 0, 100) : 0;
    }

    private static DateTimeOffset? ParseDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(value.Trim(), out var parsed) ? parsed : null;
    }

    private static List<string> SplitCsv(string value)
    {
        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "task";
        }

        var chars = new List<char>();
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (char.IsUpper(current) && index > 0)
            {
                chars.Add('_');
            }
            chars.Add(char.ToLowerInvariant(current));
        }
        return new string(chars.ToArray());
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent is not null)
        {
            if (parent is T typedParent)
            {
                return typedParent;
            }

            parent = VisualTreeHelper.GetParent(parent);
        }

        return null;
    }

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T typedChild)
            {
                yield return typedChild;
            }

            foreach (var descendant in FindVisualDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static FrameworkElement? FindParentWithContextMenu(DependencyObject child)
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent is not null)
        {
            if (parent is FrameworkElement { ContextMenu: not null } element)
            {
                return element;
            }

            parent = VisualTreeHelper.GetParent(parent);
        }

        return null;
    }
}
