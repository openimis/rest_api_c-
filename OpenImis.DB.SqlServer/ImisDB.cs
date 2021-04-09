﻿using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;

namespace OpenImis.DB.SqlServer
{
    public partial class ImisDB:IMISContext
    {

		protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
		{
			IConfigurationRoot configuration = new ConfigurationBuilder()
			.SetBasePath(Directory.GetCurrentDirectory())
			.AddJsonFile($"appsettings.json")
			//.AddJsonFile(Environment.GetEnvironmentVariable("REGISTRY_CONFIG_FILE"))
			//.AddJsonFile("appsettings.json")
			.AddJsonFile(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")!=null?$"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json": "appsettings.Production.json", optional: false, reloadOnChange: true)
			.Build();
			optionsBuilder.UseSqlServer(configuration.GetConnectionString("IMISDatabase"));
		}
	}
}
