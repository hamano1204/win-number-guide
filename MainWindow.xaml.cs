using System;
using System.Windows;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using System.Diagnostics;
namespace WinNumberGuide
{
    public partial class MainWindow : Window
    {
        private KeyboardHook _hook;
        private Storyboard _fadeIn;
        private Storyboard _fadeOut;
        private bool _isShowing = false;
        private System.Windows.Forms.NotifyIcon _notifyIcon;
        private System.Windows.Forms.ToolStripMenuItem _startupMenuItem;

        public MainWindow()
        {
            InitializeComponent();
            
            _fadeIn = (Storyboard)FindResource("FadeIn");
            _fadeOut = (Storyboard)FindResource("FadeOut");
            
            _fadeOut.Completed += FadeOut_Completed;

            // Set initial state
            this.Visibility = Visibility.Hidden;

            // Center window manually ignoring AppBars
            this.Loaded += (s, e) =>
            {
                this.Left = (SystemParameters.PrimaryScreenWidth - this.Width) / 2;
                this.Top = (SystemParameters.PrimaryScreenHeight - this.Height) / 2;
            };

            // Initialize hook
            _hook = new KeyboardHook();
            _hook.WinKeyLongPressed += Hook_WinKeyLongPressed;
            _hook.WinKeyReleased += Hook_WinKeyReleased;

            // Initialize system tray icon
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            _notifyIcon.Text = "WinNumberGuide";
            _notifyIcon.Visible = true;

            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            
            _startupMenuItem = new System.Windows.Forms.ToolStripMenuItem("Windows起動時に自動実行");
            _startupMenuItem.CheckOnClick = true;
            _startupMenuItem.Checked = IsStartupEnabled();
            _startupMenuItem.CheckedChanged += StartupMenuItem_CheckedChanged;
            
            var exitMenuItem = new System.Windows.Forms.ToolStripMenuItem("終了");
            exitMenuItem.Click += (s, ev) => System.Windows.Application.Current.Shutdown();

            contextMenu.Items.Add(_startupMenuItem);
            contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            contextMenu.Items.Add(exitMenuItem);

            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void Hook_WinKeyLongPressed(object sender, EventArgs e)
        {
            if (!_isShowing)
            {
                _isShowing = true;
                
                // Refresh taskbar apps
                var apps = TaskbarReader.GetTaskbarApps();
                AppsList.ItemsSource = apps;

                this.Visibility = Visibility.Visible;
                _fadeIn.Begin(MainContainer);
            }
        }

        private void Hook_WinKeyReleased(object sender, EventArgs e)
        {
            if (_isShowing)
            {
                _isShowing = false;
                _fadeOut.Begin(MainContainer);
            }
        }

        private void FadeOut_Completed(object sender, EventArgs e)
        {
            if (!_isShowing)
            {
                this.Visibility = Visibility.Hidden;
                AppsList.ItemsSource = null; // Clear to free memory/icons
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            _hook?.Dispose();
            base.OnClosed(e);
        }

        private const string RegistryRunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "WinNumberGuide";

        private bool IsStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, false);
                if (key != null)
                {
                    var value = key.GetValue(AppName) as string;
                    return !string.IsNullOrEmpty(value);
                }
            }
            catch (Exception ex) 
            { 
                Debug.WriteLine($"Error checking startup registry: {ex.Message}");
            }
            return false;
        }

        private void StartupMenuItem_CheckedChanged(object? sender, EventArgs e)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKey, true);
                if (key != null)
                {
                    if (_startupMenuItem.Checked)
                    {
                        string path = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
                        key.SetValue(AppName, path);
                    }
                    else
                    {
                        key.DeleteValue(AppName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error changing startup registry: {ex.Message}");
            }
        }
    }
}