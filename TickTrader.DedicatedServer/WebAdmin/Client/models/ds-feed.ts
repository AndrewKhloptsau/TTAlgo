﻿import { PackageModel } from './package-model';
import { AccountModel } from './account-model';
import { TradeBotStateModel, TradeBotModel } from './trade-bot-model';

export interface FeedSignalR extends SignalR {
    dSFeed: FeedProxy;
}

export interface FeedProxy {
    client: FeedClient;
    server: FeedServer;
}

export interface FeedClient {
    deletePackage: (packageName: string) => void;
    addPackage: (algoPackage: PackageModel) => void;
    deleteAccount: (account: AccountModel) => void;
    addAccount: (account: AccountModel) => void;
    changeBotState: (state: TradeBotStateModel) => void;
    addBot: (bot: TradeBotModel) => void;
    deleteBot: (botId: string) => void;
    updateBot: (bot: TradeBotModel) => void;
}

export interface FeedServer {
    
}

export enum ConnectionStatus {
    Connected = 1,
    Disconnected = 2,
    Error = 3
}