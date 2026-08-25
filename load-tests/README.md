# Load tests (Phần D)

k6 scripts exercising the platform through the gateway, using the seeded demo accounts
(`SEED_DEMO_ACCOUNTS=true` in `.env`). Run with the full `docker compose up` stack running.

## Running

Attach the k6 container to the same compose network so it can reach services by name:

```bash
docker run --rm -i \
  --network da_nnpl_nnpl-net \
  -v "$(pwd)/load-tests:/scripts" \
  grafana/k6 run /scripts/main-flow.js -e BASE_URL=http://gateway:8080
```

(Network name depends on the compose project name — check with `docker network ls`
if it's not `da_nnpl_nnpl-net`.)

## Scripts

- **`main-flow.js`** — the main load test. Three concurrent scenarios ramping to 45 total VUs:
  student journey (login → browse materials → practice quiz → submit → check
  progress/leaderboard), content browsing, and teacher question-bank/material reads. Thresholds:
  p95 request duration < 800ms, error rate < 1%.
- **`ai-rate-limit-check.js`** — single-VU burst of 20 rapid `/api/v1/ai/chat` calls as one user,
  confirming the gateway's per-user rate limiter (`RateLimiting:Ai:PermitLimit`, default
  10/minute) actually rejects requests past the limit with 429 rather than just existing in
  config. The first ~10 requests reaching ai-service will 500 in local dev (no real
  `GROQ_API_KEY` configured) — that's expected and unrelated to what this script checks.

Deliberately excluded: ai-service's generation endpoints from the main load test (chat, lecture
generation, question extraction) — they need a real Groq API key to return anything meaningful,
and are cost/rate-limited by design rather than meant to be raw-throughput-tested.

## Last results (local dev, 2026-07-18)

`main-flow.js`, 45 max VUs over ~55s sustained load:

| Metric | Result |
|---|---|
| Total requests | 4,709 |
| Error rate | 0.00% |
| p95 request duration | 50.98ms (threshold: <800ms) |
| Throughput | ~82 req/s |
| Checks passed | 4,709 / 4,709 (100%) |

`ai-rate-limit-check.js`: first 10/20 rapid requests reached ai-service (500 — no Groq key in
local dev, expected), requests 11-20 correctly rejected with 429 by the gateway's rate limiter.

No bottlenecks found at this scale. See the Phần D report in the project history for analysis
and what would need attention before a much larger user base (dozens of concurrent students is
comfortably within range; hundreds+ would warrant re-testing, particularly around SQL Server
connection pool sizing and the single mssql container's resource limits).
