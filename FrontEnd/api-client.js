/**
 * api-client.js — thay thế supabase-config.js.
 *
 * Toàn bộ gọi Supabase/Groq trực tiếp từ browser đã bị loại bỏ. Mọi request giờ đi qua Gateway
 * (YARP), Gateway xác thực JWT rồi forward xuống từng microservice. Xem kế hoạch di trú tại
 * README của repo / lịch sử commit "Add YARP gateway" v.v.
 *
 * Cấu hình domain Gateway: đặt `window.TTHCM_API_BASE = "https://api.tenmien.vn"` trong 1 thẻ
 * <script> TRƯỚC khi load file này (ví dụ ở <head> mỗi trang, hoặc 1 file config riêng khi
 * deploy production). Nếu không đặt, mặc định trỏ về Gateway chạy local lúc dev
 * (http://localhost:8080).
 */

const API_BASE = window.TTHCM_API_BASE || "http://localhost:8080";

const STORAGE_KEYS = {
  token: "tthcm_access_token",
  expiresAt: "tthcm_token_expires_at",
  user: "tthcm_user",
};

/**
 * Trung tâm đặt tên hệ thống/môn học/trường hiển thị trên giao diện. Đổi Ở ĐÂY để áp dụng cho
 * toàn bộ 14 trang — không hardcode text riêng từng trang. Đặt window.APP_CONFIG trước khi load
 * file này (VD 1 thẻ <script> ở <head>) để override, giống window.TTHCM_API_BASE ở trên.
 *
 * Cách dùng trong HTML: các phần tử hiển thị tên đọc qua data attribute, VD:
 *   <span data-app-name></span>, <span data-subject-name></span>, <span data-school-name></span>
 * và thẻ <title data-title-suffix>Tên trang</title> sẽ tự nối thành "Tên trang - {appName}".
 */
const APP_CONFIG = window.APP_CONFIG || {
  appName: "Giảng viên Ảo",
  subjectName: "Môn học",
  schoolName: "Học viện",
};
window.APP_CONFIG = APP_CONFIG;

const titleEl = document.querySelector("title[data-title-suffix]");
if (titleEl) {
  document.title = `${titleEl.textContent} - ${APP_CONFIG.appName}`;
}

document.addEventListener("DOMContentLoaded", () => {
  document.querySelectorAll("[data-app-name]").forEach((el) => { el.textContent = APP_CONFIG.appName; });
  document.querySelectorAll("[data-subject-name]").forEach((el) => { el.textContent = APP_CONFIG.subjectName; });
  document.querySelectorAll("[data-school-name]").forEach((el) => { el.textContent = APP_CONFIG.schoolName; });
});

// Việc 3.1 (2026-08-19) — PHẢI khớp CHÍNH XÁC Shared.Contracts.CapBacValues.All (backend validate
// lại, đây chỉ để dựng dropdown) — sửa 1 bên thì sửa cả bên kia.
const CAP_BAC_OPTIONS = [
  "Chưa cập nhật",
  "Binh nhì", "Binh nhất", "Hạ sĩ", "Trung sĩ", "Thượng sĩ",
  "Thiếu úy", "Trung úy", "Thượng úy", "Đại úy",
  "Thiếu tá", "Trung tá", "Thượng tá", "Đại tá",
];

// Rà soát Lần VI (2026-08-21) — cùng danh sách với ChucVuGvValues.All (auth-service), tách 2 nơi
// vì frontend không import được C# enum, phải copy y hệt (giống CAP_BAC_OPTIONS ở trên).
const CHUC_VU_GV_OPTIONS = [
  "Chưa cập nhật",
  "Giảng viên", "Giảng viên chính", "Giảng viên cao cấp",
  "Phó trưởng bộ môn", "Trưởng bộ môn",
  "Phó trưởng khoa", "Trưởng khoa",
];

// ---------------------------------------------------------------------------
// Token / session storage
// ---------------------------------------------------------------------------

function getToken() {
  return localStorage.getItem(STORAGE_KEYS.token);
}

function getStoredUser() {
  const raw = localStorage.getItem(STORAGE_KEYS.user);
  if (!raw) return null;
  try {
    return JSON.parse(raw);
  } catch {
    // Dữ liệu hỏng (vd. còn sót từ 1 schema session cũ) — coi như chưa có user thay vì
    // throw và làm hỏng mọi script đọc session (guard RBAC, nav-correction...).
    localStorage.removeItem(STORAGE_KEYS.user);
    return null;
  }
}

function isTokenExpired() {
  const expiresAt = localStorage.getItem(STORAGE_KEYS.expiresAt);
  if (!expiresAt) return true;
  return new Date(expiresAt).getTime() <= Date.now();
}

function isAuthenticated() {
  return !!getToken() && !isTokenExpired();
}

function storeSession(authResponse) {
  localStorage.setItem(STORAGE_KEYS.token, authResponse.accessToken);
  localStorage.setItem(STORAGE_KEYS.expiresAt, authResponse.expiresAtUtc);
  localStorage.setItem(STORAGE_KEYS.user, JSON.stringify(authResponse.user));
}

function clearSession() {
  // Audit 2026-08-14: dọn luôn lịch sử chat gắn theo userId của phiên vừa đăng xuất — máy dùng
  // chung (phòng máy, máy gia đình) không được để sót chat của người vừa đăng xuất cho người
  // đăng nhập tiếp theo thấy. Đọc user TRƯỚC khi xóa STORAGE_KEYS.user để còn biết id. Tên key
  // "chatSessions_v2_"/"chatCurrentSession_" phải khớp đúng sessionsKey()/currentSessionKey() ở
  // student/chat.html — sửa 1 bên thì nhớ sửa bên kia (không có module chung để import).
  const user = getStoredUser();
  if (user?.id) {
    localStorage.removeItem(`chatSessions_v2_${user.id}`);
    localStorage.removeItem(`chatCurrentSession_${user.id}`);
  }
  localStorage.removeItem(STORAGE_KEYS.token);
  localStorage.removeItem(STORAGE_KEYS.expiresAt);
  localStorage.removeItem(STORAGE_KEYS.user);
}

/** Call at the top of every protected page. Redirects to auth.html if not logged in. */
function requireAuth() {
  if (!isAuthenticated()) {
    window.location.href = "/auth.html";
    return false;
  }
  return true;
}

/** Non-redirecting session check — returns {token, user} or null. */
function getSession() {
  if (!isAuthenticated()) return null;
  return { token: getToken(), user: getStoredUser() };
}

/** Rà soát Lần IV (2026-08-21) — nút Hồ sơ ở header (#profileBtn) trên MỌI trang: hiện đúng ảnh đại
 * diện mới nhất (đọc từ session cache — đã tự cập nhật ngay sau uploadAvatar()/getProfile()/
 * updateProfile(), không cần gọi API lại mỗi lần vào trang khác), và bấm vào điều hướng THẲNG tới
 * trang Hồ sơ (cai-dat.html) thay vì mở modal riêng lẻ ở từng trang (tránh 2-3 nơi cùng sửa 1 dữ
 * liệu). profileHref = đường dẫn tương đối tới cai-dat.html từ trang đang gọi (VD "cai-dat.html" ở
 * FrontEnd/, "../cai-dat.html" ở FrontEnd/student/). Không tìm thấy #profileBtn thì bỏ qua êm, vô
 * hại với trang không có nút này. */
function renderHeaderProfileBtn(profileHref) {
  const btn = document.getElementById("profileBtn");
  if (!btn) return;
  const user = getStoredUser();
  btn.innerHTML = user?.avatarUrl
    ? `<img src="${user.avatarUrl}" alt="" style="width:100%;height:100%;border-radius:50%;object-fit:cover;">`
    : '<i class="fas fa-user-circle"></i>';
  btn.onclick = () => {
    window.location.href = profileHref;
  };
}

/** Việc 4.5 (2026-08-20) — gate chung theo role, thay cho khối IIFE ~15 dòng lặp lại ở mỗi trang
 * học viên (Việc D, 2026-08-16, mở rộng ra api-client.js để dùng chung được — admin-common.js chỉ
 * nạp ở trang Admin/Teacher, không có ở trang học viên). Gọi Ở ĐẦU <script> của trang, TRƯỚC init(),
 * y hệt vị trí IIFE cũ.
 *
 * Rà soát Lần XIII (2026-08-21) — Admin trước đây bị redirect NGAY khỏi mọi trang học viên gọi hàm
 * này (kể cả index.html), không có cách "ghé thăm" trang chủ hệ thống như Teacher. Theo yêu cầu
 * người dùng ("Trang chủ" của Admin phải hoạt động giống hệt Teacher), Admin giờ dùng CHUNG nhánh
 * xử lý với Teacher: không redirect, chỉ định tuyến lại nav "Tài khoản" về đúng trang quản trị của
 * mình. adminNavHref (tên cũ: adminRedirectHref) giữ nguyên Ý NGHĨA — luôn là đường dẫn tới trang
 * Admin, chỉ đổi cách dùng (gán href thay vì redirect).
 *
 * role === "Admin" hoặc "Teacher" -> ẩn nav Cài đặt (Student-only), định tuyến nav "Tài khoản" về
 * đúng trang quản trị của role đó.
 * role khác (Student, hoặc chưa đăng nhập — session null nên role undefined) -> ẩn hẳn nav
 * "Tài khoản" (Admin/GV only), KHÔNG redirect gì (requireAuth() trong init() xử lý riêng trường hợp
 * chưa đăng nhập, chạy SAU hàm này — không xung đột 2 cơ chế redirect).
 */
function applyRoleGate(adminNavHref, teacherNavHref) {
  const session = getSession();
  const role = session && session.user && session.user.role;
  if (role === "Admin" || role === "Teacher") {
    document.getElementById("navSettings")?.remove();
    const navAdmin = document.getElementById("navAdmin");
    if (navAdmin) navAdmin.href = role === "Admin" ? adminNavHref : teacherNavHref;
  } else {
    document.getElementById("navAdmin")?.remove();
  }
}

function signOut() {
  clearSession();
  window.location.href = "/auth.html";
}

/** Việc II (2026-08-20, rà soát Lần II mục 1.12) — nút Đăng xuất chuẩn hóa về 1 vị trí duy nhất
 * (góc trên header, .hdr-btn) trên toàn bộ 9 trang học viên, thay 2 vị trí cũ không đồng bộ (nút
 * full-width trong modal hồ sơ index.html, dòng list trong cai-dat.html) — gom logic xác nhận
 * (trước đây chỉ có ở cai-dat.html's confirmLogout()) vào đây để dùng chung mọi trang. */
function confirmSignOut() {
  if (confirm("Bạn có chắc muốn đăng xuất?")) signOut();
}

// ---------------------------------------------------------------------------
// Core fetch wrapper — every service responds with { success, data, error }
// ---------------------------------------------------------------------------

class ApiError extends Error {
  constructor(code, message, status, retryAfterSeconds = null) {
    super(message);
    this.code = code;
    this.status = status;
    this.retryAfterSeconds = retryAfterSeconds;
  }
}

async function apiFetch(path, { method = "GET", body, auth = true } = {}) {
  const headers = { "Content-Type": "application/json" };

  if (auth) {
    const token = getToken();
    if (token) headers.Authorization = `Bearer ${token}`;
  }

  let response;
  try {
    response = await fetch(`${API_BASE}${path}`, {
      method,
      headers,
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });
  } catch (networkError) {
    throw new ApiError("NETWORK_ERROR", "Không thể kết nối máy chủ. Kiểm tra lại kết nối mạng.", 0);
  }

  if (response.status === 401) {
    clearSession();
    window.location.href = "/auth.html";
    throw new ApiError("UNAUTHORIZED", "Phiên đăng nhập đã hết hạn.", 401);
  }

  // 204 No Content / empty body
  const text = await response.text();
  const json = text ? JSON.parse(text) : { success: response.ok, data: null };

  if (!response.ok || json.success === false) {
    const error = json.error || {};
    throw new ApiError(error.code || "UNKNOWN_ERROR", error.message || "Đã xảy ra lỗi.", response.status, error.retryAfterSeconds ?? null);
  }

  return json.data;
}

// ---------------------------------------------------------------------------
// Auth
// ---------------------------------------------------------------------------

async function login(email, password) {
  const auth = await apiFetch("/api/v1/auth/login", { method: "POST", body: { email, password }, auth: false });
  storeSession(auth);
  return auth.user;
}

/** capBac/soDienThoai/namHoc (Việc 3.1) — TÙY CHỌN, chỉ có ý nghĩa cho Student (đăng ký luôn tạo
 * Role=Student). Truyền null/undefined nếu không điền — backend tự bỏ qua field rỗng. */
function register(email, password, name, capBac, soDienThoai, namHoc) {
  return apiFetch("/api/v1/auth/register", {
    method: "POST",
    body: { email, password, name, capBac: capBac || null, soDienThoai: soDienThoai || null, namHoc: namHoc || null },
    auth: false,
  }).then((auth) => {
    storeSession(auth);
    return auth.user;
  });
}

async function getProfile() {
  const user = await apiFetch("/api/v1/auth/me");
  localStorage.setItem(STORAGE_KEYS.user, JSON.stringify(user));
  return user;
}

/** capBac/soDienThoai/namHoc chỉ áp dụng nếu user là Student, boMonKhoa chỉ áp dụng nếu là Teacher
 * — gửi field không đúng role không lỗi, chỉ đơn giản không được lưu (xem
 * AuthServiceImpl.UpdateProfileAsync). Field không truyền (undefined) giữ nguyên giá trị cũ ở phía
 * hiển thị nhưng KHÔNG xóa dữ liệu đã lưu — luôn gửi giá trị hiện tại từ form, không gửi thiếu. */
async function updateProfile({ name, capBac, soDienThoai, namHoc, boMonKhoa, chucVuGV }) {
  const user = await apiFetch("/api/v1/auth/me", {
    method: "PUT",
    body: {
      name,
      capBac: capBac || null,
      soDienThoai: soDienThoai || null,
      namHoc: namHoc || null,
      boMonKhoa: boMonKhoa || null,
      chucVuGV: chucVuGV || null,
    },
  });
  localStorage.setItem(STORAGE_KEYS.user, JSON.stringify(user));
  return user;
}

// Rà soát Lần VI (2026-08-21) — đổi mật khẩu self-service. Không trả về gì đặc biệt (chỉ throw nếu
// mật khẩu hiện tại sai — ApiError từ apiFetch).
function changeMyPassword(currentPassword, newPassword) {
  return apiFetch("/api/v1/auth/me/password", {
    method: "PUT",
    body: { currentPassword, newPassword },
  });
}

// Rà soát Lần VI (2026-08-21) — Môn học phụ trách của GV, CHỈ Admin gọi được (403 nếu không phải).
function authChangeMonHocPhuTrach(userId, monHocPhuTrach) {
  return apiFetch(`/api/v1/auth/users/${userId}/mon-hoc-phu-trach`, {
    method: "PUT",
    body: { monHocPhuTrach },
  });
}
// Rà soát Lần XIV (2026-08-21) — Chức vụ chuyên môn của GV, Admin sửa cho người khác (đường tự sửa
// của GV vẫn qua updateProfile() như cũ, cùng field).
function authChangeChucVuGV(userId, chucVuGV) {
  return apiFetch(`/api/v1/auth/users/${userId}/chuc-vu-gv`, {
    method: "PUT",
    body: { chucVuGV },
  });
}
// Rà soát Lần XVIII (2026-08-22) — Admin sửa Họ tên + Năm học của người khác (namHoc undefined =
// không đổi, cùng quy ước authUpdateLop).
function authAdminEditUser(userId, name, namHoc) {
  return apiFetch(`/api/v1/auth/users/${userId}/admin-edit`, {
    method: "PUT",
    body: { name, namHoc },
  });
}

/** Upload avatar — cùng pattern multipart với uploadMaterialFile() (browser không cầm key
 * Cloudinary). userId luôn suy từ JWT ở backend, không truyền id — chỉ đổi được avatar của chính
 * mình. Trả về UserResponse mới (đã có AvatarUrl), tự cập nhật session lưu local luôn. */
async function uploadAvatar(file) {
  const headers = {};
  const token = getToken();
  if (token) headers.Authorization = `Bearer ${token}`;

  const formData = new FormData();
  formData.append("file", file);

  let response;
  try {
    response = await fetch(`${API_BASE}/api/v1/auth/me/avatar`, {
      method: "POST",
      headers,
      body: formData,
    });
  } catch (networkError) {
    throw new ApiError("NETWORK_ERROR", "Không thể kết nối máy chủ. Kiểm tra lại kết nối mạng.", 0);
  }

  if (response.status === 401) {
    clearSession();
    window.location.href = "/auth.html";
    throw new ApiError("UNAUTHORIZED", "Phiên đăng nhập đã hết hạn.", 401);
  }

  const text = await response.text();
  const json = text ? JSON.parse(text) : { success: response.ok, data: null };

  if (!response.ok || json.success === false) {
    const error = json.error || {};
    throw new ApiError(error.code || "UNKNOWN_ERROR", error.message || "Đã xảy ra lỗi.", response.status, error.retryAfterSeconds ?? null);
  }

  localStorage.setItem(STORAGE_KEYS.user, JSON.stringify(json.data));
  return json.data;
}

// ---------------------------------------------------------------------------
// Quiz (trắc nghiệm)
// ---------------------------------------------------------------------------

function getPracticeQuestions(chapter) {
  const qs = chapter ? `?chapter=${encodeURIComponent(chapter)}` : "";
  return apiFetch(`/api/v1/quiz/questions/practice${qs}`);
}

function submitPracticeQuiz(chapter, answers) {
  return apiFetch("/api/v1/quiz/practice/submit", { method: "POST", body: { chapter, answers } });
}

// Việc 4.4 Phần B (2026-08-20) — "Đề luyện tập" giáo viên tạo sẵn, giao theo Lớp.
function listPracticeSetChapters() {
  return apiFetch("/api/v1/quiz/practice-sets/chapters");
}
function createPracticeSet(ten, chapter, lopIds) {
  return apiFetch("/api/v1/quiz/practice-sets", { method: "POST", body: { ten, chapter, lopIds } });
}
function listMyPracticeSets() {
  return apiFetch("/api/v1/quiz/practice-sets/mine");
}
function deletePracticeSet(id) {
  return apiFetch(`/api/v1/quiz/practice-sets/${id}`, { method: "DELETE" });
}
// Học viên (hoặc GV) xem đề khả dụng cho đúng Lớp của mình.
function listAvailablePracticeSets() {
  return apiFetch("/api/v1/quiz/practice-sets/available");
}

// sessionId (Việc 4.1, tùy chọn) — gắn kết quả vào đúng phiên đã /exams/start, tránh lazy-check
// sau này chốt trùng. Bỏ trống vẫn hoạt động như cũ (tương thích ngược).
function submitExam(answers, timeSpentSeconds, sessionId) {
  return apiFetch("/api/v1/quiz/exams/submit", { method: "POST", body: { answers, timeSpentSeconds, sessionId: sessionId || null } });
}

function getWrongAnswers() {
  return apiFetch("/api/v1/quiz/wrong-answers");
}

function getMyQuizResults() {
  return apiFetch("/api/v1/quiz/my-results");
}

// ===================== Việc 4.1 (2026-08-19) — Chống thoát thi thử =====================
// Gọi startExamSession/startOralSession NGAY SAU KHI fetch xong bộ câu hỏi, TRƯỚC khi hiển thị
// cho học viên — server cần biết "bài thi đã bắt đầu" và bộ câu hỏi thật để có thể tự động nộp
// đúng nếu học viên thoát giữa chừng (đóng tab/mất mạng/crash) mà không bấm Nộp.

function startExamSession(questionIds, expectedDurationSeconds) {
  return apiFetch("/api/v1/quiz/exams/start", { method: "POST", body: { questionIds, expectedDurationSeconds } });
}

/** Lớp 1 — gọi lúc rời trang (pagehide/visibilitychange), KHÔNG dùng apiFetch: fetch() thường
 * không đảm bảo hoàn thành khi trang đang bị hủy, navigator.sendBeacon() được thiết kế riêng cho
 * đúng tình huống này. sendBeacon KHÔNG set được header tùy ý (không gửi được Authorization) nên
 * JWT truyền qua query string — backend có fallback riêng cho đúng trường hợp này (xem
 * Shared.Infrastructure/Auth/JwtAuthenticationExtensions.cs), CHỈ áp dụng khi thiếu header, không
 * ảnh hưởng mọi luồng gọi API bình thường khác. Best-effort, không có cách đọc phản hồi/báo lỗi. */
function beaconAutoSubmitExam(sessionId, answers) {
  const token = getToken();
  if (!token || !sessionId) return;
  const url = `${API_BASE}/api/v1/quiz/exams/auto-submit?access_token=${encodeURIComponent(token)}`;
  const blob = new Blob([JSON.stringify({ sessionId, answers })], { type: "application/json" });
  navigator.sendBeacon(url, blob);
}

// vấn đáp (oral)
function getOralQuestions(chapter) {
  const qs = chapter ? `?chapter=${encodeURIComponent(chapter)}` : "";
  return apiFetch(`/api/v1/quiz/oral-questions/practice${qs}`);
}

function submitOralAnswer(questionId, mainAnswer, followupAnswers, sessionId) {
  return apiFetch("/api/v1/quiz/oral/submit", {
    method: "POST",
    body: { questionId, mainAnswer, followupAnswers, sessionId: sessionId || null },
  });
}

function getMyOralResults() {
  return apiFetch("/api/v1/quiz/oral/results");
}

function startOralSession(questionIds, expectedDurationSeconds) {
  return apiFetch("/api/v1/quiz/oral/start", { method: "POST", body: { questionIds, expectedDurationSeconds } });
}

/** Vấn đáp không cần gửi answers khi bỏ dở — mỗi câu đã lưu ngay lúc trả lời (submitOralAnswer),
 * không có gì để mất; chỉ cần đánh dấu phiên là bị bỏ dở. Cùng cơ chế beacon+query-string-JWT như
 * beaconAutoSubmitExam. */
function beaconAbandonOralSession(sessionId) {
  const token = getToken();
  if (!token || !sessionId) return;
  const url = `${API_BASE}/api/v1/quiz/oral/abandon?access_token=${encodeURIComponent(token)}`;
  const blob = new Blob([JSON.stringify({ sessionId })], { type: "application/json" });
  navigator.sendBeacon(url, blob);
}

function getMyOralSessions() {
  return apiFetch("/api/v1/quiz/oral/sessions");
}

// Teacher/Admin: ngân hàng câu hỏi
function listQuestionsBank(chapter) {
  const qs = chapter ? `?chapter=${encodeURIComponent(chapter)}` : "";
  return apiFetch(`/api/v1/quiz/questions${qs}`);
}
function createQuestion(question) {
  return apiFetch("/api/v1/quiz/questions", { method: "POST", body: question });
}
function updateQuestion(id, question) {
  return apiFetch(`/api/v1/quiz/questions/${id}`, { method: "PUT", body: question });
}
function deleteQuestion(id) {
  return apiFetch(`/api/v1/quiz/questions/${id}`, { method: "DELETE" });
}

function listOralQuestionsBank(chapter) {
  const qs = chapter ? `?chapter=${encodeURIComponent(chapter)}` : "";
  return apiFetch(`/api/v1/quiz/oral-questions${qs}`);
}
function createOralQuestion(question) {
  return apiFetch("/api/v1/quiz/oral-questions", { method: "POST", body: question });
}
function updateOralQuestion(id, question) {
  return apiFetch(`/api/v1/quiz/oral-questions/${id}`, { method: "PUT", body: question });
}
function deleteOralQuestion(id) {
  return apiFetch(`/api/v1/quiz/oral-questions/${id}`, { method: "DELETE" });
}

function listEssayQuestionsBank(chapter) {
  const qs = chapter ? `?chapter=${encodeURIComponent(chapter)}` : "";
  return apiFetch(`/api/v1/quiz/essay-questions${qs}`);
}
function createEssayQuestion(question) {
  return apiFetch("/api/v1/quiz/essay-questions", { method: "POST", body: question });
}
function updateEssayQuestion(id, question) {
  return apiFetch(`/api/v1/quiz/essay-questions/${id}`, { method: "PUT", body: question });
}
function deleteEssayQuestion(id) {
  return apiFetch(`/api/v1/quiz/essay-questions/${id}`, { method: "DELETE" });
}
function publishQuestion(id) {
  return apiFetch(`/api/v1/quiz/questions/${id}/publish`, { method: "PUT" });
}
function publishEssayQuestion(id) {
  return apiFetch(`/api/v1/quiz/essay-questions/${id}/publish`, { method: "PUT" });
}
/** Việc 8 (2026-08-16) — sửa lại phạm vi hiển thị (theo Lớp) của 1 câu hỏi/câu tự luận đã có.
 * lopIds rỗng = trả về toàn hệ thống. */
function updateQuestionLopVisibility(id, lopIds) {
  return apiFetch(`/api/v1/quiz/questions/${id}/lop-visibility`, { method: "PUT", body: { lopIds } });
}
function updateEssayQuestionLopVisibility(id, lopIds) {
  return apiFetch(`/api/v1/quiz/essay-questions/${id}/lop-visibility`, { method: "PUT", body: { lopIds } });
}
// Việc 4.4 Phần A (2026-08-20) — vá gap: câu hỏi Vấn đáp trước đây không có cơ chế giới hạn Lớp.
function updateOralQuestionLopVisibility(id, lopIds) {
  return apiFetch(`/api/v1/quiz/oral-questions/${id}/lop-visibility`, { method: "PUT", body: { lopIds } });
}
function publishExamVersion(versionId) {
  return apiFetch(`/api/v1/quiz/exam-sets/versions/${versionId}/publish`, { method: "PUT" });
}
function unpublishExamVersion(versionId) {
  return apiFetch(`/api/v1/quiz/exam-sets/versions/${versionId}/unpublish`, { method: "PUT" });
}

/** Xuất câu hỏi (trộn MCQ + tự luận) ra file .docx và kích hoạt tải xuống trình duyệt ngay — khác
 * apiFetch() ở chỗ response là binary (docx), không phải JSON, nên tự fetch riêng thay vì dùng
 * apiFetch(). fileName không bắt buộc, mặc định "de-thi.docx". */
async function exportQuestionsToWord(questionIds, essayQuestionIds, fileName = "de-thi.docx", oralQuestionIds = []) {
  const headers = { "Content-Type": "application/json" };
  const token = getToken();
  if (token) headers.Authorization = `Bearer ${token}`;

  let response;
  try {
    response = await fetch(`${API_BASE}/api/v1/quiz/export/word`, {
      method: "POST",
      headers,
      body: JSON.stringify({ questionIds, essayQuestionIds, oralQuestionIds }),
    });
  } catch {
    throw new ApiError("NETWORK_ERROR", "Không thể kết nối máy chủ. Kiểm tra lại kết nối mạng.", 0);
  }

  if (!response.ok) {
    let message = "Xuất file thất bại.";
    try {
      const json = await response.json();
      message = json.error?.message || message;
    } catch {
      /* response không phải JSON (vd lỗi 500 thô) — giữ message mặc định */
    }
    throw new ApiError("EXPORT_FAILED", message, response.status);
  }

  const blob = await response.blob();
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}

// ---------------------------------------------------------------------------
// Content (tài liệu / bài giảng)
// ---------------------------------------------------------------------------

function listMaterials(chapter) {
  const qs = chapter ? `?chapter=${encodeURIComponent(chapter)}` : "";
  return apiFetch(`/api/v1/content/materials${qs}`);
}
function getMaterial(id) {
  return apiFetch(`/api/v1/content/materials/${id}`);
}

/** Uploads a raw file (PDF) to content-service, which forwards it to Cloudinary server-side —
 * the browser never holds a storage API key. Returns {fileUrl, fileName, fileSize, publicId};
 * pass those straight into createMaterial() to save the metadata record (two-step, same shape
 * as the old Supabase Storage flow, just both steps now go through the Gateway). */
async function uploadMaterialFile(file) {
  const headers = {};
  const token = getToken();
  if (token) headers.Authorization = `Bearer ${token}`;

  const formData = new FormData();
  formData.append("file", file);

  let response;
  try {
    response = await fetch(`${API_BASE}/api/v1/content/materials/upload`, {
      method: "POST",
      headers,
      body: formData,
    });
  } catch (networkError) {
    throw new ApiError("NETWORK_ERROR", "Không thể kết nối máy chủ. Kiểm tra lại kết nối mạng.", 0);
  }

  if (response.status === 401) {
    clearSession();
    window.location.href = "/auth.html";
    throw new ApiError("UNAUTHORIZED", "Phiên đăng nhập đã hết hạn.", 401);
  }

  const text = await response.text();
  const json = text ? JSON.parse(text) : { success: response.ok, data: null };

  if (!response.ok || json.success === false) {
    const error = json.error || {};
    throw new ApiError(error.code || "UNKNOWN_ERROR", error.message || "Đã xảy ra lỗi.", response.status, error.retryAfterSeconds ?? null);
  }

  return json.data;
}

function createMaterial(material) {
  return apiFetch("/api/v1/content/materials", { method: "POST", body: material });
}
function updateMaterial(id, material) {
  return apiFetch(`/api/v1/content/materials/${id}`, { method: "PUT", body: material });
}
function deleteMaterial(id) {
  return apiFetch(`/api/v1/content/materials/${id}`, { method: "DELETE" });
}
function incrementMaterialView(id) {
  return apiFetch(`/api/v1/content/materials/${id}/view`, { method: "POST" });
}

// ---------------------------------------------------------------------------
// Progress (tiến độ / bảng xếp hạng)
// ---------------------------------------------------------------------------

function logStudyTime(minutes) {
  return apiFetch("/api/v1/progress/study-logs", { method: "POST", body: { minutes } });
}
function getWeeklyStudyData() {
  return apiFetch("/api/v1/progress/study-logs/weekly");
}
function getMyProgress() {
  return apiFetch("/api/v1/progress/me");
}
function getLeaderboard(top = 30) {
  return apiFetch(`/api/v1/progress/leaderboard?top=${top}`);
}
// Việc C (2026-08-16) — bảng xếp hạng theo Lớp, thay cho getLeaderboard() toàn hệ thống ở
// xep-hang.html (getLeaderboard/progress-service giữ nguyên, không xóa — có nơi khác có thể còn
// tham chiếu, xem ghi chú dọn dead code sau). Nguồn quiz-service (Điểm TB Thi thử/Luyện tập tách
// riêng), tự xác thực quyền theo JWT — Student/Teacher chỉ xem đúng lớp mình, không nhận tham số
// nào khác ngoài lopId.
function getLopLeaderboard(lopId) {
  return apiFetch(`/api/v1/quiz/stats/leaderboard-by-lop?lopId=${encodeURIComponent(lopId)}`);
}

// ---------------------------------------------------------------------------
// AI (chatbot / giảng viên ảo / chấm vấn đáp / trích xuất câu hỏi)
// ---------------------------------------------------------------------------

function chatWithAI(messages) {
  return apiFetch("/api/v1/ai/chat", { method: "POST", body: { messages } });
}

/** Tiện ích gọi chatbot với 1 prompt đơn (dùng cho các tác vụ AI phụ, không có endpoint
 * riêng — vd tóm tắt giọng nói, sinh câu hỏi phụ trong vấn đáp — KHÔNG dùng cho việc chấm
 * điểm chính thức, việc đó luôn phải qua endpoint chuyên biệt như /grade-oral. */
async function askAI(prompt) {
  const result = await chatWithAI([{ role: "user", content: prompt }]);
  return result.reply;
}
/** partIndex/partTotal/previousTail hỗ trợ sinh bài giảng theo từng đoạn (chunk) cho tài liệu dài
 * — xem giang-bai.html buildChapters(). Mặc định (partIndex=0, partTotal=1) giữ nguyên hành vi gọi
 * 1 lần như cũ cho tài liệu ngắn. */
function generateLecture(chapter, topic, sourceText, partIndex = 0, partTotal = 1, previousTail = "") {
  return apiFetch("/api/v1/ai/generate-lecture", {
    method: "POST",
    body: { chapter, topic, sourceText, partIndex, partTotal, previousTail },
  });
}
function generateComprehensionQuestions(chapter, sourceText) {
  return apiFetch("/api/v1/ai/generate-comprehension-questions", { method: "POST", body: { chapter, sourceText } });
}
function extractQuestionsFromDocument(chapter, sourceText, count = 10) {
  return apiFetch("/api/v1/ai/extract-questions", { method: "POST", body: { chapter, sourceText, count } });
}
/** Sinh một bộ đề (trắc nghiệm + tự luận) từ nội dung tài liệu trong 1 lần gọi — dùng cho nút
 * "Sinh đề bằng AI" gắn theo từng tài liệu trong panel Tài liệu bài giảng. Chỉ trả về danh sách
 * ứng viên để giáo viên review/sửa, KHÔNG tự lưu vào ngân hàng câu hỏi. */
function generateExamSet(chapter, sourceText, mcqCount = 12, essayCount = 1) {
  return apiFetch("/api/v1/ai/generate-exam-set", {
    method: "POST",
    body: { chapter, sourceText, mcqCount, essayCount },
  });
}
/** "Kiểm tra nhanh kiến thức" (Student, giang-bai.html) — vài câu trắc nghiệm TẠM THỜI để tự làm
 * thử ngay, KHÔNG lưu vào ngân hàng câu hỏi (audit 2026-08-16 mục 3). materialId không bắt buộc —
 * học viên tự upload PDF (không qua Material nào) vẫn gọi được, chỉ truyền null. */
function quickCheck(materialId, chapter, sourceText) {
  return apiFetch("/api/v1/ai/quick-check", {
    method: "POST",
    body: { materialId: materialId || null, chapter, sourceText },
  });
}

// ---------------------------------------------------------------------------
// Quiz — Bộ đề / mã đề (C2)
// ---------------------------------------------------------------------------

function listExamSets() {
  return apiFetch("/api/v1/quiz/exam-sets");
}
function getExamSet(id) {
  return apiFetch(`/api/v1/quiz/exam-sets/${id}`);
}
function generateExamSetVersions(ten, poolQuestionIds, materialId, targetCount, versionCount, lopIds = []) {
  return apiFetch("/api/v1/quiz/exam-sets/generate", {
    method: "POST",
    body: { ten, poolQuestionIds, materialId, targetCount, versionCount, lopIds },
  });
}
/** Việc 5 (2026-08-16) — "Bộ đề VĐ mới" từ ngân hàng câu hỏi vấn đáp có sẵn (không sinh AI mới,
 * không có MaterialId vì OralQuestion không gắn nguồn tài liệu). */
function generateOralExamSetVersions(ten, poolOralQuestionIds, targetCount, versionCount, lopIds = []) {
  return apiFetch("/api/v1/quiz/exam-sets/generate-oral", {
    method: "POST",
    body: { ten, poolOralQuestionIds, targetCount, versionCount, lopIds },
  });
}
/** Việc 8 (2026-08-16) — sửa lại phạm vi hiển thị (theo Lớp) của 1 mã đề đã có. */
function updateExamVersionLopVisibility(versionId, lopIds) {
  return apiFetch(`/api/v1/quiz/exam-sets/versions/${versionId}/lop-visibility`, {
    method: "PUT",
    body: { lopIds },
  });
}

// ---------------------------------------------------------------------------
// Admin
// ---------------------------------------------------------------------------

function adminListUsers(role, lopId, khoaId) {
  const params = [];
  if (role) params.push(`role=${encodeURIComponent(role)}`);
  if (lopId) params.push(`lopId=${encodeURIComponent(lopId)}`);
  if (khoaId) params.push(`khoaId=${encodeURIComponent(khoaId)}`);
  const qs = params.length ? `?${params.join("&")}` : "";
  return apiFetch(`/api/v1/admin/users${qs}`);
}
function adminChangeRole(userId, role) {
  return apiFetch(`/api/v1/admin/users/${userId}/role`, { method: "PUT", body: { role } });
}
// Rà soát Lần XII (2026-08-21) — Admin tạo tài khoản trực tiếp (Role bất kỳ) + khóa/mở khóa.
function adminCreateUser(email, password, name, role) {
  return apiFetch("/api/v1/admin/users", { method: "POST", body: { email, password, name, role } });
}
function adminSetUserLocked(userId, isLocked) {
  return apiFetch(`/api/v1/admin/users/${userId}/locked`, { method: "PUT", body: { isLocked } });
}
function adminAuditLog(top = 50) {
  return apiFetch(`/api/v1/admin/audit-log?top=${top}`);
}
function adminGetConfig() {
  return apiFetch("/api/v1/admin/config");
}
function adminSetConfig(key, value) {
  return apiFetch(`/api/v1/admin/config/${encodeURIComponent(key)}`, { method: "PUT", body: { value } });
}
function adminStatsOverview() {
  return apiFetch("/api/v1/admin/stats/overview");
}
/** Việc 7 (2026-08-16) — Dashboard "Theo dõi Giáo viên". Danh sách sắp theo tên, KHÔNG phải bảng
 * xếp hạng (xem quyết định trong báo cáo Việc 7 — không hiện so sánh/xếp hạng hiệu suất GV). */
function adminStatsTeachers() {
  return apiFetch("/api/v1/admin/stats/teachers");
}
function adminStatsQuestionsByChapter() {
  return apiFetch("/api/v1/admin/stats/questions-by-chapter");
}
// Việc D (2026-08-16) — drill-down: từng Lớp của 1 giáo viên kèm điểm TB riêng, khác
// adminStatsTeachers() ở trên (gộp mọi Lớp thành 1 số). Gọi khi Admin bấm mở rộng 1 dòng GV.
function adminStatsTeacherLopQuality(teacherId) {
  return apiFetch(`/api/v1/admin/stats/teachers/${teacherId}/lop-quality`);
}
function adminStatsLop(lopId) {
  return apiFetch(`/api/v1/admin/stats/lop/${lopId}`);
}
function adminStatsKhoa(khoaId) {
  return apiFetch(`/api/v1/admin/stats/khoa/${khoaId}`);
}

// Việc 4.2 mục 3 (2026-08-19) — Xóa toàn bộ dữ liệu Lớp (hủy diệt, Admin-only, KHÔNG khôi phục).
// Flow bắt buộc 2 bước: prepareLopDeletion() TRƯỚC (chỉ đọc + dựng backup, không xóa gì) → Admin
// tải backup về máy → executeLopDeletion() chỉ được gọi SAU khi có PreparationId hợp lệ.
function prepareLopDeletion(lopId) {
  return apiFetch(`/api/v1/admin/lop/${lopId}/prepare-deletion`, { method: "POST" });
}
function executeLopDeletion(lopId, preparationId, confirmedLopTen) {
  return apiFetch(`/api/v1/admin/lop/${lopId}/execute-deletion`, {
    method: "POST",
    body: { preparationId, confirmedLopTen },
  });
}

// ---------------------------------------------------------------------------
// Khóa / Lớp — gọi thẳng auth-service (không qua admin-service, xem
// KhoaLopEndpoints.cs remarks: chưa có audit log tương đương /users/{id}/role
// nên không cần X-Internal-Key, chỉ RequireRole(Admin) như CRUD Khóa/Lớp khác).
// ---------------------------------------------------------------------------

function authListKhoa() {
  return apiFetch("/api/v1/auth/khoa");
}
// GET khoa/{id} và lop/{id} đơn lẻ mở cho mọi user đã đăng nhập (không riêng Admin) — dùng để
// học viên tự resolve tên Lớp/Khóa của mình từ LopId trả về ở /me, hiển thị lên hồ sơ.
function authGetKhoa(id) {
  return apiFetch(`/api/v1/auth/khoa/${id}`);
}
function authCreateKhoa(ten) {
  return apiFetch("/api/v1/auth/khoa", { method: "POST", body: { ten } });
}
function authUpdateKhoa(id, ten) {
  return apiFetch(`/api/v1/auth/khoa/${id}`, { method: "PUT", body: { ten } });
}
function authDeleteKhoa(id) {
  return apiFetch(`/api/v1/auth/khoa/${id}`, { method: "DELETE" });
}
function authListLop(khoaId) {
  const qs = khoaId ? `?khoaId=${encodeURIComponent(khoaId)}` : "";
  return apiFetch(`/api/v1/auth/lop${qs}`);
}
function authGetLop(id) {
  return apiFetch(`/api/v1/auth/lop/${id}`);
}
// Dùng chung ở mọi nơi hiển thị tóm tắt hồ sơ học viên (index.html, cai-dat.html) — thay cho
// profile.course/profile.className cũ (đã xóa khỏi User ở Bước A, xem Gap 1 fix).
async function resolveLopKhoaLabel(lopId) {
  if (!lopId) return "Chưa được gán vào lớp";
  try {
    const lop = await authGetLop(lopId);
    const khoa = await authGetKhoa(lop.khoaId);
    return `Lớp ${lop.ten} · Khóa ${khoa.ten}`;
  } catch {
    return "Không tải được thông tin Lớp/Khóa";
  }
}
function authCreateLop(ten, khoaId, namHoc) {
  return apiFetch("/api/v1/auth/lop", { method: "POST", body: { ten, khoaId, namHoc } });
}
// Việc V — namHoc bỏ qua (undefined) nghĩa là "không đổi" (backend giữ nguyên giá trị cũ); truyền
// chuỗi rỗng "" để xóa Năm học đã có. Không phá lời gọi cũ authUpdateLop(id, ten) 2 tham số vì
// JSON.stringify tự bỏ key có giá trị undefined.
// Rà soát Lần IX (2026-08-21) — thêm khoaId (cùng quy ước undefined = không đổi) để modal "Sửa
// lớp" chuyển được Lớp sang Khóa khác.
function authUpdateLop(id, ten, namHoc, khoaId) {
  return apiFetch(`/api/v1/auth/lop/${id}`, { method: "PUT", body: { ten, namHoc, khoaId } });
}
function authDeleteLop(id) {
  return apiFetch(`/api/v1/auth/lop/${id}`, { method: "DELETE" });
}
function authAssignGiaoVien(lopId, giaoVienId) {
  return apiFetch(`/api/v1/auth/lop/${lopId}/giao-vien`, { method: "PUT", body: { giaoVienId } });
}
// Rà soát Lần XVI (2026-08-21) — CRUD Môn học (panel "Quản lý Môn học").
function authListMonHoc() {
  return apiFetch("/api/v1/auth/mon-hoc");
}
function authCreateMonHoc(ten, maHocPhan, tinChi, giaoVienId) {
  return apiFetch("/api/v1/auth/mon-hoc", { method: "POST", body: { ten, maHocPhan, tinChi: Number(tinChi), giaoVienId: giaoVienId || null } });
}
function authUpdateMonHoc(id, ten, maHocPhan, tinChi, giaoVienId) {
  return apiFetch(`/api/v1/auth/mon-hoc/${id}`, { method: "PUT", body: { ten, maHocPhan, tinChi: Number(tinChi), giaoVienId: giaoVienId || null } });
}
function authDeleteMonHoc(id) {
  return apiFetch(`/api/v1/auth/mon-hoc/${id}`, { method: "DELETE" });
}
function authAssignMonHocLop(id, lopIds) {
  return apiFetch(`/api/v1/auth/mon-hoc/${id}/lop`, { method: "PUT", body: { lopIds } });
}
function authAssignLop(userId, lopId) {
  return apiFetch(`/api/v1/auth/users/${userId}/lop`, { method: "PUT", body: { lopId } });
}
function authChangeChucVu(userId, chucVu) {
  return apiFetch(`/api/v1/auth/users/${userId}/chuc-vu`, { method: "PUT", body: { chucVu } });
}
// Việc V (2026-08-20) — sửa Cấp bậc trực tiếp trong roster hợp nhất Admin.
function authChangeCapBac(userId, capBac) {
  return apiFetch(`/api/v1/auth/users/${userId}/cap-bac`, { method: "PUT", body: { capBac } });
}
// Gap 2 — Teacher tự lấy (các) Lớp mình phụ trách (không cần truyền id, backend tự suy ra từ JWT).
function authListMyLop() {
  return apiFetch("/api/v1/auth/lop/mine");
}
// Gap 2 — roster của 1 Lớp. Backend tự kiểm tra Admin hoặc đúng GV chủ nhiệm, trả 403 nếu không đúng.
function authListHocVien(lopId) {
  return apiFetch(`/api/v1/auth/lop/${lopId}/hoc-vien`);
}
// Gap 2 mục 2 — tìm học viên theo email để gán vào lớp (tối thiểu 3 ký tự, tối đa 10 kết quả, chỉ
// Role=Student — xem StudentSearchResponse remarks ở auth-service).
function authSearchStudentsByEmail(email) {
  return apiFetch(`/api/v1/auth/users/search-by-email?email=${encodeURIComponent(email)}`);
}
// Gap 2 mục 3 — nhật ký hoạt động của 1 Lớp. Backend tự kiểm tra Admin hoặc đúng GV chủ nhiệm.
function authListLopActivityLog(lopId, top = 50) {
  return apiFetch(`/api/v1/auth/lop/${lopId}/activity-log?top=${top}`);
}

// ---------------------------------------------------------------------------
// Format helpers (pure JS, no API calls)
// ---------------------------------------------------------------------------

function formatDate(isoStr) {
  if (!isoStr) return "";
  return new Date(isoStr).toLocaleDateString("vi-VN");
}

function formatTime(seconds) {
  const m = Math.floor(seconds / 60);
  const s = seconds % 60;
  return `${m}p ${s}s`;
}
