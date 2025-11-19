using ExeTicksTracker.Data;
using ExeTickTracker.UI.Models;
using System.Collections.ObjectModel;
using System.Windows;

namespace ExeTickTracker.UI;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly List<AppUsageSummary> _allData = new();  // unfiltered
    private readonly ObservableCollection<AppUsageSummary> _visibleData = new();
    public MainWindow()
    {
        InitializeComponent();
        UsageGrid.ItemsSource = _visibleData;

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            using var db = new UsageDbContext();

            // Load all intervals into memory for now.
            // For your scale, this is fine. If you want, we can optimize later.
            var intervals = db.AppUsageIntervals.ToList();

            var grouped = intervals
                .GroupBy(x => x.ProcessName)
                .Select(g =>
                {
                    var totalSeconds = g.Sum(i => (i.EndUtc - i.StartUtc).TotalSeconds);
                    var firstSeen = g.Min(i => i.StartUtc);
                    var lastSeen = g.Max(i => i.EndUtc);

                    return new AppUsageSummary
                    {
                        ProcessName = g.Key,
                        TotalSeconds = totalSeconds,
                        FirstSeenUtc = firstSeen,
                        LastSeenUtc = lastSeen
                    };
                })
                .OrderByDescending(x => x.TotalSeconds)
                .ToList();

            _allData.Clear();
            _allData.AddRange(grouped);

            ApplyFilters();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "Failed to load usage data:\n" + ex.Message,
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ApplyFilters()
    {
        string search = SearchBox.Text?.Trim() ?? string.Empty;
        double minMinutes = 0;

        if (double.TryParse(MinMinutesBox.Text, out var parsed))
        {
            minMinutes = parsed;
        }

        var minSeconds = minMinutes * 60;

        var filtered = _allData
            .Where(x => x.TotalSeconds >= minSeconds)
            .Where(x => string.IsNullOrEmpty(search)
                        || x.ProcessName.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.TotalSeconds)
            .ToList();

        _visibleData.Clear();
        foreach (var item in filtered)
        {
            _visibleData.Add(item);
        }
    }

    private void SearchBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void MinMinutesBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        ApplyFilters();
    }

}