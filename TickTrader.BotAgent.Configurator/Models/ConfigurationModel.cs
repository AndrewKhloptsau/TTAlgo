﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TickTrader.BotAgent.Configurator
{
    public enum SectionNames {None, Credentials, Ssl, Protocol, Fdk, Server, MultipleAgentProvider }

    public class ConfigurationModel
    {
        private ConfigManager _configManager;
        private PortsManager _portsManager;
        private RegistryManager _registryManager;

        private DefaultServiceSettingsModel _defaultServiceSettings;

        private JObject _configurationObject;

        private readonly List<IUploaderModels> _uploaderModels;

        public ServiceManager ServiceManager { get; }

        public ConfigurationProperies Settings { get; }

        public CredentialsManager CredentialsManager { get; }

        public SslManager SslManager { get; }

        public ProtocolManager ProtocolManager { get; }

        public ServerManager ServerManager { get; }

        public FdkManager FdkManager { get; }

        public PrompterManager Prompts { get; }

        public LogsManager Logs { get; private set; }

        public MultipleAgentConfigurator BotAgentHolder { get; }

        public ConfigurationModel()
        {
            _defaultServiceSettings = new DefaultServiceSettingsModel();
            _configManager = new ConfigManager();

            Settings = _configManager.Properties;
            Prompts = new PrompterManager();

            BotAgentHolder = Settings.MultipleAgentProvider;
            _registryManager = new RegistryManager(Settings[AppProperties.RegistryAppName], Settings[AppProperties.AppSettings]);

            ServiceManager = new ServiceManager(Settings[AppProperties.ServiceName]);
            _portsManager = new PortsManager(ServiceManager, _defaultServiceSettings);

            CredentialsManager = new CredentialsManager(SectionNames.Credentials);
            SslManager = new SslManager(SectionNames.Ssl);
            ProtocolManager = new ProtocolManager(SectionNames.Protocol, _portsManager);
            FdkManager = new FdkManager(SectionNames.Fdk);
            ServerManager = new ServerManager();

            _uploaderModels = new List<IUploaderModels>() { CredentialsManager, SslManager, ProtocolManager, ServerManager, FdkManager };

            LoadConfiguration(false);
        }

        public void StartAgent()
        {
            string ports = $"{string.Join(",", ServerManager.ServerModel.Urls.Select(u => u.Port.ToString()))},{ProtocolManager.ProtocolModel.ListeningPort}";

            _portsManager.RegisterRuleInFirewall(Settings[AppProperties.ApplicationName], Path.Combine($"{BotAgentHolder.BotAgentPath}{Settings[AppProperties.ApplicationName]}.exe"), ports, Settings[AppProperties.ServiceName]);

            if (ServiceManager.IsServiceRunning)
                 ServiceManager.ServiceStop();

             ServiceManager.ServiceStart();
        }

        public void LoadConfiguration(bool loadConfig = true)
        {
            if (loadConfig)
                _configManager.LoadProperties();

            BotAgentHolder.SetNewBotAgentPath(_registryManager.BotAgentPath, Settings[AppProperties.AppSettings]);

            Logs = new LogsManager(BotAgentHolder.BotAgentPath, Settings[AppProperties.LogsPath]);

            using (var configStreamReader = new StreamReader(BotAgentHolder.BotAgentConfigPath))
            {
                _configurationObject = JObject.Parse(configStreamReader.ReadToEnd());

                foreach (var uploader in _uploaderModels)
                    UploadModel(uploader);
            }

            UploadDefaultServiceModel();
            _configManager.SaveChanges();
        }

        public void SaveChanges()
        {
            foreach (var uploader in _uploaderModels)
                uploader.SaveConfigurationModels(_configurationObject);

            using (var configStreamWriter = new StreamWriter(BotAgentHolder.BotAgentConfigPath))
            {
                configStreamWriter.Write(_configurationObject.ToString());
            }

            _configManager.SaveChanges();
            UploadDefaultServiceModel();
        }

        private void UploadModel(IUploaderModels model)
        {
            try
            {
                var args = model.SectionName == "" ? _configurationObject.Values<JProperty>() : _configurationObject[model.SectionName].Children<JProperty>();
                model.UploadModels(args.ToList());
            }
            catch (NullReferenceException ex)
            {
                Logger.Error(ex);

                if (MessageBoxManager.YesNoBoxError($"Loading section {model.SectionName} failed. Apply default settings?"))
                {
                    model.SetDefaultModelValues();
                }
                else
                    throw new Exception("Unable to load settings");
            }
        }

        private void UploadDefaultServiceModel()
        {
            _defaultServiceSettings.ListeningPort = ProtocolManager.ProtocolModel.ListeningPort;
        }
    }


    public interface IUploaderModels
    {
        string SectionName { get; }

        void UploadModels(List<JProperty> prop);

        void SetDefaultModelValues();

        void SaveConfigurationModels(JObject root);
    }

    public interface IBotAgentConfigPathHolder
    {
        string BotAgentPath { get; }

        string BotAgentConfigPath { get; }
    }
}
