var mempool = 0;
var trackerHeight = 0;
var headerHeight = 0;
var blocksLeft = "";
var nodeCount = 0;
var walletState = "NotStarted";
var torState = "NotStarted";
var progressType = "success";
var progress = "10";
var connection;
var mixerStatusResult;
var isTumblerOnline = false;

function signalrStatusUpdate() {

    connection = new signalR.HubConnection('http://localhost:37120/daemonHub');

    var headerTimer;

    connection.on('mempoolChanged', data => {
        mempool = parseInt(data);
        statusSignalRShow();
    });

    connection.on('trackerHeightChanged', data => {
        trackerHeight = parseInt(data);
        statusSignalRShow();
    });

    connection.on('headerHeightChanged', data => {
        headerHeight = parseInt(data);
        statusSignalRShow();
    });

    connection.on('nodeCountChanged', data => {
        nodeCount = parseInt(data);
        statusSignalRShow();
    });

    connection.on('walletStateChanged', data => {
        walletState = data;

        if (walletState.toUpperCase() === "SyncingHeaders".toUpperCase()) {
            headerTimer = setInterval(getHeaderHeight, 1000);
        }

        if (walletState.toUpperCase() === "Synced".toUpperCase()) {
            clearInterval(headerTimer);
        }

        httpGetTumblerAsync("connection", function (json) { }); // makes sure status response is acquired at least one and subscribed to the tumbler
        tumblerStatusBroadcastRequest();

        statusSignalRShow();
    });

    connection.on('changeBump', data => {
        updateWalletContent();
    });

    connection.on('torStateChanged', data => {
        console.log("Tor State: " + data);
        torState = data;
        updateDecryptButton();
        statusSignalRShow();
    });

    connection.on('mixerStatusChanged', data => {
        mixerStatusResult = JSON.parse(data);
        isTumblerOnline = mixerStatusResult.IsTumblerOnline;
        updateMixerTab();
        updateMixerContent();
    });

    connection.start().then(function () {
        console.log("Connected to SignalR on Daemon");
        connection.invoke('GetTorStatus');
    }).then(function () {
        console.log("Tumbler Status Request");
        connection.invoke('TumblerStatusBroadcastRequest'); //Request that the status is broadcast
    }).catch(error => {
        console.error(error.message);
    });
}

function statusSignalRShow() {

    var status = document.getElementById("status");

    let text = "";
    let blocksLeftDisplay = "";
    let mempoolDisplay = "";
    let connectionText = "Connecting...";

    if (parseInt(nodeCount) !== 0) {
        connectionText = "Connections: " + nodeCount;
    }

    if (trackerHeight !== 0) {
        blocksLeft = (headerHeight - trackerHeight).toString();
        blocksLeftDisplay = ", Blocks left: " + blocksLeft.toString();
    }

    mempoolDisplay = ", MemPool txs: " + mempool.toString();

    if (walletState.toUpperCase() === "NotStarted".toUpperCase()) {
        progressType = "info";
        text = "Tor circuit established, Wallet is offline";
    }

    if (walletState.toUpperCase() === "SyncingHeaders".toUpperCase()) {
        progressType = "info progress-bar-striped active";
        text = walletState + ", " + connectionText + ", Headers: " + headerHeight.toString();
    }

    if (walletState.toUpperCase() === "SyncingBlocks".toUpperCase()) {
        progressType = "striped active";
        text = walletState + ", " + connectionText + ", Headers: " + headerHeight.toString() + blocksLeftDisplay;
    }

    if (walletState.toUpperCase() === "SyncingMemPool".toUpperCase()) {
        progressType = "success";
        text = connectionText + ", Headers: " + headerHeight.toString() + blocksLeftDisplay + mempoolDisplay;
    }

    if (walletState.toUpperCase() === "Synced".toUpperCase()) {
        progressType = "success";
        text = walletState + ", " + connectionText + ", Headers: " + headerHeight.toString() + blocksLeftDisplay + mempoolDisplay;
    }

    if (parseInt(nodeCount) === 0 && walletState.toUpperCase() !== "NotStarted".toUpperCase()) {
        progressType = "info progress-bar-striped";
        text = "Connecting. . .";
    }

    if (torState.toUpperCase() === "CircuitEstablished".toUpperCase()) {
        progress = 100;
    }

    if (torState.toUpperCase() === "EstablishingCircuit".toUpperCase()) {
        progress = 100;
        text = "Establishing Tor circuit...";
    }

    if (torState.toUpperCase() === "NotStarted".toUpperCase()) {
        progress = 100;
        text = "Tor is not running";
        progressType = "danger";
    }

    if (progressType === "") {
        status.innerHTML = '<div class="progress" style="margin:0"><div class="progress-bar" role="progressbar" style="width:' + progress + '%;"><span><strong>' + text + '</strong></span></div></div>';
    }
    else {
        status.innerHTML = '<div class="progress" style="margin:0"><div class="progress-bar progress-bar-' + progressType + ' role="progressbar" style="width:' + progress + '%;"><span><strong>' + text + '</strong></span></div></div>';
    }
}

function updateDecryptButton() {

    try {
        let decButton = document.getElementById("decrypt-wallet-button");

        if (null !== decButton) {
            if (torState.toUpperCase() === "CircuitEstablished".toUpperCase()) {
                if (decButton.innerText === "Waiting for Tor...") {
                    decButton.innerText = "Decrypt";
                }

                if (decButton.hasAttribute("disabled")) {
                    decButton.removeAttribute("disabled");
                }
            }

            if (torState.toUpperCase() === "EstablishingCircuit".toUpperCase()) {
                decButton.innerText = "Waiting for Tor...";
                decButton.disabled = true;
            }

            if (torState.toUpperCase() === "NotStarted".toUpperCase()) {
                decButton.innerText = "Waiting for Tor...";
                decButton.disabled = true;
            }
        }
    }
    catch (err) {
        console.error(err);
    }
}

function getHeaderHeight() {
    connection.invoke('GetHeaderHeightAsync');
}

function tumblerStatusBroadcastRequest() {
    connection.invoke('TumblerStatusBroadcastRequest');
}

function updateMixerTab() {
    try {
        var mixerTabs = document.getElementsByClassName("mixer-tab-link");
        for (var i = 0; i < mixerTabs.length; i++) {
            var tab = mixerTabs[i];
            if (isTumblerOnline === false) {
                tab.style.backgroundColor = "blanchedalmond";
            }
            else {
                tab.style.backgroundColor = "";
            }
        }
    }
    catch (err) {
        console.info(err.message);
    }
}

var tumblerDenomination;
var tumblerAnonymitySet;
var tumblerNumberOfPeers;
var tumblerFeePerRound;
var tumblerWaitedInInputRegistration;
var tumblerPhase;
function updateMixerContent() {
    if (mixerStatusResult === void 0) { mixerStatusResult = null; }
    try {
        if (mixerStatusResult !== null) {
            if (mixerStatusResult.Success === true) {

                tumblerDenomination = mixerStatusResult.TumblerDenomination;
                tumblerAnonymitySet = mixerStatusResult.TumblerAnonymitySet;
                tumblerNumberOfPeers = mixerStatusResult.TumblerNumberOfPeers;
                tumblerFeePerRound = mixerStatusResult.TumblerFeePerRound;
                tumblerWaitedInInputRegistration = mixerStatusResult.TumblerWaitedInInputRegistration;
                tumblerPhase = mixerStatusResult.TumblerPhase;

                var denominationElem = document.getElementById("tumbler-denomination");
                var anonymitySetElem = document.getElementById("tumbler-anonymity-set");
                var peerCountElem = document.getElementById("tumbler-peer-count");
                var tumblerFeePerRoundElem = document.getElementById("tumbler-fee-per-round");
                var timeSpentWaitingElem = document.getElementById("tumbler-time-spent-waiting");
                var currentPhaseElem = document.getElementById("tumbler-current-phase");
                if (null !== currentPhaseElem) {
                    denominationElem.innerText = tumblerDenomination + " BTC";
                    anonymitySetElem.innerText = tumblerAnonymitySet;
                    peerCountElem.innerText = tumblerNumberOfPeers;
                    tumblerFeePerRoundElem.innerText = tumblerFeePerRound + " BTC";
                    timeSpentWaitingElem.innerText = tumblerWaitedInInputRegistration + " minutes";
                    currentPhaseElem.innerText = tumblerPhase;
                }
            }
        }
    }
    catch (err) {
        console.log(err.message);
    }
}