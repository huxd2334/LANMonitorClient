// using System;
// using RemoteClient.Camera;
//
// namespace RemoteClient
// {
//     public class Test
//     {
//         public static void PrintWebcams()
//         {
//             try
//             {
//                 using (var webcam = new Webcam())
//                 {
//                     var webcamList = webcam.Load();
//                     Console.WriteLine($"Found {webcamList.Count} webcams:");
//                     
//                     for (int i = 0; i < webcamList.Count; i++)
//                     {
//                         Console.WriteLine($"[{i}] {webcamList[i]}");
//                     }
//                 }
//             }
//             catch (Exception ex)
//             {
//                 Console.WriteLine($"Error listing webcams: {ex.Message}");
//             }
//         }
//     }
// }