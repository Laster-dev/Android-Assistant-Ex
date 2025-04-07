namespace 搞机助手Ex.Helper
{
    /// <summary>
    /// 表示Android设备的当前启动模式
    /// </summary>
    public enum DeviceMode
    {
        /// <summary>
        /// 设备模式未知或无法检测
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// 设备处于正常系统模式
        /// </summary>
        System = 1,

        /// <summary>
        /// 设备处于fastboot模式
        /// </summary>
        Fastboot = 2,

        /// <summary>
        /// 设备处于Recovery模式
        /// </summary>
        Recovery = 3,

        /// <summary>
        /// 设备处于下载模式（三星设备）
        /// </summary>
        Download = 4,

        /// <summary>
        /// 没有设备连接
        /// </summary>
        NoDevice = 5
    }
}