using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WinNumberGuide
{
    public static class IconExtractor
    {
        [DllImport("user32.dll")]
        static extern IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", EntryPoint = "GetClassLong")]
        static extern IntPtr GetClassLong32(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const int GCL_HICON = -14;
        private const int GCL_HICONSM = -34;
        private const int WM_GETICON = 0x007F;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;

        private static readonly Dictionary<string, ImageSource?> _iconCache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Try to get an icon for the app using name and appId.
        /// Strategies (in order):
        ///   1. Match running process by appId substring
        ///   2. Match running process by name (window title, process name, file description)
        ///   3. Try known exe paths for common apps
        /// </summary>
        public static ImageSource? GetIconForApp(string appName, string appId, Process[]? processes = null)
        {
            string cacheKey = !string.IsNullOrEmpty(appId) ? appId : appName;
            lock (_iconCache)
            {
                if (_iconCache.TryGetValue(cacheKey, out var cachedIcon))
                    return cachedIcon;
            }

            ImageSource? icon = ExtractIconCore(appName, appId, processes);

            // Cache the result even if it's null to avoid repeated expensive searches
            lock (_iconCache)
            {
                _iconCache[cacheKey] = icon;
            }

            return icon;
        }

        /// <summary>
        /// Returns a cached icon without any expensive lookups.
        /// Returns null if not in cache (does NOT trigger process scanning).
        /// </summary>
        public static ImageSource? GetCachedIconOnly(string appName, string appId)
        {
            string cacheKey = !string.IsNullOrEmpty(appId) ? appId : appName;
            lock (_iconCache)
            {
                _iconCache.TryGetValue(cacheKey, out var icon);
                return icon;
            }
        }

        private static ImageSource? ExtractIconCore(string appName, string appId, Process[]? processes)
        {
            // Strategy 0: Try packaged app icon (UWP/MSIX/PWA)
            if (!string.IsNullOrEmpty(appId) && appId.Contains('!'))
            {
                var icon = PackagedAppIconExtractor.GetIcon(appId);
                if (icon != null) return icon;
            }

            processes ??= Process.GetProcesses();

            // Strategy 1: Find a running process matching appId
            if (!string.IsNullOrEmpty(appId))
            {
                var icon = TryGetIconFromProcessByAppId(appId, processes);
                if (icon != null) return icon;
            }

            // Strategy 2: Find a running process matching appName
            var icon2 = TryGetIconFromProcessByName(appName, processes);
            if (icon2 != null) return icon2;

            // Strategy 3: Try known executable paths
            var icon3 = TryGetIconFromKnownPath(appName, appId, processes);
            if (icon3 != null) return icon3;

            return null;
        }

        private static ImageSource? TryGetIconFromProcessByAppId(string appId, Process[] processes)
        {
            try
            {
                foreach (var p in processes)
                {
                    try
                    {
                        if (p.MainWindowHandle == IntPtr.Zero) continue;

                        // Try to match by process name or executable path
                        string processName = p.ProcessName;

                        // appId examples:
                        //   "MSEdge" -> process name "msedge"
                        //   "Microsoft.Windows.Explorer" -> process name "explorer"
                        //   "Google.Antigravity" -> process name contains "antigravity"
                        //   "Microsoft.WindowsNotepad_8wekyb3d8bbwe!App" -> process name "Notepad"

                        // Check direct match or substring using OrdinalIgnoreCase (no need for ToLowerInvariant)
                        if (appId.Contains(processName, StringComparison.OrdinalIgnoreCase) ||
                            processName.Contains(ExtractShortName(appId), StringComparison.OrdinalIgnoreCase))
                        {
                            var result = GetIconFromWindow(p.MainWindowHandle);
                            if (result != null) return result;

                            result = GetIconFromExecutable(p);
                            if (result != null) return result;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error processing process {p.Id} in TryGetIconFromProcessByAppId: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in TryGetIconFromProcessByAppId: {ex.Message}");
            }
            return null;
        }

        private static ImageSource? TryGetIconFromProcessByName(string appName, Process[] processes)
        {
            try
            {
                foreach (var p in processes)
                {
                    try
                    {
                        if (p.MainWindowHandle == IntPtr.Zero) continue;

                        bool matched = false;

                        // Match by window title
                        if (!string.IsNullOrEmpty(p.MainWindowTitle) &&
                            (p.MainWindowTitle.Contains(appName, StringComparison.OrdinalIgnoreCase) ||
                             appName.Contains(p.MainWindowTitle, StringComparison.OrdinalIgnoreCase)))
                        {
                            matched = true;
                        }

                        // Match by process name
                        if (!matched && appName.Contains(p.ProcessName, StringComparison.OrdinalIgnoreCase))
                        {
                            matched = true;
                        }

                        // Match by file description / product name
                        if (!matched)
                        {
                            try
                            {
                                if (p.MainModule != null)
                                {
                                    var fileInfo = FileVersionInfo.GetVersionInfo(p.MainModule.FileName);
                                    if (!string.IsNullOrEmpty(fileInfo.FileDescription) &&
                                        (fileInfo.FileDescription.Contains(appName, StringComparison.OrdinalIgnoreCase) ||
                                         appName.Contains(fileInfo.FileDescription, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        matched = true;
                                    }
                                    if (!matched && !string.IsNullOrEmpty(fileInfo.ProductName) &&
                                        (fileInfo.ProductName.Contains(appName, StringComparison.OrdinalIgnoreCase) ||
                                         appName.Contains(fileInfo.ProductName, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        matched = true;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error getting MainModule or FileVersionInfo for process {p.Id}: {ex.Message}");
                            }
                        }

                        if (matched)
                        {
                            var result = GetIconFromWindow(p.MainWindowHandle);
                            if (result != null) return result;

                            result = GetIconFromExecutable(p);
                            if (result != null) return result;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error processing process {p.Id} in TryGetIconFromProcessByName: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in TryGetIconFromProcessByName: {ex.Message}");
            }
            return null;
        }

        private static ImageSource? TryGetIconFromKnownPath(string appName, string appId, Process[] processes)
        {
            // Map well-known appIds to executable paths
            var knownPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "MSEdge", @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" },
                { "Microsoft.Windows.Explorer", @"C:\Windows\explorer.exe" },
            };

            string? exePath = null;

            // Try matching by appId
            foreach (var kvp in knownPaths)
            {
                if (appId.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    exePath = kvp.Value;
                    break;
                }
            }

            // Try matching common app names to system paths
            if (exePath == null)
            {
                if (appName.Contains("エクスプローラー", StringComparison.OrdinalIgnoreCase) || appName.Contains("Explorer", StringComparison.OrdinalIgnoreCase))
                    exePath = @"C:\Windows\explorer.exe";
                else if (appName.Contains("Edge", StringComparison.OrdinalIgnoreCase))
                    exePath = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";
                else if (appName.Contains("メモ帳", StringComparison.OrdinalIgnoreCase) || appName.Contains("Notepad", StringComparison.OrdinalIgnoreCase))
                    exePath = @"C:\Windows\System32\notepad.exe";
            }

            if (exePath != null && File.Exists(exePath))
            {
                try
                {
                    var icon = Icon.ExtractAssociatedIcon(exePath);
                    if (icon != null)
                    {
                        var src = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                        src.Freeze();
                        icon.Dispose();
                        return src;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error extracting associated icon for path {exePath}: {ex.Message}");
                }
            }

            // Last resort: try to find exe by searching PATH or common locations
            return TryFindIconByProcessName(appName, processes);
        }

        private static ImageSource? TryFindIconByProcessName(string appName, Process[] processes)
        {
            // Try to find any running process (even without a main window) that matches
            try
            {
                foreach (var p in processes)
                {
                    try
                    {
                        if (appName.Contains(p.ProcessName, StringComparison.OrdinalIgnoreCase) ||
                            p.ProcessName.Contains(appName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (p.MainModule != null)
                            {
                                var icon = Icon.ExtractAssociatedIcon(p.MainModule.FileName);
                                if (icon != null)
                                {
                                    var src = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                                    src.Freeze();
                                    icon.Dispose();
                                    return src;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error processing process {p.Id} in TryFindIconByProcessName: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in TryFindIconByProcessName: {ex.Message}");
            }
            return null;
        }

        private static ImageSource? GetIconFromWindow(IntPtr hWnd)
        {
            try
            {
                IntPtr hIcon = SendMessage(hWnd, WM_GETICON, (IntPtr)ICON_BIG, IntPtr.Zero);
                if (hIcon == IntPtr.Zero)
                    hIcon = GetClassLongPtrValue(hWnd, GCL_HICON);
                if (hIcon == IntPtr.Zero)
                    hIcon = SendMessage(hWnd, WM_GETICON, (IntPtr)ICON_SMALL, IntPtr.Zero);
                if (hIcon == IntPtr.Zero)
                    hIcon = GetClassLongPtrValue(hWnd, GCL_HICONSM);

                if (hIcon != IntPtr.Zero)
                {
                    var src = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    src.Freeze();
                    return src;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GetIconFromWindow for hWnd {hWnd}: {ex.Message}");
            }
            return null;
        }

        private static ImageSource? GetIconFromExecutable(Process p)
        {
            try
            {
                if (p.MainModule != null)
                {
                    var icon = Icon.ExtractAssociatedIcon(p.MainModule.FileName);
                    if (icon != null)
                    {
                        var src = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                        src.Freeze();
                        icon.Dispose();
                        return src;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GetIconFromExecutable: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Extract a short meaningful name from an appId.
        /// "Microsoft.WindowsNotepad_8wekyb3d8bbwe!App" -> "windowsnotepad"
        /// "MSEdge" -> "msedge"
        /// "Microsoft.Windows.Explorer" -> "explorer"
        /// </summary>
        private static string ExtractShortName(string appId)
        {
            // Remove UWP package suffix (e.g., "_8wekyb3d8bbwe!App")
            int bangIdx = appId.IndexOf('!');
            if (bangIdx >= 0) appId = appId.Substring(0, bangIdx);

            int underscoreIdx = appId.IndexOf('_');
            if (underscoreIdx >= 0) appId = appId.Substring(0, underscoreIdx);

            // Get last segment after '.'
            int lastDot = appId.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < appId.Length - 1)
                return appId.Substring(lastDot + 1).ToLowerInvariant();

            return appId.ToLowerInvariant();
        }

        private static IntPtr GetClassLongPtrValue(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 4)
                return GetClassLong32(hWnd, nIndex);
            else
                return GetClassLongPtr(hWnd, nIndex);
        }
    }
}
