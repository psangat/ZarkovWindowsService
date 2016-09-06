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

namespace ZarkovWindowsService
{
    public partial class Scheduler : ServiceBase
    {
        private Timer timer = null;
        static string serverName = System.Configuration.ConfigurationSettings.AppSettings.Get("serverIP");
        static string mongodDirectory = System.Configuration.ConfigurationSettings.AppSettings.Get("mongodDirectory");
        static string dbPath = System.Configuration.ConfigurationSettings.AppSettings.Get("dbPath");
        static string[] ports = System.Configuration.ConfigurationSettings.AppSettings.Get("ports").Split(',');
        static string schedulerLogFile = AppDomain.CurrentDomain.BaseDirectory + "\\LogFile.txt";
       // static string individualLogFile = AppDomain.CurrentDomain.BaseDirectory + "\\LogFile.txt";
        static string localConnection = String.Format(@"mongodb://{0}:", serverName);
        static string mainServer = System.Configuration.ConfigurationSettings.AppSettings.Get("mainServerIP");
        static string dbName = "agilent";
        static string collectionName = "zarkov";

        public Scheduler()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            foreach (String port in ports)
            {
                bool success = false;
                while (!success)
                {
                    success = runMongods(String.Format(@"{0}\{1}", dbPath, port), port);
                }
            }

            foreach (String port in ports)
            {
                if (!port.Equals("5555"))
                {
                    Task.Factory.StartNew(() =>
                    { // runs in separate thread
                        timer = new Timer();
                        this.timer.Interval = 6000; // every 30 seconds
                        this.timer.Elapsed += delegate { this.ETLMongo(port); };
                        timer.Enabled = true;
                    });
                }
            }
            Library.writeLog(schedulerLogFile, "Zarkov Window Services has been Started.");
        }

        protected override void OnStop()
        {
            timer.Enabled = false;
            Library.writeLog(schedulerLogFile, "Zarkov Window Service Stopped.");
        }

        private bool runMongods(string dbPath, string port)
        {
            try
            {
                if (!Directory.Exists(dbPath))
                    Directory.CreateDirectory(dbPath);
                startProcess("mongod.exe", String.Format("--dbpath {0} --port {1} --logpath {2} --logappend", dbPath, port, dbPath + @"\" + port + ".log"));
                Library.writeLog(schedulerLogFile, String.Format("Mongod Status: running, Port: {0}, DBPath {1}", port, dbPath));
            }
            catch (Exception ex)
            {
                Library.writeLog(ex);
                return false;
            }
            return true;
        }

        private void ETLMongo(string currentPort)
        {
            var _collectionLocks = connectServerAndGetCollection(localConnection + currentPort, dbName, "Locks");
            var _filter = Builders<BsonDocument>.Filter.Eq("lock", "Y");
            var _result = _collectionLocks.Count(_filter);
            if (_result.Equals(0))
            { // table is not locked
                //lock the table
                var update = Builders<BsonDocument>.Update.Set("lock", "Y").Set("description", "ETL Process started.").CurrentDate("lockedDate");
                _collectionLocks.UpdateOne(_filter, update, new UpdateOptions { IsUpsert = true });
                var exportPath = String.Format(@"{0}\Export\{1}", dbPath, currentPort);

                if (!Directory.Exists(exportPath))
                    Directory.CreateDirectory(exportPath);
               
                while (exportDataAsJSON(serverName, currentPort, dbName, collectionName, exportPath))
                {
                    Library.writeLog(exportPath + "\\" + currentPort+ ".log", "Before Drop Collection");
                    new MongoClient(localConnection + currentPort).GetDatabase(dbName).DropCollection(collectionName);
                    Library.writeLog(exportPath + "\\" + currentPort + ".log", "After Drop Collection");
                    var updateLock = Builders<BsonDocument>.Update.Set("lock", "N").Set("description", "ETL Process completed.").CurrentDate("releaseDate");
                    _collectionLocks.UpdateOne(_filter, updateLock);
                }

                while (importDataToMainServer(mainServer, "5555", dbName, collectionName, exportPath))
                {
                    if (File.Exists(exportPath))
                    {
                        Library.writeLog(exportPath + "\\" + currentPort + ".log", "File Deletion started");
                        File.Delete(exportPath);
                        Library.writeLog(exportPath + "\\" + currentPort + ".log", "File Deletion started");
                    }
                }
            }
            else
            {
                // retry in 5 minutes
            }
        }

        private bool exportDataAsJSON(string host, string port, string db, string collection, string outLocation)
        {
            try
            {
                Library.writeLog(outLocation + "\\" + port + ".log", "Export Data Started.");
                startProcess("mongoexport.exe", String.Format("--host {0} --port {1} --db {2} --collection {3} --out {4}", host, port, db, collection, outLocation + @"\" + "Zarkov.json"));
                Library.writeLog(outLocation + "\\" + port + ".log", "Export Data Completed.");
            
            }
            catch (Exception)
            {
                Library.writeLog(outLocation + "\\" + port + ".log", "Export Data Failed.");
                System.Threading.Thread.Sleep(3000);
                return false;
                // log the exveptiojn
                //throw; 
            }
            return true;
        }

        private bool importDataToMainServer(string host, string port, string db, string collection, string outLocation)
        {
            try
            {
                Library.writeLog(outLocation + "\\" + port + ".log", "Import to main server Started.");
                startProcess("mongoimport.exe", String.Format("--host {0} --port {1} --db {2} --collection {3} --file {4}", host, port, db, collection, outLocation + @"\" + "Zarkov.json"));
                Library.writeLog(outLocation + "\\" + port + ".log", "Import to main server Completed.");
            }
            catch (Exception)
            {
                Library.writeLog(outLocation + "\\" + port + ".log", "Import to main server Failed.");
                System.Threading.Thread.Sleep(3000);
                return false;
                //log the exception here
                //throw;
            }
            return true;

        }

        private void startProcess(string processName, string arguments)
        {
            try
            {
                ProcessStartInfo start = new ProcessStartInfo();
                start.FileName = mongodDirectory + @"\" + processName;
                start.WindowStyle = ProcessWindowStyle.Normal;
                start.UseShellExecute = false;
                start.Arguments = arguments;
                Process mongod = Process.Start(start);
            }
            catch (Exception ex)
            {
                throw;
                //Library.writeLog(ex);
                //return false;
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
