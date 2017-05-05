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
    document.getElementById("content").innerHTML = "content not imlemented";
}

function walletShow(menuItem) {
    if (menuItem === 'receive') {
        document.getElementById("menu").innerHTML = document.getElementById("wallet-menu-frame").contentWindow.document.getElementById("receive-active").innerHTML;
    }
    else if (menuItem === 'send') {
        writeHint('Would you consider feeding the developer with some pizza? 186n7me3QKajQZJnUsVsezVhVrSwyFCCZ');
        document.getElementById("menu").innerHTML = document.getElementById("wallet-menu-frame").contentWindow.document.getElementById("send-active").innerHTML;
    }
    else if (menuItem === 'history') {
        document.getElementById("menu").innerHTML = document.getElementById("wallet-menu-frame").contentWindow.document.getElementById("history-active").innerHTML;
        writeHint('HiddenWallet? Easy Peasy Lemon Squeezey!');
    }

    updateWalletContent();
}

function updateWalletContent() {
    var bobOrAlice = document.getElementById("tabs").firstElementChild.id;
    if (bobOrAlice == "alice-active") {
        var obj = new Object();
        obj.account = "alice";
        var resp = httpGetWallet("balances/alice", obj);
        document.getElementById("balances").innerHTML = '<h4><span class="label label-default" style="display:block;">Available: ' + resp.Available + ' BTC, Incoming: ' + resp.Incoming +' BTC </span></h4>';
    }
    else if (bobOrAlice == "bob-active") {
        var obj = new Object();
        obj.account = "bob";
        var resp = httpGetWallet("balances/bob", obj);
        document.getElementById("balances").innerHTML = '<h4><span class="label label-default" style="display:block;">Available: ' + resp.Available + ' BTC, Incoming: ' + resp.Incoming + ' BTC </span></h4>';
    }
    
}