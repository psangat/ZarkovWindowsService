using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZarkovWindowsService
{
    public class Constants
    {
        public static string mainServer = ConfigurationSettings.AppSettings.Get("mainServerIP");
        public static string serverName = ConfigurationSettings.AppSettings.Get("serverIP");
        public static string mongodDirectory = ConfigurationSettings.AppSettings.Get("mongodDirectory");
        public static string dbPath = ConfigurationSettings.AppSettings.Get("dbPath");
        public static string[] ports = ConfigurationSettings.AppSettings.Get("ports").Split(',');
        public static string schedulerLogFile = AppDomain.CurrentDomain.BaseDirectory + "\\LogFile.txt";
        public static string localConnection = String.Format(@"mongodb://{0}:", serverName);
        public static string dbName = "agilent";
        public static string collectionName = "zarkov";
    }
}
