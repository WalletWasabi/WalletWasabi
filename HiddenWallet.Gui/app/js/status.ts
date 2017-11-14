function statusShow(progress: number, text: string, progressType: string = "") {
    var status: HTMLElement = document.getElementById("status");

    if (progressType === "") {
        status.innerHTML = `<div class="progress" style="margin:0"><div class="progress-bar" role="progressbar" style="width:${progress}%;"><span><strong>${text}</strong></span></div></div>`;
    }
    else {
        status.innerHTML = `<div class="progress" style="margin:0"><div class="progress-bar progress-bar-${progressType}" role="progressbar" style="width:${progress}%;"><span><strong>${text}</strong></span></div></div>`;
    }
}

let blocksLeftToSync: string;
let changeBump: number = 0;
let walletState: string;
let headerHeight: number;
let trackingHeight: number;
let connectedNodeCount: number;
let memPoolTransactionCount: number;
let torState: string;
let isTumblerOnline: boolean;

function periodicUpdate() {
    setInterval(function statusUpdate() {
        let response: any = httpGetWallet("status");

        updateDecryptButton(response.TorState);

        if (isTumblerOnline !== response.IsTumblerOnline) {
            updateMixerTab(response.IsTumblerOnline);
            isTumblerOnline = response.IsTumblerOnline;
        }

        updateMixerContent(response);

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

        let connectionText: string = "Connecting..."

        if (connectedNodeCount !== 0) {
            connectionText = `Connections: ${connectedNodeCount}`;
        }

        let blocksLeft: string = "-";

        if (trackingHeight !== 0) {
            blocksLeft = (headerHeight - trackingHeight).toString();
        }

        blocksLeftToSync = blocksLeft;

        let text: string = "";
        let progressType: string = "";

        if (walletState.toUpperCase() === "NotStarted".toUpperCase()) {
            progressType = "info";
            text = "Tor circuit established, Wallet is offline";
        }

        if (walletState.toUpperCase() === "SyncingHeaders".toUpperCase()) {
            progressType = "info progress-bar-striped active";
            text = `${walletState}, ${connectionText}, Headers: ${headerHeight}`;
        }

        if (walletState.toUpperCase() === "SyncingBlocks".toUpperCase()) {
            progressType = "striped active";
            text = `${walletState}, ${connectionText}, Headers: ${headerHeight}, Blocks left: ${blocksLeft}`;
        }

        if (walletState.toUpperCase() === "SyncingMemPool".toUpperCase()) {
            progressType = "success"; // this is the default
            text = `${connectionText}, Headers: ${headerHeight}, Blocks left: ${blocksLeft}, MemPool txs: ${memPoolTransactionCount}`;
        }

        if (walletState.toUpperCase() === "Synced".toUpperCase()) {
            progressType = "success";
            text = `${walletState}, ${connectionText}, Headers: ${headerHeight}, Blocks left: ${blocksLeft}, MemPool txs: ${memPoolTransactionCount}`;
        }

        if (connectedNodeCount === 0 && walletState.toUpperCase() !== "NotStarted".toUpperCase()) {
            progressType = "info progress-bar-striped";
            text = "Connecting. . .";
        }

        if (torState.toUpperCase() === "CircuitEstabilished".toUpperCase()) {
            statusShow(100, text, progressType);
        }

        if (torState.toUpperCase() === "EstabilishingCircuit".toUpperCase()) {
            statusShow(100, "Establishing Tor circuit...", progressType);
        }

        if (torState.toUpperCase() === "NotStarted".toUpperCase()) {
            statusShow(100, "Tor is not running", "danger");
        }

        if (response.ChangeBump !== changeBump) {
            updateWalletContent();
            changeBump = response.ChangeBump;
        }
    }, 1000);
}

function updateMixerTab(ito: boolean) {
    try {

        let mixerTabs: HTMLCollectionOf<Element> = document.getElementsByClassName("mixer-tab-link");
        for (let i: number = 0; i < mixerTabs.length; i++) {
            let tab: HTMLElement = mixerTabs[i] as HTMLElement;
            if (ito === false) {
                tab.style.backgroundColor = "blanchedalmond";
            }
            else {
                tab.style.backgroundColor = "";
            }
        }
    }
    catch (err) {

    }
}

let tumblerDenomination: string;
let tumblerAnonymitySet: string;
let tumblerNumberOfPeers: string;
let tumblerFeePerRound: string;
let tumblerWaitedInInputRegistration: number;
let tumblerPhase: string;
function updateMixerContent(response = null) {
    try {
        if (response != null) {
            // If nothing changed return
            if (tumblerDenomination === response.TumblerDenomination) {
                if (tumblerAnonymitySet === response.TumblerAnonymitySet) {
                    if (tumblerNumberOfPeers === response.TumblerNumberOfPeers) {
                        if (tumblerFeePerRound === response.TumblerFeePerRound) {
                            if (tumblerWaitedInInputRegistration === response.TumblerWaitedInInputRegistration) {
                                if (tumblerPhase === response.TumblerPhase) {
                                    return;
                                }
                            }
                        }
                    }
                }
            }

            tumblerDenomination = response.TumblerDenomination;
            tumblerAnonymitySet = response.TumblerAnonymitySet;
            tumblerNumberOfPeers = response.TumblerNumberOfPeers;
            tumblerFeePerRound = response.TumblerFeePerRound;
            tumblerWaitedInInputRegistration = response.TumblerWaitedInInputRegistration;
            tumblerPhase = response.TumblerPhase;
        }

        let denominationElem: HTMLElement = document.getElementById("tumbler-denomination");
        let anonymitySetElem: HTMLElement = document.getElementById("tumbler-anonymity-set");
        let peerCountElem: HTMLElement = document.getElementById("tumbler-peer-count");
        let tumblerFeePerRoundElem: HTMLElement = document.getElementById("tumbler-fee-per-round");
        let timeSpentWaitingElem: HTMLElement = document.getElementById("tumbler-time-spent-waiting");
        let currentPhaseElem: HTMLElement = document.getElementById("tumbler-current-phase");
        
        denominationElem.innerText = tumblerDenomination + " BTC";
        anonymitySetElem.innerText = tumblerAnonymitySet;
        peerCountElem.innerText = tumblerNumberOfPeers;
        tumblerFeePerRoundElem.innerText = tumblerFeePerRound + " BTC";
        timeSpentWaitingElem.innerText = String(Math.floor(tumblerWaitedInInputRegistration / 60)) + " minutes";
        currentPhaseElem.innerText = tumblerPhase;
    }
    catch (err) {

    }
}

function updateDecryptButton(ts: string) {
    try {
        let decButton: HTMLButtonElement = document.getElementById("decrypt-wallet-button") as HTMLButtonElement;

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

    }
}