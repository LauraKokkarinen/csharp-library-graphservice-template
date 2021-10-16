using System.Threading.Tasks;
using Microsoft.Azure.Services.AppAuthentication;
using ClassLibrary.GraphService.Interfaces;

namespace ClassLibrary.GraphService
{
    public class AuthService : IAuthService
    {
        /// <summary>
        /// Gets an access token using the managed identity of the Azure resource.
        /// </summary>
        /// <param name="resourceUrl">The URL of the API, e.g., https://graph.microsoft.com</param>
        /// <param name="appId">Optional user assigned managed identity client ID. Defaults to system assigned identity.</param>
        /// <returns>Returns the Bearer access token value</returns>
        public async Task<string> GetAccessTokenAsync(string resourceUrl, string appId = null)
        {
            string connectionString = appId != null ? $"RunAs=App;AppId={appId}" : null;
            var tokenProvider = new AzureServiceTokenProvider(connectionString);
            return await tokenProvider.GetAccessTokenAsync(resourceUrl);
        }
    }
}
