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

const electron = require('electron');
const remote = electron.remote;
const Menu = remote.Menu;

const EditMenu = Menu.buildFromTemplate([{
    label: 'Copy',
    role: 'copy',
}, {
    label: 'Paste',
    role: 'paste',
},
]);

const CopyMenu = Menu.buildFromTemplate([{
    label: 'Copy',
    role: 'copy',
},
]);

document.body.addEventListener('contextmenu', (mouseEvent: MouseEvent) => {
    mouseEvent.preventDefault();
    mouseEvent.stopPropagation();

    let node: HTMLInputElement = mouseEvent.target as HTMLInputElement;

    while (node) {
        if (node.nodeName.match(/^(input|textarea)$/i) || node.isContentEditable) {
            EditMenu.popup(remote.getCurrentWindow());
            break;
        }
        else {
            CopyMenu.popup(remote.getCurrentWindow());
            break;
        }
    }
});

// Close the dropdown menu if the user clicks outside of it
window.onclick = function (mouseEvent: MouseEvent) {
    let eventTarget: HTMLElement = mouseEvent.target as HTMLElement;

    if (eventTarget.className !== 'dropbtn') {

        var dropdowns: HTMLCollectionOf<Element> = document.getElementsByClassName("dropdown-content");

        for (let i: number = 0; i < dropdowns.length; i++) {
            var openDropdown: Element = dropdowns[i];
            if (openDropdown.classList.contains('show')) {
                openDropdown.classList.remove('show');
            }
        }
    }
}
