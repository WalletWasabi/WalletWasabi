function walletPhaseShow(tabItem = "") {
    document.getElementById("hide-btn").hidden = false;
    
    if (tabItem === "alice") {
        document.getElementById("tabs").innerHTML = document.getElementById("wallet-phase-tabs-frame").contentWindow.document.getElementById("alice-active").outerHTML;

        storage.set('lastAccount', { lastAccount: 'alice' }, function (error) {
            if (error) throw error;
        });
    }
    else if (tabItem === "bob") {
        document.getElementById("tabs").innerHTML = document.getElementById("wallet-phase-tabs-frame").contentWindow.document.getElementById("bob-active").outerHTML;
        storage.set('lastAccount', { lastAccount: 'bob' }, function (error) {
            if (error) throw error;
        });
    }
    else if (tabItem === "") {
        storage.get('lastAccount', function (error, data) {
            if (error) throw error;

            walletPhaseShow(data.lastAccount);
            return;
        });
    }   

    writeHint("Move coins between Alice and Bob only by mixing!");

    walletShow('receive');

    document.getElementById("before-menu-br").outerHTML = "";
}

function walletShow(menuItem) {
    if (menuItem === 'receive') {
        document.getElementById("menu").innerHTML = document.getElementById("wallet-menu-frame").contentWindow.document.getElementById("receive-active").outerHTML;
    }
    else if (menuItem === 'send') {
        writeHint('Would you consider feeding the developer with some pizza? 186n7me3QKajQZJnUsVsezVhVrSwyFCCZ');
        document.getElementById("menu").innerHTML = document.getElementById("wallet-menu-frame").contentWindow.document.getElementById("send-active").outerHTML;
    }
    else if (menuItem === 'history') {
        document.getElementById("menu").innerHTML = document.getElementById("wallet-menu-frame").contentWindow.document.getElementById("history-active").outerHTML;
        writeHint('HiddenWallet? Easy Peasy Lemon Squeezey!');
    }

    updateWalletContent();
}

function updateWalletContent() {
    var tabs = document.getElementById("tabs");
    if (tabs.childElementCount > 0) {
        var bobOrAlice = tabs.firstElementChild.id;

        if (bobOrAlice == "alice-active") {
            var resp = httpGetWallet("balances/alice");
            var labelType = "default";
            if (resp.Incoming > 0) labelType = "danger";
            document.getElementById("balances").innerHTML = '<h4><span class="label label-' + labelType + '" style="display:block;">Available: ' + resp.Available + ' BTC, Incoming: ' + resp.Incoming + ' BTC </span></h4>';

            var menu = document.getElementById("menu");
            if (menu.childElementCount > 0) {
                var menuId = menu.firstElementChild.id;
                if (menuId === "receive-active") {
                    document.getElementById("content").innerHTML = document.getElementById("wallet-content-frame").contentWindow.document.getElementById("receive-content").outerHTML;
                    var resp = httpGetWallet("receive/alice");
                    for (i = 0; i < 7; i++) {
                        var node = document.createElement("li");
                        node.setAttribute("class", "list-group-item");
                        var textNode = document.createTextNode(resp.Addresses[i]);
                        node.appendChild(textNode);
                        document.getElementById("receive-addresses").appendChild(node);
                    }
                }
                else if (menuId === "send-active") {
                }
                else if (menuId === "history-active") {
                }
            }
        }
        else if (bobOrAlice == "bob-active") {
            var resp = httpGetWallet("balances/bob");
            document.getElementById("balances").innerHTML = '<h4><span class="label label-default" style="display:block;">Available: ' + resp.Available + ' BTC, Incoming: ' + resp.Incoming + ' BTC </span></h4>';

            var menu = document.getElementById("menu");
            if (menu.childElementCount > 0) {
                var menuId = menu.firstElementChild.id;
                if (menuId === "receive-active") {
                    document.getElementById("content").innerHTML = document.getElementById("wallet-content-frame").contentWindow.document.getElementById("receive-content").outerHTML;
                    var resp = httpGetWallet("receive/bob");
                    for (i = 0; i < 7; i++) {
                        var node = document.createElement("li");
                        node.setAttribute("class", "list-group-item");
                        var textNode = document.createTextNode(resp.Addresses[i]);
                        node.appendChild(textNode);
                        document.getElementById("receive-addresses").appendChild(node);
                    }
                }
                else if (menuId === "send-active") {
                }
                else if (menuId === "history-active") {
                }
            }
        }
    }
}