const storage = require('electron-json-storage');

storage.has('lastAccount', function (error: any, hasKey: boolean) {
    if (error) {
        throw error
    };

    if (!hasKey) {
        storage.set('lastAccount', { lastAccount: 'alice' }, function (error) {
            if (error) {
                throw error
            };
        });
    }
});