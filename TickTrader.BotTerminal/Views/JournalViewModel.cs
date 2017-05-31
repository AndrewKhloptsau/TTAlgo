﻿using Caliburn.Micro;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Threading;
using Machinarium.Qnil;

namespace TickTrader.BotTerminal
{
    class JournalViewModel : PropertyChangedBase
    {
        private EventJournal eventJournal;
        private string filterString;
        private readonly Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public JournalViewModel(EventJournal journal)
        {
            eventJournal = journal;
            Journal = CollectionViewSource.GetDefaultView(eventJournal.Records.AsObservable());
            Journal.Filter = new Predicate<object>(Filter);
        }

        public ICollectionView Journal { get; private set; }
        public string FilterString
        {
            get { return filterString; }
            set
            {
                filterString = value;
                NotifyOfPropertyChange(nameof(FilterString));
                RefreshCollection();
            }
        }

        private void RefreshCollection()
        {
            if (this.Journal != null)
                Journal.Refresh();
        }

        public void Browse()
        {
            try
            {
                Directory.CreateDirectory(EnvService.Instance.JournalFolder);
                Process.Start(EnvService.Instance.JournalFolder);
            }
            catch (Exception ex)
            {
                logger.Warn(ex,"Failed to browse journal folder");
            }
        }

        public void Clear()
        {
            eventJournal.Clear();
        }

        private bool Filter(object obj)
        {
            var data = obj as EventMessage;
            if (data != null)
            {
                if (!string.IsNullOrEmpty(filterString))
                    return data.Time.ToString(FullDateTimeConverter.Format).IndexOf(filterString, StringComparison.OrdinalIgnoreCase) >= 0 
                        || data.Message.IndexOf(filterString, StringComparison.OrdinalIgnoreCase) >= 0;
                return true;
            }
            return false;
        }
    }
}
