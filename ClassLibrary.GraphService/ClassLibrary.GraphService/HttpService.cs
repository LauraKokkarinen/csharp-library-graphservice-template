using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ClassLibrary.GraphService.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ClassLibrary.GraphService
{
    public class HttpService
    {
        private readonly HttpClient _httpClient;

        public HttpService()
        {
            _httpClient = new HttpClient();
        }

        public async Task<JToken> GetResponseAsync(string url, Method method, HttpRequestHeaders headers = null, string body = null, string contentType = null)
        {
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    try
                    {
                        if (_httpClient.DefaultRequestHeaders.Contains(header.Key))
                        {
                            _httpClient.DefaultRequestHeaders.Remove(header.Key);
                        }
                        _httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                    }
                    catch (Exception)
                    {
                        // The instance is being used by async requests in parallel
                    }
                }
            }

            var content = body != null ? new StringContent(body, Encoding.UTF8, contentType ?? "application/json") : null;

            HttpResponseMessage response;

            switch (method)
            {
                case Method.Get:
                    response = _httpClient.GetAsync(url).Result;
                    break;
                case Method.Post:
                    response = _httpClient.PostAsync(url, content).Result;
                    break;
                case Method.Put:
                    response = _httpClient.PutAsync(url, content).Result;
                    break;
                case Method.Patch:
                    response = _httpClient.PatchAsync(url, content).Result;
                    break;
                case Method.Delete:
                    response = _httpClient.DeleteAsync(url).Result;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(method), method, null);
            }

            if (response != null)
            {
                var status = (int)response.StatusCode;
                var responseBody = await ReadResponseBody(response);

                bool batchFailed = status == 200 && ContentIsJson(response) && BatchFailed(responseBody);

                bool throttled = status == 429 || batchFailed;

                if (throttled || status == 502 || status == 504)
                {
                    if (throttled)
                    {
                        var timeSpan = batchFailed ? GetBatchRetryAfter(responseBody) : response.Headers.RetryAfter.Delta?.Seconds;
                        int milliseconds = (timeSpan ?? 5) * 1000;
                        Thread.Sleep(milliseconds);
                    }

                    return await GetResponseAsync(url, method, headers, body, contentType); //retry
                }

                if (status != 202 && status != 204)
                {
                    return responseBody;
                }
            }

            return JToken.Parse(JsonConvert.SerializeObject(response));
        }

        private static bool ContentIsJson(HttpResponseMessage response)
        {
            return response.Content.Headers.ContentType.MediaType == "application/json";
        }

        private static bool BatchFailed(JToken responseBody)
        {
            var throttledResponses = GetThrottledResponses(responseBody);

            return throttledResponses.Any();
        }

        private static int GetBatchRetryAfter(JToken responseBody)
        {
            var throttledResponses = GetThrottledResponses(responseBody).ToList();

            int retryAfter = throttledResponses.Any() ? throttledResponses.Max(response => int.Parse(response["headers"]["Retry-After"].ToString())) : 0;

            return retryAfter > 0 ? retryAfter : 5000; // Unfortunately, not all Graph operations return a Retry-After header when throttling requests. In such a case, use an arbitrary value (5 seconds).
        }

        private static IEnumerable<JToken> GetThrottledResponses(JToken responseBody)
        {
            if (responseBody.First.Path == "responses") // Is a Graph batch
            {
                try
                {
                    return responseBody.First.First.Where(response => response["status"] != null && int.TryParse(response["status"].ToString(), out int status) && status == 429);
                }
                catch (Exception)
                {
                    // Not a Graph batch after all?
                }
            }

            return new List<JToken>();
        }

        private static async Task<JToken> ReadResponseBody(HttpResponseMessage response)
        {
            string content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    return JToken.Parse(content);
                }
                catch (Exception) // Response content is not in JSON format
                {
                    return content;
                }
            }

            throw new Exception(content);
        }
    }
}
}
