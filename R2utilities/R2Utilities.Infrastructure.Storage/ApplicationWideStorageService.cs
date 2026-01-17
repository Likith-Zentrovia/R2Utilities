using System.Collections;
using R2V2.Infrastructure.Logging;
using R2V2.Infrastructure.Storages;

namespace R2Utilities.Infrastructure.Storage;

public class ApplicationWideStorageService : IApplicationWideStorageService, IStorageService
{
	private static readonly Hashtable Storage = new Hashtable();

	private readonly ILog<ApplicationWideStorageService> _log;

	public ApplicationWideStorageService(ILog<ApplicationWideStorageService> log)
	{
		_log = log;
	}

	public T Get<T>(string key)
	{
		return (T)Get(key);
	}

	public object Get(string key)
	{
		return Storage[key];
	}

	public void Put(string key, object value)
	{
		Storage.Add(key, value);
	}

	public void Remove(string key)
	{
		_log.DebugFormat("Delete() - key: {0}", key);
		Storage.Remove(key);
	}

	public bool Has(string key)
	{
		return Get(key) != null;
	}

	public int Count()
	{
		return Storage.Count;
	}

	public IDictionaryEnumerator GetEnumerator()
	{
		return Storage.GetEnumerator();
	}
}
