function blockUntilApiReady() {
    try {
        httpGetWallet("test", false);
    }
    catch (err) {
        wait(100);
        blockUntilApiReady();
    }
}
function wait(ms) {
    var start = Date.now(),
        now = start;
    while (now - start < ms) {
        now = Date.now();
    }
}

let walletExists;
function decryptionPhaseShow(menuItem = "") {
    document.getElementById("hide-btn").hidden = true;
    if (menuItem === "") {
        blockUntilApiReady();

        walletExists = httpGetWallet("wallet-exists").Value;
        if (walletExists === true) {
            let alreadyRunning = httpGetWallet("status").WalletState.toUpperCase() !== "NotStarted".toUpperCase();
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
        document.getElementById("menu").innerHTML = document.getElementById("decryption-phase-menu-frame").contentWindow.document.getElementById("decrypt-active").innerHTML;
        if (walletExists === true) {
            document.getElementById("content").innerHTML = document.getElementById("decryption-phase-content-frame").contentWindow.document.getElementById("decrypt-content").innerHTML;
        }
        else {
            document.getElementById("content").innerHTML = '<div class="alert alert-warning" role="alert"><strong>No wallet found!</strong> Generate or recover your wallet before decrypting it!</div>';
        }
    }
    else if (menuItem === "generate") {
        document.getElementById("menu").innerHTML = document.getElementById("decryption-phase-menu-frame").contentWindow.document.getElementById("generate-active").innerHTML;
        if (walletExists === false) {
            document.getElementById("content").innerHTML = document.getElementById("decryption-phase-content-frame").contentWindow.document.getElementById("generate-content").innerHTML;
        }
        else {
            document.getElementById("content").innerHTML = '<div class="alert alert-warning" role="alert"><strong>Wallet already exists!</strong> If you wish to continue with this operation you first need to delete or rename your wallet file!</div>';
        }
    }
    else if (menuItem === "recover") {
        document.getElementById("menu").innerHTML = document.getElementById("decryption-phase-menu-frame").contentWindow.document.getElementById("recover-active").innerHTML;
        if (walletExists === false) {
            document.getElementById("content").innerHTML = document.getElementById("decryption-phase-content-frame").contentWindow.document.getElementById("recover-content").innerHTML;
        }
        else {
            document.getElementById("content").innerHTML = '<div class="alert alert-warning" role="alert"><strong>Wallet already exists!</strong> If you wish to continue with this operation you first need to delete or rename your wallet file!</div>';
        }
    }
}

function generateWallet() {
    let password = document.getElementById("inputPassword").value;
    if (password !== document.getElementById("confirmPassword").value) {
        alert("Could not generate wallet, details:\n\nPassword confirmation does not match the password");
    }
    else {
        let obj = new Object();
        obj.password = password;

        document.getElementsByClassName("container").item(0).setAttribute("style", "pointer-events:none;");
        document.getElementById("generate-wallet-button").innerHTML = '<span class="glyphicon glyphicon-cog spinning"></span> Generating...';
        httpPostWalletAsync("create", obj, function (json) {
            if (json.Success == false) {
                alert("Could not generate wallet, details:\n\n" + json.Message);
                document.getElementById("generate-wallet-button").innerHTML = "Generate";
            }
            else {
                let message = "Write down these mnemonic words:\n\n"
                    + json.Mnemonic + "\n\n"
                    + "You can recover your wallet on any computer with:\n"
                    + "- the mnemonic words AND\n"
                    + "- your password AND\n"
                    + "- the wallet creation time: " + json.CreationTime.substr(0, 10)
                    + "\n\n"
                    + "Unlike most other wallets if an attacker acquires your mnemonic words, it will not be able to hack your wallet without knowing your password. On the contrary, unlike other wallets, you will not be able to recover your wallet only with the mnemonic words if you lose your password.";
                alert("Wallet is successfully generated!\n\n" + message);
                decryptionPhaseShow();
            }
            document.getElementsByClassName("container").item(0).setAttribute("style", "pointer-events:all;");
        });
    }
}

function recoverWallet() {
    let password = document.getElementById("inputPassword").value;
    let mnemonic = document.getElementById("inputMnemonic").value.trim();
    let syncFrom = document.getElementById("inputSyncFrom").value.trim();

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
        let obj = new Object();
        obj.password = password;
        obj.mnemonic = mnemonic;
        obj.creationTime = syncFrom;

        document.getElementsByClassName("container").item(0).setAttribute("style", "pointer-events:none;");
        document.getElementById("recover-wallet-button").innerHTML = '<span class="glyphicon glyphicon-cog spinning"></span> Recovering...';
        httpPostWalletAsync("recover", obj, function (json) {
            if (json.Success == false) {
                alert("Could not recover wallet, details:\n\n" + json.Message);
                document.getElementById("recover-wallet-button").innerHTML = "Recover";
            }
            else {
                alert("Wallet is successfully recovered!");
                decryptionPhaseShow();
            }
            document.getElementsByClassName("container").item(0).setAttribute("style", "pointer-events:all;");
        });
    }
}

function decryptWallet() {
    let password = document.getElementById("inputPassword").value;

    let obj = new Object();
    obj.password = password;

    document.getElementsByClassName("container").item(0).setAttribute("style", "pointer-events:none;");
    document.getElementById("decrypt-wallet-button").innerHTML = '<span class="glyphicon glyphicon-cog spinning"></span> Decrypting...';
    httpPostWalletAsync("load", obj, function (json) {
        if (json.Success == false) {
            alert("Could not decrypt wallet, details:\n\n" + json.Message);
            document.getElementById("decrypt-wallet-button").innerHTML = "Decrypt";
        }
        else {
            walletPhaseShow();
        }
        document.getElementsByClassName("container").item(0).setAttribute("style", "pointer-events:all;");
    });
}