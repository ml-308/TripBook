using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace TripBook
{
    /// <summary>把行程导出为独立的 HTML 报告（可直接分享 / 打印）</summary>
    public static class HtmlExporter
    {
        static string E(string s)
        {
            return WebUtility.HtmlEncode(s ?? "");
        }

        static string Pill(string text, string color, bool solid = false)
        {
            if (solid)
                return "<span class=\"pill\" style=\"background:" + color + ";border-color:" + color +
                       ";color:#0b1017;font-weight:700\">" + E(text) + "</span>";
            return "<span class=\"pill\" style=\"background:" + color + "22;border-color:" + color +
                   "55;color:" + color + "\">" + E(text) + "</span>";
        }

        public static string Build(Journey j)
        {
            var st = new JourneyStats(j.entries);
            var sb = new StringBuilder();

            // 统计卡
            var stats = new List<Tuple<string, string, string>>();
            if (st.DurationMin != null) stats.Add(Tuple.Create("总时长", Fmt.Duration(st.DurationMin), Palette.Accent));
            stats.Add(Tuple.Create("乘车次数", st.Rides.Count + " 次", Palette.Teal));
            stats.Add(Tuple.Create("总花费", "¥ " + Fmt.Money(st.Fare), Palette.Amber));
            stats.Add(Tuple.Create("线路 / 车辆", st.Lines.Count + " / " + st.Vehicles.Count, Palette.Violet));

            var statsHtml = string.Join("", stats.Select(t =>
                "<div class=\"stat\"><div class=\"stat-v\" style=\"color:" + t.Item3 + "\">" + E(t.Item2) +
                "</div><div class=\"stat-l\">" + E(t.Item1) + "</div></div>"));

            // 时间轴
            var rows = new StringBuilder();
            foreach (var e in j.entries)
            {
                var color = Palette.ActionColor(e.action);
                var badges = new StringBuilder();
                if (!string.IsNullOrEmpty(e.action)) badges.Append(Pill(e.action, color, true));
                if (!string.IsNullOrEmpty(e.line)) badges.Append(Pill(e.line, Palette.LineColorString(e.line)));
                if (!string.IsNullOrEmpty(e.vehicle))
                    badges.Append("<span class=\"pill mono\" style=\"background:#ffffff0d;border-color:#ffffff14;color:" +
                                  Palette.TextSub + "\">车 " + E(e.vehicle) + "</span>");
                string fare = "";
                if (e.fare != null)
                    fare = "<div class=\"fare\" style=\"color:" +
                           ((e.fare ?? 0) > 0 ? Palette.Amber : Palette.TextDim) + "\">¥ " +
                           Fmt.Money(e.fare) + "</div>";
                rows.Append(
                    "\n      <div class=\"row\"><div class=\"time mono\" style=\"color:" + color + "\">" + E(e.time) +
                    "</div><div class=\"rail\"><div class=\"dot\" style=\"background:" + color +
                    ";box-shadow:0 0 0 4px " + color + "22\"></div></div>" +
                    "<div class=\"card\"><div class=\"card-top\">" + badges + fare + "</div>" +
                    "<div class=\"station\">" + E(e.station) + "</div>" +
                    (string.IsNullOrEmpty(e.note) ? "" : "<div class=\"note\">" + E(e.note) + "</div>") +
                    "</div></div>");
            }

            // 线路链
            var chain = new StringBuilder();
            for (int i = 0; i < st.Rides.Count; i++)
            {
                var r = st.Rides[i];
                var ln = string.IsNullOrEmpty(r.line) ? "—" : r.line;
                var c = Palette.LineColorString(ln);
                chain.Append("<div class=\"chip\" style=\"border-color:" + c + "55;color:" + c + "\">" + E(ln) +
                             "<small>" + E(r.vehicle) + "</small></div>");
                if (i < st.Rides.Count - 1) chain.Append("<div class=\"arrow\">→</div>");
            }

            // POV
            string povHtml = "";
            if (j.pov.Count > 0)
            {
                var items = new StringBuilder();
                foreach (var p in j.pov)
                {
                    items.Append("<li><div class=\"pov-head\">" +
                                 (string.IsNullOrEmpty(p.tag) ? "" : "<span class=\"tag\">" + E(p.tag) + "</span>") +
                                 "<b style=\"color:" + Palette.LineColorString(p.line) + "\">" + E(p.line) + "</b>" +
                                 (string.IsNullOrEmpty(p.vehicle) ? "" : "<span class=\"mono dim\"> " + E(p.vehicle) + "</span>") +
                                 "</div>" +
                                 (p.HasSegment ? "<div class=\"pov-seg\">" + E(p.Segment) + "</div>" : "") +
                                 "</li>");
                }
                povHtml = "\n  <section><h2>POV 片段</h2><ul class=\"pov\">" + items + "</ul></section>";
            }

            string notesHtml = "";
            if (j.notes.Count > 0)
                notesHtml = "\n  <section><h2>备注</h2><div class=\"notes\">" +
                            string.Join("<br>", j.notes.Select(E)) + "</div></section>";

            string chainHtml = chain.Length > 0
                ? "\n  <section><h2>线路接力</h2><div class=\"chain\">" + chain + "</div></section>"
                : "";

            var html = new StringBuilder();
            html.Append("<!DOCTYPE html>\n<html lang=\"zh-CN\"><head>\n<meta charset=\"utf-8\">\n")
                .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n")
                .Append("<title>").Append(E(j.title)).Append(" · ").Append(E(j.date)).Append("</title>\n")
                .Append(@"<style>
  :root{--bg:__BG__;--card:__CARD__;--border:__BORDER__;--text:__TEXT__;--sub:__SUB__;--dim:__DIM__;--accent:__ACCENT__}
  *{box-sizing:border-box}
  body{margin:0;background:var(--bg);color:var(--text);font-family:'Microsoft YaHei UI','PingFang SC',-apple-system,sans-serif;-webkit-font-smoothing:antialiased}
  .wrap{max-width:880px;margin:0 auto;padding:40px 22px 70px}
  .mono{font-family:Consolas,'Cascadia Mono',monospace}
  .dim{color:var(--dim)}
  header{border-bottom:1px solid var(--border);padding-bottom:22px;margin-bottom:26px}
  .kicker{color:var(--dim);font-size:12px;letter-spacing:3px;text-transform:uppercase}
  h1{font-size:30px;margin:8px 0 6px;letter-spacing:.5px}
  .sub{color:var(--sub);font-size:13px}
  h2{font-size:13px;letter-spacing:2px;color:var(--sub);margin:34px 0 14px;font-weight:700}
  .stats{display:flex;flex-wrap:wrap;gap:10px;margin-top:20px}
  .stat{flex:1 1 140px;background:var(--card);border:1px solid var(--border);border-radius:12px;padding:12px 14px}
  .stat-v{font-family:Consolas,monospace;font-size:20px;font-weight:700}
  .stat-l{color:var(--dim);font-size:11px;margin-top:3px;letter-spacing:1px}
  .row{display:flex;margin-bottom:10px}
  .time{width:56px;flex:none;font-size:13px;font-weight:700;padding-top:14px;text-align:right}
  .rail{width:30px;flex:none;position:relative}
  .rail:before{content:'';position:absolute;left:14px;top:0;bottom:-10px;width:2px;background:#28313F}
  .row:last-child .rail:before{bottom:auto;height:22px}
  .dot{position:absolute;left:10px;top:18px;width:10px;height:10px;border-radius:50%}
  .card{flex:1;background:var(--card);border:1px solid var(--border);border-radius:12px;padding:11px 14px 12px;min-width:0}
  .card-top{display:flex;gap:6px;align-items:center;flex-wrap:wrap}
  .pill{display:inline-block;border:1px solid transparent;border-radius:7px;padding:2px 9px;font-size:11px;white-space:nowrap}
  .fare{margin-left:auto;font-family:Consolas,monospace;font-size:13px;font-weight:600}
  .station{font-size:15px;font-weight:600;margin-top:7px;word-break:break-word}
  .note{color:var(--sub);font-size:12px;margin-top:5px}
  .chain{display:flex;flex-wrap:wrap;gap:8px;align-items:center}
  .chip{border:1px solid;border-radius:10px;padding:8px 12px;font-size:14px;font-weight:700;background:#ffffff08;display:flex;flex-direction:column;line-height:1.2}
  .chip small{font-size:10px;color:var(--dim);font-weight:400;margin-top:3px}
  .arrow{color:#3a4658;font-size:14px}
  .pov{list-style:none;padding:0;margin:0;display:flex;flex-direction:column;gap:8px}
  .pov li{background:var(--card);border:1px solid var(--border);border-radius:10px;padding:11px 14px}
  .pov-head{display:flex;gap:8px;align-items:center;flex-wrap:wrap;font-size:13px}
  .tag{background:__VIOLET__22;border:1px solid __VIOLET__55;color:__VIOLET__;border-radius:6px;padding:1px 7px;font-size:11px}
  .pov-seg{color:var(--sub);font-size:12px;margin-top:5px}
  .notes{background:var(--card);border:1px solid var(--border);border-radius:10px;padding:12px 14px;color:var(--sub);font-size:13px;line-height:1.8}
  footer{margin-top:44px;border-top:1px solid var(--border);padding-top:16px;color:var(--dim);font-size:11px;display:flex;justify-content:space-between}
  @media print{body{background:#fff;color:#111}.card,.stat,.pov li,.notes{background:#fafafa}}
</style></head>
<body><div class=""wrap"">
  <header>
    <div class=""kicker"">行程簿 TripBook</div>
    <h1>__TITLE__</h1>
    <div class=""sub"">__DATECN__ · __WEEKDAY__ &nbsp;|&nbsp; __SUMMARY__</div>
    <div class=""stats"">__STATS__</div>
  </header>
  <section>
    <h2>行程时间轴</h2>
    __ROWS__
  </section>
  __CHAIN__
  __POV__
  __NOTES__
  <footer><span>由 行程簿 TripBook 生成</span><span class=""mono"">__GENERATED__</span></footer>
</div></body></html>"
                    .Replace("__BG__", Palette.Bg).Replace("__CARD__", Palette.Card)
                    .Replace("__BORDER__", Palette.Border).Replace("__TEXT__", Palette.Text)
                    .Replace("__SUB__", Palette.TextSub).Replace("__DIM__", Palette.TextDim)
                    .Replace("__ACCENT__", Palette.Accent).Replace("__TEAL__", Palette.Teal)
                    .Replace("__VIOLET__", Palette.Violet)
                    .Replace("__TITLE__", E(j.title))
                    .Replace("__DATECN__", E(Fmt.DateCn(j.date)))
                    .Replace("__WEEKDAY__", E(Fmt.WeekDayCn(j.date)))
                    .Replace("__SUMMARY__", E(st.SummaryLine()))
                    .Replace("__STATS__", statsHtml)
                    .Replace("__ROWS__", rows.ToString())
                    .Replace("__CHAIN__", chainHtml)
                    .Replace("__POV__", povHtml)
                    .Replace("__NOTES__", notesHtml)
                    .Replace("__GENERATED__", DateTime.Now.ToString("yyyy-MM-dd HH:mm")));

            return html.ToString();
        }
    }
}
