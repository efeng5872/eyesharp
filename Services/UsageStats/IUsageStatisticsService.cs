using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using eyesharp.Models;

namespace eyesharp.Services.UsageStats
{
    /// <summary>
    /// 使用统计服务接口
    /// </summary>
    public interface IUsageStatisticsService
    {
        /// <summary>
        /// 初始化服务
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// 获取今日统计
        /// </summary>
        Task<DailyUsageStatistics> GetTodayStatisticsAsync();

        /// <summary>
        /// 获取本周汇总
        /// </summary>
        Task<DailyUsageStatistics> GetWeekStatisticsAsync();

        /// <summary>
        /// 获取本月汇总
        /// </summary>
        Task<DailyUsageStatistics> GetMonthStatisticsAsync();

        /// <summary>
        /// 获取最近N天的每日统计
        /// </summary>
        Task<List<DailyUsageStatistics>> GetRecentDailyStatisticsAsync(int days);

        /// <summary>
        /// 获取指定日期范围的小时级数据
        /// </summary>
        Task<List<HourlyActivityRecord>> GetHourlyDataAsync(DateTime startDate, DateTime endDate);

        /// <summary>
        /// 获取实时状态
        /// </summary>
        Task<RealTimeUsageStatus> GetRealTimeStatusAsync();

        /// <summary>
        /// 处理活动状态变化（由InputMonitorService触发）
        /// </summary>
        void HandleActivityStateChanged(ActivityStateChangedEventArgs args);

        /// <summary>
        /// 处理锁屏事件
        /// </summary>
        void HandleLockScreen();

        /// <summary>
        /// 处理解锁事件
        /// </summary>
        void HandleUnlockScreen();

        /// <summary>
        /// 手动保存数据
        /// </summary>
        Task SaveAsync();

        /// <summary>
        /// 根据小时事实源重算指定日期范围的日汇总缓存
        /// </summary>
        Task<int> RebuildDailySummariesAsync(DateTime startDate, DateTime endDate);

        /// <summary>
        /// 导出数据到CSV
        /// </summary>
        Task<string> ExportToCsvAsync(DateTime startDate, DateTime endDate);
    }
}
