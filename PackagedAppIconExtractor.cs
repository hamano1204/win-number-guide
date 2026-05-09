using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Windows.Management.Deployment;

namespace WinNumberGuide
{
    /// <summary>
    /// Extracts icons for UWP/MSIX/PWA apps using the Windows PackageManager API.
    /// These apps store their icons inside their package install directory,
    /// referenced in AppxManifest.xml.
    /// </summary>
    public static class PackagedAppIconExtractor
    {
        /// <summary>
        /// Try to get the icon for a packaged app given its AppId from the taskbar.
        /// AppId format examples:
        ///   "MicrosoftCorporationII.Windows365_8wekyb3d8bbwe!Windows365"
        ///   "tasks.google.com-7AF29FD0_e6vhqestawfs2!App"
        ///   "Microsoft.WindowsNotepad_8wekyb3d8bbwe!App"
        /// </summary>
        public static ImageSource? GetIcon(string appId)
        {
            if (string.IsNullOrEmpty(appId)) return null;

            try
            {
                // Extract package family name (everything before '!')
                int bangIndex = appId.IndexOf('!');
                if (bangIndex < 0) return null;

                string packageFamilyName = appId.Substring(0, bangIndex);

                var pm = new PackageManager();
                var packages = pm.FindPackagesForUser("", packageFamilyName);

                foreach (var package in packages)
                {
                    string installPath = package.InstalledPath;
                    if (string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath))
                        continue;

                    // Try to read the logo from AppxManifest.xml
                    var icon = TryGetIconFromManifest(installPath);
                    if (icon != null) return icon;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PackagedAppIconExtractor error for '{appId}': {ex.Message}");
            }

            return null;
        }

        private static ImageSource? TryGetIconFromManifest(string installPath)
        {
            try
            {
                string manifestPath = Path.Combine(installPath, "AppxManifest.xml");
                if (!File.Exists(manifestPath)) return null;

                var doc = XDocument.Load(manifestPath);
                XNamespace uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";
                XNamespace defaultNs = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";

                // Look for VisualElements in Applications/Application
                var visualElements = doc.Descendants(uap + "VisualElements").FirstOrDefault();
                if (visualElements == null)
                {
                    // Try without namespace prefix (some manifests use default namespace)
                    visualElements = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "VisualElements");
                }

                if (visualElements == null) return null;

                // Try Square44x44Logo first because it usually contains the "targetsize" 
                // assets which are unpadded and best for taskbar-like displays.
                string? logoRelativePath =
                    visualElements.Attribute("Square44x44Logo")?.Value ??
                    visualElements.Attribute("Square150x150Logo")?.Value ??
                    visualElements.Attribute("Logo")?.Value;

                if (string.IsNullOrEmpty(logoRelativePath)) return null;

                return TryLoadLogoFile(installPath, logoRelativePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading manifest in '{installPath}': {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Try to load a logo file. The manifest specifies a base name like "Assets\Square44x44Logo.png"
        /// but the actual files may have scale suffixes like:
        ///   Square44x44Logo.scale-200.png
        ///   Square44x44Logo.scale-100.png
        ///   Square44x44Logo.targetsize-48.png
        ///   Square44x44Logo.targetsize-256.png
        /// </summary>
        private static ImageSource? TryLoadLogoFile(string installPath, string logoRelativePath)
        {
            // First try exact path
            string exactPath = Path.Combine(installPath, logoRelativePath);
            if (File.Exists(exactPath))
            {
                return LoadImage(exactPath);
            }

            // Try finding scaled versions
            string dir = Path.GetDirectoryName(exactPath) ?? installPath;
            string baseName = Path.GetFileNameWithoutExtension(logoRelativePath);
            string ext = Path.GetExtension(logoRelativePath);

            if (!Directory.Exists(dir)) return null;

            // Preferred order:
            // 1. altform-unplated (no padding/plate, matches desktop icons best)
            // 2. targetsize (specific sizes, usually less padding than 'scale')
            // 3. scale (usually have significant padding)
            var candidates = Directory.GetFiles(dir, $"{baseName}*{ext}")
                .OrderByDescending(f => f.Contains("altform-unplated"))
                .ThenByDescending(f => f.Contains("targetsize-256"))
                .ThenByDescending(f => f.Contains("targetsize-48"))
                .ThenByDescending(f => f.Contains("targetsize-96"))
                .ThenByDescending(f => f.Contains("targetsize-32"))
                .ThenByDescending(f => f.Contains("scale-200"))
                .ThenByDescending(f => f.Contains("scale-100"))
                .ToList();

            foreach (var candidate in candidates)
            {
                var img = LoadImage(candidate);
                if (img != null) return img;
            }

            return null;
        }

        private static ImageSource? LoadImage(string filePath)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                // Removed DecodePixelWidth to let WPF handle high-quality scaling 
                // and to avoid issues with smaller source assets.
                bitmap.EndInit();
                bitmap.Freeze(); // Thread-safe
                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading image '{filePath}': {ex.Message}");
                return null;
            }
        }
    }
}
