using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace WinNumberGuide
{
    public class TaskbarApp : INotifyPropertyChanged
    {
        public string Name { get; set; } = "";
        public string ShortcutNumber { get; set; } = "";
        public string AppId { get; set; } = "";

        private ImageSource? _icon;
        public ImageSource? Icon
        {
            get => _icon;
            set
            {
                _icon = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public Thickness Margin
        {
            get
            {
                // Create a wider gap before the 6th item ("6") to separate left-hand and right-hand keys
                return ShortcutNumber == "6" ? new Thickness(50, 0, 10, 0) : new Thickness(10, 0, 10, 0);
            }
        }
    }

    public class TaskbarReader
    {
        private static readonly string[] TASKBAR_BUTTON_CLASSES = { 
            "Taskbar.TaskListButtonAutomationPeer", 
            "AppControlHost" 
        };

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        public static List<TaskbarApp> GetTaskbarApps()
        {
            var apps = new List<TaskbarApp>();

            try
            {
                // Try to find buttons using multiple strategies
                AutomationElementCollection? buttons = null;

                // Strategy 1: Find taskbar handle and search descendants
                IntPtr taskbarHandle = FindWindow("Shell_TrayWnd", null);
                if (taskbarHandle != IntPtr.Zero)
                {
                    AutomationElement taskbar = AutomationElement.FromHandle(taskbarHandle);
                    if (taskbar != null)
                    {
                        buttons = FindButtons(taskbar);
                    }
                }

                // Strategy 2: If strategy 1 found less than 10 apps, search from RootElement
                if (buttons == null || buttons.Count < 10)
                {
                    Debug.WriteLine($"Strategy 1 found only {buttons?.Count ?? 0} buttons. Trying Strategy 2...");
                    var rootButtons = FindButtons(AutomationElement.RootElement);
                    if (rootButtons != null && rootButtons.Count > (buttons?.Count ?? 0))
                    {
                        buttons = rootButtons;
                    }
                }

                if (buttons == null || buttons.Count == 0)
                {
                    Debug.WriteLine("No taskbar buttons found after all strategies.");
                    return apps;
                }

                Debug.WriteLine($"Found {buttons.Count} potential taskbar buttons.");

                foreach (AutomationElement button in buttons)
                {
                    if (apps.Count >= 10) break;

                    try
                    {
                        string rawName = button.Current.Name;
                        string automationId = button.Current.AutomationId;
                        string className = button.Current.ClassName;
                        
                        // Filter out non-app buttons (like Start, Search, etc. if they were caught)
                        if (string.IsNullOrWhiteSpace(rawName)) continue;
                        if (!automationId.StartsWith("Appid:", StringComparison.OrdinalIgnoreCase) && 
                            !TASKBAR_BUTTON_CLASSES.Contains(className)) continue;

                        string name = SanitizeAppName(rawName);
                        string appId = ExtractAppId(automationId);

                        if (apps.Any(a => a.Name == name)) continue;

                        int appIndex = apps.Count;
                        string shortcutNum = (appIndex == 9) ? "0" : (appIndex + 1).ToString();

                        apps.Add(new TaskbarApp
                        {
                            Name = name,
                            ShortcutNumber = shortcutNum,
                            Icon = null,
                            AppId = appId
                        });
                    }
                    catch (ElementNotAvailableException) { }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error processing button: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Critical error in GetTaskbarApps: {ex.Message}");
            }

            // Start loading icons asynchronously
            Task.Run(() => LoadIconsAsync(apps));

            return apps;
        }

        private static AutomationElementCollection? FindButtons(AutomationElement parent)
        {
            foreach (var className in TASKBAR_BUTTON_CLASSES)
            {
                try
                {
                    var buttons = parent.FindAll(
                        TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.ClassNameProperty, className));

                    if (buttons != null && buttons.Count > 0)
                        return buttons;
                }
                catch { }
            }

            // Fallback: search for any button and we will filter in the loop
            try
            {
                return parent.FindAll(
                    TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
            }
            catch { return null; }
        }

        private static void LoadIconsAsync(List<TaskbarApp> apps)
        {
            try
            {
                var processes = Process.GetProcesses();
                foreach (var app in apps)
                {
                    var icon = IconExtractor.GetIconForApp(app.Name, app.AppId, processes);
                    if (icon != null)
                    {
                        System.Windows.Application.Current.Dispatcher.InvokeAsync(() => app.Icon = icon);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in LoadIconsAsync: {ex.Message}");
            }
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
