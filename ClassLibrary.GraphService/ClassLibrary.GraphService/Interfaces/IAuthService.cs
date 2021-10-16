using System.Threading.Tasks;

namespace ClassLibrary.GraphService.Interfaces
{
    public interface IAuthService
    {
        Task<string> GetAccessTokenAsync(string resourceUrl, string appId = null);
    }
}
