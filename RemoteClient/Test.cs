using System;
using System.Drawing;
using System.Drawing.Design;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using RemoteClient.Camera;
using RemoteClient.Core;

namespace RemoteClient
{
    public class Test
    {
        public static void PrintWebcams()
        {
            try
            {
                using (var webcam = new Webcam())
                {
                    var webcamList = webcam.Load();
                    Console.WriteLine($"Found {webcamList.Count} webcams:");

                    for (int i = 0; i < webcamList.Count; i++)
                    {
                        Console.WriteLine($"[{i}] {webcamList[i]}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error listing webcams: {ex.Message}");
            }
        }

        public static void ViewScreenshot(ScreenshotData screenshot)
        {
            try
            {
                using (var ms = new System.IO.MemoryStream(screenshot.ImageBytes))
                using (var bitmap = new Bitmap(ms))
                using (var form = new Form())
                using (var pictureBox = new PictureBox())
                {
                    form.Text = $"Screenshot - Process {screenshot.ProcessId}";
                    form.Size = new Size(800, 600);
                    form.StartPosition = FormStartPosition.CenterScreen;

                    pictureBox.Dock = DockStyle.Fill;
                    pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
                    pictureBox.Image = bitmap;

                    form.Controls.Add(pictureBox);
                    form.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error viewing screenshot: {ex.Message}");
            }
        }
        public static void TestScreenshotViewer()
        {
            try
            {
                // Take screenshot of primary screen
                Rectangle bounds = Screen.PrimaryScreen.Bounds;
                using (Bitmap screenshot = new Bitmap(bounds.Width, bounds.Height))
                using (Graphics g = Graphics.FromImage(screenshot))
                {
                    g.CopyFromScreen(0, 0, 0, 0, bounds.Size);
        
                    // Convert to byte array
                    using (var ms = new System.IO.MemoryStream())
                    {
                        using (var encoderParams = new EncoderParameters(1))
                        using (var qualityParam = new EncoderParameter(Encoder.Quality, 80L))
                        {
                            var jpegCodec = ImageCodecInfo.GetImageEncoders()
                                .First(codec => codec.MimeType == "image/jpeg");
                            encoderParams.Param[0] = qualityParam;
                            screenshot.Save(ms, jpegCodec, encoderParams);
                        }
        
                        // Create test screenshot data
                        var screenshotData = new ScreenshotData
                        {
                            ProcessId = "test_screenshot",
                            ImageBytes = ms.ToArray(),
                            ImageSize = bounds.Size
                        };
        
                        // View the screenshot
                        ViewScreenshot(screenshotData);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error testing screenshot viewer: {ex.Message}");
            }
        }
        public static void Main(string[] args)
        {
            // Test webcam listing
            // PrintWebcams();

            // Test screenshot viewing (replace with actual screenshot data)
            Test.TestScreenshotViewer();
        }
    }
}