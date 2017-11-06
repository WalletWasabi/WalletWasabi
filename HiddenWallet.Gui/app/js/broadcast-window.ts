function closeWindow() {
    window.close();
}

let hex: any;

require('electron').ipcRenderer.on('broadcast-response', (event, response) => {
    hex = response.Hex;

    let fee: HTMLElement = document.getElementById("fee");
    let feePercent: HTMLElement = document.getElementById("fee-percent-of-sent");
    let spends: HTMLElement = document.getElementById("spends-unconfirmed");
    let transaction: HTMLElement = document.getElementById("transaction");

    fee.innerText = response.Fee;

    if (parseFloat(response.FeePercentOfSent) > 1) {
        feePercent.classList.add("label-danger");
        fee.classList.add("label-danger");
    }
    else {
        feePercent.classList.add("label-warning");
        fee.classList.add("label-warning");
    }

    feePercent.innerText = response.FeePercentOfSent;

    if (response.SpendsUnconfirmed == true) {
        spends.classList.add("label-danger");
    }
    else {
        spends.classList.add("label-warning");
    }

    spends.innerText = response.SpendsUnconfirmed;

    transaction.innerText =
        `${response.Transaction} 

HEX: ${hex}`;

});

interface BroadcastTransaction {
    Hex: any;
    QuickSend: boolean;
}

function broadcastTransaction() {
    let broadcastButton: HTMLElement = document.getElementById("broadcast-button");

    let containerElement: Element = document.getElementsByClassName("container").item(0);

    containerElement.setAttribute("style", "pointer-events:none;");

    broadcastButton.innerHTML = '<span class="glyphicon glyphicon-refresh spinning"></span> Broadcasting...';

    var obj: BroadcastTransaction = { Hex: hex, QuickSend: false };

    httpPostWalletAsync("send-transaction", obj, function (json) {
        let result: any = httpPostWallet("send-transaction", obj);

        if (result.Success) {
            alert("SUCCESS! Transaction is successfully broadcasted!");
        }
        else {
            let failText = "FAIL! " + result.Message;

            if (result.Details) {
                failText =
                    `${failText} 

Details: ${result.Details}`;

            }
            alert(failText);
        }
        window.close();
    });
}