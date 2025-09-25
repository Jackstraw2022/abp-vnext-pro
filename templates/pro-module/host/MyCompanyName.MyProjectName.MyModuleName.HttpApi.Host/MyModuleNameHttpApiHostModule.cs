using Lion.AbpPro.AspNetCore;

namespace MyCompanyName.MyProjectName.MyModuleName;

[DependsOn(
    typeof(AbpProAspNetCoreModule),
    typeof(MyModuleNameApplicationModule),
    typeof(MyModuleNameEntityFrameworkCoreModule),
    typeof(MyModuleNameHttpApiModule),
    typeof(AbpAspNetCoreMvcUiMultiTenancyModule),
    typeof(AbpAutofacModule),
    typeof(AbpCachingStackExchangeRedisModule),
    typeof(AbpEntityFrameworkCoreMySQLModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpSwashbuckleModule)
)]
public class MyModuleNameHttpApiHostModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpProSwagger("MyProjectName")
            .AddAbpProRedis()
            .AddAbpProCors()
            .AddAbpProLocalization()
            .AddAbpProExceptions()
            .AddAbpProHealthChecks()
            .AddAbpProTenantResolvers()
            .AddAbpProMultiTenancy()
            .AddAbpProAntiForgery()
            .AddAbpProVirtualFileSystem()
            .AddAbpProDbContext()
            .AddAlwaysAllowAuthorization();
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        app.UseAbpProRequestLocalization();
        app.UseCorrelationId();
        app.MapAbpStaticAssets();
        app.UseRouting();
        app.UseAbpProCors();
        app.UseAuthentication();
        app.UseAbpProMultiTenancy();
        app.UseAuthorization();
        app.UseAbpProSwaggerUI("/swagger/MyProjectName/swagger.json", "MyProjectName");
        app.UseAbpSerilogEnrichers();
        app.UseUnitOfWork();
        app.UseConfiguredEndpoints(endpoints => { endpoints.MapHealthChecks("/health"); });
    }
}