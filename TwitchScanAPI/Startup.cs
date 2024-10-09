using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using TwitchScanAPI.Data.Twitch.Manager;
using TwitchScanAPI.DbContext;
using TwitchScanAPI.HostedServices;
using TwitchScanAPI.Hubs;
using TwitchScanAPI.Services;

namespace TwitchScanAPI
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddSignalR();
            
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "TwitchScanAPI", Version = "v1" });
            });
            
            services.AddCors(options =>
            {
                options.AddPolicy("AllowSpecificOrigins",
                    builder =>
                    {
                        builder.SetIsOriginAllowedToAllowWildcardSubdomains()
                            .SetIsOriginAllowed(_ => true)
                            .WithOrigins("https://dreckbu.de", "http://localhost:4200")
                            .AllowAnyHeader()
                            .AllowAnyMethod()
                            .AllowCredentials(); // Allow credentials for SignalR
                    });
            });
            
            // Register services
            services.AddSingleton<NotificationService>();
            services.AddSingleton<TwitchVodService>();
            services.AddSingleton<TwitchChannelManager>();
            services.AddHttpClient<TwitchAuthService>();
            // Register DbContext
            services.AddSingleton<MongoDbContext>();
            services.AddHostedService<TwitchChannelManagerHostedService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();

                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "TwitchScanAPI v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();
            
            // Enable CORS
            app.UseCors("AllowSpecificOrigins");

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHub<TwitchHub>("/twitchHub"); // Map the SignalR hub
            });
        }
    }
}