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

        public static EntryVm Build(ItineraryEntry e, bool first, bool last)
        {
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
        public List<WaitItem> Waits { get; set; } = new List<WaitItem>();
        public List<PovVm> Pov { get; set; } = new List<PovVm>();
        public List<string> Notes { get; set; } = new List<string>();

        public bool HasChain { get { return Chain.Count > 0; } }
        public bool HasWaits { get { return Waits.Count > 0; } }
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
            if (st.AvgWait != null)
                vm.Stats.Add(new StatVm
                {
                    Value = Fmt.Duration(st.AvgWait),
                    Label = "平均候车",
                    Hint = "最长 " + Fmt.Duration(st.MaxWait),
                    Accent = Palette.Brush(Palette.Sky)
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

            vm.Waits = st.Waits;

            foreach (var p in j.pov)
                vm.Pov.Add(new PovVm
                {
                    Tag = p.tag,
                    Line = p.line,
                    LineFg = Palette.Brush(Palette.LineColorString(p.line)),
                    Vehicle = p.vehicle,
                    Segment = p.Segment
                });

            vm.Notes = j.notes;
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
