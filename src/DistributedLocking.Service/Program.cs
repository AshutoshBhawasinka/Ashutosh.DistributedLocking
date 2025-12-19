using Ashutosh.Common.Logger;
using Ashutosh.DistributedLocking.Service.Services;

var logger = new Logger(typeof(Program));

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "Ashutosh's Distributed Locking Service";
});

builder.Services.AddControllers();
builder.Services.AddSingleton<LockManager>();

var app = builder.Build();

app.MapControllers();

logger.Log("DistributedLocking Service starting...");

app.Run();
