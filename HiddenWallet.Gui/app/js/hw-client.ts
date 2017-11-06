function httpGetWallet(path: string, returnJson = true) {
    let theUrl: string = "http://localhost:37120/api/v1/wallet/" + path;
    let xmlHttp: XMLHttpRequest = new XMLHttpRequest();
    xmlHttp.open("GET", theUrl, false); // false for synchronous request
    xmlHttp.send(null);

    if (returnJson) {
        return JSON.parse(xmlHttp.responseText);
    }
    else {
        return xmlHttp.responseText;
    }
}

function httpPostWallet(path: string, data: any) {
    let theUrl: string = "http://localhost:37120/api/v1/wallet/" + path;
    let xmlHttp: XMLHttpRequest = new XMLHttpRequest();
    xmlHttp.open("POST", theUrl, false); // false for synchronous request
    xmlHttp.setRequestHeader('Content-Type', 'application/json; charset=UTF-8');
    xmlHttp.send(JSON.stringify(data));
    return JSON.parse(xmlHttp.responseText);
}

function httpGetWalletAsync(path: string, callback) {
    let theUrl: string = "http://localhost:37120/api/v1/wallet/" + path;
    let xmlHttp: XMLHttpRequest = new XMLHttpRequest();

    xmlHttp.onreadystatechange = function () {
        if (xmlHttp.readyState == 4 && xmlHttp.status == 200)
            callback(JSON.parse(xmlHttp.responseText));
    }

    xmlHttp.open("GET", theUrl, true); // true for asynchronous 
    xmlHttp.send(null);
}

function httpPostWalletAsync(path: string, data: any, callback) {
    let theUrl = "http://localhost:37120/api/v1/wallet/" + path;
    let xmlHttp: XMLHttpRequest = new XMLHttpRequest();

    xmlHttp.onreadystatechange = function () {
        if (xmlHttp.readyState == 4 && xmlHttp.status == 200)
            callback(JSON.parse(xmlHttp.responseText));
    }

    xmlHttp.open("POST", theUrl, true); // true for asynchronous 
    xmlHttp.setRequestHeader('Content-Type', 'application/json; charset=UTF-8');
    xmlHttp.send(JSON.stringify(data));
}