let hex: string;
let showTransactionText: any;

function setPrivacyImprovementVisibility(numberOfInputs, changeOutputAmount)
{
    let broadcast: HTMLButtonElement = document.getElementById("broadcast-button") as HTMLButtonElement;
    broadcast.style.removeProperty("display");
    broadcast.classList.remove("btn-danger");
    broadcast.classList.remove("btn-warning");
    broadcast.classList.remove("btn-success");
    let rebuild: HTMLButtonElement = document.getElementById("rebuild-button") as HTMLButtonElement;
    rebuild.style.display = "none";

    let privacyImprovementArea: HTMLElement = document.getElementById("privacy-improvement-area");
    privacyImprovementArea.hidden = true;
    let multipleCoinsArea: HTMLElement = document.getElementById("multiple-coins-content");
    multipleCoinsArea.hidden = true;
    let changeArea: HTMLElement = document.getElementById("change-content");
    changeArea.hidden = true;
    if (Number(numberOfInputs) > 1) {
        multipleCoinsArea.hidden = false;
        privacyImprovementArea.hidden = false;
    }
    if (Number(changeOutputAmount) > 0) {
        changeArea.hidden = false;
        privacyImprovementArea.hidden = false;
    }

    if (Number(numberOfInputs) > 1) {
        broadcast.classList.add("btn-danger");
    }
    else if (Number(changeOutputAmount) > 0) {
        broadcast.classList.add("btn-warning");
    }
    else {
        broadcast.classList.add("btn-success");
    }
}
let noi;
let coa;

let previousBobOrAlice;
let previousRequest;

let allInputCoins;

function reloadPage(response, bobOrAlice, request)
{
    previousBobOrAlice = bobOrAlice;
    previousRequest = request;

    setPrivacyImprovementVisibility(response.NumberOfInputs, response.ChangeOutputAmount);

    noi = response.NumberOfInputs;
    coa = response.ChangeOutputAmount;

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

    feePercent.classList.remove("label-danger");
    feePercent.classList.remove("label-warning");
    feePercent.classList.add("label-success");
    fee.classList.remove("label-danger");
    fee.classList.remove("label-warning");
    fee.classList.add("label-success");
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
    
    spends.classList.remove("label-danger");
    spends.classList.remove("label-warning");
    spends.classList.add("label-success");
    if (response.SpendsUnconfirmed == true) {
        spends.classList.remove("label-success");
        spends.classList.add("label-danger");
    }

    numberOfInputs.classList.remove("label-danger");
    numberOfInputs.classList.remove("label-warning");
    numberOfInputs.classList.add("label-success");
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

    changeOutputAmount.classList.remove("label-danger");
    changeOutputAmount.classList.remove("label-warning");
    changeOutputAmount.classList.add("label-success");
    if (response.ChangeOutputAmount != 0) {
        changeOutputAmount.classList.remove("label-success");
        changeOutputAmount.classList.add("label-warning");
    }
    changeOutputAmount.innerText = response.ChangeOutputAmount;

    showTransactionText = `${response.Transaction}`;

    let spendWholeCoin: HTMLElement = document.getElementById("spend-whole-coin-radio-span");
    let donateChange: HTMLElement = document.getElementById("donate-change-radio-span");
    let coinSelectionCheckboxes: HTMLElement = document.getElementById("coin-selection-checkboxes");

    spendWholeCoin.innerHTML = spendWholeCoin.innerHTML = "Spend the whole coin: " + (parseFloat(response.ActiveOutputAmount) + parseFloat(response.ChangeOutputAmount)).toFixed(8) + " BTC  to " + response.ActiveOutputAddress;
    donateChange.innerHTML = donateChange.innerHTML = "Donate the change to the development of HiddenWallet: " + response.ChangeOutputAmount + " BTC";

    coinSelectionCheckboxes.innerHTML = "";
    allInputCoins = response.Inputs;
    for (let i = 0; i < response.NumberOfInputs; i++) {
        let input = response.Inputs[i];
        coinSelectionCheckboxes.innerHTML = coinSelectionCheckboxes.innerHTML += '<input onchange="actOnCoinSelectionChanged()" type="checkbox" name= "coin" value= "' + input.Index + ':' + input.Hash + '" checked>' + input.Amount + ' BTC at ' + input.Index + ':' + input.Hash + '<br>';
    }
}

require('electron').ipcRenderer.on('broadcast-response', (event, response, bobOrAlice, request) => {
    reloadPage(response, bobOrAlice, request);
});

function actOnCoinSelectionChanged()
{
    let keepChange: HTMLInputElement = document.getElementById("keep-change-radio") as HTMLInputElement;
    let spendWholeCoin: HTMLInputElement = document.getElementById("spend-whole-coin-radio") as HTMLInputElement;
    let donateChange: HTMLInputElement = document.getElementById("donate-change-radio") as HTMLInputElement;
    let rebuild: HTMLButtonElement = document.getElementById("rebuild-button") as HTMLButtonElement;
    let broadcast: HTMLButtonElement = document.getElementById("broadcast-button") as HTMLButtonElement;

    keepChange.checked = true;
    spendWholeCoin.checked = false;
    donateChange.checked = false;
    rebuild.style.display = "none";
    broadcast.style.removeProperty("display");

    let coins = document.getElementsByName("coin");
    let allSelected = true;
    let selectedNum = 0;
    for (let i = 0; i < coins.length; i++) {
        let coin: HTMLInputElement = coins[i] as HTMLInputElement;
        if (coin.checked)
        {
            selectedNum++;
        }
        else {
            allSelected = false;
        }
    }
    if (selectedNum == 1)
    {
        for (let i = 0; i < coins.length; i++) {
            let coin: HTMLInputElement = coins[i] as HTMLInputElement;
            if (coin.checked)
            {
                coin.disabled = true;
            }
        }
    }
    else {
        for (let i = 0; i < coins.length; i++) {
            let coin: HTMLInputElement = coins[i] as HTMLInputElement;
            coin.disabled = false;
        }
    }

    if (allSelected)
    {
        setPrivacyImprovementVisibility(noi, coa);
    }
    else
    {
        rebuild.style.removeProperty("display");
        broadcast.style.display = "none";
        let changeArea: HTMLElement = document.getElementById("change-content");
        changeArea.hidden = true;
    }
}

function actOnChangeRadio()
{
    let keepChange: HTMLInputElement = document.getElementById("keep-change-radio") as HTMLInputElement;
    let spendWholeCoin: HTMLInputElement = document.getElementById("spend-whole-coin-radio") as HTMLInputElement;
    let donateChange: HTMLInputElement = document.getElementById("donate-change-radio") as HTMLInputElement;
    let rebuild: HTMLButtonElement = document.getElementById("rebuild-button") as HTMLButtonElement;
    let broadcast: HTMLButtonElement = document.getElementById("broadcast-button") as HTMLButtonElement;

    if (keepChange.checked)
    {
        rebuild.style.display = "none";
        broadcast.style.removeProperty("display");
    }
    else if (spendWholeCoin.checked)
    {
        rebuild.style.removeProperty("display");
        broadcast.style.display = "none";
    }
    else if (donateChange.checked)
    {
        rebuild.style.removeProperty("display");
        broadcast.style.display = "none";
    }
}

function showTransaction()
{
    alert(showTransactionText);
}


function copyHex() {
    let textArea: HTMLTextAreaElement = document.createElement("textarea");

    textArea.style.position = 'fixed';
    textArea.style.width = '2em';
    textArea.style.height = '2em';
    textArea.style.border = 'none';
    textArea.style.outline = 'none';
    textArea.style.boxShadow = 'none';
    textArea.style.background = 'transparent';
    textArea.value = hex;
    document.body.appendChild(textArea);
    textArea.select();
    try {
        let successful = document.execCommand('copy');
        let msg = successful ? 'successful' : 'unsuccessful';
        console.log('Copying text command was ' + msg);
    } catch (err) {
        console.log('Oops, unable to copy');
    }
    document.body.removeChild(textArea);
}

interface BroadcastTransaction {
    Hex: any;
    QuickSend: boolean;
}

interface FundTransactionRequest {
    Password: string;
    Address: string;
    FeeType: string;
    Inputs: string[];
}

function rebuildTransaction() {

    let donateChange: HTMLInputElement = document.getElementById("donate-change-radio") as HTMLInputElement;
    let spendWholeCoin: HTMLInputElement = document.getElementById("spend-whole-coin-radio") as HTMLInputElement;

    let buildTransactionRequest: BuildTransactionRequest = null;
    let fundTransactionRequest: FundTransactionRequest = null;

    let coins = document.getElementsByName("coin");
    let allSelected = true;
    let selectedNum = 0;
    for (let i = 0; i < coins.length; i++) {
        let coin: HTMLInputElement = coins[i] as HTMLInputElement;
        if (coin.checked) {
            selectedNum++;
        }
        else {
            allSelected = false;
        }
    }

    if (!allSelected && selectedNum > 0)
    {
        let inputs: string[] = [];
        for (let i = 0; i < coins.length; i++) {
            let coin: HTMLInputElement = coins[i] as HTMLInputElement;
            if (coin.checked) {
                inputs.push(coin.value);
            }
        }
        fundTransactionRequest = { Address: previousRequest.Address, Password: previousRequest.Password, FeeType: previousRequest.FeeType, Inputs: inputs };
    }    
    else if (spendWholeCoin.checked)
    {
        let inputs: string[] = [];
        for (let i = 0; i < allInputCoins.length; i++) {
            inputs.push(allInputCoins[i].Index + ':' + allInputCoins[i].Hash);
        }
        fundTransactionRequest = { Address: previousRequest.Address, Password: previousRequest.Password, FeeType: previousRequest.FeeType, Inputs: inputs };
    }
    else if (donateChange.checked)
    {
        buildTransactionRequest = previousRequest;
        buildTransactionRequest.DonateChange = true;
    }
    let json: any;

    let containerElement: Element = document.getElementsByClassName("container").item(0);
    let buildTXButton: HTMLElement = document.getElementById("rebuild-button");

    containerElement.setAttribute("style", "pointer-events:none;");

    buildTXButton.innerHTML = '<span class="glyphicon glyphicon-cog spinning"></span> Building...';

    let request;
    let buildFund;
    if (buildTransactionRequest !== null)
    {
        request = buildTransactionRequest;
        buildFund = "build";
    }
    else if (fundTransactionRequest !== null)
    {
        request = fundTransactionRequest;
        buildFund = "fund";
    }
    else
    {
        alert("Couldn't build the request");
        return;
    }

    httpPostWalletAsync(`${buildFund}-transaction/${previousBobOrAlice}`, request, function (json) {
        if (json.Success === false) {
            let alertMessage: string = "Couldn't rebuild the transaciton";

            try {
                alertMessage += ": " + json.Message;
                if (json.Details) {
                    alertMessage = alertMessage
                        + "\n"
                        + "\n" + json.Details;
                }
            } catch {

            }            

            alert(alertMessage);
        }
        else {
            reloadPage(json, previousBobOrAlice, request);
        }

        buildTXButton.innerHTML = '<span class="mdi mdi-tor"></span> Rebuild Transaction';
        containerElement.setAttribute("style", "pointer-events:all;");
    });
}

function broadcastTransaction() {
    let broadcastButton: HTMLElement = document.getElementById("broadcast-button");

    let containerElement: Element = document.getElementsByClassName("container").item(0);

    containerElement.setAttribute("style", "pointer-events:none;");
    broadcastButton.innerHTML = '<span class="glyphicon glyphicon-refresh spinning"></span> Broadcasting...';

    var obj: BroadcastTransaction = { Hex: hex, QuickSend: false };
    
    httpPostWalletAsync("send-transaction", obj, function (json) {
        if (json.Success) {
            alert("SUCCESS! Transaction is successfully broadcasted!");
            window.parent.focus();
            window.close();
        }
        else {
            let alertMessage = "FAIL! ";

            try {
                alertMessage = alertMessage + json.Message;
                if (json.Details) {
                    alertMessage = alertMessage
                        + "\n"
                        + "\n" + json.Details;
                }
            } catch{ }

            alert(alertMessage);

            broadcastButton.innerHTML = '<span class="mdi mdi-tor"></span> Try Broadcast Again';
            containerElement.setAttribute("style", "pointer-events:all;");
        }
    });
}