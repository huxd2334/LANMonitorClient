using RemoteClient.Forms;

namespace RemoteClient.Core
{
    public static class ConsentManager
    {
        private static bool _consentGiven = false;
        private static Notification _notificationBar = null;
        
        public static bool ConsentGiven
        {
            get { return _consentGiven; }
            set 
            { 
                _consentGiven = value;
            
                if (_consentGiven)
                {
                    ShowNotificationBar();
                }
                else
                {
                    HideNotificationBar();
                }
            }
        }
        private static void ShowNotificationBar()
        {
            if (_notificationBar == null || _notificationBar.IsDisposed)
            {
                _notificationBar = new Notification();
            }
        
            if (!_notificationBar.Visible)
            {
                _notificationBar.Show();
            }
        }

        private static void HideNotificationBar()
        {
            if (_notificationBar != null && !_notificationBar.IsDisposed)
            {
                _notificationBar.Hide();
            }
        }
    
        // Add this method to manually refresh notification state
        public static void RefreshNotificationBar()
        {
            if (_consentGiven)
            {
                ShowNotificationBar();
            }
            else
            {
                HideNotificationBar();
            }
        }
        
        public static bool CheckConsent()
        {
            return _consentGiven;
        }
    }
}