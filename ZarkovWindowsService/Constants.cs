using System;
using System.Configuration;

namespace ZarkovWindowsService
{
    public class Constants
    {
        public static string MAINSERVER = ConfigurationSettings.AppSettings.Get("mainServerIP");
        public static string SERVERNAME = ConfigurationSettings.AppSettings.Get("serverIP");
        public static string MONGODDIRECTORY = ConfigurationSettings.AppSettings.Get("mongodDirectory");
        public static string DBPATH = ConfigurationSettings.AppSettings.Get("dbPath");
        public static string LOGPATH = ConfigurationSettings.AppSettings.Get("logPath");
        public static string EXPORTPATH = ConfigurationSettings.AppSettings.Get("exportPath");
        public static string[] PORTS = ConfigurationSettings.AppSettings.Get("ports").Split(',');
        public static string SCHEDULERLOGFILE = AppDomain.CurrentDomain.BaseDirectory + "\\scheduler.log";
        public static string LOCALCONNECTION = String.Format(@"mongodb://{0}:", SERVERNAME);

        public static string SMTPCLIENT = ConfigurationSettings.AppSettings.Get("smtpClient");
        public static string MAILTO = ConfigurationSettings.AppSettings.Get("mailTo");
        public static string MAILFROM = ConfigurationSettings.AppSettings.Get("mailFrom");
        public static string SERVICERUNNINGINTERVALMIN = ConfigurationSettings.AppSettings.Get("serviceRunningIntervalMin");
        public static string SERVICERUNNINGINTERVALMAX = ConfigurationSettings.AppSettings.Get("serviceRunningIntervalMax");
        public static string LOCKCLEARINGTIME = ConfigurationSettings.AppSettings.Get("lockClearingTime");

        public static string SOURCEDATABASENAME = ConfigurationSettings.AppSettings.Get("sourceDatabaseName");
        public static string DESTINATIONDATABASENAME = ConfigurationSettings.AppSettings.Get("destinationDatabaseName");
        public static string SOURCECOLLECTIONNAME = ConfigurationSettings.AppSettings.Get("sourceCollectionName");
        public static string DESTINATIONCOLLECTIONNAME = ConfigurationSettings.AppSettings.Get("destinationCollectionName");
        public static string LOCKSCOLLECTIONNAME = ConfigurationSettings.AppSettings.Get("locksCollectionName");
    }
}
