using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autofac;
using Autofac.Core;
using Autofac.Extras.CommonServiceLocator;
using Microsoft.Practices.ServiceLocation;
using R2V2.DataAccess;
using R2V2.Extensions;
using R2V2.Infrastructure.DependencyInjection;
using R2V2.Infrastructure.Logging;
using R2V2.Infrastructure.Settings;

namespace R2Utilities.Infrastructure;

public class ServiceLocatorBuilder
{
	public static IContainer Build(IEnumerable<Assembly> assemblies)
	{
		ContainerBuilder builder = new ContainerBuilder();
		Assembly[] arrayOfAssemblies = assemblies.ToArray();
		IEnumerable<IModule> modules = arrayOfAssemblies.FindImplementationsOf<IModule>();
		foreach (IModule module in modules)
		{
			builder.RegisterModule(module);
		}
		(from x in builder.RegisterAssemblyTypes(arrayOfAssemblies.ToArray())
			where x.GetCustomAttributes(typeof(DoNotRegisterWithContainerAttribute), inherit: false).IsEmpty() && !x.Inherits<IAutoSettings>()
			select x).DefaultRegistration();
		(from x in builder.RegisterAssemblyTypes(arrayOfAssemblies.ToArray())
			where x.Inherits<IAutoSettings>()
			select x).AsSelf().AsImplementedInterfaces().SingleInstance();
		builder.RegisterGeneric(typeof(NhibernateQueryableFacade<>)).As(typeof(IQueryable<>));
		builder.RegisterGeneric(typeof(Log<>)).As(typeof(ILog<>));
		IContainer container = builder.Build();
		ServiceLocator.SetLocatorProvider(() => new AutofacServiceLocator(container));
		return container;
	}
}
