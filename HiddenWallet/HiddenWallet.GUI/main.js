const electron = require('electron');
// Module to control application life.
const app = electron.app;
// Module to create native browser window.
const BrowserWindow = electron.BrowserWindow;

const path = require('path');
const url = require('url');

// Keep a global reference of the window object, if you don't, the window will
// be closed automatically when the JavaScript object is garbage collected.
let mainWindow;

function createWindow() {
    // Create the browser window.
    mainWindow = new BrowserWindow({ width: 800, height: 600, resizable: false, frame: false });
    
    // and load the index.html of the app.
    mainWindow.loadURL(url.format({
        pathname: path.join(__dirname, 'index.html'),
        protocol: 'file:',
        slashes: true
    }));

    // Emitted when the window is closed.
    mainWindow.on('closed', function () {
        // Dereference the window object, usually you would store windows
        // in an array if your app supports multi windows, this is the time
        // when you should delete the corresponding element.
        mainWindow = null;
    });
}

// This method will be called when Electron has finished
// initialization and is ready to create browser windows.
// Some APIs can only be used after this event occurs.
app.on('ready', startApi);

// Quit when all windows are closed.
app.on('window-all-closed', function () {
    // On OS X it is common for applications and their menu bar
    // to stay active until the user quits explicitly with Cmd + Q
    if (process.platform !== 'darwin') {
        app.quit();
    }
});

app.on('activate', function () {
    // On OS X it's common to re-create a window in the app when the
    // dock icon is clicked and there are no other windows open.
    if (mainWindow === null) {
        createWindow();
    }
});
// In this file you can include the rest of your app's specific main process
// code. You can also put them in separate files and require them here.

const os = require('os');
var apiProcess;
function startApi() {
    var spawn = require('child_process').spawn;
    //  run server
    apipath = path.join(__dirname, '..//HiddenWallet.API//bin//dist//current-target//HiddenWallet.API');    
    if (os.platform() === 'win32') {
        var apipath = path.join(__dirname, '..\\HiddenWallet.API\\bin\\dist\\current-target\\HiddenWallet.API.exe');
    }
    
    apiProcess = spawn(apipath, {
        detached: true        
    });
    
    apiProcess.stdout.on('data', (data) => {
        writeLog(`stdout: ${data}`);
        if (mainWindow == null) {
            createWindow();
        }
    });
}

// loads module and registers app specific cleanup callback...
const cleanup = require('./app/js/cleanup').Cleanup(myCleanup);
// defines app specific callback...
function myCleanup() {   
    writeLog('exit');
};

function writeLog(msg) {
    console.log(msg);
}

// Disable default menu
app.on('browser-window-created', function (e, window) {
    window.setMenu(null);    
});