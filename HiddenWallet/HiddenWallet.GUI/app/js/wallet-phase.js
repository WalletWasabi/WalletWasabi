function walletPhaseShow(tabItem) {
    document.getElementById("hide-btn").hidden = false;

    if (tabItem === "alice") {
        document.getElementById("tabs").innerHTML = document.getElementById("wallet-phase-tabs-frame").contentWindow.document.getElementById("alice-active").innerHTML;
    }
    else if (tabItem === "bob") {
        document.getElementById("tabs").innerHTML = document.getElementById("wallet-phase-tabs-frame").contentWindow.document.getElementById("bob-active").innerHTML;
    }
    
    document.getElementById("menu").innerHTML = "menu not implemented";
    document.getElementById("content").innerHTML = "content not implemented";
}