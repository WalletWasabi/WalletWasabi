const storage = require('electron-json-storage');

storage.has('lastAccount', function (error, hasKey) {
    if (error) throw error;

    if (!hasKey) {
        storage.set('lastAccount', { lastAccount: 'alice' }, function (error) {
            if (error) throw error;
        });
    }
});