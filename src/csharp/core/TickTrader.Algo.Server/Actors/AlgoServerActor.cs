﻿using Google.Protobuf;
using System.IO;
using System.Threading.Tasks;
using TickTrader.Algo.Async.Actors;
using TickTrader.Algo.Core.Lib;
using TickTrader.Algo.Domain;
using TickTrader.Algo.Domain.ServerControl;
using TickTrader.Algo.Package;
using TickTrader.Algo.Rpc;
using TickTrader.Algo.Rpc.OverTcp;
using TickTrader.Algo.Server.Persistence;

namespace TickTrader.Algo.Server
{
    internal class AlgoServerActor : Actor
    {
        public const string InternalApiAddress = "127.0.0.1";

        private static readonly IAlgoLogger _logger = AlgoLoggerFactory.GetLogger<AlgoServerActor>();

        private readonly AlgoServerSettings _settings;

        private EnvService _env;
        private IActorRef _eventBus, _stateManager;
        private ServerStateModel _savedState;
        private AlertManagerModel _alerts;
        private AlgoServerPrivate _serverPrivate;
        private PackageStorage _pkgStorage;
        private RuntimeManager _runtimes;
        private AccountManager _accounts;
        private PluginManager _plugins;
        private PluginFileManager _pluginFiles;
        private RpcServer _internalApiServer;

        private MappingCollectionInfo _mappings;


        private AlgoServerActor(AlgoServerSettings settings)
        {
            _settings = settings;

            Receive<EventBusRequest, IActorRef>(_ => _eventBus);

            Receive<StartCmd>(Start);
            Receive<StopCmd>(Stop);
            Receive<NeedLegacyStateRequest, bool>(_ => !File.Exists(_env.ServerStateFilePath));
            Receive<LoadLegacyStateCmd>(cmd => _savedState.LoadSavedState(cmd.SavedState));

            Receive<AlgoServerPrivate.LockPkgRefCmd>(cmd => _pkgStorage.LockPkgRef(cmd.Id));
            Receive<AlgoServerPrivate.ReleasePkgRefCmd>(cmd => _pkgStorage.ReleasePkgRef(cmd.Id));
            Receive<AlgoServerPrivate.RuntimeStoppedMsg>(msg => _runtimes.OnRuntimeStopped(msg.Id));
            Receive<AlgoServerPrivate.ConnectRuntimeCmd>(cmd => _runtimes.ConnectRuntime(cmd.Id, cmd.Session));
            Receive<AlgoServerPrivate.AccountControlRequest, AccountControlModel>(r => _accounts.GetAccountControl(r.Id));
            Receive<AlgoServerPrivate.PkgRuntimeRequest, PkgRuntimeModel>(r => _runtimes.GetPkgRuntime(r.PkgId));

            Receive<LocalAlgoServer.PkgFileExistsRequest, bool>(r => _pkgStorage.PackageFileExists(r.PkgName));
            Receive<LocalAlgoServer.PkgBinaryRequest, byte[]>(r => _pkgStorage.GetPackageBinary(r.Id));
            Receive<LocalAlgoServer.UploadPackageCmd>(cmd => _pkgStorage.UploadPackage(cmd.Request, cmd.FilePath));
            Receive<RemovePackageRequest>(r => _pkgStorage.RemovePackage(r));
            Receive<MappingsInfoRequest, MappingCollectionInfo>(_ => _mappings);

            Receive<AddAccountRequest>(r => _accounts.AddAccount(r));
            Receive<ChangeAccountRequest>(r => _accounts.ChangeAccount(r));
            Receive<RemoveAccountRequest>(RemoveAccount);
            Receive<TestAccountRequest>(r => _accounts.TestAccount(r));
            Receive<TestAccountCredsRequest>(r => _accounts.TestAccountCreds(r));
            Receive<AccountMetadataRequest>(r => _accounts.GetMetadata(r));

            Receive<LocalAlgoServer.PluginExistsRequest, bool>(r => _plugins.PluginExists(r.Id));
            Receive<LocalAlgoServer.GeneratePluginIdRequest, string>(r => _plugins.GeneratePluginId(r.PluginDisplayName));
            Receive<AddPluginRequest>(r => _plugins.AddPlugin(r));
            Receive<ChangePluginConfigRequest>(r => _plugins.UpdateConfig(r));
            Receive<RemovePluginRequest>(r => _plugins.RemovePlugin(r));
            Receive<StartPluginRequest>(r => _plugins.StartPlugin(r));
            Receive<StopPluginRequest>(r => _plugins.StopPlugin(r));
            Receive<PluginLogsRequest, PluginLogRecord[]>(r => _plugins.GetPluginLogs(r));
            Receive<PluginStatusRequest, string>(r => _plugins.GetPluginStatus(r));

            Receive<PluginFolderInfoRequest, PluginFolderInfo>(r => _pluginFiles.GetFolderInfo(r));
            Receive<ClearPluginFolderRequest>(r => _pluginFiles.ClearFolder(r));
            Receive<DeletePluginFileRequest>(r => _pluginFiles.DeleteFile(r));
            Receive<DownloadPluginFileRequest, string>(r => _pluginFiles.GetFileReadPath(r));
            Receive<UploadPluginFileRequest, string>(r => _pluginFiles.GetFileWritePath(r));

            Receive<PluginAlertsRequest, AlertRecordInfo[]>(r => _alerts.GetAlerts(r));
        }


        public static IActorRef Create(AlgoServerSettings settings)
        {
            return ActorSystem.SpawnLocal(() => new AlgoServerActor(settings), $"{nameof(AlgoServerActor)}");
        }


        protected override void ActorInit(object initMsg)
        {
            var reductions = new ReductionCollection();
            reductions.LoadDefaultReductions();
            _mappings = reductions.CreateMappings();

            _env = new EnvService(_settings.DataFolder);
            _eventBus = ServerBusActor.Create();
            _stateManager = ServerStateManager.Create(_env.ServerStateFilePath);
            _alerts = new AlertManagerModel(AlertManager.Create());
            _savedState = new ServerStateModel(_stateManager);
            _serverPrivate = new AlgoServerPrivate(Self, _env, _eventBus, _savedState, _alerts);
            _serverPrivate.AccountOptions = Account.ConnectionOptions.CreateForServer(_settings.EnableAccountLogs, _env.LogFolder);

            _pkgStorage = new PackageStorage(_eventBus);
            _runtimes = new RuntimeManager(_serverPrivate);
            _accounts = new AccountManager(_serverPrivate);
            _plugins = new PluginManager(_serverPrivate);
            _pluginFiles = new PluginFileManager(_serverPrivate);

            _internalApiServer = new RpcServer(new TcpFactory(), _serverPrivate);
        }


        public async Task Start(StartCmd cmd)
        {
            _logger.Debug("Starting...");

            await _pkgStorage.Start(_settings.PkgStorage, OnPackageRefUpdate);

            await _pkgStorage.WhenLoaded();

            await _internalApiServer.Start(InternalApiAddress, 0);
            _logger.Info($"Started AlgoServer internal API on port {_internalApiServer.BoundPort}");

            _serverPrivate.Address = InternalApiAddress;
            _serverPrivate.BoundPort = _internalApiServer.BoundPort;

            var stateSnapshot = await _savedState.GetSnapshot();

            _accounts.Restore(stateSnapshot);

            await _plugins.Restore(stateSnapshot);

            _logger.Debug("Started");
        }

        public async Task Stop(StopCmd cmd)
        {
            _logger.Debug("Stopping...");

            await _savedState.StopSaving();

            await _plugins.Shutdown();

            await _pkgStorage.Stop();

            await _runtimes.Shutdown();

            await _accounts.Shutdown();

            await _internalApiServer.Stop();

            _logger.Debug("Stopped");
        }


        private void OnPackageRefUpdate(PackageVersionUpdate update)
        {
            var pkgId = update.PkgId;
            var pkgRefId = update.LatestPkgRefId;

            _runtimes.GetPkgRuntime(pkgId)?.MarkForShutdown();

            if (string.IsNullOrEmpty(pkgRefId))
                return;

            CreateRuntime(pkgId);
        }

        private void CreateRuntime(string pkgId)
        {
            var pkgRef = _pkgStorage.GetPkgRef(pkgId);

            var pkgInfo = pkgRef.PkgInfo;
            if (!pkgInfo.IsValid)
            {
                _logger.Debug($"Skipped creating runtime for pkg ref '{pkgRef.Id}'");
                return;
            }

            _runtimes.CreateRuntime(pkgRef.Id.Replace('/', '-'), pkgRef);
        }

        private async Task RemoveAccount(RemoveAccountRequest request)
        {
            var accId = request.AccountId;
            var account = _accounts.GetAccountRefOrThrow(accId);

            await _plugins.RemoveAllPluginsFromAccount(accId);

            await _accounts.RemoveAccountInternal(accId, account);
        }


        internal class EventBusRequest : Singleton<EventBusRequest> { }

        internal class StartCmd : Singleton<StartCmd> { }

        internal class StopCmd : Singleton<StopCmd> { }

        internal class NeedLegacyStateRequest : Singleton<NeedLegacyStateRequest> { }

        internal class LoadLegacyStateCmd
        {
            public ServerSavedState SavedState { get; }

            public LoadLegacyStateCmd(ServerSavedState savedState)
            {
                SavedState = savedState;
            }
        }
    }
}
