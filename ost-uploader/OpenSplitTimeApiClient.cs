using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace ost_uploader
{
    public class OpenSplitTimeApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _apiKey;

        public OpenSplitTimeApiClient(string baseUrl, string apiKey)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _apiKey = apiKey;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }
        }
        public void Dispose()
        {
            _httpClient.Dispose();
        }

        /// <summary>
        /// Posts JSON data to the OpenSplitTime.org API.
        /// </summary>
        /// <param name="endpoint">API endpoint, e.g. "/api/v1/raw_times/batch_create"</param>
        /// <param name="jsonPayload">JSON string to send</param>
        /// <returns>Response string from the API</returns>
        public async Task<string> PostJsonAsync(string endpoint, string jsonPayload)
        {
            var url = $"{_baseUrl}{endpoint}";
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        /// </summary>
        /// <param name="endpoint">API endpoint, e.g. "/api/v1/event_groups/:id/import"</param>
        /// <param name="content">HttpContent to send in the request body</param>
        /// <returns>Response string from the API</returns>
        public async Task<string> PostAsync(string endpoint, HttpContent content)
        {
            var url = $"{_baseUrl}{endpoint}";
            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// <summary>
        /// Sends an HTTP GET request to the specified endpoint.
        /// </summary>
        /// <param name="endpoint">API endpoint, e.g. "/api/v1/events"</param>
        /// <returns>Response string from the API</returns>
        public async Task<string> GetAsync(string endpoint)
        {
            var url = $"{_baseUrl}{endpoint}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }
}