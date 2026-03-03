using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using eyesharp.Models;

namespace eyesharp.Services
{
    /// <summary>
    /// 统计服务接口
    /// </summary>
    public interface IStatisticsService
    {
        /// <summary>
        /// 开始记录休息
        /// </summary>
        void StartRest(int plannedDurationSeconds, bool isForcedMode);

        /// <summary>
        /// 结束记录休息
        /// </summary>
        void EndRest(bool isCompleted);

        /// <summary>
        /// 获取今日统计
        /// </summary>
        DailyStatistics GetTodayStatistics();

        /// <summary>
        /// 获取本周统计
        /// </summary>
        DailyStatistics GetWeekStatistics();

        /// <summary>
        /// 获取本月统计
        /// </summary>
        DailyStatistics GetMonthStatistics();

        /// <summary>
        /// 获取最近N天的每日统计
        /// </summary>
        List<DailyStatistics> GetRecentDailyStatistics(int days);

        /// <summary>
        /// 获取所有历史记录（最近100条）
        /// </summary>
        List<RestRecord> GetRecentRecords(int count = 100);

        /// <summary>
        /// 异步保存统计数据
        /// </summary>
        Task SaveAsync();

        /// <summary>
        /// 异步加载统计数据
        /// </summary>
        Task LoadAsync();
    }
}
