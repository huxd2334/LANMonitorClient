using System;
using System.Drawing;
using Newtonsoft.Json;

namespace RemoteClient.Core
{
    [Serializable]
    public class ScreenshotData
    {
        public string ProcessId { get; set; }
        public byte[] ImageBytes { get; set; }
        public Size ImageSize { get; set; }
        
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
    
}