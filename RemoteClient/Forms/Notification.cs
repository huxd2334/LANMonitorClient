using System.Windows.Forms;
using System.Drawing;
using System.ComponentModel;
using System;
using System.Diagnostics;
using System.Threading;

namespace RemoteClient.Forms
{
    public partial class Notification : Form
    {
        private Label notificationLabel;
        private Point mouseOffset;
        private bool isMouseDown = false;
        private System.Windows.Forms.Button btnExit;
        public Notification()
        {
            InitializeComponent();
        }
        
        private void Notification_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isMouseDown = true;
                mouseOffset = new Point(e.X, e.Y);
            }
        }

        private void Notification_MouseMove(object sender, MouseEventArgs e)
        {
            if (isMouseDown)
            {
                Point newLocation = this.Location;
                newLocation.X = newLocation.X + (e.X - mouseOffset.X);
                newLocation.Y = newLocation.Y + (e.Y - mouseOffset.Y);
                this.Location = newLocation;
            }
        }

        private void Notification_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isMouseDown = false;
            }
        }
        protected override CreateParams CreateParams
        {
            get
            {
                // This makes the window a "tool window" which won't appear in the taskbar
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80; // WS_EX_TOOLWINDOW
                return cp;
            }
        }
        private void btnExit_Click(object sender, System.EventArgs e)
        {try
            {
                // Send exit command first
                RemoteClient.SendMessage(new DataObject
                {
                    CommandType = "RTL",
                    CommandName = "RTAUPD",
                    CommandData = "Client exited"
                });

                // Give time for message to be sent
                Thread.Sleep(200);

                // Unhook keyboard listener
                Program.Unhook();

                // Exit application
                Application.Exit();
                Thread.Sleep(100);
                Process.GetCurrentProcess().Kill();
            }
            catch (Exception)
            {
                Environment.Exit(0);
            }
        }
    }
}