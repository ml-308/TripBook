using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace TripBook
{
    /// <summary>
    /// 行程文本解析器：把「随手记」格式的文本解析成结构化行程。
    ///
    /// 设计目标：读「意思」而不是死抠格式。
    ///  - 以「时间」和「动作词」为锚点切分，而不是假定「时间 站点动作 线路_车号 票价」的列顺序；
    ///  - 一条记录里可能同时含「进站 … 出站」两个事件（地铁进出站合并行），会被拆成两条；
    ///  - 动作词不限于 上车/下车，也支持 进站/出站（地铁）；
    ///  - 只有时间、没有物理动作的行（如「刷新换乘优惠」）被识别为时间标记，而不是误判成乘车；
    ///  - 车号/线路从语义上区分：车号是「偏后的纯数字（可带尾字母）串」，线路是其余部分。
    ///
    /// 兼容：全角/半角、¥/元、611_65028 或空格分隔、费用缺失、手机复制折行、跨零点。
    /// </summary>
    public static class ItineraryParser
    {
        // 时间：可在行中任意位置出现（地铁合并行含两个时间）
        static readonly Regex TimeAnyRe = new Regex(@"(\d{1,2})\s*[:：]\s*(\d{1,2})", RegexOptions.Compiled);
        static readonly Regex TimeLeadRe = new Regex(@"^\s*(\d{1,2})\s*[:：]\s*(\d{1,2})", RegexOptions.Compiled);
        // 中文/连写时间写法：11点56分、11时56分、1156（仅行首）
        static readonly Regex TimeCnRe = new Regex(@"^\s*(\d{1,2})\s*(?:点|时)\s*(\d{1,2})\s*分?", RegexOptions.Compiled);
        static readonly Regex TimeCompactRe = new Regex(@"^\s*(\d{2})(\d{2})(?=\s|[\u4e00-\u9fa5])", RegexOptions.Compiled);
        static readonly Regex FareRe = new Regex(@"([0-9]+(?:\.[0-9]+)?)\s*[¥￥元块]", RegexOptions.Compiled);
        // 货币符号在前：¥2.00 / ￥ 2
        static readonly Regex CurrencyFareRe = new Regex(@"[¥￥]\s*([0-9]+(?:\.[0-9]+)?)", RegexOptions.Compiled);
        // 无货币符号的费用（必须带小数点，避免把「14:41」里的 41 当钱）
        static readonly Regex PlainFareRe = new Regex(@"(?<![0-9.])([0-9]+\.[0-9]{1,2})(?![0-9])", RegexOptions.Compiled);
        static readonly Regex FareOnlyRe = new Regex(@"^[¥￥]?\s*([0-9]+(?:\.[0-9]+)?)\s*[¥￥元]?$", RegexOptions.Compiled);
        static readonly Regex TotalRe = new Regex(@"(总计|合计|总共|共计)", RegexOptions.Compiled);
        static readonly Regex DurRe = new Regex(@"(\d+)\s*(?:h|H|小时|时)\s*(\d+)?\s*(?:min|MIN|分钟|分)?", RegexOptions.Compiled);
        static readonly Regex Dur2Re = new Regex(@"(\d+)\s*(?:min|分钟|分)", RegexOptions.Compiled);
        static readonly Regex TagRe = new Regex(@"[【\[]\s*([^】\]]+?)\s*[】\]]", RegexOptions.Compiled);
        static readonly Regex PovHeadRe = new Regex(@"^\s*(?:pov|第一视角|车载视角|机位)\s*[:：]?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        // 日期：完整（含年）+ 月日
        static readonly Regex FullDateRe = new Regex(@"(20\d{2})\s*[.\-/年_]\s*(\d{1,2})\s*[.\-/月_]\s*(\d{1,2})\s*日?", RegexOptions.Compiled);
        static readonly Regex FullDateCompactRe = new Regex(@"(?<![0-9])(20\d{2})(\d{2})(\d{2})(?![0-9])", RegexOptions.Compiled);
        static readonly Regex MonthDayRe = new Regex(@"(?<![0-9])(\d{1,2})\s*[.．/月\-_]\s*(\d{1,2})\s*日?(?![0-9])", RegexOptions.Compiled);
        static readonly Regex SegSplitRe = new Regex(@"\s*[-—–~～→至]+\s*", RegexOptions.Compiled);
        static readonly Regex LineHintRe = new Regex(@"\d|路|线|快|慢|专|环|区间|观光|BRT|地铁|轨交|城铁|轻轨", RegexOptions.Compiled);
        static readonly Regex RouteVehRe = new Regex(@"^(.*?)[_\-—–]([A-Za-z]{0,2}\d{3,6}[A-Za-z]?)$", RegexOptions.Compiled);
        static readonly Regex SpaceRe = new Regex(@"[ \t]+", RegexOptions.Compiled);
        static readonly Regex RouteTailRe = new Regex(@"[路线程线]", RegexOptions.Compiled);
        // 车号显式标签：车号:75205 / 车牌 苏B12345 里的数字部分
        static readonly Regex VehicleLabelRe = new Regex(@"(?:车号|車號|车牌|車牌|车辆|車輛|编号|編號)\s*[:：]?\s*([A-Za-z]{0,2}\d{3,6}[A-Za-z]?)", RegexOptions.Compiled);
        // 纯分隔线 / 装饰行
        static readonly Regex RuleLineRe = new Regex(@"^[\s\-=—–_*~·。、,，]+$", RegexOptions.Compiled);
        static readonly Regex HanRe = new Regex(@"[\u4e00-\u9fa5]", RegexOptions.Compiled);
        static readonly Regex BareYearRe = new Regex(@"(?<![0-9])(20\d{2})(?![0-9])", RegexOptions.Compiled);

        // 构成一次乘降事件的「物理动作」（同义词归一）
        static readonly (string[] kws, string canon)[] ActionDefs = new[]
        {
            (new[] { "上车", "上車", "乘车", "乘車", "搭乘", "登车", "登車" }, "上车"),
            (new[] { "下车", "下車", "到站", "抵达", "到達", "到达" }, "下车"),
            (new[] { "进站", "進站", "入站", "进闸", "進閘", "入闸" }, "进站"),
            (new[] { "出站", "出閘", "出闸" }, "出站"),
        };

        // 噪声词：不带物理动作，属于状态/优惠/支付方式说明，解析线路车号时剔除
        static readonly string[] NoiseWords = {
            "行程中", "换乘优惠", "优惠", "折扣", "免费", "待支付", "已支付",
            "刷卡", "扫码", "扫码支付", "现金", "投币", "在途", "已出站", "已进站"
        };

        // ------------------------------------------------------------ 工具

        static string Norm(string s)
        {
            if (s == null) return "";
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (var ch in s)
            {
                char c = ch;
                if (c == '\u3000') c = ' ';
                else if (c >= '０' && c <= '９') c = (char)('0' + (c - '０'));
                else if (c >= 'Ａ' && c <= 'Ｚ') c = (char)('A' + (c - 'Ａ'));
                else if (c >= 'ａ' && c <= 'ｚ') c = (char)('a' + (c - 'ａ'));
                else if (c == '（') c = '(';
                else if (c == '）') c = ')';
                else if (c == '：') c = ':';
                else if (c == '．') c = '.';
                sb.Append(c);
            }
            return SpaceRe.Replace(sb.ToString(), " ").Trim();
        }

        static decimal? ToDecimal(string s)
        {
            decimal v;
            if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out v))
                return Math.Round(v, 2);
            return null;
        }

        static string FmtTime(int hh, int mm) => string.Format("{0:00}:{1:00}", hh, mm);

        /// <summary>把「11点56分」「1156」等写法统一成 11:56，只作用于行首，避免误伤「耗时5小时30分」。</summary>
        static string NormalizeTimeText(string n)
        {
            var mc = TimeCnRe.Match(n);
            if (mc.Success)
            {
                int hh = int.Parse(mc.Groups[1].Value), mm = int.Parse(mc.Groups[2].Value);
                if (hh <= 24 && mm <= 59)
                    return FmtTime(hh, mm) + n.Substring(mc.Length);
            }
            var m2 = TimeCompactRe.Match(n);
            if (m2.Success)
            {
                int hh = int.Parse(m2.Groups[1].Value), mm = int.Parse(m2.Groups[2].Value);
                if (hh <= 24 && mm <= 59)
                    return FmtTime(hh, mm) + " " + n.Substring(m2.Length).TrimStart();
            }
            return n;
        }

        /// <summary>从任意文本（文件名、标题、正文行）里抽出的日期线索</summary>
        public class DateHint
        {
            public int? Year;
            public int? Month;
            public int? Day;
            public bool HasFull { get { return Year.HasValue && Month.HasValue && Day.HasValue; } }
            public bool HasMonthDay { get { return Month.HasValue && Day.HasValue; } }
        }

        /// <summary>
        /// 从文件名 / 文本中解析日期。先找带年份的完整日期（2026-11-01、2026年11月1日、20261101），
        /// 再退化到「月.日」（11.1行程、2月9日）。会先剥掉路径与扩展名。
        /// </summary>
        public static DateHint ParseDateHint(string nameOrText)
        {
            var h = new DateHint();
            if (string.IsNullOrEmpty(nameOrText)) return h;

            string s = nameOrText.Replace('\\', '/').Trim();
            int slash = s.LastIndexOf('/');
            if (slash >= 0) s = s.Substring(slash + 1);
            int dot = s.LastIndexOf('.');
            if (dot > 0)
            {
                var ext = s.Substring(dot + 1);
                // 仅当后缀看起来像扩展名（纯字母且不超过 5 位）才剥掉，避免把「11.1行程」切成「11」
                if (ext.Length > 0 && ext.Length <= 5 && ext.All(char.IsLetter))
                    s = s.Substring(0, dot);
            }
            s = Norm(s);

            var f = FullDateRe.Match(s);
            if (f.Success)
            {
                int y = int.Parse(f.Groups[1].Value), m = int.Parse(f.Groups[2].Value), d = int.Parse(f.Groups[3].Value);
                if (ValidYmd(y, m, d)) { h.Year = y; h.Month = m; h.Day = d; return h; }
            }
            var fc = FullDateCompactRe.Match(s);
            if (fc.Success)
            {
                int y = int.Parse(fc.Groups[1].Value), m = int.Parse(fc.Groups[2].Value), d = int.Parse(fc.Groups[3].Value);
                if (ValidYmd(y, m, d)) { h.Year = y; h.Month = m; h.Day = d; return h; }
            }
            var md = MonthDayRe.Match(s);
            if (md.Success)
            {
                int m = int.Parse(md.Groups[1].Value), d = int.Parse(md.Groups[2].Value);
                if (m >= 1 && m <= 12 && d >= 1 && d <= 31) { h.Month = m; h.Day = d; }
            }
            // 文件名只写了年份（如「2024 行程.txt」）也能用来校正正文里只有月日的日期
            if (!h.Year.HasValue)
            {
                var by = BareYearRe.Match(s);
                if (by.Success) h.Year = int.Parse(by.Groups[1].Value);
            }
            return h;
        }

        /// <summary>只认「带年份的完整日期」，用于识别独立成行的日期，不把「11.1行程」误吃成日期行。</summary>
        static DateTime? FullDateFromLine(string s)
        {
            var f = FullDateRe.Match(s);
            if (f.Success)
            {
                int y = int.Parse(f.Groups[1].Value), m = int.Parse(f.Groups[2].Value), d = int.Parse(f.Groups[3].Value);
                if (ValidYmd(y, m, d)) return new DateTime(y, m, d);
            }
            var fc = FullDateCompactRe.Match(s);
            if (fc.Success)
            {
                int y = int.Parse(fc.Groups[1].Value), m = int.Parse(fc.Groups[2].Value), d = int.Parse(fc.Groups[3].Value);
                if (ValidYmd(y, m, d)) return new DateTime(y, m, d);
            }
            return null;
        }

        static bool ValidYmd(int y, int m, int d)
        {
            if (y < 1900 || y > 2200 || m < 1 || m > 12 || d < 1 || d > 31) return false;
            try { var _ = new DateTime(y, m, d); return true; }
            catch { return false; }
        }

        /// <summary>安全构造日期：非法日（如把 2-29 改到平年）自动收敛到当月最后一天。</summary>
        static DateTime SafeDate(int y, int m, int d)
        {
            if (y < 1900 || y > 2200) y = DateTime.Today.Year;
            if (m < 1) m = 1;
            if (m > 12) m = 12;
            int max = DateTime.DaysInMonth(y, m);
            if (d < 1) d = 1;
            if (d > max) d = max;
            return new DateTime(y, m, d);
        }

        /// <summary>把日期线索换算成具体日期：无年份时按今天推，超过 45 天视为去年。</summary>
        public static DateTime? HintToDate(DateHint h, DateTime today)
        {
            if (h == null || !h.HasMonthDay) return null;
            int year = h.Year ?? today.Year;
            DateTime d;
            try { d = new DateTime(year, h.Month.Value, h.Day.Value); }
            catch { return null; }
            if (!h.Year.HasValue && (d - today).TotalDays > 45)
            {
                try { d = new DateTime(year - 1, h.Month.Value, h.Day.Value); } catch { return null; }
            }
            return d;
        }

        /// <summary>从一段文本里直接抽日期（完整日期优先，其次月日；forceYear 可校正月日的年份）。</summary>
        public static DateTime? ExtractDate(string text, DateTime today, int? forceYear = null)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var s = Norm(text);
            var f = FullDateRe.Match(s);
            if (f.Success)
            {
                int y = int.Parse(f.Groups[1].Value), m = int.Parse(f.Groups[2].Value), d = int.Parse(f.Groups[3].Value);
                if (ValidYmd(y, m, d)) return new DateTime(y, m, d);
            }
            var fc = FullDateCompactRe.Match(s);
            if (fc.Success)
            {
                int y = int.Parse(fc.Groups[1].Value), m = int.Parse(fc.Groups[2].Value), d = int.Parse(fc.Groups[3].Value);
                if (ValidYmd(y, m, d)) return new DateTime(y, m, d);
            }
            var md = MonthDayRe.Match(s);
            if (!md.Success) return null;
            int mo = int.Parse(md.Groups[1].Value), dy = int.Parse(md.Groups[2].Value);
            if (mo < 1 || mo > 12 || dy < 1 || dy > 31) return null;
            int year = forceYear ?? today.Year;
            var r = SafeDate(year, mo, dy);
            if (!forceYear.HasValue && (r - today).TotalDays > 45)
                r = SafeDate(year - 1, mo, dy);
            return r;
        }

        /// <summary>是否像一个「车号」：纯数字（3-6 位）或数字+尾字母，且不少于 3 位数字、整体偏长/带字母。</summary>
        static bool IsVehicleToken(string tok)
        {
            if (string.IsNullOrEmpty(tok)) return false;
            if (!VehCoreRe.IsMatch(tok)) return false;
            int digits = tok.Count(char.IsDigit);
            return digits >= 3 && (tok.Length >= 4 || tok.Any(char.IsLetter));
        }
        static readonly Regex VehCoreRe = new Regex(@"^[A-Za-z]{0,2}\d{3,6}[A-Za-z]?$", RegexOptions.Compiled);

        static string CleanRoute(string s)
        {
            if (s == null) return "";
            s = s.Trim(' ', ',', '，', '、', '·', '|', '_', '-', '—', '–', ':', '：');
            foreach (var pre in new[] { "乘坐", "搭乘", "换乘", "转乘" })
                if (s.StartsWith(pre)) s = s.Substring(pre.Length).Trim();
            return s;
        }

        // ------------------------------------------------------------ 线路 / 车号

        /// <summary>从「动作之后的剩余文本」里拆出线路与车号。会剔除状态噪声词。</summary>
        static void SplitRouteVehicle(string remain, out string route, out string vehicle)
        {
            remain = Norm(remain);
            foreach (var w in NoiseWords)
                remain = remain.Replace(w, " ");
            remain = SpaceRe.Replace(remain.Trim(), " ").Trim();

            route = "";
            vehicle = "";

            // 显式车号标签：车号:75205 / 车牌 08025D
            var vl = VehicleLabelRe.Match(remain);
            if (vl.Success)
            {
                vehicle = vl.Groups[1].Value;
                remain = SpaceRe.Replace(remain.Replace(vl.Value, " ").Trim(), " ").Trim();
            }

            var m = RouteVehRe.Match(remain);
            if (m.Success)
            {
                route = CleanRoute(m.Groups[1].Value);
                if (string.IsNullOrEmpty(vehicle)) vehicle = m.Groups[2].Value;
                return;
            }

            var toks = remain.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (toks.Count == 0) return;

            // 车号通常是「偏后的、最像车号的那一个 token」
            int vIdx = -1;
            for (int i = toks.Count - 1; i >= 0; i--)
            {
                if (IsVehicleToken(toks[i])) { vIdx = i; break; }
            }
            if (vIdx >= 0)
            {
                if (string.IsNullOrEmpty(vehicle)) vehicle = toks[vIdx];
                toks.RemoveAt(vIdx);
            }
            route = CleanRoute(string.Join(" ", toks));
        }

        // ------------------------------------------------------------ 单条记录（可拆成多条）

        /// <summary>
        /// 解析一行文本，可能得到 0/1/2 条记录（地铁进出站合并行拆成两条）。
        /// 不再依赖固定列顺序，而是用「时间 + 动作词」做锚点切分。
        /// </summary>
        public static List<ItineraryEntry> ParseEntryLine(string line)
        {
            var res = new List<ItineraryEntry>();
            var n = NormalizeTimeText(Norm(line));
            if (n == "") return res;

            // —— 费用（先抽出来，避免干扰后续切分）——
            // 「¥2.00」这种符号在前的写法最明确，优先取；否则它会让后面的「数字¥」正则
            // 跨过空格把车号 10001 和这个 ¥ 连起来误判成费用。
            var fares = new List<decimal?>();
            foreach (Match fm in CurrencyFareRe.Matches(n))
                fares.Add(ToDecimal(fm.Groups[1].Value));
            if (fares.Count == 0)
            {
                foreach (Match fm in FareRe.Matches(n))
                    fares.Add(ToDecimal(fm.Groups[1].Value));
                if (fares.Count == 0)
                {
                    var pm = PlainFareRe.Match(n);
                    if (pm.Success) fares.Add(ToDecimal(pm.Groups[1].Value));
                }
            }
            // 清理顺序同样重要：先抹掉「¥数字」，再抹「数字¥」
            var work = CurrencyFareRe.Replace(n, " ");
            work = FareRe.Replace(work, " ");
            work = PlainFareRe.Replace(work, " ");
            work = work.Replace("¥", " ").Replace("￥", " ").Replace("元", " ").Replace("块", " ");
            work = SpaceRe.Replace(work, " ").Trim();

            // —— 时间（可多个）——
            var times = new List<(int idx, int len, int hh, int mm)>();
            foreach (Match tm in TimeAnyRe.Matches(work))
            {
                int hh = int.Parse(tm.Groups[1].Value), mm = int.Parse(tm.Groups[2].Value);
                if (hh <= 24 && mm <= 59) times.Add((tm.Index, tm.Length, hh, mm));
            }
            if (times.Count == 0) return res;

            // —— 动作词 ——
            // 保留原文关键词 kw，切分时按它定位（同义词/繁体归一后长度可能不同）
            var actions = new List<(int idx, int end, string canon, string kw)>();
            foreach (var def in ActionDefs)
                foreach (var kw in def.kws)
                {
                    int i = work.IndexOf(kw, StringComparison.Ordinal);
                    while (i >= 0)
                    {
                        actions.Add((i, i + kw.Length, def.canon, kw));
                        i = work.IndexOf(kw, i + kw.Length, StringComparison.Ordinal);
                    }
                }
            actions = actions.OrderBy(a => a.idx).ToList();
            // 同义动作词挨在一起时（如「乘车上车」）只算一次事件
            var dedup = new List<(int idx, int end, string canon, string kw)>();
            foreach (var a in actions)
            {
                var last = dedup.Count > 0 ? dedup[dedup.Count - 1] : (idx: -99, end: -99, canon: "", kw: "");
                if (last.canon == a.canon && a.idx - last.idx < 4) continue;
                dedup.Add(a);
            }
            actions = dedup;

            // —— 没有物理动作：可能是「时间标记」行（如 刷新换乘优惠）——
            if (actions.Count == 0)
            {
                var text = work.Trim();
                var lead = TimeLeadRe.Match(text);
                if (lead.Success) text = text.Substring(lead.Length).Trim();
                if (text.Length > 0)
                {
                    res.Add(new ItineraryEntry
                    {
                        time = FmtTime(times[0].hh, times[0].mm),
                        station = text,
                        action = "",
                        line = "",
                        vehicle = "",
                        fare = null,
                        note = "",
                        marker = true
                    });
                }
                return res;
            }

            // —— 锚点：时间起点/终点、动作起点/终点，用于界定每段文本范围 ——
            var anchors = new List<int>();
            foreach (var t in times) { anchors.Add(t.idx); anchors.Add(t.idx + t.len); }
            foreach (var a in actions) { anchors.Add(a.idx); anchors.Add(a.end); }
            anchors = anchors.Distinct().OrderBy(x => x).ToList();

            for (int k = 0; k < actions.Count; k++)
            {
                var act = actions[k];
                int left = 0;
                for (int a = anchors.Count - 1; a >= 0; a--)
                {
                    if (anchors[a] < act.idx) { left = anchors[a]; break; }
                }
                int right = work.Length;
                for (int a = 0; a < anchors.Count; a++)
                {
                    if (anchors[a] > act.end) { right = anchors[a]; break; }
                }
                if (right < left) right = work.Length;

                string span = work.Substring(left, right - left);
                int rel = span.IndexOf(act.kw, StringComparison.Ordinal);
                if (rel < 0) rel = 0;
                string station = span.Substring(0, rel)
                    .Trim(' ', ',', '，', '、', '·', '|', '_', '-', '—', '–', '：', ':');
                string restAfter = span.Substring(Math.Min(rel + act.kw.Length, span.Length)).Trim();

                // 动作写在站点前面（「11:56 上车 八佰伴 703 85060」）：从后续 token 里补回站点
                if (station == "")
                {
                    var tks = restAfter.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    if (tks.Count >= 2 && HanRe.IsMatch(tks[0]) &&
                        !LooksLikeLine(tks[0]) && !IsVehicleToken(tks[0]))
                    {
                        station = tks[0].Trim(' ', ',', '，', '、', '·', '|');
                        restAfter = string.Join(" ", tks.Skip(1));
                    }
                }

                // 时间：取本动作之前、最近的一个时间
                var t = times.Where(x => x.idx <= act.idx).OrderByDescending(x => x.idx).First();

                string route, vehicle;
                SplitRouteVehicle(restAfter, out route, out vehicle);

                res.Add(new ItineraryEntry
                {
                    time = FmtTime(t.hh, t.mm),
                    station = station,
                    action = act.canon,
                    line = route,
                    vehicle = vehicle,
                    fare = null,
                    note = "",
                    marker = false
                });
            }

            // —— 费用分配：多个事件时，优先给「下车/出站」；否则给第一条 ——
            if (fares.Count > 0)
            {
                var exitEv = res.FirstOrDefault(e => e.action == "下车" || e.action == "出站");
                (exitEv ?? res[0]).fare = fares[0];
            }

            // —— 线路推断：下/出站若没有线路，沿用同一次乘降的进/上车线路 ——
            for (int i = 0; i < res.Count; i++)
            {
                if ((res[i].action == "下车" || res[i].action == "出站") && string.IsNullOrEmpty(res[i].line))
                {
                    for (int j = i - 1; j >= 0; j--)
                    {
                        if ((res[j].action == "上车" || res[j].action == "进站") && !string.IsNullOrEmpty(res[j].line))
                        {
                            res[i].line = res[j].line;
                            if (string.IsNullOrEmpty(res[i].vehicle)) res[i].vehicle = res[j].vehicle;
                            break;
                        }
                    }
                }
            }

            return res;
        }

        // ------------------------------------------------------------ POV

        static bool LooksLikeLine(string tok)
        {
            return !string.IsNullOrEmpty(tok) && LineHintRe.IsMatch(tok);
        }

        public static PovItem ParsePovLine(string line)
        {
            var raw = Norm(line);
            if (raw == "") return null;

            var tags = TagRe.Matches(raw).Cast<Match>().Select(x => x.Groups[1].Value.Trim()).ToList();
            var text = Norm(TagRe.Replace(raw, " "));
            if (text == "") return null;

            string vehicle = "";
            var toks = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (toks.Count >= 2 && IsVehicleToken(toks[toks.Count - 1]))
            {
                vehicle = toks[toks.Count - 1];
                toks.RemoveAt(toks.Count - 1);
                text = string.Join(" ", toks);
            }

            string segFrom = "", segTo = "";
            var parts = SegSplitRe.Split(text, 2);   // 兼容 - 与 -- 分隔符
            var head = parts[0].Trim();
            if (parts.Length == 2) segTo = parts[1].Trim(' ', ',', '，', '、', '-', '—', '–');

            var htoks = head.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            string route = head;
            if (htoks.Count >= 2 && LooksLikeLine(htoks[0]))
            {
                route = htoks[0];
                segFrom = string.Join(" ", htoks.Skip(1));
            }
            route = CleanRoute(route);
            if (route == "" && vehicle == "") return null;

            return new PovItem
            {
                tag = string.Join(" / ", tags),
                line = route,
                from = segFrom,
                to = segTo,
                vehicle = vehicle,
                raw = line.Trim()
            };
        }

        // ------------------------------------------------------------ 标题 / 日期

        /// <summary>兼容旧签名；forceYear 可由文件名提供，用于校正只有「月.日」的正文标题年份。</summary>
        public static DateTime? TitleToDate(string title, DateTime today, int? forceYear = null)
        {
            return ExtractDate(title, today, forceYear);
        }

        // ------------------------------------------------------------ 主入口

        /// <summary>
        /// fileName 为导入时的文件名（可含路径）。日期判断优先级：
        /// 正文完整日期行 &gt; 文件名完整日期（带年）&gt; 正文标题/前几行的月日（年份用文件名年份校正）&gt; 文件名月日 &gt; 今天。
        /// </summary>
        public static Journey Parse(string text, DateTime? defaultDate = null, string fileName = null)
        {
            var today = defaultDate ?? DateTime.Today;
            var hint = ParseDateHint(fileName);
            int? forceYear = hint.Year;
            var hintDate = HintToDate(hint, today);
            var journey = new Journey
            {
                id = Guid.NewGuid().ToString("N").Substring(0, 12),
                date = today.ToString("yyyy-MM-dd"),
                source_text = (text ?? "").Trim(),
                created_at = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                updated_at = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
            };

            var lines = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var entries = journey.entries;
            var pov = journey.pov;
            var notes = journey.notes;
            string title = "";
            bool inPov = false;
            DateTime? explicitDate = null;

            foreach (var rawLine in lines)
            {
                var line = Norm(rawLine);
                if (line == "" || RuleLineRe.IsMatch(line)) continue;

                if (PovHeadRe.IsMatch(line)) { inPov = true; continue; }

                if (inPov)
                {
                    if (TotalRe.IsMatch(line) && !TimeAnyRe.IsMatch(line)) inPov = false;
                    else
                    {
                        var item = ParsePovLine(line);
                        if (item != null) pov.Add(item);
                        continue;
                    }
                }

                if (TotalRe.IsMatch(line) && !TimeAnyRe.IsMatch(line))
                {
                    var dm = DurRe.Match(line);
                    if (dm.Success)
                        journey.summary.dur_text = dm.Groups[2].Success && dm.Groups[2].Value != ""
                            ? dm.Groups[1].Value + "h" + dm.Groups[2].Value + "min"
                            : dm.Value.Trim();
                    else
                    {
                        var dm2 = Dur2Re.Match(line);
                        if (dm2.Success) journey.summary.dur_text = dm2.Value.Trim();
                    }

                    var fm = FareRe.Match(line);
                    if (!fm.Success) fm = Regex.Match(line, @"([0-9]+(?:\.[0-9]+)?)");
                    if (fm.Success) journey.summary.fare = ToDecimal(fm.Groups[1].Value);
                    journey.summary.raw = line;
                    continue;
                }

                // 独立成行的完整日期：2026年11月1日 / 2026-11-01 / 日期:20261101
                // 必须在「折行补回」之前判断，否则纯数字日期会被误当成票价补到上一条
                if (!inPov && !TimeAnyRe.IsMatch(line))
                {
                    var fd = FullDateFromLine(line);
                    if (fd.HasValue) { explicitDate = fd.Value; continue; }
                }

                var ent = ParseEntryLine(line);
                if (ent.Count > 0) { entries.AddRange(ent); continue; }

                // 手机复制折行：单独的费用 / 车号，补回上一条
                if (entries.Count > 0)
                {
                    var last = entries[entries.Count - 1];
                    var mf = FareOnlyRe.Match(line);
                    if (mf.Success && (last.fare == null || last.fare == 0m))
                    {
                        last.fare = ToDecimal(mf.Groups[1].Value);
                        continue;
                    }
                    if (IsVehicleToken(line) && string.IsNullOrEmpty(last.vehicle))
                    {
                        last.vehicle = line;
                        continue;
                    }
                    if (line.Length <= 6 && RouteTailRe.IsMatch(line))
                    {
                        last.line = (last.line + " " + line).Trim();
                        continue;
                    }
                }

                if (title == "" && (line.Contains("行程") || line.Contains("运转")) && line.Length <= 30)
                {
                    title = line.Trim(' ', '-', '—', '·', '|');
                    continue;
                }

                notes.Add(line);
            }

            // 跨零点：保持原始顺序，用累计分钟数记录
            int day = 0, prevAbs = -1;
            var timeRe2 = new Regex(@"^(\d{1,2}):(\d{2})$");
            foreach (var e in entries)
            {
                var tm = timeRe2.Match(e.time);
                if (!tm.Success) continue;
                int m = int.Parse(tm.Groups[1].Value) * 60 + int.Parse(tm.Groups[2].Value) + day * 1440;
                if (prevAbs >= 0 && m < prevAbs)
                {
                    day++;
                    m += 1440;
                }
                e.day = day;
                e.abs_min = m;
                prevAbs = m;
            }

            // 日期判断：
            //  1) 正文里独立成行的完整日期（含年）最可信
            //  2) 否则取正文标题/前几行的「月.日」，年份优先用文件名给出的年份校正
            //  3) 正文完全没有日期时，用文件名里的日期（完整或月日）
            //  4) 都没有才回落到今天
            // 只取「月.日」线索（不构造 DateTime，避免 2-29 在基准年不是闰年时丢信息）
            DateHint bodyHint = ParseDateHint(title);
            if (!bodyHint.HasMonthDay)
            {
                foreach (var ln in lines.Take(3))
                {
                    var bh = ParseDateHint(ln);
                    if (bh.HasMonthDay) { bodyHint = bh; break; }
                }
            }

            DateTime? d = explicitDate;
            if (d == null && (bodyHint.HasMonthDay || hint.HasMonthDay))
            {
                int year = forceYear ?? (hintDate.HasValue ? hintDate.Value.Year : today.Year);
                int mo = bodyHint.HasMonthDay ? bodyHint.Month.Value : hint.Month.Value;
                int dy = bodyHint.HasMonthDay ? bodyHint.Day.Value : hint.Day.Value;
                d = SafeDate(year, mo, dy);
            }
            if (d == null) d = hintDate;
            var finalDate = d ?? today;

            journey.title = title != "" ? title : (finalDate.Month + "." + finalDate.Day + " 行程");
            journey.date = finalDate.ToString("yyyy-MM-dd");
            return journey;
        }

        /// <summary>示例：手机备忘录风格的随手记（含地铁进出站合并行、进站/出站、换乘优惠标记、-- POV 分隔符）。</summary>
        public const string SampleText =
@"11.1行程

11:56 八佰伴上车 703 85060 2.00¥
12:15 公交三场（红星路）上车 105 75205 0.00¥
12:19 公交三场（贡湖大道）上车望亭专线 31853 0.00¥
12:26 金城新村上车 86 31881 0.00¥
12:41 公交三场（明慈医院）上车 56 35021 0.00¥
13:32 古华山路（龙光路）上车 711区间 82615 0.00¥
14:41 刷新换乘优惠
15:05 刷新换乘优惠
15:36 东湖塘上车 809 85105 0.00¥
16:18 无锡东站进站地铁二号线 0212 行程中 16:30云林出站 1.00¥
16:56 二泉东路上车东亭专线 85801 0.00¥
17:26 太湖大道（锡土路）上车 763 08025D 0.00¥
总计3.00¥ 耗时5h30m

POV：
【4K】711区间 古华山路（龙光路）--东湖塘 82615
东亭专线 二泉东路--太湖大道 85801
";
    }
}
