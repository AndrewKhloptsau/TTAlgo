﻿using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace TickTrader.BotAgent.Configurator
{
    public class ConfigurationViewModel : IDisposable
    {
        private Window _mainWindow;

        private AgentVersionManager _versionManager;
        private ConfigurationModel _model;

        private bool _runnignApplication;

        private DelegateCommand _startAgent;
        private DelegateCommand _restartApplication;
        private DelegateCommand _saveChanges;
        private DelegateCommand _cancelChanges;

        public CredentialViewModel AdminModel { get; set; }

        public CredentialViewModel DealerModel { get; set; }

        public CredentialViewModel ViewerModel { get; set; }

        public SslViewModel SslModel { get; set; }

        public ProtocolViewModel ProtocolModel { get; set; }

        public ServerViewModel ServerModel { get; set; }

        public StateServiceViewModel StateServiceModel { get; set; }

        public FdkViewModel FdkModel { get; set; }

        public AdvancedViewModel AdvancedModel { get; set; }

        public LogsViewModel LogsModel { get; set; }

        public RefreshManager RefreshManager { get; }

        public bool WasUpdate => RefreshManager.Update;

        public ConfigurationViewModel()
        {
            try
            {
                _mainWindow = Application.Current.MainWindow;

                _runnignApplication = true;
                _model = new ConfigurationModel();

                RefreshManager = new RefreshManager();

                _versionManager = new AgentVersionManager(_model.BotAgentHolder.BotAgentPath, _model.Settings[AppProperties.ApplicationName]);

                AdminModel = new CredentialViewModel(_model.CredentialsManager.Admin, RefreshManager)
                {
                    ModelDescription = _model.Prompts.GetPrompt(SectionNames.Credentials, _model.CredentialsManager.Admin.Name)
                };

                DealerModel = new CredentialViewModel(_model.CredentialsManager.Dealer, RefreshManager)
                {
                    ModelDescription = _model.Prompts.GetPrompt(SectionNames.Credentials, _model.CredentialsManager.Dealer.Name)
                };

                ViewerModel = new CredentialViewModel(_model.CredentialsManager.Viewer, RefreshManager)
                {
                    ModelDescription = _model.Prompts.GetPrompt(SectionNames.Credentials, _model.CredentialsManager.Viewer.Name)
                };


                SslModel = new SslViewModel(_model.SslManager.SslModel, RefreshManager);

                ProtocolModel = new ProtocolViewModel(_model.ProtocolManager.ProtocolModel, RefreshManager)
                {
                    ListeningPortDescription = _model.Prompts.GetPrompt(SectionNames.Protocol, ProtocolManager.PortNameProperty),
                    DirectoryNameDescription = _model.Prompts.GetPrompt(SectionNames.Protocol, ProtocolManager.DirectoryNameProperty),
                    LogMessageDescription = _model.Prompts.GetPrompt(SectionNames.Protocol, ProtocolManager.UseLogNameProperty),
                };

                ServerModel = new ServerViewModel(_model.ServerManager.ServerModel, RefreshManager)
                {
                    UrlsDescription = _model.Prompts.GetPrompt(SectionNames.Server, ServerManager.UrlsNameProperty),
                    SecretKeyDescription = _model.Prompts.GetPrompt(SectionNames.Server, ServerManager.SecretKeyNameProperty),
                };

                FdkModel = new FdkViewModel(_model.FdkManager.FdkModel, RefreshManager)
                {
                    ModelDescription = _model.Prompts.GetPrompt(SectionNames.Fdk, FdkManager.EnableLogsNameProperty),
                };

                AdvancedModel = new AdvancedViewModel(_model.Settings, RefreshManager)
                {
                    ModelDescription = _model.Prompts.GetPrompt(SectionNames.MultipleAgentProvider, MultipleAgentConfigurator.AgentCongPathsNameSection),
                };

                StateServiceModel = new StateServiceViewModel(_model.Settings[AppProperties.ServiceName]);

                RefreshManager.NewValuesEvent += () => StateServiceModel.VisibleRestartMessage = true;
                RefreshManager.SaveValuesEvent += () => StateServiceModel.VisibleRestartMessage = false;

                ThreadPool.QueueUserWorkItem(RefreshServiceState);

                _mainWindow.Title = $"BotAgent Configurator: {_versionManager.FullVersion}";
                _mainWindow.Closing += MainWindow_Closing;
            }
            catch (Exception ex)
            {
                MessageBoxManager.ErrorBox(ex.Message);
                Application.Current.Shutdown();
            }
        }

        public DelegateCommand StartAgent => _startAgent ?? (
            _startAgent = new DelegateCommand(obj =>
            {
                if (SaveChangesQuestion())
                {
                    try
                    {
                        if (_model.StartAgent())
                            MessageBoxManager.OKBox("Agent has been started!");
                    }
                    catch (WarningException ex)
                    {
                        MessageBoxManager.WarningBox(ex.Message);
                    }
                    catch (Exception exx)
                    {
                        MessageBoxManager.ErrorBox(exx.Message);
                    }
                }
            }));

        public DelegateCommand RestartApplication => _restartApplication ?? (
            _restartApplication = new DelegateCommand(obj =>
            {
                _mainWindow.Closing -= MainWindow_Closing;
                SaveChangesMethod();
                System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
                Application.Current.Shutdown();
            }));

        public DelegateCommand SaveChanges => _saveChanges ?? (
            _saveChanges = new DelegateCommand(obj =>
            {
                try
                {
                    _model.SaveChanges();
                    RefreshManager.DropRefresh();
                    MessageBoxManager.OKBox("Saving configuration successfully!");
                }
                catch (Exception ex)
                {
                    MessageBoxManager.ErrorBox(ex.Message);
                }
            }));

        public DelegateCommand CancelChanges => _cancelChanges ?? (
            _cancelChanges = new DelegateCommand(obj =>
            {
                if (MessageBoxManager.YesNoBoxQuestion("The model has been changed. Сancel changes?"))
                {
                    try
                    {
                        _model.LoadConfiguration();
                        RefreshManager.DropRefresh();
                        RefreshModels();
                    }
                    catch (Exception ex)
                    {
                        MessageBoxManager.ErrorBox(ex.Message);
                        Application.Current.Shutdown();
                    }
                }
            }));

        public void RefreshModels()
        {
            ServerModel.ResetSetting();

            AdminModel.RefreshModel();
            DealerModel.RefreshModel();
            ViewerModel.RefreshModel();

            SslModel.RefreshModel();
            ProtocolModel.RefreshModel();
            ServerModel.RefreshModel();
            FdkModel.RefreshModel();
        }

        public void Dispose()
        {
            _runnignApplication = false;
        }

        private async void RefreshServiceState(object obj)
        {
            LogsModel = new LogsViewModel(_model.Logs);

            while (_runnignApplication)
            {
                await Task.Delay(5000);
                StateServiceModel.RefreshService(_model.ServiceManager.ServiceStatus);
                LogsModel.RefreshLog();
            }
        }

        private void SaveChangesMethod()
        {
            try
            {
                SaveChangesQuestion();
            }
            catch
            {
                MessageBoxManager.ErrorBox("Saving settings was failed");
            }
            finally
            {
                Dispose();
            }
        }

        private bool SaveChangesQuestion()
        {
            if (WasUpdate)
            {
                var result = MessageBoxManager.YesNoBoxQuestion("The model has been changed. Save changes?");

                if (result)
                    SaveChangesMethod();

                return result;
            }

            return !WasUpdate;
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            SaveChangesMethod();
        }
    }

    public class RefreshManager : INotifyPropertyChanged
    {
        private bool _update;

        public delegate void ConfigurationStateChanged();

        public event ConfigurationStateChanged NewValuesEvent;
        public event ConfigurationStateChanged SaveValuesEvent;

        public bool Update
        {
            get => _update;

            private set
            {
                if (_update == value)
                    return;

                _update = value;
                OnPropertyChanged(nameof(Update));
            }
        }

        public void Refresh()
        {
            Update = true;

            NewValuesEvent?.Invoke();
        }

        public void DropRefresh()
        {
            Update = false;

            SaveValuesEvent?.Invoke();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged([CallerMemberName]string prop = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }

    public interface IContentViewModel : INotifyPropertyChanged
    {
        string ModelDescription { get; set; }

        void RefreshModel();

        void OnPropertyChanged([CallerMemberName]string prop = "");
    }
}
