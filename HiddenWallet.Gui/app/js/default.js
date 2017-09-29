const packageJson = require('./package.json');
document.title = "HiddenWallet " + packageJson.version + " - " + packageJson.author.name + " (EXPERIMENTAL)";
document.getElementById("title").innerHTML = document.title;

(function () {

    const remote = require('electron').remote;

    function init() {
        document.getElementById("close-btn").addEventListener("click", function (e) {
            const window = remote.getCurrentWindow();
            const BrowserWindow = remote.BrowserWindow;
            var shutDownWindow = new BrowserWindow({ width: 300, height: 60, frame: true, resizable: false, title: "HiddenWallet", icon: __dirname + '/app/assets/TumbleBit.png' });
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

function writeHint(message) {
    document.getElementById('hint').innerHTML = message;
}

'use strict';

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

document.body.addEventListener('contextmenu', (e) => {
    e.preventDefault();
    e.stopPropagation();

    let node = e.target;

    while (node) {
        if (node.nodeName.match(/^(input|textarea)$/i) || node.isContentEditable) {
            EditMenu.popup(remote.getCurrentWindow());
            break;
        }
        else {
            CopyMenu.popup(remote.getCurrentWindow());
            break;
        }
        node = node.parentNode;
    }
});

// Close the dropdown menu if the user clicks outside of it
window.onclick = function (event) {
    if (!event.target.matches('.dropbtn')) {

        var dropdowns = document.getElementsByClassName("dropdown-content");
        var i;
        for (i = 0; i < dropdowns.length; i++) {
            var openDropdown = dropdowns[i];
            if (openDropdown.classList.contains('show')) {
                openDropdown.classList.remove('show');
            }
        }
    }
}