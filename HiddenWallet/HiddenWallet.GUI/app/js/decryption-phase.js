var walletExists;
function decryptionPhaseShow(menuItem = "") {
    document.getElementById("hide-btn").hidden = true;
    if (menuItem === "") {
        walletExists = httpGetWallet("wallet-exists").Value;
        if (walletExists === true) {
            var alreadyRunning = httpGetWallet("status").WalletState.toUpperCase() !== "NotStarted".toUpperCase();
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
    var password = document.getElementById("inputPassword").value;
    if (password !== document.getElementById("confirmPassword").value) {
        alert("Password confirmation does not match the password", "Could not generate wallet");
    }
    else {
        var obj = new Object();
        obj.password = password;
        var result = httpPostWallet("create", obj);

        if (result.Success == false) {
            alert(result.Message, "Could not generate wallet");
        }
        else {
            var message = "Write down these mnemonic words:\n\n"
                + result.Mnemonic + "\n\n"
                + "You can recover your wallet on any computer with:\n"
                + "- the mnemonic words AND\n"
                + "- your password AND\n"
                + "- the wallet creation time: " + result.CreationTime.substr(0, 10)
                + "\n\n"
                + "Unlike most other wallets if an attacker acquires your mnemonic words, it will not be able to hack your wallet without knowing your password. On the contrary, unlike other wallets, you will not be able to recover your wallet only with the mnemonic words if you lose your password.";
            alert(message, "Wallet is successfully generated");
        }
        decryptionPhaseShow();
    }
}

function recoverWallet() {
    var password = document.getElementById("inputPassword").value;
    var mnemonic = document.getElementById("inputMnemonic").value.trim();
    var syncFrom = document.getElementById("inputSyncFrom").value.trim();

    if (mnemonic === "") {
        alert("Mnemonic is required", "Could not recover wallet");
    }
    else if (mnemonic.split(" ").length !== 12) {
        alert("Wrong mnemonic format. 12 mnemonic words are required.", "Could not recover wallet");
    }
    else if (syncFrom === "") {
        alert("'Syncronize transactions from' date is required", "Could not recover wallet");
    }
    else if (syncFrom.length !== 10) {
        alert("Wrong 'Syncronize transactions from' date format. Format must be like: 2017-01-01", "Could not recover wallet");
    }
    else {
        var obj = new Object();
        obj.password = password;
        obj.mnemonic = mnemonic;
        obj.creationTime = syncFrom;
        var result = httpPostWallet("recover", obj);

        if (result.Success == false) {
            alert(result.Message, "Could not recover wallet");
        }
        else {
            alert("Wallet is successfully recovered!", "Success");
        }
        decryptionPhaseShow();
    }
}

function decryptWallet() {
    var password = document.getElementById("inputPassword").value;

    var obj = new Object();
    obj.password = password;
    var result = httpPostWallet("load", obj);

    if (result.Success == false) {
        alert(result.Message, "Could not decrypt wallet");
    }
    else {
        walletPhaseShow();
    }
}