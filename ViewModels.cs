using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace TripBook
{
    public class ListItemVm
    {
        public Journey J { get; set; }
        public string Day { get; set; } = "--";
        public string Month { get; set; } = "";
        public string Title { get; set; } = "";
        public string Meta { get; set; } = "";

        public static ListItemVm Build(Journey j)
        {
            var st = new JourneyStats(j.entries);
            var vm = new ListItemVm { J = j, Title = j.title };
            DateTime d;
            if (DateTime.TryParse(j.date, out d))
            {
                vm.Day = d.Day.ToString();
                vm.Month = d.Month + "月";
            }
            var meta = st.Rides.Count + " 次乘车 · " + Fmt.Duration(st.DurationMin);
            if (st.Fare > 0) meta += " · ¥" + Fmt.Money(st.Fare);
            vm.Meta = meta;
            return vm;
        }
    }

    public class PillVm
    {
        public string Text { get; set; } = "";
        public Brush Fg { get; set; }
        public Brush Bg { get; set; }
        public Brush Border { get; set; }
        public double FontSize { get; set; } = 11;
    }

    public class StatVm
    {
        public string Value { get; set; } = "";
        public string Label { get; set; } = "";
        public string Hint { get; set; } = "";
        public Brush Accent { get; set; }
        public bool HasHint { get { return !string.IsNullOrEmpty(Hint); } }
    }

    public class ChainVm
    {
        public bool IsArrow { get; set; }
        public string Line { get; set; } = "";
        public string Vehicle { get; set; } = "";
        public Brush Fg { get; set; }
        public Brush Bg { get; set; }
        public Brush Border { get; set; }
        public bool HasVehicle { get { return !string.IsNullOrEmpty(Vehicle); } }
        public string VehicleText { get { return "车 " + Vehicle; } }
    }

    public class EntryVm
    {
        public string Time { get; set; } = "";
        public string DayMark { get; set; } = "";
        public bool HasDayMark { get { return !string.IsNullOrEmpty(DayMark); } }
        public string Action { get; set; } = "";
        public bool HasAction { get { return !string.IsNullOrEmpty(Action); } }
        public string Line { get; set; } = "";
        public bool HasLine { get { return !string.IsNullOrEmpty(Line); } }
        public string Vehicle { get; set; } = "";
        public bool HasVehicle { get { return !string.IsNullOrEmpty(Vehicle); } }
        public string VehicleText { get { return "车 " + Vehicle; } }
        public string Fare { get; set; } = "";
        public bool HasFare { get { return !string.IsNullOrEmpty(Fare); } }
        public Brush FareBrush { get; set; }
        public string Station { get; set; } = "";
        public string Note { get; set; } = "";
        public bool HasNote { get { return !string.IsNullOrEmpty(Note); } }
        public Brush ActionFg { get; set; }
        public Brush ActionBg { get; set; }
        public Brush ActionBorder { get; set; }
        public Brush LineFg { get; set; }
        public Brush LineBg { get; set; }
        public Brush LineBorder { get; set; }
        public Brush DotBrush { get; set; }
        public bool ShowRailTop { get; set; }
        public bool ShowRailBottom { get; set; }
        public bool IsMarker { get; set; }
        public string MarkerText { get; set; } = "";

        public static EntryVm Build(ItineraryEntry e, bool first, bool last)
        {
            if (e.marker)
            {
                return new EntryVm
                {
                    Time = e.time,
                    IsMarker = true,
                    MarkerText = e.station,
                    ShowRailTop = false,
                    ShowRailBottom = false
                };
            }
            var color = Palette.ActionColor(e.action);
            var lc = Palette.LineColorString(e.line);
            return new EntryVm
            {
                Time = e.time,
                DayMark = e.DayMark,
                Action = e.action,
                Line = e.line,
                Vehicle = e.vehicle,
                Fare = e.fare == null ? "" : "¥ " + Fmt.Money(e.fare),
                FareBrush = (e.fare ?? 0) > 0 ? Palette.Brush(Palette.Amber) : Palette.Brush(Palette.TextDim),
                Station = string.IsNullOrEmpty(e.station) ? "" : e.station,
                Note = e.note,
                ActionFg = Palette.Brush(color),
                ActionBg = Palette.BrushA(color, 0.16),
                ActionBorder = Palette.BrushA(color, 0.30),
                LineFg = Palette.Brush(lc),
                LineBg = Palette.BrushA(lc, 0.16),
                LineBorder = Palette.BrushA(lc, 0.32),
                DotBrush = Palette.Brush(color),
                ShowRailTop = !first,
                ShowRailBottom = !last
            };
        }
    }

    public class PovVm
    {
        public string Tag { get; set; } = "";
        public bool HasTag { get { return !string.IsNullOrEmpty(Tag); } }
        public string Line { get; set; } = "";
        public Brush LineFg { get; set; }
        public string Vehicle { get; set; } = "";
        public bool HasVehicle { get { return !string.IsNullOrEmpty(Vehicle); } }
        public string Segment { get; set; } = "";
        public bool HasSegment { get { return !string.IsNullOrEmpty(Segment); } }
    }

    public class JourneyVm
    {
        public Journey Model { get; set; }
        public string Kicker { get; set; } = "";
        public string Title { get; set; } = "";
        public List<PillVm> Chips { get; set; } = new List<PillVm>();
        public List<StatVm> Stats { get; set; } = new List<StatVm>();
        public List<ChainVm> Chain { get; set; } = new List<ChainVm>();
        public List<EntryVm> Entries { get; set; } = new List<EntryVm>();
        public List<PovVm> Pov { get; set; } = new List<PovVm>();
        public List<string> Notes { get; set; } = new List<string>();
        public int PovSegmentCount { get; set; }
        public int PovLineCount { get; set; }
        public int PovVehicleCount { get; set; }
        public int PovTagCount { get; set; }
        public string PovSubtitle { get; set; } = "";

        public bool HasChain { get { return Chain.Count > 0; } }
        public bool HasPov { get { return Pov.Count > 0; } }
        public bool HasNotes { get { return Notes.Count > 0; } }

        public static JourneyVm Build(Journey j)
        {
            var st = new JourneyStats(j.entries);
            var vm = new JourneyVm { Model = j, Title = j.title };
            vm.Kicker = Fmt.DateCn(j.date) + " · " + Fmt.WeekDayCn(j.date);

            vm.Chips.Add(new PillVm
            {
                Text = st.Rides.Count + " 次乘车",
                Fg = Palette.Brush(Palette.Teal),
                Bg = Palette.BrushA(Palette.Teal, 0.14),
                Border = Palette.BrushA(Palette.Teal, 0.30)
            });
            if (st.Transfers > 0)
                vm.Chips.Add(new PillVm
                {
                    Text = st.Transfers + " 次换乘",
                    Fg = Palette.Brush(Palette.Accent),
                    Bg = Palette.BrushA(Palette.Accent, 0.14),
                    Border = Palette.BrushA(Palette.Accent, 0.30)
                });
            if (st.DurationMin != null)
                vm.Chips.Add(new PillVm
                {
                    Text = Fmt.Duration(st.DurationMin),
                    Fg = Palette.Brush(Palette.Amber),
                    Bg = Palette.BrushA(Palette.Amber, 0.13),
                    Border = Palette.BrushA(Palette.Amber, 0.28)
                });
            vm.Chips.Add(new PillVm
            {
                Text = "¥ " + Fmt.Money(st.Fare),
                Fg = Palette.Brush(Palette.Amber),
                Bg = Palette.BrushA(Palette.Amber, 0.10),
                Border = Palette.BrushA(Palette.Amber, 0.24)
            });

            vm.Stats.Add(new StatVm
            {
                Value = st.DurationMin != null ? Fmt.Duration(st.DurationMin) : "—",
                Label = "总时长",
                Hint = st.Start != "" ? st.Start + " → " + st.End : "",
                Accent = Palette.Brush(Palette.Accent)
            });
            vm.Stats.Add(new StatVm
            {
                Value = st.Rides.Count.ToString(),
                Label = "乘车次数",
                Hint = st.Transfers > 0 ? "换乘 " + st.Transfers + " 次" : "直达",
                Accent = Palette.Brush(Palette.Teal)
            });
            vm.Stats.Add(new StatVm
            {
                Value = "¥ " + Fmt.Money(st.Fare),
                Label = "总花费",
                Hint = st.PaidRides > 0 ? st.PaidRides + " 次付费" : "免费",
                Accent = Palette.Brush(Palette.Amber)
            });
            vm.Stats.Add(new StatVm
            {
                Value = st.Lines.Count + " / " + st.Vehicles.Count,
                Label = "线路 / 车辆",
                Hint = st.Lines.Count > 0 ? st.Lines[0] + " 起" : "",
                Accent = Palette.Brush(Palette.Violet)
            });

            for (int i = 0; i < st.Rides.Count; i++)
            {
                var r = st.Rides[i];
                var ln = string.IsNullOrEmpty(r.line) ? "—" : r.line;
                var c = Palette.LineColorString(ln);
                vm.Chain.Add(new ChainVm
                {
                    Line = ln,
                    Vehicle = r.vehicle,
                    Fg = Palette.Brush(c),
                    Bg = Palette.BrushA("#FFFFFF", 0.03),
                    Border = Palette.BrushA(c, 0.33)
                });
                if (i < st.Rides.Count - 1)
                    vm.Chain.Add(new ChainVm { IsArrow = true, Fg = Palette.Brush("#3A4658") });
            }

            var ordered = j.entries.OrderBy(e => e.abs_min).ToList();
            for (int i = 0; i < ordered.Count; i++)
                vm.Entries.Add(EntryVm.Build(ordered[i], i == 0, i == ordered.Count - 1));

            var povLines = new HashSet<string>();
            var povVehicles = new HashSet<string>();
            foreach (var p in j.pov)
            {
                vm.Pov.Add(new PovVm
                {
                    Tag = p.tag,
                    Line = p.line,
                    LineFg = Palette.Brush(Palette.LineColorString(p.line)),
                    Vehicle = p.vehicle,
                    Segment = p.Segment
                });
                if (!string.IsNullOrWhiteSpace(p.line)) povLines.Add(p.line.Trim());
                if (!string.IsNullOrWhiteSpace(p.vehicle)) povVehicles.Add(p.vehicle.Trim());
                if (!string.IsNullOrWhiteSpace(p.tag)) vm.PovTagCount++;
            }
            vm.PovSegmentCount = vm.Pov.Count;
            vm.PovLineCount = povLines.Count;
            vm.PovVehicleCount = povVehicles.Count;
            vm.PovSubtitle = vm.Pov.Count > 0
                ? vm.Pov.Count + " 个片段 · " + povLines.Count + " 条线路 · " + povVehicles.Count + " 个车号"
                : "在行程文本里写上 POV 段落，这里会自动汇总";

            vm.Notes = j.notes;
            return vm;
        }
    }

    /// <summary>总览页：行程日期分布图所需数据</summary>
    public class DashBarVm
    {
        public string Date { get; set; } = "";
        public int Count { get; set; }
        public int BarWidth { get; set; }
        public Brush Brush { get; set; }
        public string CountText { get { return Count + " 条"; } }
    }

    /// <summary>总览页 POV 总结里的一行</summary>
    public class PovRowVm
    {
        public string Date { get; set; } = "";
        public string JourneyTitle { get; set; } = "";
        public string Line { get; set; } = "";
        public Brush LineFg { get; set; }
        public string Segment { get; set; } = "";
        public bool HasSegment { get { return !string.IsNullOrEmpty(Segment); } }
        public string Vehicle { get; set; } = "";
        public bool HasVehicle { get { return !string.IsNullOrEmpty(Vehicle); } }
        public string Tag { get; set; } = "";
        public bool HasTag { get { return !string.IsNullOrEmpty(Tag); } }
    }

    /// <summary>跨行程的 POV 总结（数据聚合在 Models.PovSummary，这里只补显示属性）</summary>
    public class PovSummaryVm
    {
        public int SegmentCount { get; set; }
        public int LineCount { get; set; }
        public int VehicleCount { get; set; }
        public int JourneyCount { get; set; }
        public List<PovRowVm> Rows { get; set; } = new List<PovRowVm>();
        public bool HasRows { get { return Rows.Count > 0; } }
        public string Subtitle
        {
            get
            {
                return HasRows
                    ? "覆盖 " + JourneyCount + " 条行程 · " + LineCount + " 条线路 · " + VehicleCount + " 个车号"
                    : "在文本里写上 POV 段落即可自动汇总";
            }
        }

        public static PovSummaryVm Build(List<Journey> all)
        {
            var s = PovSummary.Build(all);
            var vm = new PovSummaryVm
            {
                SegmentCount = s.SegmentCount,
                LineCount = s.LineCount,
                VehicleCount = s.VehicleCount,
                JourneyCount = s.JourneyCount
            };
            foreach (var r in s.Rows)
                vm.Rows.Add(new PovRowVm
                {
                    Date = r.Date,
                    JourneyTitle = r.JourneyTitle,
                    Line = r.Line,
                    LineFg = Palette.Brush(Palette.LineColorString(r.Line)),
                    Segment = r.Segment,
                    Vehicle = r.Vehicle,
                    Tag = r.Tag
                });
            return vm;
        }
    }

    public class DashboardVm
    {
        public int JourneyCount { get; set; }
        public int RecordCount { get; set; }
        public string FirstDate { get; set; } = "—";
        public string LastDate { get; set; } = "—";
        public string DateRange { get { return FirstDate + "  ~  " + LastDate; } }
        public List<DashBarVm> Bars { get; set; } = new List<DashBarVm>();
        public bool HasBars { get { return Bars.Count > 0; } }
        public PovSummaryVm Pov { get; set; } = new PovSummaryVm();
        public bool HasPov { get { return Pov.HasRows; } }

        public static DashboardVm Build(List<Journey> all)
        {
            var vm = new DashboardVm();
            vm.JourneyCount = all.Count;
            vm.Pov = PovSummaryVm.Build(all);
            vm.RecordCount = all.Sum(j => j.entries.Count(e => !e.marker));

            var byDate = new Dictionary<string, int>();
            foreach (var j in all)
                foreach (var e in j.entries)
                    if (!e.marker && !string.IsNullOrEmpty(e.time))
                    {
                        if (!byDate.ContainsKey(j.date)) byDate[j.date] = 0;
                        byDate[j.date]++;
                    }

            var sorted = byDate.OrderBy(k => k.Key).ToList();
            int max = sorted.Count > 0 ? sorted.Max(k => k.Value) : 1;
            const int maxW = 440;
            foreach (var k in sorted)
            {
                vm.Bars.Add(new DashBarVm
                {
                    Date = k.Key.Length >= 10 ? k.Key.Substring(5) : k.Key,
                    Count = k.Value,
                    BarWidth = max <= 0 ? 0 : Math.Max(6, (int)(maxW * k.Value / (double)max)),
                    Brush = Palette.Brush(Palette.LineColorString(k.Key))
                });
            }
            if (sorted.Count > 0)
            {
                vm.FirstDate = Fmt.DateCn(sorted.First().Key);
                vm.LastDate = Fmt.DateCn(sorted.Last().Key);
            }
            return vm;
        }
    }

    /// <summary>把行程还原成可再次编辑的文本</summary>
    public static class JourneyText
    {
        public static string ToText(Journey j)
        {
            if (!string.IsNullOrWhiteSpace(j.source_text)) return j.source_text;
            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(j.title)) sb.AppendLine(j.title).AppendLine();
            foreach (var e in j.entries.OrderBy(x => x.abs_min))
            {
                var parts = new List<string> { e.time };
                if (!string.IsNullOrEmpty(e.station)) parts.Add(e.station + (string.IsNullOrEmpty(e.action) ? "上车" : e.action));
                else if (!string.IsNullOrEmpty(e.action)) parts.Add(e.action);
                if (!string.IsNullOrEmpty(e.line)) parts.Add(e.line);
                if (!string.IsNullOrEmpty(e.vehicle)) parts.Add(e.vehicle);
                if (e.fare != null) parts.Add(e.fare.Value.ToString("0.00") + "¥");
                sb.AppendLine(string.Join(" ", parts));
            }
            if (j.pov.Count > 0)
            {
                sb.AppendLine().AppendLine("POV:");
                foreach (var p in j.pov)
                {
                    var head = string.IsNullOrEmpty(p.tag) ? "" : "【" + p.tag + "】";
                    var seg = (string.IsNullOrEmpty(p.from) && string.IsNullOrEmpty(p.to))
                        ? "" : p.from + "-" + p.to + " ";
                    sb.AppendLine((head + p.line + " " + seg + p.vehicle).Trim());
                }
            }
            return sb.ToString();
        }
    }
}
