using System;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

namespace NetheritInjector
{
    public static class KeySystem
    {
        private const string SECRET = "NETHERIT_2026_SECRET_KEY";
        
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
