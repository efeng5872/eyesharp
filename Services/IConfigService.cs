using System;
using System.Threading.Tasks;
using eyesharp.Models;

namespace eyesharp.Services
{
    /// <summary>
    /// 配置服务接口
    /// </summary>
    public interface IConfigService
    {
        /// <summary>
        /// 配置文件损坏事件
        /// </summary>
        event EventHandler<ConfigCorruptedEventArgs>? ConfigCorrupted;

        /// <summary>
        /// 加载配置
        /// </summary>
        Task<AppConfig> LoadConfigAsync();

        /// <summary>
        /// 保存配置
        /// </summary>
        Task SaveConfigAsync(AppConfig config);

        /// <summary>
        /// 获取配置文件路径
        /// </summary>
        string GetConfigFilePath();

        /// <summary>
        /// 检查配置文件是否存在
        /// </summary>
        bool ConfigExists();
    }
}
