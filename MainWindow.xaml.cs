using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace WinNumberGuide
{
    public partial class MainWindow : Window
    {
        private KeyboardHook _hook;
        private Storyboard _fadeIn;
        private Storyboard _fadeOut;
        private bool _isShowing = false;

        public MainWindow()
        {
            InitializeComponent();
            
            _fadeIn = (Storyboard)FindResource("FadeIn");
            _fadeOut = (Storyboard)FindResource("FadeOut");
            
            _fadeOut.Completed += FadeOut_Completed;

            // Set initial state
            this.Visibility = Visibility.Hidden;

            // Initialize hook
            _hook = new KeyboardHook();
            _hook.WinKeyLongPressed += Hook_WinKeyLongPressed;
            _hook.WinKeyReleased += Hook_WinKeyReleased;
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
            _hook?.Dispose();
            base.OnClosed(e);
        }
    }
}