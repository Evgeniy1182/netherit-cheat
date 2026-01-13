using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NetheritInjector
{
    public class SupabaseApiKey
    {
        public string id { get; set; } = "";
        public string key_code { get; set; } = "";
        public string user_id { get; set; } = "";
        public DateTime? activated_at { get; set; }
        public DateTime? expires_at { get; set; }
        public bool is_active { get; set; }
        public int duration_days { get; set; }
        public int activation_count { get; set; }
        public int max_activations { get; set; }
    }

    public class SupabaseActivation
    {
        public string id { get; set; } = "";
        public string key_id { get; set; } = "";
        public DateTime expires_at { get; set; }
        public string hwid { get; set; } = "";
    }

    public class SupabaseInjectionLog
    {
        public string key_id { get; set; } = "";
        public string user_id { get; set; } = "";
        public string process_name { get; set; } = "";
        public int? process_id { get; set; }
        public string dll_path { get; set; } = "";
        public bool success { get; set; }
        public string? error_message { get; set; }
        public string hwid { get; set; } = "";
        public string ip_address { get; set; } = "";
    }

    public static class SupabaseService
    {
        private static readonly string SupabaseUrl = "https://boglgrhvknokfnhktsca.supabase.co";
        private static readonly string SupabaseKey = "sb_publishable_NamiWNXv6Fhw_Yo5ISWmbg_hV3kuMw-";
        private static readonly HttpClient client = new HttpClient();

        static SupabaseService()
        {
            client.DefaultRequestHeaders.Add("apikey", SupabaseKey);
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {SupabaseKey}");
        }

        // Get all active keys for a user
        public static async Task<List<SupabaseApiKey>> GetUserKeysAsync(string userId)
        {
            try
            {
                string url = $"{SupabaseUrl}/rest/v1/api_keys?user_id=eq.{userId}&is_active=eq.true&select=*";
                
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                
                string json = await response.Content.ReadAsStringAsync();
                var keys = JsonSerializer.Deserialize<List<SupabaseApiKey>>(json);
                
                return keys ?? new List<SupabaseApiKey>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching keys: {ex.Message}");
                return new List<SupabaseApiKey>();
            }
        }

        // Get specific key by code
        public static async Task<SupabaseApiKey?> GetKeyByCodeAsync(string keyCode)
        {
            try
            {
                string url = $"{SupabaseUrl}/rest/v1/api_keys?key_code=eq.{keyCode}&select=*";
                
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                
                string json = await response.Content.ReadAsStringAsync();
                var keys = JsonSerializer.Deserialize<List<SupabaseApiKey>>(json);
                
                return keys?.Count > 0 ? keys[0] : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching key: {ex.Message}");
                return null;
            }
        }

        // Activate a key for the user
        public static async Task<bool> ActivateKeyAsync(string keyCode, string userId, string hwid)
        {
            try
            {
                var key = await GetKeyByCodeAsync(keyCode);
                if (key == null)
                    return false;

                // Check if already activated
                if (key.activated_at.HasValue && key.activation_count >= key.max_activations)
                    return false;

                // Create activation record
                var activation = new
                {
                    key_id = key.id,
                    user_id = userId,
                    expires_at = DateTime.UtcNow.AddDays(key.duration_days),
                    hwid = hwid
                };

                string json = JsonSerializer.Serialize(activation);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                string url = $"{SupabaseUrl}/rest/v1/activations";
                var response = await client.PostAsync(url, content);
                
                if (response.IsSuccessStatusCode)
                {
                    // Update key record
                    await UpdateKeyActivationAsync(key.id, userId);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error activating key: {ex.Message}");
                return false;
            }
        }

        // Update key activation timestamp
        private static async Task<bool> UpdateKeyActivationAsync(string keyId, string userId)
        {
            try
            {
                var update = new
                {
                    activated_at = DateTime.UtcNow,
                    user_id = userId,
                    is_active = true
                };

                string json = JsonSerializer.Serialize(update);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                string url = $"{SupabaseUrl}/rest/v1/api_keys?id=eq.{keyId}";
                var request = new HttpRequestMessage(HttpMethod.Patch, url) { Content = content };
                
                var response = await client.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating key: {ex.Message}");
                return false;
            }
        }

        // Log injection attempt to Supabase
        public static async Task<bool> LogInjectionAsync(SupabaseInjectionLog log)
        {
            try
            {
                string json = JsonSerializer.Serialize(log);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                string url = $"{SupabaseUrl}/rest/v1/injections";
                var response = await client.PostAsync(url, content);
                
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error logging injection: {ex.Message}");
                return false;
            }
        }

        // Check if key is still valid on server
        public static async Task<bool> VerifyKeyAsync(string keyCode, string hwid)
        {
            try
            {
                var key = await GetKeyByCodeAsync(keyCode);
                
                if (key == null || !key.is_active)
                    return false;

                if (key.expires_at.HasValue && key.expires_at < DateTime.UtcNow)
                    return false;

                // Get activation record
                string url = $"{SupabaseUrl}/rest/v1/activations?key_id=eq.{key.id}&select=*";
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();
                var activations = JsonSerializer.Deserialize<List<SupabaseActivation>>(json);

                if (activations == null || activations.Count == 0)
                    return false;

                var activation = activations[0];
                
                // Check expiration and HWID
                if (activation.expires_at < DateTime.UtcNow)
                    return false;

                // Optional HWID check (uncomment if needed)
                // if (!string.IsNullOrEmpty(activation.hwid) && activation.hwid != hwid)
                //     return false;

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error verifying key: {ex.Message}");
                return false;
            }
        }

        // Get remaining time for key
        public static async Task<TimeSpan?> GetKeyTimeRemainingAsync(string keyCode)
        {
            try
            {
                var key = await GetKeyByCodeAsync(keyCode);
                
                if (key == null || !key.is_active || !key.expires_at.HasValue)
                    return null;

                TimeSpan remaining = key.expires_at.Value - DateTime.UtcNow;
                
                if (remaining.TotalSeconds > 0)
                    return remaining;
                
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting time remaining: {ex.Message}");
                return null;
            }
        }

        // Get system HWID (Windows)
        public static string GetSystemHWID()
        {
            try
            {
                using (var searcher = new System.Management.ManagementObjectSearcher("select UUID from Win32_ComputerSystemProduct"))
                {
                    foreach (System.Management.ManagementObject item in searcher.Get())
                    {
                        return item["UUID"].ToString();
                    }
                }
            }
            catch { }

            return System.Environment.MachineName;
        }
    }
}
