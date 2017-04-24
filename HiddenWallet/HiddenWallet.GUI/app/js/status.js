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
function periodicStatusUpdate() {
    setInterval(function statusUpdate() {
        var response = httpGetWallet("status");
        if (walletState === response.WalletState) {
            if (headerHeight === response.HeaderHeight) {
                if (trackingHeight === response.TrackingHeight) {
                    return;
                }
            }
        }
        walletState = response.WalletState;
        headerHeight = response.HeaderHeight;
        trackingHeight = response.TrackingHeight;

        var text = "Status: " + walletState + ", Header Height: " + headerHeight + ", Tracking Height: " + trackingHeight;
        var zeroCheck = 1;
        if (trackingHeight !== 0) zeroCheck = trackingHeight;
        var progress = Math.floor((headerHeight / zeroCheck) * 100);

        var progressType = "";
        if (walletState.toUpperCase() === "NotStarted".toUpperCase()) {
            progressType = "warning";
        }
        if (walletState.toUpperCase() === "SyncingHeaders".toUpperCase()) {
            progressType = "info";
        }
        if (walletState.toUpperCase() === "SyncingBlocks".toUpperCase()) {
            progressType = "striped";
        }
        if (walletState.toUpperCase() === "SyncingMemPool".toUpperCase()) {
            progressType = ""; // this is the default
        }
        if (walletState.toUpperCase() === "Synced".toUpperCase()) {
            progressType = "success";
        }

        if (progress > 50) statusShow(progress, text, progressType);
        else statusShow(50, text, progressType);
    }, 1000);
}