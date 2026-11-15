namespace GitHubStats;

internal static class Assets
{
    public const string Css = """
:root {
    color-scheme: light dark;
    --bg: #f6f8fa; --card: #ffffff; --text: #1f2328; --muted: #656d76; --border: #d0d7de;
    --link: #0969da; --up: #1a7f37; --down: #cf222e; --accent: #eaeef2; --warn-bg: #fff8c5; --warn-border: #d4a72c;
}
@media (prefers-color-scheme: dark) {
    :root {
        --bg: #0d1117; --card: #161b22; --text: #e6edf3; --muted: #8d96a0; --border: #30363d;
        --link: #4493f8; --up: #3fb950; --down: #f85149; --accent: #21262d; --warn-bg: #3a2d00; --warn-border: #9e6a03;
    }
}
* { box-sizing: border-box; }
body { margin: 0; padding: 24px 16px 48px; background: var(--bg); color: var(--text);
       font: 15px/1.5 system-ui, -apple-system, "Segoe UI", Roboto, sans-serif; }
main { max-width: 1100px; margin: 0 auto; }
a { color: var(--link); text-decoration: none; }
a:hover { text-decoration: underline; }
h1 { font-size: 1.5rem; margin: 0 0 4px; }
h2 { font-size: 1.1rem; margin: 32px 0 10px; }
.sub, .muted { color: var(--muted); }
.sub { margin-bottom: 20px; font-size: .9rem; }
.cards { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 12px; }
.card { background: var(--card); border: 1px solid var(--border); border-radius: 8px; padding: 12px 14px; }
.card .v { font-size: 1.6rem; font-weight: 600; line-height: 1.2; }
.card .l { color: var(--muted); font-size: .8rem; text-transform: uppercase; letter-spacing: .04em; }
.card .d { font-size: .8rem; color: var(--muted); }
.banner { border: 1px solid var(--warn-border); background: var(--warn-bg); border-radius: 8px; padding: 10px 14px; margin-bottom: 16px; }
.wrap { overflow-x: auto; background: var(--card); border: 1px solid var(--border); border-radius: 8px; }
table { border-collapse: collapse; width: 100%; }
th, td { padding: 8px 12px; text-align: left; border-bottom: 1px solid var(--border); vertical-align: top; }
tr:last-child > td { border-bottom: 0; }
th { font-size: .75rem; text-transform: uppercase; letter-spacing: .04em; color: var(--muted); background: var(--accent); white-space: nowrap; }
td.num, th.num { text-align: right; font-variant-numeric: tabular-nums; white-space: nowrap; }
.up { color: var(--up); } .down { color: var(--down); } .zero { color: var(--muted); }
.desc { color: var(--muted); font-size: .85rem; }
.badge { display: inline-block; font-size: .7rem; border: 1px solid var(--border); border-radius: 999px; padding: 0 7px; margin-left: 6px; color: var(--muted); }
.sort { font-size: .85rem; margin: 0 0 8px; color: var(--muted); }
.sort a { margin-right: 10px; } .sort a.active { color: var(--text); font-weight: 600; }
tr.rel td { background: var(--bg); padding: 4px 12px 10px; }
details summary { cursor: pointer; color: var(--muted); font-size: .85rem; }
.rtable { margin-top: 8px; font-size: .88rem; }
.rtable th { background: transparent; }
.assets { margin: 2px 0 0; padding-left: 18px; color: var(--muted); font-size: .82rem; }
footer { margin-top: 28px; color: var(--muted); font-size: .8rem; }

/* per-repo charts: categorical slots 1-3 of the validated palette */
:root { --s1: #2a78d6; --s2: #eb6834; --s3: #1baf7a; }
@media (prefers-color-scheme: dark) { :root { --s1: #3987e5; --s2: #d95926; --s3: #199e70; } }
.k-d { --c: var(--s1); } .k-s { --c: var(--s2); } .k-r { --c: var(--s3); }
.bar { display: flex; flex-wrap: wrap; gap: 6px 22px; align-items: center; margin: 0 0 8px; }
.bar .sort { margin: 0; }
.toggle { font-size: .85rem; cursor: pointer; user-select: none; }
.toggle input { margin-right: 6px; vertical-align: -2px; }
tr.chart { display: none; }
body:has(#showcharts:checked) tr.chart { display: table-row; }
tr.chart td { background: var(--card); padding: 4px 12px 14px; }
.chartbox { max-width: 760px; }
.lgd { display: flex; flex-wrap: wrap; gap: 2px 20px; font-size: .85rem; margin: 2px 0 2px; }
.lg { white-space: nowrap; }
.k { display: inline-block; width: 16px; height: 0; border-top: 2px solid var(--c); vertical-align: middle; margin-right: 6px; border-radius: 2px; }
.pc { color: var(--muted); font-variant-numeric: tabular-nums; }
svg.hchart { width: 100%; height: auto; display: block; touch-action: pan-y; }
svg.hchart:focus-visible { outline: 2px solid var(--link); outline-offset: 2px; border-radius: 4px; }
svg.hchart text { font: 11px system-ui, -apple-system, "Segoe UI", Roboto, sans-serif; fill: var(--muted); }
svg.hchart text.dl { fill: var(--text); }
svg.hchart .g { stroke: var(--border); stroke-width: 1; }
svg.hchart .g.z { stroke: var(--muted); stroke-opacity: .55; }
svg.hchart .ln { fill: none; stroke: var(--c); stroke-width: 2; stroke-linejoin: round; stroke-linecap: round; }
svg.hchart .ed, svg.hchart .hd { fill: var(--c); stroke: var(--card); stroke-width: 2; }
svg.hchart .cross { stroke: var(--muted); stroke-width: 1; }
.cap { color: var(--muted); font-size: .75rem; margin-top: 2px; }
details.tv { margin-top: 6px; }
.tvwrap { max-height: 220px; overflow: auto; margin-top: 6px; border: 1px solid var(--border); border-radius: 6px; }
.tvwrap table { font-size: .82rem; }
.tvwrap th { position: sticky; top: 0; }
#tip { position: fixed; z-index: 10; display: none; pointer-events: none; min-width: 180px; background: var(--card); color: var(--text);
       border: 1px solid var(--border); border-radius: 8px; padding: 8px 10px; font-size: .8rem; box-shadow: 0 4px 14px rgba(0, 0, 0, .18); }
#tip .d { color: var(--muted); margin-bottom: 4px; }
#tip .r { display: flex; align-items: center; gap: 6px; line-height: 1.7; }
#tip .r b { font-weight: 600; min-width: 4ch; font-variant-numeric: tabular-nums; }
#tip .r .n { color: var(--muted); }
#tip .r .p { margin-left: auto; padding-left: 10px; color: var(--muted); font-variant-numeric: tabular-nums; }

/* v1.1: generic series classes, notes, sharing, insight */
.k-1 { --c: var(--s1); } .k-2 { --c: var(--s2); } .k-3 { --c: var(--s3); }
.note { border: 1px solid var(--border); background: var(--card); border-radius: 8px; padding: 8px 14px; margin-bottom: 16px; color: var(--muted); font-size: .88rem; }
.pad { padding: 8px 14px 12px; }
.chart-only { display: none; }
body:has(#showcharts:checked) .chart-only { display: block; }
.two { display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 14px; align-items: start; }
.two.small { gap: 10px 20px; margin: 8px 0 4px; font-size: .88rem; }
.tblock { margin-top: 14px; padding-top: 8px; border-top: 1px solid var(--border); }
.mbar { display: inline-block; width: 60px; height: 6px; background: var(--accent); border-radius: 3px; vertical-align: middle; margin-right: 8px; overflow: hidden; }
.mbar i { display: block; height: 100%; background: var(--s1); border-radius: 3px; }
td.num .mbar { margin-right: 8px; }
details.share { margin: 0 0 16px; background: var(--card); border: 1px solid var(--border); border-radius: 8px; padding: 8px 14px; }
details.share > summary { cursor: pointer; color: var(--text); font-size: .9rem; }
.sharebody { margin-top: 8px; font-size: .88rem; }
.sharebody p { margin: 8px 0; }
.sharebody textarea { width: 100%; max-width: 760px; font: 12px/1.4 ui-monospace, SFMono-Regular, Consolas, monospace; background: var(--bg); color: var(--text); border: 1px solid var(--border); border-radius: 6px; padding: 8px; }
.sharebody button { font: inherit; font-size: .85rem; padding: 3px 12px; border: 1px solid var(--border); border-radius: 6px; background: var(--card); color: var(--text); cursor: pointer; }
.sharebody code { background: var(--accent); border-radius: 4px; padding: 0 4px; font-size: .85em; }
.badge-img { vertical-align: middle; margin-right: 6px; height: 20px; }
""";

    public const string Js = """
(function () {
  var cb = document.getElementById('showcharts');
  if (cb) {
    cb.addEventListener('change', function () {
      try { localStorage.setItem('ghstats.charts', cb.checked ? '1' : '0'); } catch (e) {}
      if (!cb.checked) { var t = document.getElementById('tip'); if (t) t.style.display = 'none'; }
    });
  }
  var cm = document.getElementById('copymd'), ta = document.getElementById('mdblock');
  if (cm && ta) {
    cm.addEventListener('click', function () {
      function done() { cm.textContent = 'Copied'; setTimeout(function () { cm.textContent = 'Copy'; }, 1500); }
      ta.select();
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(ta.value).then(done, function () { try { document.execCommand('copy'); done(); } catch (e) {} });
      } else { try { document.execCommand('copy'); done(); } catch (e) {} }
    });
  }
  var el = document.getElementById('hist-data'), tip = document.getElementById('tip');
  if (!el || !tip) return;
  var D;
  try { D = JSON.parse(el.textContent); } catch (e) { return; }
  function fp(p) {
    if (p === null || p === undefined) return '';
    var a = Math.abs(p);
    if (a < 0.05) return '0%';
    return (p > 0 ? '+' : '\u2212') + (a < 10 ? a.toFixed(1) : Math.round(a).toLocaleString('en-US')) + '%';
  }
  function fv(v) { return v === null || v === undefined ? '\u2013' : v.toLocaleString('en-US'); }

  document.querySelectorAll('svg.hchart').forEach(function (svg) {
    var C = D.c[svg.getAttribute('data-id')];
    if (!C) return;
    var n = C.x.length, hov = svg.querySelector('.hov'), cross = svg.querySelector('.cross'),
        dots = svg.querySelectorAll('.hd'), cur = n - 1, topY = +cross.getAttribute('y1');
    function nearest(vx) {
      var b = 0, bd = 1e9;
      for (var i = 0; i < n; i++) { var d = Math.abs(C.x[i] - vx); if (d < bd) { bd = d; b = i; } }
      return b;
    }
    function render(i) {
      cur = i;
      var x = C.x[i];
      cross.setAttribute('x1', x); cross.setAttribute('x2', x);
      hov.style.display = '';
      tip.textContent = '';
      var head = document.createElement('div');
      head.className = 'd'; head.textContent = C.l[i];
      tip.appendChild(head);
      C.s.forEach(function (s, j) {
        var d = dots[j], y = s.y[i];
        if (y === null || y === undefined) { d.setAttribute('visibility', 'hidden'); }
        else { d.setAttribute('visibility', 'visible'); d.setAttribute('cx', x); d.setAttribute('cy', y); }
        var row = document.createElement('div'); row.className = 'r';
        var key = document.createElement('i'); key.className = 'k ' + s.c;
        var val = document.createElement('b'); val.textContent = fv(s.v[i]);
        var nm = document.createElement('span'); nm.className = 'n'; nm.textContent = s.n;
        var pc = document.createElement('span'); pc.className = 'p'; pc.textContent = s.p ? fp(s.p[i]) : '';
        row.appendChild(key); row.appendChild(val); row.appendChild(nm); row.appendChild(pc);
        tip.appendChild(row);
      });
    }
    function place(cx, cy) {
      tip.style.display = 'block';
      var w = tip.offsetWidth, h = tip.offsetHeight, x = cx + 14, y = cy - h - 12;
      if (x + w > window.innerWidth - 8) x = cx - w - 14;
      if (y < 8) y = cy + 16;
      tip.style.left = Math.max(8, x) + 'px';
      tip.style.top = y + 'px';
    }
    function placeAtIndex(i) {
      var r = svg.getBoundingClientRect();
      place(r.left + C.x[i] / 640 * r.width, r.top + (topY + 24) / 190 * r.height);
    }
    function hide() { hov.style.display = 'none'; tip.style.display = 'none'; }
    svg.addEventListener('pointermove', function (e) {
      var r = svg.getBoundingClientRect();
      render(nearest((e.clientX - r.left) / r.width * 640));
      place(e.clientX, e.clientY);
    });
    svg.addEventListener('pointerleave', hide);
    svg.addEventListener('focus', function () { render(cur); placeAtIndex(cur); });
    svg.addEventListener('blur', hide);
    svg.addEventListener('keydown', function (e) {
      if (e.key !== 'ArrowLeft' && e.key !== 'ArrowRight') return;
      e.preventDefault();
      var i = Math.max(0, Math.min(n - 1, cur + (e.key === 'ArrowLeft' ? -1 : 1)));
      render(i); placeAtIndex(i);
    });
  });
})();
""";
}
