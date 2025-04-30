using System;
using System.Drawing;
using System.Windows.Forms;
using RemoteClient.Core;


namespace RemoteClient.Forms
{
    public partial class Welcome : Form
    {
        private bool consentGiven = false;
        private RemoteClient remoteClient;
        private Button continueButton;

        public Welcome()
        {
            InitializeComponent();
            remoteClient = new RemoteClient();
        }

        private void InitializeComponent()
        {
            this.Text = "Remote Monitoring System - Welcome";
            this.Size = new Size(550, 450);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Create title label
            Label titleLabel = new Label
            {
                Text = "Remote Monitoring System",
                Font = new Font("Arial", 16, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, 20)
            };

            // Create info text
            TextBox infoTextBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                Size = new Size(500, 200),
                Location = new Point(20, 60),
                ScrollBars = ScrollBars.Vertical,
                Text = "Welcome to the Remote Monitoring System.\r\n\r\n" +
                       "Ứng dụng này cho phép nhân viên được ủy quyền:\r\n" +
                       "• Giám sát hoạt động màn hình của bạn\r\n" +
                       "• Truy cập vào webcam\r\n" +
                       "• Điều khiển máy tính của bạn từ xa\r\n\r\n" +
                       "Quyền riêng tư và bảo mật của bạn rất quan trọng đối với chúng tôi. Việc giám sát " +
                       "từ xa sẽ chỉ được bắt đầu khi có sự đồng ý rõ ràng của bạn, và bạn sẽ " +
                       "được thông báo khi việc giám sát đang hoạt động."
            };

            // Create rules text
            TextBox rulesTextBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                Size = new Size(500, 80),
                Location = new Point(20, 270),
                ScrollBars = ScrollBars.Vertical,
                Text = "Bằng cách đồng ý với việc giám sát từ xa, bạn đồng ý với các điều khoản sau:\r\n" +
                       "1. Việc giám sát chỉ được sử dụng cho các mục đích được ủy quyền\r\n" +
                       "2. Dữ liệu của bạn sẽ được xử lý theo chính sách bảo mật của chúng tôi\r\n" +
                       "3. Bạn có thể rút lại sự đồng ý bất kỳ lúc nào bằng cách đóng ứng dụng"
            };

            // Create consent checkbox
            CheckBox consentCheckBox = new CheckBox
            {
                Text = "Tôi đồng ý với việc giám sát từ xa và các điều khoản trên.",
                AutoSize = true,
                Location = new Point(20, 360)
            };
            consentCheckBox.CheckedChanged += ConsentCheckBox_CheckedChanged;

            // Create continue button
            continueButton = new Button
            {
                Text = "Continue",
                Size = new Size(100, 30),
                Location = new Point(420, 360),
                Enabled = false
            };
            continueButton.Click += ContinueButton_Click;

            // Add controls to form
            this.Controls.Add(titleLabel);
            this.Controls.Add(infoTextBox);
            this.Controls.Add(rulesTextBox);
            this.Controls.Add(consentCheckBox);
            this.Controls.Add(continueButton);
        }

        private void ConsentCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            CheckBox checkBox = sender as CheckBox;
            consentGiven = checkBox.Checked;
            continueButton.Enabled = consentGiven;
        }

        private void ContinueButton_Click(object sender, EventArgs e)
        {
            try
            {
                // Set global consent status - this will now also handle the notification bar
                ConsentManager.ConsentGiven = consentGiven;

                RemoteClient.SendMessage(new DataObject
                {
                    CommandType = "Consent",
                    CommandName = "ConsentStatus",
                    CommandData = consentGiven
                });

                if (consentGiven)
                {
                    MessageBox.Show("Consent has been recorded. Remote monitoring is now enabled.",
                        "Consent Given", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    // Only start RemoteClient and hooks if consent is given
                    Program.InitHook();

                    // Initialize RemoteClient only after consent
                    remoteClient = new RemoteClient();

                    // Hide form but keep app running
                    this.Hide();
                }
                else
                {
                    MessageBox.Show("Monitoring disabled. No consent given.",
                        "Consent Declined", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    // Exit application if consent not given
                    Application.Exit();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Connection Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
    }
}