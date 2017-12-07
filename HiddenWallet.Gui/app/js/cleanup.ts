// Object to capture process exits

exports.Cleanup = function Cleanup() {
    
    // catch ctrl+c event and exit normally
    (process as NodeJS.EventEmitter).on('SIGINT', function () {
        console.log('Ctrl-C...');
        process.exit(2);
    });

    //catch uncaught exceptions, trace, then exit normally
    (process as NodeJS.EventEmitter).on('uncaughtException', function (e) {
        console.log('Uncaught Exception...');
        console.log(e.stack);
        process.exit(99);
    });
};