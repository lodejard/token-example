using System;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ServerApp
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
                        .UseUrls("http://0.0.0.0:6000/");
                })
                .Build();

            await host.RunAsync();
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseMiddleware<BearerAuthMiddleware>();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.Run(async context =>
            {
                if (context.User?.Identity?.IsAuthenticated ?? false)
                {
                    await context.Response.WriteAsync($"Hello, {context.User?.Identity?.Name}");
                }
                else
                {
                    await context.Response.WriteAsync($"Hello, Anonymous");
                }
            });
        }
    }

    public class BearerAuthMiddleware
    {
        private readonly RequestDelegate next;

        public BearerAuthMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            if (AuthenticationHeaderValue.TryParse(httpContext.Request.Headers["Authorization"], out var auth) &&
                string.Equals(auth.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
            {
                using var kubernetes = new Kubernetes(KubernetesClientConfiguration.BuildDefaultConfig());

                var tokenReview = await kubernetes.CreateTokenReviewAsync(
                    new V1TokenReview(
                        new V1TokenReviewSpec(token: auth.Parameter)));

                if (tokenReview.Status.Authenticated.GetValueOrDefault())
                {
                    var identity = new ClaimsIdentity("Kubernetes");
                    identity.AddClaim(new Claim(identity.NameClaimType, tokenReview.Status.User.Username));
                    foreach(var group in tokenReview.Status.User.Groups)
                    {
                        identity.AddClaim(new Claim(identity.RoleClaimType, group));
                    }
                    httpContext.User = new ClaimsPrincipal(identity);
                }
            }

            await next.Invoke(httpContext);
        }
    }
}
