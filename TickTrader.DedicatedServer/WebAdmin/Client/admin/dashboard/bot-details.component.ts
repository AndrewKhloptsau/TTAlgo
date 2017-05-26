﻿import { Component, OnInit, OnDestroy } from '@angular/core';
import { Observable } from "rxjs/Rx";
import { ApiService, ToastrService } from '../../services/index';
import { Router, ActivatedRoute, Params } from '@angular/router';
import { TradeBotModel, TradeBotLog, ObservableRequest, TradeBotStates, TradeBotStateModel, ResponseStatus, LogEntryTypes, TradeBotStatus, File } from '../../models/index';

@Component({
    selector: 'bot-details-cmp',
    template: require('./bot-details.component.html'),
    styles: [require('../../app.component.css')]
})

export class BotDetailsComponent implements OnInit {
    public TradeBotState = TradeBotStates;
    public LogEntryType = LogEntryTypes;
    public Bot: TradeBotModel;
    public Log: TradeBotLog;
    public AlgoData: File[];
    public Status: string;

    public BotRequest: ObservableRequest<TradeBotModel>;
    public LogRequest: ObservableRequest<TradeBotLog>;
    public AlgoDataRequest: ObservableRequest<File[]>;
    public StatusRequest: ObservableRequest<TradeBotStatus>;

    constructor(
        private _route: ActivatedRoute,
        private _api: ApiService,
        private _router: Router,
        private _toastr: ToastrService
    ) { }

    ngOnInit() {
        this._route.params
            .subscribe((params: Params) => {
                this.BotRequest = new ObservableRequest(params['id'] ?
                    this._api.GetTradeBot(params['id']) :
                    Observable.of(<TradeBotModel>null)
                ).Subscribe(result => this.Bot = result);

                this.LogRequest = new ObservableRequest(params['id'] ?
                    this._api.GetTradeBotLog(params['id']) :
                    Observable.of(<TradeBotLog>null)
                ).Subscribe(result => this.Log = result);

                this.AlgoDataRequest = new ObservableRequest(params['id'] ?
                    this._api.GetTradeBotAlgoData(params['id']) :
                    Observable.of(<File[]>[])
                ).Subscribe(result => {
                    this.AlgoData = result;
                });

                this.StatusRequest = new ObservableRequest(params['id'] ?
                    this._api.GetTradeBotStatus(params['id']) :
                    Observable.of(<TradeBotStatus>null)
                ).Subscribe(result => {
                    if (result) { this.Status = result.Status; }
                    else { this.Status = ""; }
                });
            });

        this._api.Feed.ChangeBotState
            .filter(state => this.Bot && this.Bot.Id == state.Id)
            .subscribe(botState => this.updateBotState(botState));
    }

    public DonwloadAlgoDataLink(botId: string, file: string) {
        return this._api.GetDownloadAlgoDataUrl(botId, file);
    }

    public DonwloadLogLink(botId: string, file: string) {
        return this._api.GetDownloadLogUrl(botId, file);
    }

    public get IsOnline(): boolean {
        return this.Bot.State === TradeBotStates.Online;
    }

    public get IsProcessing(): boolean {
        return this.Bot.State === TradeBotStates.Starting
            || this.Bot.State === TradeBotStates.Reconnecting
            || this.Bot.State === TradeBotStates.Stopping;
    }

    public get IsOffline(): boolean {
        return this.Bot.State === TradeBotStates.Offline;
    }

    public get Faulted(): boolean {
        return this.Bot.State === TradeBotStates.Faulted;
    }

    public get Broken(): boolean {
        return this.Bot.State === TradeBotStates.Broken;
    }

    public get CanStop(): boolean {
        return (this.Bot.State === TradeBotStates.Online
            || this.Bot.State === TradeBotStates.Starting
            || this.Bot.State === TradeBotStates.Reconnecting) && !this.Broken;
    }

    public get CanStart(): boolean {
        return (this.Bot.State === TradeBotStates.Offline
            || this.Bot.State === TradeBotStates.Faulted) && !this.Broken;
    }

    public get CanDelete(): boolean {
        return this.Bot.State === TradeBotStates.Offline
            || this.Bot.State === TradeBotStates.Faulted
            || this.Broken;
    }

    public get CanConfigurate(): boolean {
        return (this.Bot.State === TradeBotStates.Offline
            || this.Bot.State === TradeBotStates.Faulted) && !this.Broken;
    }

    public Start(botId: string) {
        this.Bot = <TradeBotModel>{ ...this.Bot, State: TradeBotStates.Starting }

        this._api.StartBot(botId).subscribe(
            ok => { },
            err => this.notifyAboutError(err)
        );
    }

    public Stop(botId: string) {
        this._api.StopBot(botId).subscribe(
            ok => { },
            err => this.notifyAboutError(err)
        );
    }

    public Delete(botId: string, cleanLog: boolean, claenAlgoData: boolean) {
        this._api.DeleteBot(botId, cleanLog, claenAlgoData).subscribe(ok => {
            this._router.navigate(["/dashboard"]);
        },
            err => this.notifyAboutError(err)
        );
    }

    public DeleteLogFile(botId: string, file: string) {
        this._api.DeleteLogFile(botId, file).subscribe(
            ok => this.Log.Files = this.Log.Files.filter(f => f.Name !== file),
            err => this.notifyAboutError(err)
        );
    }

    public DeleteAlgoDataFile(botId: string, file: string) {

    }

    public Configurate(botId: string) {
        if (botId)
            this._router.navigate(['/configurate', botId]);
    }

    private updateBotState(botState: TradeBotStateModel) {
        this.Bot = <TradeBotModel>{ ...this.Bot, State: botState.State, FaultMessage: botState.FaultMessage }
    }

    private notifyAboutError(response: ResponseStatus) {
        this._toastr.error(response.Message);
    }

}