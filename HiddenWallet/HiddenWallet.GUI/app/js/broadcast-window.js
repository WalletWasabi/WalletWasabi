function closeWindow() {
    window.close();
}

let hex;
require('electron').ipcRenderer.on('broadcast-response', (event, response) => {
    hex = response.Hex;

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
    document.getElementById("transaction").innerText = response.Transaction + "\n HEX: " + hex;
});

function broadcastTransaction() {
    document.getElementsByClassName("container").item(0).setAttribute("style", "pointer-events:none;");
    document.getElementById("broadcast-button").innerHTML = '<span class="glyphicon glyphicon-refresh spinning"></span> Broadcasting...';

    let obj = new Object();
    obj.hex = hex;
    obj.quickSend = false;
    httpPostWalletAsync("send-transaction", obj, function (json) {
        let result = httpPostWallet("send-transaction", obj);
        if (result.Success) {
            alert("SUCCESS! Transaction is successfully broadcasted!");
        }
        else {
            let failText = "FAIL! " + result.Message;
            if (result.Details) {
                failText = failText + "\n\nDetails:\n" + result.Details;
            }
            alert(failText);
        }
        window.close();
    });    
}