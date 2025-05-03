using System;
using System.IO;
using System.Threading.Tasks;
namespace RemoteClient.Core
{
    public class ScreenCaptureHelper
    {
        public async Task<byte[]> CapturePrimaryScreenAsync()
        {
            // Tạm thời trả về mảng byte giả để code không lỗi khi biên dịch.
            // Sau này bạn sẽ thay phần này bằng Windows.Graphics.Capture thực sự.
            await Task.Delay(100); // mô phỏng delay async
            return File.ReadAllBytes("screenshot-fake.jpg"); // giả lập ảnh
        } 
    }
}