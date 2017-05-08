function closeWindow() {
    window.close();
}

require('electron').ipcRenderer.on('broadcast-response', (event, response) => {
    document.getElementById("fee").innerText = response.Fee;

    if (parseFloat(response.FeePercentOfSent) > 1) {
        document.getElementById("fee-percent-of-sent").classList.add("label-danger");
        document.getElementById("fee").classList.add("label-danger");
    }
    else {
        document.getElementById("fee-percent-of-sent").classList.add("label-warning");
        document.getElementById("fee").classList.add("label-warning");
    }
    document.getElementById("fee-percent-of-sent").innerText = response.FeePercentOfSent;

    if (response.SpendsUnconfirmed == true) {
        document.getElementById("spends-unconfirmed").classList.add("label-danger");
    }
    else {
        document.getElementById("spends-unconfirmed").classList.add("label-warning");
    }
    document.getElementById("spends-unconfirmed").innerText = response.SpendsUnconfirmed;
    document.getElementById("transaction").innerText = response.Transaction;

    hex = response.Hex;
});