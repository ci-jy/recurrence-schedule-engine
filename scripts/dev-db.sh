#!/usr/bin/env bash
# Runs a project-local PostgreSQL cluster on localhost:25432 (same address as the docker compose
# database) for machines without Docker. Usage: scripts/dev-db.sh start|stop|status
set -euo pipefail
cd "$(dirname "$0")/.."
DATA=.pgdata
PORT=25432
BIN=$(command -v pg_ctl >/dev/null && dirname "$(command -v pg_ctl)" || ls -d /usr/lib/postgresql/*/bin | sort -V | tail -1)

case "${1:-start}" in
  start)
    if [ ! -d "$DATA" ]; then
      "$BIN/initdb" -D "$DATA" -U schedule --auth=trust -E UTF8 --no-instructions >/dev/null
    fi
    "$BIN/pg_ctl" -D "$DATA" -l "$DATA/server.log" -w \
      -o "-p $PORT -c listen_addresses=localhost -c unix_socket_directories=''" start
    psql "host=localhost port=$PORT user=schedule dbname=postgres" -tAc \
      "SELECT 1 FROM pg_database WHERE datname='schedule'" | grep -q 1 \
      || psql "host=localhost port=$PORT user=schedule dbname=postgres" -qc "CREATE DATABASE schedule"
    ;;
  stop) "$BIN/pg_ctl" -D "$DATA" -w stop ;;
  status) "$BIN/pg_ctl" -D "$DATA" status ;;
  *) echo "usage: $0 start|stop|status" >&2; exit 2 ;;
esac
