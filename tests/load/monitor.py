#!/usr/bin/env python3
"""Sample CPU and memory of every container (and optionally one local process) while a load test runs.

    python3 tests/load/monitor.py --out tests/load/out/stats-local.csv --pid "$(pgrep -f TrustCabinet.Web)"
    python3 tests/load/monitor.py --ssh tc-server --out tests/load/out/stats-server.csv --stop-mem 85

Writes one CSV row per container per sample: time, name, cpu % (100 = one core), memory MB, plus the host's
memory in use. With --stop-mem, it creates out/STOP when the host's memory in use stays above that percentage for
three samples in a row; run.sh checks for that file and stops sending load. CPU is not a stop signal: the speech
worker is meant to use every core while it transcribes. Stop it with Ctrl+C.
"""
import argparse
import csv
import os
import subprocess
import sys
import time

UNITS = {"B": 1 / 2**20, "KiB": 1 / 1024, "MiB": 1, "GiB": 1024, "kB": 1 / 1000, "MB": 1, "GB": 1000}


def mb(text):
    for unit in sorted(UNITS, key=len, reverse=True):
        if text.endswith(unit):
            return float(text[: -len(unit)]) * UNITS[unit]
    return float(text)


def sample(ssh):
    probe = ("docker stats --no-stream --format '{{.Name}}|{{.CPUPerc}}|{{.MemUsage}}'; echo ---; "
             "nproc; free -m | awk '/Mem:/ {print $2, $3}'")
    if ssh is None:
        probe = probe.replace("nproc; free -m | awk '/Mem:/ {print $2, $3}'",
                              "sysctl -n hw.ncpu; echo $(( $(sysctl -n hw.memsize) / 1048576 )) 0")
    out = subprocess.run(["ssh", ssh, probe] if ssh else ["sh", "-c", probe],
                         capture_output=True, text=True, timeout=60).stdout
    stats, _, host = out.partition("---")
    rows = []
    for line in stats.strip().splitlines():
        name, cpu, mem = line.split("|")
        rows.append((name, float(cpu.rstrip("%")), mb(mem.split("/")[0].strip())))
    lines = host.split()
    return rows, int(lines[0]), int(lines[1]), int(lines[2])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--ssh", help="sample a remote Docker host over ssh instead of this machine")
    ap.add_argument("--pid", type=int, help="also sample this local process (the backend under `dotnet run`)")
    ap.add_argument("--every", type=float, default=2.0)
    ap.add_argument("--out", required=True)
    ap.add_argument("--stop-mem", type=float, help="percent of host memory; write out/STOP when crossed three samples in a row")
    args = ap.parse_args()
    stop_file = os.path.join(os.path.dirname(os.path.abspath(args.out)), "STOP")
    hot = 0
    with open(args.out, "w", newline="") as fh:
        w = csv.writer(fh)
        w.writerow(["time", "name", "cpu_percent", "mem_mb", "host_mem_used_mb", "host_mem_total_mb", "host_cpus"])
        while True:
            t = time.time()
            try:
                rows, cpus, total, used = sample(args.ssh)
            except (subprocess.TimeoutExpired, ValueError, IndexError) as err:
                print(f"sample failed: {err}", file=sys.stderr)
                time.sleep(args.every)
                continue
            if args.pid:
                ps = subprocess.run(["ps", "-o", "%cpu=,rss=", "-p", str(args.pid)], capture_output=True, text=True).stdout.split()
                if ps:
                    rows.append(("backend(dotnet)", float(ps[0]), int(ps[1]) / 1024))
            for name, cpu, mem in rows:
                w.writerow([f"{t:.1f}", name, f"{cpu:.1f}", f"{mem:.0f}", used, total, cpus])
            fh.flush()
            if args.stop_mem and used:
                hot = hot + 1 if used / total * 100 > args.stop_mem else 0
                if hot >= 3 and not os.path.exists(stop_file):
                    open(stop_file, "w").write(f"{time.ctime()}: memory {used}/{total} MB\n")
                    print(f"STOP: memory over {args.stop_mem}%", file=sys.stderr)
            time.sleep(max(0.0, args.every - (time.time() - t)))


if __name__ == "__main__":
    main()
