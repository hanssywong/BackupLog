using Microsoft.Extensions.Configuration;

namespace CompressBackup
{
    public class CompressLogSetting
    {
        public string? SourcePath { get; set; }
        public string? TargetPath { get; set; }
        public int Months { get; set; }
        public int Days { get; set; }
        public int ExpiryDate { get; set; }
    }
    internal class AppConfig
    {
        internal static List<CompressLogSetting>? GetCompressLogSetting(IConfiguration configuration)
        {
            List<CompressLogSetting>? setting = configuration.GetSection("LoadSettings")?.Get<List<CompressLogSetting>?>();
            return setting;
        }
    }
}
