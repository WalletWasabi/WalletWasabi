function walletPhaseShow(tabItem = "") {
    document.getElementById("content").setAttribute("style", "max-height: 320px");
    document.getElementById("hide-btn").hidden = false;
    
    if (tabItem === "alice") {
        document.getElementById("tabs").innerHTML = document.getElementById("wallet-phase-tabs-frame").contentWindow.document.getElementById("alice-active").outerHTML;

        storage.set('lastAccount', { lastAccount: 'alice' }, function (error) {
            if (error) throw error;
        });
        writeHint("Move coins between Alice and Bob only by mixing!");
        document.getElementById("before-menu-br").style.display = "none";
        document.getElementById("after-menu-hr").style.display = "block";
        walletShow('receive');
    }
    else if (tabItem === "bob") {
        document.getElementById("tabs").innerHTML = document.getElementById("wallet-phase-tabs-frame").contentWindow.document.getElementById("bob-active").outerHTML;
        storage.set('lastAccount', { lastAccount: 'bob' }, function (error) {
            if (error) throw error;
        });
        writeHint("Move coins between Alice and Bob only by mixing!");
        document.getElementById("before-menu-br").style.display = "none";
        document.getElementById("after-menu-hr").style.display = "block";
        walletShow('receive');
    }
    else if (tabItem === "mixer") {
        document.getElementById("tabs").innerHTML = document.getElementById("wallet-phase-tabs-frame").contentWindow.document.getElementById("mixer-active").outerHTML;
        writeHint("TumbleBit cannot steal your coins, nor deanonymize you");
        document.getElementById("menu").innerHTML = "";
        document.getElementById("balances").innerHTML = "";
        document.getElementById("before-menu-br").style.display = "block";
        document.getElementById("after-menu-hr").style.display = "none";
        mixerShow();
    }
    else if (tabItem === "") {
        storage.get('lastAccount', function (error, data) {
            if (error) throw error;

            walletPhaseShow(data.lastAccount);
            return;
        });
    }    
}

function walletShow(menuItem) {
    if (menuItem === 'receive') {
        document.getElementById("menu").innerHTML = document.getElementById("wallet-menu-frame").contentWindow.document.getElementById("receive-active").outerHTML;
    }
    else if (menuItem === 'send') {
        writeHint('Would you consider feeding the developer with some pizza? 186n7me3QKajQZJnUsVsezVhVrSwyFCCZ');
        document.getElementById("menu").innerHTML = document.getElementById("wallet-menu-frame").contentWindow.document.getElementById("send-active").outerHTML;
        document.getElementById("content").innerHTML = document.getElementById("wallet-content-frame").contentWindow.document.getElementById("send-content").outerHTML;
    }
    else if (menuItem === 'history') {
        document.getElementById("menu").innerHTML = document.getElementById("wallet-menu-frame").contentWindow.document.getElementById("history-active").outerHTML;
        writeHint('HiddenWallet? Easy Peasy Lemon Squeezey!');
    }

    updateWalletContent();
}

function mixerShow() {
    document.getElementById("content").innerHTML = document.getElementById("wallet-content-frame").contentWindow.document.getElementById("mixer-content").outerHTML;

    document.getElementById("tumbler-address").innerText = "http://testnet.ntumblebit.metaco.com/api/v1/tumblers/7c762c3dc672d440a9a0ed3082a2fc5fbfcc51db";
    document.getElementById("tumbler-status").innerText = "Online";
    document.getElementById("tumbler-status").classList.add("label-success");
    document.getElementById("tumbler-denomination").innerText = "0.7";
    document.getElementById("tumbler-denomination").classList.add("label-warning");
    document.getElementById("tumbler-fee").innerText = "1.01";
    document.getElementById("tumbler-fee").classList.add("label-warning");
    document.getElementById("tumbling-time").innerText = "3";
    document.getElementById("tumbling-time").classList.add("label-warning");
    document.getElementById("tumbling-network-fees").innerText = "0.001";
    document.getElementById("tumbling-network-fees").classList.add("label-warning");
}

function updateWalletContent() {
    let tabs = document.getElementById("tabs");
    if (tabs.childElementCount > 0) {
        let bobOrAlice = tabs.firstElementChild.id;

        if (bobOrAlice == "alice-active") {
            let resp = httpGetWallet("balances/alice");
            let labelType = "default";
            if (resp.Incoming > 0) labelType = "danger";
            document.getElementById("balances").innerHTML = '<h4><span class="label label-' + labelType + '" style="display:block;">Available: ' + resp.Available + ' BTC, Incoming: ' + resp.Incoming + ' BTC </span></h4>';

            let menu = document.getElementById("menu");
            if (menu.childElementCount > 0) {
                let menuId = menu.firstElementChild.id;
                if (menuId === "receive-active") {
                    document.getElementById("content").innerHTML = document.getElementById("wallet-content-frame").contentWindow.document.getElementById("receive-content").outerHTML;
                    let resp = httpGetWallet("receive/alice");
                    for (i = 0; i < 6; i++) {
                        let node = document.createElement("li");
                        node.setAttribute("class", "list-group-item");
                        let textNode = document.createTextNode(resp.Addresses[i]);
                        node.appendChild(textNode);
                        document.getElementById("receive-addresses").appendChild(node);
                    }
                    document.getElementById("extpubkey").innerText = resp.ExtPubKey;
                }
                else if (menuId === "history-active") {
                    document.getElementById("content").innerHTML = document.getElementById("wallet-content-frame").contentWindow.document.getElementById("history-content").outerHTML;
                    let resp = httpGetWallet("history/alice");
                    for (i = 0; i < resp.History.length; i++) {
                        let trNode = document.createElement("tr");
                        let tdNodeHeight = document.createElement("td");
                        tdNodeHeight.innerText = resp.History[i].Height;
                        let tdNodeAmount = document.createElement("td");
                        tdNodeAmount.innerText = resp.History[i].Amount;
                        let tdNodeTxId = document.createElement("td");
                        tdNodeTxId.innerText = resp.History[i].TxId;
                        trNode.appendChild(tdNodeHeight);
                        trNode.appendChild(tdNodeAmount);
                        trNode.appendChild(tdNodeTxId);
                        document.getElementById("history-records").appendChild(trNode);
                    }
                }
            }
        }
        else if (bobOrAlice == "bob-active") {
            let resp = httpGetWallet("balances/bob");
            let labelType = "default";
            if (resp.Incoming > 0) labelType = "danger";
            document.getElementById("balances").innerHTML = '<h4><span class="label label-' + labelType + '" style="display:block;">Available: ' + resp.Available + ' BTC, Incoming: ' + resp.Incoming + ' BTC </span></h4>';

            let menu = document.getElementById("menu");
            if (menu.childElementCount > 0) {
                let menuId = menu.firstElementChild.id;
                if (menuId === "receive-active") {
                    document.getElementById("content").innerHTML = document.getElementById("wallet-content-frame").contentWindow.document.getElementById("receive-content").outerHTML;
                    let resp = httpGetWallet("receive/bob");
                    for (i = 0; i < 6; i++) {
                        let node = document.createElement("li");
                        node.setAttribute("class", "list-group-item");
                        let textNode = document.createTextNode(resp.Addresses[i]);
                        node.appendChild(textNode);
                        document.getElementById("receive-addresses").appendChild(node);
                    }
                    document.getElementById("extpubkey").innerText = resp.ExtPubKey;
                }
                else if (menuId === "history-active") {
                    document.getElementById("content").innerHTML = document.getElementById("wallet-content-frame").contentWindow.document.getElementById("history-content").outerHTML;
                    let resp = httpGetWallet("history/bob");
                    for (i = 0; i < resp.History.length; i++) {
                        let trNode = document.createElement("tr");
                        let tdNodeHeight = document.createElement("td");
                        tdNodeHeight.innerText = resp.History[i].Height;
                        let tdNodeAmount = document.createElement("td");
                        tdNodeAmount.innerText = resp.History[i].Amount;
                        let tdNodeTxId = document.createElement("td");
                        tdNodeTxId.innerText = resp.History[i].TxId;
                        trNode.appendChild(tdNodeHeight);
                        trNode.appendChild(tdNodeAmount);
                        trNode.appendChild(tdNodeTxId);
                        document.getElementById("history-records").appendChild(trNode);
                    }
                }
            }
        }
    }
}

function setAmountToAll() {
    document.getElementById("amount-to-send").value = "all";
}

function buildTransaction() {
    var address = document.getElementById("address-to-send").value;
    var amount = document.getElementById("amount-to-send").value;
    var fastFeeChecked = document.getElementById("fast-fee-radio").checked;
    var slowFeeChecked = document.getElementById("slow-fee-radio").checked;
    var password = document.getElementById("send-password").value;

    if (!address) {
        alert("Couldn't build the transaciton: Wrong address!");
        return;
    }
    if (!amount || amount <= 0) {
        alert("Couldn't build the transaciton: Wrong amount!");
        return;
    }
    // if both are checked or none are checked (cannot happen)
    if ((fastFeeChecked && slowFeeChecked) || (!fastFeeChecked && !slowFeeChecked)) {
        alert("Couldn't build the transaciton: Wrong fee tpye!");
        return;
    }
    // (cannot happen)
    if (password == null) {
        alert("Couldn't build the transaciton: Wrong fee tpye!");
        return;
    }

    let feeType;
    if (fastFeeChecked) feeType = "high";
    if (slowFeeChecked) feeType = "low";

    let obj = new Object();
    obj.password = password;
    obj.address = address;
    obj.amount = amount;
    obj.feeType = feeType;

    let json;
    let tabs = document.getElementById("tabs");
    let bobOrAlice;
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

    document.getElementsByClassName("container").item(0).setAttribute("style", "pointer-events:none;");
    document.getElementById("build-transaction-button").innerHTML = '<span class="glyphicon glyphicon-cog spinning"></span> Building...';
    httpPostWalletAsync("build-transaction/" + bobOrAlice, obj, function (json) {
        if (json.Success === false) {
            let alertMessage = "Couldn't build the transaciton: " + json.Message;
            if (!isBlank(json.Details)) {
                alertMessage = alertMessage + "\n\nDetails:\n" + json.Details;
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
            broadcastWindow.loadURL('file://' + __dirname + '/app/html/broadcast-transaction-window.html');
            broadcastWindow.webContents.on('did-finish-load', () => {
                broadcastWindow.webContents.send('broadcast-response', json);
            })
        }
        document.getElementById("build-transaction-button").innerHTML = '<span class="mdi mdi-tor"></span> Build Transaction';
        document.getElementsByClassName("container").item(0).setAttribute("style", "pointer-events:all;");
    });    
}

function isBlank(str) {
    return (!str || /^\s*$/.test(str));
}