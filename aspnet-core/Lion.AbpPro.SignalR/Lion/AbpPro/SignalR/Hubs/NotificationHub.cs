using Microsoft.AspNetCore.Authorization;
using Volo.Abp.AspNetCore.SignalR;
using Volo.Abp.Auditing;

namespace Lion.AbpPro.SignalR.Hubs
{
    [HubRoute("SignalR/Notification")]
    [Authorize]
    [DisableAuditing]
    public class NotificationHub : AbpHub<INotificationHub>
    {
    }
}