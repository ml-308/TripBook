using System;
using System.Collections.Generic;
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

    static void Dump(Journey j)
    {
        Console.WriteLine("title=" + j.title + " date=" + j.date +
                          " entries=" + j.entries.Count + " pov=" + j.pov.Count);
        foreach (var e in j.entries)
            Console.WriteLine("  " + e.time + " " + e.action + " | " + e.station +
                              " | " + e.line + " | " + e.vehicle + " | " + e.fare +
                              (e.marker ? " [标记]" : ""));
        foreach (var p in j.pov)
            Console.WriteLine("  POV [" + p.tag + "] " + p.line + " | " + p.from + " -> " + p.to + " | " + p.vehicle);
    }

    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // 1. 新版示例：手机备忘录风格（含地铁进出站合并行、进站/出站、换乘优惠标记、-- POV）
        Console.WriteLine("== 示例（新版）==");
        var j = ItineraryParser.Parse(ItineraryParser.SampleText);
        Dump(j);
        Check(j.title == "11.1行程", "标题 11.1行程");
        Check(j.date.EndsWith("11-01"), "日期解析为 11-01");
        Check(j.entries.Count == 13, "共 13 条（9 上车 + 地铁拆 2 + 2 标记）");
        Check(j.pov.Count == 2, "2 条 POV");
        Check(j.entries.Count(e => e.marker) == 2, "2 个时间标记行");

        // 地铁进出站合并行 -> 拆成进站 + 出站
        var board = j.entries.First(e => e.station == "无锡东站");
        var alight = j.entries.First(e => e.station == "云林");
        Check(board.action == "进站" && board.line == "地铁二号线" && board.vehicle == "0212", "拆分：进站 无锡东站 地铁二号线 0212");
        Check(alight.action == "出站" && alight.line == "地铁二号线" && alight.fare == 1.00m, "拆分：出站 云林（线路推断 + 票价 1.00）");

        // 标记行
        Check(j.entries.Any(e => e.marker && e.station == "刷新换乘优惠"), "标记行：刷新换乘优惠");

        // 尾部字母车号
        Check(j.entries.Any(e => e.vehicle == "08025D" && e.line == "763"), "尾部字母车号 08025D / 线路 763");

        // POV 用 -- 分隔（括号按归一化规则为半角）
        Check(j.pov[0].from.Contains("古华山路") && j.pov[0].to == "东湖塘" && j.pov[0].vehicle == "82615", "POV -- 分隔 + 车号");
        Check(j.pov[1].from == "二泉东路" && j.pov[1].to == "太湖大道", "POV 第二行 -- 分隔");

        var st = new JourneyStats(j.entries);
        Check(st.DurationMin == 330, "时长 330 分钟（11:56→17:26）");
        Check(st.Fare == 3.00m, "总花费 3.00（2.00 + 1.00）");
        Check(st.Rides.Count == 10, "乘车次数 10（9 上车 + 1 进站）");
        Check(st.MarkerCount == 2, "标记数 2");

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
        var st3 = new JourneyStats(j3.entries);
        Check(st3.DurationMin == 110, "跨零点时长 110");
        Check(st3.Rides.Count == 2, "2 次上车");

        // 4. 全角 + 无标题
        var j4 = ItineraryParser.Parse("７:５０ 汽车站上车 5路 A1234 1.00 元");
        Check(j4.entries.Count == 1 && j4.entries[0].time == "07:50", "全角时间");
        Check(j4.entries[0].vehicle == "A1234" && j4.entries[0].fare == 1.00m, "字母车号 + 元");
        Check(j4.title.Contains("行程"), "无标题自动生成");

        // 5. 地铁出站无线路，应沿用进站线路
        var j5 = ItineraryParser.Parse("地铁日\n08:00 人民广场进站 1号线 A123 行程中 08:25 徐家汇出站 3.00¥");
        var a5 = j5.entries.First(e => e.action == "出站");
        Check(a5.line == "1号线" && a5.fare == 3.00m, "出站沿用进站线路 + 票价");

        // 6. 批量解析（模拟批量上传多个 txt）
        Console.WriteLine("== 批量 ==");
        var texts = new[]
        {
            "10.1 行程\n09:00 甲上车 1路 A1 2.00¥",
            "10.2 行程\n10:00 乙上车 2路 B2 1.00¥",
            "不是行程文本，无时间"
        };
        int total = 0, valid = 0;
        foreach (var t in texts)
        {
            var pj = ItineraryParser.Parse(t);
            total += pj.entries.Count;
            if (pj.entries.Count > 0) valid++;
        }
        Check(valid == 2 && total == 2, "批量解析：2 个有效、共 2 条记录");

        // 7. 按 txt 文件名判断行程日期
        Console.WriteLine("== 文件名日期 ==");
        var today = new DateTime(2026, 11, 8);

        var h = ItineraryParser.ParseDateHint(@"C:\temp\2024-02-09 行程.txt");
        Check(h.HasFull && h.Year == 2024 && h.Month == 2 && h.Day == 9, "文件名含路径的完整日期");

        var jf1 = ItineraryParser.Parse("行程\n09:00 甲上车 1路 10001 2.00¥", today, "2026-02-09.txt");
        Check(jf1.date == "2026-02-09", "正文无日期时用文件名完整日期");

        var jf2 = ItineraryParser.Parse("2.9行程\n09:00 甲上车 1路 10001 2.00¥", today, "2024_行程.txt");
        Check(jf2.date == "2024-02-09", "文件名里的年份校正正文「2.9」");

        var jf3 = ItineraryParser.Parse("2.9行程\n09:00 甲上车 1路 10001 2.00¥", today, "2023-11-01.txt");
        Check(jf3.date == "2023-02-09", "正文月日优先，文件名只提供年份");

        var jf4 = ItineraryParser.Parse("随手记\n09:00 甲上车 1路 10001 2.00¥", today, "11.1行程.txt");
        Check(jf4.date == "2026-11-01", "文件名「11.1行程」月日兜底");

        var jf5 = ItineraryParser.Parse("出行\n2025年12月6日\n09:00 甲上车 1路 10001 2.00¥",
                                       today, "2026-01-01.txt");
        Check(jf5.date == "2025-12-06", "正文完整日期行优先于文件名");

        var jf6 = ItineraryParser.Parse("2月29日行程\n09:00 甲上车 1路 10001 2.00¥", today, "2023年行程.txt");
        Check(jf6.date == "2023-02-28", "平年 2-29 自动收敛为 2-28");

        // 8. 读取容错（格式不统一，智能判断）
        Console.WriteLine("== 读取容错 ==");
        var je1 = ItineraryParser.Parse("11点56分 甲上车 1路 10001 ¥2.00", today);
        Check(je1.entries.Count == 1 && je1.entries[0].time == "11:56" &&
              je1.entries[0].fare == 2.00m, "中文时间「11点56分」+ ¥ 前置费用");

        var je2 = ItineraryParser.Parse("09:00 上车 八佰伴 703 85060 2.00¥", today);
        Check(je2.entries.Count == 1 && je2.entries[0].station == "八佰伴" &&
              je2.entries[0].line == "703" && je2.entries[0].vehicle == "85060",
              "动作写在站点前也能补回站点/线路/车号");

        var je3 = ItineraryParser.Parse("10:00 乙乘车 5路 车号:12345 1.00¥", today);
        Check(je3.entries.Count == 1 && je3.entries[0].action == "上车" &&
              je3.entries[0].vehicle == "12345", "同义动作「乘车」+ 车号标签");

        var je4 = ItineraryParser.Parse("----\n11:00 丙上车 9路 99999 0.00¥\n====", today);
        Check(je4.entries.Count == 1 && je4.notes.Count == 0, "分隔线不进备注");

        var je5 = ItineraryParser.Parse("1156 丁上车 2路 22222 1.00¥", today);
        Check(je5.entries.Count == 1 && je5.entries[0].time == "11:56", "连写时间 1156");

        // 9. POV 总结（跨行程聚合）
        Console.WriteLine("== POV 总结 ==");
        var ps = PovSummary.Build(new List<Journey> { j });
        Check(ps.SegmentCount == 2 && ps.LineCount == 2 && ps.VehicleCount == 2 &&
              ps.JourneyCount == 1 && ps.HasRows, "单条行程 POV 聚合");
        var ps2 = PovSummary.Build(new List<Journey> { j, j });
        Check(ps2.SegmentCount == 4 && ps2.JourneyCount == 2, "跨行程 POV 聚合");
        var psEmpty = PovSummary.Build(new List<Journey> { je1 });
        Check(!psEmpty.HasRows && psEmpty.SegmentCount == 0, "无 POV 时聚合为空");

        // 10. 导出 HTML
        var html = HtmlExporter.Build(j);
        Check(html.Contains("11.1行程") && html.Contains("线路接力") && html.Contains("</html>"), "HTML 导出");
        System.IO.File.WriteAllText("sample_report.html", html);

        Console.WriteLine(fails == 0 ? "\n全部通过" : "\n失败 " + fails + " 项");
        Environment.Exit(fails == 0 ? 0 : 1);
    }
}
