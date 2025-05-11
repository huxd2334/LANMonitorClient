using AForge.Video;
using AForge.Video.DirectShow;
using RemoteClient.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Encoder = System.Drawing.Imaging.Encoder;

namespace RemoteClient.Camera
{
    
    public class Webcam : IDisposable
    {
        private VideoCaptureDevice videoSource;
        private UdpClient udpStreamClient;
        private IPEndPoint serverStreamEndPoint;
        private bool isStreaming;
        private int quality;
        private readonly object lockObject = new object();
        private bool disposed;
    
        public Webcam(int quality = 70)
        {
            this.quality = quality;
        }
        public void SetQuality(int quality)
        {
            if (quality >= 0 && quality <= 100)
                this.quality = quality;
        }
    
        public void InitializeUdpStream(string serverIp, int port)
        {
            udpStreamClient = new UdpClient();
            serverStreamEndPoint = new IPEndPoint(IPAddress.Parse(serverIp), port);
        }
    
        public void StartUdpStreaming()
        {
            isStreaming = true;
            videoSource?.Start();
        }
    
        public void StopUdpStreaming()
        {
            isStreaming = false;
            videoSource?.Stop();
        }
    
        public void setCamera(int index)
        {
            var devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (index < devices.Count)
            {
                videoSource?.Stop();
                videoSource = new VideoCaptureDevice(devices[index].MonikerString);
                videoSource.NewFrame += VideoSource_NewFrame;
            }
        }
    
private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
{
    if (!isStreaming || udpStreamClient == null) return;

    try
    {
        using (var bitmap = (Bitmap)eventArgs.Frame.Clone())
        {
            // Calculate scaled dimensions while maintaining aspect ratio
            double scale = Math.Min(640.0 / bitmap.Width, 480.0 / bitmap.Height);
            int scaledWidth = (int)(bitmap.Width * scale);
            int scaledHeight = (int)(bitmap.Height * scale);

            using (var scaled = new Bitmap(scaledWidth, scaledHeight))
            using (var graphics = Graphics.FromImage(scaled))
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.CompositingQuality = CompositingQuality.HighSpeed;
                graphics.SmoothingMode = SmoothingMode.HighSpeed;
                graphics.DrawImage(bitmap, 0, 0, scaledWidth, scaledHeight);

                using (var ms = new MemoryStream())
                {
                    var encoderParams = new EncoderParameters(1)
                    {
                        Param = { [0] = new EncoderParameter(Encoder.Quality, quality) }
                    };
                    var codec = GetEncoderInfo("image/jpeg");

                    scaled.Save(ms, codec, encoderParams);
                    var imageBytes = ms.ToArray();

                    if (imageBytes.Length > 65507) // UDP packet size limit
                    {
                        Debug.WriteLine("Image too large for single UDP packet");
                        return;
                    }

                    lock (lockObject)
                    {
                        if (udpStreamClient != null && isStreaming)
                        {
                            udpStreamClient.Send(imageBytes, imageBytes.Length, serverStreamEndPoint);
                        }
                    }
                }
            }
        }
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"Error in VideoSource_NewFrame: {ex.Message}");
    }
}    
        public List<string> Load()
        {
            return new FilterInfoCollection(FilterCategory.VideoInputDevice)
                .Cast<FilterInfo>()
                .Select(device => device.Name)
                .ToList();
        }
    
        private static ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            return ImageCodecInfo.GetImageEncoders()
                .FirstOrDefault(codec => codec.MimeType == mimeType);
        }
    
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;
        
            if (disposing)
            {
                StopUdpStreaming();
                if (videoSource != null)
                {
                    videoSource.Stop();
                    videoSource.SignalToStop();
                    videoSource.WaitForStop();
                    videoSource = null;
                }
                udpStreamClient?.Dispose();
            }
        
            disposed = true;
        }
    
        ~Webcam()
        {
            Dispose(false);
        }
    }
}
