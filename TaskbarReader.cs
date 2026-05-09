using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Automation;
using System.Windows.Media;

namespace WinNumberGuide
{
    public class TaskbarApp
    {
        public string Name { get; set; } = "";
        public string ShortcutNumber { get; set; } = "";
        public ImageSource? Icon { get; set; }
        public string AppId { get; set; } = "";
    }

    public class TaskbarReader
    {
        private const string TASKBAR_BUTTON_CLASS = "Taskbar.TaskListButtonAutomationPeer";

        public static List<TaskbarApp> GetTaskbarApps()
        {
            var apps = new List<TaskbarApp>();

            try
            {
                // Find the main taskbar window
                AutomationElement? taskbar = AutomationElement.RootElement.FindFirst(
                    TreeScope.Children,
                    new PropertyCondition(AutomationElement.ClassNameProperty, "Shell_TrayWnd"));

                if (taskbar == null) return apps;

                // Find only Taskbar app buttons by their ClassName
                var buttons = taskbar.FindAll(
                    TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ClassNameProperty, TASKBAR_BUTTON_CLASS));

                foreach (AutomationElement button in buttons)
                {
                    if (apps.Count >= 10) break; // Max 10 apps (1-9, 0)

                    string rawName = button.Current.Name;
                    string automationId = button.Current.AutomationId;
                    if (string.IsNullOrWhiteSpace(rawName)) continue;

                    string name = SanitizeAppName(rawName);
                    string appId = ExtractAppId(automationId);

                    // Avoid duplicates
                    if (apps.Any(a => a.Name == name)) continue;

                    int appIndex = apps.Count;
                    string shortcutNum = (appIndex == 9) ? "0" : (appIndex + 1).ToString();

                    // Try to get icon using multiple strategies
                    ImageSource? icon = IconExtractor.GetIconForApp(name, appId);

                    apps.Add(new TaskbarApp
                    {
                        Name = name,
                        ShortcutNumber = shortcutNum,
                        Icon = icon,
                        AppId = appId
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading taskbar: {ex.Message}");
            }

            return apps;
        }

        private static string SanitizeAppName(string name)
        {
            // Remove Windows 11 Taskbar suffixes (Japanese & English)
            name = System.Text.RegularExpressions.Regex.Replace(name, @"\s*-\s*\d+\s*つの実行中ウィンドウ", "");
            name = System.Text.RegularExpressions.Regex.Replace(name, @"\s*-\s*\d+\s*running windows?", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            name = System.Text.RegularExpressions.Regex.Replace(name, @"\s*固定済み$", "");
            name = System.Text.RegularExpressions.Regex.Replace(name, @"\s*Pinned$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return name.Trim();
        }

        private static string ExtractAppId(string automationId)
        {
            // AutomationId format: "Appid: Microsoft.Windows.Explorer"
            if (automationId.StartsWith("Appid: ", StringComparison.OrdinalIgnoreCase))
            {
                return automationId.Substring("Appid: ".Length).Trim();
            }
            return "";
        }
    }
}
