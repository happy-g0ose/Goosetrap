using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Goosetrap.Utility
{
    public static class AccountsHelper
    {
        public static string Encrypt(string text)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(text);
                byte[] encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encrypted);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("AccountsHelper::Encrypt", "Encryption failed: " + ex.Message);
                return "";
            }
        }

        public static string Decrypt(string cipherText)
        {
            try
            {
                byte[] data = Convert.FromBase64String(cipherText);
                byte[] decrypted = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("AccountsHelper::Decrypt", "Decryption failed: " + ex.Message);
                return "";
            }
        }

        public static async Task<(bool success, string username, string displayName, long userId)> ValidateCookieAsync(string cookie)
        {
            const string LOG_IDENT = "AccountsHelper::ValidateCookie";
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "https://users.roblox.com/v1/users/authenticated");
                request.Headers.Add("Cookie", ".ROBLOSECURITY=" + cookie);
                
                using var response = await App.HttpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    string body = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;
                    
                    long id = root.GetProperty("id").GetInt64();
                    string name = root.GetProperty("name").GetString() ?? "";
                    string displayName = root.GetProperty("displayName").GetString() ?? "";
                    
                    return (true, name, displayName, id);
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
            }
            return (false, "", "", 0);
        }

        public static async Task<string> FetchAvatarUriAsync(long userId)
        {
            const string LOG_IDENT = "AccountsHelper::FetchAvatarUri";
            try
            {
                string url = $"https://thumbnails.roblox.com/v1/users/avatar-headshot?userIds={userId}&size=150x150&format=Png&isCircular=true";
                string body = await App.HttpClient.GetStringAsync(url);
                
                using var doc = JsonDocument.Parse(body);
                var dataArray = doc.RootElement.GetProperty("data");
                if (dataArray.GetArrayLength() > 0)
                {
                    return dataArray[0].GetProperty("imageUrl").GetString() ?? "";
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
            }
            // Fallback default avatar
            return "pack://application:,,,/Goosetrap.png";
        }

        public static async Task<string> GetLaunchTicketAsync(string cookie)
        {
            const string LOG_IDENT = "AccountsHelper::GetLaunchTicket";
            try
            {
                // Roblox needs X-CSRF-TOKEN. First, do a request to get the token.
                using var client = new HttpClient(new HttpClientHandler 
                { 
                    UseCookies = false,
                    SslProtocols = System.Security.Authentication.SslProtocols.Tls12,
                    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
                });
                client.Timeout = TimeSpan.FromSeconds(15);
                client.DefaultRequestHeaders.Add("Cookie", ".ROBLOSECURITY=" + cookie);
                client.DefaultRequestHeaders.Add("User-Agent", "Roblox/WinInet");

                // Get CSRF Token
                string csrfToken = "";
                using (var dummyRequest = new HttpRequestMessage(HttpMethod.Post, "https://128.116.5.3/v1/authentication-ticket"))
                {
                    dummyRequest.Headers.Host = "auth.roblox.com";
                    dummyRequest.Content = new StringContent("{}", Encoding.UTF8, "application/json");
                    using var response = await client.SendAsync(dummyRequest);
                    if (response.Headers.Contains("x-csrf-token"))
                    {
                        csrfToken = response.Headers.GetValues("x-csrf-token").FirstOrDefault() ?? "";
                    }
                    else
                    {
                        string body = await response.Content.ReadAsStringAsync();
                        App.Logger.WriteLine(LOG_IDENT, $"First CSRF attempt status: {response.StatusCode}, Body: {body}");
                    }
                }

                if (string.IsNullOrEmpty(csrfToken))
                {
                    // Try auth.roblox.com root
                    using var dummyRequest = new HttpRequestMessage(HttpMethod.Post, "https://128.116.5.3/");
                    dummyRequest.Headers.Host = "auth.roblox.com";
                    dummyRequest.Content = new StringContent("{}", Encoding.UTF8, "application/json");
                    using var response = await client.SendAsync(dummyRequest);
                    if (response.Headers.Contains("x-csrf-token"))
                    {
                        csrfToken = response.Headers.GetValues("x-csrf-token").FirstOrDefault() ?? "";
                    }
                    else
                    {
                        string body = await response.Content.ReadAsStringAsync();
                        App.Logger.WriteLine(LOG_IDENT, $"Second CSRF attempt status: {response.StatusCode}, Body: {body}");
                    }
                }

                if (string.IsNullOrEmpty(csrfToken))
                {
                    App.Logger.WriteLine(LOG_IDENT, "Failed to retrieve CSRF token");
                    return "";
                }

                App.Logger.WriteLine(LOG_IDENT, $"Retrieved CSRF token: {csrfToken}");

                // Now make the actual request to get the ticket
                using (var request = new HttpRequestMessage(HttpMethod.Post, "https://128.116.5.3/v1/authentication-ticket"))
                {
                    request.Headers.Host = "auth.roblox.com";
                    request.Headers.Add("x-csrf-token", csrfToken);
                    request.Headers.Add("Referer", "https://www.roblox.com/");
                    request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
                    
                    using var response = await client.SendAsync(request);
                    if (response.Headers.Contains("rbx-authentication-ticket"))
                    {
                        string ticket = response.Headers.GetValues("rbx-authentication-ticket").FirstOrDefault() ?? "";
                        App.Logger.WriteLine(LOG_IDENT, "Successfully retrieved launch ticket");
                        return ticket;
                    }
                    else
                    {
                        string body = await response.Content.ReadAsStringAsync();
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to get ticket. Status: {response.StatusCode}, Body: {body}");
                        if (response.Headers.Contains("x-csrf-token"))
                        {
                            App.Logger.WriteLine(LOG_IDENT, $"Returned updated x-csrf-token: {response.Headers.GetValues("x-csrf-token").FirstOrDefault()}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
            }
            return "";
        }
        /// <summary>
        /// Записывает .ROBLOSECURITY cookie в локальное хранилище Roblox Desktop App
        /// чтобы при запуске через Аккаунты пользователь был автоматически авторизован.
        /// </summary>
        public static void SetRobloxCookie(string cookie)
        {
            const string LOG_IDENT = "AccountsHelper::SetRobloxCookie";
            try
            {
                string localStoragePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Roblox", "LocalStorage");

                if (!Directory.Exists(localStoragePath))
                    Directory.CreateDirectory(localStoragePath);

                string cookieFilePath = Path.Combine(localStoragePath, "RobloxCookies.dat");

                // Remove ReadOnly attribute if it exists so we can overwrite
                if (File.Exists(cookieFilePath))
                {
                    var attributes = File.GetAttributes(cookieFilePath);
                    if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    {
                        File.SetAttributes(cookieFilePath, attributes & ~FileAttributes.ReadOnly);
                    }
                }

                // Формат Netscape cookie file, такой же как использует Roblox
                var sb = new StringBuilder();
                sb.Append($"#HttpOnly_.roblox.com\tTRUE\t/\tTRUE\t0\t.ROBLOSECURITY\t{cookie}; ");

                string plainText = sb.ToString();
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
                string base64 = Convert.ToBase64String(encryptedBytes);

                string jsonContent = JsonSerializer.Serialize(new
                {
                    CookiesVersion = "1",
                    CookiesData = base64
                });

                File.WriteAllText(cookieFilePath, jsonContent);

                // Restore ReadOnly attribute as Roblox does
                File.SetAttributes(cookieFilePath, File.GetAttributes(cookieFilePath) | FileAttributes.ReadOnly);

                App.Logger.WriteLine(LOG_IDENT, "Successfully wrote cookie to RobloxCookies.dat");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }
    }
}
