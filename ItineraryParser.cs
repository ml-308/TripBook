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
        static readonly Regex FareRe = new Regex(@"([0-9]+(?:\.[0-9]+)?)\s*[¥￥元块]", RegexOptions.Compiled);
        // 无货币符号的费用（必须带小数点，避免把「14:41」里的 41 当钱）
        static readonly Regex PlainFareRe = new Regex(@"(?<![0-9.])([0-9]+\.[0-9]{1,2})(?![0-9])", RegexOptions.Compiled);
        static readonly Regex FareOnlyRe = new Regex(@"^[¥￥]?\s*([0-9]+(?:\.[0-9]+)?)\s*[¥￥元]?$", RegexOptions.Compiled);
        static readonly Regex TotalRe = new Regex(@"(总计|合计|总共|共计)", RegexOptions.Compiled);
        static readonly Regex DurRe = new Regex(@"(\d+)\s*(?:h|H|小时|时)\s*(\d+)?\s*(?:min|MIN|分钟|分)?", RegexOptions.Compiled);
        static readonly Regex Dur2Re = new Regex(@"(\d+)\s*(?:min|分钟|分)", RegexOptions.Compiled);
        static readonly Regex TagRe = new Regex(@"[【\[]\s*([^】\]]+?)\s*[】\]]", RegexOptions.Compiled);
        static readonly Regex PovHeadRe = new Regex(@"^\s*(?:pov|第一视角|车载视角|机位)\s*[:：]?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static readonly Regex MonthDayRe = new Regex(@"(\d{1,2})\s*[.．/月\-]\s*(\d{1,2})", RegexOptions.Compiled);
        static readonly Regex SegSplitRe = new Regex(@"\s*[-—–~～→至]+\s*", RegexOptions.Compiled);
        static readonly Regex LineHintRe = new Regex(@"\d|路|线|快|慢|专|环|区间|观光|BRT|地铁|轨交|城铁|轻轨", RegexOptions.Compiled);
        static readonly Regex RouteVehRe = new Regex(@"^(.*?)[_\-—–]([A-Za-z]{0,2}\d{3,6}[A-Za-z]?)$", RegexOptions.Compiled);
        static readonly Regex SpaceRe = new Regex(@"[ \t]+", RegexOptions.Compiled);
        static readonly Regex RouteTailRe = new Regex(@"[路线程线]", RegexOptions.Compiled);

        // 构成一次乘降事件的「物理动作」
        static readonly (string[] kws, string canon)[] ActionDefs = new[]
        {
            (new[] { "上车", "上車" }, "上车"),
            (new[] { "下车", "下車" }, "下车"),
            (new[] { "进站", "入站", "进闸", "入闸" }, "进站"),
            (new[] { "出站", "出闸" }, "出站"),
        };

        // 噪声词：不带物理动作，属于状态/优惠/支付方式说明，解析线路车号时剔除
        static readonly string[] NoiseWords = {
            "行程中", "换乘优惠", "优惠", "折扣", "免费", "待支付", "已支付",
            "刷卡", "扫码", "扫码支付", "现金", "投币"
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

            var m = RouteVehRe.Match(remain);
            if (m.Success)
            {
                route = CleanRoute(m.Groups[1].Value);
                vehicle = m.Groups[2].Value;
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
                vehicle = toks[vIdx];
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
            var n = Norm(line);
            if (n == "") return res;

            // —— 费用（先抽出来，避免干扰后续切分）——
            var fares = new List<decimal?>();
            foreach (Match fm in FareRe.Matches(n))
                fares.Add(ToDecimal(fm.Groups[1].Value));
            if (fares.Count == 0)
            {
                var pm = PlainFareRe.Match(n);
                if (pm.Success) fares.Add(ToDecimal(pm.Groups[1].Value));
            }
            var work = FareRe.Replace(n, " ");
            work = PlainFareRe.Replace(work, " ");

            // —— 时间（可多个）——
            var times = new List<(int idx, int len, int hh, int mm)>();
            foreach (Match tm in TimeAnyRe.Matches(work))
            {
                int hh = int.Parse(tm.Groups[1].Value), mm = int.Parse(tm.Groups[2].Value);
                if (hh <= 24 && mm <= 59) times.Add((tm.Index, tm.Length, hh, mm));
            }
            if (times.Count == 0) return res;

            // —— 动作词 ——
            var actions = new List<(int idx, int end, string canon)>();
            foreach (var def in ActionDefs)
                foreach (var kw in def.kws)
                {
                    int i = work.IndexOf(kw, StringComparison.Ordinal);
                    while (i >= 0)
                    {
                        actions.Add((i, i + kw.Length, def.canon));
                        i = work.IndexOf(kw, i + kw.Length, StringComparison.Ordinal);
                    }
                }
            actions = actions.OrderBy(a => a.idx).ToList();

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
                int rel = span.IndexOf(act.canon, StringComparison.Ordinal);
                if (rel < 0) rel = 0;
                string station = span.Substring(0, rel)
                    .Trim(' ', ',', '，', '、', '·', '|', '_', '-', '—', '–', '：', ':');
                string restAfter = span.Substring(rel + act.canon.Length).Trim();

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

        public static DateTime? TitleToDate(string title, DateTime today)
        {
            if (string.IsNullOrEmpty(title)) return null;
            var m = MonthDayRe.Match(title);
            if (!m.Success) return null;
            int mo = int.Parse(m.Groups[1].Value), dy = int.Parse(m.Groups[2].Value);
            if (mo < 1 || mo > 12 || dy < 1 || dy > 31) return null;
            try
            {
                var d = new DateTime(today.Year, mo, dy);
                if ((d - today).TotalDays > 45)
                    d = new DateTime(today.Year - 1, mo, dy);
                return d;
            }
            catch { return null; }
        }

        // ------------------------------------------------------------ 主入口

        public static Journey Parse(string text, DateTime? defaultDate = null)
        {
            var today = defaultDate ?? DateTime.Today;
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

            foreach (var rawLine in lines)
            {
                var line = Norm(rawLine);
                if (line == "") continue;

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

            DateTime? d = TitleToDate(title, today);
            if (d == null)
            {
                foreach (var ln in lines.Take(3))
                {
                    d = TitleToDate(ln, today);
                    if (d != null) break;
                }
            }
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
