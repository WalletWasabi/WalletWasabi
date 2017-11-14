function walletPhaseShow(tabItem: string = "") {

    let tabsFrame: HTMLIFrameElement = <HTMLIFrameElement>document.getElementById("wallet-phase-tabs-frame");

    let content: HTMLElement = document.getElementById("content");
    let hideButton: HTMLElement = document.getElementById("hide-btn");
    let alice: HTMLElement = tabsFrame.contentWindow.document.getElementById("alice-active");
    let bob: HTMLElement = tabsFrame.contentWindow.document.getElementById("bob-active");
    let mixer: HTMLElement = tabsFrame.contentWindow.document.getElementById("mixer-active");
    let tabs: HTMLElement = document.getElementById("tabs");
    let before: HTMLElement = document.getElementById("before-menu-br");
    let after: HTMLElement = document.getElementById("after-menu-hr");
    let menu: HTMLElement = document.getElementById("menu");
    let balances: HTMLElement = document.getElementById("balances");

    hideButton.hidden = false;

    if (tabItem === "alice") {
        content.setAttribute("style", "max-height: 320px");
        tabs.innerHTML = alice.outerHTML;

        storage.set('lastAccount', { lastAccount: 'alice' }, function (error) {
            if (error) throw error;
        });

        writeHint("Move coins between Alice and Bob only by mixing!");
        before.style.display = "none";
        after.style.display = "block";
        walletShow('receive');
    }
    else if (tabItem === "bob") {
        content.setAttribute("style", "max-height: 320px");
        tabs.innerHTML = bob.outerHTML;

        storage.set('lastAccount', { lastAccount: 'bob' }, function (error) {
            if (error) throw error;
        });

        writeHint("Move coins between Alice and Bob only by mixing!");
        before.style.display = "none";
        after.style.display = "block";
        walletShow('receive');
    }
    else if (tabItem === "mixer") {
        content.setAttribute("style", "max-height: 430");
        tabs.innerHTML = mixer.outerHTML;
        let hintTextArray = ["The coordinator of the mixing cannot steal your coins, nor deanonymize you", "ZeroLink's mixing technique is trustless", "There are no hidden fees. You only pay the Bitcoin network fees."];
        let randomHint = hintTextArray[Math.floor(Math.random() * hintTextArray.length)];
        writeHint(randomHint);
        menu.innerHTML = "";
        balances.innerHTML = "";
        before.style.display = "block";
        after.style.display = "none";
        mixerShow();
    }
    else if (tabItem === "") {
        storage.get('lastAccount', function (error, data) {
            if (error) {
                throw error;
            }

            walletPhaseShow(data.lastAccount);
            return;
        });
    }
    updateMixerTab(isTumblerOnline);
}

function walletShow(menuItem: string) {
    let walletMenuFrame: HTMLIFrameElement = <HTMLIFrameElement>document.getElementById("wallet-menu-frame");
    let walletContentFrame: HTMLIFrameElement = <HTMLIFrameElement>document.getElementById("wallet-content-frame");

    let menu: HTMLElement = document.getElementById("menu");
    let content: HTMLElement = document.getElementById("content");
    let sendActive: HTMLElement = walletMenuFrame.contentWindow.document.getElementById("send-active");
    let historyActive: HTMLElement = walletMenuFrame.contentWindow.document.getElementById("history-active");
    let receiveActive: HTMLElement = walletMenuFrame.contentWindow.document.getElementById("receive-active");
    let sendContent: HTMLElement = walletContentFrame.contentWindow.document.getElementById("send-content");

    if (menuItem === 'receive') {
        menu.innerHTML = receiveActive.outerHTML;
    }
    else if (menuItem === 'send') {
        writeHint('Would you consider feeding the developer with some pizza? 186n7me3QKajQZJnUsVsezVhVrSwyFCCZ');
        menu.innerHTML = sendActive.outerHTML;
        content.innerHTML = sendContent.outerHTML;
    }
    else if (menuItem === 'history') {
        menu.innerHTML = historyActive.outerHTML;
        writeHint('HiddenWallet? Easy Peasy Lemon Squeezey!');
    }

    updateWalletContent();
}

/* When the user clicks on the button, 
toggle between hiding and showing the dropdown content */
function chooseWalletDropdown(aliceBob: string = "") {
    let chooseWallet: HTMLElement = document.getElementById("choose-wallet-dropdown");
    let chooseWalletActive: HTMLElement = document.getElementById("choose-wallet-dropdown-active");
    let tumblingTo: HTMLElement = document.getElementById("tumbling-to-wallet");

    let resp: any;

    if (aliceBob === "") {
        chooseWallet.classList.toggle("show");
    }
    else if (aliceBob === "alice") {
        chooseWalletActive.innerHTML = "Alice";
        tumblingTo.innerHTML = "Bob";
        resp = httpGetWallet("balances/alice");
    }
    else if (aliceBob === "bob") {
        chooseWalletActive.innerHTML = "Bob";
        tumblingTo.innerHTML = "Alice";
        resp = httpGetWallet("balances/bob");
    }
    if (aliceBob === "bob" || aliceBob === "alice") {
        chooseWalletActive.classList.remove("label-danger");
        chooseWalletActive.classList.add("label-success");
        tumblingTo.classList.remove("label-danger");
        tumblingTo.classList.add("label-success");

        //todo - check they have enough funds to mix before submitting
    }
}

function mixerShow() {
    httpGetTumblerAsync("connection", function (json) { }); // makes sure the tumbler is initialized
    let walletContentFrame: HTMLIFrameElement = <HTMLIFrameElement>document.getElementById("wallet-content-frame");
    let mixerContentElem: HTMLElement = walletContentFrame.contentWindow.document.getElementById("mixer-content");

    let contentElem: HTMLElement = document.getElementById("content");
    contentElem.innerHTML = mixerContentElem.outerHTML;

    let mixerSettingsContentElem: HTMLElement = document.getElementById("mixer-settings-content");

    let tumblerStatusElem: HTMLElement = document.getElementById("tumbler-status");
    let tumblerStatusLabel: HTMLLabelElement = document.getElementById("tumbler-status-label") as HTMLLabelElement;
    let refreshButton: HTMLButtonElement = document.getElementById("refresh-tumbler-connection-button") as HTMLButtonElement;

    let mixerHrs: HTMLCollectionOf<Element> = document.getElementsByClassName("mixer-hr");

    if (isTumblerOnline === false) {
        tumblerStatusElem.classList.add("label-danger");
        tumblerStatusElem.style.display = "inline";
        tumblerStatusLabel.style.display = "inline";
        tumblerStatusElem.innerText = "The coordinator of the mixing is offline";
        mixerSettingsContentElem.style.display = "none";
        for (let i: number = 0; i < mixerHrs.length; i++) {
            let hr: HTMLElement = mixerHrs[i] as HTMLElement;
            hr.style.display = "none";
        }
        refreshButton.style.display = "inline";
    }
    else {
        tumblerStatusElem.classList.add("label-success");
        tumblerStatusElem.style.display = "none"
        tumblerStatusLabel.style.display = "none";
        mixerSettingsContentElem.style.display = "inline";
        for (let i: number = 0; i < mixerHrs.length; i++) {
            let hr: HTMLElement = mixerHrs[i] as HTMLElement;
            hr.style.display = "inline";
        }
        refreshButton.style.display = "none";
        updateMixerContent();
        
        let walletSelectedElem: HTMLElement = document.getElementById("wallet-selected");
        let amountElem: HTMLElement = document.getElementById("amount-input");

        amountElem.innerText = String(1); 
    }
}

function refreshTumblerConnection() {
    
    let containerElement: Element = document.getElementsByClassName("container").item(0);
    let refreshTumblerConnectionButton: HTMLElement = document.getElementById("refresh-tumbler-connection-button");

    containerElement.setAttribute("style", "pointer-events:none;");

    refreshTumblerConnectionButton.innerHTML = '<span class="glyphicon glyphicon-cog spinning"></span> Refreshing...';
    
    httpGetTumblerAsync("connection", function (json) {
        try {
            if (json.Success === false) {
                isTumblerOnline = false;
                mixerShow();
            }
            else {
                isTumblerOnline = true;
                mixerShow();
            }
        } catch (err) {

        }        

        refreshTumblerConnectionButton.innerHTML = '<span class="mdi mdi-tor"></span> Refresh';
        containerElement.setAttribute("style", "pointer-events:all;");
    });
}

interface TumbleRequest {
    From: string;
    To: string;
}

function mix() {

    let from: HTMLElement = document.getElementById("choose-wallet-dropdown-active");
    let to: HTMLElement = document.getElementById("tumbling-to-wallet");

    var obj: TumbleRequest = { From: from.innerText, To: to.innerText };

    let json: any;

    let mixingButton: HTMLButtonElement = document.getElementById("mixing-button") as HTMLButtonElement;

    httpPostWalletAsync("tumble", obj, function (json) {
        if (json.Success === false) {
            let alertMessage: string = `Couldn't start the mix: ${json.Message}`;

            if (!isBlank(json.Details)) {
                alertMessage = `${alertMessage}\n\nDetails:\n${json.Details}`;
            }

            alert(alertMessage);
        }
        else {
            mixingButton.disabled = true;
            alert("Mix submitted");
        }
    });
}

function updateWalletContent() {
    let walletContentFrame: HTMLIFrameElement = <HTMLIFrameElement>document.getElementById("wallet-content-frame");

    let tabs: HTMLElement = document.getElementById("tabs");
    let menu: HTMLElement = document.getElementById("menu");
    let content: HTMLElement = document.getElementById("content");
    let recContent: HTMLElement = walletContentFrame.contentWindow.document.getElementById("receive-content");
    let historyContent: HTMLElement = walletContentFrame.contentWindow.document.getElementById("history-content");
    let balances: HTMLElement = document.getElementById("balances");

    if (tabs.childElementCount > 0) {
        let bobOrAlice: string = tabs.firstElementChild.id;

        if (bobOrAlice == "alice-active") {
            let resp: any = httpGetWallet("balances/alice");

            let labelType: string = "default";

            if (resp.Incoming > 0) labelType = "danger";

            balances.innerHTML = `<h4><span class="label label-${labelType}" style="display:block;">Available: ${resp.Available} BTC, Incoming: ${resp.Incoming} BTC </span></h4>`;

            if (menu.childElementCount > 0) {
                let menuId: string = menu.firstElementChild.id;

                if (menuId === "receive-active") {
                    content.innerHTML = recContent.outerHTML;

                    let extPubKey: HTMLElement = document.getElementById("extpubkey");
                    let recAddresses: HTMLElement = document.getElementById("receive-addresses");

                    let resp: any = httpGetWallet("receive/alice");
                    let i: number = 0

                    for (i = 0; i < 6; i++) {
                        let node: HTMLElement = document.createElement("li");
                        node.setAttribute("class", "list-group-item");
                        let textNode: Text = document.createTextNode(resp.Addresses[i]);
                        node.appendChild(textNode);
                        recAddresses.appendChild(node);
                    }

                    extPubKey.innerText = resp.ExtPubKey;
                }
                else if (menuId === "history-active") {
                    content.innerHTML = historyContent.outerHTML;

                    let historyRecords: HTMLElement = document.getElementById("history-records");

                    let resp: any = httpGetWallet("history/alice");
                    let i: number = 0

                    for (i = 0; i < resp.History.length; i++) {
                        let trNode: HTMLElement = document.createElement("tr");
                        let tdNodeHeight: HTMLElement = document.createElement("td");
                        tdNodeHeight.innerText = resp.History[i].Height;
                        let tdNodeAmount: HTMLElement = document.createElement("td");
                        tdNodeAmount.innerText = resp.History[i].Amount;
                        let tdNodeTxId: HTMLElement = document.createElement("td");
                        tdNodeTxId.innerText = resp.History[i].TxId;
                        trNode.appendChild(tdNodeHeight);
                        trNode.appendChild(tdNodeAmount);
                        trNode.appendChild(tdNodeTxId);
                        historyRecords.appendChild(trNode);
                    }
                }
            }
        }
        else if (bobOrAlice == "bob-active") {
            let resp: any = httpGetWallet("balances/bob");
            let labelType: string = "default";

            if (resp.Incoming > 0) {
                labelType = "danger";
            }

            let balances: HTMLElement = document.getElementById("balances");

            balances.innerHTML = `<h4><span class="label label-${labelType}" style="display:block;">Available: ${resp.Available} BTC, Incoming: ${resp.Incoming} BTC </span></h4>`;

            let menu: HTMLElement = document.getElementById("menu");

            if (menu.childElementCount > 0) {
                let menuId: string = menu.firstElementChild.id;

                if (menuId === "receive-active") {
                    content.innerHTML = recContent.outerHTML;
                    let extPubKey: HTMLElement = document.getElementById("extpubkey");
                    let recAddresses: HTMLElement = document.getElementById("receive-addresses");

                    let resp: any = httpGetWallet("receive/bob");
                    let i: number = 0

                    for (i = 0; i < 6; i++) {
                        let node: HTMLElement = document.createElement("li");
                        node.setAttribute("class", "list-group-item");
                        let textNode: Text = document.createTextNode(resp.Addresses[i]);
                        node.appendChild(textNode);

                        recAddresses.appendChild(node);
                    }

                    extPubKey.innerText = resp.ExtPubKey;
                }
                else if (menuId === "history-active") {
                    content.innerHTML = historyContent.outerHTML;

                    let historyRecords: HTMLElement = document.getElementById("history-records");

                    let resp: any = httpGetWallet("history/bob");
                    let i: number = 0

                    for (i = 0; i < resp.History.length; i++) {
                        let trNode: HTMLElement = document.createElement("tr");
                        let tdNodeHeight: HTMLElement = document.createElement("td");
                        tdNodeHeight.innerText = resp.History[i].Height;
                        let tdNodeAmount: HTMLElement = document.createElement("td");
                        tdNodeAmount.innerText = resp.History[i].Amount;
                        let tdNodeTxId: HTMLElement = document.createElement("td");
                        tdNodeTxId.innerText = resp.History[i].TxId;
                        trNode.appendChild(tdNodeHeight);
                        trNode.appendChild(tdNodeAmount);
                        trNode.appendChild(tdNodeTxId);
                        historyRecords.appendChild(trNode);
                    }
                }
            }
        }
    }
}

function setAmountToAll() {
    let amount: HTMLInputElement = <HTMLInputElement>document.getElementById("amount-to-send");
    amount.value = "all";
}

interface Transaction {
    Password: string;
    Address: string;
    Amount: string;
    FeeType: string;
}

function buildTransaction() {
    var address: string = (<HTMLInputElement>document.getElementById("address-to-send")).value;
    var amount: string = (<HTMLInputElement>document.getElementById("amount-to-send")).value;
    var password: string = (<HTMLInputElement>document.getElementById("send-password")).value;
    var fastFeeChecked: boolean = (<HTMLInputElement>document.getElementById("fast-fee-radio")).checked;
    var slowFeeChecked: boolean = (<HTMLInputElement>document.getElementById("slow-fee-radio")).checked;

    if (!address) {
        alert("Couldn't build the tansaction: Wrong address!");
        return;
    }

    if (!amount || Number(amount) <= 0) {
        alert("Couldn't build the tansaction: Wrong amount!");
        return;
    }

    // if both are checked or none are checked (cannot happen)
    if ((fastFeeChecked && slowFeeChecked) || (!fastFeeChecked && !slowFeeChecked)) {
        alert("Couldn't build the tansaction: Wrong fee type!");
        return;
    }

    // (cannot happen)
    if (password == null) {
        alert("Couldn't build the tansaction: Wrong fee type!");
        return;
    }

    let feeType: string;

    if (fastFeeChecked) {
        feeType = "high";
    }

    if (slowFeeChecked) {
        feeType = "low";
    }

    var obj: Transaction = { Password: password, Address: address, Amount: amount, FeeType: feeType };

    let json: any;
    let tabs: HTMLElement = document.getElementById("tabs");
    let bobOrAlice: string;

    if (tabs.childElementCount > 0) {
        let bobOrAliceTab = tabs.firstElementChild.id;

        if (bobOrAliceTab == "alice-active") {
            bobOrAlice = "alice";
        }

        if (bobOrAliceTab == "bob-active") {
            bobOrAlice = "bob";
        }
    }
    else {
        alert("Alice or Bob is not chosen"); // this should never be happen
        return;
    }

    let containerElement: Element = document.getElementsByClassName("container").item(0);
    let buildTXButton: HTMLElement = document.getElementById("build-transaction-button");

    containerElement.setAttribute("style", "pointer-events:none;");

    buildTXButton.innerHTML = '<span class="glyphicon glyphicon-cog spinning"></span> Building...';

    httpPostWalletAsync(`build-transaction/${bobOrAlice}`, obj, function (json) {
        if (json.Success === false) {
            let alertMessage: string = `Couldn't build the tansaction: ${json.Message}`;

            if (!isBlank(json.Details)) {
                alertMessage = `${alertMessage}

                                Details:
                                ${json.Details}`;
            }

            alert(alertMessage);
        }
        else {
            const remote = require('electron').remote;
            const window = remote.getCurrentWindow();
            const BrowserWindow = remote.BrowserWindow;
            let broadcastWindow = new BrowserWindow({ width: 600, height: 400, frame: false, resizable: false, alwaysOnTop: true, parent: window, icon: __dirname + '/app/assets/TumbleBit.png' });
            broadcastWindow.show();
            broadcastWindow.focus();
            broadcastWindow.loadURL(`file://${__dirname}/app/html/broadcast-transaction-window.html`);

            broadcastWindow.webContents.on('did-finish-load', () => {
                broadcastWindow.webContents.send('broadcast-response', json);
            })
        }

        buildTXButton.innerHTML = '<span class="mdi mdi-tor"></span> Build Transaction';
        containerElement.setAttribute("style", "pointer-events:all;");
    });
}

function isBlank(str: string) {
    return (!str || /^\s*$/.test(str));
}

function getPhaseName(phase: string): string {
    let phaseName: string;

    switch (phase) {
        case "InputRegistration": {
            phaseName = "Input Registration";
            break;
        }
        case "ConnectionConfirmation": {
            phaseName = "Connection Confirmation";
            break;
        }
        case "OutputRegistration": {
            phaseName = "Output Registration";
            break;
        }
        case "Signing": {
            phaseName = "Signing";
            break;
        }
        default: {
            phaseName = "Unknown";
            break;
        }
    }

    return phaseName;
}