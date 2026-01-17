using System;
using System.Collections.Generic;
using System.Configuration;
using System.Reflection;
using Autofac;
using Common.Logging;
using R2Utilities.Infrastructure;
using R2Utilities.Infrastructure.Settings;
using R2V2.Infrastructure.Settings;

namespace R2Utilities;

public static class Bootstrapper
{
	public class InfrastructureModule : Autofac.Module
	{
		protected override void Load(ContainerBuilder builder)
		{
		}
	}

	private static readonly ILog Log;

	public static IContainer Container;

	static Bootstrapper()
	{
		Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType.FullName);
	}

	public static void Initialize()
	{
		try
		{
			List<Assembly> assemblies = new List<Assembly>();
			Assembly[] assemblies2 = AppDomain.CurrentDomain.GetAssemblies();
			foreach (Assembly assembly in assemblies2)
			{
				Log.DebugFormat("assembly.FullName: {0}", assembly.FullName);
				if (assembly.FullName.ToLower().Contains("r2v2") || assembly.FullName.ToLower().Contains("r2utilities"))
				{
					Log.DebugFormat("adding assembly: {0}", assembly.FullName);
					assemblies.Add(assembly);
				}
			}
			Container = ServiceLocatorBuilder.Build(assemblies);
			SettingsInitializer.Initialize(AutoDatabaseSettings.BuildAutoSettings(ConfigurationManager.AppSettings["SettingsConfigurationKey"]));
		}
		catch (Exception ex)
		{
			Log.Error(ex.Message, ex);
			throw;
		}
	}
}
