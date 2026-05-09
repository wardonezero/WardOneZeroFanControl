using WardOneZeroFanControl;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options => { options.ServiceName = "WardOneZero Fan Control"; });

builder.Services.Configure<FanControlOptions>(builder.Configuration.GetSection("FanControl"));

builder.Services.AddSingleton<FanCurveService>();
builder.Services.AddSingleton<ECService>();

builder.Services.AddHostedService<Worker>();

IHost host = builder.Build();
host.Run();