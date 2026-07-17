using System;
using System.IO;

namespace VocaChat.Data;

/// <summary>
/// 集中定义 VocaChat 在当前用户本地应用数据目录中的持久化位置。
/// </summary>
public static class VocaChatApplicationPaths
{
    private const string ApplicationDirectoryName = "VocaChat";
    private const string MediaDirectoryName = "Media";

    /// <summary>
    /// 返回并确保创建 VocaChat 的本地应用数据根目录。
    /// </summary>
    public static string GetApplicationDataDirectory()
    {
        string localApplicationDataDirectory = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        string applicationDirectory = Path.Combine(
            localApplicationDataDirectory,
            ApplicationDirectoryName);

        Directory.CreateDirectory(applicationDirectory);
        return applicationDirectory;
    }

    /// <summary>
    /// 返回并确保创建本地媒体根目录。
    /// </summary>
    public static string GetMediaDirectory()
    {
        string mediaDirectory = Path.Combine(
            GetApplicationDataDirectory(),
            MediaDirectoryName);

        Directory.CreateDirectory(mediaDirectory);
        return mediaDirectory;
    }
}
