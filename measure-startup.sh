#!/usr/bin/env bash
# Misst die Container-Startup-Zeit (Container-Start bis "Application started"-Logzeile)
# ueber mehrere Durchlaeufe und gibt Einzelwerte + Durchschnitt aus.
set -euo pipefail
export LC_NUMERIC=C

IMAGE="${1:-mcp-dotnet-server:latest}"
RUNS="${2:-5}"
CONTAINER="mcp-startup-timing"

cleanup() {
  docker rm -f "$CONTAINER" >/dev/null 2>&1 || true
}
trap cleanup EXIT

to_epoch() {
  date -d "$1" +%s.%N
}

total=0
for i in $(seq 1 "$RUNS"); do
  cleanup
  docker run -d --name "$CONTAINER" -P "$IMAGE" >/dev/null

  # Auf die "Application started"-Zeile warten (Timeout 10s)
  for _ in $(seq 1 100); do
    if docker logs "$CONTAINER" 2>&1 | grep -q "Application started"; then
      break
    fi
    sleep 0.1
  done

  started_at=$(docker inspect -f '{{.State.StartedAt}}' "$CONTAINER")
  log_at=$(docker logs -t "$CONTAINER" 2>&1 | grep "Application started" | awk '{print $1}')

  if [ -z "$log_at" ]; then
    echo "Run $i: 'Application started' nicht innerhalb 10s gefunden" >&2
    docker logs "$CONTAINER" >&2
    exit 1
  fi

  delta=$(echo "$(to_epoch "$log_at") - $(to_epoch "$started_at")" | bc)
  printf 'Run %d: %.3fs\n' "$i" "$delta"
  total=$(echo "$total + $delta" | bc)
done

avg=$(echo "$total / $RUNS" | bc -l)
printf 'Durchschnitt ueber %d Durchlaeufe: %.3fs\n' "$RUNS" "$avg"
