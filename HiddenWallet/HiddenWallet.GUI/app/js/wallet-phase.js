function walletPhaseShow(tabItem = "") {
    document.getElementById("hide-btn").hidden = false;
    
    if (tabItem === "alice") {
        document.getElementById("tabs").innerHTML = document.getElementById("wallet-phase-tabs-frame").contentWindow.document.getElementById("alice-active").innerHTML;

        storage.set('lastAccount', { lastAccount: 'alice' }, function (error) {
            if (error) throw error;
        });
    }
    else if (tabItem === "bob") {
        document.getElementById("tabs").innerHTML = document.getElementById("wallet-phase-tabs-frame").contentWindow.document.getElementById("bob-active").innerHTML;
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
    document.getElementById("balances").innerHTML = '<h4><span class="label label-default" style="display:block;">Available: 3.123213123 BTC, Incoming: 2023.321323 BTC </span></h4>';
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
}