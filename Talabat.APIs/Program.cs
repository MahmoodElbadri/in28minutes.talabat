using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis; // Add if needed for Redis connection
using Talabat.APIs.Errors;
using Talabat.APIs.Extensions;
using Talabat.APIs.Extentions;
using Talabat.APIs.Helpers;
using Talabat.APIs.Middlewares;
using Talabat.Core.Entities.Identity;
using Talabat.Core.Repository.Contract;
using Talabat.Repository;
using Talabat.Repository.Data;
using Talabat.Repository.Data.Data_Seed;
using Talabat.Repository.Identity;
using Talabat.Repository.Identity.DataSeed;
using Talabat.Repositry.Data;
using Talabat.Repositry.Data.Data_Seed;
using Talabat.Repositry.Identity;
using Talabat.Repositry.Identity.DataSeed;

namespace Talabat.APIs
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            #region Configure Services

            #region Databases
            builder.Services.AddDbContext<StoreContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("defaultConnection"))
            );

            builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
            {
                var connection = builder.Configuration.GetConnectionString("Redis");
                return ConnectionMultiplexer.Connect(connection);
            });

            builder.Services.AddDbContext<IdentityDbContext>(options =>
            {
                options.UseSqlServer(builder.Configuration.GetConnectionString("IdentityConnection"));
            });
            #endregion

            // Add identity services to container
            builder.Services.AddIdentity<AppUser, IdentityRole>()
                .AddEntityFrameworkStores<IdentityDbContext>();

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer(); // Required for minimal API and Swagger
            builder.Services.AddSwaggerServices();

            builder.Services.AddApplicationServices();
            builder.Services.AddIdentityServices();

            #endregion

            var app = builder.Build();

            #region Update Database and Data Seeding
            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;

                var dbContext = services.GetRequiredService<StoreContext>();
                var identityDbContext = services.GetRequiredService<IdentityDbContext>();
                var userManager = services.GetRequiredService<UserManager<AppUser>>();
                var loggerFactory = services.GetRequiredService<ILoggerFactory>();

                try
                {
                    // Database migration
                    await dbContext.Database.MigrateAsync();
                    await identityDbContext.Database.MigrateAsync();

                    // Data seeding
                    await DataSeeding.DataSeedAsync(dbContext, loggerFactory);
                    await IdentityDataSeeding.SeedIdentityDataAsync(userManager);
                }
                catch (Exception ex)
                {
                    var logger = loggerFactory.CreateLogger<Program>();
                    logger.LogError(ex, "An error occurred while updating the database");
                }
            }
            #endregion

            // Configure the HTTP request pipeline.
            #region Configure Middleware
            app.UseMiddleware<ExceptionMiddleware>();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger(); // Use Swagger in development
                app.UseSwaggerUI();
            }

            app.UseStatusCodePagesWithReExecute("/errors/{0}");
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseAuthentication(); // Added for authentication in .NET 8
            app.UseAuthorization();

            app.MapControllers();
            #endregion

            app.Run();
        }
    }
}
