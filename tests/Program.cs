using System;
using System.Linq;
using TripBook;

class Program
{
    static int fails = 0;
    static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + what);
        if (!ok) fails++;
    }

    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // 1. 示例文本
        var j = ItineraryParser.Parse(ItineraryParser.SampleText);
        Console.WriteLine("== 示例 ==");
        Console.WriteLine("title=" + j.title + " date=" + j.date + " entries=" + j.entries.Count + " pov=" + j.pov.Count);
        foreach (var e in j.entries)
            Console.WriteLine("  " + e.time + " " + e.action + " | " + e.station + " | " + e.line + " | " + e.vehicle + " | " + e.fare);
        foreach (var p in j.pov)
            Console.WriteLine("  POV [" + p.tag + "] " + p.line + " | " + p.from + " -> " + p.to + " | " + p.vehicle);
        Check(j.entries.Count == 5, "5 条记录");
        Check(j.pov.Count == 4, "4 条 POV");
        Check(j.title == "12.6 行程", "标题");
        Check(j.date == "2026-12-06", "日期");
        Check(j.entries[0].line == "611" && j.entries[0].vehicle == "65028" && j.entries[0].fare == 2.00m, "611_65028 2.00");
        Check(j.entries[4].line == "888 都市观光线", "多词线路名");
        Check(j.pov[0].from == "无锡汽车站(锡沪西路)" && j.pov[0].to == "公交梅村总站", "POV 区段");
        var st = new JourneyStats(j.entries);
        Check(st.DurationMin == 268, "时长 268 分钟");
        Check(st.Fare == 2.00m, "总花费 2.00");
        Check(st.Waits.Count == 4 && st.Waits[0].Minutes == 45, "候车间隔");
        Check(st.Lines.Count == 5, "5 条线路");

        // 2. 折行 + 下车 + 无¥
        Console.WriteLine("== 折行 ==");
        var j2 = ItineraryParser.Parse("12.6 行程\n12:00 人民中路上车 611_65028 2.00\n0.00¥\n13:30 锡惠公园下车 611_65028\n总计 1h30min 花费 2.00元");
        Check(j2.entries.Count == 2, "2 条");
        Check(j2.entries[0].fare == 2.00m && j2.entries[0].line == "611", "无符号费用识别");
        Check(j2.entries[1].action == "下车", "下车");
        Check(j2.summary.dur_text == "1h30min", "总计时长");

        // 3. 跨零点
        Console.WriteLine("== 跨零点 ==");
        var j3 = ItineraryParser.Parse("夜间运转\n23:30 火车站上车 K1 10001 2.00¥\n00:40 东站下车 K1 10001\n01:20 东站上车 K2 20002 0.00¥");
        foreach (var e in j3.entries) Console.WriteLine("  " + e.time + " day=" + e.day + " abs=" + e.abs_min);
        var st3 = new JourneyStats(j3.entries);
        Check(st3.DurationMin == 110, "跨零点时长 110");
        Check(st3.Rides.Count == 2, "2 次上车");

        // 4. 全角 + 无标题
        var j4 = ItineraryParser.Parse("７:５０ 汽车站上车 5路 A1234 1.00 元");
        Check(j4.entries.Count == 1 && j4.entries[0].time == "07:50", "全角时间");
        Check(j4.entries[0].vehicle == "A1234" && j4.entries[0].fare == 1.00m, "字母车号 + 元");
        Check(j4.title.Contains("行程"), "无标题自动生成");

        // 5. 导出 HTML
        var html = HtmlExporter.Build(j);
        Check(html.Contains("12.6 行程") && html.Contains("线路接力") && html.Contains("</html>"), "HTML 导出");
        System.IO.File.WriteAllText("sample_report.html", html);

        Console.WriteLine(fails == 0 ? "\n全部通过" : "\n失败 " + fails + " 项");
        Environment.Exit(fails == 0 ? 0 : 1);
    }
}
