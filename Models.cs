using System;
using System.Collections.Generic;
using System.Linq;

namespace TripBook
{
    /// <summary>一次乘车/换乘记录</summary>
    public class ItineraryEntry
    {
        public string time { get; set; } = "";
        public int day { get; set; }
        public int abs_min { get; set; }
        public string station { get; set; } = "";
        public string action { get; set; } = "";
        public string line { get; set; } = "";
        public string vehicle { get; set; } = "";
        public decimal? fare { get; set; }
        public string note { get; set; } = "";
        /// <summary>是否为「时间标记」行（如换乘优惠刷新），不参与上下车统计</summary>
        public bool marker { get; set; }

        public bool IsBoard { get { return action == "上车" || action == "进站"; } }
        public bool IsAlight { get { return action == "下车" || action == "出站"; } }
        public bool IsMarker { get { return marker; } }
        public string TimeDisp { get { return time; } }
        public string DayMark { get { return day > 0 ? "+" + day + "天" : ""; } }
    }

    /// <summary>POV / 机位片段</summary>
    public class PovItem
    {
        public string tag { get; set; } = "";
        public string line { get; set; } = "";
        public string from { get; set; } = "";
        public string to { get; set; } = "";
        public string vehicle { get; set; } = "";
        /// <summary>片段时长（分钟），来自记录里的「70min」这类写法，可缺省</summary>
        public int? dur_min { get; set; }
        public string raw { get; set; } = "";

        public string DurText { get { return dur_min == null ? "" : Fmt.Duration(dur_min); } }
        public bool HasDur { get { return dur_min != null; } }

        public string Segment
        {
            get
            {
                if (string.IsNullOrEmpty(from) && string.IsNullOrEmpty(to)) return "";
                return (string.IsNullOrEmpty(from) ? "?" : from) + " → " +
                       (string.IsNullOrEmpty(to) ? "?" : to);
            }
        }
        public bool HasSegment { get { return !string.IsNullOrEmpty(Segment); } }
        public string TagVisible { get { return string.IsNullOrEmpty(tag) ? "Collapsed" : "Visible"; } }
    }

    /// <summary>总计时长/花费</summary>
    public class SummaryInfo
    {
        public string dur_text { get; set; } = "";
        public decimal? fare { get; set; }
        public string raw { get; set; } = "";
    }

    /// <summary>一条完整行程</summary>
    public class Journey
    {
        public string id { get; set; } = "";
        public string title { get; set; } = "";
        public string date { get; set; } = "";
        public List<ItineraryEntry> entries { get; set; } = new List<ItineraryEntry>();
        public List<PovItem> pov { get; set; } = new List<PovItem>();
        public SummaryInfo summary { get; set; } = new SummaryInfo();
        public List<string> notes { get; set; } = new List<string>();
        public string source_text { get; set; } = "";
        public string created_at { get; set; } = "";
        public string updated_at { get; set; } = "";

        public Journey Clone()
        {
            var j = (Journey)MemberwiseClone();
            j.entries = entries.Select(e => new ItineraryEntry
            {
                time = e.time, day = e.day, abs_min = e.abs_min, station = e.station,
                action = e.action, line = e.line, vehicle = e.vehicle,
                fare = e.fare, note = e.note, marker = e.marker
            }).ToList();
            j.pov = pov.Select(p => new PovItem
            {
                tag = p.tag, line = p.line, from = p.from, to = p.to,
                vehicle = p.vehicle, dur_min = p.dur_min, raw = p.raw
            }).ToList();
            j.notes = new List<string>(notes);
            j.summary = new SummaryInfo
            {
                dur_text = summary?.dur_text, fare = summary?.fare, raw = summary?.raw
            };
            return j;
        }
    }

    /// <summary>统计指标</summary>
    public class JourneyStats
    {
        public int Count { get; private set; }
        public string Start { get; private set; } = "";
        public string End { get; private set; } = "";
        public int? DurationMin { get; private set; }
        public decimal Fare { get; private set; }
        public int PaidRides { get; private set; }
        public List<ItineraryEntry> Rides { get; private set; } = new List<ItineraryEntry>();
        public List<string> Lines { get; private set; } = new List<string>();
        public List<string> Vehicles { get; private set; } = new List<string>();
        public int Transfers { get { return Math.Max(0, Rides.Count - 1); } }
        public int MarkerCount { get; private set; }

        public JourneyStats(IEnumerable<ItineraryEntry> entries)
        {
            var all = (entries ?? Enumerable.Empty<ItineraryEntry>()).ToList();
            MarkerCount = all.Count(e => e.marker);
            var rows = all
                .Where(e => !string.IsNullOrEmpty(e.time) && !e.marker)
                .OrderBy(e => e.abs_min)
                .ToList();
            Count = rows.Count;
            if (rows.Count > 0)
            {
                Start = rows[0].time;
                End = rows[rows.Count - 1].time;
            }
            Rides = rows.Where(e => e.action == "上车" || e.action == "进站").ToList();
            if (rows.Count >= 2)
                DurationMin = Math.Max(0, rows[rows.Count - 1].abs_min - rows[0].abs_min);

            Fare = rows.Sum(e => e.fare ?? 0m);
            PaidRides = rows.Count(e => (e.fare ?? 0m) > 0);

            foreach (var e in rows)
            {
                var ln = (e.line ?? "").Trim();
                if (ln.Length > 0 && !Lines.Contains(ln)) Lines.Add(ln);
                var vh = (e.vehicle ?? "").Trim();
                if (vh.Length > 0 && !Vehicles.Contains(vh)) Vehicles.Add(vh);
            }
        }

        public string SummaryLine()
        {
            var bits = new List<string>();
            if (DurationMin != null) bits.Add(Fmt.Duration(DurationMin));
            if (Rides.Count > 0) bits.Add(Rides.Count + " 次乘车");
            if (Fare > 0) bits.Add("¥" + Fmt.Money(Fare));
            return bits.Count == 0 ? "暂无记录" : string.Join(" · ", bits);
        }
    }

    /// <summary>POV 汇总里的一行（纯数据，不依赖 WPF）</summary>
    public class PovSummaryRow
    {
        public string Date { get; set; } = "";
        public string JourneyTitle { get; set; } = "";
        public string Line { get; set; } = "";
        public string Segment { get; set; } = "";
        public string Vehicle { get; set; } = "";
        public string Tag { get; set; } = "";
        public string Dur { get; set; } = "";
    }

    /// <summary>跨行程的 POV 总结（纯逻辑，可在无 WPF 环境下测试）</summary>
    public class PovSummary
    {
        public int SegmentCount { get; private set; }
        public int LineCount { get; private set; }
        public int VehicleCount { get; private set; }
        public int JourneyCount { get; private set; }
        public List<PovSummaryRow> Rows { get; private set; } = new List<PovSummaryRow>();
        public bool HasRows { get { return Rows.Count > 0; } }

        public static PovSummary Build(IEnumerable<Journey> journeys)
        {
            var s = new PovSummary();
            var all = (journeys ?? Enumerable.Empty<Journey>()).Where(j => j != null).ToList();
            var lines = new HashSet<string>();
            var vehicles = new HashSet<string>();
            int covered = 0;

            foreach (var j in all.OrderByDescending(x => x.date ?? "").ThenBy(x => x.title ?? ""))
            {
                var pov = j.pov ?? new List<PovItem>();
                if (pov.Count > 0) covered++;
                foreach (var p in pov)
                {
                    if (p == null) continue;
                    var ln = (p.line ?? "").Trim();
                    var vh = (p.vehicle ?? "").Trim();
                    if (ln.Length > 0) lines.Add(ln);
                    if (vh.Length > 0) vehicles.Add(vh);

                    s.Rows.Add(new PovSummaryRow
                    {
                        Date = (j.date ?? "").Length >= 10 ? j.date.Substring(5) : (j.date ?? ""),
                        JourneyTitle = j.title ?? "",
                        Line = ln,
                        Segment = p.Segment ?? "",
                        Vehicle = vh,
                        Tag = p.tag ?? "",
                        Dur = p.DurText ?? ""
                    });
                }
            }

            s.SegmentCount = s.Rows.Count;
            s.LineCount = lines.Count;
            s.VehicleCount = vehicles.Count;
            s.JourneyCount = covered;
            return s;
        }
    }

    public static class Fmt
    {
        public static string Duration(int? minutes)
        {
            if (minutes == null) return "—";
            int m = Math.Max(0, minutes.Value);
            int h = m / 60, r = m % 60;
            if (h > 0 && r > 0) return h + "h" + r.ToString("00") + "min";
            if (h > 0) return h + "h00min";
            return r + "min";
        }

        public static string Duration(double? minutes)
        {
            if (minutes == null) return "—";
            return Duration((int?)Math.Round(minutes.Value));
        }

        public static string Money(decimal? v)
        {
            if (v == null) return "—";
            return v.Value.ToString("0.00");
        }

        public static string DateCn(string iso)
        {
            DateTime d;
            if (!DateTime.TryParse(iso, out d)) return iso ?? "";
            return d.Year + "年" + d.Month + "月" + d.Day + "日";
        }

        public static string WeekDayCn(string iso)
        {
            DateTime d;
            if (!DateTime.TryParse(iso, out d)) return "";
            return "周" + "日一二三四五六"[(int)d.DayOfWeek];
        }
    }
}
