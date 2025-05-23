﻿using Medallion.Threading;
using Medallion.Threading.Redis;
using Volo.Abp.BlobStoring;
using Volo.Abp.BlobStoring.FileSystem;
#pragma warning disable CS0618 // Type or member is obsolete

namespace Lion.AbpPro;

public partial class AbpProHttpApiHostModule
{
    private void ConfigureHangfire(ServiceConfigurationContext context)
    {
        var redisStorageOptions = new RedisStorageOptions()
        {
            Db = context.Services.GetConfiguration().GetValue<int>("Hangfire:Redis:DB")
        };

        Configure<AbpBackgroundJobOptions>(options => { options.IsJobExecutionEnabled = true; });

        context.Services.AddHangfire(config =>
        {
            config.UseRedisStorage(
                    context.Services.GetConfiguration().GetValue<string>("Hangfire:Redis:Host"), redisStorageOptions)
                .WithJobExpirationTimeout(TimeSpan.FromDays(7));
            var delaysInSeconds = new[] { 10, 60, 60 * 3 }; // 重试时间间隔
            const int Attempts = 3; // 重试次数
            config.UseFilter(new AutomaticRetryAttribute() { Attempts = Attempts, DelaysInSeconds = delaysInSeconds });
            //config.UseFilter(new AutoDeleteAfterSuccessAttribute(TimeSpan.FromDays(7)));
            config.UseFilter(new JobRetryLastFilter(Attempts));
        });
    }

    /// <summary>
    /// 配置MiniProfiler
    /// </summary>
    private void ConfigureMiniProfiler(ServiceConfigurationContext context)
    {
        if (context.Services.GetConfiguration().GetValue("MiniProfiler:Enabled", false))
        {
            context.Services.AddMiniProfiler(options => options.RouteBasePath = "/profiler").AddEntityFramework();
        }
    }

    /// <summary>
    /// 配置JWT
    /// </summary>
    private void ConfigureJwtAuthentication(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters =
                    new TokenValidationParameters()
                    {
                        // 是否开启签名认证
                        ValidateIssuerSigningKey = true,
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero,
                        ValidIssuer = configuration["Jwt:Issuer"],
                        ValidAudience = configuration["Jwt:Audience"],
                        IssuerSigningKey =
                            new SymmetricSecurityKey(
                                Encoding.ASCII.GetBytes(configuration["Jwt:SecurityKey"]))
                    };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = currentContext =>
                    {
                        var path = currentContext.HttpContext.Request.Path;
                        if (path.StartsWithSegments("/login"))
                        {
                            return Task.CompletedTask;
                        }

                        var accessToken = string.Empty;
                        if (currentContext.HttpContext.Request.Headers.ContainsKey("Authorization"))
                        {
                            accessToken = currentContext.HttpContext.Request.Headers["Authorization"];
                            if (!string.IsNullOrWhiteSpace(accessToken))
                            {
                                accessToken = accessToken.Split(" ").LastOrDefault();
                            }
                        }

                        if (accessToken.IsNullOrWhiteSpace())
                        {
                            accessToken = currentContext.Request.Query["access_token"].FirstOrDefault();
                        }

                        if (accessToken.IsNullOrWhiteSpace())
                        {
                            accessToken = currentContext.Request.Cookies[AbpProHttpApiHostConst.DefaultCookieName];
                        }

                        currentContext.Token = accessToken;
                        currentContext.Request.Headers.Remove("Authorization");
                        currentContext.Request.Headers.Append("Authorization", $"Bearer {accessToken}");

                        return Task.CompletedTask;
                    }
                };
            });
    }


    /// <summary>
    /// Redis缓存
    /// </summary>
    private void ConfigureCache(ServiceConfigurationContext context)
    {
        Configure<AbpDistributedCacheOptions>(
            options => { options.KeyPrefix = "AbpPro:"; });
        var configuration = context.Services.GetConfiguration();
        var redis = ConnectionMultiplexer.Connect(configuration.GetValue<string>("Redis:Configuration"));
        context.Services
            .AddDataProtection()
            .PersistKeysToStackExchangeRedis(redis, "AbpPro-Protection-Keys");
    }

    /// <summary>
    /// 配置Identity
    /// </summary>
    private void ConfigureIdentity(ServiceConfigurationContext context)
    {
        context.Services.Configure<IdentityOptions>(options => { options.Lockout = new LockoutOptions() { AllowedForNewUsers = false }; });
    }

    private void ConfigurationSignalR(ServiceConfigurationContext context)
    {
        context.Services
            .AddSignalR()
            .AddStackExchangeRedis(context.Services.GetConfiguration().GetValue<string>("Redis:Configuration"),
                options => { options.Configuration.ChannelPrefix = "Lion.AbpPro"; });
    }

    private void ConfigureSwaggerServices(ServiceConfigurationContext context)
    {
        context.Services.AddSwaggerGen(
            options =>
            {
                // 文件下载类型
                options.MapType<FileContentResult>(() => new OpenApiSchema() { Type = "file" });

                options.SwaggerDoc("AbpPro",
                    new OpenApiInfo { Title = "AbpPro API", Version = "v1" });
                options.DocInclusionPredicate((docName, description) => true);
                options.EnableAnnotations(); // 启用注解
                options.DocumentFilter<HiddenAbpDefaultApiFilter>();
                options.SchemaFilter<EnumSchemaFilter>();
                // 加载所有xml注释，这里会导致swagger加载有点缓慢
                var xmlPaths = Directory.GetFiles(AppContext.BaseDirectory, "*.xml");
                foreach (var xml in xmlPaths)
                {
                    options.IncludeXmlComments(xml, true);
                }

                options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme,
                    new OpenApiSecurityScheme()
                    {
                        Description = "直接在下框输入JWT生成的Token",
                        Name = "Authorization",
                        In = ParameterLocation.Header,
                        Type = SecuritySchemeType.Http,
                        Scheme = JwtBearerDefaults.AuthenticationScheme,
                        BearerFormat = "JWT"
                    });
                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme, Id = "Bearer"
                            }
                        },
                        new List<string>()
                    }
                });

                options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme()
                {
                    Type = SecuritySchemeType.ApiKey,
                    In = ParameterLocation.Header,
                    Name = "Accept-Language",
                    Description = "多语言设置，系统预设语言有zh-Hans、en，默认为zh-Hans",
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                                { Type = ReferenceType.SecurityScheme, Id = "ApiKey" }
                        },
                        Array.Empty<string>()
                    }
                });
            });
    }


    private void ConfigureCap(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        context.AddAbpCap(capOptions =>
        {
            capOptions.SetCapDbConnectionString(configuration["ConnectionStrings:Default"]);
            capOptions.UseEntityFramework<AbpProDbContext>();
            capOptions.UseRabbitMQ(option =>
            {
                option.HostName = configuration.GetValue<string>("Cap:RabbitMq:HostName");
                option.UserName = configuration.GetValue<string>("Cap:RabbitMq:UserName");
                option.Password = configuration.GetValue<string>("Cap:RabbitMq:Password");
                option.Port = configuration.GetValue<int>("Cap:RabbitMq:Port");
            });
            capOptions.UseDashboard(options =>
            {
                options.AuthorizationPolicy = AbpProCapPermissions.CapManagement.Cap;
            });
        });
    }

    /// <summary>
    /// 审计日志
    /// </summary>
    private void ConfigureAuditLog(ServiceConfigurationContext context)
    {
        Configure<AbpAuditingOptions>
        (
            options =>
            {
                options.IsEnabled = true;
                options.EntityHistorySelectors.AddAllEntities();
                options.ApplicationName = "Lion.AbpPro";
            }
        );

        Configure<AbpAspNetCoreAuditingOptions>(
            options =>
            {
                options.IgnoredUrls.Add("/AuditLogs/page");
                options.IgnoredUrls.Add("/hangfire/stats");
                options.IgnoredUrls.Add("/hangfire/recurring/trigger");
                options.IgnoredUrls.Add("/cap");
                options.IgnoredUrls.Add("/");
            });
    }

    private void ConfigurationMultiTenancy()
    {
        Configure<AbpMultiTenancyOptions>(options => { options.IsEnabled = MultiTenancyConsts.IsEnabled; });
    }

    /// <summary>
    /// 配置redis分布式锁
    /// </summary>
    private void ConfigurationDistributedLocking(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        var connectionString = configuration.GetValue<string>("Redis:Configuration");
        context.Services.AddSingleton<IDistributedLockProvider>(sp =>
        {
            var connection = ConnectionMultiplexer.Connect(connectionString);
            return new RedisDistributedSynchronizationProvider(connection.GetDatabase());
        });
    }

    /// <summary>
    /// 配置blob设置
    /// </summary>
    private void ConfigureBlobStorage()
    {
        Configure<AbpBlobStoringOptions>(options =>
        {
            options.Containers.ConfigureDefault(container =>
            {
                container.UseFileSystem(fileSystem =>
                {
                    fileSystem.BasePath = "C:\\my-files";
                });
            });
        });
    }
}