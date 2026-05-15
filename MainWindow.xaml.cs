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

        // Idea 1+3: Prefetch cache
        private List<TaskbarApp>? _cachedApps = null;
        private Task<List<TaskbarApp>>? _prefetchTask = null;

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
            _hook.WinKeyDown += Hook_WinKeyDown;
            _hook.WinKeyLongPressed += Hook_WinKeyLongPressed;
            _hook.WinKeyReleased += Hook_WinKeyReleased;

            // Initialize system tray icon
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application;
            _notifyIcon.Text = L10n.TrayIconTooltip;
            _notifyIcon.Visible = true;

            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            
            _startupMenuItem = new System.Windows.Forms.ToolStripMenuItem(L10n.MenuAutoStart);
            _startupMenuItem.CheckOnClick = true;
            _startupMenuItem.Checked = IsStartupEnabled();
            _startupMenuItem.CheckedChanged += StartupMenuItem_CheckedChanged;
            
            var exitMenuItem = new System.Windows.Forms.ToolStripMenuItem(L10n.MenuExit);
            exitMenuItem.Click += (s, ev) => System.Windows.Application.Current.Shutdown();

            contextMenu.Items.Add(_startupMenuItem);
            contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            contextMenu.Items.Add(exitMenuItem);

            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        /// <summary>
        /// Idea 1: Win キー押下の瞬間にプリフェッチを開始する。
        /// 長押し判定（600ms）の待ち時間を有効活用し、表示時にはすでに結果が揃っている状態にする。
        /// </summary>
        private void Hook_WinKeyDown(object sender, EventArgs e)
        {
            if (_prefetchTask == null || _prefetchTask.IsCompleted)
            {
                _prefetchTask = Task.Run(() => TaskbarReader.GetTaskbarApps());
            }
        }

        /// <summary>
        /// Idea 3: 前回のキャッシュを即座に表示し、プリフェッチ結果で更新する。
        /// </summary>
        private async void Hook_WinKeyLongPressed(object sender, EventArgs e)
        {
            if (_isShowing) return;
            _isShowing = true;

            // Idea 3: 前回キャッシュを即座に表示（ゼロラグ）
            bool showedCached = false;
            if (_cachedApps != null && _cachedApps.Count > 0)
            {
                AppsList.ItemsSource = _cachedApps;
                this.Visibility = Visibility.Visible;
                _fadeIn.Begin(MainContainer);
                showedCached = true;
            }

            // Idea 1: プリフェッチ結果を待つ（600ms後なのでほぼ完了済みのはず）
            if (_prefetchTask != null)
            {
                try
                {
                    var freshApps = await _prefetchTask;
                    _prefetchTask = null;

                    // 切り替え時のアイコンちらつきを防ぐため、キャッシュからアイコンを先に埋める
                    foreach (var app in freshApps)
                    {
                        var icon = IconExtractor.GetCachedIconOnly(app.Name, app.AppId);
                        if (icon != null) app.Icon = icon;
                    }

                    _cachedApps = freshApps;
                    AppsList.ItemsSource = freshApps;

                    if (!showedCached)
                    {
                        this.Visibility = Visibility.Visible;
                        _fadeIn.Begin(MainContainer);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Prefetch failed: {ex.Message}");
                    // フォールバック: キャッシュがあればそれを使用
                    if (!showedCached && _cachedApps != null)
                    {
                        AppsList.ItemsSource = _cachedApps;
                        this.Visibility = Visibility.Visible;
                        _fadeIn.Begin(MainContainer);
                    }
                }
            }
            else if (!showedCached)
            {
                // 通常は来ないが念のためのフォールバック
                var apps = await Task.Run(() => TaskbarReader.GetTaskbarApps());
                _cachedApps = apps;
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
                AppsList.ItemsSource = null; // UIリソース解放（_cachedApps のオブジェクトは保持）
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