﻿using Machinarium.State;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TickTrader.Algo.Core.Lib;
using TickTrader.Algo.Core.Metadata;

namespace TickTrader.Algo.Core.Repository
{
    public class AlgoRepository : IDisposable
    {
        private const string AlgoFilesPattern = "*.dll";

        public enum States { Created, Scanning, Waiting, Watching, Closing, Closed }
        public enum Events { Start, DoneScanning, ScanFailed, NextAttempt, CloseRequested, DoneClosing }

        private StateMachine<States> stateControl = new StateMachine<States>();
        private object scanUpdateLockObj = new object();
        private object globalLockObj = new object();
        private FileSystemWatcher watcher;
        private bool isWatcherFailed;
        private Task scanTask;
        private string repPath;
        private Dictionary<string, AlgoAssembly> assemblies = new Dictionary<string, AlgoAssembly>();

        public AlgoRepository(string repPath)
        {
            this.repPath = repPath;

            stateControl.AddTransition(States.Created, Events.Start, States.Scanning);
            stateControl.AddTransition(States.Scanning, Events.DoneScanning, States.Watching);
            stateControl.AddTransition(States.Scanning, Events.CloseRequested, States.Closing);
            stateControl.AddTransition(States.Scanning, Events.ScanFailed, States.Waiting);
            stateControl.AddTransition(States.Waiting, Events.NextAttempt, States.Scanning);
            stateControl.AddTransition(States.Waiting, Events.CloseRequested, States.Closing);
            stateControl.AddTransition(States.Watching, Events.CloseRequested, States.Closing);
            stateControl.AddTransition(States.Watching, () => isWatcherFailed, States.Scanning);
            stateControl.AddTransition(States.Closing, Events.DoneClosing, States.Closed);

            stateControl.AddScheduledEvent(States.Waiting, Events.NextAttempt, 1000);

            stateControl.OnEnter(States.Scanning, () => scanTask = Task.Factory.StartNew(Scan));
        }

        public event Action<AlgoPluginRef> Added = delegate { };
        public event Action<AlgoPluginRef> Removed = delegate { };
        public event Action<AlgoPluginRef> Replaced = delegate { };

        public void Start()
        {
            stateControl.PushEvent(Events.Start);
        }

        public Task Stop()
        {
            return stateControl.PushEventAndWait(Events.CloseRequested, States.Closed);
        }

        private void Scan()
        {
            try
            {
                if (watcher != null)
                {
                    watcher.Dispose();
                    watcher.Changed -= watcher_Changed;
                    watcher.Created -= watcher_Changed;
                    watcher.Deleted -= watcher_Deleted;
                    watcher.Renamed -= watcher_Renamed;
                    watcher.Error += watcher_Error;
                }

                isWatcherFailed = false;

                watcher = new FileSystemWatcher(repPath);
                watcher.Path = repPath;
                watcher.IncludeSubdirectories = false;
                watcher.Filter = AlgoFilesPattern;

                watcher.Changed += watcher_Changed;
                watcher.Created += watcher_Changed;
                watcher.Deleted += watcher_Deleted;
                watcher.Renamed += watcher_Renamed;

                lock (scanUpdateLockObj)
                {
                    watcher.EnableRaisingEvents = true;

                    string[] fileList = Directory.GetFiles(repPath, AlgoFilesPattern, SearchOption.AllDirectories);
                    foreach (string file in fileList)
                    {
                        if (stateControl.Current == States.Closing)
                            break;

                        AlgoAssembly assemblyMetadata;
                        if (!assemblies.TryGetValue(file, out assemblyMetadata))
                        {
                            assemblyMetadata = new AlgoAssembly(file);
                            assemblyMetadata.Added += m => Added(m);
                            assemblyMetadata.Removed += m => Removed(m);
                            assemblyMetadata.Replaced += m => Replaced(m);
                            assemblies.Add(file, assemblyMetadata);
                        }
                        else
                            assemblyMetadata.CheckForChanges();
                    }
                }

                stateControl.PushEvent(Events.DoneScanning);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                stateControl.PushEvent(Events.ScanFailed);
            }
        }

        void watcher_Error(object sender, ErrorEventArgs e)
        {
            stateControl.ModifyConditions(() => isWatcherFailed = true);
        }

        void watcher_Renamed(object sender, RenamedEventArgs e)
        {
            lock (scanUpdateLockObj)
            {
                AlgoAssembly assembly;
                if (assemblies.TryGetValue(e.OldFullPath, out assembly))
                {
                    assemblies.Remove(e.OldFullPath);
                    assemblies.Add(e.FullPath, assembly);
                    assembly.Rename(e.FullPath);
                }
                else if (assemblies.TryGetValue(e.FullPath, out assembly))
                {
                    // I dunno
                }
                else
                {
                    assembly = new AlgoAssembly(e.FullPath);
                    assemblies.Add(e.FullPath, assembly);
                }
            }
        }

        void watcher_Deleted(object sender, FileSystemEventArgs e)
        {
        }

        void watcher_Changed(object sender, FileSystemEventArgs e)
        {
            lock (scanUpdateLockObj)
            {
                AlgoAssembly assembly;
                if (assemblies.TryGetValue(e.FullPath, out assembly))
                    assembly.CheckForChanges();
                else
                {
                    assembly = new AlgoAssembly(e.FullPath);
                    assemblies.Add(e.FullPath, assembly);
                }
            }
        }

        public void Dispose()
        {
        }
    }
}
