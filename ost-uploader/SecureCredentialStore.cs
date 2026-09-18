using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ost_uploader
{
    // Securely stores and retrieves the API authentication token and expiration.
    public class SecureCredentialStore
    {
        private readonly string _filePath;

        public SecureCredentialStore()
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(local, "ost-uploader");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            _filePath = Path.Combine(dir, "credentials.bin");
        }

        public bool TryGetValidToken(out APIAuthResponse auth, out string baseUrl)
        {
            auth = null;
            baseUrl = string.Empty;
            if (!File.Exists(_filePath))
                return false;

            try
            {
                var encrypted = File.ReadAllBytes(_filePath);
                var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                var json = Encoding.UTF8.GetString(decrypted);
                var saved = JsonSerializer.Deserialize<SavedCredential>(json);
                if (saved == null || saved.Auth == null || string.IsNullOrWhiteSpace(saved.Auth.token) || string.IsNullOrWhiteSpace(saved.Auth.expiration) || string.IsNullOrWhiteSpace(saved.BaseUrl))
                    return false;

                if (DateTime.TryParse(saved.Auth.expiration, out var exp))
                {
                    // Compare in UTC to avoid timezone issues
                    if (exp.ToUniversalTime() > DateTime.UtcNow)
                    {
                        auth = saved.Auth;
                        baseUrl = saved.BaseUrl;
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                // If anything goes wrong, don't throw — treat as no valid token.
                return false;
            }
        }

        public bool SaveToken(APIAuthResponse resp, string baseUrl)
        {
            if (resp == null || string.IsNullOrWhiteSpace(resp.token) || string.IsNullOrWhiteSpace(resp.expiration))
                return false;

            if (string.IsNullOrWhiteSpace(baseUrl))
                return false;

            try
            {
                var saved = new SavedCredential { Auth = resp, BaseUrl = baseUrl };
                var json = JsonSerializer.Serialize(saved);
                var bytes = Encoding.UTF8.GetBytes(json);
                var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(_filePath, encrypted);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Clear()
        {
            try
            {
                if (File.Exists(_filePath))
                    File.Delete(_filePath);
            }
            catch
            {
                // ignore
            }
        }
    }

    // Wrapper for serialized saved credentials
    internal class SavedCredential
    {
        public APIAuthResponse Auth { get; set; } = new APIAuthResponse();
        public string BaseUrl { get; set; } = string.Empty;
    }
}
