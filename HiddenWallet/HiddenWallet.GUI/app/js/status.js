function statusShow(progress, text, progressType = "") {
    if (progressType == "") {
        document.getElementById("status").innerHTML = '<div class="progress" style="margin:0"><div class="progress-bar" role="progressbar" style="width:' + progress + '%;"><span><strong>' + text + '</strong></span></div></div>';
    }
    else {
        document.getElementById("status").innerHTML = '<div class="progress" style="margin:0"><div class="progress-bar progress-bar-' + progressType + '" role="progressbar" style="width:' + progress + '%;"><span><strong>' + text + '</strong></span></div></div>';
    }
}

let blocksLeftToSync;
let changeBump = 0;
let walletState;
let headerHeight;
let trackingHeight;
let connectedNodeCount;
let memPoolTransactionCount;
let torState;
function periodicUpdate() {
    setInterval(function statusUpdate() {
        let response = httpGetWallet("status");

        if (walletState === response.WalletState) {
            if (headerHeight === response.HeaderHeight) {
                if (trackingHeight === response.TrackingHeight) {
                    if (connectedNodeCount === response.ConnectedNodeCount) {
                        if (memPoolTransactionCount === response.MemPoolTransactionCount) {
                            if (torState === response.TorState) {
                                if (changeBump === response.ChangeBump) {
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }
        
        walletState = response.WalletState;
        headerHeight = response.HeaderHeight;
        trackingHeight = response.TrackingHeight;
        connectedNodeCount = response.ConnectedNodeCount;
        memPoolTransactionCount = response.MemPoolTransactionCount;
        torState = response.TorState;

        let connectionText = "Connecting..."
        if (connectedNodeCount !== 0) {
            connectionText = "Connections: " + connectedNodeCount;
        }
        let blocksLeft = "-";
        if (trackingHeight !== 0) {
            blocksLeft = headerHeight - trackingHeight;
        }
        blocksLeftToSync = blocksLeft;

        let text = "";
        let progressType = "";
        if (walletState.toUpperCase() === "NotStarted".toUpperCase()) {
            progressType = "info";
            text = "Tor circuit estabilished, Wallet is offline";
        }
        if (walletState.toUpperCase() === "SyncingHeaders".toUpperCase()) {
            progressType = "info progress-bar-striped active";
            text = walletState + ", " + connectionText + ", Headers: " + headerHeight;
        }
        if (walletState.toUpperCase() === "SyncingBlocks".toUpperCase()) {
            progressType = "striped active";
            text = walletState + ", " + connectionText + ", Headers: " + headerHeight + ", Blocks left: " + blocksLeft;
        }
        if (walletState.toUpperCase() === "SyncingMemPool".toUpperCase()) {
            progressType = "success"; // this is the default
            text = connectionText + ", Headers: " + headerHeight + ", Blocks left: " + blocksLeft + ", MemPool txs: " + memPoolTransactionCount;
        }
        if (walletState.toUpperCase() === "Synced".toUpperCase()) {
            progressType = "success";
            text = walletState + ", " + connectionText + ", Headers: " + headerHeight + ", Blocks left: " + blocksLeft + ", MemPool txs: " + memPoolTransactionCount;
        }
        if (connectedNodeCount === 0 && walletState.toUpperCase() !== "NotStarted".toUpperCase()) {
            progressType = "info progress-bar-striped";
            text = "Connecting. . .";
        }

        if (torState.toUpperCase() === "CircuitEstabilished".toUpperCase()) {
            statusShow(100, text, progressType);
            if (document.getElementById("decrypt-wallet-button").innerText === "Waiting for Tor...") {
                document.getElementById("decrypt-wallet-button").innerText = "Decrypt";
            }
            if (document.getElementById("decrypt-wallet-button").hasAttribute("disabled")) {
                document.getElementById("decrypt-wallet-button").removeAttribute("disabled");                
            }
        }
        if (torState.toUpperCase() === "EstabilishingCircuit".toUpperCase()) {
            statusShow(100, "Estabilishing Tor circuit...", progressType);
            document.getElementById("decrypt-wallet-button").innerText = "Waiting for Tor...";
            document.getElementById("decrypt-wallet-button").setAttribute("disabled");
        } 
        if (torState.toUpperCase() === "NotStarted".toUpperCase()) {
            statusShow(100, "Tor is not running", "danger");
            document.getElementById("decrypt-wallet-button").innerText = "Waiting for Tor...";
            document.getElementById("decrypt-wallet-button").setAttribute("disabled");
        }
               

        if (response.ChangeBump !== changeBump) {
            updateWalletContent();
            changeBump = response.ChangeBump;
        }
    }, 1000);
}