namespace Lion.AbpPro.IdentityServers.ApiScopes
{
    public interface IApiScopeAppService : IApplicationService
    {
        Task<PagedResultDto<PagingApiScopeListOutput>> GetListAsync(PagingApiScopeListInput input);

        Task CreateAsync(CreateApiScopeInput input);

        Task UpdateAsync(UpdateCreateApiScopeInput input);
        
        Task DeleteAsync(IdInput input);

        Task<List<FromSelector<string, string>>> FindAllAsync();
    }
}