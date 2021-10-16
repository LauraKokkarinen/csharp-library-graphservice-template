using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using ClassLibrary.GraphService.Enums;
using ClassLibrary.GraphService.Interfaces;
using Newtonsoft.Json;

namespace ClassLibrary.GraphService
{
    public class GraphService
    {
        private readonly IAuthService _authService;
        private readonly IHttpService _httpService;
        private static string _debugAccessToken;

        public GraphService(IAuthService authService, IHttpService httpService)
        {
            _authService = authService;
            _httpService = httpService;
        }

        public void Initialize(string debugAccessToken = null)
        {
            _debugAccessToken = debugAccessToken;
        }

        private async Task<HttpRequestHeaders> GetHeaders()
        {
            var headers = new HttpRequestMessage().Headers;
            string accessToken = Debugger.IsAttached && _debugAccessToken != null ? _debugAccessToken : await _authService.GetAccessTokenAsync("https://graph.microsoft.com/");
            headers.TryAddWithoutValidation("Authorization", $"Bearer {accessToken}");
            return headers;
        }

        public async Task<IEnumerable<JToken>> Get(string url)
        {
            return await Page(url, new List<JToken>()); //Always use paging just in case.
        }

        private async Task<IEnumerable<JToken>> Page(string url, IEnumerable<JToken> value)
        {
            var response = await _httpService.GetResponseAsync(url, Method.Get, await GetHeaders());

            if (response["value"] == null) return response; // The result is a single object, no need for paging.

            value = value.Concat(response["value"]);

            if (response["@odata.nextLink"] != null)
            {
                return await Page(response["@odata.nextLink"].ToString(), value);
            }

            return value;
        }

        public async Task<IEnumerable<JToken>> Batch(string[] urls, string method, JObject commonBody = null)
        {
            var result = new List<JToken>();

            var batch = new GraphBatch();
            var id = 0;

            foreach (string url in urls)
            {
                id += 1;
                var request = new GraphBatchRequest(id.ToString(), method, url, commonBody);
                batch.Requests.Add(request);

                if (id == 20 || url == urls.Last())
                {
                    string batchBody = JsonConvert.SerializeObject(batch);
                    var responses = (await _httpService.GetResponseAsync($"https://graph.microsoft.com/v1.0/$batch", Method.Post, await GetHeaders(), batchBody))["responses"];
                    result.AddRange(responses.Select(response => response["body"]).Where(responseBody => responseBody != null));
                    batch = new GraphBatch();
                    id = 0;
                }
            }

            return result;
        }

        /// <summary>
        /// Example method for using Batch.
        /// </summary>
        /// <param name="groupIds">The IDs of the groups which properties to get.</param>
        /// <returns>Group properties.</returns>
        public async Task<IEnumerable<JToken>> GetGroups(string[] groupIds)
        {
            string[] urls = groupIds.Select(groupId => $"/groups?$filter=id eq '{groupId}'&$expand=owners&$select=id,displayName,owners").ToArray();
            var responses = await Batch(urls, "GET");
            var groups = responses.Select(response => response["value"].FirstOrDefault()).ToArray();
            return groups;
        }
    }

    public class GraphBatch
    {
        public List<GraphBatchRequest> Requests;

        public GraphBatch()
        {
            Requests = new List<GraphBatchRequest>();
        }
    }

    public class GraphBatchRequest
    {
        public string Id;
        public string Method;
        public string Url;
        public JObject Body;
        public JObject Headers;

        public GraphBatchRequest(string id, string method, string url, JObject body = null)
        {
            Id = id;
            Method = method;
            Url = url;
            Body = body;
            Headers = new JObject { { "Content-Type", "application/json" } };
        }
    }
}
