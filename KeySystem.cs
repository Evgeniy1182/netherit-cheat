using System;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.IO;

namespace NetheritInjector
{
    public static class KeySystem
    {
        private const string SECRET = "NETHERIT_2026_SECRET_KEY";
        private static readonly string KeyDataFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NetheritInjector",
            "keydata.dat"
        );

        public class KeyActivationData
        {
            public long ActivatedAt { get; set; }
            public long ExpiresAt { get; set; }
            public int Duration { get; set; }
        }
        
        // Проверка валидности ключа и получение срока
        public static bool ValidateKey(string key, out int durationDays)
        {
            durationDays = 0;
            
            if (string.IsNullOrWhiteSpace(key))
                return false;
            
            // Удаляем дефисы
            string cleanKey = key.Replace("-", "").ToUpper();
            
            // Проверяем длину
            if (cleanKey.Length != 16)
                return false;
            
            // Проверяем что это только hex символы
            if (!cleanKey.All(c => "0123456789ABCDEF".Contains(c)))
                return false;
            
            // Первый символ - код длительности
            char durationCode = cleanKey[0];
            string data = cleanKey.Substring(1, 11);
            string checksum = cleanKey.Substring(12);
            
            // Декодируем длительность
            durationDays = DecodeDuration(durationCode);
            if (durationDays == 0)
                return false;
            
            // Вычисляем ожидаемую контрольную сумму
            string expectedChecksum = CalculateChecksum(durationCode + data);
            
            return checksum == expectedChecksum;
        }

        // Активировать ключ и сохранить информацию
        public static bool ActivateKey(string key, out string message)
        {
            message = "";

            if (!ValidateKey(key, out int durationDays))
            {
                message = "Неверный ключ";
                return false;
            }

            // Проверяем, не активирован ли уже ключ
            var existingData = GetKeyActivationData(key);
            if (existingData != null)
            {
                if (IsKeyExpired(key))
                {
                    message = "Ключ истек";
                    return false;
                }
                message = "Ключ уже активирован";
                return false;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long expiresAt;

            if (durationDays == -1)
            {
                // Lifetime - 100 лет
                expiresAt = now + (100L * 365 * 24 * 60 * 60 * 1000);
            }
            else
            {
                expiresAt = now + (durationDays * 24L * 60 * 60 * 1000);
            }

            var activationData = new KeyActivationData
            {
                ActivatedAt = now,
                ExpiresAt = expiresAt,
                Duration = durationDays
            };

            SaveKeyActivationData(key, activationData);
            message = "Ключ успешно активирован";
            return true;
        }

        // Проверить истек ли активированный ключ
        public static bool IsKeyExpired(string key)
        {
            var data = GetKeyActivationData(key);
            if (data == null)
                return true;

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return now >= data.ExpiresAt;
        }

        // Получить оставшееся время в миллисекундах
        public static long GetKeyTimeLeft(string key)
        {
            var data = GetKeyActivationData(key);
            if (data == null)
                return 0;

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long timeLeft = data.ExpiresAt - now;
            return timeLeft > 0 ? timeLeft : 0;
        }

        // Форматировать оставшееся время
        public static string FormatTimeLeft(long milliseconds)
        {
            if (milliseconds <= 0)
                return "Истек";

            long seconds = milliseconds / 1000;
            long minutes = seconds / 60;
            long hours = minutes / 60;
            long days = hours / 24;

            if (days > 365)
                return "Навсегда";

            long displayDays = days;
            long displayHours = hours % 24;
            long displayMinutes = minutes % 60;
            long displaySeconds = seconds % 60;

            if (days > 0)
                return $"{displayDays}д {displayHours}ч {displayMinutes}м {displaySeconds}с";
            else if (hours > 0)
                return $"{displayHours}ч {displayMinutes}м {displaySeconds}с";
            else if (minutes > 0)
                return $"{displayMinutes}м {displaySeconds}с";
            else
                return $"{displaySeconds}с";
        }

        // Сохранить данные активации ключа
        private static void SaveKeyActivationData(string key, KeyActivationData data)
        {
            try
            {
                string dir = Path.GetDirectoryName(KeyDataFile)!;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string line = $"{key}|{data.ActivatedAt}|{data.ExpiresAt}|{data.Duration}";
                File.AppendAllText(KeyDataFile, line + Environment.NewLine);
            }
            catch { }
        }

        // Получить данные активации ключа
        private static KeyActivationData? GetKeyActivationData(string key)
        {
            try
            {
                if (!File.Exists(KeyDataFile))
                    return null;

                var lines = File.ReadAllLines(KeyDataFile);
                foreach (var line in lines)
                {
                    var parts = line.Split('|');
                    if (parts.Length == 4 && parts[0] == key)
                    {
                        return new KeyActivationData
                        {
                            ActivatedAt = long.Parse(parts[1]),
                            ExpiresAt = long.Parse(parts[2]),
                            Duration = int.Parse(parts[3])
                        };
                    }
                }
            }
            catch { }
            return null;
        }

        // Очистить истекшие ключи
        public static void CleanupExpiredKeys()
        {
            try
            {
                if (!File.Exists(KeyDataFile))
                    return;

                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var lines = File.ReadAllLines(KeyDataFile);
                var validLines = lines.Where(line =>
                {
                    var parts = line.Split('|');
                    if (parts.Length != 4)
                        return false;

                    long expiresAt = long.Parse(parts[2]);
                    return expiresAt > now;
                }).ToArray();

                File.WriteAllLines(KeyDataFile, validLines);
            }
            catch { }
        }
        
        // Генерация нового ключа с указанным сроком
        public static string GenerateKey(int durationDays)
        {
            char durationCode = EncodeDuration(durationDays);
            if (durationCode == '0')
                throw new ArgumentException("Неверный срок подписки");
            
            // Генерируем 11 случайных hex символов
            byte[] randomBytes = new byte[6];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            
            string data = BitConverter.ToString(randomBytes).Replace("-", "").ToUpper().Substring(0, 11);
            
            // Вычисляем контрольную сумму
            string checksum = CalculateChecksum(durationCode + data);
            
            // Объединяем и форматируем
            string fullKey = durationCode + data + checksum;
            return $"{fullKey.Substring(0, 4)}-{fullKey.Substring(4, 4)}-{fullKey.Substring(8, 4)}-{fullKey.Substring(12)}";
        }
        
        private static char EncodeDuration(int days)
        {
            return days switch
            {
                1 => '1',      // 1 день
                7 => '7',      // 7 дней
                30 => 'A',     // 30 дней (месяц)
                90 => 'B',     // 90 дней (3 месяца)
                180 => 'C',    // 180 дней (6 месяцев)
                365 => 'D',    // 365 дней (год)
                -1 => 'F',     // Lifetime (бессрочный)
                _ => '0'
            };
        }
        
        private static int DecodeDuration(char code)
        {
            return code switch
            {
                '1' => 1,
                '7' => 7,
                'A' => 30,
                'B' => 90,
                'C' => 180,
                'D' => 365,
                'F' => -1,     // Lifetime
                _ => 0
            };
        }
        
        private static string CalculateChecksum(string data)
        {
            string combined = data + SECRET;
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
                return BitConverter.ToString(hashBytes).Replace("-", "").Substring(0, 4).ToUpper();
            }
        }
        
        public static string GetDurationText(int days)
        {
            if (days == -1) return "Навсегда";
            if (days == 1) return "1 день";
            if (days == 7) return "7 дней";
            if (days == 30) return "30 дней";
            if (days == 90) return "90 дней";
            if (days == 180) return "180 дней";
            if (days == 365) return "365 дней";
            return $"{days} дней";
        }
    }
}
