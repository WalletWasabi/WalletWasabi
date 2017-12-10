var mempool = 0;
var trackerHeight = 0;
var headerHeight = 0;
var blocksLeft = "";
var nodeCount = 0;
var walletState = "NotStarted";
var torState = "NotStarted";
var progressType = "success";
var progress = "10";
var signalRConnection;
var mixerStatusResult;
var isTumblerOnline = false;

function signalrStatusUpdate() {
    initializeStatus();

    signalRConnection = new signalR.HubConnection('http://localhost:37120/daemonHub');

    var headerTimer;

    signalRConnection.on('mempoolChanged', data => {
        mempool = parseInt(data);
        statusSignalRShow();
    });

    signalRConnection.on('trackerHeightChanged', data => {
        trackerHeight = parseInt(data);
        statusSignalRShow();
    });

    signalRConnection.on('headerHeightChanged', data => {
        headerHeight = parseInt(data);
        statusSignalRShow();
    });

    signalRConnection.on('nodeCountChanged', data => {
        nodeCount = parseInt(data);
        statusSignalRShow();
    });

    signalRConnection.on('walletStateChanged', data => {
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

    signalRConnection.on('changeBump', data => {
        updateWalletContent();
    });

    signalRConnection.on('torStateChanged', data => {
        console.log("Tor State: " + data);
        torState = data;
        updateDecryptButton();
        statusSignalRShow();
    });

    signalRConnection.on('mixerStatusChanged', data => {
        mixerStatusResult = JSON.parse(data);
        isTumblerOnline = mixerStatusResult.IsTumblerOnline;
        updateMixerTab();
        updateMixerContent();
    });

    signalRConnection.start().then(function () {
        console.log("Connected to SignalR on Daemon");
        signalRConnection.invoke('GetTorStatusAsync');
    }).then(function () {
        console.log("Tumbler Status Request");
        signalRConnection.invoke('TumblerStatusBroadcastRequestAsync'); //Request that the status is broadcast
    }).catch(error => {
        console.error(error.message);
        });
}

function initializeStatus() {
    let response = httpGetWallet("status");

    walletState = response.WalletState;
    headerHeight = parseInt(response.HeaderHeight);
    trackerHeight = parseInt(response.TrackingHeight);
    nodeCount = parseInt(response.ConnectedNodeCount);
    mempool = parseInt(response.MemPoolTransactionCount);
    torState = response.TorState;
}

function statusSignalRShow() {
    let text = "";
    let blocksLeftDisplay = "";
    let mempoolDisplay = "";
    let connectionText = "Connecting...";

    if (parseInt(nodeCount) !== 0) {
        connectionText = "Connections: " + nodeCount;
    }

    if (trackerHeight !== 0) {
        blocksLeft = (headerHeight - trackerHeight).toString();
        if (blocksLeft !== "0") {
            blocksLeftDisplay = ", Blocks left: " + blocksLeft.toString();
        }
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

    var progressBarDivElem = document.getElementById("progress-bar-div");
    if (progressType === "") {
        progressBarDivElem.className = "progress-bar";
    }
    else {
        progressBarDivElem.className = "progress-bar progress-bar-" + progressType;
    }

    var progressBarTextElem = document.getElementById("progress-bar-text");
    progressBarTextElem.innerText = text;
    
    progressBarDivElem.style.width = progress + "%";
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
    signalRConnection.invoke('GetHeaderHeightAsync');
}

function tumblerStatusBroadcastRequest() {
    signalRConnection.invoke('TumblerStatusBroadcastRequestAsync');
}

function updateMixerTab() {
    httpGetTumblerAsync("connection", function (json) {
        try {
            var mixerTabs = document.getElementsByClassName("mixer-tab-link");
            for (var i = 0; i < mixerTabs.length; i++) {
                var tab = mixerTabs[i];
                if (json.Success === false) {
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
    });
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

                var phaseProgressBarDiv = document.getElementById("phase-progress-bar-div");
                var percentProgressBarDiv = document.getElementById("peercount-progress-bar-div");
                if (null !== currentPhaseElem) {
                    denominationElem.innerText = tumblerDenomination + " BTC";
                    tumblerFeePerRoundElem.innerText = tumblerFeePerRound + " BTC";
                    timeSpentWaitingElem.innerText = tumblerWaitedInInputRegistration + " minutes";

                    anonymitySetElem.innerText = tumblerAnonymitySet;
                    peerCountElem.innerText = tumblerNumberOfPeers;

                    let total = parseInt(tumblerAnonymitySet);
                    let actual = parseInt(tumblerNumberOfPeers);
                    let perc = "";
                    if (isNaN(total) || isNaN(actual)) {
                        perc = "0";
                    } else {
                        perc = Math.round((actual / total) * 100);
                    }

                    if (perc === 100) {
                        percentProgressBarDiv.className = "progress-bar progress-bar-success";
                    }
                    else if (perc > 80)
                    {
                        percentProgressBarDiv.className = "progress-bar progress-bar-success progress-bar-striped active";
                    }
                    else if (perc > 50) {
                        percentProgressBarDiv.className = "progress-bar progress-bar-info progress-bar-striped active";
                    }
                    else {
                        percentProgressBarDiv.className = "progress-bar progress-bar-info";
                    }

                    percentProgressBarDiv.style.width = perc + "%";

                    currentPhaseElem.innerText = tumblerPhase;

                    if (tumblerPhase == "InputRegistration") {
                        phaseProgressBarDiv.className = "progress-bar progress-bar-info";
                        phaseProgressBarDiv.style.width = "0%";
                    }
                    if (tumblerPhase == "ConnectionConfirmation") {
                        phaseProgressBarDiv.className = "progress-bar progress-bar-info progress-bar-striped active";
                        phaseProgressBarDiv.style.width = "70%";
                    }
                    if (tumblerPhase == "OutputRegistration") {
                        phaseProgressBarDiv.className = "progress-bar progress-bar-success progress-bar-striped active";
                        phaseProgressBarDiv.style.width = "80%";
                    }
                    if (tumblerPhase == "Signing") {
                        phaseProgressBarDiv.className = "progress-bar progress-bar-success progress-bar-striped active";
                        phaseProgressBarDiv.style.width = "98%";
                    }
                }
            }
        }
    }
    catch (err) {
        console.log(err.message);
    }
}