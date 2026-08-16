using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace imgsaver
{
    public enum LockTriggerMode
    {
        AlwaysOnStartup = 0,    // Every time app is opened/started, require password
        ManualOrRestart = 1     // Session-based: only manual lock (Ctrl+L) or reset when Windows restarts
    }

    public enum LockAuthType
    {
        Pattern = 0,    // 9-dot pattern
        Password = 1    // text password
    }

    public static class SecurityManager
    {
        private const int SaltSize = 32; // 256-bit salt
        private const int KeySize = 64;  // 512-bit derived hash
        private const int Iterations = 100000;
        private const string VaultFileName = "security_vault.dat";
        private const string SessionAuthFileName = "session_auth.dat";

        private static readonly object _lock = new object();

        private class SecurityVaultPayload
        {
            public string SaltBase64 { get; set; } = "";
            public string HashBase64 { get; set; } = "";
            public string PatternSaltBase64 { get; set; } = "";
            public string PatternHashBase64 { get; set; } = "";
            public int Iterations { get; set; } = 100000;
            public int LockMode { get; set; } = (int)LockTriggerMode.AlwaysOnStartup;
            public int PreferredAuthType { get; set; } = (int)LockAuthType.Pattern;
            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
            public DateTime LastUnlockedAt { get; set; } = DateTime.UtcNow;
        }

        private class SessionAuthPayload
        {
            public long BootTimeSeconds { get; set; }
            public int SessionId { get; set; }
            public bool IsRevoked { get; set; } = false;
            public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;
        }

        public static bool IsPasswordConfigured()
        {
            lock (_lock)
            {
                string filePath = DataPathManager.GetSettingsFilePath(VaultFileName);
                if (!File.Exists(filePath)) return false;

                try
                {
                    var payload = LoadVaultPayload(filePath);
                    return payload != null && !string.IsNullOrEmpty(payload.HashBase64) && !string.IsNullOrEmpty(payload.SaltBase64);
                }
                catch
                {
                    return false;
                }
            }
        }

        public static bool IsPatternConfigured()
        {
            lock (_lock)
            {
                string filePath = DataPathManager.GetSettingsFilePath(VaultFileName);
                if (!File.Exists(filePath)) return false;

                try
                {
                    var payload = LoadVaultPayload(filePath);
                    return payload != null && !string.IsNullOrEmpty(payload.PatternHashBase64) && !string.IsNullOrEmpty(payload.PatternSaltBase64);
                }
                catch
                {
                    return false;
                }
            }
        }

        public static bool IsAnyAuthConfigured()
        {
            return IsPasswordConfigured() || IsPatternConfigured();
        }

        public static LockAuthType GetPreferredAuthType()
        {
            lock (_lock)
            {
                string filePath = DataPathManager.GetSettingsFilePath(VaultFileName);
                if (!File.Exists(filePath)) return LockAuthType.Pattern;

                try
                {
                    var payload = LoadVaultPayload(filePath);
                    if (payload == null) return LockAuthType.Pattern;
                    return (LockAuthType)payload.PreferredAuthType;
                }
                catch
                {
                    return LockAuthType.Pattern;
                }
            }
        }

        public static void SetPreferredAuthType(LockAuthType authType)
        {
            lock (_lock)
            {
                string filePath = DataPathManager.GetSettingsFilePath(VaultFileName);
                if (!File.Exists(filePath)) return;

                try
                {
                    var payload = LoadVaultPayload(filePath);
                    if (payload != null)
                    {
                        payload.PreferredAuthType = (int)authType;
                        SaveVaultPayload(payload);
                    }
                }
                catch { }
            }
        }

        public static LockTriggerMode GetLockMode()
        {
            lock (_lock)
            {
                string filePath = DataPathManager.GetSettingsFilePath(VaultFileName);
                if (!File.Exists(filePath)) return LockTriggerMode.AlwaysOnStartup;

                try
                {
                    var payload = LoadVaultPayload(filePath);
                    if (payload == null) return LockTriggerMode.AlwaysOnStartup;
                    return (LockTriggerMode)payload.LockMode;
                }
                catch
                {
                    return LockTriggerMode.AlwaysOnStartup;
                }
            }
        }

        public static void SetLockMode(LockTriggerMode mode)
        {
            lock (_lock)
            {
                string filePath = DataPathManager.GetSettingsFilePath(VaultFileName);
                if (!File.Exists(filePath)) return;

                try
                {
                    var payload = LoadVaultPayload(filePath);
                    if (payload != null)
                    {
                        payload.LockMode = (int)mode;
                        SaveVaultPayload(payload);
                    }
                }
                catch { }
            }
        }

        public static bool ShouldPromptPasswordOnStartup()
        {
            lock (_lock)
            {
                if (!IsAnyAuthConfigured()) return false;

                var mode = GetLockMode();
                if (mode == LockTriggerMode.AlwaysOnStartup)
                {
                    return true;
                }

                // ManualOrRestart mode: check if current Windows session is already unlocked
                string sessionFilePath = DataPathManager.GetSettingsFilePath(SessionAuthFileName);
                if (!File.Exists(sessionFilePath))
                {
                    return true;
                }

                try
                {
                    var session = LoadSessionPayload(sessionFilePath);
                    if (session == null || session.IsRevoked)
                    {
                        return true;
                    }

                    // Check if system restarted
                    long currentBoot = GetCurrentBootTimestamp();
                    if (Math.Abs(currentBoot - session.BootTimeSeconds) > 15)
                    {
                        // OS was rebooted
                        return true;
                    }

                    // Check Windows user session id
                    int currentSessionId = System.Diagnostics.Process.GetCurrentProcess().SessionId;
                    if (session.SessionId != currentSessionId)
                    {
                        return true;
                    }

                    return false; // Valid session; skip startup prompt
                }
                catch
                {
                    return true;
                }
            }
        }

        public static void RecordSuccessfulUnlock()
        {
            lock (_lock)
            {
                try
                {
                    var session = new SessionAuthPayload
                    {
                        BootTimeSeconds = GetCurrentBootTimestamp(),
                        SessionId = System.Diagnostics.Process.GetCurrentProcess().SessionId,
                        IsRevoked = false,
                        UnlockedAt = DateTime.UtcNow
                    };
                    SaveSessionPayload(session);
                }
                catch { }
            }
        }

        public static void RevokeSession()
        {
            lock (_lock)
            {
                try
                {
                    string sessionFilePath = DataPathManager.GetSettingsFilePath(SessionAuthFileName);
                    if (File.Exists(sessionFilePath))
                    {
                        var session = LoadSessionPayload(sessionFilePath);
                        if (session != null)
                        {
                            session.IsRevoked = true;
                            SaveSessionPayload(session);
                        }
                    }
                }
                catch { }
            }
        }

        public static bool SetMasterPattern(string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return false;

            lock (_lock)
            {
                try
                {
                    byte[] salt = new byte[SaltSize];
                    using (var rng = RandomNumberGenerator.Create())
                    {
                        rng.GetBytes(salt);
                    }

                    byte[] hash = HashPassword(pattern, salt, Iterations);

                    SecurityVaultPayload payload = new SecurityVaultPayload();
                    string filePath = DataPathManager.GetSettingsFilePath(VaultFileName);
                    if (File.Exists(filePath))
                    {
                        var existing = LoadVaultPayload(filePath);
                        if (existing != null) payload = existing;
                    }

                    payload.PatternSaltBase64 = Convert.ToBase64String(salt);
                    payload.PatternHashBase64 = Convert.ToBase64String(hash);
                    payload.Iterations = Iterations;
                    payload.LastUnlockedAt = DateTime.UtcNow;

                    SaveVaultPayload(payload);
                    RecordSuccessfulUnlock();
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public static bool ValidatePattern(string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return false;

            lock (_lock)
            {
                try
                {
                    string filePath = DataPathManager.GetSettingsFilePath(VaultFileName);
                    if (!File.Exists(filePath)) return false;

                    var payload = LoadVaultPayload(filePath);
                    if (payload == null || string.IsNullOrEmpty(payload.PatternHashBase64) || string.IsNullOrEmpty(payload.PatternSaltBase64))
                        return false;

                    byte[] salt = Convert.FromBase64String(payload.PatternSaltBase64);
                    byte[] expectedHash = Convert.FromBase64String(payload.PatternHashBase64);
                    int iterations = payload.Iterations > 0 ? payload.Iterations : Iterations;

                    byte[] actualHash = HashPassword(pattern, salt, iterations);

                    bool isValid = CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
                    if (isValid)
                    {
                        payload.LastUnlockedAt = DateTime.UtcNow;
                        SaveVaultPayload(payload);
                        RecordSuccessfulUnlock();
                    }
                    return isValid;
                }
                catch
                {
                    return false;
                }
            }
        }

        public static bool ChangeMasterPattern(string currentPattern, string newPattern)
        {
            if (!ValidatePattern(currentPattern)) return false;
            return SetMasterPattern(newPattern);
        }

        public static bool SetMasterPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return false;

            lock (_lock)
            {
                try
                {
                    byte[] salt = new byte[SaltSize];
                    using (var rng = RandomNumberGenerator.Create())
                    {
                        rng.GetBytes(salt);
                    }

                    byte[] hash = HashPassword(password, salt, Iterations);

                    SecurityVaultPayload payload = new SecurityVaultPayload();
                    string filePath = DataPathManager.GetSettingsFilePath(VaultFileName);
                    if (File.Exists(filePath))
                    {
                        var existing = LoadVaultPayload(filePath);
                        if (existing != null) payload = existing;
                    }

                    payload.SaltBase64 = Convert.ToBase64String(salt);
                    payload.HashBase64 = Convert.ToBase64String(hash);
                    payload.Iterations = Iterations;
                    payload.LastUnlockedAt = DateTime.UtcNow;

                    SaveVaultPayload(payload);
                    RecordSuccessfulUnlock();
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public static bool ValidatePassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return false;

            lock (_lock)
            {
                try
                {
                    string filePath = DataPathManager.GetSettingsFilePath(VaultFileName);
                    if (!File.Exists(filePath)) return false;

                    var payload = LoadVaultPayload(filePath);
                    if (payload == null || string.IsNullOrEmpty(payload.HashBase64) || string.IsNullOrEmpty(payload.SaltBase64))
                        return false;

                    byte[] salt = Convert.FromBase64String(payload.SaltBase64);
                    byte[] expectedHash = Convert.FromBase64String(payload.HashBase64);
                    int iterations = payload.Iterations > 0 ? payload.Iterations : Iterations;

                    byte[] actualHash = HashPassword(password, salt, iterations);

                    // Cryptographic constant-time comparison to prevent timing attacks
                    bool isValid = CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
                    if (isValid)
                    {
                        payload.LastUnlockedAt = DateTime.UtcNow;
                        SaveVaultPayload(payload);
                        RecordSuccessfulUnlock();
                    }
                    return isValid;
                }
                catch
                {
                    return false;
                }
            }
        }

        public static bool ChangeMasterPassword(string currentPassword, string newPassword)
        {
            if (!ValidatePassword(currentPassword)) return false;
            return SetMasterPassword(newPassword);
        }

        private static long GetCurrentBootTimestamp()
        {
            DateTime bootTime = DateTime.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64);
            return (long)bootTime.Subtract(DateTime.UnixEpoch).TotalSeconds;
        }

        private static byte[] HashPassword(string password, byte[] salt, int iterations)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA512);
            return pbkdf2.GetBytes(KeySize);
        }

        private static SecurityVaultPayload? LoadVaultPayload(string filePath)
        {
            byte[] encryptedBytes = File.ReadAllBytes(filePath);
            if (encryptedBytes == null || encryptedBytes.Length == 0) return null;

            byte[] decryptedBytes;
            try
            {
                decryptedBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
            }
            catch
            {
                return null;
            }

            string json = Encoding.UTF8.GetString(decryptedBytes);
            return JsonSerializer.Deserialize<SecurityVaultPayload>(json);
        }

        private static void SaveVaultPayload(SecurityVaultPayload payload)
        {
            string filePath = DataPathManager.GetSettingsFilePath(VaultFileName);
            string json = JsonSerializer.Serialize(payload);
            byte[] plainBytes = Encoding.UTF8.GetBytes(json);

            byte[] encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);

            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllBytes(filePath, encryptedBytes);
        }

        private static SessionAuthPayload? LoadSessionPayload(string filePath)
        {
            byte[] encryptedBytes = File.ReadAllBytes(filePath);
            if (encryptedBytes == null || encryptedBytes.Length == 0) return null;

            byte[] decryptedBytes;
            try
            {
                decryptedBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
            }
            catch
            {
                return null;
            }

            string json = Encoding.UTF8.GetString(decryptedBytes);
            return JsonSerializer.Deserialize<SessionAuthPayload>(json);
        }

        private static void SaveSessionPayload(SessionAuthPayload payload)
        {
            string filePath = DataPathManager.GetSettingsFilePath(SessionAuthFileName);
            string json = JsonSerializer.Serialize(payload);
            byte[] plainBytes = Encoding.UTF8.GetBytes(json);

            byte[] encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);

            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllBytes(filePath, encryptedBytes);
        }
    }
}
