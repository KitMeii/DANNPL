// Phần D load test — exercises the platform through the gateway the same way the real frontend
// does, using the seeded demo accounts (SEED_DEMO_ACCOUNTS=true). Deliberately excludes
// ai-service's generation endpoints (chat/generate-lecture/extract-questions): those need a real
// GROQ_API_KEY, which local dev doesn't have, and are separately protected by the gateway's own
// per-user rate limiter (RateLimiting:Ai:PermitLimit) rather than raw throughput — not what this
// script is measuring.
//
// Usage: docker run --rm -i --network host -v <repo>/load-tests:/scripts grafana/k6 run \
//   /scripts/main-flow.js -e BASE_URL=http://localhost:8080
//
// 70% of iterations: full student journey (login, browse materials, practice quiz, check
// progress/leaderboard). 20%: anonymous-style content browsing. 10%: teacher management reads.
import http from "k6/http";
import { check, sleep, group } from "k6";
import { Rate, Trend } from "k6/metrics";

const BASE_URL = __ENV.BASE_URL || "http://localhost:8080";

const errorRate = new Rate("errors");
const loginDuration = new Trend("login_duration");
const quizSubmitDuration = new Trend("quiz_submit_duration");

export const options = {
  scenarios: {
    student_journey: {
      executor: "ramping-vus",
      exec: "studentJourney",
      startVUs: 0,
      stages: [
        { duration: "15s", target: 30 },
        { duration: "30s", target: 30 },
        { duration: "10s", target: 0 },
      ],
    },
    content_browsing: {
      executor: "ramping-vus",
      exec: "contentBrowsing",
      startVUs: 0,
      stages: [
        { duration: "15s", target: 10 },
        { duration: "30s", target: 10 },
        { duration: "10s", target: 0 },
      ],
    },
    teacher_reads: {
      executor: "ramping-vus",
      exec: "teacherReads",
      startVUs: 0,
      stages: [
        { duration: "15s", target: 5 },
        { duration: "30s", target: 5 },
        { duration: "10s", target: 0 },
      ],
    },
  },
  thresholds: {
    http_req_duration: ["p(95)<800"],
    errors: ["rate<0.01"],
  },
};

function login(email, password) {
  const res = http.post(
    `${BASE_URL}/api/v1/auth/login`,
    JSON.stringify({ email, password }),
    { headers: { "Content-Type": "application/json" }, tags: { name: "login" } }
  );
  loginDuration.add(res.timings.duration);
  const ok = check(res, { "login 200": (r) => r.status === 200 });
  errorRate.add(!ok);
  if (!ok) return null;
  return res.json("data.accessToken");
}

function authParams(token, name) {
  return {
    headers: { Authorization: `Bearer ${token}`, "Content-Type": "application/json" },
    tags: { name },
  };
}

export function studentJourney() {
  group("student journey", () => {
    const token = login("student@demo.nnpl", "Demo123!");
    if (!token) {
      sleep(1);
      return;
    }
    let res = http.get(`${BASE_URL}/api/v1/content/materials`, authParams(token, "list_materials"));
    errorRate.add(!check(res, { "materials 200": (r) => r.status === 200 }));

    res = http.get(`${BASE_URL}/api/v1/quiz/questions/practice`, authParams(token, "practice_questions"));
    errorRate.add(!check(res, { "practice 200": (r) => r.status === 200 }));

    const questions = res.status === 200 ? res.json("data") : [];
    if (Array.isArray(questions) && questions.length > 0) {
      const answers = questions.slice(0, Math.min(3, questions.length)).map((q) => ({
        questionId: q.id,
        selectedOption: 0,
      }));
      const submitRes = http.post(
        `${BASE_URL}/api/v1/quiz/practice/submit`,
        JSON.stringify({ chapter: null, answers }),
        authParams(token, "submit_practice")
      );
      quizSubmitDuration.add(submitRes.timings.duration);
      errorRate.add(!check(submitRes, { "submit 200": (r) => r.status === 200 }));
    }

    res = http.get(`${BASE_URL}/api/v1/progress/me`, authParams(token, "my_progress"));
    errorRate.add(!check(res, { "progress 200": (r) => r.status === 200 }));

    res = http.get(`${BASE_URL}/api/v1/progress/leaderboard`, authParams(token, "leaderboard"));
    errorRate.add(!check(res, { "leaderboard 200": (r) => r.status === 200 }));
  });

  sleep(Math.random() * 2 + 1);
}

export function contentBrowsing() {
  group("content browsing", () => {
    const token = login("student@demo.nnpl", "Demo123!");
    if (!token) {
      sleep(1);
      return;
    }
    const res = http.get(`${BASE_URL}/api/v1/content/materials`, authParams(token, "list_materials_anon"));
    errorRate.add(!check(res, { "materials 200": (r) => r.status === 200 }));

    const materials = res.status === 200 ? res.json("data") : [];
    if (Array.isArray(materials) && materials.length > 0) {
      const id = materials[0].id;
      const detailRes = http.get(`${BASE_URL}/api/v1/content/materials/${id}`, authParams(token, "material_detail"));
      errorRate.add(!check(detailRes, { "detail 200": (r) => r.status === 200 }));
    }
  });

  sleep(Math.random() * 2 + 1);
}

export function teacherReads() {
  group("teacher reads", () => {
    const token = login("teacher@demo.nnpl", "Demo123!");
    if (!token) {
      sleep(1);
      return;
    }
    let res = http.get(`${BASE_URL}/api/v1/quiz/questions`, authParams(token, "question_bank"));
    errorRate.add(!check(res, { "questions 200": (r) => r.status === 200 }));

    res = http.get(`${BASE_URL}/api/v1/quiz/oral-questions`, authParams(token, "oral_question_bank"));
    errorRate.add(!check(res, { "oral questions 200": (r) => r.status === 200 }));

    res = http.get(`${BASE_URL}/api/v1/content/materials`, authParams(token, "materials_teacher"));
    errorRate.add(!check(res, { "materials 200": (r) => r.status === 200 }));
  });

  sleep(Math.random() * 2 + 1);
}
