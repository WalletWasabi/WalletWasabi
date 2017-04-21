'use strict';

var app = angular.module('WalletApp', []);

document.addEventListener('DOMContentLoaded', function () {
    angular.bootstrap(document, ['WalletApp']);
});

app.controller('WalletCtrl', function (WalletService) {
    var ctrl = this;

    LoadWallet();

    function LoadWallet() {
        WalletService.Get()
            .then(function (wallet) {
                ctrl.Wallet = wallet;
            }, function (error) {
                ctrl.ErrorMessage = error;
            });
    }
});

app.service('WalletService', function ($http) {
    var svc = this;
    var apiUrl = 'http://localhost:5000/api/v1';

    svc.Get = function () {
        return $http.get(apiUrl + '/wallet')
            .then(function success(response) {
                return response.data;
            });
    };
});