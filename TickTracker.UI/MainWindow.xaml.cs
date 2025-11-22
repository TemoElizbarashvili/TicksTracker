using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TickTracker.UI.Helpers;
using TickTracker.Utils;
using TickTracker.Utils.Data;
using TickTracker.Utils.Helpers;
using TickTracker.Utils.Models;

namespace TickTracker.UI;

public partial class MainWindow : Window
{
    private readonly List<AppUsageSummary> _allData = new();
    private readonly List<AppUsageSummary> _filteredData = new();
    private readonly ObservableCollection<AppUsageSummary> _visibleData = new();
    private readonly ObservableCollection<AppUsageSummary> _rangeData = new();
    private readonly ObservableCollection<string> _blacklistedApps = new();
    private bool _isLoading;
    private const int PageSize = 20;
    private int _loadedCount;
    public double MaxTotalSeconds { get; private set; }
    private bool _suppressSettingsSave;
    private string _currentTheme = "Light";
    private string _currentSortMember = "TotalSeconds";
    private ListSortDirection _currentSortDirection = ListSortDirection.Descending;

    public MainWindow()
    {
        _suppressSettingsSave = true;

        InitializeComponent();

        UsageGrid.ItemsSource = _visibleData;
        RangeGrid.ItemsSource = _rangeData;
        BlacklistGrid.ItemsSource = _blacklistedApps;

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;

        LoadSettings();

        _suppressSettingsSave = false;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadDataAsync();
        await LoadBlacklistAsync();
    }

    private async Task LoadDataAsync()
    {
        if (_isLoading) return;

        _isLoading = true;
        SummaryText.Text = "Loading usage data...";

        try
        {
            var allSummaries = await Task.Run(DbOperations.GetAllUsageSummaries);
            _allData.Clear();
            _allData.AddRange(allSummaries);

            UpdateSummary();
            ApplyFilters();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Failed to load usage data:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            _allData.Clear();
            _visibleData.Clear();
            SummaryText.Text = "Failed to load data.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task LoadBlacklistAsync()
    {
        var names = await Task.Run(DbOperations.GetBlacklistedAppNames);
        _blacklistedApps.Clear();
        foreach (var name in names) _blacklistedApps.Add(name);
    }

    private void OptionsButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: not null } btn)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.IsOpen = true;
        }
    }

    private async void DoNotTrackMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;

        var contextMenu = menuItem.Parent as ContextMenu;
        if (contextMenu?.PlacementTarget is not Button button) return;

        if (button.Tag is not AppUsageSummary summary) return;

        var result = MessageBox.Show(this,
            $"Are you sure you want to stop tracking '{summary.ProcessName}'?\n\nAll existing history for this app will be deleted.",
            "Stop tracking app",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        await Task.Run(() => DbOperations.BlacklistApp(summary.ProcessName));
        await LoadDataAsync();
        await LoadBlacklistAsync();
    }

    private async void UnblockButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { CommandParameter: string processName })
        {
            var result = MessageBox.Show(this,
                $"Allow tracking for '{processName}' again?",
                "Unblock app",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            await DbOperations.RemoveItemFromBlackListAsync(processName);
            await LoadDataAsync();
            await LoadBlacklistAsync();
        }
    }


    #region --- FILTERS & DATA LOGIC ---

    private void ApplyFilters()
    {
        if (!IsLoaded) return;

        var search = SearchBox.Text?.Trim() ?? string.Empty;
        double.TryParse(MinMinutesBox.Text, out var minMinutes);
        int.TryParse(MinSessionsBox.Text, out var minSessions);
        var minSeconds = minMinutes * 60;
        var minLastUsedUtc = GetMinLastUsedUtcFilter();

        var query = _allData.AsEnumerable();

        if (minSeconds > 0) query = query.Where(x => x.TotalSeconds >= minSeconds);
        if (minSessions > 0) query = query.Where(x => x.SessionCount >= minSessions);
        if (!string.IsNullOrEmpty(search)) query = query.Where(x => x.ProcessName.Contains(search, StringComparison.OrdinalIgnoreCase));
        if (minLastUsedUtc.HasValue) query = query.Where(x => x.LastSeenUtc.HasValue && x.LastSeenUtc.Value >= minLastUsedUtc.Value);

        var filtered = ApplySort(query).ToList();

        _filteredData.Clear();
        _filteredData.AddRange(filtered);

        _visibleData.Clear();
        _loadedCount = 0;
        LoadNextPage();
    }

    private void LoadNextPage()
    {
        var remaining = _filteredData.Count - _loadedCount;
        if (remaining <= 0)
        {
            LoadMoreButton.Visibility = Visibility.Collapsed;
            SummaryText.Text = _allData.Count == 0 ? "No data loaded." : $"Showing {_visibleData.Count} of {_filteredData.Count} apps";
            return;
        }

        var nextItems = _filteredData.Skip(_loadedCount).Take(PageSize);
        foreach (var item in nextItems) _visibleData.Add(item);

        _loadedCount = _visibleData.Count;

        var chartTop = _filteredData.OrderByDescending(x => x.TotalSeconds).Take(10).ToList();
        MaxTotalSeconds = chartTop.Count == 0 ? 0 : chartTop.Max(x => x.TotalSeconds);
        ChartItems.ItemsSource = chartTop;

        SummaryText.Text = $"Showing {_visibleData.Count} of {_filteredData.Count} apps";
        LoadMoreButton.Visibility = _loadedCount < _filteredData.Count ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateSummary()
    {
        if (_allData.Count == 0)
        {
            SummaryTotalTimeText.Text = "0.0 minutes";
            SummaryAppCountText.Text = "0 apps";
            SummaryTopAppText.Text = "-";
            return;
        }

        var totalSeconds = _allData.Sum(x => x.TotalSeconds);
        SummaryTotalTimeText.Text = totalSeconds >= 3600 ? $"{totalSeconds / 3600d:0.0} hours" : $"{totalSeconds / 60d:0.0} minutes";
        SummaryAppCountText.Text = $"{_allData.Count} apps";
        var top = _allData.MaxBy(x => x.TotalSeconds);
        SummaryTopAppText.Text = top == null ? "-" : $"{top.ProcessName} ({top.TotalHoursDisplay})";
    }

    private DateTime? GetMinLastUsedUtcFilter()
    {
        if (LastUsedCombo?.SelectedItem is not ComboBoxItem { Tag: string tag }) return null;
        return tag switch
        {
            "Today" => DateTime.UtcNow.Date,
            "7" => DateTime.UtcNow.AddDays(-7),
            "30" => DateTime.UtcNow.AddDays(-30),
            _ => null
        };
    }

    #endregion

    #region --- UI HELPERS ---

    private void HeaderBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            if (e.ClickCount == 2) MaximizeButton_OnClick(sender, e);
            else DragMove();
        }
    }

    private void MinimizeButton_OnClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeButton_OnClick(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void CloseButton_OnClick(object sender, RoutedEventArgs e) => Close();

    private void LoadMoreButton_OnClick(object sender, RoutedEventArgs e) => LoadNextPage();
    private void SearchBox_OnTextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();
    private void MinMinutesBox_OnTextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();
    private void MinSessionsBox_OnTextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();
    private void LastUsedCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilters();

    private void ThemeCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeCombo?.SelectedItem is ComboBoxItem { Tag: string tag })
        {
            ThemeConfiguration.ApplyTheme(tag);
            _currentTheme = tag;
        }
    }

    private void UsageGrid_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!e.Handled && VisualTreeHelper.GetParent((DependencyObject)sender) is UIElement parent)
        {
            var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = UIElement.MouseWheelEvent, Source = sender };
            parent.RaiseEvent(eventArg);
            e.Handled = true;
        }
    }

    private IEnumerable<AppUsageSummary> ApplySort(IEnumerable<AppUsageSummary> query)
    {
        return _currentSortMember switch
        {
            "ProcessName" => _currentSortDirection == ListSortDirection.Ascending ? query.OrderBy(x => x.ProcessName) : query.OrderByDescending(x => x.ProcessName),
            "SessionCount" => _currentSortDirection == ListSortDirection.Ascending ? query.OrderBy(x => x.SessionCount) : query.OrderByDescending(x => x.SessionCount),
            "FirstSeenUtc" => _currentSortDirection == ListSortDirection.Ascending ? query.OrderBy(x => x.FirstSeenUtc) : query.OrderByDescending(x => x.FirstSeenUtc),
            "LastSeenUtc" => _currentSortDirection == ListSortDirection.Ascending ? query.OrderBy(x => x.LastSeenUtc) : query.OrderByDescending(x => x.LastSeenUtc),
            "TotalSeconds" => _currentSortDirection == ListSortDirection.Ascending ? query.OrderBy(x => x.TotalSeconds) : query.OrderByDescending(x => x.TotalSeconds),
            _ => query.OrderByDescending(x => x.TotalSeconds)
        };
    }

    private void UsageGrid_OnSorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        var column = e.Column;
        var sortMemberPath = column.SortMemberPath ?? "TotalSeconds";
        var direction = column.SortDirection == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;

        _currentSortMember = sortMemberPath;
        _currentSortDirection = direction;

        foreach (var col in UsageGrid.Columns) if (!ReferenceEquals(col, column)) col.SortDirection = null;
        column.SortDirection = direction;

        ApplyFilters();
    }

    private void RefreshButton_OnClick(object sender, RoutedEventArgs e) => _ = LoadDataAsync();

    #endregion

    #region --- DATE RANGE LOGIC ---

    private async Task LoadRangeDataAsync()
    {
        if (RangeFromPicker.SelectedDate is not { } fromDate) return;

        var toDate = RangeToPicker.SelectedDate;
        var search = RangeSearchBox.Text.Trim();
        var useRange = RangeIsSpanCheckBox.IsChecked == true;
        var fromDateOnly = DateOnly.FromDateTime(fromDate.Date);
        var toDateOnly = useRange && toDate.HasValue ? DateOnly.FromDateTime(toDate.Value.Date) : fromDateOnly;

        var summaries = await Task.Run(() =>
        {
            var intervals = DbOperations.Query<AppUsageInterval>(x => x.ProcessUsingDate >= fromDateOnly && x.ProcessUsingDate <= toDateOnly);
            var grouped = intervals.GroupBy(i => i.ProcessName)
                .Select(g => new AppUsageSummary
                {
                    ProcessName = g.Key,
                    TotalSeconds = g.Sum(i => (i.EndUtc - i.StartUtc).TotalSeconds),
                    FirstSeenUtc = g.Min(i => i.StartUtc),
                    LastSeenUtc = g.Max(i => i.EndUtc),
                    SessionCount = g.Count()
                }).ToList();

            if (!string.IsNullOrEmpty(search)) grouped = grouped.Where(x => x.ProcessName.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
            return grouped.OrderByDescending(x => x.TotalSeconds).ToList();
        });

        _rangeData.Clear();
        foreach (var s in summaries) _rangeData.Add(s);
    }

    private void RangeFromPicker_OnSelectedDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (RangeIsSpanCheckBox.IsChecked != true && RangeFromPicker.SelectedDate.HasValue) RangeToPicker.SelectedDate = RangeFromPicker.SelectedDate;
        _ = LoadRangeDataAsync();
    }
    private void RangeToPicker_OnSelectedDateChanged(object? sender, SelectionChangedEventArgs e) { if (RangeIsSpanCheckBox.IsChecked == true) _ = LoadRangeDataAsync(); }
    private void RangeSearchBox_OnTextChanged(object sender, TextChangedEventArgs e) => _ = LoadRangeDataAsync();
    private void RangeIsSpanCheckBox_OnCheckedChanged(object? sender, RoutedEventArgs e)
    {
        var useRange = RangeIsSpanCheckBox.IsChecked == true;
        RangeToPicker.IsEnabled = useRange;
        if (!useRange && RangeFromPicker.SelectedDate.HasValue) RangeToPicker.SelectedDate = RangeFromPicker.SelectedDate;
    }

    #endregion

    #region --- SETTINGS LOGIC ---


    private void LoadSettings()
    {
        try
        {
            var retentionDays = int.TryParse(DbOperations.GetFromAppSettings(Constants.RetentionDaysKey), out var r)
                ? r
                : 90;
            var pollSeconds = int.TryParse(DbOperations.GetFromAppSettings(Constants.PollSecondsKey), out var p)
                ? p
                : 2;
            var theme = DbOperations.GetFromAppSettings(Constants.ThemeKey) ?? "Light";
            var ignoreWindows =
                !bool.TryParse(DbOperations.GetFromAppSettings(Constants.IgnoreWindowsAppsKey), out var i) || i;

            _suppressSettingsSave = true;
            RetentionDaysBox.Text = retentionDays.ToString();

            var pollTag = pollSeconds.ToString();
            AccuracyCombo.SelectedItem =
                AccuracyCombo.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == pollTag) ??
                AccuracyCombo.Items[1];

            var themeItem = ThemeCombo.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == theme);
            if (themeItem != null)
            {
                ThemeCombo.SelectedItem = themeItem;
                ThemeConfiguration.ApplyTheme(theme);
                _currentTheme = theme;
            }

            IgnoreWindowsAppsCheckBox.IsChecked = ignoreWindows;
            UpdateConfigText(retentionDays, pollSeconds, ignoreWindows);
        }
        catch
        {
            // Ignore errors
        }
        finally { _suppressSettingsSave = false; }
    }

    private void SaveSettings(int? retentionDays = null, int? pollSeconds = null, bool? ignoreWindows = null)
    {
        if (_suppressSettingsSave) return;

        var days = retentionDays ?? (int.TryParse(RetentionDaysBox.Text, out var d) ? d : 90);
        var seconds = pollSeconds ?? (AccuracyCombo.SelectedItem is ComboBoxItem { Tag: string s } && int.TryParse(s, out var p) ? p : 2);
        var ignore = ignoreWindows ?? (IgnoreWindowsAppsCheckBox.IsChecked == true);

        try
        {
            DbOperations.SetInAppSettings(Constants.RetentionDaysKey, days.ToString());
            DbOperations.SetInAppSettings(Constants.PollSecondsKey, seconds.ToString());
            DbOperations.SetInAppSettings(Constants.IgnoreWindowsAppsKey, ignore.ToString().ToLower());
        }
        catch
        {
            // Ignore errors
        }

        UpdateConfigText(days, seconds, ignore);
    }

    private void UpdateConfigText(int days, int seconds, bool ignore)
    {
        if (CurrentConfigText != null)
            CurrentConfigText.Text = $"Current config: {seconds}s polling, {days} days retention, {(ignore ? "ignore" : "track")} Windows apps.";
    }

    private void RetentionDaysBox_OnLostFocus(object sender, RoutedEventArgs e) => SaveSettings(retentionDays: int.TryParse(RetentionDaysBox.Text, out var d) && d > 0 ? d : 90);
    private void AccuracyCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e) => SaveSettings();
    private void IgnoreWindowsAppsCheckBox_OnClick(object sender, RoutedEventArgs e) => SaveSettings();
    private void RetentionDaysBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e) => e.Handled = e.Text.Any(ch => !char.IsDigit(ch));
    private void MainWindow_Closing(object? sender, CancelEventArgs e) => ThemeConfiguration.SaveTheme(_currentTheme);

    #endregion
}