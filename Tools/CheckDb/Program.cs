using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using SQLitePCL;

namespace CheckDb
{
    class Program
    {
        static void Main(string[] args)
        {
            Batteries_V2.Init();

            var dbPath = @"D:\java\ClaudeCode\eyesharp\bin\Debug\net8.0-windows\win-x64\usage_statistics.db";

            // 获取加密密钥
            var key = GetEncryptionKey();
            var connStr = $"Data Source={dbPath};Password={key};";

            try
            {
                using var conn = new SqliteConnection(connStr);
                conn.Open();

                Console.WriteLine("=== 数据库查询 ===");
                Console.WriteLine();

                // 查询小时记录
                Console.WriteLine("--- HourlyRecords 表 ---");
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM HourlyRecords ORDER BY Hour DESC LIMIT 5;";
                    using var reader = cmd.ExecuteReader();
                    bool hasData = false;
                    while (reader.Read())
                    {
                        hasData = true;
                        Console.WriteLine($"小时: {reader["Hour"]}, 活跃: {reader["ActiveSeconds"]}秒, 空闲: {reader["IdleSeconds"]}秒, 按键: {reader["KeyPressCount"]}");
                    }
                    if (!hasData) Console.WriteLine("无数据");
                }

                Console.WriteLine();

                // 查询日汇总
                Console.WriteLine("--- DailySummaries 表 ---");
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM DailySummaries ORDER BY Date DESC LIMIT 5;";
                    using var reader = cmd.ExecuteReader();
                    bool hasData = false;
                    while (reader.Read())
                    {
                        hasData = true;
                        Console.WriteLine($"日期: {reader["Date"]}, 开机: {reader["TotalBootSeconds"]}秒, 活跃: {reader["ActiveSeconds"]}秒");
                    }
                    if (!hasData) Console.WriteLine("无数据");
                }

                Console.WriteLine();

                // 统计总数
                using (var cmd = conn.CreateCommand())
                {
                    var hourlyCount = cmd.ExecuteScalar();
                    cmd.CommandText = "SELECT COUNT(*) FROM HourlyRecords;";
                    hourlyCount = cmd.ExecuteScalar();
                    Console.WriteLine($"小时记录总数: {hourlyCount}");

                    cmd.CommandText = "SELECT COUNT(*) FROM DailySummaries;";
                    var dailyCount = cmd.ExecuteScalar();
                    Console.WriteLine($"日汇总总数: {dailyCount}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
            }

            Console.WriteLine();
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
        }

        static string GetEncryptionKey()
        {
            try
            {
                var machineGuid = GetMachineGuid();
                var systemInfo = $"{Environment.ProcessorCount}:{Environment.OSVersion.Version}:{Environment.MachineName}:{Environment.UserName}";
                var combined = $"{machineGuid}:{systemInfo}:EyeSharpSalt_v1";
                using var sha256 = SHA256.Create();
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
                return Convert.ToHexString(hash);
            }
            catch
            {
                var userPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var fallback = $"{userPath}:EyeSharpSalt_v1:Fallback";
                using var sha256 = SHA256.Create();
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(fallback));
                return Convert.ToHexString(hash);
            }
        }

        static string GetMachineGuid()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                var guid = key?.GetValue("MachineGuid")?.ToString();
                return !string.IsNullOrEmpty(guid) ? guid : "UnknownGuid";
            }
            catch
            {
                return "UnknownGuid";
            }
        }
    }
}
