let isTumblerOnline: boolean;

function periodicUpdate() {
    setInterval(function statusUpdate() {
        let response: any = httpGetWallet("status");

        if (isTumblerOnline !== response.IsTumblerOnline) {
            updateMixerTab(response.IsTumblerOnline);
            isTumblerOnline = response.IsTumblerOnline;
        }

        updateMixerContent(response);

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

let tumblerDenomination: number;
let tumblerAnonymitySet: string;
let tumblerNumberOfPeers: string;
let tumblerFeePerRound: number;
let tumblerWaitedInInputRegistration: string;
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
        timeSpentWaitingElem.innerText = tumblerWaitedInInputRegistration + " minutes";
        currentPhaseElem.innerText = tumblerPhase;
    }
    catch (err) {

    }
}