using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication;
using WireMock.Server;

namespace Integration.Tests
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly WireMockServer _wireMockServer;
        public WireMockServer WireMock => _wireMockServer;

        private string FrankfutherBaseUrl => _wireMockServer.Urls[0] + "/";
        public CustomWebApplicationFactory()
        {
            _wireMockServer = WireMockServer.Start();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                // Override the base URL for Frankfurt
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "Frankfurter:BaseUrl", FrankfutherBaseUrl }
                });
            });

            builder.ConfigureServices(services =>
            {
                // Here you can add any additional service configurations if needed
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.Scheme;
                    options.DefaultChallengeScheme = TestAuthHandler.Scheme;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, _ => { });
            });
            
        }
    }
}
