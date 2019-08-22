﻿using System;
using System.Linq;
using System.Reflection;
using OpenImis.ModulesV2.Helpers;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenImis.ModulesV2.LoginModule;
using OpenImis.ModulesV2.ClaimModule;
using OpenImis.ModulesV2.InsureeModule;
using OpenImis.ModulesV2.CoverageModule;
using OpenImis.ModulesV2.PaymentModule;
using OpenImis.ModulesV2.MasterDataModule;
using OpenImis.ModulesV2.MasterDataModule.Logic;
using System.Diagnostics;

namespace OpenImis.ModulesV2
{
    public class ImisModules : IImisModules
    {
        private ILoginModule loginModule;

        private IInsureeModule insureeModule;
        private IClaimModule claimModule;
        private ICoverageModule coverageModule;
        private IPaymentModule paymentModule;
        private IMasterDataModule masterDataModule;

        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly IServiceProvider _serviceProvider;

        public ImisModules(IConfiguration configuration, ILoggerFactory loggerFactory, IServiceProvider serviceProvider)
        {
            _configuration = configuration;
            _logger = loggerFactory.CreateLogger("LoggerCategory");
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Creates and returns the login module version 2.
        /// </summary>
        /// <returns>
        /// The Login module V2.
        /// </returns>
        public ILoginModule GetLoginModule()
        {
            if (loginModule == null)
            {
                loginModule = new LoginModule.LoginModule(_configuration);

                Type loginLogicType = CreateTypeFromConfiguration("LoginModule", "LoginLogic", "OpenImis.ModulesV2.LoginModule.Logic.LoginLogic");
                loginModule.SetLoginLogic((LoginModule.Logic.ILoginLogic)ActivatorUtilities.CreateInstance(_serviceProvider, loginLogicType));
            }
            return loginModule;
        }

        /// <summary>
        /// Creates and returns the claim module version 2.
        /// </summary>
        /// <returns>
        /// The Claim module V2.
        /// </returns>
        public IClaimModule GetClaimModule()
        {
            if (claimModule == null)
            {
                claimModule = new ClaimModule.ClaimModule();

                Type claimLogicType = CreateTypeFromConfiguration("ClaimModule", "ClaimLogic", "OpenImis.ModulesV2.ClaimModule.Logic.ClaimLogic");
                claimModule.SetClaimLogic((ClaimModule.Logic.IClaimLogic)ActivatorUtilities.CreateInstance(_serviceProvider, claimLogicType));
            }
            return claimModule;
        }

        /// <summary>
        /// Creates and returns the insuree module version 2.
        /// </summary>
        /// <returns>
        /// The Insuree module V2.
        /// </returns>
        public IInsureeModule GetInsureeModule()
        {
            if (insureeModule == null)
            {
                insureeModule = new InsureeModule.InsureeModule(_configuration);

                Type familyLogicType = CreateTypeFromConfiguration("InsureeModule", "FamilyLogic", "OpenImis.ModulesV2.InsureeModule.Logic.FamilyLogic");
                insureeModule.SetFamilyLogic((InsureeModule.Logic.IFamilyLogic)ActivatorUtilities.CreateInstance(_serviceProvider, familyLogicType));

                Type policyLogicType = CreateTypeFromConfiguration("InsureeModule", "PolicyLogic", "OpenImis.ModulesV2.InsureeModule.Logic.PolicyLogic");
                insureeModule.SetPolicyLogic((InsureeModule.Logic.IPolicyLogic)ActivatorUtilities.CreateInstance(_serviceProvider, policyLogicType));

                Type contributionLogicType = CreateTypeFromConfiguration("InsureeModule", "ContributionLogic", "OpenImis.ModulesV2.InsureeModule.Logic.ContributionLogic");
                insureeModule.SetContributionLogic((InsureeModule.Logic.IContributionLogic)ActivatorUtilities.CreateInstance(_serviceProvider, contributionLogicType));
            }
            return insureeModule;
        }

        /// <summary>
        /// Creates and returns the payment module version 2.
        /// </summary>
        /// <returns>
        /// The Payment module V2.
        /// </returns>
        public ICoverageModule GetCoverageModule()
        {
            if (coverageModule == null)
            {
                coverageModule = new CoverageModule.CoverageModule(_configuration);

                Type coverageLogicType = CreateTypeFromConfiguration("CoverageModule", "CoverageLogic", "OpenImis.ModulesV2.CoverageModule.Logic.CoverageLogic");
                coverageModule.SetCoverageLogic((CoverageModule.Logic.ICoverageLogic)ActivatorUtilities.CreateInstance(_serviceProvider, coverageLogicType));
            }
            return coverageModule;
        }

        /// <summary>
        /// Creates and returns the payment module version 2.
        /// </summary>
        /// <returns>
        /// The Payment module V2.
        /// </returns>
        public IPaymentModule GetPaymentModule()
        {
            if (paymentModule == null)
            {
                paymentModule = new PaymentModule.PaymentModule(_configuration);

                Type paymentLogicType = CreateTypeFromConfiguration("PaymentModule", "PaymentLogic", "OpenImis.ModulesV2.PaymentModule.Logic.PaymentLogic");
                paymentModule.SetPaymentLogic((PaymentModule.Logic.IPaymentLogic)ActivatorUtilities.CreateInstance(_serviceProvider, paymentLogicType));
            }
            return paymentModule;
        }

        /// <summary>
        /// Creates and returns the master data module version 2.
        /// </summary>
        /// <returns>
        /// The MasterData module V2.
        /// </returns>
        public IMasterDataModule GetMasterDataModule()
        {
            if (masterDataModule == null)
            {
                masterDataModule = new MasterDataModule.MasterDataModule(_configuration);

                Type masterDataLogicType = CreateTypeFromConfiguration("MasterDataModule", "MasterDataLogic", "OpenImis.ModulesV2.MasterDataModule.Logic.MasterDataLogic");
                masterDataModule.SetMasterDataLogic((IMasterDataLogic)ActivatorUtilities.CreateInstance(_serviceProvider, masterDataLogicType));
            }
            return masterDataModule;
        }

        /// <summary>
		/// Creates and returns the type based on the string from the configuration 
		/// </summary>
		/// <param name="moduleName">The module name</param>
		/// <param name="sectionName">The section name</param>
		/// <param name="defaultValue">The default section value</param>
		/// <returns>Type represented by the section</returns>
		private Type CreateTypeFromConfiguration(string moduleName, string sectionName, string defaultValue)
        {
            Type type;

            Assembly assembly = Assembly.GetCallingAssembly();
            string part = GetSectionName(moduleName, sectionName, "2");
            type = assembly.GetType(part);
            if (type == null)
            {
                _logger.LogError(moduleName + " " + sectionName + " error: the type " + part + " was not found. Using default " + defaultValue + " configuration.");
                type = assembly.GetType(defaultValue);
            }
            else
            {
                _logger.LogInformation(moduleName + " load OK: " + part);
            }

            return type;
        }

        public string GetSectionName(string moduleName, string sectionName, string apiVersion)
        {
            string part = "";

            var listImisModules = _configuration.GetSection("ImisModules").Get<List<ConfigImisModules>>();
           
            var module = listImisModules.Where(m => m.Version == apiVersion).Select(x => GetPropValue(x, moduleName)).FirstOrDefault();

            if (GetPropValue(module, sectionName) != null)
            {
                part = GetPropValue(module, sectionName).ToString();
            }
            return part;
        }

        public static object GetPropValue(object src, string propName)
        {
            return src.GetType().GetProperty(propName).GetValue(src, null);
        }
    }
}
