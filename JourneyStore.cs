using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;

namespace TripBook
{
    public class AppConfig
    {
        public string data_dir { get; set; } = "";
        public string last_id { get; set; } = "";
        public WindowState window { get; set; } = new WindowState();
    }

    public class WindowState
    {
        public double w { get; set; }
        public double h { get; set; }
        // 用可空类型：窗口尚未定位时写 null，避免 Newtonsoft 序列化 NaN 抛异常
        public double? x { get; set; }
        public double? y { get; set; }
        public bool max { get; set; }
    }

    public class DataFile
    {
        public int version { get; set; }
        public string app { get; set; } = "TripBook";
        public string exported_at { get; set; } = "";
        public List<Journey> journeys { get; set; } = new List<Journey>();
    }

    /// <summary>本地存储：配置 + 行程数据（JSON，原子写入，自动备份）</summary>
    public class JourneyStore
    {
        public const string AppName = "TripBook";
        public const string DataFileName = "journeys.json";
        public const string BackupFileName = "journeys.bak.json";

        public AppConfig Config { get; private set; }
        public string DataDir { get; private set; }
        public List<Journey> Journeys { get; private set; } = new List<Journey>();
        public string LastError { get; private set; } = "";

        public JourneyStore()
        {
            Config = LoadConfig();
            DataDir = Config.data_dir;
            Reload();
        }

        // ---------------------------------------------------------- 路径

        public static string AppDir
        {
            get
            {
                var exe = Assembly.GetExecutingAssembly().Location;
                return Path.GetDirectoryName(exe);
            }
        }

        static string _configDir;

        /// <summary>配置目录：优先 %APPDATA%\TripBook，不可写时依次回退（保证便携使用也正常）</summary>
        public static string ConfigDir
        {
            get
            {
                if (_configDir != null) return _configDir;
                _configDir = ResolveConfigDir();
                return _configDir;
            }
        }

        static string ResolveConfigDir()
        {
            var candidates = new List<string>();
            var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(roaming)) candidates.Add(Path.Combine(roaming, AppName));
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(local)) candidates.Add(Path.Combine(local, AppName));
            candidates.Add(Path.Combine(AppDir, "config"));

            foreach (var dir in candidates)
            {
                try
                {
                    Directory.CreateDirectory(dir);
                    var probe = Path.Combine(dir, ".wtest");
                    File.WriteAllText(probe, "ok");
                    File.Delete(probe);
                    return dir;
                }
                catch { }
            }
            return candidates[0];
        }

        static bool IsWritable(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                var t = Path.Combine(dir, ".write_test");
                File.WriteAllText(t, "ok");
                File.Delete(t);
                return true;
            }
            catch { return false; }
        }

        public static string DefaultDataDir()
        {
            var cand = Path.Combine(AppDir, "data");
            if (IsWritable(cand)) return cand;
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var p = Path.Combine(docs, "TripBook数据");
            Directory.CreateDirectory(p);
            return p;
        }

        public string DataFile { get { return Path.Combine(DataDir, DataFileName); } }

        // ---------------------------------------------------------- 配置

        static string ConfigFile { get { return Path.Combine(ConfigDir, "config.json"); } }

        public static AppConfig LoadConfig()
        {
            var cfg = new AppConfig();
            try
            {
                if (File.Exists(ConfigFile))
                    cfg = JsonConvert.DeserializeObject<AppConfig>(File.ReadAllText(ConfigFile, Encoding.UTF8)) ?? new AppConfig();
            }
            catch { }
            if (string.IsNullOrWhiteSpace(cfg.data_dir)) cfg.data_dir = DefaultDataDir();
            if (cfg.window == null) cfg.window = new WindowState();
            return cfg;
        }

        public void SaveConfig()
        {
            try
            {
                File.WriteAllText(ConfigFile,
                    JsonConvert.SerializeObject(Config, Formatting.Indented), Encoding.UTF8);
            }
            catch { }
        }

        // ---------------------------------------------------------- 读写

        public void Reload()
        {
            LastError = "";
            Journeys = new List<Journey>();
            try { Directory.CreateDirectory(DataDir); }
            catch (Exception ex) { LastError = "数据目录不可用：" + ex.Message; return; }
            if (!File.Exists(DataFile)) return;

            try
            {
                var df = JsonConvert.DeserializeObject<DataFile>(File.ReadAllText(DataFile, Encoding.UTF8));
                if (df != null && df.journeys != null)
                    Journeys = df.journeys.Where(j => j != null).Select(Normalize).ToList();
            }
            catch (Exception ex)
            {
                var bak = Path.Combine(DataDir, BackupFileName);
                if (File.Exists(bak))
                {
                    try
                    {
                        var df = JsonConvert.DeserializeObject<DataFile>(File.ReadAllText(bak, Encoding.UTF8));
                        if (df != null && df.journeys != null)
                        {
                            Journeys = df.journeys.Select(Normalize).ToList();
                            LastError = "主数据文件损坏，已从备份恢复";
                            Sort();
                            return;
                        }
                    }
                    catch { }
                }
                LastError = "数据文件解析失败：" + ex.Message;
            }
            Sort();
        }

        void Sort()
        {
            Journeys = Journeys
                .OrderByDescending(j => j.date ?? "")
                .ThenByDescending(j => j.created_at ?? "")
                .ToList();
        }

        public bool Save()
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                var df = new DataFile
                {
                    version = 1,
                    app = AppName,
                    exported_at = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                    journeys = Journeys
                };
                var json = JsonConvert.SerializeObject(df, Formatting.Indented);
                var tmp = DataFile + ".tmp";
                File.WriteAllText(tmp, json, new UTF8Encoding(false));
                if (File.Exists(DataFile))
                {
                    try { File.Copy(DataFile, Path.Combine(DataDir, BackupFileName), true); } catch { }
                }
                if (File.Exists(DataFile)) File.Delete(DataFile);
                File.Move(tmp, DataFile);
                LastError = "";
                return true;
            }
            catch (Exception ex)
            {
                LastError = "保存失败：" + ex.Message;
                return false;
            }
        }

        public static Journey Normalize(Journey j)
        {
            if (j == null) return new Journey();
            if (j.entries == null) j.entries = new List<ItineraryEntry>();
            foreach (var e in j.entries)
            {
                if (e.time == null) e.time = "";
                if (e.station == null) e.station = "";
                if (e.action == null) e.action = "";
                if (e.line == null) e.line = "";
                if (e.vehicle == null) e.vehicle = "";
                if (e.note == null) e.note = "";
            }
            if (j.pov == null) j.pov = new List<PovItem>();
            foreach (var p in j.pov)
            {
                if (p.tag == null) p.tag = "";
                if (p.line == null) p.line = "";
                if (p.from == null) p.from = "";
                if (p.to == null) p.to = "";
                if (p.vehicle == null) p.vehicle = "";
                if (p.raw == null) p.raw = "";
            }
            if (j.notes == null) j.notes = new List<string>();
            if (j.summary == null) j.summary = new SummaryInfo();
            if (string.IsNullOrEmpty(j.id)) j.id = Guid.NewGuid().ToString("N").Substring(0, 12);
            return j;
        }

        // ---------------------------------------------------------- 增删改

        public Journey Add(Journey j)
        {
            j = Normalize(j);
            if (string.IsNullOrEmpty(j.id)) j.id = Guid.NewGuid().ToString("N").Substring(0, 12);
            j.created_at = string.IsNullOrEmpty(j.created_at)
                ? DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") : j.created_at;
            j.updated_at = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            Journeys.Add(j);
            Sort();
            Save();
            return j;
        }

        public Journey Update(Journey j)
        {
            var old = Get(j.id);
            if (old == null) return Add(j);
            j.created_at = string.IsNullOrEmpty(old.created_at)
                ? DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") : old.created_at;
            j.updated_at = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            int idx = Journeys.IndexOf(old);
            Journeys[idx] = Normalize(j);
            Sort();
            Save();
            return Journeys.First(x => x.id == j.id);
        }

        public void Delete(string id)
        {
            Journeys.RemoveAll(j => j.id == id);
            Save();
        }

        /// <summary>批量删除，返回实际删除条数（只写一次盘）。</summary>
        public int DeleteMany(IEnumerable<string> ids)
        {
            var set = new HashSet<string>(ids ?? Enumerable.Empty<string>());
            if (set.Count == 0) return 0;
            int n = Journeys.RemoveAll(j => set.Contains(j.id));
            if (n > 0) Save();
            return n;
        }

        /// <summary>批量把所选行程的年份改为 year（保留月日；2-29 遇平年收敛到 2-28）。返回修改条数。</summary>
        public int ChangeYear(IEnumerable<string> ids, int year)
        {
            var set = new HashSet<string>(ids ?? Enumerable.Empty<string>());
            if (set.Count == 0 || year < 1900 || year > 2200) return 0;
            int n = 0;
            var now = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            foreach (var j in Journeys)
            {
                if (!set.Contains(j.id)) continue;
                DateTime d;
                if (!DateTime.TryParse(j.date, out d)) continue;
                int max = DateTime.DaysInMonth(year, d.Month);
                int day = d.Day > max ? max : d.Day;
                j.date = new DateTime(year, d.Month, day).ToString("yyyy-MM-dd");
                j.updated_at = now;
                n++;
            }
            if (n > 0) { Sort(); Save(); }
            return n;
        }

        public Journey Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return Journeys.FirstOrDefault(j => j.id == id);
        }

        // ---------------------------------------------------------- 目录切换 / 导入导出

        public bool ChangeDir(string newDir, bool copyExisting, out string error)
        {
            error = "";
            try { Directory.CreateDirectory(newDir); }
            catch (Exception ex) { error = "无法创建目录：" + ex.Message; return false; }

            if (copyExisting)
            {
                try
                {
                    var target = Path.Combine(newDir, DataFileName);
                    var existing = new List<Journey>();
                    if (File.Exists(target))
                    {
                        var df = JsonConvert.DeserializeObject<DataFile>(File.ReadAllText(target, Encoding.UTF8));
                        if (df != null && df.journeys != null) existing = df.journeys;
                    }
                    var have = new HashSet<string>(existing.Select(x => x.id));
                    var merged = existing.Concat(Journeys.Where(j => !have.Contains(j.id))).ToList();
                    var payload = new DataFile { version = 1, journeys = merged,
                        exported_at = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") };
                    File.WriteAllText(target, JsonConvert.SerializeObject(payload, Formatting.Indented),
                        new UTF8Encoding(false));
                }
                catch (Exception ex) { error = "写入新目录失败：" + ex.Message; return false; }
            }

            DataDir = newDir;
            Config.data_dir = newDir;
            SaveConfig();
            Reload();
            return true;
        }

        public int ImportFile(string path, out string error)
        {
            error = "";
            try
            {
                var df = JsonConvert.DeserializeObject<DataFile>(File.ReadAllText(path, Encoding.UTF8));
                if (df == null || df.journeys == null) { error = "文件里没有行程数据"; return 0; }
                var have = new HashSet<string>(Journeys.Select(j => j.id));
                int n = 0;
                foreach (var j in df.journeys)
                {
                    Normalize(j);
                    if (have.Contains(j.id)) j.id = Guid.NewGuid().ToString("N").Substring(0, 12);
                    Journeys.Add(j);
                    have.Add(j.id);
                    n++;
                }
                Sort();
                Save();
                return n;
            }
            catch (Exception ex) { error = ex.Message; return 0; }
        }

        public bool ExportAll(string path, out string error)
        {
            error = "";
            try
            {
                var df = new DataFile { version = 1, journeys = Journeys,
                    exported_at = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") };
                File.WriteAllText(path, JsonConvert.SerializeObject(df, Formatting.Indented),
                    new UTF8Encoding(false));
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }
    }
}
