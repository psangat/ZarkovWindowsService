using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Timers;
namespace ZarkovWindowsService
{
    public partial class Scheduler : ServiceBase
    {
        private static Random random = new Random();
        public Scheduler()
        {
            InitializeComponent();
        }

        /// <summary>
        /// On start of the service runs monogd service in each port and start ETL process for each port
        /// in random time interval between service running interval min and max
        /// </summary>
        /// <param name="args"></param>
        protected override void OnStart(string[] args)
        {
            foreach (String port in Constants.PORTS)
            {
                Task.Factory.StartNew(() =>
                {
                    runMongods(String.Format(@"{0}\{1}", Constants.DBPATH, port), port);
                });

                Task.Factory.StartNew(() =>
                {
                    Timer timer = new Timer();
                    timer.Enabled = true;
                    int time = getRandomTime(Convert.ToInt32(Constants.SERVICERUNNINGINTERVALMIN), Convert.ToInt32(Constants.SERVICERUNNINGINTERVALMAX));
                    timer.Interval = time; // every 30 - 45 mins
                    timer.Elapsed += delegate { this.ETLData(port, timer); };
                });
            }
            Library.writeLog(Constants.SCHEDULERLOGFILE, "Zarkov Window Services has been Started.");
        }

        /// <summary>
        /// On stop of the service sends email to system administrator 
        /// </summary>
        protected override void OnStop()
        {
            Library.writeLog(Constants.SCHEDULERLOGFILE, "Zarkov Window Service Stopped.");
            Library.sendMail(Constants.MAILTO,
                    Constants.MAILFROM,
                    "Zarkov Window Service Stopped.",
                    "Zarkov Window Service has Stopped. \n\nPlease ignore the email if the service was stopped intentionally else check the server for issues. \nPlease contact system administrator for further assistance.\n\n\n This email is unattended. Please do not reply.");
        }

        /// <summary>
        /// Transfers the data from each database to the main server
        /// </summary>
        /// <param name="port">database port runnning the mongod sevice</param>
        /// <param name="timer">timer for ETL process</param>
        private void ETLData(string port, Timer timer)
        {
            int newTime = getRandomTime(Convert.ToInt32(Constants.SERVICERUNNINGINTERVALMIN), Convert.ToInt32(Constants.SERVICERUNNINGINTERVALMAX));
            timer.Interval = newTime; // every 30 - 45 mins
            var batchSize = 10000;
            var logPath = String.Format(@"{0}\{1}", Constants.LOGPATH, port);
            var logLocation = logPath + "\\log" + DateTime.Now.ToString("_dd_MM_yyyy") + ".log";
            var sourceConn = String.Format(@"mongodb://{0}:", Constants.SERVERNAME);
            var destinationConn = String.Format(@"mongodb://{0}:", Constants.MAINSERVER);
            if (!Directory.Exists(logPath))
                Directory.CreateDirectory(logPath);
            Library.writeLog(logLocation, "*********** Start ETL *************");
            timer.Stop();
            Library.writeLog(logLocation, "Timer has been stopped.");
            var isPortOpen = isPortAvailable(Constants.SERVERNAME, Convert.ToInt16(port), logLocation);
            var isPortOpenMainServer = isPortAvailable(Constants.MAINSERVER, 5555, logLocation);

            if (isPortOpen && isPortOpenMainServer)
            {
                while (true)
                {
                    try
                    {
                        var _collectionLocks = connectServerAndGetCollection(sourceConn + port, Constants.SOURCEDATABASENAME, Constants.LOCKSCOLLECTIONNAME);
                        var _filter = Builders<BsonDocument>.Filter.Eq("lock", "Y");
                        var _result = _collectionLocks.Find(_filter).FirstOrDefault();
                        if (_result == null)
                        {
                            var options = new InsertManyOptions { IsOrdered = false };
                            var update = Builders<BsonDocument>.Update.Set("lock", "Y").CurrentDate("lockedDate").Set("description", "ETL Process Started.");
                            _collectionLocks.UpdateOne(_filter, update, new UpdateOptions { IsUpsert = true });

                            Library.writeLog(logLocation, "Export Data Started.");

                            var _collectionSource = connectServerAndGetCollection(sourceConn + port, Constants.SOURCEDATABASENAME, Constants.SOURCECOLLECTIONNAME);
                            var _collectionDestination = connectServerAndGetCollection(destinationConn + "5555", Constants.DESTINATIONDATABASENAME, Constants.DESTINATIONCOLLECTIONNAME);
                            var allDocsCount = _collectionSource.Count(new BsonDocument());
                            var loopLimit = allDocsCount / batchSize;
                            for (int i = 0; i <= loopLimit; i++)
                            {
                                var batchDocs = _collectionSource.Find(new BsonDocument()).Limit(batchSize).ToList();
                                var listDoc = new List<BsonDocument>();
                                var listIDToRemove = new List<BsonValue>();
                                foreach (var doc in batchDocs)
                                {
                                    listIDToRemove.Add(doc.GetValue("_id"));
                                    doc.Remove("_id");
                                    listDoc.Add(doc);
                                }
                                if (listDoc.Count > 0)
                                {
                                    try
                                    {
                                        _collectionDestination.InsertMany(listDoc, options);
                                        Library.writeLog(logLocation, String.Format("Batch No.: {0}, Count: {1}", i, listDoc.Count));
                                        foreach (var id in listIDToRemove)
                                        {
                                            _collectionSource.DeleteOne(new BsonDocument().Add("_id", id));
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        // logging the exception but letting the process contiue
                                        // data is not lost but will retransfer in next schedule
                                        Library.writeLog(logLocation, ex);
                                        Library.writeLog(logLocation, String.Format("Batch No.: {0} has been skipped due to error and will be transferred in next schedule.", i));
                                    }
                                }
                                //listDoc.Clear();
                            }

                            var updateLock = Builders<BsonDocument>.Update.Set("lock", "N").CurrentDate("releaseDate").Set("description", "ETL Completed.");
                            _collectionLocks.UpdateOne(_filter, updateLock);

                            Library.writeLog(logLocation, "Export Data Completed.");
                            timer.Start();
                            Library.writeLog(logLocation, "Timer has been restarted.");
                            Library.writeLog(logLocation, "Next ETL Process is scheduled after " + (newTime / 60000) + " minutes.");
                            Library.writeLog(logLocation, "*********** End ETL *************");
                            break;
                        }
                        else
                        {
                            var lockedDate = _result.GetValue("lockedDate").ToUniversalTime();

                            TimeSpan span = DateTime.Now.ToUniversalTime().Subtract(lockedDate);
                            var lockClearingTime = Convert.ToInt16(Constants.LOCKCLEARINGTIME);
                            if (span.Hours >= lockClearingTime)
                            {
                                var updateLock = Builders<BsonDocument>.Update.Set("lock", "N").Set("description", "Updated Lock After 2 hours.").CurrentDate("releaseDate");
                                _collectionLocks.UpdateOne(_filter, updateLock);
                                Library.writeLog(logLocation, "Updated Lock After 2 hours. Retrying ETL Process.");
                                // Update lock to N
                            }
                            else
                            {
                                Library.writeLog(logLocation, "Collection in Use. Retrying ETL Process in 5 Minutes.");
                                System.Threading.Thread.Sleep(300000);
                                Library.writeLog(logLocation, "Retrying ETL Process.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Library.writeLog(logLocation, ex);
                        System.Threading.Thread.Sleep(300000);
                        Library.writeLog(logLocation, "Retrying ETL Process.");
                    }
                }
            }
            else
            {
                Library.writeLog(logLocation, "Could not start the ETL process because the required ports were not available.");
                timer.Start();
                Library.writeLog(logLocation, "Timer has been restarted.");
                Library.writeLog(logLocation, "Next ETL Process is scheduled after " + (newTime / 60000) + " minutes.");
                Library.writeLog(logLocation, "*********** End ETL *************");
            }

        }

        /// <summary>
        /// Runs mongod service in each port
        /// </summary>
        /// <param name="dbPath">location where the data is situated</param>
        /// <param name="port">database port to run the mongod service</param>
        /// <returns></returns>
        private bool runMongods(string dbPath, string port)
        {
            try
            {
                if (!Directory.Exists(dbPath))
                    Directory.CreateDirectory(dbPath);
                startProcess("mongod.exe", String.Format("--dbpath {0} --port {1} --logpath {2} --smallfiles", dbPath, port, dbPath + @"\mongod.log"), Constants.SCHEDULERLOGFILE);
            }
            catch (Exception ex)
            {
                Library.writeLog(ex);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Starts the command processes from C#
        /// </summary>
        /// <param name="processName">Name of the process</param>
        /// <param name="arguments">Arguments for the process</param>
        /// <param name="logPath">Log path to write log</param>
        /// <returns></returns>
        private bool startProcess(string processName, string arguments, string logPath)
        {
            try
            {
                Process _process = new Process();
                _process.StartInfo.FileName = Constants.MONGODDIRECTORY + @"\" + processName;
                _process.StartInfo.CreateNoWindow = true;
                _process.StartInfo.UseShellExecute = false;
                _process.StartInfo.Arguments = arguments;

                _process.StartInfo.RedirectStandardOutput = true;
                _process.StartInfo.RedirectStandardError = true;
                _process.StartInfo.RedirectStandardInput = true;
                _process.EnableRaisingEvents = true;

                _process.Start();
                var stdOut = _process.StandardOutput;
                String outline;
                while ((outline = stdOut.ReadLine()) != null)
                {
                    Library.writeLog(logPath, outline);
                }

                var stdErr = _process.StandardError;
                String errline;
                while ((errline = stdErr.ReadLine()) != null)
                {
                    Library.writeLog(logPath, errline);
                }
                _process.WaitForExit();


                return true;
            }
            catch (Exception ex)
            {
                Library.writeLog(logPath, String.Format("Error while executing {0}. ProcessName: {0}, Arguments: {1}", processName, arguments));
                Library.writeLog(logPath, ex.ToString());
                Library.sendMail(Constants.MAILTO,
                    Constants.MAILFROM,
                    String.Format("Important!!! {0} Start Failure", processName),
                    String.Format("The system was unable to run {0}. \nDetails: \nProcessName: {0}, \nArguments Used: {1}. \n\nPlease contact system administrator for further assistance. \n\n\n This email is unattended. Please do not reply.", processName, arguments));
                return false;
            }
        }

        /// <summary>
        /// Checks if the port is open in the remote server.
        /// </summary>
        /// <param name="_HostURI"></param>
        /// <param name="_PortNumber"></param>
        /// <returns>boolean</returns>
        private bool isPortAvailable(string _HostURI, int _PortNumber, string logPath)
        {
            try
            {
                TcpClient client = new TcpClient(_HostURI, _PortNumber);
                return true;
            }
            catch (Exception ex)
            {
                Library.writeLog(logPath, String.Format("Port #{0} is not running mongod service.", _PortNumber));
                Library.writeLog(logPath, ex);
                Library.sendMail(Constants.MAILTO,
                    Constants.MAILFROM,
                    String.Format(" Database Port #{0} is not running mongod service.", _PortNumber),
                    ex.ToString());
                return false;
            }
        }

        /// <summary>
        /// Makes a connection to the server and returns the required collection.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="dbName"></param>
        /// <param name="collection"></param>
        /// <returns>MongoDB Collection</returns>
        private IMongoCollection<BsonDocument> connectServerAndGetCollection(string connection, string dbName, string collection)
        {
            try
            {
                return new MongoClient(connection).GetDatabase(dbName).GetCollection<BsonDocument>(collection);
            }
            catch (Exception ex)
            {
                // Library.writeLog(String.Format("[Error]: {0}", ex.Message));
                return null;
            }
        }

        /// <summary>
        /// Gets the random time for service running time
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        private int getRandomTime(int min, int max)
        {
            return random.Next(min, max);
        }
    }
}
