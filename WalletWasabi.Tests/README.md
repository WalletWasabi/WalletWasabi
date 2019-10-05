# Tests

We divide tests into two different categories. **Unit Tests** and **Integration Tests**.  

We define **Unit Tests** as tests that have no **external dependencies**, however **local dependencies** are allowed. **Unit Tests** run on CI.  
We define **Integration Tests** as tests that have **external dependencies**. Because these **external dependencies** are unreliable, **Integration Tests** do not run on CI.  

Examples for **external dependencies** are communication over the Internet, over the Tor network or with hardware wallets.  
Examples for **local dependencies** are the file system, processes that do not interact with external dependencies, like these HWI's version command call or system mutexes.  
