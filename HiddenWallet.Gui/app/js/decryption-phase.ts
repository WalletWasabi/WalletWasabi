function blockUntilApiReady() {
    try {
        httpGetWallet("test", false);
    }
    catch (err) {
        wait(100);
        blockUntilApiReady();
    }
}
function wait(ms: number) {
    var start: number = Date.now();
    var now: number = start;

    while (now - start < ms) {
        now = Date.now();
    }
}

let walletExists: boolean;

function decryptionPhaseShow(menuItem: string = "") {
    let decPhaseMenuFrame: HTMLIFrameElement = (<HTMLIFrameElement>document.getElementById("decryption-phase-menu-frame"));
    let decPhaseContentFrame: HTMLIFrameElement = <HTMLIFrameElement>document.getElementById("decryption-phase-content-frame")

    let content: HTMLElement = document.getElementById("content");
    let menu: HTMLElement = document.getElementById("menu");
    let hideButton: HTMLElement = document.getElementById("hide-btn");
    let decActive: HTMLElement = decPhaseMenuFrame.contentWindow.document.getElementById("decrypt-active");
    let decContent: HTMLElement = decPhaseContentFrame.contentWindow.document.getElementById("decrypt-content");
    let recActive: HTMLElement = decPhaseMenuFrame.contentWindow.document.getElementById("recover-active");
    let recContent: HTMLElement = decPhaseContentFrame.contentWindow.document.getElementById("recover-content");

    menu.hidden = false;
    hideButton.hidden = true;

    if (menuItem === "") {
        blockUntilApiReady();

        walletExists = httpGetWallet("wallet-exists").Value;

        if (walletExists === true) {
            let alreadyRunning: boolean = httpGetWallet("status").WalletState.toUpperCase() !== "NotStarted".toUpperCase();

            if (alreadyRunning) {
                walletPhaseShow();
            }
            else {
                decryptionPhaseShow("decrypt");
            }
        }
        else {
            decryptionPhaseShow("generate");
        }
    }
    else if (menuItem === "decrypt") {
        menu.innerHTML = decActive.innerHTML;

        if (walletExists === true) {
            content.innerHTML = decContent.innerHTML;
        }
        else {
            content.innerHTML = '<div class="alert alert-warning" role="alert"><strong>No wallet found!</strong> Generate or recover your wallet before decrypting it!</div>';
        }
    }
    else if (menuItem === "generate") {
        menu.innerHTML = decPhaseMenuFrame.contentWindow.document.getElementById("generate-active").innerHTML;

        if (walletExists === false) {
            content.innerHTML = decPhaseContentFrame.contentWindow.document.getElementById("generate-content").innerHTML;
        }
        else {
            content.innerHTML = '<div class="alert alert-warning" role="alert"><strong>Wallet already exists!</strong> If you wish to continue with this operation you first need to delete or rename your wallet file!</div>';
        }
    }
    else if (menuItem === "recover") {
        menu.innerHTML = recActive.innerHTML;

        if (walletExists === false) {
            content.innerHTML = recContent.innerHTML;
        }
        else {
            content.innerHTML = '<div class="alert alert-warning" role="alert"><strong>Wallet already exists!</strong> If you wish to continue with this operation you first need to delete or rename your wallet file!</div>';
        }
    }
}

interface GenerateWallet {
    Password: string;
}

function generateWallet() {
    let decPhaseContentFrame: HTMLIFrameElement = (<HTMLIFrameElement>document.getElementById("decryption-phase-content-frame"));

    let menu: HTMLElement = document.getElementById("menu");
    let genWalletButton: HTMLElement = document.getElementById("generate-wallet-button");
    let mnemonic: HTMLElement = decPhaseContentFrame.contentWindow.document.getElementById("mnemonic-words");
    let creation: HTMLElement = decPhaseContentFrame.contentWindow.document.getElementById("wallet-creation-time");
    let generated: HTMLElement = decPhaseContentFrame.contentWindow.document.getElementById("wallet-generated-content");
    let content: HTMLElement = document.getElementById("content");

    let containerElement: Element = document.getElementsByClassName("container").item(0);

    let password: string = (<HTMLInputElement>document.getElementById("inputPassword")).value;
    let passwordConfirm: string = (<HTMLInputElement>document.getElementById("confirmPassword")).value;


    if (password !== passwordConfirm) {
        alert("Could not generate wallet, details:\n\nPassword confirmation does not match the password");
    }
    else {
        var obj: GenerateWallet = { Password: password };

        containerElement.setAttribute("style", "pointer-events:none;");
        genWalletButton.innerHTML = '<span class="glyphicon glyphicon-cog spinning"></span> Generating...';

        httpPostWalletAsync("create", obj, function (json) {
            if (json.Success == false) {
                alert("Could not generate wallet, details:\n\n" + json.Message);
                genWalletButton.innerHTML = "Generate";
            }
            else {
                mnemonic.innerHTML = json.Mnemonic;
                creation.innerHTML = json.CreationTime.substr(0, 10);
                content.innerHTML = generated.innerHTML;
                menu.hidden = true;
            }
            containerElement.setAttribute("style", "pointer-events:all;");
        });
    }
}

interface RecoverWallet {
    Password: string;
    Mnemonic: string;
    CreationTime: string;
}

function recoverWallet() {
    let recWalletButton: HTMLElement = document.getElementById("recover-wallet-button");

    let containerElement: Element = document.getElementsByClassName("container").item(0);

    let password: string = (<HTMLInputElement>document.getElementById("inputPassword")).value;
    let mnemonic: string = (<HTMLInputElement>document.getElementById("inputMnemonic")).value.trim();
    let syncFrom: string = (<HTMLInputElement>document.getElementById("inputSyncFrom")).value.trim();

    if (mnemonic === "") {
        alert("Could not recover wallet, details:\n\nMnemonic is required");
    }
    else if (mnemonic.split(" ").length !== 12) {
        alert("Could not recover wallet, details:\n\nWrong mnemonic format. 12 mnemonic words are required.");
    }
    else if (syncFrom === "") {
        alert("Could not recover wallet, details:\n\n'Syncronize transactions from' date is required");
    }
    else if (syncFrom.length !== 10) {
        alert("Could not recover wallet, details:\n\nWrong 'Syncronize transactions from' date format. Format must be like: 2017-01-01");
    }
    else {
        var obj: RecoverWallet = { Password: password, Mnemonic: mnemonic, CreationTime: syncFrom };

        containerElement.setAttribute("style", "pointer-events:none;");
        recWalletButton.innerHTML = '<span class="glyphicon glyphicon-cog spinning"></span> Recovering...';

        httpPostWalletAsync("recover", obj, function (json) {
            if (json.Success == false) {
                alert("Could not recover wallet, details:\n\n" + json.Message);
                recWalletButton.innerHTML = "Recover";
            }
            else {
                alert("Wallet is successfully recovered!");
                decryptionPhaseShow();
            }
            containerElement.setAttribute("style", "pointer-events:all;");
        });
    }
}

interface DecryptWallet {
    Password: string;
}

function decryptWallet() {
    let password: string = (<HTMLInputElement>document.getElementById("inputPassword")).value;

    let decWalletButton: HTMLElement = document.getElementById("decrypt-wallet-button");

    let containerElement: Element = document.getElementsByClassName("container").item(0);

    var obj: DecryptWallet = { Password: password };

    containerElement.setAttribute("style", "pointer-events:none;");
    decWalletButton.innerHTML = '<span class="glyphicon glyphicon-cog spinning"></span> Initializing...';

    httpPostWalletAsync("load", obj, function (json) {
        if (json.Success == false) {
            alert("Could not decrypt wallet, details:\n\n" + json.Message);
            decWalletButton.innerHTML = "Decrypt";
        }
        else {
            walletPhaseShow();
        }
        containerElement.setAttribute("style", "pointer-events:all;");
    });
}