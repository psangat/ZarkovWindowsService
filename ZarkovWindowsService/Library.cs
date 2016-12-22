using System;
using System.IO;
using System.Net.Mail;

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

        public static void sendMail(string mailTo, string mailFrom, string subject, string body)
        {
            try
            {
                MailMessage mailMessage = new MailMessage();

                String[] mailTos = mailTo.Split(';');

                foreach (var _mailTo in mailTos)
                {
                    mailMessage.To.Add(_mailTo);
                }

                mailMessage.From = new MailAddress(mailFrom);
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.Priority = MailPriority.High;
                SmtpClient smtpClient = new SmtpClient(Constants.SMTPCLIENT, 25);
                smtpClient.Send(mailMessage);
            }
            catch (Exception ex)
            {
                writeLog(Constants.SCHEDULERLOGFILE, ex.ToString());
            }
        }
    }
}
