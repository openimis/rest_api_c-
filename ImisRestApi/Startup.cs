﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImisRestApi.Formaters;
using ImisRestApi.Repo;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Swashbuckle.AspNetCore.Swagger;

namespace ImisRestApi
{
    public class Startup
    {
        
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

            //
            //Configuration.Bind(head);
        }

        public IConfiguration Configuration { get; }

        
        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            TokenValidationParameters tokenParams = new TokenValidationParameters()
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidIssuer = Configuration["JWT:issuer"],
                ValidAudience = Configuration["JWT:audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["JWT:key"]))
            };

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
           .AddJwtBearer(jwtconfig =>
           {
               jwtconfig.TokenValidationParameters = tokenParams;
              // jwtconfig.SecurityTokenValidators.Clear();
              // jwtconfig.SecurityTokenValidators.Add(new TokenValidatorImis());
           });
            
            services.AddMvc(config => {
                config.RespectBrowserAcceptHeader = true;
                config.ReturnHttpNotAcceptable = true;
#if CHF
                config.InputFormatters.Add(new GePGXmlSerializerInputFormatter(Configuration));
#else
                config.InputFormatters.Add(new XmlSerializerInputFormatter());
#endif
                config.OutputFormatters.Add(new XmlSerializerOutputFormatter());
            });
            //services.ConfigureMvc();
            services.AddSwaggerGen(x => {
                x.SwaggerDoc("v1", new Info { Title = "IMIS REST" , Version = "v1"});
            });

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseBrowserLink();
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }
            app.UseAuthentication();
            app.UseStaticFiles();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });

            app.UseSwagger(c =>
            {
                c.PreSerializeFilters.Add((swagger, httpReq) => swagger.Host = httpReq.Host.Value);
            });

            app.UseSwaggerUI(x => {
#if CHF
                x.SwaggerEndpoint("/restapi/swagger/v1/swagger.json", "IMIS REST");
#else
                x.SwaggerEndpoint("/swagger/v1/swagger.json", "IMIS REST");
#endif
                // x.SwaggerEndpoint("/swagger/v1/swagger.json", "IMIS REST");
            });
        }
    }
}
