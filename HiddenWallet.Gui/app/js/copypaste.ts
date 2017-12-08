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
