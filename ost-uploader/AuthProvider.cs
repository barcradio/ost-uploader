using System;
using System.Net;
using System.Net.Http;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;

namespace ost_uploader
{
    public class AuthProvider
    {
        private readonly HttpClient _httpClient;
        private APIAuthResponse _authResponse;

        public AuthProvider()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            _authResponse = new APIAuthResponse();
        }

        public APIAuthResponse AuthResponse
        {
            get { return _authResponse; }
            set { _authResponse = value; }
        }

        /// Sign in using basic username and password.
        /// </summary>
        public async Task<APIAuthResponse> SignInBasicAsync(string email, SecureString password)
        {
            try
            {
                var payload = new UserAuthRequest
                {
                    User = new UserAuthRequest.UserCredentials
                    {
                        Email = email,
                        Password = new NetworkCredential("", password).Password
                    }
                };

                var jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var task = _httpClient.PostAsync("https://www.opensplittime.org/api/v1/auth", content);
                var response = task.GetAwaiter().GetResult();

                response.EnsureSuccessStatusCode();
                var responseContent = await response.Content.ReadAsStringAsync();

                jsonPayload = null;
                content = null;

                AuthResponse = JsonSerializer.Deserialize<APIAuthResponse>(responseContent) ?? new APIAuthResponse();
                return _authResponse;
            }
            catch (HttpRequestException ex)
            {
                if (ex.StatusCode.HasValue)
                    MessageBox.Show($"HTTP Error {(int)ex.StatusCode.Value}: {ex.Message}");
                else
                {
                    // Handle HTTP request errors
                    MessageBox.Show($"Request error: {ex.Message}");
                }
                return new APIAuthResponse();
            }
            catch (Exception ex)
            {
                // Handle other potential errors
                MessageBox.Show($"Unexpected error: {ex.Message}");
                return new APIAuthResponse();
            }
        }
    }

    public class APIAuthResponse
    {
        public string token { get; set; } = string.Empty;
        public string expiration { get; set; } = string.Empty;
    }

    public class UserAuthRequest
    {
        [JsonPropertyName("user")]
        public UserCredentials User { get; set; } = new UserCredentials();

        public class UserCredentials
        {
            [JsonPropertyName("email")]
            public string Email { get; set; } = string.Empty;

            [JsonPropertyName("password")]
            public string Password { get; set; } = string.Empty;
        }
    }
}