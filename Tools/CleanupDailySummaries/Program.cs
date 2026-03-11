using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using SQLitePCL;

namespace CleanupDailySummaries
{
    /// <summary>
    /// 数据库加密密钥提供器（从主项目复制）
    /// </summary>
    public static class DatabaseEncryptionKeyProvider
    {
        private const string KeySalt = "EyeSharpSalt_v1";

        public static string GetEncryptionKey()
        {
            try
            {
                var machineGuid = GetMachineGuid();
                var systemInfo = GetSystemInfo();
                var combined = $"{machineGuid}:{systemInfo}:{KeySalt}";

                using var sha256 = SHA256.Create();
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
                return Convert.ToHexString(hash);
            }
            catch
            {
                return GetFallbackKey();
            }
        }

        private static string GetMachineGuid()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Cryptography");
                var guid = key?.GetValue("MachineGuid")?.ToString();
                return !string.IsNullOrEmpty(guid) ? guid : "UnknownGuid";
            }
            catch
            {
                return "UnknownGuid";
            }
        }

        private static string GetSystemInfo()
        {
            try
            {
                return $"{Environment.ProcessorCount}:{Environment.OSVersion.Version}:{Environment.MachineName}:{Environment.UserName}";
            }
            catch
            {
                return "UnknownSystem";
            }
        }

        private static string GetFallbackKey()
        {
            var userPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var fallback = $"{userPath}:{KeySalt}:Fallback";
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(fallback));
            return Convert.ToHexString(hash);
        }
    }

    /// <summary>
    /// 一次性清理工具：删除日汇总数据，让系统自动重建
    /// </summary>
    class Program
    {
        /// <summary>
        /// 查找数据库文件
        /// </summary>
        static string? FindDatabaseFile()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var possiblePaths = new[]
            {
                // 1. 当前目录
                Path.Combine(baseDir, "usage_statistics.db"),
                // 2. 项目根目录
                Path.Combine(baseDir, "..", "..", "..", "..", "usage_statistics.db"),
                // 3. Debug 输出目录
                Path.Combine(baseDir, "..", "..", "..", "..", "bin", "Debug", "net8.0-windows", "win-x64", "usage_statistics.db"),
                // 4. Release 输出目录
                Path.Combine(baseDir, "..", "..", "..", "..", "bin", "Release", "net8.0-windows", "win-x64", "usage_statistics.db"),
            };

            foreach (var path in possiblePaths)
            {
                var fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return null;
        }

        static void Main(string[] args)
        {
            // 初始化 SQLitePCLRaw（加载 SQLCipher）
            Batteries_V2.Init();

            Console.WriteLine("=== EyeSharp 日汇总数据清理工具 ===");
            Console.WriteLine();

            string? dbPath = FindDatabaseFile();

            if (dbPath == null)
            {
                Console.WriteLine("错误：找不到数据库文件 usage_statistics.db");
                Console.WriteLine();
                Console.WriteLine("尝试搜索的位置：");
                Console.WriteLine("1. 当前目录");
                Console.WriteLine("2. ..\\..\\..\\..\\ (项目根目录)");
                Console.WriteLine("3. ..\\..\\..\\..\\bin\\Debug\\net8.0-windows\\win-x64\\");
                Console.WriteLine("4. ..\\..\\..\\..\\bin\\Release\\net8.0-windows\\win-x64\\");
                Console.WriteLine();
                Console.WriteLine("请手动输入数据库文件路径：");
                var inputPath = Console.ReadLine()?.Trim().Trim('"');

                if (!string.IsNullOrEmpty(inputPath) && File.Exists(inputPath))
                {
                    dbPath = inputPath;
                }
                else
                {
                    Console.WriteLine("路径无效，按任意键退出...");
                    Console.ReadKey();
                    return;
                }
            }

            Console.WriteLine($"数据库路径: {dbPath}");
            Console.WriteLine();

            try
            {
                // 检查程序是否正在运行（通过尝试独占打开）
                try
                {
                    using var testStream = new FileStream(dbPath, FileMode.Open, FileAccess.Read, FileShare.None);
                    testStream.Close();
                }
                catch
                {
                    Console.WriteLine("错误：数据库文件被占用，请先关闭 EyeSharp 程序！");
                    Console.WriteLine();
                    Console.WriteLine("按任意键退出...");
                    Console.ReadKey();
                    return;
                }

                // 连接数据库（使用SQLCipher加密）
                var encryptionKey = DatabaseEncryptionKeyProvider.GetEncryptionKey();
                var connectionString = $"Data Source={dbPath};Password={encryptionKey};";
                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                // 检查当前记录数
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM DailySummaries;";
                    var count = (long?)cmd.ExecuteScalar() ?? 0;
                    Console.WriteLine($"当前日汇总记录数: {count}");
                    Console.WriteLine();
                }

                // 确认清理
                Console.Write("确认删除所有日汇总数据吗？(Y/N): ");
                var confirm = Console.ReadLine()?.Trim().ToUpper();

                if (confirm != "Y" && confirm != "YES")
                {
                    Console.WriteLine("操作已取消");
                    Console.WriteLine();
                    Console.WriteLine("按任意键退出...");
                    Console.ReadKey();
                    return;
                }

                // 执行删除
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM DailySummaries;";
                    var affected = cmd.ExecuteNonQuery();
                    Console.WriteLine();
                    Console.WriteLine($"✓ 已删除 {affected} 条日汇总记录");
                }

                // 验证
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM DailySummaries;";
                    var count = (long?)cmd.ExecuteScalar() ?? 0;
                    Console.WriteLine($"✓ 验证：当前记录数 = {count}");
                }

                // 查询小时数据记录数（保留的数据）
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM HourlyRecords;";
                    var count = (long?)cmd.ExecuteScalar() ?? 0;
                    Console.WriteLine($"✓ 小时详细数据保留：{count} 条");
                }

                Console.WriteLine();
                Console.WriteLine("=== 清理完成 ===");
                Console.WriteLine();
                Console.WriteLine("说明：");
                Console.WriteLine("- 日汇总数据已清空");
                Console.WriteLine("- 小时详细数据已保留");
                Console.WriteLine("- 重启 EyeSharp 后会自动重新生成日汇总");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"错误: {ex.Message}");
                Console.WriteLine();
            }

            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
        }
    }
}
