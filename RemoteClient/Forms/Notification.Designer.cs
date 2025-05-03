using System.ComponentModel;
using System.Windows.Forms;
using System.Windows.Forms;
using System.Drawing;
namespace RemoteClient.Forms
{
    partial class Notification
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }

            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code
        private void InitializeComponent()
        {
            this.btnExit = new System.Windows.Forms.Button();
            this.SuspendLayout();
        
            // btnExit
            this.btnExit.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnExit.ForeColor = System.Drawing.Color.White;
            this.btnExit.Location = new System.Drawing.Point(270, 0);
            this.btnExit.Name = "btnExit";
            this.btnExit.Size = new System.Drawing.Size(30, 25);
            this.btnExit.TabIndex = 0;
            this.btnExit.Text = "X";
            this.btnExit.UseVisualStyleBackColor = true;
            this.btnExit.Click += new System.EventHandler(this.btnExit_Click);
        
            // Notification Form
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(45)))));
            this.ClientSize = new System.Drawing.Size(300, 100);
            this.Controls.Add(this.btnExit);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "Notification";
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);
            
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.Location = new Point(Screen.PrimaryScreen.Bounds.Width / 2 - 150, 0);
            this.Size = new Size(300, 35); // Smaller width, slightly higher
            this.BackColor = Color.FromArgb(192, 0, 0); // Dark red
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
            this.Opacity = 0.8;
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

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
    
        #endregion
    }
}