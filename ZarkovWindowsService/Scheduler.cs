using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Configuration;
namespace ZarkovWindowsService
{
    public partial class Scheduler : ServiceBase
    {
        public Scheduler()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            foreach (String port in Constants.ports)
            {
                bool success = false;
                int tryCount = 0;
                while (!success)
                {
                    success = runMongods(String.Format(@"{0}\{1}", Constants.dbPath, port), port);
                    tryCount++;
                    if (tryCount <= 3 && !success)
                    {
                        Library.writeLog(Constants.schedulerLogFile, String.Format("Try {0}: Could not run mongod on port {1}", tryCount, port));
                    }
                    else
                    {
                        Library.writeLog(Constants.schedulerLogFile, String.Format("Could not run mongod on port {0} even after 4 tries. Stopping trying.", port));
                        break;
                    }

                }
            }

            foreach (String port in Constants.ports)
            {
                if (!port.Equals("5555"))
                {
                    Task.Factory.StartNew(() =>
                    {
                        Timer timer = new Timer();
                        timer.Interval = 300000; // every 5 mins
                        timer.Elapsed += delegate { this.ETLMongo(port); };
                        timer.Enabled = true;
                    });
                }
            }
            Library.writeLog(Constants.schedulerLogFile, "Zarkov Window Services has been Started.");
        }

        protected override void OnStop()
        {
            //timer.Enabled = false;
            Library.writeLog(schedulerLogFile, "Zarkov Window Service Stopped.");
        }

        private bool runMongods(string dbPath, string port)
        {
            try
            {
                if (!Directory.Exists(dbPath))
                    Directory.CreateDirectory(dbPath);
                startProcess("mongod.exe", String.Format("--dbpath {0} --port {1} --logpath {2} --logappend", dbPath, port, dbPath + @"\mongod.log"), Constants.schedulerLogFile);
                Library.writeLog(Constants.schedulerLogFile, String.Format("Mongod Status: running, Port: {0}, DBPath {1}", port, dbPath));
            }
            catch (Exception ex)
            {
                Library.writeLog(ex);
                return false;
            }
            return true;
        }

        private void testTiming(string currentPort)
        {
            var exportPath = String.Format(@"{0}\Export\{1}", dbPath, currentPort);
            if (!Directory.Exists(exportPath))
                Directory.CreateDirectory(exportPath);
            Library.writeLog(exportPath + "\\" + currentPort + "_test.log", DateTime.Now.ToString());
        }

        private void ETLMongo(string currentPort)
        {
            var exportPath = String.Format(@"{0}\Export\{1}", Constants.dbPath, currentPort);
            string logLocation = exportPath + "\\Buffer_Server_" + currentPort + ".log";
            while (true)
            {
                var _collectionLocks = connectServerAndGetCollection(Constants.localConnection + currentPort, Constants.dbName, "Locks");
                var _filter = Builders<BsonDocument>.Filter.Eq("lock", "Y");
                var _result = _collectionLocks.Count(_filter);
                if (_result.Equals(0)) // table is not locked
                {
                    var update = Builders<BsonDocument>.Update.Set("lock", "Y").Set("description", "ETL Process started.").CurrentDate("lockedDate");
                    _collectionLocks.UpdateOne(_filter, update, new UpdateOptions { IsUpsert = true });

                    if (!Directory.Exists(exportPath))
                        Directory.CreateDirectory(exportPath);
                    string fileName;
                    while (true)
                    {
                        bool success = exportDataAsJSON(Constants.serverName, currentPort, Constants.dbName, Constants.collectionName, exportPath, out fileName);
                        if (success)
                        {
                            Library.writeLog(logLocation, "Drop Collection Started.");
                            new MongoClient(Constants.localConnection + currentPort).GetDatabase(Constants.dbName).DropCollection(Constants.collectionName);
                            Library.writeLog(logLocation, "Drop Collection Completed.");
                            var updateLock = Builders<BsonDocument>.Update.Set("lock", "N").Set("description", "ETL Process completed.").CurrentDate("releaseDate");
                            _collectionLocks.UpdateOne(_filter, updateLock);
                            break;
                        }
                    }

                    while (true)
                    {
                        bool success = importDataToMainServer(Constants.mainServer, currentPort, Constants.dbName, Constants.collectionName, exportPath, fileName);
                        if (success)
                        {
                            while (true)
                            {
                                string filePath = exportPath + @"\" + fileName;
                                if (File.Exists(filePath))
                                {
                                    Library.writeLog(logLocation, "File Deletion Started.");
                                    File.Delete(filePath);
                                    Library.writeLog(logLocation, "File Deletion Completed.");
                                    Library.writeLog(logLocation, "*********** End ETL *************");
                                    break;
                                }
                                else
                                {
                                    Library.writeLog(logLocation, fileName + " does not exists.");
                                }
                            }
                            break;
                        }
                    }
                    break;
                }
                else
                {
                    Library.writeLog(logLocation, "Collection in Use. Retrying ETL Process in 5 Minutes.");
                    System.Threading.Thread.Sleep(300000);
                }
            }
        }

        private bool exportDataAsJSON(string host, string port, string db, string collection, string outLocation, out string fileName)
        {
            string logLocation = outLocation + "\\Buffer_Server_" + port + ".log";
            fileName = "Zarkov_" + DateTime.Now.ToString("MM_dd_yyyy_H_mm_ss") + ".json";
            try
            {
                Library.writeLog(logLocation, "*********** Start ETL *************");
                Library.writeLog(logLocation, "Export Data Started.");
                startProcess("mongoexport.exe", String.Format("--host {0} --port {1} --db {2} --collection {3} --out {4}", host, port, db, collection, outLocation + @"\" + fileName), logLocation);
                Library.writeLog(logLocation, "Export Data Completed.");
            }
            catch (Exception ex)
            {
                Library.writeLog(logLocation, "Export Data Failed.");
                Library.writeLog(logLocation, ex);
                System.Threading.Thread.Sleep(300000);
                return false;
            }
            return true;
        }

        private bool importDataToMainServer(string host, string port, string db, string collection, string outLocation, string fileName)
        {
            string logLocation = outLocation + "\\Buffer_Server_" + port + ".log";
            try
            {
                Library.writeLog(logLocation, "Import to main server Started.");
                startProcess("mongoimport.exe", String.Format("--host {0} --port {1} --db {2} --collection {3} --file {4}", host, "5555", db, collection, outLocation + @"\" + fileName), logLocation);
                Library.writeLog(logLocation, "Import to main server Completed.");
            }
            catch (Exception ex)
            {
                Library.writeLog(logLocation, "Import to main server Failed.");
                Library.writeLog(logLocation, ex);
                System.Threading.Thread.Sleep(300000);
                return false;
            }
            return true;

        }

        private void startProcess(string processName, string arguments, string logPath)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = Constants.mongodDirectory + @"\" + processName;
                startInfo.WindowStyle = ProcessWindowStyle.Normal;
                startInfo.UseShellExecute = false;
                startInfo.Arguments = arguments;

                Process mongod = Process.Start(startInfo);
                bool completed = mongod.WaitForExit(5000);
                if (!completed)
                {
                    Library.writeLog(logPath, String.Format("Running {0} did not complete in 5 seconds", processName));
                }
            }
            catch
            {
                throw;
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

    }
}
