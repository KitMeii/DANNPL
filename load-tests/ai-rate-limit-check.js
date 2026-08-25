// Confirms the gateway's per-user AI rate limiter (RateLimiting:Ai:PermitLimit, default 10/min)
// actually engages under a burst — this is a cost-control guard (Groq calls cost money), so it's
// worth checking under load, not just trusting the config exists. A single VU fires 20 rapid
// requests as the same user; expect the first ~10 to reach ai-service (which will itself fail
// with no real GROQ_API_KEY in local dev — that's fine, we're checking for 429 appearing, not
// for the chat call to succeed) and the rest to be rejected with 429 before ever reaching it.
//
// Usage: docker run --rm -i --network <compose-network> -v <repo>/load-tests:/scripts \
//   grafana/k6 run /scripts/ai-rate-limit-check.js -e BASE_URL=http://gateway:8080
import http from "k6/http";
import { check } from "k6";

const BASE_URL = __ENV.BASE_URL || "http://localhost:8080";

export const options = { vus: 1, iterations: 1 };

export default function () {
  const loginRes = http.post(
    `${BASE_URL}/api/v1/auth/login`,
    JSON.stringify({ email: "student@demo.nnpl", password: "Demo123!" }),
    { headers: { "Content-Type": "application/json" } }
  );
  check(loginRes, { "login 200": (r) => r.status === 200 });
  const token = loginRes.json("data.accessToken");

  const statuses = [];
  for (let i = 0; i < 20; i++) {
    const res = http.post(
      `${BASE_URL}/api/v1/ai/chat`,
      JSON.stringify({ messages: [{ role: "user", content: `ping ${i}` }] }),
      { headers: { Authorization: `Bearer ${token}`, "Content-Type": "application/json" } }
    );
    statuses.push(res.status);
  }

  const rateLimited = statuses.filter((s) => s === 429).length;
  console.log(`Statuses: ${statuses.join(",")}`);
  console.log(`429 (rate limited) count: ${rateLimited} / 20`);

  check(rateLimited, { "rate limiter engaged (at least one 429)": (n) => n > 0 });
}
