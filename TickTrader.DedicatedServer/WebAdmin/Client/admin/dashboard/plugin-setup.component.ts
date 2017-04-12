﻿import { Input, Component } from '@angular/core';
import { PluginModel, ParameterDataTypes, AccountModel, PluginSetupModel } from '../../models/index';
import { ApiService } from '../../services/index';

@Component({
    selector: 'plugin-setup-cmp',
    template: require('./plugin-setup.component.html'),
    styles: [require('../../app.component.css')],
})

export class PluginSetupComponent {
    public ParameterDataType = ParameterDataTypes;
    public Symbols: string[];

    @Input() Setup: PluginSetupModel;
    @Input() Accounts: AccountModel[];

    constructor(private _api: ApiService) { }


    OnAccountChanged(account: AccountModel) {
        this._api.GetSymbols(account).subscribe(symbols => this.Symbols = symbols);
    }

}