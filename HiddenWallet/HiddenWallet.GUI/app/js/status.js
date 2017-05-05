function statusShow(progress, text, progressType = "") {
    if (progressType == "") {
        document.getElementById("status").innerHTML = '<div class="progress" style="margin:0"><div class="progress-bar" role="progressbar" style="width:' + progress + '%;"><span><strong>' + text + '</strong></span></div></div>';
    }
    else {
        document.getElementById("status").innerHTML = '<div class="progress" style="margin:0"><div class="progress-bar progress-bar-' + progressType + '" role="progressbar" style="width:' + progress + '%;"><span><strong>' + text + '</strong></span></div></div>';
    }
}

var changeBump = 0;
var walletState;
var headerHeight;
var trackingHeight;
var connectedNodeCount;
var memPoolTransactionCount;
function periodicUpdate() {
    setInterval(function statusUpdate() {
        var response = httpGetWallet("status");

        if (walletState === response.WalletState) {
            if (headerHeight === response.HeaderHeight) {
                if (trackingHeight === response.TrackingHeight) {
                    if (connectedNodeCount === response.ConnectedNodeCount) {
                        if (memPoolTransactionCount === response.MemPoolTransactionCount) {
                            if (changeBump === response.ChangeBump) {
                                return;
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

        var connectionText = "Connecting..."
        if (connectedNodeCount !== 0) {
            connectionText = "Connections: " + connectedNodeCount;
        }
        var blocksLeft = "-";
        if (trackingHeight !== 0) {
            blocksLeft = headerHeight - trackingHeight;
        }

        var text = "";
        var progressType = "";
        if (walletState.toUpperCase() === "NotStarted".toUpperCase()) {
            progressType = "info";
            text = "NotConnected";
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

        statusShow(100, text, progressType);

        if (response.ChangeBump !== changeBump) {
            updateWalletContent();
            changeBump = response.ChangeBump;
        }
    }, 1000);
}