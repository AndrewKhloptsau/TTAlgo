﻿import { PackageModel, AccountModel, TradeBotStateModel, TradeBotModel } from './ba-models';

export interface FeedSignalR extends SignalR {
    bAFeed: FeedProxy;
}

export interface FeedProxy {
    client: FeedClient;
    server: FeedServer;
}

export interface FeedClient {
    deletePackage: (packageName: string) => void;
    addOrUpdatePackage: (algoPackage: PackageModel) => void;
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
    Reconnecting = 2,
    Disconnected = 3,
    Error = 4
}