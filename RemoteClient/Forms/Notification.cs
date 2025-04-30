using System.Windows.Forms;
using System.Drawing;
using System.ComponentModel;
using System;

namespace RemoteClient.Forms
{
    public partial class Notification : Form
    {
        private Label notificationLabel;
        private Point mouseOffset;
        private bool isMouseDown = false;
        public Notification()
        {
            InitializeComponent();
        }
        private void InitializeComponent()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            
            // Position at the top of the screen, but not full width
            this.Location = new Point(Screen.PrimaryScreen.Bounds.Width / 2 - 150, 0);
            this.Size = new Size(300, 35); // Smaller width, slightly higher
            this.BackColor = Color.FromArgb(192, 0, 0); // Dark red
            
            // Create the notification label
            notificationLabel = new Label
            {
                Text = "You are being monitored...",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,
                Font = new Font("Arial", 10, FontStyle.Bold),
                Dock = DockStyle.Fill,
                AutoSize = false
            };
            
            this.Controls.Add(notificationLabel);

            // Make the form semi-transparent
            this.Opacity = 0.8;
            
            // Add a border for better visibility
            this.Paint += (sender, e) => {
                e.Graphics.DrawRectangle(new Pen(Color.White, 1), 0, 0, Width - 1, Height - 1);
            };
            
            // Add mouse events for drag functionality
            this.MouseDown += Notification_MouseDown;
            this.MouseMove += Notification_MouseMove;
            this.MouseUp += Notification_MouseUp;
            
            // Same for the child label
            notificationLabel.MouseDown += Notification_MouseDown;
            notificationLabel.MouseMove += Notification_MouseMove;
            notificationLabel.MouseUp += Notification_MouseUp;
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
    }
}