# PooledServiceClientFactory
Custom pool for Dataverse Service Client
## Background
Recently, we are facing some challenges from socket depletion while connecting to Dynamics 365 instance through ServiceClient in nuget package Microsoft.PowerPlatform.Dataverse.Client.\
After some deep dive, we find out that when you dispose a ServiceClient instance, the underline socket connection is NOT closed.  Based on MS knowledge article, that is prepared for connection pool usage.\
Then, we also have tried shared (or static) instances, which leads to unsafe thread issues.  More specific, we are following MS suggestion that we connection to Dynamics 365 as one Application User (not individual named user), then set up CallID to identify who is actually doing the work. After switch to shared (or static) instances, we found all read / write threads are messed up with unpredictable user access.
## Solution
We must find out a way to use ServiceClient as scoped instances, while not dispose instances after each usage, but put back to a pool for future.\
Luckily, after some research we find out that .NET core has a built-in BlockingCollection<T> generic class for this kind of purpose.\
Therefore, we come up with the idea of this pooled Service Client factory.
## Explanation of Codes
Most codes are self-explanatory, just a few setences to outline the main idea here.\
The public interface is prepare for CDI whether for a .NET Core website or an Azure Function.\
The main class should be initialized with a valid Dynamics 365 connection string, and a capacity that satisfy normal work load.\
We assume at the peak time, you might need four times of normal work load, certainly please change that for your situation.\
For each increasing number or resources in the pool, we set up to 50% of the original capacity, you can adjust that based on your need as well.
## Sample usage
In program.cs, conduct a CDI like following.
```
services.AddSingleton<IPooledServiceClientFactory>(new PooledServiceClientFactory(YOUR_CRM_CONNECTION_STRING, YOUR_INIT_CAPACITY));
```
Whereever you need a scoped instnace, insert codes like following, assuming you have done CDI for your class.
```
ServiceClient client = _clientFactory.Acquire();
try
{
	client.CallerId = YOUR_CALLER_GUID;
	...
}
finally
{
	_clientFactory.Release(client);
}
```
## Conclusion
Follow the pattern as shown above, you can avoid short-lived ServiceClient, and still using them as scoped instance to guarantee thread safe.\
Just pull down the source codes into VS2022 and compile them, then you are ready to go.
## License
Free software, absolutely no warranty, use at your own risk!


