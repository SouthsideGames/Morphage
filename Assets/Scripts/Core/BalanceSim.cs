using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Mutagen
{
    /// <summary>One completed bot run in a balance batch (pure data — captured by Game's driver).</summary>
    public class BalanceRunRecord
    {
        public uint seed;
        public string isolationTarget;   // null = sampling run · "" = bite-only baseline · else mutation id
        public int wave, level, kills, ticks;
        public float damageDealt, damageTaken, time;
        public bool reached15;
        public Dictionary<string, int> mutations = new();
    }

    /// <summary>One line of the on-screen balance report.</summary>
    public struct BalanceRow
    {
        public string title;   // mutation name
        public string right;   // headline number (e.g. "+1.4 waves")
        public string detail;  // sample sizes / secondary stats
    }

    /// <summary>
    /// Aggregation + CSV for the Balance Lab. All statistics live here (no sim logic): the driver in
    /// Game.cs produces <see cref="BalanceRunRecord"/>s; this turns them into reports.
    /// CAVEAT (also printed in the reports): bots have no dodging skill or target priority — the data
    /// measures RELATIVE mutation power, which is what balance outlier-hunting needs.
    /// </summary>
    public static class BalanceSim
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ---------------- sampling: with-vs-without comparison across random-draft runs ----------------
        public static List<BalanceRow> BuildSamplingReport(List<BalanceRunRecord> all, List<MutationDef> defs, out string csv)
        {
            var runs = new List<BalanceRunRecord>();
            foreach (var r in all) if (r.isolationTarget == null) runs.Add(r);

            var rows = new List<(BalanceRow row, float lift)>();
            var sb = new StringBuilder();
            sb.AppendLine("id,displayName,rarity,runsWith,avgStacks,avgWaveWith,avgWaveWithout,liftWaves,win15With,win15Without,avgDpsWith");

            foreach (var d in defs)
            {
                string key = string.IsNullOrEmpty(d.move) ? d.id : d.move;
                int nW = 0, nWo = 0, winW = 0, winWo = 0, stacks = 0;
                float waveW = 0f, waveWo = 0f, dpsW = 0f;
                foreach (var r in runs)
                {
                    if (r.mutations.TryGetValue(key, out int s))
                    {
                        nW++; stacks += s; waveW += r.wave;
                        dpsW += r.damageDealt / Mathf.Max(r.time, 1f);
                        if (r.reached15) winW++;
                    }
                    else { nWo++; waveWo += r.wave; if (r.reached15) winWo++; }
                }
                float aW = nW > 0 ? waveW / nW : 0f, aWo = nWo > 0 ? waveWo / nWo : 0f;
                float lift = (nW > 0 && nWo > 0) ? aW - aWo : 0f;
                float pWinW = nW > 0 ? 100f * winW / nW : 0f, pWinWo = nWo > 0 ? 100f * winWo / nWo : 0f;
                float dps = nW > 0 ? dpsW / nW : 0f, avgStk = nW > 0 ? (float)stacks / nW : 0f;

                sb.AppendLine(string.Join(",",
                    d.id, Csv(d.displayName), d.rarity, nW.ToString(Inv), avgStk.ToString("0.00", Inv),
                    aW.ToString("0.00", Inv), aWo.ToString("0.00", Inv), lift.ToString("0.00", Inv),
                    pWinW.ToString("0.0", Inv), pWinWo.ToString("0.0", Inv), dps.ToString("0.0", Inv)));

                rows.Add((new BalanceRow
                {
                    title = d.displayName,
                    right = nW == 0 ? "no data" : lift.ToString("+0.0;-0.0", Inv) + " waves",
                    detail = $"n={nW} · wave {aW.ToString("0.0", Inv)} vs {aWo.ToString("0.0", Inv)} without · win15 {pWinW.ToString("0", Inv)}% vs {pWinWo.ToString("0", Inv)}% · dps {dps.ToString("0", Inv)}",
                }, lift));
            }
            rows.Sort((a, b) => b.lift.CompareTo(a.lift));
            csv = sb.ToString();
            var outRows = new List<BalanceRow>();
            foreach (var r in rows) outRows.Add(r.row);
            return outRows;
        }

        // ---------------- isolation: each mutation force-built, measured against a bite-only baseline ----------------
        public static List<BalanceRow> BuildIsolationReport(List<BalanceRunRecord> all, List<MutationDef> defs, out string csv)
        {
            // group by target
            var groups = new Dictionary<string, List<BalanceRunRecord>>();
            foreach (var r in all)
            {
                if (r.isolationTarget == null) continue;
                if (!groups.TryGetValue(r.isolationTarget, out var list)) groups[r.isolationTarget] = list = new List<BalanceRunRecord>();
                list.Add(r);
            }

            (int n, float wave, float secs, float dps, float kills) Stats(List<BalanceRunRecord> list)
            {
                float w = 0f, t = 0f, d = 0f, k = 0f;
                foreach (var r in list) { w += r.wave; t += r.time; d += r.damageDealt / Mathf.Max(r.time, 1f); k += r.kills; }
                int n = list.Count;
                return n == 0 ? (0, 0f, 0f, 0f, 0f) : (n, w / n, t / n, d / n, k / n);
            }

            var baseline = groups.TryGetValue("", out var bl) ? Stats(bl) : (0, 0f, 0f, 0f, 0f);
            string NameOf(string id)
            {
                foreach (var d in defs) if (d.id == id) return d.displayName;
                return id;
            }

            var rows = new List<(BalanceRow row, float wave)>();
            var sb = new StringBuilder();
            sb.AppendLine("target,displayName,runs,avgWave,deltaWaveVsBaseline,avgSurvivalSec,avgDps,avgKills");
            sb.AppendLine(string.Join(",", "BASELINE", "bite only", baseline.n.ToString(Inv),
                baseline.wave.ToString("0.00", Inv), "0", baseline.secs.ToString("0", Inv),
                baseline.dps.ToString("0.0", Inv), baseline.kills.ToString("0.0", Inv)));

            foreach (var kv in groups)
            {
                if (kv.Key == "") continue;
                var s = Stats(kv.Value);
                float delta = s.wave - baseline.wave;
                sb.AppendLine(string.Join(",", kv.Key, Csv(NameOf(kv.Key)), s.n.ToString(Inv),
                    s.wave.ToString("0.00", Inv), delta.ToString("0.00", Inv), s.secs.ToString("0", Inv),
                    s.dps.ToString("0.0", Inv), s.kills.ToString("0.0", Inv)));
                rows.Add((new BalanceRow
                {
                    title = NameOf(kv.Key),
                    right = delta.ToString("+0.0;-0.0", Inv) + " vs baseline",
                    detail = $"n={s.n} · wave {s.wave.ToString("0.0", Inv)} · {s.secs.ToString("0", Inv)}s · dps {s.dps.ToString("0", Inv)} · kills {s.kills.ToString("0", Inv)}",
                }, s.wave));
            }
            rows.Sort((a, b) => b.wave.CompareTo(a.wave));
            csv = sb.ToString();

            var outRows = new List<BalanceRow>
            {
                new BalanceRow
                {
                    title = "BASELINE (bite only)",
                    right = "wave " + baseline.wave.ToString("0.0", Inv),
                    detail = $"n={baseline.n} · {baseline.secs.ToString("0", Inv)}s · dps {baseline.dps.ToString("0.0", Inv)}",
                },
            };
            foreach (var r in rows) outRows.Add(r.row);
            return outRows;
        }

        // ---------------- raw per-run rows (spreadsheet pivoting) ----------------
        public static string RawCsv(List<BalanceRunRecord> runs)
        {
            var sb = new StringBuilder();
            sb.AppendLine("seed,mode,target,wave,ticks,timeSec,level,kills,damageDealt,damageTaken,reached15,mutations");
            foreach (var r in runs)
            {
                var muts = new StringBuilder();
                foreach (var kv in r.mutations) { if (muts.Length > 0) muts.Append('|'); muts.Append(kv.Key).Append(':').Append(kv.Value); }
                sb.AppendLine(string.Join(",",
                    r.seed.ToString(Inv),
                    r.isolationTarget == null ? "sampling" : "isolation",
                    r.isolationTarget == null ? "" : (r.isolationTarget == "" ? "BASELINE" : r.isolationTarget),
                    r.wave.ToString(Inv), r.ticks.ToString(Inv), r.time.ToString("0.0", Inv),
                    r.level.ToString(Inv), r.kills.ToString(Inv),
                    r.damageDealt.ToString("0", Inv), r.damageTaken.ToString("0", Inv),
                    r.reached15 ? "1" : "0", muts.ToString()));
            }
            return sb.ToString();
        }

        public static void WriteCsv(string fileName, string content)
        {
            try
            {
                string path = Path.Combine(Application.persistentDataPath, fileName);
                File.WriteAllText(path, content);
                Debug.Log($"[MUTAGEN][balance] wrote {path}");
            }
            catch (System.Exception e) { Debug.LogWarning($"[MUTAGEN][balance] CSV write failed: {e.Message}"); }
        }

        public static string ConsoleSummary(List<BalanceRow> sampling, List<BalanceRow> isolation)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[MUTAGEN][balance] ===== BALANCE LAB REPORT =====");
            sb.AppendLine("(bot data: measures RELATIVE power — use to spot outliers, not absolute feel)");
            void Section(string name, List<BalanceRow> rows)
            {
                sb.AppendLine($"--- {name}: strongest ---");
                for (int i = 0; i < rows.Count && i < 5; i++) sb.AppendLine($"  {rows[i].title,-22} {rows[i].right,-18} {rows[i].detail}");
                sb.AppendLine($"--- {name}: weakest ---");
                for (int i = Mathf.Max(0, rows.Count - 5); i < rows.Count; i++) sb.AppendLine($"  {rows[i].title,-22} {rows[i].right,-18} {rows[i].detail}");
            }
            Section("SAMPLING (lift in waves)", sampling);
            Section("ISOLATION (vs bite-only baseline)", isolation);
            sb.AppendLine($"CSVs: {Application.persistentDataPath}");
            return sb.ToString();
        }

        static string Csv(string s) => s != null && s.Contains(",") ? "\"" + s + "\"" : s ?? "";
    }
}
