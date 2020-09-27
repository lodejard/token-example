using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;

namespace ClientApp
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            using var host = Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .UseStartup<Program>()
                        .UseUrls("http://0.0.0.0:5000/");
                })
                .Build();

            var configuration = host.Services.GetRequiredService<IConfiguration>();

            var backgroundTask = Task.Run(() => BackgroundAsync(configuration["ServerAppUrl"]));
            var anotherBackgroundTask = Task.Run(() => AnotherBackgroundAsync(configuration["ServerAppUrl"]));

            await host.RunAsync();
        }

        public static async Task BackgroundAsync(string serverAppUrl)
        {
            Console.WriteLine($"Background work starting. Sending requests to {serverAppUrl}");

            while (true)
            {
                try
                {
                    using var kubernetes = new Kubernetes(KubernetesClientConfiguration.BuildDefaultConfig());

                    using var httpClient = new HttpClient(
                        new CredentialsHandler(kubernetes.Credentials)
                        {
                            InnerHandler = new HttpClientHandler()
                        });

                    var result = await httpClient.GetStringAsync(serverAppUrl);
                    Console.WriteLine(result);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }

        public static async Task AnotherBackgroundAsync(string serverAppUrl)
        {
            Console.WriteLine($"Background work starting. Sending requests to {serverAppUrl}");

            while (true)
            {
                try
                {
                    using var kubernetes = new Kubernetes(KubernetesClientConfiguration.BuildDefaultConfig());

                    var httpRequest = new HttpRequestMessage(HttpMethod.Get, serverAppUrl);
                    await kubernetes.Credentials.ProcessHttpRequestAsync(httpRequest, default);

                    using var httpClient = new HttpClient();
                    var result = await httpClient.SendAsync(httpRequest);

                    Console.WriteLine(await result.Content.ReadAsStringAsync());
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("Hello World!");
                });
            });
        }
    }

    public class CredentialsHandler : DelegatingHandler
    {
        private ServiceClientCredentials credentials;

        public CredentialsHandler(ServiceClientCredentials credentials)
        {
            this.credentials = credentials;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await this.credentials.ProcessHttpRequestAsync(request, cancellationToken);
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
