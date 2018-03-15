﻿using System;
using System.Collections.Generic;
using TickTrader.Algo.Core;
using TickTrader.Algo.Core.Lib;
using NLog;
using NLog.Targets;
using NLog.Layouts;
using NLog.Config;
using System.IO;
using TickTrader.BotAgent.Extensions;
using System.Linq;
using TickTrader.Algo.Common.Model;

namespace TickTrader.BotAgent.BA.Models
{
    public class BotLog : IBotLog, IBotWriter
    {
        private object _internalSync = new object();
        private object _sync;
        private CircularList<ILogEntry> _logMessages;
        private ILogger _logger;
        private int _keepInmemory;
        private string _name;
        private string _logDirectory;
        private readonly string _fileExtension = ".txt";

        public BotLog(string name, object sync, int keepInMemory = 100)
        {
            _sync = sync;
            _name = name;
            _logDirectory = $"{ServerModel.Environment.BotLogFolder}/{_name.Escape()}/";
            _keepInmemory = keepInMemory;
            _logMessages = new CircularList<ILogEntry>(_keepInmemory);
            _logger = GetLogger(name);
        }

        public string Status { get; private set; }

        public string Folder => _logDirectory;

        public IEnumerable<ILogEntry> Messages
        {
            get
            {
                lock (_sync)
                    return _logMessages.ToArray();
            }
        }

        public IFile[] Files
        {
            get
            {
                if (Directory.Exists(_logDirectory))
                {
                    DirectoryInfo dInfo = new DirectoryInfo(_logDirectory);
                    return dInfo.GetFiles($"*{_fileExtension}").Select(fInfo => new ReadOnlyFileModel(fInfo.FullName)).ToArray();
                }
                else
                    return new ReadOnlyFileModel[0];
            }
        }

        public event Action<string> StatusUpdated;

        private void WriteLog(LogEntryType type, string message)
        {
            lock (_internalSync)
            {
                var msg = new LogEntry(type, message);

                switch (type)
                {
                    case LogEntryType.Custom:
                    case LogEntryType.Info:
                    case LogEntryType.Trading:
                    case LogEntryType.TradingSuccess:
                    case LogEntryType.TradingFail:
                        _logger.Info(msg.ToString());
                        break;
                    case LogEntryType.Error:
                        _logger.Error(msg.ToString());
                        break;
                }

                if (IsLogFull)
                    _logMessages.Dequeue();

                _logMessages.Add(msg);
            }
        }

        private bool IsLogFull
        {
            get { return _logMessages.Count >= _keepInmemory; }
        }

        private ILogger GetLogger(string botId)
        {
            var logTarget = $"all-{botId}";
            var errTarget = $"error-{botId}";
            var statusTarget = $"status-{botId}";

            var logFile = new FileTarget(logTarget)
            {
                FileName = Layout.FromString($"{_logDirectory}${{shortdate}}-log{_fileExtension}"),
                Layout = Layout.FromString("${longdate} | ${logger} | ${message}")
            };

            var errorFile = new FileTarget(errTarget)
            {
                FileName = Layout.FromString($"{_logDirectory}${{shortdate}}-error{_fileExtension}"),
                Layout = Layout.FromString("${longdate} | ${logger} | ${message}")
            };

            var statusFile = new FileTarget(statusTarget)
            {
                FileName = Layout.FromString($"{_logDirectory}${{shortdate}}-status{_fileExtension}"),
                Layout = Layout.FromString("${longdate} | ${logger} | ${message}")
            };

            var logConfig = new LoggingConfiguration();
            logConfig.AddTarget(logFile);
            logConfig.AddTarget(errorFile);
            logConfig.AddTarget(statusFile);
            logConfig.AddRule(LogLevel.Trace, LogLevel.Trace, statusTarget);
            logConfig.AddRule(LogLevel.Debug, LogLevel.Fatal, logTarget);
            logConfig.AddRule(LogLevel.Error, LogLevel.Fatal, errTarget);

            var nlogFactory = new LogFactory(logConfig);

            return nlogFactory.GetLogger(botId);
        }

        public IFile GetFile(string file)
        {
            if (file.IsFileNameValid())
            {
                var fullPath = Path.Combine(_logDirectory, file);

                return new ReadOnlyFileModel(fullPath);
            }

            throw new ArgumentException($"Incorrect file name {file}");
        }

        public void Clear()
        {
            lock (_sync)
            {
                _logMessages.Clear();

                if (Directory.Exists(_logDirectory))
                {
                    new DirectoryInfo(_logDirectory).Clean();
                    Directory.Delete(_logDirectory);
                }
            }
        }

        public void DeleteFile(string file)
        {
            File.Delete(Path.Combine(_logDirectory, file));
        }

        private static LogEntryType Convert(LogSeverities severity)
        {
            switch (severity)
            {
                case LogSeverities.Custom: return LogEntryType.Custom;
                case LogSeverities.Error: return LogEntryType.Error;
                case LogSeverities.Info: return LogEntryType.Info;
                case LogSeverities.Trade: return LogEntryType.Trading;
                case LogSeverities.TradeSuccess: return LogEntryType.TradingSuccess;
                case LogSeverities.TradeFail: return LogEntryType.TradingFail;
                default: return LogEntryType.Info;
            }
        }


        #region IBotWriter implementation

        void IBotWriter.LogMesssages(IEnumerable<BotLogRecord> records)
        {
            lock (_sync)
            {
                foreach (var rec in records)
                {
                    if (rec.Severity != LogSeverities.CustomStatus)
                        WriteLog(Convert(rec.Severity), rec.Message);
                }
            }
        }

        void IBotWriter.UpdateStatus(string status)
        {
            lock (_sync)
            {
                Status = status;
                StatusUpdated?.Invoke(status);
            }
        }

        void IBotWriter.LogStatus(string status)
        {
            lock (_sync)
            {
                _logger.Trace(status);
            }
        }

        #endregion IBotWriter implementation
    }
}