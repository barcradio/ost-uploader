using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security;
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

        public SecureCredentialStore(string filePath)
        {
            _filePath = filePath;
        }

        public bool TryGetValidToken(out APIAuthResponse auth, out string baseUrl)
        {
            auth = null;
            baseUrl = string.Empty;
            if (!TryReadSavedCredential(out var saved))
                return false;

            if (saved.Auth == null || string.IsNullOrWhiteSpace(saved.Auth.token) || string.IsNullOrWhiteSpace(saved.Auth.expiration) || string.IsNullOrWhiteSpace(saved.BaseUrl))
                return false;

            if (DateTime.TryParse(saved.Auth.expiration, out var exp) && exp.ToUniversalTime() > DateTime.UtcNow)
            {
                auth = saved.Auth;
                baseUrl = saved.BaseUrl;
                return true;
            }

            return false;
        }

        public bool TryGetSavedCredentials(string baseUrl, out string email, out SecureString password, out APIAuthResponse auth)
        {
            email = string.Empty;
            password = new SecureString();
            auth = new APIAuthResponse();
            var normalizedBaseUrl = NormalizeBaseUrl(baseUrl);

            if (string.IsNullOrWhiteSpace(normalizedBaseUrl) || !TryReadSavedCredential(GetCredentialFilePath(normalizedBaseUrl), out var saved) ||
                string.IsNullOrWhiteSpace(saved.Email) ||
                string.IsNullOrWhiteSpace(saved.Password) ||
                !string.Equals(NormalizeBaseUrl(saved.BaseUrl), normalizedBaseUrl, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            email = saved.Email;
            password = new NetworkCredential(string.Empty, saved.Password).SecurePassword;
            auth = saved.Auth ?? new APIAuthResponse();
            return true;
        }

        public bool SaveToken(APIAuthResponse resp, string baseUrl)
        {
            if (resp == null || string.IsNullOrWhiteSpace(resp.token) || string.IsNullOrWhiteSpace(resp.expiration) || string.IsNullOrWhiteSpace(baseUrl))
                return false;

            try
            {
                return SaveCredential(new SavedCredential { Auth = resp, BaseUrl = NormalizeBaseUrl(baseUrl) });
            }
            catch
            {
                return false;
            }
        }

        public bool SaveToken(APIAuthResponse resp, string baseUrl, string email, SecureString password)
        {
            if (resp == null || string.IsNullOrWhiteSpace(resp.token) || string.IsNullOrWhiteSpace(resp.expiration))
                return false;

            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(email) || password == null || password.Length == 0)
                return false;

            try
            {
                var normalizedBaseUrl = NormalizeBaseUrl(baseUrl);
                return SaveCredential(new SavedCredential
                {
                    Auth = resp,
                    BaseUrl = normalizedBaseUrl,
                    Email = email,
                    Password = new NetworkCredential(string.Empty, password).Password
                }, GetCredentialFilePath(normalizedBaseUrl));
            }
            catch
            {
                return false;
            }
        }

        private static string NormalizeBaseUrl(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
                return baseUrl;

            var authority = uri.IsDefaultPort ? uri.Authority.Replace(":80", string.Empty).Replace(":443", string.Empty) : uri.Authority;
            return $"{uri.Scheme}://{authority}";
        }

        private string GetCredentialFilePath(string baseUrl)
        {
            var directory = Path.GetDirectoryName(_filePath) ?? string.Empty;
            var name = Path.GetFileNameWithoutExtension(_filePath);
            var extension = Path.GetExtension(_filePath);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeBaseUrl(baseUrl).ToLowerInvariant())))[..16];
            return Path.Combine(directory, $"{name}-{hash}{extension}");
        }

        private bool TryReadSavedCredential(out SavedCredential saved)
        {
            return TryReadSavedCredential(_filePath, out saved);
        }

        private bool TryReadSavedCredential(string filePath, out SavedCredential saved)
        {
            saved = new SavedCredential();
            if (!File.Exists(filePath))
                return false;

            try
            {
                var encrypted = File.ReadAllBytes(filePath);
                var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                saved = JsonSerializer.Deserialize<SavedCredential>(Encoding.UTF8.GetString(decrypted)) ?? new SavedCredential();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool SaveCredential(SavedCredential saved, string filePath = null)
        {
            var json = JsonSerializer.Serialize(saved);
            var bytes = Encoding.UTF8.GetBytes(json);
            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(filePath ?? _filePath, encrypted);
            return true;
        }

        public void Clear()
        {
            try
            {
                if (File.Exists(_filePath))
                    File.Delete(_filePath);

                var directory = Path.GetDirectoryName(_filePath);
                var prefix = Path.GetFileNameWithoutExtension(_filePath) + "-";
                if (directory != null)
                {
                    foreach (var path in Directory.EnumerateFiles(directory, prefix + "*" + Path.GetExtension(_filePath)))
                        File.Delete(path);
                }
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
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
