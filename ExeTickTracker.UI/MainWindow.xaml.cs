using ExeTicksTracker.Data;
using ExeTickTracker.UI.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;
using ExeTickTracker.UI.Helpers;

namespace ExeTickTracker.UI;

public partial class MainWindow : Window
{
    private readonly List<AppUsageSummary> _allData = new();
    private readonly List<AppUsageSummary> _filteredData = new();
    private readonly ObservableCollection<AppUsageSummary> _visibleData = new();
    private readonly ObservableCollection<AppUsageSummary> _rangeData = new();
    private bool _isLoading;
    private const int PageSize = 20;
    private int _loadedCount;
    public double MaxTotalSeconds { get; private set; }
    private bool _suppressSettingsSave;
    private string _currentTheme = "Light";

    public MainWindow()
    {
        InitializeComponent();
        UsageGrid.ItemsSource = _visibleData;
        RangeGrid.ItemsSource = _rangeData;

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;

        LoadSettings();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        if (_isLoading)
        {
            return;
        }

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
            MessageBox.Show(this,
                "Failed to load usage data:\n" + ex.Message,
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            _allData.Clear();
            _visibleData.Clear();
            SummaryText.Text = "Failed to load data.";
            SummaryTotalTimeText.Text = "0.0 minutes";
            SummaryAppCountText.Text = "0 apps";
            SummaryTopAppText.Text = "-";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ApplyFilters()
    {
        if (!IsLoaded)
        {
            return;
        }

        var search = SearchBox.Text?.Trim() ?? string.Empty;
        var minSessions = 0;

        if (!double.TryParse(MinMinutesBox.Text, out var minMinutes))
        {
            minMinutes = 0;
        }

        if (int.TryParse(MinSessionsBox.Text, out var parsedSessions))
        {
            minSessions = parsedSessions;
        }

        var minSeconds = minMinutes * 60;
        var minLastUsedUtc = GetMinLastUsedUtcFilter();

        var query = _allData.AsEnumerable();

        if (minSeconds > 0)
        {
            query = query.Where(x => x.TotalSeconds >= minSeconds);
        }

        if (minSessions > 0)
        {
            query = query.Where(x => x.SessionCount >= minSessions);
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(x => x.ProcessName.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (minLastUsedUtc.HasValue)
        {
            var minUtc = minLastUsedUtc.Value;
            query = query.Where(x => x.LastSeenUtc.HasValue && x.LastSeenUtc.Value >= minUtc);
        }

        var filtered = query
            .OrderByDescending(x => x.TotalSeconds)
            .ToList();

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
            SummaryText.Text = _allData.Count == 0
                ? "No data loaded."
                : $"Showing {_visibleData.Count} of {_filteredData.Count} apps";
            return;
        }

        var toTake = Math.Min(PageSize, remaining);
        var nextItems = _filteredData
            .Skip(_loadedCount)
            .Take(toTake);

        foreach (var item in nextItems)
        {
            _visibleData.Add(item);
        }

        _loadedCount = _visibleData.Count;

        var chartTop = _filteredData.Take(10).ToList();
        MaxTotalSeconds = chartTop.Count == 0
            ? 0
            : chartTop.Max(x => x.TotalSeconds);
        ChartItems.ItemsSource = chartTop;

        SummaryText.Text = _allData.Count == 0
            ? "No data loaded."
            : $"Showing {_visibleData.Count} of {_filteredData.Count} apps";

        LoadMoreButton.Visibility = _loadedCount < _filteredData.Count
            ? Visibility.Visible
            : Visibility.Collapsed;
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
        SummaryTotalTimeText.Text = totalSeconds >= 3600 ? $"{totalSeconds / 3600d:0.0} hours" : 
            $"{totalSeconds / 60d:0.0} minutes";

        SummaryAppCountText.Text = $"{_allData.Count} apps";

        var top = _allData.MaxBy(x => x.TotalSeconds);

        SummaryTopAppText.Text = top == null
            ? "-"
            : $"{top.ProcessName} ({top.TotalHoursDisplay})";
    }

    private DateTime? GetMinLastUsedUtcFilter()
    {
        if (LastUsedCombo?.SelectedItem is not ComboBoxItem comboItem)
        {
            return null;
        }

        if (comboItem.Tag is not string tag)
        {
            return null;
        }

        var now = DateTime.UtcNow;

        return tag switch
        {
            "Today" => DateTime.UtcNow.Date,
            "7" => now.AddDays(-7),
            "30" => now.AddDays(-30),
            _ => null
        };
    }

    private void LoadMoreButton_OnClick(object sender, RoutedEventArgs e)
    {
        LoadNextPage();
    }

    private void SearchBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void MinMinutesBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void MinSessionsBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void LastUsedCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void ThemeCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeCombo?.SelectedItem is ComboBoxItem { Tag: string tag })
        {
            ThemeConfiguration.ApplyTheme(tag);
            _currentTheme = tag;
        }
    }
  
    private void LoadSettings()
    {
        try
        {

            var retentionDays = 90;
            var pollSeconds = 2;
            var theme = "Light";

            var retentionSetting = DbOperations.GetFromAppSettings("RetentionDays");
            if (retentionSetting != null && int.TryParse(retentionSetting, out var parsedRetention) && parsedRetention > 0)
            {
                retentionDays = parsedRetention;
            }

            var pollSetting = DbOperations.GetFromAppSettings("PollSeconds");
            if (pollSetting != null && int.TryParse(pollSetting, out var parsedPoll) && parsedPoll >= 1 && parsedPoll <= 10)
            {
                pollSeconds = parsedPoll;
            }

            var themeSetting = DbOperations.GetFromAppSettings("Theme");
            if (themeSetting is not null & themeSetting is "Light" or "Dark" or "Night")
            {
                theme = themeSetting;
            }

            _suppressSettingsSave = true;

            RetentionDaysBox.Text = retentionDays.ToString();

            var targetTag = pollSeconds.ToString();
            var selected = AccuracyCombo.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(i => (string?)i.Tag == targetTag);
            if (selected != null)
            {
                AccuracyCombo.SelectedItem = selected;
            }
            else
            {
                AccuracyCombo.SelectedIndex = 1;
            }

            var themeItem = ThemeCombo.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(i => (string?)i.Tag == theme);
            if (themeItem != null)
            {
                ThemeCombo.SelectedItem = themeItem;
                ThemeConfiguration.ApplyTheme(theme!);
                _currentTheme = theme!;
            }

            UpdateConfigText(retentionDays, pollSeconds);
        }
        catch
        {
            // Ignore settings load errors, Use Default
        }
        finally
        {
            _suppressSettingsSave = false;
        }
    }

    private void SaveSettings(int? retentionDaysOverride = null, int? pollSecondsOverride = null)
    {
        if (_suppressSettingsSave)
        {
            return;
        }

        var days = retentionDaysOverride ?? (int.TryParse(RetentionDaysBox.Text, out var parsedDays) && parsedDays > 0
            ? parsedDays
            : 90);
        days = Math.Max(1, days);

        var seconds = pollSecondsOverride ?? GetPollingSecondsFromUi();
        seconds = Math.Clamp(seconds, 1, 10);

        try
        {
            DbOperations.SetInAppSettings(Constants.RetentionDaysKey, days.ToString());
            DbOperations.SetInAppSettings(Constants.PollSecondsKey, seconds.ToString());
        }
        catch
        {
            // Ignore settings save errors; tracker will just use defaults
        }

        UpdateConfigText(days, seconds);
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentTheme))
        {
            ThemeConfiguration.SaveTheme(_currentTheme!);
        }
    }

    private int GetPollingSecondsFromUi()
    {
        if (AccuracyCombo.SelectedItem is ComboBoxItem { Tag: string tag } &&
            int.TryParse(tag, out var pollSeconds))
        {
            return pollSeconds;
        }

        return 2;
    }

    private void UpdateConfigText(int retentionDays, int pollSeconds)
    {
        if (CurrentConfigText == null)
        {
            return;
        }

        CurrentConfigText.Text = $"Current tracker config: {pollSeconds} seconds polling, {retentionDays} days retention.";
    }

    private void RetentionDaysBox_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(RetentionDaysBox.Text, out var days) || days <= 0)
        {
            // revert to default 90 if invalid
            RetentionDaysBox.Text = "90";
            days = 90;
        }

        SaveSettings(retentionDaysOverride: days, pollSecondsOverride: null);
    }

    private void AccuracyCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSettingsSave)
        {
            return;
        }

        if (AccuracyCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag && int.TryParse(tag, out var pollSeconds))
        {
            SaveSettings(retentionDaysOverride: null, pollSecondsOverride: pollSeconds);
        }
    }

    private void RetentionDaysBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = e.Text.Any(ch => !char.IsDigit(ch));
    }

    private async void RefreshButton_OnClick(object sender, RoutedEventArgs e)
    {
        await LoadDataAsync();
    }

    private async Task LoadRangeDataAsync()
    {
        if (RangeFromPicker.SelectedDate is not { } fromDate)
        {
            MessageBox.Show(this,
                "Please select at least a start date.",
                "Date required",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var toDate = RangeToPicker.SelectedDate;
        var search = RangeSearchBox.Text.Trim();
        var useRange = RangeIsSpanCheckBox.IsChecked == true;

        var fromDateOnly = DateOnly.FromDateTime(fromDate.Date);
        DateOnly? toDateOnly = useRange && toDate.HasValue
            ? DateOnly.FromDateTime(toDate.Value.Date)
            : fromDateOnly;

        var summaries = await Task.Run(() =>
        {
            var from = fromDateOnly;
            var to = toDateOnly;

            var intervals = DbOperations.Query<AppUsageInterval>(
                x => x.ProcessUsingDate >= from && x.ProcessUsingDate <= to.Value);

            var grouped = intervals
                .GroupBy(i => i.ProcessName)
                .Select(g =>
                {
                    var totalSeconds = g.Sum(i => (i.EndUtc - i.StartUtc).TotalSeconds);
                    var firstSeen = g.Min(i => i.StartUtc);
                    var lastSeen = g.Max(i => i.EndUtc);
                    var sessionCount = g.Count();

                    return new AppUsageSummary
                    {
                        ProcessName = g.Key,
                        TotalSeconds = totalSeconds,
                        FirstSeenUtc = firstSeen,
                        LastSeenUtc = lastSeen,
                        SessionCount = sessionCount
                    };
                })
                .ToList();

              if (!string.IsNullOrEmpty(search))
              {
                  grouped = grouped
                      .Where(x => x.ProcessName.Contains(search, StringComparison.OrdinalIgnoreCase))
                      .ToList();
              }

            return grouped
                .OrderByDescending(x => x.TotalSeconds)
                .ToList();
        });

        _rangeData.Clear();
        foreach (var summary in summaries)
        {
            _rangeData.Add(summary);
        }
    }

    private async void RangeFromPicker_OnSelectedDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (RangeIsSpanCheckBox.IsChecked != true && RangeFromPicker.SelectedDate.HasValue)
        {
            RangeToPicker.SelectedDate = RangeFromPicker.SelectedDate;
        }

        await LoadRangeDataAsync();
    }

    private async void RangeToPicker_OnSelectedDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (RangeIsSpanCheckBox.IsChecked == true)
        {
            await LoadRangeDataAsync();
        }
    }

    private async void RangeSearchBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        await LoadRangeDataAsync();
    }

    private void RangeIsSpanCheckBox_OnCheckedChanged(object? sender, RoutedEventArgs e)
    {
        var useRange = RangeIsSpanCheckBox.IsChecked == true;
        RangeToPicker.IsEnabled = useRange;

        switch (useRange)
        {
            case false when RangeFromPicker.SelectedDate.HasValue:
            case true when RangeFromPicker.SelectedDate.HasValue && !RangeToPicker.SelectedDate.HasValue:
                RangeToPicker.SelectedDate = RangeFromPicker.SelectedDate;
                break;
        }
    }

    private void UsageGrid_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        if (VisualTreeHelper.GetParent((DependencyObject)sender) is UIElement parent)
        {
            var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender
            };
            parent.RaiseEvent(eventArg);
            e.Handled = true;
        }
    }

    private void UsageGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (UsageGrid.SelectedItem is AppUsageSummary summary)
        {
            MessageBox.Show(this,
                $"Process: {summary.ProcessName}\n" +
                $"Total time: {summary.TotalTimeFormatted}\n" +
                $"Sessions: {summary.SessionCount}\n" +
                $"First seen: {summary.FirstSeenLocal}\n" +
                $"Last seen: {summary.LastSeenLocal}",
                "App usage details",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
