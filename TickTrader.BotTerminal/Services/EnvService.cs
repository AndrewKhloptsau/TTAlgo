﻿using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TickTrader.BotTerminal
{
    internal class EnvService
    {
        private Logger logger;
        private EnvService()
        {
            logger = NLog.LogManager.GetCurrentClassLogger();
            ApplicationName = "BotTrader";
            var myDocumentsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),ApplicationName);
            LogFolder = Path.Combine(myDocumentsFolder, "Logs");
            JournalFolder = Path.Combine(myDocumentsFolder, "Journals");

            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            AlgoRepositoryFolder = Path.Combine(appDir, "AlgoRepository");
            FeedHistoryCacheFolder = Path.Combine(appDir, "FeedCache");

            EnsureFolder(AlgoRepositoryFolder);
            EnsureFolder(LogFolder);
            EnsureFolder(JournalFolder);

            string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            

            UserDataStorage = new XmlObjectStorage(new FolderBinStorage(appDataFolder));
            ProtectedUserDataStorage = new XmlObjectStorage(new SecureStorageLayer(new FolderBinStorage(appDataFolder)));
        }

        private static EnvService instance = new EnvService();
        public static EnvService Instance { get { return instance; } }

        public string FeedHistoryCacheFolder { get; private set; }
        public string ApplicationName { get; private set; }
        public string LogFolder { get; private set; }
        public string JournalFolder { get; private set; }
        public string AlgoRepositoryFolder { get; private set; }
        public IObjectStorage UserDataStorage { get; private set; }
        public IObjectStorage ProtectedUserDataStorage { get; private set; }

        private void EnsureFolder(string folderPath)
        {
            try
            {
                Directory.CreateDirectory(folderPath);
            }
            catch (IOException ex)
            {
                logger.Error("Cannot create directory {0}: {1}", folderPath, ex.Message);
            }
        }
    }
}
