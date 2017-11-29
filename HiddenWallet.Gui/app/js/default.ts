const packageJson = require('./package.json');

let title: string = `HiddenWallet ${packageJson.version} - ${packageJson.author.name}  (EXPERIMENTAL)`;

let titleElement: HTMLElement = document.getElementById("title");

titleElement.innerHTML = title;
document.title = title;

(function () {
    const remote = require('electron').remote;

    function init() {
        let closeButton: HTMLElement = document.getElementById("close-btn");
        let hideButton: HTMLElement = document.getElementById("hide-btn");

        closeButton.addEventListener("click", function (e) {
            const window = remote.getCurrentWindow();
            const BrowserWindow = remote.BrowserWindow;
            var shutDownWindow = new BrowserWindow({ width: 300, height: 60, frame: true, resizable: false, title: "HiddenWallet", icon: __dirname + '/app/assets/TumbleBit.png' });
            shutDownWindow.show();
            shutDownWindow.focus();
            shutDownWindow.loadURL('file://' + __dirname + '/app/html/shutdown.html');
            window.hide();

            try {
                httpGetWallet("shutdown"); //An exception means the web service is offline and therefore shutdown has succeeded
            }
            catch (e) { }

            shutDownWindow.close();
            window.close();
        });

        hideButton.addEventListener("click", function (e) {
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

function writeHint(message: string) {
    let hint: HTMLElement = document.getElementById('hint');
    hint.innerHTML = message;
}