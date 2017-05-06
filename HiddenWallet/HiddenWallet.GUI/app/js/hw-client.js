function httpGetWallet(path, returnString = false) {
    var theUrl = "http://localhost:5000/api/v1/wallet/" + path;
    var xmlHttp = new XMLHttpRequest();
    xmlHttp.open("GET", theUrl, false); // false for synchronous request
    xmlHttp.send(null);
    if (returnString) return xmlHttp.responseText;
    else return JSON.parse(xmlHttp.responseText);
}

function httpPostWallet(path, data, returnString = false) {
    var theUrl = "http://localhost:5000/api/v1/wallet/" + path;
    var xmlHttp = new XMLHttpRequest();
    xmlHttp.open("POST", theUrl, false); // false for synchronous request
    xmlHttp.setRequestHeader('Content-Type', 'application/json; charset=UTF-8');
    xmlHttp.send(JSON.stringify(data));
    if (returnString) return xmlHttp.responseText;
    else return JSON.parse(xmlHttp.responseText);
}