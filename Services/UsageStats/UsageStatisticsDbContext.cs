using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using eyesharp.Models;

namespace eyesharp.Services.UsageStats
{
    /// <summary>
    /// 使用统计数据访问上下文
    /// </summary>
    public class UsageStatisticsDbContext : IDisposable
    {
        private readonly ILogService _logService;
        private readonly string _dbPath;
        private readonly string _encryptionKey;
        private SqliteConnection? _connection;

        public UsageStatisticsDbContext(ILogService logService, string? dbPath = null)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _dbPath = string.IsNullOrWhiteSpace(dbPath)
                ? Path.Combine(baseDir, "usage_statistics.db")
                : dbPath;
            _encryptionKey = DatabaseEncryptionKeyProvider.GetEncryptionKey();
        }

        /// <summary>
        /// 初始化数据库（创建表、索引、启用WAL模式）
        /// </summary>
        public async Task InitializeAsync()
        {
            _logService.Info($"[UsageStatsDb] 初始化数据库: {_dbPath}");

            // 创建连接（使用SQLCipher加密）
            var connectionString = $"Data Source={_dbPath};Password={_encryptionKey};";
            _connection = new SqliteConnection(connectionString);
            await _connection.OpenAsync();

            // 启用WAL模式（提升写入性能）
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA journal_mode = WAL;";
                await cmd.ExecuteNonQueryAsync();

                cmd.CommandText = "PRAGMA synchronous = NORMAL;";
                await cmd.ExecuteNonQueryAsync();
            }

            // 创建表和索引
            await CreateTablesAsync();

            _logService.Info("[UsageStatsDb] 数据库初始化完成");
        }

        /// <summary>
        /// 创建数据库表和索引
        /// </summary>
        private async Task CreateTablesAsync()
        {
            using var cmd = _connection!.CreateCommand();

            // 元数据表
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS DbMetadata (
                    Key TEXT PRIMARY KEY,
                    Value TEXT NOT NULL,
                    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                );
            ";
            await cmd.ExecuteNonQueryAsync();

            // 插入版本信息
            cmd.CommandText = @"
                INSERT OR REPLACE INTO DbMetadata (Key, Value) VALUES
                ('Version', '1'),
                ('CreatedAt', datetime('now')),
                ('MachineName', @MachineName),
                ('UserName', @UserName);
            ";
            cmd.Parameters.AddWithValue("@MachineName", Environment.MachineName);
            cmd.Parameters.AddWithValue("@UserName", Environment.UserName);
            await cmd.ExecuteNonQueryAsync();

            // 小时记录表
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS HourlyRecords (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Hour DATETIME NOT NULL UNIQUE,
                    ActiveSeconds INTEGER DEFAULT 0,
                    IdleSeconds INTEGER DEFAULT 0,
                    LockScreenSeconds INTEGER DEFAULT 0,
                    LockScreenCount INTEGER DEFAULT 0,
                    KeyPressCount INTEGER DEFAULT 0,
                    MouseMoveDistance INTEGER DEFAULT 0,
                    MouseLeftClickCount INTEGER DEFAULT 0,
                    MouseRightClickCount INTEGER DEFAULT 0,
                    MouseMiddleClickCount INTEGER DEFAULT 0,
                    MouseWheelScrollCount INTEGER DEFAULT 0,
                    IsCompleteHour BOOLEAN DEFAULT 1,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                );
            ";
            await cmd.ExecuteNonQueryAsync();

            // 日汇总表
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS DailySummaries (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Date DATE NOT NULL UNIQUE,
                    TotalBootSeconds INTEGER DEFAULT 0,
                    ActiveSeconds INTEGER DEFAULT 0,
                    IdleSeconds INTEGER DEFAULT 0,
                    LockScreenSeconds INTEGER DEFAULT 0,
                    LockScreenCount INTEGER DEFAULT 0,
                    TotalKeyPressCount INTEGER DEFAULT 0,
                    TotalMouseMoveDistance INTEGER DEFAULT 0,
                    TotalMouseLeftClickCount INTEGER DEFAULT 0,
                    TotalMouseRightClickCount INTEGER DEFAULT 0,
                    TotalMouseMiddleClickCount INTEGER DEFAULT 0,
                    TotalMouseWheelScrollCount INTEGER DEFAULT 0,
                    FirstRecordTime DATETIME,
                    LastRecordTime DATETIME,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                );
            ";
            await cmd.ExecuteNonQueryAsync();

            // 创建索引
            cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_hourly_hour ON HourlyRecords(Hour);";
            await cmd.ExecuteNonQueryAsync();

            cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_hourly_date ON HourlyRecords(date(Hour));";
            await cmd.ExecuteNonQueryAsync();

            cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_daily_date ON DailySummaries(Date);";
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// 保存或更新小时记录（UPSERT）
        /// </summary>
        public async Task SaveHourlyRecordAsync(HourlyActivityRecord record)
        {
            using var cmd = _connection!.CreateCommand();

            // 修复：根据是否完整小时选择更新策略
            if (record.IsCompleteHour)
            {
                // 已完成小时：累加模式（可能有多个保存事件）
                cmd.CommandText = @"
                    INSERT INTO HourlyRecords (
                        Hour, ActiveSeconds, IdleSeconds, LockScreenSeconds, LockScreenCount,
                        KeyPressCount, MouseMoveDistance, MouseLeftClickCount, MouseRightClickCount,
                        MouseMiddleClickCount, MouseWheelScrollCount, IsCompleteHour, UpdatedAt
                    ) VALUES (
                        @Hour, @ActiveSeconds, @IdleSeconds, @LockScreenSeconds, @LockScreenCount,
                        @KeyPressCount, @MouseMoveDistance, @MouseLeftClickCount, @MouseRightClickCount,
                        @MouseMiddleClickCount, @MouseWheelScrollCount, @IsCompleteHour, @UpdatedAt
                    )
                    ON CONFLICT(Hour) DO UPDATE SET
                        ActiveSeconds = ActiveSeconds + excluded.ActiveSeconds,
                        IdleSeconds = IdleSeconds + excluded.IdleSeconds,
                        LockScreenSeconds = LockScreenSeconds + excluded.LockScreenSeconds,
                        LockScreenCount = LockScreenCount + excluded.LockScreenCount,
                        KeyPressCount = KeyPressCount + excluded.KeyPressCount,
                        MouseMoveDistance = MouseMoveDistance + excluded.MouseMoveDistance,
                        MouseLeftClickCount = MouseLeftClickCount + excluded.MouseLeftClickCount,
                        MouseRightClickCount = MouseRightClickCount + excluded.MouseRightClickCount,
                        MouseMiddleClickCount = MouseMiddleClickCount + excluded.MouseMiddleClickCount,
                        MouseWheelScrollCount = MouseWheelScrollCount + excluded.MouseWheelScrollCount,
                        IsCompleteHour = excluded.IsCompleteHour,
                        UpdatedAt = excluded.UpdatedAt;
                ";
            }
            else
            {
                // 当前未完成小时：替换模式（内存中的数据是最新的）
                cmd.CommandText = @"
                    INSERT INTO HourlyRecords (
                        Hour, ActiveSeconds, IdleSeconds, LockScreenSeconds, LockScreenCount,
                        KeyPressCount, MouseMoveDistance, MouseLeftClickCount, MouseRightClickCount,
                        MouseMiddleClickCount, MouseWheelScrollCount, IsCompleteHour, UpdatedAt
                    ) VALUES (
                        @Hour, @ActiveSeconds, @IdleSeconds, @LockScreenSeconds, @LockScreenCount,
                        @KeyPressCount, @MouseMoveDistance, @MouseLeftClickCount, @MouseRightClickCount,
                        @MouseMiddleClickCount, @MouseWheelScrollCount, @IsCompleteHour, @UpdatedAt
                    )
                    ON CONFLICT(Hour) DO UPDATE SET
                        ActiveSeconds = excluded.ActiveSeconds,
                        IdleSeconds = excluded.IdleSeconds,
                        LockScreenSeconds = excluded.LockScreenSeconds,
                        LockScreenCount = excluded.LockScreenCount,
                        KeyPressCount = excluded.KeyPressCount,
                        MouseMoveDistance = excluded.MouseMoveDistance,
                        MouseLeftClickCount = excluded.MouseLeftClickCount,
                        MouseRightClickCount = excluded.MouseRightClickCount,
                        MouseMiddleClickCount = excluded.MouseMiddleClickCount,
                        MouseWheelScrollCount = excluded.MouseWheelScrollCount,
                        IsCompleteHour = excluded.IsCompleteHour,
                        UpdatedAt = excluded.UpdatedAt;
                ";
            }

            cmd.Parameters.AddWithValue("@Hour", record.Hour.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("@ActiveSeconds", record.ActiveSeconds);
            cmd.Parameters.AddWithValue("@IdleSeconds", record.IdleSeconds);
            cmd.Parameters.AddWithValue("@LockScreenSeconds", record.LockScreenSeconds);
            cmd.Parameters.AddWithValue("@LockScreenCount", record.LockScreenCount);
            cmd.Parameters.AddWithValue("@KeyPressCount", record.KeyPressCount);
            cmd.Parameters.AddWithValue("@MouseMoveDistance", record.MouseMoveDistance);
            cmd.Parameters.AddWithValue("@MouseLeftClickCount", record.MouseLeftClickCount);
            cmd.Parameters.AddWithValue("@MouseRightClickCount", record.MouseRightClickCount);
            cmd.Parameters.AddWithValue("@MouseMiddleClickCount", record.MouseMiddleClickCount);
            cmd.Parameters.AddWithValue("@MouseWheelScrollCount", record.MouseWheelScrollCount);
            cmd.Parameters.AddWithValue("@IsCompleteHour", record.IsCompleteHour);
            cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// 保存或更新日汇总（UPSERT）
        /// </summary>
        public async Task SaveDailySummaryAsync(DailyUsageStatistics summary)
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO DailySummaries (
                    Date, TotalBootSeconds, ActiveSeconds, IdleSeconds, LockScreenSeconds, LockScreenCount,
                    TotalKeyPressCount, TotalMouseMoveDistance, TotalMouseLeftClickCount, TotalMouseRightClickCount,
                    TotalMouseMiddleClickCount, TotalMouseWheelScrollCount, FirstRecordTime, LastRecordTime, UpdatedAt
                ) VALUES (
                    @Date, @TotalBootSeconds, @ActiveSeconds, @IdleSeconds, @LockScreenSeconds, @LockScreenCount,
                    @TotalKeyPressCount, @TotalMouseMoveDistance, @TotalMouseLeftClickCount, @TotalMouseRightClickCount,
                    @TotalMouseMiddleClickCount, @TotalMouseWheelScrollCount, @FirstRecordTime, @LastRecordTime, @UpdatedAt
                )
                ON CONFLICT(Date) DO UPDATE SET
                    TotalBootSeconds = excluded.TotalBootSeconds,
                    ActiveSeconds = excluded.ActiveSeconds,
                    IdleSeconds = excluded.IdleSeconds,
                    LockScreenSeconds = excluded.LockScreenSeconds,
                    LockScreenCount = excluded.LockScreenCount,
                    TotalKeyPressCount = excluded.TotalKeyPressCount,
                    TotalMouseMoveDistance = excluded.TotalMouseMoveDistance,
                    TotalMouseLeftClickCount = excluded.TotalMouseLeftClickCount,
                    TotalMouseRightClickCount = excluded.TotalMouseRightClickCount,
                    TotalMouseMiddleClickCount = excluded.TotalMouseMiddleClickCount,
                    TotalMouseWheelScrollCount = excluded.TotalMouseWheelScrollCount,
                    FirstRecordTime = COALESCE(DailySummaries.FirstRecordTime, excluded.FirstRecordTime),
                    LastRecordTime = excluded.LastRecordTime,
                    UpdatedAt = excluded.UpdatedAt;
            ";

            cmd.Parameters.AddWithValue("@Date", summary.Date.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@TotalBootSeconds", summary.TotalBootSeconds);
            cmd.Parameters.AddWithValue("@ActiveSeconds", summary.ActiveSeconds);
            cmd.Parameters.AddWithValue("@IdleSeconds", summary.IdleSeconds);
            cmd.Parameters.AddWithValue("@LockScreenSeconds", summary.LockScreenSeconds);
            cmd.Parameters.AddWithValue("@LockScreenCount", summary.LockScreenCount);
            cmd.Parameters.AddWithValue("@TotalKeyPressCount", summary.TotalKeyPressCount);
            cmd.Parameters.AddWithValue("@TotalMouseMoveDistance", summary.TotalMouseMoveDistance);
            cmd.Parameters.AddWithValue("@TotalMouseLeftClickCount", summary.TotalMouseLeftClickCount);
            cmd.Parameters.AddWithValue("@TotalMouseRightClickCount", summary.TotalMouseRightClickCount);
            cmd.Parameters.AddWithValue("@TotalMouseMiddleClickCount", summary.TotalMouseMiddleClickCount);
            cmd.Parameters.AddWithValue("@TotalMouseWheelScrollCount", summary.TotalMouseWheelScrollCount);
            cmd.Parameters.AddWithValue("@FirstRecordTime", summary.FirstRecordTime.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("@LastRecordTime", summary.LastRecordTime.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// 获取指定日期范围的小时记录
        /// </summary>
        public async Task<List<HourlyActivityRecord>> GetHourlyRecordsAsync(DateTime startDate, DateTime endDate)
        {
            var results = new List<HourlyActivityRecord>();

            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
                SELECT * FROM HourlyRecords
                WHERE Hour >= @StartDate AND Hour <= @EndDate
                ORDER BY Hour;
            ";
            cmd.Parameters.AddWithValue("@StartDate", startDate.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("@EndDate", endDate.ToString("yyyy-MM-dd HH:mm:ss"));

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapHourlyRecord(reader));
            }

            return results;
        }

        /// <summary>
        /// 获取指定日期的日汇总
        /// </summary>
        public async Task<DailyUsageStatistics?> GetDailySummaryAsync(DateTime date)
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = "SELECT * FROM DailySummaries WHERE Date = @Date;";
            cmd.Parameters.AddWithValue("@Date", date.ToString("yyyy-MM-dd"));

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapDailySummary(reader);
            }

            return null;
        }

        /// <summary>
        /// 获取最近N天的日汇总
        /// </summary>
        public async Task<List<DailyUsageStatistics>> GetRecentDailySummariesAsync(int days)
        {
            var results = new List<DailyUsageStatistics>();

            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
                SELECT * FROM DailySummaries
                WHERE Date >= date('now', '-' || @Days || ' days')
                ORDER BY Date DESC;
            ";
            cmd.Parameters.AddWithValue("@Days", days);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapDailySummary(reader));
            }

            return results;
        }

        /// <summary>
        /// 清理过期数据
        /// </summary>
        public async Task CleanupOldDataAsync()
        {
            using var transaction = _connection!.BeginTransaction();
            try
            {
                // 删除90天前的详细小时数据
                using (var cmd = _connection.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM HourlyRecords WHERE Hour < datetime('now', '-90 days');";
                    var hourlyDeleted = await cmd.ExecuteNonQueryAsync();
                    _logService.Debug($"[UsageStatsDb] 清理了{hourlyDeleted}条过期小时记录");
                }

                // 删除365天前的日汇总
                using (var cmd = _connection.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM DailySummaries WHERE Date < date('now', '-365 days');";
                    var dailyDeleted = await cmd.ExecuteNonQueryAsync();
                    _logService.Debug($"[UsageStatsDb] 清理了{dailyDeleted}条过期日记录");
                }

                transaction.Commit();

                // 执行VACUUM释放空间
                using (var cmd = _connection.CreateCommand())
                {
                    cmd.CommandText = "VACUUM;";
                    await cmd.ExecuteNonQueryAsync();
                }

                _logService.Info("[UsageStatsDb] 过期数据清理完成");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logService.Error(ex, "[UsageStatsDb] 清理过期数据失败");
                throw;
            }
        }

        /// <summary>
        /// 删除指定小时的小时记录（用于修复历史数据）
        /// </summary>
        public async Task DeleteHourlyRecordAsync(DateTime hour)
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = "DELETE FROM HourlyRecords WHERE Hour = @Hour;";
            cmd.Parameters.AddWithValue("@Hour", hour.ToString("yyyy-MM-dd HH:mm:ss"));
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// 映射小时记录
        /// </summary>
        private static HourlyActivityRecord MapHourlyRecord(SqliteDataReader reader)
        {
            return new HourlyActivityRecord
            {
                Hour = reader.GetDateTime(1),
                ActiveSeconds = reader.GetInt32(2),
                IdleSeconds = reader.GetInt32(3),
                LockScreenSeconds = reader.GetInt32(4),
                LockScreenCount = reader.GetInt32(5),
                KeyPressCount = reader.GetInt32(6),
                MouseMoveDistance = reader.GetInt64(7),
                MouseLeftClickCount = reader.GetInt32(8),
                MouseRightClickCount = reader.GetInt32(9),
                MouseMiddleClickCount = reader.GetInt32(10),
                MouseWheelScrollCount = reader.GetInt32(11),
                IsCompleteHour = reader.GetBoolean(12)
            };
        }

        /// <summary>
        /// 映射日汇总
        /// </summary>
        private static DailyUsageStatistics MapDailySummary(SqliteDataReader reader)
        {
            return new DailyUsageStatistics
            {
                Date = reader.GetDateTime(1),
                TotalBootSeconds = reader.GetInt32(2),
                ActiveSeconds = reader.GetInt32(3),
                IdleSeconds = reader.GetInt32(4),
                LockScreenSeconds = reader.GetInt32(5),
                LockScreenCount = reader.GetInt32(6),
                TotalKeyPressCount = reader.GetInt32(7),
                TotalMouseMoveDistance = reader.GetInt64(8),
                TotalMouseLeftClickCount = reader.GetInt32(9),
                TotalMouseRightClickCount = reader.GetInt32(10),
                TotalMouseMiddleClickCount = reader.GetInt32(11),
                TotalMouseWheelScrollCount = reader.GetInt32(12),
                FirstRecordTime = reader.GetDateTime(13),
                LastRecordTime = reader.GetDateTime(14)
            };
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }
    }
}
