using System;
using System.Collections;
using System.Linq;
using R2V2.Extensions;
using R2V2.Infrastructure.Storages;

namespace R2Utilities.Infrastructure.Storage;

public class LocalStorageService : ILocalStorageService, IDisposable
{
	private static readonly object LocalDataKey = new object();

	[ThreadStatic]
	private static readonly Hashtable ThreadsLocal = new Hashtable();

	protected Hashtable Local => ThreadsLocal;

	public void Remove(string key)
	{
		if (Local.ContainsKey(key))
		{
			object item = Local[key];
			if (item is IDisposable)
			{
				item.As<IDisposable>().Dispose();
			}
			Local.Remove(key);
		}
	}

	public ICollection Keys()
	{
		return Local.Keys;
	}

	public void Clear()
	{
		foreach (IDisposable item in Local.Values.OfType<IDisposable>())
		{
			item.As<IDisposable>().Dispose();
		}
		Local.Clear();
	}

	public T Get<T>(object key) where T : class
	{
		object item = Get(key);
		return (item != null) ? item.As<T>() : null;
	}

	public object Get(object key)
	{
		object item = Local[key];
		if (item is WeakReference)
		{
			WeakReference reference = item.As<WeakReference>();
			return reference.IsAlive ? reference.Target : null;
		}
		return item;
	}

	public void PutWeak(object key, object item)
	{
		Put(key, item);
	}

	public void Put(object key, object item)
	{
		Local[key] = item;
	}

	public bool Has(object key)
	{
		object item = Local[key];
		if (item is WeakReference)
		{
			return item.As<WeakReference>().IsAlive;
		}
		return item != null;
	}

	public void Dispose()
	{
		ArrayList tempList = new ArrayList(Local.Values);
		foreach (object item in tempList)
		{
			if (item is IDisposable)
			{
				item.As<IDisposable>().Dispose();
			}
			if (item is WeakReference)
			{
				WeakReference weakItem = item.As<WeakReference>();
				if (weakItem.IsAlive && weakItem.Target is IDisposable)
				{
					weakItem.Target.As<IDisposable>().Dispose();
				}
			}
		}
	}
}
