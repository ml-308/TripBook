using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace TripBook
{
    /// <summary>
    /// 行程文本解析器：把「随手记」格式的文本解析成结构化行程。
    /// 支持全角/半角、¥/元、611_65028 或空格分隔、费用缺失、手机复制折行、跨零点。
    /// </summary>
    public static class ItineraryParser
    {
        static readonly Regex TimeRe = new Regex(@"^\s*(\d{1,2})\s*[:：]\s*(\d{1,2})\s*(.*)$", RegexOptions.Compiled);
        static readonly Regex FareRe = new Regex(@"([0-9]+(?:\.[0-9]+)?)\s*[¥￥元块]", RegexOptions.Compiled);
        static readonly Regex PlainFareRe = new Regex(@"(?:^|\s)([0-9]+\.[0-9]{1,2})(?=\s|$)", RegexOptions.Compiled);
        static readonly Regex FareOnlyRe = new Regex(@"^[¥￥]?\s*([0-9]+(?:\.[0-9]+)?)\s*[¥￥元]?$", RegexOptions.Compiled);
        static readonly Regex VehOnlyRe = new Regex(@"^[A-Za-z]{0,2}\d{3,6}$", RegexOptions.Compiled);
        static readonly Regex TotalRe = new Regex(@"(总计|合计|总共|共计)", RegexOptions.Compiled);
        static readonly Regex DurRe = new Regex(@"(\d+)\s*(?:h|H|小时|时)\s*(\d+)?\s*(?:min|MIN|分钟|分)?", RegexOptions.Compiled);
        static readonly Regex Dur2Re = new Regex(@"(\d+)\s*(?:min|分钟|分)", RegexOptions.Compiled);
        static readonly Regex TagRe = new Regex(@"[【\[]\s*([^】\]]+?)\s*[】\]]", RegexOptions.Compiled);
        static readonly Regex PovHeadRe = new Regex(@"^\s*(?:pov|第一视角|车载视角)\s*[:：]?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static readonly Regex MonthDayRe = new Regex(@"(\d{1,2})\s*[.．/月\-]\s*(\d{1,2})", RegexOptions.Compiled);
        static readonly Regex SegSplitRe = new Regex(@"\s*[-—–~～→至]\s*", RegexOptions.Compiled);
        static readonly Regex LineHintRe = new Regex(@"\d|路|线|快|慢|专|环|支|区间|观光|BRT", RegexOptions.Compiled);
        static readonly Regex RouteVehRe = new Regex(@"^(.*?)[_\-—–]([A-Za-z]{0,2}\d{3,6})$", RegexOptions.Compiled);
        static readonly Regex SpaceRe = new Regex(@"[ \t]+", RegexOptions.Compiled);
        static readonly Regex RouteTailRe = new Regex(@"[路线程线]", RegexOptions.Compiled);

        static readonly string[] Actions = { "上车", "下车", "上車", "下車" };

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

        static string CleanRoute(string s)
        {
            if (s == null) return "";
            s = s.Trim(' ', ',', '，', '、', '·', '|', '_', '-', '—', '–', ':', '：');
            foreach (var pre in new[] { "乘坐", "搭乘", "换乘", "转乘" })
                if (s.StartsWith(pre)) s = s.Substring(pre.Length).Trim();
            return s;
        }

        // ------------------------------------------------------------ 单条记录

        static void SplitRouteVehicle(string remain, out string route, out string vehicle)
        {
            remain = Norm(remain);
            vehicle = "";
            route = remain;

            var m = RouteVehRe.Match(remain);
            if (m.Success)
            {
                route = m.Groups[1].Value;
                vehicle = m.Groups[2].Value;
            }
            else
            {
                var toks = remain.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (toks.Length >= 2 && VehOnlyRe.IsMatch(toks[toks.Length - 1]))
                {
                    vehicle = toks[toks.Length - 1];
                    route = string.Join(" ", toks.Take(toks.Length - 1));
                }
                else if (toks.Length == 1 && toks[0].Length >= 4 && VehOnlyRe.IsMatch(toks[0]))
                {
                    vehicle = toks[0];
                    route = "";
                }
            }
            route = CleanRoute(route);
        }

        public static ItineraryEntry ParseEntryLine(string line)
        {
            var m = TimeRe.Match(Norm(line));
            if (!m.Success) return null;
            int hh = int.Parse(m.Groups[1].Value), mm = int.Parse(m.Groups[2].Value);
            if (hh > 24 || mm > 59) return null;

            var rest = Norm(m.Groups[3].Value);
            decimal? fare = null;

            var fares = FareRe.Matches(rest);
            if (fares.Count > 0)
            {
                fare = ToDecimal(fares[0].Groups[1].Value);
                rest = FareRe.Replace(rest, " ").Trim();
            }
            if (fare == null)
            {
                var pm = PlainFareRe.Match(rest);
                if (pm.Success)
                {
                    fare = ToDecimal(pm.Groups[1].Value);
                    rest = (rest.Substring(0, pm.Index) + " " + rest.Substring(pm.Index + pm.Length)).Trim();
                }
            }

            // 动作词（取最靠前的）
            int actIdx = -1;
            string act = "";
            foreach (var w in Actions)
            {
                int i = rest.IndexOf(w, StringComparison.Ordinal);
                if (i >= 0 && (actIdx < 0 || i < actIdx))
                {
                    actIdx = i;
                    act = w;
                }
            }

            string station, remain;
            if (act != "")
            {
                act = act.Contains("上") ? "上车" : "下车";
                station = rest.Substring(0, actIdx).Trim(' ', ',', '，', '、', '·', '|', '_', '-', '—', '–');
                remain = rest.Substring(actIdx + 2).Trim();
            }
            else
            {
                station = "";
                remain = rest;
            }

            string route, vehicle;
            SplitRouteVehicle(remain, out route, out vehicle);

            return new ItineraryEntry
            {
                time = string.Format("{0:00}:{1:00}", hh, mm),
                station = station,
                action = act == "" ? (route != "" || vehicle != "" ? "上车" : "") : act,
                line = route,
                vehicle = vehicle,
                fare = fare,
                note = ""
            };
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
            if (toks.Count >= 2 && VehOnlyRe.IsMatch(toks[toks.Count - 1]))
            {
                vehicle = toks[toks.Count - 1];
                toks.RemoveAt(toks.Count - 1);
                text = string.Join(" ", toks);
            }

            string segFrom = "", segTo = "";
            var parts = SegSplitRe.Split(text, 2);
            var head = parts[0].Trim();
            if (parts.Length == 2) segTo = parts[1].Trim(' ', ',', '，', '、');

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
                    if (TotalRe.IsMatch(line) && !TimeRe.IsMatch(line)) inPov = false;
                    else
                    {
                        var item = ParsePovLine(line);
                        if (item != null) pov.Add(item);
                        continue;
                    }
                }

                if (TotalRe.IsMatch(line) && !TimeRe.IsMatch(line))
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
                if (ent != null) { entries.Add(ent); continue; }

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
                    if (VehOnlyRe.IsMatch(line) && string.IsNullOrEmpty(last.vehicle))
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

        public const string SampleText =
@"12.6 行程

12:00 人民中路上车 611_65028 2.00¥
12:45 无锡汽车站(锡沪西路) 上车 19路快线 11055 0.00¥
14:01 公交梅村总站上车 162_15811 0.00¥
14:59 团结路公交停车场上车 130_91879 0.00¥
16:28 海岸城公交停车场上车 888 都市观光线 75986 0.00¥
总计 4h28min 花费 2.00¥

POV:
【4K】19路快线 无锡汽车站(锡沪西路)-公交梅村总站 11055
【广角】162 公交梅村总站-团结路(先锋路) 15811
130 团结路公交停车场-海岸城公交停车场 91879
【4K广角】888都市观光线 海岸城公交停车场-胜利门 75986
";
    }
}
