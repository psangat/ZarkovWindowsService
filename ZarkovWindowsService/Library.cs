using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZarkovWindowsService
{
    public static class Library
    {
        public static void writeLog(Exception ex)
        {
            StreamWriter sw = null;
            try
            {
                sw = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "\\LogFile.txt", true);
                sw.WriteLine(String.Format("{0}: {1}; {2}", DateTime.Now.ToString("[MM-dd-yyyy H:mm:ss]"), ex.Source.ToString().Trim(), ex.Message.ToString().Trim()));
                sw.Flush();
                sw.Close();
            }
            catch (Exception)
            {
                
                throw;
            }
        }

        public static void writeLog(string logLocation, string message)
        {
            StreamWriter sw = null;
            try
            {
                sw = new StreamWriter(logLocation, true);
                sw.WriteLine(String.Format("{0}: {1}", DateTime.Now.ToString("[MM-dd-yyyy H:mm:ss]"), message));
                sw.Flush();
                sw.Close();
            }
            catch (Exception)
            {
                
                throw;
            }
        }

        public static void writeLog(string logLocation, Exception ex)
        {
            StreamWriter sw = null;
            try
            {
                sw = new StreamWriter(logLocation, true);
                sw.WriteLine(String.Format("{0}: {1}; {2}", DateTime.Now.ToString("[MM-dd-yyyy H:mm:ss]"), ex.Source.ToString().Trim(), ex.Message.ToString().Trim()));
                sw.Flush();
                sw.Close();
            }
            catch (Exception)
            {

                throw;
            }
        }
    }
}
