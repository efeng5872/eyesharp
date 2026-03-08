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
        DailyUsageStatistics GetTodayStatistics();

        /// <summary>
        /// 获取本周汇总
        /// </summary>
        DailyUsageStatistics GetWeekStatistics();

        /// <summary>
        /// 获取本月汇总
        /// </summary>
        DailyUsageStatistics GetMonthStatistics();

        /// <summary>
        /// 获取最近N天的每日统计
        /// </summary>
        List<DailyUsageStatistics> GetRecentDailyStatistics(int days);

        /// <summary>
        /// 获取指定日期范围的小时级数据
        /// </summary>
        List<HourlyActivityRecord> GetHourlyData(DateTime startDate, DateTime endDate);

        /// <summary>
        /// 获取实时状态
        /// </summary>
        RealTimeUsageStatus GetRealTimeStatus();

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
        /// 导出数据到CSV
        /// </summary>
        Task<string> ExportToCsvAsync(DateTime startDate, DateTime endDate);
    }
}
