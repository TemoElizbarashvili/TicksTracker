using System.Windows;
using System.Windows.Media;
using TickTracker.Utils;

namespace TickTracker.UI.Helpers;

public static class ThemeConfiguration
{
    public static void ApplyTheme(string theme)
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

    private static void SetBrush(ResourceDictionary resources, string key, Color color)
    {
        resources[key] = new SolidColorBrush(color);
    }

    public static void SaveTheme(string theme)
    {
        try
        {
            DbOperations.SetInAppSettings(Constants.ThemeKey, theme);
        }
        catch
        {
            // Ignore theme save errors
        }
    }

}
