function closeWindow() {
    window.close();
}

let hex: any;

require('electron').ipcRenderer.on('broadcast-response', (event, response) => {
    hex = response.Hex;

    let fee: HTMLElement = document.getElementById("fee");
    let activeOutputAmount: HTMLElement = document.getElementById("active-output-amount");
    let changeOutputAmount: HTMLElement = document.getElementById("change-output-amount");
    let activeOutputAddress: HTMLElement = document.getElementById("active-output-address");
    let feePercent: HTMLElement = document.getElementById("fee-percent-of-sent");
    let spends: HTMLElement = document.getElementById("spends-unconfirmed");
    let transaction: HTMLElement = document.getElementById("transaction");
    let numberOfInputs: HTMLElement = document.getElementById("number-of-inputs");
    let coinsLabel: HTMLElement = document.getElementById("coins-label");

    fee.innerText = response.Fee;

    if (parseFloat(response.FeePercentOfSent) > 5) {
        feePercent.classList.remove("label-success");
        feePercent.classList.add("label-danger");
        fee.classList.remove("label-success");
        fee.classList.add("label-danger");
    }
    else if (parseFloat(response.FeePercentOfSent) > 1) {
        feePercent.classList.remove("label-success");
        feePercent.classList.add("label-warning");
        fee.classList.remove("label-success");
        fee.classList.add("label-warning");
    }

    feePercent.innerText = response.FeePercentOfSent;

    if (response.SpendsUnconfirmed == true) {
        spends.classList.remove("label-success");
        spends.classList.add("label-danger");
    }

    if (response.NumberOfInputs > 1) {
        coinsLabel.innerText = "coins";
        numberOfInputs.classList.remove("label-success");
        numberOfInputs.classList.add("label-danger");
    }
    else {
        coinsLabel.innerText = "coin";
    }
    numberOfInputs.innerText = response.NumberOfInputs;

    spends.innerText = response.SpendsUnconfirmed;

    activeOutputAmount.innerText = response.ActiveOutputAmount;
    activeOutputAddress.innerText = response.ActiveOutputAddress;

    if (response.ChangeOutputAmount != 0)
    {
        changeOutputAmount.classList.remove("label-success");
        changeOutputAmount.classList.add("label-warning");
    }
    changeOutputAmount.innerText = response.ChangeOutputAmount;

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
            window.close();
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
    });
}