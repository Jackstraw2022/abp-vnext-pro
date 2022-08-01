namespace Lion.AbpPro.Users
{
    public class AccountAppService : AbpProAppService, IAccountAppService
    {
        private readonly IdentityUserManager _userManager;
        private readonly JwtOptions _jwtOptions;
        private readonly Microsoft.AspNetCore.Identity.SignInManager<IdentityUser> _signInManager;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly Volo.Abp.Domain.Repositories.IRepository<IdentityRole> _identityRoleRepository;

        public AccountAppService(
            IdentityUserManager userManager,
            IOptionsSnapshot<JwtOptions> jwtOptions,
            Microsoft.AspNetCore.Identity.SignInManager<IdentityUser> signInManager,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            Volo.Abp.Domain.Repositories.IRepository<IdentityRole> identityRoleRepository)
        {
            _userManager = userManager;
            _jwtOptions = jwtOptions.Value;
            _signInManager = signInManager;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _identityRoleRepository = identityRoleRepository;
        }


        public async Task<LoginOutput> LoginAsync(LoginInput input)
        {
            var result = await _signInManager.PasswordSignInAsync(input.Name, input.Password, false, true);
            if (result.IsNotAllowed)
            {
                throw new BusinessException(AbpProDomainErrorCodes.UserLockedOut);
            }

            if (!result.Succeeded)
            {
                throw new BusinessException(AbpProDomainErrorCodes.UserOrPasswordMismatch);
            }

            var user = await _userManager.FindByNameAsync(input.Name);
            return await BuildResult(user);
        }

        /// <summary>
        /// identityServer4第三方登录
        /// </summary>
        /// <returns></returns>
        public async Task<LoginOutput> Id4LoginAsync(string accessToken)
        {
            // 通过access token 获取用户信息
            Dictionary<string, string> headers = new Dictionary<string, string>
                { { "Authorization", $"Bearer {accessToken}" } };
            var response = await _httpClientFactory.GetAsync<LoginStsResponse>(HttpClientNameConsts.Sts, "connect/userinfo", headers);
            var user = await _userManager.FindByNameAsync(response.name);
            if (!user.IsActive)
            {
                throw new BusinessException(AbpProDomainErrorCodes.UserLockedOut);
            }

            return await BuildResult(user);
        }

        /// <summary>
        /// github第三方登录
        /// </summary>
        /// <param name="code">授权码</param>
        /// <returns></returns>
        public async Task<LoginOutput> GithubLoginAsync(string code)
        {
            var headers = new Dictionary<string, string> { { "Accept", $"application/json" } };
            // 通过code获取access token
            var accessTokenUrl =
                $"login/oauth/access_token?client_id={_configuration.GetValue<string>("HttpClient:Github:ClientId")}&client_secret={_configuration.GetValue<string>("HttpClient:Github:ClientSecret")}&code={code}";
            var accessTokenResponse = await _httpClientFactory.GetAsync<GithubAccessTokenResponse>(HttpClientNameConsts.Github, accessTokenUrl, headers);

            // 获取github用户信息
            headers.Add("Authorization", $"token {accessTokenResponse.Access_token}");
            headers.Add("User-Agent", _configuration.GetValue<string>("HttpClient:GithubApi:ClientName"));
            var userResponse = await _httpClientFactory.GetAsync<LoginGithubResponse>(HttpClientNameConsts.GithubApi, "/user", headers);

            var user = await _userManager.FindByEmailAsync(userResponse.email);
            if (user != null) return await BuildResult(user);
            return await RegisterAsync(userResponse.name, userResponse.email);
        }

        #region 私有方法

        /// <summary>
        /// 注册用户
        /// </summary>
        /// <returns></returns>
        private async Task<LoginOutput> RegisterAsync(string userName, string email, string password = "1q2w3E*")
        {
            var result = new LoginOutput();
            var roles = await _identityRoleRepository.GetListAsync(e => e.IsDefault);
            if (roles == null || roles.Count == 0) throw new AbpAuthorizationException();
            var userId = GuidGenerator.Create();

            var user = new IdentityUser(userId, userName, email)
            {
                Name = userName
            };

            roles.ForEach(e => { user.AddRole(e.Id); });
            user.SetIsActive(true);
            await _userManager.CreateAsync(user, password);
            result.Token = GenerateJwt(user.Id, user.UserName, user.Name, user.Email,
                user.TenantId.ToString(), roles.Select(e => e.Name).ToList());
            result.Id = user.Id;
            result.UserName = userName;
            result.Name = userName;
            result.Roles = roles.Select(e => e.Name).ToList();
            return result;
        }


        private async Task<LoginOutput> BuildResult(IdentityUser user)
        {
            if (!user.IsActive) throw new BusinessException(AbpProDomainErrorCodes.UserLockedOut);
            var roles = await _userManager.GetRolesAsync(user);
            if (roles == null || roles.Count == 0) throw new AbpAuthorizationException();
            var token = GenerateJwt(user.Id, user.UserName, user.Name, user.Email,
                user.TenantId.ToString(), roles.ToList());
            var loginOutput = ObjectMapper.Map<IdentityUser, LoginOutput>(user);
            loginOutput.Token = token;
            loginOutput.Roles = roles.ToList();
            return loginOutput;
        }

        /// <summary>
        /// 生成jwt token
        /// </summary>
        /// <returns></returns>
        private string GenerateJwt(Guid userId, string userName, string name, string email,
            string tenantId, List<string> roles)
        {
            var dateNow = DateTime.Now;
            var expirationTime = dateNow + TimeSpan.FromHours(_jwtOptions.ExpirationTime);
            var key = Encoding.ASCII.GetBytes(_jwtOptions.SecurityKey);

            var claims = new List<Claim>
            {
                new Claim(JwtClaimTypes.Audience, _jwtOptions.Audience),
                new Claim(JwtClaimTypes.Issuer, _jwtOptions.Issuer),
                new Claim(AbpClaimTypes.UserId, userId.ToString()),
                new Claim(AbpClaimTypes.Name, name),
                new Claim(AbpClaimTypes.UserName, userName),
                new Claim(AbpClaimTypes.Email, email),
                new Claim(AbpClaimTypes.TenantId, tenantId)
            };

            foreach (var item in roles)
            {
                claims.Add(new Claim(JwtClaimTypes.Role, item));
            }

            var tokenDescriptor = new SecurityTokenDescriptor()
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expirationTime,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };
            var handler = new JwtSecurityTokenHandler();
            var token = handler.CreateToken(tokenDescriptor);
            return handler.WriteToken(token);
        }

        #endregion
    }
}