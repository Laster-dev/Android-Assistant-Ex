using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;

namespace 搞机助手Ex.Helper
{
    // 定义返回的结构体
    public struct AppInfo
    {
        public string AppName { get; set; }
        public string IconUrl { get; set; }
        public string PackageName { get; set; }
    }

    public class AppInfoFetcher
    {
        /// <summary>
        /// 获取应用程序信息
        /// </summary>
        /// <param name="packname">应用包名</param>
        /// <returns>包含App名称、图标URL和包名的AppInfo结构体</returns>
        public static async Task<AppInfo> GetAppInfoAsync(string packname)
        {
            // 构建腾讯应用宝URL
            string url = $"https://sj.qq.com/appdetail/{packname}";

            try
            {
                // 使用HttpClient发送请求，指定编码处理
                using (HttpClient client = new HttpClient())
                {


                    // 获取网页内容时不要自动解析响应内容为字符串
                    HttpResponseMessage response = await client.GetAsync(url);

                    // 确保响应成功
                    response.EnsureSuccessStatusCode();

                    // 获取原始响应流
                    var responseStream = await response.Content.ReadAsByteArrayAsync();


                    // 使用确定的编码解析内容
                    string pageContent = Encoding.UTF8.GetString(responseStream);
                    Console.WriteLine(pageContent);
                    // 正则匹配应用程序名称 - 首先尝试主要匹配模式
                    string namePattern = @"<h1\s+title=""([^""]+)""\s+class=""GameCard_name___MG5g"">([^<]+)</h1>";
                    Match nameMatch = Regex.Match(pageContent, namePattern);
                    string appName;
                    string iconUrl;
                    if (nameMatch.Success)
                    {
                        // 主要匹配模式成功
                        appName = nameMatch.Groups[1].Value;
                        // 正则匹配图片地址
                        // 更精确的图标提取正则表达式，考虑了自闭合标签
                        string imgPattern = @"<img\s+loading=""eager""\s+alt=""[^""]*""\s+src=""([^""]+)""\s+class=""jsx-\d+\s*GameIcon_icon__[^""]*""\s*/?>";
                        Match imgMatch = Regex.Match(pageContent, imgPattern);
                        iconUrl = imgMatch.Success ? imgMatch.Groups[1].Value : "";
                    }
                    else
                    {
                        // 如果主要匹配失败，尝试匹配搜索结果标题中的应用名称
                        // 更宽松地匹配应用名称（任何引号内的内容，包括中文）“
                        string searchTitlePattern = @"“([^""]+)”的相关推荐";
                        Match searchTitleMatch = Regex.Match(pageContent, searchTitlePattern);
                        appName = searchTitleMatch.Success ? searchTitleMatch.Groups[1].Value : "未找到应用程序名称";
                        iconUrl = "";
                    }

                    // 返回结果
                    return new AppInfo
                    {
                        AppName = appName,
                        IconUrl = iconUrl,
                        PackageName = packname
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取应用程序信息时出错: {ex.Message}");
                return new AppInfo
                {
                    AppName = "未知应用",
                    IconUrl = "",
                    PackageName = packname
                };
            }
        }


        public static async Task Main()
        {
            var a = await GetAppInfoAsync("com.miui.voiceassist");
            Console.WriteLine(a.AppName);
            Console.WriteLine(a.IconUrl);
            Console.ReadLine();
        }
    }
}

