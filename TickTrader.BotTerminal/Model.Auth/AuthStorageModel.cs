﻿using Machinarium.Qnil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace TickTrader.BotTerminal
{
    [DataContract(Namespace = "")]
    public class AuthStorageModel : IChangableObject, IPersistableObject<AuthStorageModel>
    {
        [DataMember(Name = "Accounts")]
        private DynamicList<AccountSorageEntry> accounts;
        private string lastLogin;
        private string lastServer;

        public AuthStorageModel()
        {
            accounts = new DynamicList<AccountSorageEntry>();
        }

        [DataMember]
        public string LastLogin
        {
            get { return lastLogin; }
            set { lastLogin = value; }
        }

        [DataMember]
        public string LastServer
        {
            get { return lastServer; }
            set { lastServer = value; }
        }

        public void UpdateLast(string login, string server)
        {
            lastLogin = login;
            lastServer = server;
        }

        public void TriggerSave()
        {
            OnChanged();
        }

        public AuthStorageModel(AuthStorageModel src)
        {
            accounts = new DynamicList<AccountSorageEntry>(src.accounts.Values.Select(a => a.Clone()));
            lastLogin = src.lastLogin;
            lastServer = src.lastServer;
        }

        public DynamicList<AccountSorageEntry> Accounts { get { return accounts; } }

        public void Remove(AccountSorageEntry account)
        {
            var index = accounts.Values.IndexOf(a => a.Login == account.Login && a.ServerAddress == account.ServerAddress);
            if (index != -1)
                accounts.RemoveAt(index);
        }

        public void Update(AccountSorageEntry account)
        {
            int index = accounts.Values.IndexOf(a => a.Login == account.Login && a.ServerAddress == account.ServerAddress);
            if (index < 0)
                accounts.Values.Add(account);
            else
            {
                if (accounts.Values[index].Password != account.Password)
                    accounts.Values[index].Password = account.Password;
            }
        }

        public event Action Changed;

        private void OnChanged()
        {
            if (this.Changed != null)
                Changed();
        }

        public AuthStorageModel GetCopyToSave()
        {
            return new AuthStorageModel(this);
        }

        
    }

    [DataContract(Namespace = "")]
    public class AccountSorageEntry
    {
        private string password;
        private string login;
        private string server;

        public AccountSorageEntry()
        {
        }

        public AccountSorageEntry(string login, string password, string server)
        {
            this.login = login;
            this.password = password;
            this.server = server;
        }

        [DataMember]
        public string Login { get { return login; } set { login = value; } }

        [DataMember]
        public string ServerAddress { get { return server; } set { server = value; } }

        public bool HasPassword { get { return password != null; } }

        [DataMember]
        public string Password { get { return password; } set { password = value; } }

        public AccountSorageEntry Clone()
        {
            return new AccountSorageEntry(Login, Password, ServerAddress);
        }
    }
}