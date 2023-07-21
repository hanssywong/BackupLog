using Microsoft.Extensions.Configuration;
using Serilog;
using System.IO.Compression;

namespace CompressBackup
{
    internal class BackupLogProc
    {
        public IConfiguration Config { get; }
        public BackupLogProc(IConfiguration configuration)
        {
            Config = configuration;
        }
        /// <summary>
        /// 斜杠
        /// </summary>
        private string Slash { get; } = "/";
        /// <summary>
        /// 复制文件夹
        /// </summary>
        /// <param name="sourceFilePath"></param>
        /// <param name="targetFilePath"></param>
        public void CopyFilefolder(string sourceFilePath, string targetFilePath)
        {
            string[] files = Directory.GetFiles(sourceFilePath);
            string fileName;
            string destFile;
            if (!Directory.Exists(targetFilePath))
            {
                Directory.CreateDirectory(targetFilePath);
            }
            //将获取到的文件一个一个拷贝到目标文件夹中  
            foreach (string s in files)
            {
                fileName = Path.GetFileName(s);
                destFile = Path.Combine(targetFilePath, fileName);
                File.Copy(s, destFile, true);
            }

            DirectoryInfo dirinfo = new DirectoryInfo(sourceFilePath);
            DirectoryInfo[] subFileFolder = dirinfo.GetDirectories();
            for (int j = 0; j < subFileFolder.Length; j++)
            {
                //获取所有子文件夹名 
                //string subSourcePath = $"{sourceFilePath}{Slash}{subFileFolder[j].Name}";
                string subSourcePath = Path.Combine(sourceFilePath, subFileFolder[j].Name);
                //string subTargetPath = $"{targetFilePath}{Slash}{subFileFolder[j].Name}";
                string subTargetPath = Path.Combine(targetFilePath, subFileFolder[j].Name);
                //把得到的子文件夹当成新的源文件夹，递归调用CopyFilefolder
                CopyFilefolder(subSourcePath, subTargetPath);
            }
        }
        /// <summary>
        /// 按月份拷贝文件夹
        /// </summary>
        /// <param name="compressedList"></param>
        public void DoCopyFilefolder(List<FileSystemInfo> compressedList, string targetPath)
        {
            string targetFilePath;
            foreach (var oneList in compressedList)
            {
                string dt = oneList.CreationTime.ToString("yyyy-MM");
                //string onepath = $"{targetPath}{Slash}{dt}";
                string onepath = Path.Combine(targetPath, dt);
                if (!Directory.Exists(onepath))//如果不存在就创建file文件夹
                {
                    try
                    {
                        Directory.CreateDirectory(onepath);
                        Log.Information($"CteateFilefolder：创建月份文件夹（{dt}）成功！");
                    }
                    catch (Exception ex)
                    {
                        Log.Information($"CteateFilefolder：创建月份文件夹（{dt}）失败，错误为：{ex}");
                    }
                }
                //targetFilePath = $"{targetPath}{Slash}{oneList.CreationTime.ToString("yyyy-MM")}{Slash}{oneList.Name}";
                targetFilePath = Path.Combine(targetPath, oneList.CreationTime.ToString("yyyy-MM"), oneList.Name);
                try
                {
                    if (oneList is FileInfo)
                    {
                        File.Copy(oneList.FullName, targetFilePath, true);
                        File.Delete(oneList.FullName);
                    }
                    else if (oneList is DirectoryInfo)
                    {
                        CopyFilefolder(oneList.FullName, targetFilePath);
                        if (Directory.Exists(oneList.FullName)) Directory.Delete(oneList.FullName, true);//拷贝之后就删除相应的文件夹
                        Log.Information($"文件夹（{oneList.Name}）拷贝成功,并删除成功。");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"文件夹（{oneList.Name}）拷贝并删除失败，错误为：{ex}");
                }
            }
        }
        public void DoBackup()
        {
            var settings = AppConfig.GetCompressLogSetting(Config);
            if (settings == null)
            {
                Log.Error("LoadSettings is null");
                return;
            }
            int cnt = 0;
            foreach (var setting in settings)
            {
                Log.Information($"Starting setting - {cnt++}");
                try
                {
                    if (!Directory.Exists(setting.SourcePath))
                    {
                        Log.Error($"{setting.SourcePath} is not exist");
                        continue;
                    }
                    DirectoryInfo SourceDirInfo = new DirectoryInfo(setting.SourcePath);
                    FileSystemInfo[] GetSourceDirectories = SourceDirInfo.GetFileSystemInfos();
                    DateTime compressedDate = DateTime.Now;
                    compressedDate = DateTime.Now.AddMonths(-setting.Months).AddDays(-setting.Days);
                    DateTime expiryDate = DateTime.Now;
                    expiryDate = DateTime.Now.AddMonths(-setting.ExpiryDate);
                    List<FileSystemInfo> compressedList = GetSourceDirectories.Where(a => a.CreationTime.CompareTo(compressedDate) <= 0).OrderBy(a => a.CreationTime).ToList();
                    Log.Information($"{setting.SourcePath} CompressedLog Start!");
                    Log.Information($"满足压缩条件的记录数：{compressedList.Count}");
                    if (string.IsNullOrEmpty(setting.TargetPath))
                    {
                        Log.Error("targetPath is null or empty");
                        return;
                    }
                    DoCopyFilefolder(compressedList, setting.TargetPath);//把相应月份的文件夹拷贝到新创建的月份文件夹
                    if (!Directory.Exists(setting.TargetPath))
                    {
                        Directory.CreateDirectory(setting.TargetPath);
                    }
                    Log.Information($@"Processing target dir: {setting.TargetPath}");
                    DirectoryInfo TargetDirInfo = new DirectoryInfo(setting.TargetPath);
                    FileSystemInfo[] GetTargetDirectories = TargetDirInfo.GetFileSystemInfos();
                    List<string> ListOfTargetDirectories = new List<string>();
                    foreach (var dir in GetTargetDirectories)
                    {
                        try
                        {
                            string type = string.Empty;
                            if (dir is FileInfo)
                            {
                                type = "file";
                            }
                            else if (dir is DirectoryInfo)
                            {
                                ListOfTargetDirectories.Add(dir.FullName);
                                type = "directory";
                            }
                            else
                            {
                                type = "unknown";
                            }
                            bool bIsExpiry = dir.CreationTime.CompareTo(expiryDate) <= 0;
                            Log.Information($"{dir.FullName} is {type}, is expiry?: {bIsExpiry}");
                            if (bIsExpiry)
                            {
                                if (dir is FileInfo) dir.Delete();
                                else if (dir is DirectoryInfo) Directory.Delete(dir.FullName, true);
                                Log.Information($"{dir.FullName} deleted");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex.ToString());
                        }
                    }
                    foreach (var TargetDirectories in ListOfTargetDirectories)
                    {
                        string desFileName = $"{TargetDirectories}.zip";
                        Log.Information($"desFileName = {desFileName}");
                        try
                        {
                            if (!File.Exists(desFileName))
                            {
                                ZipFile.CreateFromDirectory(TargetDirectories, desFileName);//压缩文件夹                        
                            }
                            else
                            {
                                using ZipArchive archive = ZipFile.OpenRead(desFileName);
                                foreach (ZipArchiveEntry entry in archive.Entries)
                                {
                                    Log.Information(entry.FullName);
                                    if (entry.FullName.IndexOf(Slash) > 0)
                                    {
                                        Log.Information(entry.FullName.Substring(0, entry.FullName.IndexOf(Slash)));
                                        if (!Directory.Exists(Path.Combine(TargetDirectories, entry.FullName.Substring(0, entry.FullName.IndexOf(Slash)))))
                                        {
                                            Directory.CreateDirectory(Path.Combine(TargetDirectories, entry.FullName.Substring(0, entry.FullName.IndexOf(Slash))));
                                        }
                                    }
                                    else if (entry.FullName.IndexOf("\\") > 0)
                                    {
                                        Log.Information(entry.FullName.Substring(0, entry.FullName.IndexOf("\\")));
                                        if (!Directory.Exists(Path.Combine(TargetDirectories, entry.FullName.Substring(0, entry.FullName.IndexOf("\\")))))
                                        {
                                            Directory.CreateDirectory(Path.Combine(TargetDirectories, entry.FullName.Substring(0, entry.FullName.IndexOf("\\"))));
                                        }
                                    }

                                    entry.ExtractToFile(Path.Combine(TargetDirectories, entry.FullName), true);
                                }
                                archive.Dispose(); // must dispose, so we can overwrite it
                                                   //ZipFile.ExtractToDirectory(desFileName, onepath);
                                File.Delete(desFileName);
                                ZipFile.CreateFromDirectory(TargetDirectories, desFileName);
                            }
                            if (Directory.Exists(TargetDirectories))
                            {
                                Directory.Delete(TargetDirectories, true);//删除创建的文件夹
                            }
                            Log.Information($"压缩并删除文件夹（{TargetDirectories.Substring(TargetDirectories.LastIndexOf(Slash) + 1)}）成功");
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"压缩并删除文件夹（{TargetDirectories.Substring(TargetDirectories.LastIndexOf(Slash) + 1)}）失败，错误为：{ex.ToString()}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex.ToString());
                }
            }
        }
    }
}
