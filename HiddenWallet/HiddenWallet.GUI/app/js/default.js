const packageJson = require('./package.json');
document.title = "HiddenWallet " + packageJson.version + " - " + packageJson.author.name + " (EXPERIMENTAL)";
document.getElementById("title").innerHTML = document.title;

(function () {

    const remote = require('electron').remote;

    function init() {
        document.getElementById("close-btn").addEventListener("click", function (e) {
            const window = remote.getCurrentWindow();
            const BrowserWindow = remote.BrowserWindow;
            var shutDownWindow = new BrowserWindow({ width: 300, height: 60, frame: true, resizable: false, title: "HiddenWallet" });
            shutDownWindow.show();
            shutDownWindow.focus();
            shutDownWindow.loadURL('file://' + __dirname + '/app/html/shutdown.html');
            window.hide();
            httpGetWallet("shutdown");
            shutDownWindow.close();
            window.close();
        });
        document.getElementById("hide-btn").addEventListener("click", function (e) {
            const window = remote.getCurrentWindow();
            window.close();
        });
    };

    document.onreadystatechange = function () {
        if (document.readyState == "complete") {
            init();
        }
    };
})();