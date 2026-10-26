using Canteen.BAL.Interfaces;
using Canteen.BAL.Services;
using Canteen.DAL.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Canteen.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "TestDb_" + Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<CanteenDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            var internalSp = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddDbContext<CanteenDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
                options.UseInternalServiceProvider(internalSp);
            });

            var paymentDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IPaymentService));
            if (paymentDescriptor != null)
            {
                services.Remove(paymentDescriptor);
            }
            services.AddScoped<IPaymentService, MockPaymentService>();
        });

        builder.UseEnvironment("Testing");
    }
}
