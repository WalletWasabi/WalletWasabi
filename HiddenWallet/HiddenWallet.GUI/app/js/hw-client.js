function httpGetWallet(path) {
    var theUrl = "http://localhost:5000/api/v1/wallet/" + path;
    var xmlHttp = new XMLHttpRequest();
    xmlHttp.open("GET", theUrl, false); // false for synchronous request
    xmlHttp.send(null);
    return JSON.parse(xmlHttp.responseText);
}

function httpPostWallet(path, data) {
    var theUrl = "http://localhost:5000/api/v1/wallet/" + path;
    var xmlHttp = new XMLHttpRequest();
    xmlHttp.open("POST", theUrl, false); // false for synchronous request
    xmlHttp.setRequestHeader('Content-Type', 'application/json; charset=UTF-8');
    xmlHttp.send(JSON.stringify(data));
    return JSON.parse(xmlHttp.responseText);
}