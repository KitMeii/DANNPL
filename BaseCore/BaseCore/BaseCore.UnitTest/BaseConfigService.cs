using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;
using BaseCore.Common;
using System.Configuration;
using BaseCore.Libs.Repository;
using MongoDB.Driver;
using Microsoft.Extensions.DependencyInjection;
using BaseCore.Repository.Authen;

namespace BaseCore.UnitTest
{
    public class BaseConfigService
    {
        public IOptions<AppSettings> Option;
        public readonly IConfiguration ConfigurationRoot;

        public BaseConfigService()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            ConfigurationRoot = builder.Build();
            Option = Options.Create(new AppSettings
            {
                Secret = ""
            });

            IServiceCollection service = new ServiceCollection();
            service.Configure<Settings>(
               options =>
               {
                   options.ConnectionString = ConfigurationRoot.GetSection("MongoDb:ConnectionString").Value;
                   options.Database = ConfigurationRoot.GetSection("MongoDb:Database").Value;
               });

            service.AddSingleton<IMongoClient, MongoClient>(_ => new MongoClient(ConfigurationRoot.GetSection("MongoDb:ConnectionString").Value));
            service.AddSingleton<IUserRepository, UserRepository>();
        }
    }
}
