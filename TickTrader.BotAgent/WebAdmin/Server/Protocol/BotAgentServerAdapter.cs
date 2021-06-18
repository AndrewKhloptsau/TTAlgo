﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TickTrader.Algo.Core.Lib;
using TickTrader.Algo.Domain;
using TickTrader.Algo.Domain.ServerControl;
using TickTrader.Algo.ServerControl;
using TickTrader.BotAgent.BA;
using TickTrader.BotAgent.BA.Models;
using TickTrader.BotAgent.WebAdmin.Server.Models;
using TickTrader.Algo.Package;

namespace TickTrader.BotAgent.WebAdmin.Server.Protocol
{
    public class BotAgentServerAdapter : IAlgoServerProvider
    {
        private static IAlgoLogger _logger = AlgoLoggerFactory.GetLogger<BotAgentServerAdapter>();
        private static readonly SetupContextInfo _agentContext = new SetupContextInfo(Feed.Types.Timeframe.M1,
            new SymbolConfig("none", SymbolConfig.Types.SymbolOrigin.Online), MappingDefaults.DefaultBarToBarMapping.Key);


        private readonly IBotAgent _botAgent;
        private readonly IAuthManager _authManager;


        public event Action AdminCredsChanged = delegate { };
        public event Action DealerCredsChanged = delegate { };
        public event Action ViewerCredsChanged = delegate { };

        public event Action<UpdateInfo<PackageInfo>> PackageUpdated = delegate { };
        public event Action<UpdateInfo<AccountModelInfo>> AccountUpdated = delegate { };
        public event Action<UpdateInfo<PluginModelInfo>> BotUpdated = delegate { };
        public event Action<PackageStateUpdate> PackageStateUpdated = delegate { };
        public event Action<PluginStateUpdate> BotStateUpdated = delegate { };
        public event Action<AccountStateUpdate> AccountStateUpdated = delegate { };


        public BotAgentServerAdapter(IBotAgent botAgent, IAuthManager authManager)
        {
            _botAgent = botAgent;
            _authManager = authManager;

            _botAgent.AccountChanged += OnAccountChanged;
            _botAgent.BotChanged += OnBotChanged;
            _botAgent.PackageChanged += OnPackageChanged;
            _botAgent.BotStateChanged += OnBotStateChanged;
            _botAgent.AccountStateChanged += OnAccountStateChanged;
            _botAgent.PackageStateChanged += OnPackageStateChanged;

            _authManager.AdminCredsChanged += OnAdminCredsChanged;
            _authManager.DealerCredsChanged += OnDealerCredsChanged;
            _authManager.ViewerCredsChanged += OnViewerCredsChanged;
        }


        public ClientClaims.Types.AccessLevel ValidateCreds(string login, string password)
        {
            if (_authManager.ValidViewerCreds(login, password))
                return ClientClaims.Types.AccessLevel.Viewer;
            if (_authManager.ValidDealerCreds(login, password))
                return ClientClaims.Types.AccessLevel.Dealer;
            if (_authManager.ValidAdminCreds(login, password))
                return ClientClaims.Types.AccessLevel.Admin;

            return ClientClaims.Types.AccessLevel.Anonymous;
        }

        public Task<List<AccountModelInfo>> GetAccountList()
        {
            return _botAgent.GetAccounts();
        }

        public Task<List<PluginModelInfo>> GetBotList()
        {
            return _botAgent.GetBots();
        }

        public Task<List<PackageInfo>> GetPackageList()
        {
            return _botAgent.GetPackageSnapshot();
        }

        public Task<ApiMetadataInfo> GetApiMetadata()
        {
            return Task.FromResult(ApiMetadataInfo.CreateCurrentMetadata());
        }

        public Task<MappingCollectionInfo> GetMappingsInfo()
        {
            return _botAgent.GetMappingsInfo();
        }

        public Task<SetupContextInfo> GetSetupContext()
        {
            return Task.FromResult(_agentContext);
        }

        public async Task<AccountMetadataInfo> GetAccountMetadata(AccountMetadataRequest request)
        {
            var accountId = request.AccountId;

            var (error, accMetadata) = await _botAgent.GetAccountMetadata(accountId);
            if (!error.IsOk)
                throw new Exception($"Account '{accountId}' failed to connect");
            return accMetadata;
        }

        public Task StartBot(StartPluginRequest request)
        {
            return _botAgent.StartBot(request);
        }

        public Task StopBot(StopPluginRequest request)
        {
            return _botAgent.StopBotAsync(request);
        }

        public Task AddBot(AddPluginRequest request)
        {
            return _botAgent.AddBot(request);
        }

        public Task RemoveBot(RemovePluginRequest request)
        {
            return _botAgent.RemoveBot(request);
        }

        public Task ChangeBotConfig(ChangePluginConfigRequest request)
        {
            return _botAgent.ChangeBotConfig(request);
        }

        public Task AddAccount(AddAccountRequest request)
        {
            return _botAgent.AddAccount(request);
        }

        public Task RemoveAccount(RemoveAccountRequest request)
        {
            return _botAgent.RemoveAccount(request);
        }

        public Task ChangeAccount(ChangeAccountRequest request)
        {
            return _botAgent.ChangeAccount(request);
        }

        public Task<ConnectionErrorInfo> TestAccount(TestAccountRequest request)
        {
            return _botAgent.TestAccount(request);
        }

        public Task<ConnectionErrorInfo> TestAccountCreds(TestAccountCredsRequest request)
        {
            return _botAgent.TestCreds(request);
        }

        public Task RemovePackage(RemovePackageRequest request)
        {
            return _botAgent.RemovePackage(request);
        }

        public Task UploadPackage(UploadPackageRequest request, string pkgFilePath)
        {
            return _botAgent.UploadPackage(request, pkgFilePath);
        }

        public Task<byte[]> GetPackageBinary(DownloadPackageRequest request)
        {
            return _botAgent.DownloadPackage(request.PackageId);
        }

        public async Task<string> GetBotStatusAsync(PluginStatusRequest request)
        {
            var log = await _botAgent.GetBotLog(request.PluginId);
            return await log.GetStatusAsync();
        }

        public async Task<LogRecordInfo[]> GetBotLogsAsync(PluginLogsRequest request)
        {
            var log = await _botAgent.GetBotLog(request.PluginId);
            var msgs = await log.QueryMessagesAsync(request.LastLogTimeUtc, request.MaxCount);

            return msgs.Select(e => new LogRecordInfo
            {
                TimeUtc = e.TimeUtc,
                Severity = e.Severity,
                Message = e.Message,
            }).ToArray();
        }

        public async Task<AlertRecordInfo[]> GetAlertsAsync(PluginAlertsRequest request)
        {
            var storage = await _botAgent.GetAlertStorage();
            var alerts = await storage.QueryAlertsAsync(request.LastLogTimeUtc, request.MaxCount);

            return alerts.Select(e => new AlertRecordInfo
            {
                TimeUtc = e.TimeUtc,
                Message = e.Message,
                PluginId = e.BotId,
            }).ToArray();
        }

        public async Task<PluginFolderInfo> GetBotFolderInfo(PluginFolderInfoRequest request)
        {
            var botId = request.PluginId;
            var folderId = request.FolderId;

            var botFolder = await GetBotFolder(botId, folderId);

            var res = new PluginFolderInfo
            {
                PluginId = botId,
                FolderId = folderId,
                Path = await botFolder.GetFolder(),
            };
            res.Files.AddRange((await botFolder.GetFiles()).Select(f => new PluginFileInfo { Name = f.Name, Size = f.Size }));
            return res;
        }

        public async Task ClearBotFolder(ClearPluginFolderRequest request)
        {
            var botFolder = await GetBotFolder(request.PluginId, request.FolderId);

            await botFolder.Clear();
        }

        public async Task DeleteBotFile(DeletePluginFileRequest request)
        {
            var botFolder = await GetBotFolder(request.PluginId, request.FolderId);

            await botFolder.DeleteFile(request.FileName);
        }

        public async Task<string> GetBotFileReadPath(DownloadPluginFileRequest request)
        {
            var botFolder = await GetBotFolder(request.PluginId, request.FolderId);

            return await botFolder.GetFileReadPath(request.FileName);
        }

        public async Task<string> GetBotFileWritePath(UploadPluginFileRequest request)
        {
            var botFolder = await GetBotFolder(request.PluginId, request.FolderId);

            return await botFolder.GetFileWritePath(request.FileName);
        }


        private UpdateInfo.Types.UpdateType Convert(ChangeAction action)
        {
            switch (action)
            {
                case ChangeAction.Added:
                    return UpdateInfo.Types.UpdateType.Added;
                case ChangeAction.Modified:
                    return UpdateInfo.Types.UpdateType.Replaced;
                case ChangeAction.Removed:
                    return UpdateInfo.Types.UpdateType.Removed;
                default:
                    throw new ArgumentException();
            }
        }

        private async Task<IBotFolder> GetBotFolder(string botId, PluginFolderInfo.Types.PluginFolderId folderId)
        {
            switch (folderId)
            {
                case PluginFolderInfo.Types.PluginFolderId.AlgoData:
                    return await _botAgent.GetAlgoData(botId);
                case PluginFolderInfo.Types.PluginFolderId.BotLogs:
                    return await _botAgent.GetBotLog(botId);
                default:
                    throw new ArgumentException();
            }
        }

        #region Event handlers

        private void OnAccountChanged(AccountModelInfo account, ChangeAction action)
        {
            try
            {
                AccountUpdated(new UpdateInfo<AccountModelInfo>(Convert(action), account));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to send update: {ex.Message}");
            }
        }

        private void OnBotChanged(PluginModelInfo bot, ChangeAction action)
        {
            try
            {
                BotUpdated(new UpdateInfo<PluginModelInfo>(Convert(action), bot));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to send update: {ex.Message}");
            }
        }

        private void OnPackageChanged(PackageUpdate update)
        {
            try
            {
                UpdateInfo<PackageInfo> updateInfo = default;
                switch (update.Action)
                {
                    case Package.Types.UpdateAction.Upsert:
                        updateInfo = new UpdateInfo<PackageInfo>(UpdateInfo.Types.UpdateType.Replaced, update.Package);
                        break;
                    case Package.Types.UpdateAction.Removed:
                        updateInfo = new UpdateInfo<PackageInfo>(UpdateInfo.Types.UpdateType.Removed, new PackageInfo { PackageId = update.Id });
                        break;
                }
                if (updateInfo != null)
                    PackageUpdated(updateInfo);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to send update: {ex.Message}");
            }
        }

        private void OnBotStateChanged(PluginStateUpdate bot)
        {
            try
            {
                BotStateUpdated(bot);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to send update: {ex.Message}");
            }
        }

        private void OnAccountStateChanged(AccountStateUpdate account)
        {
            try
            {
                AccountStateUpdated(account);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to send update: {ex.Message}");
            }
        }

        private void OnPackageStateChanged(PackageStateUpdate package)
        {
            try
            {
                PackageStateUpdated(package);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to send update: {ex.Message}");
            }
        }

        private void OnAdminCredsChanged()
        {
            AdminCredsChanged?.Invoke();
        }

        private void OnDealerCredsChanged()
        {
            DealerCredsChanged?.Invoke();
        }

        private void OnViewerCredsChanged()
        {
            ViewerCredsChanged?.Invoke();
        }

        #endregion Event handlers
    }
}
