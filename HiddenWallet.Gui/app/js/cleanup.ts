// Object to capture process exits and call app specific cleanup function

function noOp() { };

exports.Cleanup = function Cleanup(callback) {

    // attach user callback to the process event emitter
    // if no callback, it will still exit gracefully on Ctrl-C
    callback = callback || noOp;

    (process as NodeJS.EventEmitter).on('cleanup', callback);

    (process as NodeJS.EventEmitter).on('exit', function () {
        process.emit('cleanup');
    });

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