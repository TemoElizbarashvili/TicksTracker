using ExeTicksTracker.Data;
using ExeTickTracker.UI.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Linq;
using System.Windows.Media;

namespace ExeTickTracker.UI;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly List<AppUsageSummary> _allData = new();
    private readonly ObservableCollection<AppUsageSummary> _visibleData = new();
    private bool _isLoading;
    public double MaxTotalSeconds { get; private set; }
    private bool _suppressSettingsSave;

    public MainWindow()
    {
        InitializeComponent();
        UsageGrid.ItemsSource = _visibleData;

        Loaded += MainWindow_Loaded;

        ApplyTheme("Light");
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
            var grouped = await Task.Run(() =>
            {
                using var db = new UsageDbContext();

                var aggregates = db.AppUsageAggregates
                    .AsNoTracking()
                    .ToList();

                var intervalGroups = db.AppUsageIntervals
                    .AsNoTracking()
                    .ToList()
                    .GroupBy(x => x.ProcessName);

                var summaries = new Dictionary<string, AppUsageSummary>(StringComparer.OrdinalIgnoreCase);

                // Seed from aggregates (older, rolled-up data)
                foreach (var agg in aggregates)
                {
                    summaries[agg.ProcessName] = new AppUsageSummary
                    {
                        ProcessName = agg.ProcessName,
                        TotalSeconds = agg.TotalSeconds,
                        SessionCount = agg.SessionCount,
                        FirstSeenUtc = agg.FirstSeenUtc,
                        LastSeenUtc = agg.LastSeenUtc
                    };
                }

                // Add recent intervals (last ~90 days)
                foreach (var group in intervalGroups)
                {
                    var processName = group.Key;
                    var totalSeconds = group.Sum(i => (i.EndUtc - i.StartUtc).TotalSeconds);
                    var firstSeen = group.Min(i => i.StartUtc);
                    var lastSeen = group.Max(i => i.EndUtc);
                    var sessionCount = group.Count();

                    if (summaries.TryGetValue(processName, out var existing))
                    {
                        existing.TotalSeconds += totalSeconds;
                        existing.SessionCount += sessionCount;

                        if (!existing.FirstSeenUtc.HasValue || firstSeen < existing.FirstSeenUtc.Value)
                        {
                            existing.FirstSeenUtc = firstSeen;
                        }

                        if (!existing.LastSeenUtc.HasValue || lastSeen > existing.LastSeenUtc.Value)
                        {
                            existing.LastSeenUtc = lastSeen;
                        }
                    }
                    else
                    {
                        summaries[processName] = new AppUsageSummary
                        {
                            ProcessName = processName,
                            TotalSeconds = totalSeconds,
                            SessionCount = sessionCount,
                            FirstSeenUtc = firstSeen,
                            LastSeenUtc = lastSeen
                        };
                    }
                }

                return summaries.Values
                    .OrderByDescending(x => x.TotalSeconds)
                    .ToList();
            });

            _allData.Clear();
            _allData.AddRange(grouped);

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
            SummaryTotalTimeText.Text = "0.0 h";
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

        _visibleData.Clear();
        foreach (var item in filtered)
        {
            _visibleData.Add(item);
        }

        var chartTop = filtered.Take(10).ToList();
        MaxTotalSeconds = chartTop.Count == 0
            ? 0
            : chartTop.Max(x => x.TotalSeconds);
        ChartItems.ItemsSource = chartTop;

        SummaryText.Text = _allData.Count == 0
            ? "No data loaded."
            : $"Showing {filtered.Count} of {_allData.Count} apps";
    }

    private void UpdateSummary()
    {
        if (_allData.Count == 0)
        {
            SummaryTotalTimeText.Text = "0.0 h";
            SummaryAppCountText.Text = "0 apps";
            SummaryTopAppText.Text = "-";
            return;
        }

        var totalSeconds = _allData.Sum(x => x.TotalSeconds);
        SummaryTotalTimeText.Text = $"{totalSeconds / 3600d:0.0} h";

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
            ApplyTheme(tag);
        }
    }

    private static void SetBrush(ResourceDictionary resources, string key, Color color)
    {
        resources[key] = new SolidColorBrush(color);
    }

    private void ApplyTheme(string theme)
    {
        var resources = Application.Current.Resources;
        Color windowBackground;
        Color contentBackground;
        Color primaryText;
        Color accent;

        switch (theme)
        {
            case "Dark":
                windowBackground = Color.FromRgb(0x18, 0x18, 0x18);
                contentBackground = Color.FromRgb(0x20, 0x20, 0x20);
                primaryText = Colors.White;
                accent = Color.FromRgb(0x3B, 0x82, 0xF6);

                SetBrush(resources, "AppWindowBackgroundBrush", windowBackground);
                SetBrush(resources, "AppHeaderBackgroundBrush", Color.FromRgb(0x22, 0x22, 0x22));
                SetBrush(resources, "AppContentBackgroundBrush", contentBackground);
                SetBrush(resources, "AppBorderBrush", Color.FromRgb(0x33, 0x33, 0x33));
                SetBrush(resources, "AppRowBackgroundBrush", contentBackground);
                SetBrush(resources, "AppRowAlternateBackgroundBrush", Color.FromRgb(0x27, 0x27, 0x27));
                SetBrush(resources, "AppPrimaryTextBrush", primaryText);
                SetBrush(resources, "AppSecondaryTextBrush", Color.FromRgb(0xAA, 0xAA, 0xAA));
                SetBrush(resources, "AppAccentBrush", accent);
                break;

            case "Night":
                windowBackground = Color.FromRgb(0x0B, 0x0B, 0x0F);
                contentBackground = Color.FromRgb(0x12, 0x12, 0x18);
                primaryText = Colors.White;
                accent = Color.FromRgb(0xFB, 0x92, 0x3B);

                SetBrush(resources, "AppWindowBackgroundBrush", windowBackground);
                SetBrush(resources, "AppHeaderBackgroundBrush", Color.FromRgb(0x14, 0x14, 0x1A));
                SetBrush(resources, "AppContentBackgroundBrush", contentBackground);
                SetBrush(resources, "AppBorderBrush", Color.FromRgb(0x26, 0x26, 0x30));
                SetBrush(resources, "AppRowBackgroundBrush", contentBackground);
                SetBrush(resources, "AppRowAlternateBackgroundBrush", Color.FromRgb(0x1A, 0x1A, 0x24));
                SetBrush(resources, "AppPrimaryTextBrush", primaryText);
                SetBrush(resources, "AppSecondaryTextBrush", Color.FromRgb(0x99, 0x99, 0xAA));
                SetBrush(resources, "AppAccentBrush", accent);
                break;

            default: // Light
                windowBackground = Color.FromRgb(0xF2, 0xF2, 0xF2);
                contentBackground = Colors.White;
                primaryText = Color.FromRgb(0x11, 0x11, 0x11);
                accent = Color.FromRgb(0x3B, 0x82, 0xF6);

                SetBrush(resources, "AppWindowBackgroundBrush", windowBackground);
                SetBrush(resources, "AppHeaderBackgroundBrush", Colors.White);
                SetBrush(resources, "AppContentBackgroundBrush", contentBackground);
                SetBrush(resources, "AppBorderBrush", Color.FromRgb(0xDD, 0xDD, 0xDD));
                SetBrush(resources, "AppRowBackgroundBrush", contentBackground);
                SetBrush(resources, "AppRowAlternateBackgroundBrush", Color.FromRgb(0xF5, 0xF7, 0xFB));
                SetBrush(resources, "AppPrimaryTextBrush", primaryText);
                SetBrush(resources, "AppSecondaryTextBrush", Color.FromRgb(0x66, 0x66, 0x66));
                SetBrush(resources, "AppAccentBrush", accent);
                break;
        }

        // Keep system colors roughly aligned so built-in templates (e.g. combo popups) match the theme
        resources[SystemColors.WindowBrushKey] = new SolidColorBrush(contentBackground);
        resources[SystemColors.ControlBrushKey] = new SolidColorBrush(contentBackground);
        resources[SystemColors.ControlTextBrushKey] = new SolidColorBrush(primaryText);
        resources[SystemColors.HighlightBrushKey] = new SolidColorBrush(accent);
        resources[SystemColors.HighlightTextBrushKey] = new SolidColorBrush(Colors.White);
    }

    private void LoadSettings()
    {
        try
        {
            using var db = new UsageDbContext();

            int retentionDays = 90;
            int pollSeconds = 2;

            var retentionSetting = db.AppSettings.FirstOrDefault(x => x.Key == "RetentionDays");
            if (retentionSetting != null && int.TryParse(retentionSetting.Value, out var parsedRetention) && parsedRetention > 0)
            {
                retentionDays = parsedRetention;
            }

            var pollSetting = db.AppSettings.FirstOrDefault(x => x.Key == "PollSeconds");
            if (pollSetting != null && int.TryParse(pollSetting.Value, out var parsedPoll) && parsedPoll >= 1 && parsedPoll <= 10)
            {
                pollSeconds = parsedPoll;
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
                AccuracyCombo.SelectedIndex = 1; // Normal
            }

            UpdateConfigText(retentionDays, pollSeconds);
        }
        catch
        {
            // Ignore settings load errors and keep defaults
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

        // Determine final values we are saving
        var days = retentionDaysOverride ?? (int.TryParse(RetentionDaysBox.Text, out var parsedDays) && parsedDays > 0
            ? parsedDays
            : 90);
        days = Math.Max(1, days);

        var seconds = pollSecondsOverride ?? GetPollingSecondsFromUi();
        seconds = Math.Clamp(seconds, 1, 10);

        try
        {
            using var db = new UsageDbContext();

            var retentionSetting = db.AppSettings.FirstOrDefault(x => x.Key == "RetentionDays");
            if (retentionSetting == null)
            {
                retentionSetting = new AppSetting { Key = "RetentionDays" };
                db.AppSettings.Add(retentionSetting);
            }
            retentionSetting.Value = days.ToString();

            var pollSetting = db.AppSettings.FirstOrDefault(x => x.Key == "PollSeconds");
            if (pollSetting == null)
            {
                pollSetting = new AppSetting { Key = "PollSeconds" };
                db.AppSettings.Add(pollSetting);
            }
            pollSetting.Value = seconds.ToString();

            db.SaveChanges();
        }
        catch
        {
            // Ignore settings save errors; tracker will just use defaults
        }

        UpdateConfigText(days, seconds);
    }

    private int GetPollingSecondsFromUi()
    {
        if (AccuracyCombo.SelectedItem is ComboBoxItem item &&
            item.Tag is string tag &&
            int.TryParse(tag, out var pollSeconds))
        {
            return pollSeconds;
        }

        return 2; // default
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
        // Only allow digits
        e.Handled = e.Text.Any(ch => !char.IsDigit(ch));
    }

    private async void RefreshButton_OnClick(object sender, RoutedEventArgs e)
    {
        await LoadDataAsync();
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
