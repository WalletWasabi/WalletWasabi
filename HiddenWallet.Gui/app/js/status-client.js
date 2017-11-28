var mempool = "";
var trackerHeight = 0;
var headerHeight = 0;
var blocksLeft = "";
var nodeCount = 0;
var walletState = "NotStarted";
var torState = "NotStarted";
var progressType = "success";
var progress = "10";
let connection;

function signalrStatusUpdate() {

    connection = new signalR.HubConnection('http://localhost:37120/daemonHub');

    var headerTimer;
    
    connection.on('mempoolChanged', data => {
        mempool = data;
        statusSignalRShow();
    });

    connection.on('trackerHeightChanged', data => {
        trackerHeight = parseInt(data);
        blocksLeft = (headerHeight - trackerHeight).toString();
        statusSignalRShow();
    });

    connection.on('headerHeightChanged', data => {
        headerHeight = parseInt(data);
        blocksLeft = (headerHeight - trackerHeight).toString();
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

        if (walletState.toUpperCase() === "SyncingBlocks".toUpperCase()) {
            clearInterval(headerTimer);
        }

        statusSignalRShow();
    });

    connection.on('changeBump', data => {
        updateWalletContent();
    });

    connection.on('torStateChanged', data => {
        console.log("Tor State: " + data);
        torState = data;
        updateDecryptButton(data);
        statusSignalRShow();
    });

    connection.start().then(function () {
        console.log("Connected to SignalR on Daemon");
        connection.invoke('GetTorStatus');
    }).catch(error => {
        console.error(error.message);
    });
}

function statusSignalRShow() {

    var status = document.getElementById("status");

    let text = "";

    let connectionText = "Connecting...";

    if (parseInt(nodeCount) !== 0) {
        connectionText = "Connections: " + nodeCount;
    }

    if (trackerHeight !== 0) {
        blocksLeft = (headerHeight - trackerHeight).toString();
    }

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
        text = walletState + ", " + connectionText + ", Headers: " + headerHeight.toString() + ", Blocks left: " + blocksLeft.toString();
    }

    if (walletState.toUpperCase() === "SyncingMemPool".toUpperCase()) {
        progressType = "success"; 
        text = connectionText + ", Headers: " + headerHeight.toString() + ", Blocks left: " + blocksLeft.toString() + ", MemPool txs: " + mempool.toString();
    }

    if (walletState.toUpperCase() === "Synced".toUpperCase()) {
        progressType = "success";
        text = walletState + ", " + connectionText + ", Headers: " + headerHeight.toString() + ", Blocks left: " + blocksLeft.toString() + ", MemPool txs: " + mempool.toString();
    }

    if (parseInt(nodeCount) === 0 && walletState.toUpperCase() !== "NotStarted".toUpperCase()) {
        progressType = "info progress-bar-striped";
        text = "Connecting. . .";
    }

    if (torState.toUpperCase() === "CircuitEstabilished".toUpperCase()) {
        progress = 100;
    }

    if (torState.toUpperCase() === "EstabilishingCircuit".toUpperCase()) {
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

function updateDecryptButton(ts) {

    try {
        let decButton = document.getElementById("decrypt-wallet-button");

        if (ts.toUpperCase() === "CircuitEstabilished".toUpperCase()) {
            if (decButton.innerText === "Waiting for Tor...") {
                decButton.innerText = "Decrypt";
            }

            if (decButton.hasAttribute("disabled")) {
                decButton.removeAttribute("disabled");
            }
        }

        if (ts.toUpperCase() === "EstabilishingCircuit".toUpperCase()) {
            decButton.innerText = "Waiting for Tor...";
            decButton.disabled = true;
        }

        if (ts.toUpperCase() === "NotStarted".toUpperCase()) {
            decButton.innerText = "Waiting for Tor...";
            decButton.disabled = true;
        }
    }
    catch (err) {
        console.error(err);
    }
}

function getHeaderHeight() {
    connection.invoke('GetHeaderHeightAsync');
}
