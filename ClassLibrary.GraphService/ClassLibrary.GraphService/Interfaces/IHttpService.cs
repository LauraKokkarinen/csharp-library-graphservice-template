using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using ClassLibrary.GraphService.Enums;

namespace ClassLibrary.GraphService.Interfaces
{
    public interface IHttpService
    {
        Task<JToken> GetResponseAsync(string url, Method method, HttpRequestHeaders headers = null, string body = null, string contentType = null);
    }
}
