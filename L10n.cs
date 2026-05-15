using System.Resources;
using System.Reflection;

namespace WinNumberGuide
{
    public static class L10n
    {
        private static readonly ResourceManager _resourceManager = 
            new ResourceManager("WinNumberGuide.Resources.Strings", Assembly.GetExecutingAssembly());

        public static string GetString(string name) => _resourceManager.GetString(name) ?? name;

        public static string MenuAutoStart => GetString("MenuAutoStart");
        public static string MenuExit => GetString("MenuExit");
        public static string TrayIconTooltip => GetString("TrayIconTooltip");
        public static string RegexRunningWindows => GetString("RegexRunningWindows");
        public static string RegexPinned => GetString("RegexPinned");
    }
}
