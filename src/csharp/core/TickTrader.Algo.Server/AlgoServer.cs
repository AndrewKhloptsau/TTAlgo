﻿using System;
using System.Threading.Tasks;
using TickTrader.Algo.Core.Lib;
using TickTrader.Algo.Domain;
using TickTrader.Algo.Package;
using TickTrader.Algo.Rpc;
using TickTrader.Algo.Rpc.OverTcp;
using TickTrader.Algo.Server.Persistence;

namespace TickTrader.Algo.Server
{
    public class AlgoServer : IRpcHost
    {
        private static readonly IAlgoLogger _logger = AlgoLoggerFactory.GetLogger<AlgoServer>();

        private readonly RpcServer _rpcServer;
        private readonly AlgoServerSettings _settings;


        public string Address { get; } = "127.0.0.1";

        public int BoundPort => _rpcServer.BoundPort;

        public EnvService Env { get; }

        public ServerBusModel EventBus { get; }

        public AlertManagerModel Alerts { get; }

        public PackageStorage PkgStorage { get; } = new PackageStorage();

        public AccountManagerModel Accounts { get; }

        public RuntimeManagerModel Runtimes { get; }

        public PluginManagerModel Plugins { get; }

        public PluginFileManagerModel PluginFiles { get; }


        internal ServerStateModel SavedState { get; }


        public AlgoServer(AlgoServerSettings settings)
        {
            _settings = settings;

            Env = new EnvService(settings.DataFolder);

            EventBus = new ServerBusModel(ServerBusActor.Create());
            Alerts = new AlertManagerModel(AlertManager.Create());

            Accounts = new AccountManagerModel(AccountManager.Create(this));
            Runtimes = new RuntimeManagerModel(RuntimeManager.Create(this));
            Plugins = new PluginManagerModel(PluginManager.Create(this));

            PluginFiles = new PluginFileManagerModel(PluginFileManager.Create(this));

            SavedState = new ServerStateModel(ServerStateManager.Create(Env.ServerStateFilePath));

            _rpcServer = new RpcServer(new TcpFactory(), this);
        }


        public async Task Start()
        {
            PkgStorage.PackageUpdated.Subscribe(upd => EventBus.SendUpdate(upd));

            foreach (var assembly in _settings.PkgStorage.Assemblies)
                await PkgStorage.RegisterAssemblyAsPackage(assembly);
            foreach (var loc in _settings.PkgStorage.Locations)
                await PkgStorage.RegisterRepositoryLocation(loc.LocationId, loc.Path, loc.LocationId == _settings.PkgStorage.UploadLocationId);

            await PkgStorage.WaitLoaded();

            await _rpcServer.Start(Address, 0);
            _logger.Info($"Started AlgoServer internal API on port {BoundPort}");

            await Accounts.Restore();

            await Plugins.Restore();
        }

        public async Task Stop()
        {
            _logger.Debug("Stopping...");

            await SavedState.StopSaving();

            await Plugins.Shutdown();

            await PkgStorage.Stop();

            await Runtimes.Shutdown();

            await Accounts.Shutdown();

            await _rpcServer.Stop();

            _logger.Debug("Stopped");
        }

        public async Task<ExecutorModel> CreateExecutor(string pkgId, string instanceId, ExecutorConfig config)
        {
            var runtime = await Runtimes.GetPkgRuntime(pkgId);
            if (runtime == null)
                throw new ArgumentException($"Package doesn't exists '{pkgId}'");
            await runtime.Start();

            return await runtime.CreateExecutor(instanceId, config);
        }

        public Task LoadSavedData(ServerSavedState serverState)
        {
            return SavedState.LoadSavedState(serverState);
        }

        #region IRpcHost implementation

        ProtocolSpec IRpcHost.Resolve(ProtocolSpec protocol, out string error)
        {
            error = string.Empty;
            return protocol;
        }

        IRpcHandler IRpcHost.GetRpcHandler(ProtocolSpec protocol)
        {
            switch (protocol.Url)
            {
                case KnownProtocolUrls.RuntimeV1:
                    return new ServerRuntimeV1Handler(this);
            }
            return null;
        }

        #endregion IRpcHost implementation
    }
}
