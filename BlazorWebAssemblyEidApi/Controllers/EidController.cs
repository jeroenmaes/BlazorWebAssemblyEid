
using BlazorWebAssemblyEidShared;
using IdentityModel.Client;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BlazorWebAssemblyEidApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class EidController : ControllerBase
    {                
        private static Dictionary<string, ClientRegistrationResponse> clientRegistrationStore = new Dictionary<string, ClientRegistrationResponse>();

        private readonly ILogger<EidController> _logger;
        private readonly string _baseUrl;

        public EidController(ILogger<EidController> logger)
        {
            _logger = logger;
            _baseUrl = "https://www.e-contract.be/eid-idp/oidc/ident";
        }

        [HttpGet]
        [Route("RegisterClient")]
        public async Task<ClientRegistrationResponse> RegisterClient(string state, string redirectUrl)
        {
            var clientInfo = await InternalRegisterClient(redirectUrl);
            
            clientRegistrationStore.Add(state, clientInfo);

            return clientInfo;
        }

        private async Task<ClientRegistrationResponse> InternalRegisterClient(string redirectUrl)
        {
            var client = new HttpClient();

            var response = await client.RegisterClientAsync(new DynamicClientRegistrationRequest
            {
                Address = _baseUrl + "/register",
                Document = new DynamicClientRegistrationDocument
                {
                    RedirectUris = { new Uri(redirectUrl) }
                }
            });
                        
            return new ClientRegistrationResponse { ClientId = response.ClientId, ClientSecret = response.ClientSecret };
        }

        [HttpGet]
        [Route("GetUserInfo")]
        public async Task<UserResponse> GetUserInfo(string state, string returnCode, string redirectUrl)
        {
            var clientInfo = clientRegistrationStore.GetValueOrDefault(state);

            if(clientInfo != null)
            {
                var token = await GetToken(returnCode, redirectUrl, clientInfo.ClientId, clientInfo.ClientSecret);

                clientRegistrationStore.Remove(state);
                return await InternalGetUserInfo(token);
            }

            return new UserResponse { Error = "Invalid State" };
        }

        private async Task<UserResponse> InternalGetUserInfo(string token)
        {
            var client = new HttpClient();

            UserInfoResponse response2 = await client.GetUserInfoAsync(new UserInfoRequest
            {
                Address = _baseUrl + "/userinfo",
                Token = token
            });

            var claims = new List<UserClaim>();

            foreach (var item in response2.Claims)
            {
                claims.Add(new UserClaim { Type = item.Type, Value = item.Value });
            }

            return new UserResponse { Claims = claims };
        }

        private async Task<string> GetToken(string returnCode, string redirectUrl, string ClientId, string ClientSecret)
        {
            var client = new HttpClient();
            var response = await client.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
            {
                Address = _baseUrl + "/token",
                ClientId = ClientId,
                ClientSecret = ClientSecret,
                Code = returnCode,
                RedirectUri = redirectUrl,
                GrantType = "authorization_code"                
            });

            return response.AccessToken;
        }
    }
}