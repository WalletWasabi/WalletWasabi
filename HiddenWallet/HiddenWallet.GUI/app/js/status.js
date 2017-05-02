function statusShow(progress, text, progressType = "") {
    if (progressType == "") {
        document.getElementById("status").innerHTML = '<div class="progress" style="margin:0"><div class="progress-bar" role="progressbar" style="width:' + progress + '%;"><span><strong>' + text + '</strong></span></div></div>';
    }
    else {
        document.getElementById("status").innerHTML = '<div class="progress" style="margin:0"><div class="progress-bar progress-bar-' + progressType + '" role="progressbar" style="width:' + progress + '%;"><span><strong>' + text + '</strong></span></div></div>';
    }
}

var walletState;
var headerHeight;
var trackingHeight;
var connectedNodeCount;
var memPoolTransactionCount;
function periodicStatusUpdate() {
    setInterval(function statusUpdate() {
        var response = httpGetWallet("status");

        if (walletState === response.WalletState) {
            if (headerHeight === response.HeaderHeight) {
                if (trackingHeight === response.TrackingHeight) {
                    if (connectedNodeCount === response.ConnectedNodeCount) {
                        if (memPoolTransactionCount === response.MemPoolTransactionCount) {
                            return;
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
            var difference = headerHeight - trackingHeight;
            if (difference >= 0) {
                blocksLeft = difference;
            }
        }

        var zeroHeaderCheck = 1;
        if (headerHeight !== 0) zeroHeaderCheck = headerHeight;
        var zeroTrackingCheck = 1;
        if (trackingHeight !== 0) zeroTrackingCheck = trackingHeight;
        var progress = Math.floor((zeroTrackingCheck / zeroHeaderCheck) * 100);

        var text = "";
        var progressType = "";
        if (walletState.toUpperCase() === "NotStarted".toUpperCase()) {
            progressType = "warning";
            text = "NotConnected";
        }
        if (walletState.toUpperCase() === "SyncingHeaders".toUpperCase()) {
            progressType = "info";
            text = walletState + ", " + connectionText + ", Headers: " + headerHeight;
        }
        if (walletState.toUpperCase() === "SyncingBlocks".toUpperCase()) {
            progressType = "striped";
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
            text = "Connecting. . .";
        }

        if (progress < 50) statusShow(50, text, progressType); //mincheck
        else if (progress > 100) statusShow(100, text, progressType); //maxcheck
        else statusShow(progress, text, progressType);
    }, 1000);
}