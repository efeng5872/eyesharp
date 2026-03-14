using eyesharp.Models;

namespace eyesharp.Services
{
    /// <summary>
    /// 鼠标移动像素到物理距离换算服务
    /// </summary>
    public interface IMouseDistanceConverterService
    {
        /// <summary>
        /// 将鼠标移动像素换算为米
        /// </summary>
        double ConvertPixelsToMeters(long pixels);

        /// <summary>
        /// 获取当前生效的像素/米换算值
        /// </summary>
        double GetPixelsPerMeter();
    }
}
