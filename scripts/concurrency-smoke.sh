#!/usr/bin/env bash
# Two-instance concurrency smoke test (bonus B1).
#
#   docker compose -f docker-compose.yml -f docker-compose.multi.yml up -d --build
#   ./scripts/concurrency-smoke.sh                 # SEATS=20 N=300 PARALLEL=100 by default
#   SEATS=5 N=500 ./scripts/concurrency-smoke.sh
#
# Creates a class with SEATS seats, then fires N applications from N different students at the same time,
# alternating between two backend processes that share one database. Passes only if exactly SEATS
# applications succeed (201), every other one is a clean "class full" 409, nothing is a 5xx or a dropped
# connection, and both instances then report heldSeats == SEATS. Exit code 0 = pass, 1 = fail, 2 = setup problem.
# Needs bash, curl, seq, xargs (Linux, macOS, or Git Bash on Windows).
set -euo pipefail

API1="${API1:-http://localhost:8080}"
API2="${API2:-http://localhost:8081}"
SEATS="${SEATS:-20}"
N="${N:-300}"
PARALLEL="${PARALLEL:-100}"

die() { echo "SETUP PROBLEM: $*" >&2; exit 2; }

for api in "$API1" "$API2"; do
  curl -fsS --max-time 5 "$api/health" >/dev/null 2>&1 \
    || die "$api/health is not answering. Start both instances: docker compose -f docker-compose.yml -f docker-compose.multi.yml up -d --build"
done
(( N > SEATS )) || die "N ($N) must be larger than SEATS ($SEATS) to test the limit"
(( N < 10000000 )) || die "N must be below 10,000,000 (student ids are generated from it)"

json_field() { # json_field <name> : first "name":value from stdin (number or string), without quotes
  sed -nE "s/.*\"$1\":\"?([^\",}]*)\"?.*/\1/p" | head -n1
}

CLASS_NAME="Smoke $(date +%Y%m%d-%H%M%S)-$$-$RANDOM"
CLASS_ID=$(curl -fsS -X POST "$API1/classes" -H 'Content-Type: application/json' \
  -d "{\"name\":\"$CLASS_NAME\",\"totalSeats\":$SEATS}" | json_field id) || die "could not create a class on $API1"
[[ -n "$CLASS_ID" ]] || die "could not read the new class id"

echo "Class \"$CLASS_NAME\" ($CLASS_ID) with $SEATS seats."
echo "Firing $N applications from $N students, $PARALLEL at a time, alternating $API1 and $API2..."
export CLASS_ID API1 API2

# One line per request: "<instance> <http status>" (000 = connection failed or timed out).
results=$(seq 1 "$N" | xargs -P "$PARALLEL" -I{} bash -c '
  i={}
  if (( i % 2 )); then api=$API1; name=instance-1; else api=$API2; name=instance-2; fi
  sid=$(printf "2016%013d" "$i")
  code=$(curl -s -o /dev/null --max-time 30 -w "%{http_code}" -X POST "$api/classes/$CLASS_ID/holds" -H "X-Student-Id: $sid" || true)
  echo "$name ${code:-000}"
')

echo
echo "Responses (count, instance, status):"
echo "$results" | sort | uniq -c | sed 's/^/  /'

created=$(echo "$results" | awk '$2 == "201"' | wc -l | tr -d ' ')
full=$(echo "$results" | awk '$2 == "409"' | wc -l | tr -d ' ')
other=$(( N - created - full ))

state1=$(curl -fsS "$API1/classes/$CLASS_ID")
state2=$(curl -fsS "$API2/classes/$CLASS_ID")
held1=$(echo "$state1" | json_field heldSeats); avail1=$(echo "$state1" | json_field availableSeats)
held2=$(echo "$state2" | json_field heldSeats); avail2=$(echo "$state2" | json_field availableSeats)

echo
echo "Final state via $API1: $state1"
echo "Final state via $API2: $state2"
echo

fail=0
check() { # check <description> <actual> <expected>
  if [[ "$2" == "$3" ]]; then echo "  ok    $1: $2"; else echo "  FAIL  $1: got $2, expected $3"; fail=1; fi
}
check "201 Created" "$created" "$SEATS"
check "409 class full" "$full" "$(( N - SEATS ))"
check "anything else (5xx, dropped, other)" "$other" "0"
check "heldSeats on instance 1" "$held1" "$SEATS"
check "heldSeats on instance 2" "$held2" "$SEATS"
check "availableSeats on both" "$avail1/$avail2" "0/0"

echo
if (( fail )); then echo "FAIL: the seat limit was not respected across two instances."; exit 1; fi
echo "PASS: exactly $SEATS of $N simultaneous applications got a seat across two backend instances."
