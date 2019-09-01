# Tests

We divide tests into two diffent categories. **Unit Tests** and **Integration Tests**.  

**Unit Tests** run on CI. We define these as tests those have no **external dependencies**, however **local dependencies** are allowed.  
We define **Integration Tests** as tests those have **external dependencies**. Because these **external dependencies** are unreliable, **Integration Tests** do not run on CI.  

Examples for **external dependencies** are communication over the Internet, over the Tor network or with hardware wallets.  
Examples for **local dependencies** are the file system, processes those do not interact with external dependencies, like these HWI's version command call or system mutexes.  
