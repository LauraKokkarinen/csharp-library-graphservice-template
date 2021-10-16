using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace ClassLibrary.GraphService.Interfaces
{
    public interface IGraphService
    {
        void Initialize(string debugAccessToken = null);
        Task<IEnumerable<JToken>> Get();
        Task<IEnumerable<JToken>> Batch(string[] urls, string method, JObject commonBody = null);
    }
}
