using CompanyName.ProjectName.NotificationManagement.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace CompanyName.ProjectName.NotificationManagement
{
    /* Domain tests are configured to use the EF Core provider.
     * You can switch to MongoDB, however your domain tests should be
     * database independent anyway.
     */
    [DependsOn(
        typeof(NotificationManagementEntityFrameworkCoreTestModule)
        )]
    public class NotificationManagementDomainTestModule : AbpModule
    {
        
    }
}
