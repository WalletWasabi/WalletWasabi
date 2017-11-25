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
        if ((node.nodeName.match(/^(input|textarea)$/i) || node.isContentEditable) && !node.disabled) {
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
