// ============================================================
// DÙNG CHUNG giữa teacher/quan-ly-noi-dung.html và admin/quan-tri-he-thong.html
// Sửa file này thì cả 2 trang đều nhận thay đổi (không cần sửa 2 nơi).
// Ngược lại, các khối <div class="panel">...</div> trong 2 file HTML là
// bản sao vật lý (không có build step để include HTML dùng chung) — mỗi
// khối HTML dùng chung có comment "DÙNG CHUNG với ..." nhắc sửa cả 2 bên.
//
// Yêu cầu ở trang gọi file này: đã load api-client.js trước, có sẵn các
// element #toast, #questionsList, #oralList, #matList, #teacherLopContainer
// (Teacher, Gap 2), #q*, #batch*, #xlsx*, #word*, #pasteArea/#pasteChapter/
// #pasteStatus, #oralImportStatus, #oralModal + #oral-chapter/#oral-q/#oral-a/
// #oral-diff, #matTitle/#matChapter/#matDesc/#matFileInput/#matProg*/
// #matUploadStatus, #profileNameInput/#profileEmailValue/#profilePhoneGroup+Input/
// #profileBoMonKhoaGroup+Input/#profileCapBacGroup+Input/#profileChucVuGVGroup+Input/
// #profileMonHocRow+Value/#profileLopPhuTrachRow+Value/#profileAvatarImg/
// #profileAvatarPlaceholder/#profileAvatarInput/#profileAvatarStatus/#profileSaveNameBtn/
// #profileSaveMsg (panel Hồ sơ cá nhân, dùng chung thật cả 2 trang — Rà soát Lần VI, 2026-08-21) +
// #cpCurrentInput/#cpNewInput/#cpConfirmInput/#cpMsg/#cpSubmitBtn (modal Đổi mật khẩu). Trang gọi file này phải
// tự khai báo `let me` (session user) trước khi các hàm dưới đây chạy, và tự
// gọi init() + document.addEventListener("DOMContentLoaded", init) ở script riêng.
// ============================================================

pdfjsLib.GlobalWorkerOptions.workerSrc =
  "https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174/pdf.worker.min.js";

let allQuestions = [],
  allOralQ = [],
  allMaterials = [];

// ============================================================
// VIỆC 8 (2026-08-16) — "Phạm vi hiển thị" (Toàn hệ thống / Chỉ Lớp cụ thể) dùng chung ở MỌI nơi
// lưu câu hỏi/bộ đề mới: modal Sinh đề AI (candidate-list C1 + Import nhánh AI), panel Import từ
// file (nhánh literal-parse), modal "Bộ đề mới" (Việc 5/7). LopIds rỗng = toàn hệ thống (mặc định,
// không đổi hành vi cũ). Widget đọc/ghi qua 4 hàm dưới — mỗi nơi dùng chỉ cần gọi
// scopePickerHtml(idPrefix) lúc render HTML, initScopePicker(idPrefix) lúc mở, và
// getScopePickerLopIds(idPrefix) lúc lưu. Danh sách Lớp nạp 1 lần/phiên (Teacher chỉ thấy Lớp
// mình chủ nhiệm qua authListMyLop(), Admin thấy mọi Lớp qua authListLop() — khớp đúng RBAC đã
// chốt ở backend, tránh hiện Lớp mà Teacher gán sẽ bị 403 khi lưu).
// ============================================================
let scopePickerLopCache = null;

async function loadScopePickerLopOptions() {
  if (scopePickerLopCache) return scopePickerLopCache;
  try {
    scopePickerLopCache = me.role === "Admin" ? await authListLop() : await authListMyLop();
  } catch {
    scopePickerLopCache = [];
  }
  return scopePickerLopCache;
}

// Rà soát Lần X (2026-08-21) — thay <select multiple> (phải giữ Ctrl để chọn nhiều, không rõ
// "chọn tất cả") bằng 1 list checkbox click-để-chọn + dòng "Chọn tất cả" riêng, đúng yêu cầu người
// dùng ("giảng viên click từng lớp, có thể chọn 1/2/3... lớp hoặc chọn tất cả"). Nguồn danh sách
// Lớp KHÔNG đổi — vẫn qua loadScopePickerLopOptions() (Teacher chỉ thấy Lớp Admin đã gán mình chủ
// nhiệm, Admin thấy mọi Lớp), giữ nguyên chữ ký 5 hàm public để không phải sửa nơi gọi.
function scopePickerHtml(idPrefix, label = "Phạm vi hiển thị") {
  return `<div class="form-group">
    <label class="form-label">${label}</label>
    <div style="display:flex;gap:14px;font-size:0.82rem;margin-bottom:6px;">
      <label style="display:flex;align-items:center;gap:4px;cursor:pointer;font-weight:400;">
        <input type="radio" name="${idPrefix}Mode" value="all" checked onchange="toggleScopePickerMode('${idPrefix}')"> Toàn hệ thống
      </label>
      <label style="display:flex;align-items:center;gap:4px;cursor:pointer;font-weight:400;">
        <input type="radio" name="${idPrefix}Mode" value="lop" onchange="toggleScopePickerMode('${idPrefix}')"> Chỉ Lớp cụ thể
      </label>
    </div>
    <div id="${idPrefix}LopList" style="display:none;border:1.5px solid var(--gray-200);border-radius:8px;max-height:200px;overflow-y:auto;">
      <label style="display:flex;align-items:center;gap:8px;padding:8px 10px;border-bottom:1px solid var(--gray-200);cursor:pointer;font-size:0.82rem;font-weight:600;background:var(--gray-50);position:sticky;top:0;">
        <input type="checkbox" id="${idPrefix}LopSelectAll" onchange="toggleScopePickerSelectAll('${idPrefix}')"> Chọn tất cả
      </label>
      <div id="${idPrefix}LopItems"></div>
    </div>
  </div>`;
}

/** preselectedLopIds khác rỗng (dùng khi sửa phạm vi câu hỏi/mã đề ĐÃ CÓ) → tự chuyển sang chế độ
 * "Chỉ Lớp cụ thể" và chọn sẵn đúng các Lớp đó, thay vì luôn mặc định "Toàn hệ thống". */
async function initScopePicker(idPrefix, preselectedLopIds = []) {
  const itemsEl = document.getElementById(`${idPrefix}LopItems`);
  if (!itemsEl) return;
  const lops = await loadScopePickerLopOptions();
  itemsEl.innerHTML = lops.length
    ? lops
        .map(
          (l) => `
      <label style="display:flex;align-items:center;gap:8px;padding:7px 10px;cursor:pointer;font-size:0.82rem;font-weight:400;border-bottom:1px solid var(--gray-100);">
        <input type="checkbox" class="${idPrefix}LopItemCheck" value="${l.id}" onchange="syncScopePickerSelectAll('${idPrefix}')"> ${escapeHtml(l.ten)}
      </label>`,
        )
        .join("")
    : `<div style="padding:10px;font-size:0.8rem;color:var(--gray-500);">Không có Lớp nào để chọn</div>`;

  if (preselectedLopIds.length) {
    const lopRadio = document.querySelector(`input[name="${idPrefix}Mode"][value="lop"]`);
    if (lopRadio) lopRadio.checked = true;
    const listEl = document.getElementById(`${idPrefix}LopList`);
    if (listEl) listEl.style.display = "block";
    itemsEl.querySelectorAll(`.${idPrefix}LopItemCheck`).forEach((cb) => (cb.checked = preselectedLopIds.includes(cb.value)));
    syncScopePickerSelectAll(idPrefix);
  }
}

function toggleScopePickerMode(idPrefix) {
  const checked = document.querySelector(`input[name="${idPrefix}Mode"]:checked`);
  const listEl = document.getElementById(`${idPrefix}LopList`);
  if (listEl) listEl.style.display = checked?.value === "lop" ? "block" : "none";
}

/** Dòng "Chọn tất cả" bấm → tick/bỏ tick toàn bộ checkbox Lớp bên dưới. */
function toggleScopePickerSelectAll(idPrefix) {
  const selectAll = document.getElementById(`${idPrefix}LopSelectAll`);
  if (!selectAll) return;
  document.querySelectorAll(`.${idPrefix}LopItemCheck`).forEach((cb) => (cb.checked = selectAll.checked));
}

/** Bấm 1 checkbox Lớp lẻ → đồng bộ lại trạng thái "Chọn tất cả" (checked nếu đủ hết, indeterminate
 * nếu chọn 1 phần, bỏ tick nếu không chọn gì). */
function syncScopePickerSelectAll(idPrefix) {
  const selectAll = document.getElementById(`${idPrefix}LopSelectAll`);
  if (!selectAll) return;
  const items = Array.from(document.querySelectorAll(`.${idPrefix}LopItemCheck`));
  const checkedCount = items.filter((cb) => cb.checked).length;
  selectAll.checked = items.length > 0 && checkedCount === items.length;
  selectAll.indeterminate = checkedCount > 0 && checkedCount < items.length;
}

function getScopePickerLopIds(idPrefix) {
  const checked = document.querySelector(`input[name="${idPrefix}Mode"]:checked`);
  if (checked?.value !== "lop") return [];
  return Array.from(document.querySelectorAll(`.${idPrefix}LopItemCheck:checked`)).map((cb) => cb.value);
}

function resetScopePicker(idPrefix) {
  const allRadio = document.querySelector(`input[name="${idPrefix}Mode"][value="all"]`);
  if (allRadio) allRadio.checked = true;
  const listEl = document.getElementById(`${idPrefix}LopList`);
  if (listEl) listEl.style.display = "none";
  document.querySelectorAll(`.${idPrefix}LopItemCheck`).forEach((cb) => (cb.checked = false));
  const selectAll = document.getElementById(`${idPrefix}LopSelectAll`);
  if (selectAll) {
    selectAll.checked = false;
    selectAll.indeterminate = false;
  }
}

// ── Sửa phạm vi hiển thị của câu hỏi/mã đề ĐÃ CÓ (retroactive edit — quyết định người dùng chọn
// khi được hỏi trực tiếp lúc bắt đầu Việc 8, khác với thiết kế ban đầu chỉ định chọn lúc TẠO MỚI). ──
let editLopVisibilityTarget = null; // { type: "question" | "examVersion" | "oralQuestion", id }

function openEditLopVisibilityModal(type, id, currentLopIds) {
  editLopVisibilityTarget = { type, id };
  document.getElementById("editLopVisibilityStatus").textContent = "";
  document.getElementById("editLopVisibilityScopeContainer").innerHTML = scopePickerHtml("editLopVisibilityScope");
  initScopePicker("editLopVisibilityScope", currentLopIds || []);
  openModal("editLopVisibilityModal");
}

async function saveLopVisibilityEdit() {
  if (!editLopVisibilityTarget) return;
  const { type, id } = editLopVisibilityTarget;
  const lopIds = getScopePickerLopIds("editLopVisibilityScope");
  const statusEl = document.getElementById("editLopVisibilityStatus");
  const btn = document.getElementById("editLopVisibilitySaveBtn");

  btn.disabled = true;
  statusEl.style.color = "var(--gray-500)";
  statusEl.textContent = "Đang lưu...";
  try {
    if (type === "question") {
      await updateQuestionLopVisibility(id, lopIds);
      if (typeof loadQuestions === "function") loadQuestions();
    } else if (type === "examVersion") {
      await updateExamVersionLopVisibility(id, lopIds);
      if (typeof loadExamSetsList === "function") loadExamSetsList();
    } else if (type === "oralQuestion") {
      await updateOralQuestionLopVisibility(id, lopIds);
      if (typeof loadOralQuestions === "function") loadOralQuestions();
    }
    showToast("Đã cập nhật phạm vi hiển thị", "success");
    closeModal("editLopVisibilityModal");
  } catch (err) {
    statusEl.style.color = "#c62828";
    statusEl.textContent = "Lỗi: " + err.message;
  } finally {
    btn.disabled = false;
  }
}

// ============================================================
// PANEL SWITCHING — generic: các panel role-riêng (VD "students", "settings"
// bản đầy đủ) chỉ tồn tại ở admin/quan-tri-he-thong.html; ở
// teacher/quan-ly-noi-dung.html các hàm loadStudents/loadAuditLog/
// loadSystemConfig không được định nghĩa nên các nhánh if dưới đây tự
// bỏ qua (typeof === "function" guard), không lỗi.
//
// "overview": Dashboard của Admin đã tách hẳn khỏi Teacher (không còn HTML
// dùng chung) — Admin dùng loadAdminDashboard() định nghĩa riêng trong
// admin/quan-tri-he-thong.html, Teacher vẫn dùng loadOverview() dưới đây.
// ============================================================
function showPanel(name) {
  document
    .querySelectorAll(".panel")
    .forEach((p) => p.classList.remove("active"));
  document
    .querySelectorAll(".sidebar-item")
    .forEach((i) => i.classList.remove("active"));
  document.getElementById("panel-" + name).classList.add("active");
  event?.target?.closest(".sidebar-item")?.classList.add("active");
  // VIỆC 2 mục 1 — breadcrumb (Admin-only, định nghĩa trong admin/quan-tri-he-thong.html), cùng
  // convention guard typeof với loadKhoaList/loadAdminDashboard bên dưới.
  if (typeof updateBreadcrumb === "function") updateBreadcrumb(name);
  // VIỆC 2 phần cuối — tự thu gọn sidebar trên màn nhỏ sau khi chọn 1 mục (Admin-only; optional-
  // chaining nên vô hại ở Teacher, trang đó không có #sidebarNav/#sidebarToggle).
  document.getElementById("sidebarNav")?.classList.remove("expanded");
  document.getElementById("sidebarToggle")?.classList.remove("expanded");
  if (name === "materials") loadMaterials();
  if (name === "profile") loadProfilePanel();
  if (name === "overview") {
    if (typeof loadAdminDashboard === "function" && me.role === "Admin") loadAdminDashboard();
    else loadOverview();
  }
  if (name === "questions") loadQuestions();
  if (name === "import" && typeof loadExamSetsList === "function") loadExamSetsList();
  // Việc 8 — render 1 lần duy nhất (không ghi đè lựa chọn dở dang của giáo viên mỗi lần chuyển
  // panel qua lại), khớp guard idempotent giống cách batchImportScopeContainer chỉ là 1 panel tĩnh
  // (không phải modal mở lại từ đầu như examGenScopeContainer).
  if (name === "import") {
    const container = document.getElementById("batchImportScopeContainer");
    if (container && !container.innerHTML) {
      container.innerHTML = scopePickerHtml("batchImportScope");
      initScopePicker("batchImportScope");
    }
    // Việc 4.4 Phần A (2026-08-20) — cùng pattern batchImportScopeContainer ở trên, nhưng riêng
    // cho import Excel Tự luận (khác ngân hàng TN, không dùng chung 1 picker).
    const oralContainer = document.getElementById("oralImportScopeContainer");
    if (oralContainer && !oralContainer.innerHTML) {
      oralContainer.innerHTML = scopePickerHtml("oralImportScope");
      initScopePicker("oralImportScope");
    }
    // Việc III (2026-08-20) — picker RIÊNG cho khối "Dán văn bản trực tiếp", không còn ngầm dùng
    // chung lựa chọn của batchImportScope.
    const pasteContainer = document.getElementById("pasteScopeContainer");
    if (pasteContainer && !pasteContainer.innerHTML) {
      pasteContainer.innerHTML = scopePickerHtml("pasteScope");
      initScopePicker("pasteScope");
    }
  }
  if (name === "students" && typeof loadStudents === "function" && me.role === "Admin")
    loadStudents();
  if (name === "oral") loadOralQuestions();
  if (name === "settings" && typeof loadAuditLog === "function" && me.role === "Admin") {
    loadAuditLog();
    loadSystemConfig();
  }
  if (name === "khoalop" && typeof loadKhoaList === "function" && me.role === "Admin")
    loadKhoaList();
  // Gap 2 mục 3 — Teacher-only, định nghĩa trong <script> riêng của
  // teacher/quan-ly-noi-dung.html (không phải admin-common.js), cùng convention với
  // loadKhoaList/loadStudents ở trên (Admin-only, định nghĩa trong admin/quan-tri-he-thong.html).
  // (Rà soát Lần III, mục A — panel "members" hợp nhất vào "lopcrud", đã xóa dispatch riêng.)
  if (name === "lopaudit" && typeof loadLopAudit === "function") loadLopAudit();
  // Việc 4.2 (2026-08-19) — Teacher-only, định nghĩa trong <script> riêng của
  // teacher/quan-ly-noi-dung.html, cùng convention với members/lopaudit ở trên.
  if (name === "lopcrud" && typeof loadLopCrud === "function") loadLopCrud();
  if (name === "leaderboard" && typeof loadTeacherLeaderboard === "function") loadTeacherLeaderboard();
  // Rà soát Lần XIII (2026-08-21) — Admin-only, định nghĩa trong admin/quan-tri-he-thong.html.
  // "leaderboard" trùng tên panel với Teacher (2 hàm khác nhau) — không xung đột vì mỗi guard chỉ
  // khớp đúng hàm tồn tại trên trang đang chạy (Admin không có loadTeacherLeaderboard, Teacher
  // không có loadAdminTeacherLeaderboard).
  if (name === "leaderboard" && typeof loadAdminTeacherLeaderboard === "function") loadAdminTeacherLeaderboard();
  if (name === "teachers" && typeof loadTeachersManagement === "function") loadTeachersManagement();
  if (name === "subjects" && typeof loadMonHocList === "function") loadMonHocList();
}

// ============================================================
// OVERVIEW (Teacher only) — Gap 2: "Lớp tôi phụ trách" — roster + Chức vụ
// (badge màu + select sửa) + điểm Kiểm tra từng học viên + điểm TB lớp.
// Xem loadAdminDashboard() trong admin/quan-tri-he-thong.html cho Admin — 2
// dashboard đã tách hẳn, không còn dùng chung hàm/HTML này.
//
// Việc 4.3 (2026-08-20) — CHỐNG TRÀN: adminStatsLop() (điểm TB + sĩ số + roster đầy đủ
// stats.hocVien nằm CHUNG 1 response, backend không có endpoint tách riêng) vẫn phải gọi 1 lần cho
// MỌI lớp để hiện được sĩ số/điểm TB ở dòng tóm tắt — nhưng phần NẶNG thật sự (2 canvas Chart.js +
// bảng roster đầy đủ không phân trang, nhân với N lớp) giờ chỉ dựng DOM khi người dùng bấm mở rộng
// 1 lớp cụ thể. teacherLopSummaries cache nguyên response gốc trong bộ nhớ phiên — thu gọn/mở lại
// 1 lớp KHÔNG gọi lại API, chỉ toggle render.
// ============================================================
let teacherLopSummaries = []; // [{ lop, khoa, stats }] — cache cả phiên, dùng lại khi mở/thu gọn
let teacherLopExpanded = new Set(); // lopId đang mở rộng (nhiều lớp có thể mở cùng lúc)
let teacherLopRosterState = {}; // lopId -> { page, pageSize, sortKey, sortDir } cho bảng roster

async function loadOverview() {
  const container = document.getElementById("teacherLopContainer");
  if (!container) return;
  container.innerHTML = '<div class="empty"><i class="fas fa-spinner fa-spin"></i> Đang tải lớp phụ trách...</div>';

  let myLop;
  try {
    myLop = await authListMyLop();
  } catch (err) {
    container.innerHTML = `<div class="empty">Lỗi tải danh sách lớp: ${err.message}</div>`;
    return;
  }

  if (!myLop.length) {
    container.innerHTML = `
      <div class="empty">
        <i class="fas fa-chalkboard" style="font-size: 2rem; color: var(--gray-300); margin-bottom: 8px; display: block;"></i>
        Bạn hiện chưa được phân công chủ nhiệm lớp nào.
        <div style="font-size: 0.78rem; color: var(--gray-500); margin-top: 4px;">
          Liên hệ Admin để được gán làm giáo viên chủ nhiệm 1 Lớp.
        </div>
      </div>`;
    return;
  }

  const khoaCache = {};
  const getKhoa = (khoaId) => {
    if (!khoaCache[khoaId]) khoaCache[khoaId] = authGetKhoa(khoaId).catch(() => ({ ten: "?" }));
    return khoaCache[khoaId];
  };

  teacherLopSummaries = await Promise.all(
    myLop.map(async (lop) => {
      const [khoa, stats] = await Promise.all([
        getKhoa(lop.khoaId),
        adminStatsLop(lop.id).catch((err) => ({ error: err.message })),
      ]);
      return { lop, khoa, stats };
    }),
  );
  teacherLopExpanded = new Set();
  teacherLopRosterState = {};
  renderTeacherLopList();
}

function chucVuBadge(chucVu) {
  const cls = chucVu === "Lớp trưởng" ? "cv-loptruong" : chucVu === "Lớp phó" ? "cv-loppho" : "cv-hocvien";
  return `<span class="chucvu-badge ${cls}">${chucVu}</span>`;
}

function renderTeacherLopList() {
  const container = document.getElementById("teacherLopContainer");
  if (!container) return;
  container.innerHTML = teacherLopSummaries.map(({ lop, khoa, stats }) => renderTeacherLopCard(lop, khoa, stats)).join("");

  // Chart.js cần canvas đã có thật trong DOM, và bảng roster cần tbody đã render — cả 2 chỉ tồn
  // tại trong chuỗi HTML của những lớp ĐANG mở rộng (xem renderTeacherLopCard) nên chỉ cần lặp qua
  // đúng tập đó, không phải toàn bộ danh sách.
  for (const { lop, stats } of teacherLopSummaries) {
    if (teacherLopExpanded.has(lop.id) && !stats.error) {
      drawLopCharts(lop.id, stats);
      renderTeacherLopRosterPage(lop.id);
    }
  }
}

function toggleTeacherLopCard(lopId) {
  if (teacherLopExpanded.has(lopId)) teacherLopExpanded.delete(lopId);
  else teacherLopExpanded.add(lopId);
  renderTeacherLopList();
}

function renderTeacherLopCard(lop, khoa, stats) {
  if (stats.error) {
    return `
      <div class="card">
        <div class="card-title"><i class="fas fa-chalkboard"></i> ${lop.ten} · Khóa ${khoa.ten || "?"}</div>
        <div class="empty">Lỗi tải điểm lớp: ${stats.error}</div>
      </div>`;
  }

  const fmtScore = (v) => (v === null || v === undefined ? "—" : Number(v).toFixed(2));
  const expanded = teacherLopExpanded.has(lop.id);

  return `
    <div class="card">
      <div class="card-title" style="display:flex; align-items:center; justify-content:space-between; cursor:pointer;" onclick="toggleTeacherLopCard('${lop.id}')">
        <span><i class="fas fa-chalkboard"></i> ${lop.ten} · Khóa ${khoa.ten || "?"} <span style="font-weight:400; color:var(--gray-500); font-size:0.78rem;">(${stats.tongHocVien} học viên)</span></span>
        <i class="fas fa-chevron-${expanded ? "up" : "down"}"></i>
      </div>
      <div class="overview-grid" style="grid-template-columns: 1fr; margin-bottom: 14px;">
        <div class="ov-card">
          <div class="ov-value" style="color: #1565c0">${fmtScore(stats.diemTBThiThu)}</div>
          <div class="ov-label">Điểm TB Kiểm tra (${stats.tongLuotThiThu} lượt)</div>
        </div>
      </div>
      ${
        expanded
          ? `
      <div style="display: flex; gap: 10px; flex-wrap: wrap; margin-bottom: 14px;">
        <div style="flex: 1; min-width: 220px; height: 190px;">
          <canvas id="lopHistogram-${lop.id}"></canvas>
        </div>
        <div style="flex: 1; min-width: 220px; height: 190px;">
          <canvas id="lopRadar-${lop.id}"></canvas>
        </div>
      </div>
      <div class="data-table-wrap">
        <table class="data-table">
          <thead>
            <tr>
              <th>Học viên</th>
              <th>Chức vụ</th>
              <th>Kiểm tra</th>
            </tr>
          </thead>
          <tbody id="teacherLopRosterRows-${lop.id}"></tbody>
        </table>
      </div>
      <div id="teacherLopRosterPag-${lop.id}"></div>`
          : `<button class="btn btn-outline btn-sm" onclick="toggleTeacherLopCard('${lop.id}')"><i class="fas fa-chevron-down"></i> Xem chi tiết (biểu đồ + danh sách học viên)</button>`
      }
    </div>`;
}

// Chỉ hiển thị (badge), KHÔNG sửa được ở đây nữa — sửa Chức vụ đã chuyển hẳn sang panel
// "Danh sách thành viên" (Gap 2 mục 2, teacher/quan-ly-noi-dung.html's own <script>) để tránh 2
// nơi cùng sửa 1 dữ liệu ("không làm trùng 2 chỗ" theo yêu cầu).
function renderTeacherLopRosterPage(lopId) {
  const entry = teacherLopSummaries.find((s) => s.lop.id === lopId);
  const tbody = document.getElementById(`teacherLopRosterRows-${lopId}`);
  if (!entry || !tbody) return;

  const hocVien = entry.stats.hocVien || [];
  const fmtScore = (v) => (v === null || v === undefined ? "—" : Number(v).toFixed(2));

  if (!hocVien.length) {
    tbody.innerHTML = '<tr><td colspan="3" class="empty">Lớp chưa có học viên</td></tr>';
    document.getElementById(`teacherLopRosterPag-${lopId}`).innerHTML = "";
    return;
  }

  teacherLopRosterState[lopId] = teacherLopRosterState[lopId] || { page: 1, pageSize: 10, sortKey: null, sortDir: "asc" };
  const state = teacherLopRosterState[lopId];
  const { pageItems, totalPages } = paginateAndSort(state, hocVien);

  tbody.innerHTML = pageItems
    .map(
      (h) => `
    <tr>
      <td>${h.hoTen}</td>
      <td>${chucVuBadge(h.chucVu)}</td>
      <td>${fmtScore(h.diemThiThu)} <span style="color: var(--gray-400); font-size: 0.7rem;">(${h.soLuotThiThu} lượt)</span></td>
    </tr>`,
    )
    .join("");

  // renderPaginationControls (admin-common.js) giả định 1 instance/trang (gọi onPageChangeFn(page)
  // trực tiếp) — ở đây có THỂ nhiều lớp mở rộng cùng lúc, mỗi lớp 1 bảng riêng, nên tự dựng phần
  // pagination gắn kèm lopId thay vì dùng chung hàm đó (vẫn dùng chung paginateAndSort ở trên).
  const pagEl = document.getElementById(`teacherLopRosterPag-${lopId}`);
  if (totalPages <= 1) {
    pagEl.innerHTML = "";
  } else {
    pagEl.innerHTML = `
      <div class="pagination">
        <button class="btn btn-outline btn-sm" ${state.page === 1 ? "disabled" : ""} onclick="changeTeacherLopRosterPage('${lopId}', ${state.page - 1})">
          <i class="fas fa-chevron-left"></i>
        </button>
        <span class="pagination-info">Trang ${state.page}/${totalPages} (${state.total ?? ""})</span>
        <button class="btn btn-outline btn-sm" ${state.page === totalPages ? "disabled" : ""} onclick="changeTeacherLopRosterPage('${lopId}', ${state.page + 1})">
          <i class="fas fa-chevron-right"></i>
        </button>
      </div>`;
  }
}

function changeTeacherLopRosterPage(lopId, page) {
  teacherLopRosterState[lopId] = teacherLopRosterState[lopId] || { page: 1, pageSize: 10, sortKey: null, sortDir: "asc" };
  teacherLopRosterState[lopId].page = page;
  renderTeacherLopRosterPage(lopId);
}

// Việc 7 (2026-08-16) — nâng cấp từ số liệu tĩnh sang biểu đồ trực quan, dùng ĐÚNG dữ liệu
// stats.hocVien đã có sẵn (không gọi thêm API nào) — histogram phân bố điểm học viên (bar) + radar
// tổng quan hiệu suất lớp (điểm TB Kiểm tra + tỷ lệ tham gia). Lưu Chart instance theo id để
// destroy() trước khi vẽ lại — tránh cảnh báo "Canvas is already in use" khi loadOverview() chạy
// lại (đổi lớp, quay lại Dashboard).
let lopChartInstances = {};

function drawLopCharts(lopId, stats) {
  const buckets = ["0-2", "2-4", "4-6", "6-8", "8-10"];
  const bucketIndex = (score) => Math.min(4, Math.max(0, Math.floor(score / 2)));
  const examBuckets = [0, 0, 0, 0, 0];
  for (const h of stats.hocVien) {
    if (h.diemThiThu !== null && h.diemThiThu !== undefined) examBuckets[bucketIndex(h.diemThiThu)]++;
  }

  const histId = `lopHistogram-${lopId}`;
  const histCanvas = document.getElementById(histId);
  if (histCanvas) {
    if (lopChartInstances[histId]) lopChartInstances[histId].destroy();
    lopChartInstances[histId] = new Chart(histCanvas, {
      type: "bar",
      data: {
        labels: buckets,
        datasets: [
          { label: "Kiểm tra", data: examBuckets, backgroundColor: "#1565c0" },
        ],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: { legend: { position: "bottom", labels: { font: { size: 10 }, boxWidth: 12 } } },
        scales: { y: { beginAtZero: true, ticks: { precision: 0 } } },
      },
    });
  }

  const total = stats.hocVien.length;
  const participated = stats.hocVien.filter((h) => (h.soLuotThiThu || 0) > 0).length;
  const participationRate = total > 0 ? Math.round((participated / total) * 100) : 0;

  const radarId = `lopRadar-${lopId}`;
  const radarCanvas = document.getElementById(radarId);
  if (radarCanvas) {
    if (lopChartInstances[radarId]) lopChartInstances[radarId].destroy();
    lopChartInstances[radarId] = new Chart(radarCanvas, {
      type: "radar",
      data: {
        labels: ["Điểm TB Kiểm tra", "Tỷ lệ tham gia"],
        datasets: [
          {
            // Quy điểm (thang 0-10) về cùng trục 0-100 với tỷ lệ tham gia (%) để radar cân đối —
            // tooltip bên dưới hiện lại giá trị thật, không phải giá trị đã quy đổi.
            data: [Number(stats.diemTBThiThu || 0) * 10, participationRate],
            backgroundColor: "rgba(20, 90, 58, 0.2)",
            borderColor: "#145a3a",
          },
        ],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        scales: { r: { beginAtZero: true, max: 100, ticks: { stepSize: 25, font: { size: 9 } } } },
        plugins: {
          legend: { display: false },
          tooltip: {
            callbacks: {
              label: (ctx) => (ctx.dataIndex < 1 ? `${(ctx.raw / 10).toFixed(2)}/10` : `${ctx.raw}%`),
            },
          },
        },
      },
    });
  }
}

// ============================================================
// QUESTIONS (trắc nghiệm) — quiz-service question bank
// ============================================================
let questionsLoadSeq = 0; // showPanel("questions") tự gọi loadQuestions() ngầm — chặn response cũ
// ghi đè response mới hơn khi 2 lệnh gọi chồng nhau (vd goToQuestionsFilteredByChapter()).
async function loadQuestions() {
  const mySeq = ++questionsLoadSeq;
  let result;
  try {
    result = await listQuestionsBank();
  } catch (err) {
    result = [];
    showToast("Lỗi tải câu hỏi: " + err.message, "error");
  }
  if (mySeq !== questionsLoadSeq) return; // đã có lệnh gọi mới hơn, bỏ qua kết quả cũ
  allQuestions = result;
  const chapters = [
    ...new Set(allQuestions.map((q) => q.chapter).filter(Boolean)),
  ];
  const sel = document.getElementById("qChapter");
  sel.innerHTML =
    '<option value="">Tất cả chương</option>' +
    chapters.map((c) => `<option value="${c}">${c}</option>`).join("");
  renderQuestions(allQuestions);
}

// ── Việc 4 (2026-08-16) — nhóm câu hỏi theo Chương: card thu gọn (tên chương, ngày cập nhật gần
// nhất, loại, số lượng), bấm mở rộng xem đầy đủ danh sách (giữ nguyên UI chi tiết từng câu). Có
// tìm kiếm hoặc đã chọn đúng 1 chương cụ thể ở dropdown → bỏ qua nhóm, hiện thẳng danh sách khớp.
function groupQuestionsByChapter(list) {
  const groups = new Map();
  for (const q of list) {
    const ch = q.chapter || "Chung";
    if (!groups.has(ch)) groups.set(ch, []);
    groups.get(ch).push(q);
  }
  return [...groups.entries()].sort((a, b) => {
    const latestA = Math.max(...a[1].map((q) => new Date(q.createdAtUtc).getTime()));
    const latestB = Math.max(...b[1].map((q) => new Date(q.createdAtUtc).getTime()));
    return latestB - latestA;
  });
}

/** cardsHtmlFn(items) trả về HTML của danh sách câu hỏi chi tiết bên trong 1 nhóm đã mở rộng —
 * dùng chung cho cả TN (questionCardsHtml) và VĐ (oralCardsHtml). */
function renderChapterGroups(list, { typeLabel, expandedSet, toggleFnName, cardsHtmlFn }) {
  return groupQuestionsByChapter(list)
    .map(([chapter, items]) => {
      const latestMs = Math.max(...items.map((q) => new Date(q.createdAtUtc).getTime()));
      const dateStr = new Date(latestMs).toLocaleString("vi-VN");
      const expanded = expandedSet.has(chapter);
      const safeChapter = chapter.replace(/'/g, "\\'");
      return `<div class="q-group-card">
        <div class="q-group-header" onclick="${toggleFnName}('${safeChapter}')">
          <div>
            <div class="q-group-title">${escapeHtml(chapter)}</div>
            <div class="q-group-meta">${typeLabel} · ${items.length} câu · cập nhật ${dateStr}</div>
          </div>
          <i class="fas fa-chevron-${expanded ? "up" : "down"}"></i>
        </div>
        ${expanded ? `<div class="q-group-body">${cardsHtmlFn(items)}</div>` : ""}
      </div>`;
    })
    .join("");
}

let questionsExpandedChapters = new Set();

function questionCardsHtml(list) {
  const letters = ["A", "B", "C", "D"];
  return list
    .map((q, i) => {
      const opts = [q.optionA, q.optionB, q.optionC, q.optionD].filter(Boolean);
      const publishBadge = q.isPublished
        ? `<span class="q-opt" style="background:#e8f5e9;color:#2e7d32;">✓ Đã xuất bản</span>`
        : `<span class="q-opt" style="background:#fff3e0;color:#b45309;">Chưa xuất bản</span>`;
      // Việc 8 — badge phạm vi hiển thị: rỗng = toàn hệ thống (mặc định/cũ), có phần tử = giới hạn.
      const lopIds = q.lopIds || [];
      const scopeBadge = lopIds.length
        ? `<span class="q-opt" style="background:#ede7f6;color:#4527a0;">🔒 ${lopIds.length} Lớp</span>`
        : `<span class="q-opt" style="background:#eceff1;color:#455a64;">🌐 Toàn hệ thống</span>`;
      return `<div class="q-card">
      <div style="display:flex;justify-content:space-between;align-items:flex-start;gap:8px;">
        <input type="checkbox" ${selectedQuestionIdsForExport.has(q.id) ? "checked" : ""} onchange="toggleQuestionExportSelect('${q.id}', this.checked)" style="margin-top:4px;flex-shrink:0;" aria-label="Chọn câu hỏi ${i + 1} để xuất Word">
        <div style="flex:1;">
          <div class="q-chapter">${escapeHtml(q.chapter || "Chung")} &nbsp; ${publishBadge} ${scopeBadge}</div>
          <div class="q-text">${i + 1}. ${escapeHtml(q.questionText)}</div>
          <div class="q-opts">${opts.map((o, j) => `<span class="q-opt ${j === q.correctAnswer ? "correct" : ""}">${letters[j]}. ${escapeHtml(o)}</span>`).join("")}</div>
          ${q.explanation ? `<div style="font-size:0.72rem;color:var(--gray-500);margin-top:4px;">💡 ${escapeHtml(q.explanation)}</div>` : ""}
        </div>
        <div style="display:flex;flex-direction:column;gap:4px;flex-shrink:0;">
          <button onclick="toggleQuestionPublish('${q.id}')" style="background:none;border:none;cursor:pointer;color:${q.isPublished ? "#b45309" : "#2e7d32"};padding:4px;" title="${q.isPublished ? "Gỡ xuất bản" : "Xuất bản câu hỏi"}" aria-label="${q.isPublished ? "Gỡ xuất bản câu hỏi" : "Xuất bản câu hỏi"} ${i + 1}"><i class="fas fa-${q.isPublished ? "eye-slash" : "eye"}"></i></button>
          <button onclick='openEditLopVisibilityModal("question", "${q.id}", ${JSON.stringify(lopIds)})' style="background:none;border:none;cursor:pointer;color:var(--gray-400);padding:4px;" title="Sửa phạm vi hiển thị" aria-label="Sửa phạm vi hiển thị câu hỏi ${i + 1}"><i class="fas fa-users"></i></button>
          <button onclick="deleteQuestionRow('${q.id}')" style="background:none;border:none;cursor:pointer;color:var(--gray-400);padding:4px;" title="Xóa" aria-label="Xóa câu hỏi"><i class="fas fa-trash"></i></button>
        </div>
      </div>
    </div>`;
    })
    .join("");
}

function toggleQuestionsGroup(chapter) {
  if (questionsExpandedChapters.has(chapter)) questionsExpandedChapters.delete(chapter);
  else questionsExpandedChapters.add(chapter);
  filterQuestions();
}

function renderQuestions(list) {
  document.getElementById("qCount").textContent = `${list.length} câu hỏi`;
  const el = document.getElementById("questionsList");
  if (!list.length) {
    el.innerHTML =
      '<div class="empty"><i class="fas fa-inbox"></i><br>Chưa có câu hỏi. Nhấn Import để thêm.</div>';
    return;
  }

  const search = document.getElementById("qSearch")?.value.trim();
  const chapterFilter = document.getElementById("qChapter")?.value;
  if (search || chapterFilter) {
    // Tìm kiếm hoặc đã chọn đúng 1 chương → hiện thẳng danh sách khớp, bỏ qua nhóm (vẫn giữ cap
    // 50 câu như hành vi cũ, tránh render quá nhiều DOM node cùng lúc).
    el.innerHTML =
      questionCardsHtml(list.slice(0, 50)) +
      (list.length > 50
        ? `<div style="grid-column:1/-1;text-align:center;padding:12px;font-size:0.78rem;color:var(--gray-400);">Hiển thị 50/${list.length} câu. Import thêm để bổ sung.</div>`
        : "");
    return;
  }

  el.innerHTML = renderChapterGroups(list, {
    typeLabel: "Trắc nghiệm",
    expandedSet: questionsExpandedChapters,
    toggleFnName: "toggleQuestionsGroup",
    cardsHtmlFn: questionCardsHtml,
  });
}

// ── XUẤT WORD (C4) — tick chọn nhiều câu trong ngân hàng TN rồi xuất, dùng lại export endpoint
// dùng chung với candidate list C1 (exportQuestionsToWord trong api-client.js). ──
let selectedQuestionIdsForExport = new Set();

function toggleQuestionExportSelect(id, checked) {
  if (checked) selectedQuestionIdsForExport.add(id);
  else selectedQuestionIdsForExport.delete(id);
  const count = selectedQuestionIdsForExport.size;
  document.getElementById("qExportSelectedCount").textContent = count;
  document.getElementById("qExportWordBtn").disabled = count === 0;
}

async function exportSelectedQuestionsToWord() {
  const ids = Array.from(selectedQuestionIdsForExport);
  if (!ids.length) return;
  const btn = document.getElementById("qExportWordBtn");
  btn.disabled = true;
  try {
    await exportQuestionsToWord(ids, [], "ngan-hang-cau-hoi.docx");
    showToast("Đã xuất file Word", "success");
  } catch (err) {
    showToast("Lỗi xuất Word: " + err.message, "error");
  } finally {
    btn.disabled = selectedQuestionIdsForExport.size === 0;
  }
}

// ════════════════════════════════════════════
// C2 — TRÍCH XUẤT BỘ ĐỀ LỚN (150→50 + sinh nhiều mã đề). AI trích xuất theo TỪNG ĐỢT nhỏ (25
// câu/lần — đúng giới hạn Count hiện có của POST /ai/extract-questions, KHÔNG đổi giới hạn đó)
// rồi gộp lại thành 1 pool lớn, vì 1 lần gọi LLM duy nhất không đủ token để sinh ổn định ~150 câu
// JSON hợp lệ cùng lúc (đã thấy JSON bị lỗi định dạng ngay cả với response nhỏ hơn nhiều ở C1).
// ════════════════════════════════════════════
let examPoolQuestionIds = [];
let examVersionsData = null;

async function extractPdfFileText(file) {
  const buffer = await file.arrayBuffer();
  const pdf = await pdfjsLib.getDocument({ data: buffer }).promise;
  let fullText = "";
  for (let p = 1; p <= Math.min(pdf.numPages, 60); p++) {
    const page = await pdf.getPage(p);
    const tc = await page.getTextContent();
    fullText += tc.items.map((i) => i.str).join(" ") + "\n";
  }
  return fullText;
}

// Việc III (2026-08-20) — hiện tên file đã chọn bằng tiếng Việt, thay cho text native "No file
// chosen" của trình duyệt (input đã bị ẩn bằng overlay, xem .import-area).
function onPoolFileSelected(input) {
  const label = document.getElementById("poolFileName");
  label.textContent = input.files[0]?.name || "Chọn file PDF nội dung lớn...";
}

async function extractLargeQuestionPool() {
  const fileInput = document.getElementById("poolFileInput");
  const file = fileInput.files[0];
  const chapter = document.getElementById("poolChapter").value.trim();
  const totalWanted = parseInt(document.getElementById("poolTotalCount").value, 10) || 150;
  const statusEl = document.getElementById("poolExtractStatus");
  const btn = document.getElementById("poolExtractBtn");

  if (!file) {
    statusEl.style.color = "#c62828";
    statusEl.textContent = "Vui lòng chọn file PDF.";
    return;
  }
  if (!chapter) {
    statusEl.style.color = "#c62828";
    statusEl.textContent = "Vui lòng nhập Chương/Phần.";
    return;
  }

  btn.disabled = true;
  examPoolQuestionIds = [];
  document.getElementById("poolGenerateForm").style.display = "none";
  document.getElementById("examVersionsResult").innerHTML = "";

  try {
    statusEl.style.color = "var(--gray-500)";
    statusEl.textContent = "Đang đọc nội dung file...";
    const text = await extractPdfFileText(file);
    if (!text.trim()) {
      statusEl.style.color = "#c62828";
      statusEl.textContent = "Không trích được nội dung văn bản từ file này.";
      return;
    }
    const sourceText = text.slice(0, 8000);

    const BATCH_SIZE = 25;
    const MAX_BATCHES = 12; // an toàn — tránh vòng lặp kéo dài vô hạn nếu AI liên tục lỗi
    let createdCount = 0;
    let batchIndex = 0;

    while (createdCount < totalWanted && batchIndex < MAX_BATCHES) {
      batchIndex++;
      statusEl.textContent = `Đang trích xuất... (${createdCount}/${totalWanted} câu, đợt ${batchIndex})`;

      let batchResult;
      try {
        batchResult = await extractQuestionsFromDocument(chapter, sourceText, Math.min(BATCH_SIZE, totalWanted - createdCount));
      } catch (e) {
        console.warn("Batch extract lỗi, bỏ qua đợt này:", e);
        continue;
      }

      const questions = batchResult.questions || [];
      if (!questions.length) break; // AI không sinh thêm được nữa

      for (const q of questions) {
        if (createdCount >= totalWanted) break;
        try {
          const created = await createQuestion({
            chapter,
            questionText: q.questionText,
            optionA: q.optionA,
            optionB: q.optionB,
            optionC: q.optionC,
            optionD: q.optionD,
            correctAnswer: q.correctAnswer,
            explanation: q.explanation || "",
            sourceType: "AiGenerated",
            difficulty: q.difficulty || null,
            topic: q.topic || null,
          });
          examPoolQuestionIds.push(created.id);
          createdCount++;
        } catch {
          /* bỏ qua câu lỗi, tiếp tục các câu còn lại */
        }
      }
    }

    statusEl.style.color = createdCount > 0 ? "#2e7d32" : "#c62828";
    statusEl.innerHTML =
      createdCount > 0
        ? `Đã tạo pool gồm ${createdCount} câu hỏi — cần xuất bản trước khi học viên thấy được.<br>` +
          goToQuestionsButtonHtml(chapter)
        : "Không trích xuất được câu hỏi nào — thử file khác.";

    if (createdCount > 0) {
      document.getElementById("examSetTargetCount").max = createdCount;
      document.getElementById("poolGenerateForm").style.display = "block";
      if (typeof loadQuestions === "function") loadQuestions();
    }
  } catch (err) {
    statusEl.style.color = "#c62828";
    statusEl.textContent = "Lỗi: " + err.message;
  } finally {
    btn.disabled = false;
  }
}

async function generateExamVersions() {
  if (!examPoolQuestionIds.length) return;
  const ten = document.getElementById("examSetTen").value.trim();
  const targetCount = parseInt(document.getElementById("examSetTargetCount").value, 10) || 50;
  const versionCount = parseInt(document.getElementById("examSetVersionCount").value, 10) || 3;
  const statusEl = document.getElementById("examSetGenerateStatus");
  const btn = document.getElementById("examSetGenerateBtn");

  if (!ten) {
    statusEl.style.color = "#c62828";
    statusEl.textContent = "Vui lòng nhập tên bộ đề.";
    return;
  }

  btn.disabled = true;
  statusEl.style.color = "var(--gray-500)";
  statusEl.textContent = "Đang sinh mã đề...";

  try {
    examVersionsData = await generateExamSetVersions(ten, examPoolQuestionIds, null, targetCount, versionCount);
    statusEl.style.color = "#2e7d32";
    statusEl.textContent = `Đã sinh ${examVersionsData.versions.length} mã đề, mỗi mã ${targetCount} câu.`;
    renderExamVersionsResult(examVersionsData);
  } catch (err) {
    statusEl.style.color = "#c62828";
    statusEl.textContent = "Lỗi: " + err.message;
  } finally {
    btn.disabled = false;
  }
}

/** Đăng ký thật (versionId -> {maDe, questions, isPublished}) — dùng chung cho MỌI nơi hiển thị
 * thẻ mã đề, dù là bộ vừa sinh (generateExamVersions) hay bộ tải lại từ "Bộ đề đã tạo" (Việc 2,
 * loadExamSetsList/toggleExamSetDetail) — để exportExamVersionToWord/toggleVersionPublish tìm
 * đúng dữ liệu mã đề bất kể nó đến từ luồng nào. */
let examVersionsById = new Map();

function registerExamVersions(examSet) {
  for (const v of examSet.versions) {
    examVersionsById.set(v.id, { maDe: v.maDe, kind: v.kind, questions: v.questions, oralQuestions: v.oralQuestions, isPublished: v.isPublished, lopIds: v.lopIds || [] });
  }
}

/** Kind="Oral" (Việc 5) không có khái niệm xuất bản (OralQuestion không có publish-gate, đã xác
 * nhận cố ý từ trước) — ẩn hẳn nút/badge Xuất bản thay vì hiện rồi gọi lỗi 400. */
function versionCardHtml(v) {
  const isOral = v.kind === "Oral";
  const count = isOral ? v.oralQuestions.length : v.questions.length;
  const countLabel = isOral ? `${count} câu tự luận` : `${count} câu trắc nghiệm`;
  const badgeStyle = v.isPublished ? "background:#e8f5e9;color:#2e7d32;" : "background:#fff3e0;color:#b45309;";
  const badgeText = v.isPublished ? "✓ Đã xuất bản" : "Chưa xuất bản";
  const toggleLabel = v.isPublished ? "Hủy xuất bản" : "Xuất bản";
  const toggleIcon = v.isPublished ? "fa-rotate-left" : "fa-upload";
  const badgeHtml = isOral ? "" : `<span id="examVersionBadge-${v.id}" style="font-size:0.68rem;font-weight:700;padding:2px 8px;border-radius:100px;${badgeStyle}">${badgeText}</span>`;
  const publishBtnHtml = isOral
    ? ""
    : `<button class="btn btn-outline btn-sm" id="examVersionPublishBtn-${v.id}" onclick="toggleVersionPublish('${v.id}', '${v.maDe}')">
          <i class="fas ${toggleIcon}"></i> ${toggleLabel}
        </button>`;
  // Việc 8 — badge + nút sửa phạm vi hiển thị của mã đề.
  const lopIds = v.lopIds || [];
  const scopeBadge = lopIds.length
    ? `<span style="font-size:0.68rem;font-weight:700;padding:2px 8px;border-radius:100px;background:#ede7f6;color:#4527a0;">🔒 ${lopIds.length} Lớp</span>`
    : `<span style="font-size:0.68rem;font-weight:700;padding:2px 8px;border-radius:100px;background:#eceff1;color:#455a64;">🌐 Toàn hệ thống</span>`;
  return `<div class="card" id="examVersionCard-${v.id}" style="margin-bottom:8px;padding:10px;display:flex;justify-content:space-between;align-items:center;">
      <div>
        <div style="font-weight:700;">Mã đề ${v.maDe} ${badgeHtml} ${scopeBadge}</div>
        <div style="font-size:0.75rem;color:var(--gray-500);">${countLabel}</div>
      </div>
      <div style="display:flex;gap:6px;">
        ${publishBtnHtml}
        <button class="btn btn-outline btn-sm" onclick='openEditLopVisibilityModal("examVersion", "${v.id}", ${JSON.stringify(lopIds)})'>
          <i class="fas fa-users"></i> Phạm vi
        </button>
        <button class="btn btn-outline btn-sm" onclick="exportExamVersionToWord('${v.id}', '${v.maDe}')">
          <i class="fas fa-file-word"></i> Xuất Word
        </button>
      </div>
    </div>`;
}

function renderExamVersionsResult(examSet) {
  registerExamVersions(examSet);
  const container = document.getElementById("examVersionsResult");
  container.innerHTML = examSet.versions.map(versionCardHtml).join("");
}

/** C3 (+ Việc 1 unpublish) — toggle xuất bản/hủy xuất bản cả mã đề, phản ánh đúng trạng thái thật
 * từ server (ExamVersion.IsPublished) thay vì chỉ suy đoán ở UI. */
async function toggleVersionPublish(versionId, maDe) {
  const entry = examVersionsById.get(versionId);
  if (!entry) return;
  const btn = document.getElementById(`examVersionPublishBtn-${versionId}`);
  btn.disabled = true;
  try {
    if (entry.isPublished) {
      const result = await unpublishExamVersion(versionId);
      entry.isPublished = false;
      showToast(`Đã hủy xuất bản mã đề ${maDe} (${result.unpublishedCount} câu)`, "success");
    } else {
      const result = await publishExamVersion(versionId);
      entry.isPublished = true;
      showToast(`Đã xuất bản mã đề ${maDe} cho học viên (${result.publishedCount} câu)`, "success");
    }
    examVersionsById.set(versionId, entry);
    const badge = document.getElementById(`examVersionBadge-${versionId}`);
    badge.textContent = entry.isPublished ? "✓ Đã xuất bản" : "Chưa xuất bản";
    badge.style.background = entry.isPublished ? "#e8f5e9" : "#fff3e0";
    badge.style.color = entry.isPublished ? "#2e7d32" : "#b45309";
    btn.innerHTML = entry.isPublished
      ? '<i class="fas fa-rotate-left"></i> Hủy xuất bản'
      : '<i class="fas fa-upload"></i> Xuất bản';
    if (typeof loadQuestions === "function") loadQuestions();
  } catch (err) {
    showToast("Lỗi: " + err.message, "error");
  } finally {
    btn.disabled = false;
  }
}

async function exportExamVersionToWord(versionId, maDe) {
  const entry = examVersionsById.get(versionId);
  if (!entry) return;
  try {
    if (entry.kind === "Oral") {
      await exportQuestionsToWord([], [], `de-vandap-${maDe}.docx`, entry.oralQuestions.map((q) => q.id));
    } else {
      await exportQuestionsToWord(entry.questions.map((q) => q.id), [], `de-${maDe}.docx`);
    }
  } catch (err) {
    showToast("Lỗi xuất Word: " + err.message, "error");
  }
}

// ════════════════════════════════════════════
// VIỆC 5 (2026-08-16) — "Bộ đề TN mới" / "Bộ đề VĐ mới" từ ngân hàng câu hỏi CÓ SẴN, không sinh AI
// mới. Nguồn TN CHỈ lấy câu isPublished=true (tránh publish ngược câu chưa duyệt qua
// PublishVersionAsync — đã duyệt thiết kế). Nguồn VĐ lấy toàn bộ ngân hàng (OralQuestion không có
// publish-gate). Dùng lại đúng generateExamSetVersions()/generateOralExamSetVersions() —
// ExamSetService.GenerateAsync không quan tâm nguồn gốc PoolQuestionIds, không cần endpoint riêng
// cho TN; VĐ cần endpoint mới (generate-oral) vì OralQuestion khác bảng/FK-space với Question.
// ════════════════════════════════════════════
let bankExamSetMode = "mcq"; // "mcq" | "oral"

function openBankExamSetModal(mode) {
  bankExamSetMode = mode;
  const isOral = mode === "oral";

  document.getElementById("bankExamSetTitle").textContent = isOral ? "Bộ đề VĐ mới từ ngân hàng" : "Bộ đề TN mới từ ngân hàng";
  document.getElementById("bankExamSetTen").value = "";
  document.getElementById("bankExamSetCount").min = isOral ? 1 : 25;
  document.getElementById("bankExamSetCount").max = isOral ? 4 : 50;
  document.getElementById("bankExamSetCount").value = isOral ? 2 : 25;
  document.getElementById("bankExamSetCountHint").textContent = isOral ? "1-4 câu" : "25-50 câu";
  document.getElementById("bankExamSetStatus").textContent = "";

  const eligibleList = bankExamSetEligibleList();
  const chapters = [...new Set(eligibleList.map((q) => q.chapter).filter(Boolean))];
  const sel = document.getElementById("bankExamSetChapter");
  sel.innerHTML = '<option value="">Tất cả chương</option>' + chapters.map((c) => `<option value="${c}">${c}</option>`).join("");

  document.getElementById("bankExamSetPoolInfo").textContent =
    `${eligibleList.length} câu ${isOral ? "tự luận" : "đã xuất bản"} khả dụng trong ngân hàng.`;

  document.getElementById("bankExamSetScopeContainer").innerHTML = scopePickerHtml("bankExamSetScope");
  initScopePicker("bankExamSetScope");

  openModal("bankExamSetModal");
}

/** Nguồn TN: chỉ câu đã xuất bản (isPublished). Nguồn Tự luận: toàn bộ ngân hàng — không có
 * cột publish để lọc. Dùng allQuestions/allOralQ đã cache sẵn từ loadQuestions()/loadOralQuestions()
 * (panel Câu hỏi TN/Tự luận luôn load trước khi nút này bấm được), không fetch lại. */
function bankExamSetEligibleList() {
  if (bankExamSetMode === "oral") return allOralQ;
  return allQuestions.filter((q) => q.isPublished);
}

async function submitBankExamSet() {
  const isOral = bankExamSetMode === "oral";
  const ten = document.getElementById("bankExamSetTen").value.trim();
  const chapter = document.getElementById("bankExamSetChapter").value;
  const targetCount = parseInt(document.getElementById("bankExamSetCount").value, 10) || 0;
  const versionCount = parseInt(document.getElementById("bankExamSetVersionCount").value, 10) || 3;
  const statusEl = document.getElementById("bankExamSetStatus");
  const btn = document.getElementById("bankExamSetSubmitBtn");

  if (!ten) {
    statusEl.style.color = "#c62828";
    statusEl.textContent = "Vui lòng nhập tên bộ đề.";
    return;
  }

  const eligibleList = bankExamSetEligibleList().filter((q) => !chapter || q.chapter === chapter);
  const poolIds = eligibleList.map((q) => q.id);

  if (poolIds.length < targetCount) {
    // Báo lỗi rõ ngay phía client, không tự động lách xuống số ít hơn yêu cầu (đúng thiết kế đã
    // duyệt) — dù backend cũng chặn y hệt, chặn sớm ở đây tránh 1 round-trip mạng vô ích.
    statusEl.style.color = "#c62828";
    statusEl.textContent =
      `Ngân hàng chỉ có ${poolIds.length} câu ${isOral ? "tự luận" : "đã xuất bản"}${chapter ? " ở chương này" : ""} — không đủ ${targetCount} câu yêu cầu.`;
    return;
  }

  btn.disabled = true;
  statusEl.style.color = "var(--gray-500)";
  statusEl.textContent = "Đang tạo bộ đề...";

  const lopIds = getScopePickerLopIds("bankExamSetScope");
  try {
    if (isOral) {
      await generateOralExamSetVersions(ten, poolIds, targetCount, versionCount, lopIds);
    } else {
      await generateExamSetVersions(ten, poolIds, null, targetCount, versionCount, lopIds);
    }
    statusEl.style.color = "#2e7d32";
    statusEl.textContent = "✓ Đã tạo bộ đề thành công!";
    showToast("Đã tạo bộ đề mới từ ngân hàng!", "success");
    setTimeout(() => closeModal("bankExamSetModal"), 800);
    if (typeof loadExamSetsList === "function") loadExamSetsList();
  } catch (err) {
    statusEl.style.color = "#c62828";
    statusEl.textContent = "Lỗi: " + err.message;
  } finally {
    btn.disabled = false;
  }
}

// ════════════════════════════════════════════
// VIỆC 2 — "Bộ đề đã tạo": danh sách bộ đề tồn tại qua reload trang (GET /exam-sets có sẵn từ C2,
// không thêm entity/endpoint mới). Mỗi bộ đề xem chi tiết (lazy-load qua GET /exam-sets/{id}) để
// hiện các mã đề — dùng lại đúng versionCardHtml/toggleVersionPublish/exportExamVersionToWord ở
// trên, không viết trùng logic Xuất Word/Xuất bản.
// ════════════════════════════════════════════
async function loadExamSetsList() {
  const container = document.getElementById("examSetsListContainer");
  if (!container) return;
  container.innerHTML = '<div style="text-align:center;padding:16px;color:var(--gray-400);"><i class="fas fa-spinner fa-spin"></i></div>';

  let sets;
  try {
    sets = await listExamSets();
  } catch (err) {
    container.innerHTML = `<div style="text-align:center;padding:16px;color:#c62828;">Lỗi tải danh sách bộ đề: ${err.message}</div>`;
    return;
  }

  if (!sets.length) {
    container.innerHTML = '<div style="text-align:center;padding:16px;color:var(--gray-400);">Chưa có bộ đề nào. Trích xuất pool và sinh mã đề ở trên để bắt đầu.</div>';
    return;
  }

  container.innerHTML = sets
    .map(
      (s) => `<div class="card" style="margin-bottom:8px;padding:10px;">
      <div style="display:flex;justify-content:space-between;align-items:center;gap:8px;">
        <div>
          <div style="font-weight:700;">${s.ten}</div>
          <div style="font-size:0.75rem;color:var(--gray-500);">${s.totalPoolSize} câu trong pool · ${s.versionCount} mã đề · ${new Date(s.createdAtUtc).toLocaleString("vi-VN")}</div>
        </div>
        <button class="btn btn-outline btn-sm" onclick="toggleExamSetDetail('${s.id}')" id="examSetToggleBtn-${s.id}">
          <i class="fas fa-chevron-down"></i> Xem mã đề
        </button>
      </div>
      <div id="examSetDetail-${s.id}" style="display:none;margin-top:10px;"></div>
    </div>`,
    )
    .join("");
}

async function toggleExamSetDetail(examSetId) {
  const detailEl = document.getElementById(`examSetDetail-${examSetId}`);
  const btn = document.getElementById(`examSetToggleBtn-${examSetId}`);
  const isOpen = detailEl.style.display !== "none";

  if (isOpen) {
    detailEl.style.display = "none";
    btn.innerHTML = '<i class="fas fa-chevron-down"></i> Xem mã đề';
    return;
  }

  detailEl.style.display = "block";
  btn.innerHTML = '<i class="fas fa-chevron-up"></i> Ẩn mã đề';
  detailEl.innerHTML = '<div style="text-align:center;padding:8px;color:var(--gray-400);"><i class="fas fa-spinner fa-spin"></i></div>';

  try {
    const examSet = await getExamSet(examSetId);
    registerExamVersions(examSet);
    detailEl.innerHTML = examSet.versions.map(versionCardHtml).join("");
  } catch (err) {
    detailEl.innerHTML = `<div style="color:#c62828;font-size:0.8rem;">Lỗi tải mã đề: ${err.message}</div>`;
  }
}

function filterQuestions() {
  const q = document.getElementById("qSearch").value.toLowerCase();
  const ch = document.getElementById("qChapter").value;
  renderQuestions(
    allQuestions.filter(
      (x) =>
        (!q || x.questionText.toLowerCase().includes(q)) && (!ch || x.chapter === ch),
    ),
  );
}

async function deleteQuestionRow(id) {
  if (!confirm("Xóa câu hỏi này?")) return;
  try {
    await deleteQuestion(id);
    showToast("Đã xóa câu hỏi", "success");
  } catch (err) {
    showToast("Lỗi: " + err.message, "error");
  }
  loadQuestions();
}

/** C3 — bấm để xuất bản/gỡ xuất bản 1 câu hỏi (toggle). */
async function toggleQuestionPublish(id) {
  try {
    const updated = await publishQuestion(id);
    showToast(updated.isPublished ? "Đã xuất bản cho học viên" : "Đã gỡ xuất bản", "success");
  } catch (err) {
    showToast("Lỗi: " + err.message, "error");
  }
  loadQuestions();
}

// ============================================================
// ORAL QUESTIONS
// ============================================================
async function loadOralQuestions() {
  try {
    allOralQ = await listOralQuestionsBank();
  } catch (err) {
    allOralQ = [];
    showToast("Lỗi tải câu hỏi tự luận: " + err.message, "error");
  }
  // Việc 4 — dropdown lọc chương, giống hệt qChapter bên panel Câu hỏi TN.
  const chapters = [...new Set(allOralQ.map((q) => q.chapter).filter(Boolean))];
  const sel = document.getElementById("oralChapterFilter");
  if (sel) {
    sel.innerHTML =
      '<option value="">Tất cả chương</option>' + chapters.map((c) => `<option value="${c}">${c}</option>`).join("");
  }
  renderOralQuestions(allOralQ);
}

let oralExpandedChapters = new Set();

function oralCardsHtml(list) {
  return list
    .map(
      (q, i) => {
        // Việc 4.4 Phần A (2026-08-20) — badge phạm vi hiển thị, cùng quy ước questionCardsHtml.
        const lopIds = q.lopIds || [];
        const scopeBadge = lopIds.length
          ? `<span class="q-opt" style="background:#ede7f6;color:#4527a0;">🔒 ${lopIds.length} Lớp</span>`
          : `<span class="q-opt" style="background:#eceff1;color:#455a64;">🌐 Toàn hệ thống</span>`;
        return `
    <div class="q-card">
      <div style="display:flex;justify-content:space-between;align-items:flex-start;gap:8px;">
        <div style="flex:1;">
          <div class="q-chapter">${escapeHtml(q.chapter || "Chung")} · Độ khó: ${"⭐".repeat(q.difficulty || 1)} &nbsp; ${scopeBadge}</div>
          <div class="q-text">${i + 1}. ${escapeHtml(q.questionText)}</div>
          <div style="font-size:0.75rem;color:var(--gray-600);margin-top:6px;background:var(--gray-50);padding:8px;border-radius:6px;"><strong>Đáp án chuẩn:</strong> ${escapeHtml(q.expectedAnswer || "—")}</div>
        </div>
        <div style="display:flex;flex-direction:column;gap:4px;flex-shrink:0;">
          <button onclick='openEditLopVisibilityModal("oralQuestion", "${q.id}", ${JSON.stringify(lopIds)})' style="background:none;border:none;cursor:pointer;color:var(--gray-400);padding:4px;" title="Sửa phạm vi hiển thị" aria-label="Sửa phạm vi hiển thị câu hỏi tự luận ${i + 1}"><i class="fas fa-users"></i></button>
          <button onclick="deleteOralQ('${q.id}')" style="background:none;border:none;cursor:pointer;color:var(--gray-400);padding:4px;" aria-label="Xóa câu hỏi tự luận"><i class="fas fa-trash"></i></button>
        </div>
      </div>
    </div>`;
      },
    )
    .join("");
}

function toggleOralGroup(chapter) {
  if (oralExpandedChapters.has(chapter)) oralExpandedChapters.delete(chapter);
  else oralExpandedChapters.add(chapter);
  filterOralQuestions();
}

function renderOralQuestions(list) {
  const el = document.getElementById("oralList");
  if (!list.length) {
    el.innerHTML =
      '<div class="empty"><i class="fas fa-comments"></i><br>Chưa có câu hỏi tự luận</div>';
    return;
  }

  const search = document.getElementById("oralSearch")?.value.trim();
  const chapterFilter = document.getElementById("oralChapterFilter")?.value;
  if (search || chapterFilter) {
    el.innerHTML = oralCardsHtml(list);
    return;
  }

  el.innerHTML = renderChapterGroups(list, {
    typeLabel: "Tự luận",
    expandedSet: oralExpandedChapters,
    toggleFnName: "toggleOralGroup",
    cardsHtmlFn: oralCardsHtml,
  });
}

function filterOralQuestions() {
  const q = document.getElementById("oralSearch").value.toLowerCase();
  const ch = document.getElementById("oralChapterFilter").value;
  renderOralQuestions(
    allOralQ.filter((x) => (!q || x.questionText.toLowerCase().includes(q)) && (!ch || x.chapter === ch)),
  );
}

function openAddOralModal() {
  // Việc 4.4 Phần A (2026-08-20) — picker riêng cho modal này (idempotent: chỉ render lần đầu,
  // reset về "Toàn hệ thống" mỗi lần mở lại, cùng convention resetScopePicker dùng ở nơi khác).
  const container = document.getElementById("oralAddScopeContainer");
  if (container && !container.innerHTML) {
    container.innerHTML = scopePickerHtml("oralAddScope");
  }
  initScopePicker("oralAddScope").then(() => resetScopePicker("oralAddScope"));
  openModal("oralModal");
}

async function saveOralQuestion() {
  const chapter = document.getElementById("oral-chapter").value.trim();
  const questionText = document.getElementById("oral-q").value.trim();
  const expectedAnswer = document.getElementById("oral-a").value.trim();
  const difficulty = parseInt(document.getElementById("oral-diff").value);
  const lopIds = getScopePickerLopIds("oralAddScope");
  if (!chapter || !questionText || !expectedAnswer)
    return alert("Vui lòng điền đủ thông tin!");
  try {
    await createOralQuestion({ chapter, questionText, expectedAnswer, difficulty, lopIds });
  } catch (err) {
    showToast("Lỗi: " + err.message, "error");
    return;
  }
  showToast("Đã thêm câu hỏi tự luận!", "success");
  closeModal("oralModal");
  document.getElementById("oral-chapter").value = "";
  document.getElementById("oral-q").value = "";
  document.getElementById("oral-a").value = "";
  loadOralQuestions();
}

async function deleteOralQ(id) {
  if (!confirm("Xóa câu hỏi tự luận này?")) return;
  try {
    await deleteOralQuestion(id);
    showToast("Đã xóa", "success");
  } catch (err) {
    showToast("Lỗi: " + err.message, "error");
  }
  loadOralQuestions();
}

async function importOralExcel(e) {
  const file = e.target.files[0];
  if (!file) return;
  const buffer = await file.arrayBuffer();
  const wb = XLSX.read(buffer);
  const wsName = wb.SheetNames.find((n) => n.includes("TỰ LUẬN") || n.includes("VẤN ĐÁP")) || wb.SheetNames[0];
  const ws = wb.Sheets[wsName];
  const rows = XLSX.utils.sheet_to_json(ws, { defval: "" });

  const oqs = rows
    .filter((r) => r.cau_hoi || r["Câu hỏi tự luận"] || r["Câu hỏi vấn đáp"])
    .map((r) => ({
      chapter: r.chuong || r["Chương/Phần"] || "Chung",
      questionText: r.cau_hoi || r["Câu hỏi tự luận"] || r["Câu hỏi vấn đáp"],
      expectedAnswer: r.dap_an_chuan || r["Đáp án chuẩn"] || "",
      difficulty: parseInt(r.do_kho || r["Độ khó (1/2/3)"] || 2),
    }))
    .filter((q) => q.questionText);

  const status = document.getElementById("oralImportStatus");
  const inserted = await batchInsertOral(oqs);
  status.textContent = `✅ Đã import ${inserted}/${oqs.length} câu hỏi tự luận!`;
  status.style.color = "#2e7d32";
  showToast(`Import ${inserted} câu tự luận!`, "success");
  loadOralQuestions();
}

/** Lý do khả dĩ khi parseQText() không tìm được câu nào — nêu rõ nghi vấn hay gặp nhất (audit
 * 2026-08-16 tái hiện được: danh sách tự động/bullet-numbering của Word làm mất hẳn tiền tố
 * "A./B./C./D." khi hệ thống đọc raw text, dù file NHÌN đúng định dạng trên Word) thay vì chỉ báo
 * "0 câu hỏi" im lặng không rõ nguyên nhân. */
const NO_QUESTIONS_FOUND_HINT =
  'Không tìm thấy câu hỏi đúng định dạng. Kiểm tra: (1) mỗi câu bắt đầu bằng "Câu 1:", "Câu 2:"...; ' +
  "(2) các lựa chọn A/B/C/D phải GÕ TAY thành chữ (vd \"A. Nội dung...\") — KHÔNG dùng danh sách tự " +
  "động (bullet/numbering) của Word, vì định dạng tự động sẽ mất khi hệ thống đọc nội dung file; " +
  '(3) có dòng "ĐÁP ÁN: X" ngay sau các lựa chọn.';

/** Điều hướng sang panel Câu hỏi TN và lọc sẵn theo đúng Chương vừa import — thay vì chỉ báo
 * "Hoàn thành" rồi dừng, giáo viên bấm 1 nút là thấy ngay đúng câu vừa tạo (audit 2026-08-16). */
async function goToQuestionsFilteredByChapter(chapter) {
  showPanel("questions");
  await loadQuestions();
  const sel = document.getElementById("qChapter");
  if (sel && Array.from(sel.options).some((o) => o.value === chapter)) {
    sel.value = chapter;
  }
  const search = document.getElementById("qSearch");
  if (search) search.value = "";
  filterQuestions();
}

function goToQuestionsButtonHtml(chapter) {
  const safeChapter = chapter.replace(/'/g, "\\'");
  return `<button class="btn btn-outline btn-sm" style="margin-top: 8px" onclick="goToQuestionsFilteredByChapter('${safeChapter}')">
    <i class="fas fa-arrow-right"></i> Xem ngay — vào Câu hỏi TN, lọc theo "${escapeHtml(chapter)}"
  </button>`;
}

async function importPaste() {
  const text = document.getElementById("pasteArea").value.trim();
  const chapter = document.getElementById("pasteChapter").value.trim() || "Chương mới";
  const status = document.getElementById("pasteStatus");
  if (!text) return alert("Vui lòng dán nội dung câu hỏi!");

  const parsed = parseQText(text, chapter);
  if (!parsed.length) {
    status.style.color = "#c62828";
    status.textContent = "❌ " + NO_QUESTIONS_FOUND_HINT;
    return;
  }
  // Việc III (2026-08-20, rà soát Lần II mục 1.7) — trước đây "Dán văn bản" âm thầm dùng chung
  // lựa chọn Lớp của khối "Import từ file" phía trên, không hiện gì cho giáo viên biết đang giao
  // Lớp nào. Giờ có picker RIÊNG (pasteScopeContainer, xem HTML) — đọc đúng "pasteScope".
  const inserted = await batchInsertQuestions(parsed, undefined, "pasteScope");
  status.style.color = "#2e7d32";
  status.innerHTML =
    `✅ Import thành công ${inserted}/${parsed.length} câu hỏi vào "${escapeHtml(chapter)}"! ` +
    `Câu import cần xuất bản trước khi học viên thấy được.<br>` +
    (inserted > 0 ? goToQuestionsButtonHtml(chapter) : "");
  document.getElementById("pasteArea").value = "";
  showToast(`Import ${inserted} câu!`, "success");
  loadQuestions();
}

/** Parse văn bản dạng "Câu X: ... A. ... B. ... ĐÁP ÁN: A" → CreateQuestionRequest[] */
function parseQText(text, chapter) {
  const results = [];
  text = text.replace(/\r\n/g, "\n").replace(/\r/g, "\n");
  const blocks = text.split(/(?=Câu\s*\d+\s*[:\.]\s*)/i).filter((b) => b.trim());
  for (const block of blocks) {
    const lines = block
      .split("\n")
      .map((l) => l.trim())
      .filter(Boolean);
    if (lines.length < 3) continue;
    const qText = lines[0].replace(/^Câu\s*\d+\s*[:\.]\s*/i, "").trim();
    if (!qText) continue;
    const opts = [];
    for (const line of lines.slice(1)) {
      const m = line.match(/^[ABCDabcd]\s*[\.:\)]\s*(.+)/);
      if (m) opts.push(m[1].trim());
      if (opts.length === 4) break;
    }
    if (opts.length < 2) continue;
    let ans = 0;
    const ansLine = lines.find((l) => /đáp án|answer/i.test(l));
    if (ansLine) {
      const m = ansLine.match(/[ABCD]/i);
      if (m) ans = ["A", "B", "C", "D"].indexOf(m[0].toUpperCase());
    }
    const expLine = lines.find((l) => /giải thích|note/i.test(l));
    const exp = expLine ? expLine.replace(/^(giải thích|note)\s*[:\.]\s*/i, "") : "";
    results.push({
      chapter,
      questionText: qText,
      optionA: opts[0] || "",
      optionB: opts[1] || "",
      optionC: opts[2] || "",
      optionD: opts[3] || "",
      correctAnswer: Math.max(0, ans),
      explanation: exp,
      // Câu có sẵn trong file giáo viên chọn (Word/PDF/Text) — không phải gõ tay từng field vào
      // form, không phải AI tự sinh — đúng nghĩa "Imported", cần giáo viên xuất bản thủ công
      // trước khi học viên thấy (audit 2026-08-15, cùng lớp lỗi với auto-extract-khi-upload đã vá).
      sourceType: "Imported",
    });
  }
  return results;
}

// ============================================================
// HELPERS
// ============================================================
function showToast(msg, type = "") {
  const t = document.getElementById("toast");
  t.textContent = msg;
  t.className = `toast ${type} show`;
  setTimeout(() => t.classList.remove("show"), 3000);
}
function openModal(id) {
  const el = document.getElementById(id);
  el.classList.add("show");
  el.setAttribute("aria-hidden", "false");
}
function closeModal(id) {
  const el = document.getElementById(id);
  el.classList.remove("show");
  el.setAttribute("aria-hidden", "true");
}

// ════════════════════════════════════════════
// BATCH IMPORT — PDF + Word + Excel + Text
// ════════════════════════════════════════════

let batchFiles = []; // [{file, id, status, count}]
let batchRunning = false;

// File Excel luôn là câu hỏi có sẵn theo template — không hỏi mục đích, vào thẳng batchFiles như
// trước. Word/PDF/Text có thể là câu hỏi có sẵn HOẶC tài liệu cần AI đọc — giữ lại chờ chọn mục
// đích trước khi vào batchFiles (audit 2026-08-16 mục 1). batchFiles vì vậy CHỈ BAO GIỜ chứa file
// đã xác định là literal-parse — startBatchImport()/renderBatchList() không cần biết gì về AI,
// giữ nguyên y hệt hành vi đã fix ở commit a4ee557.
let importPurposeStaging = []; // [{file, id, status, count, ext}] — pdf/docx/txt chờ chọn mục đích

// Việc III (2026-08-20) — bỏ hẳn Excel khỏi luồng import câu hỏi TN (chỉ giữ Word + PDF + Text),
// theo đúng yêu cầu rà soát Lần II mục 1.7. Excel Tự luận (importOralExcel, khối riêng "Import câu
// hỏi tự luận từ Excel") KHÔNG bị đụng — khác định dạng cột, ngoài phạm vi yêu cầu này.
function addBatchFiles(files) {
  const needsPurpose = [];
  for (const f of files) {
    const ext = f.name.split(".").pop().toLowerCase();
    if (!["pdf", "docx", "txt"].includes(ext)) continue;
    if (batchFiles.find((b) => b.file.name === f.name && b.file.size === f.size)) continue;
    if (importPurposeStaging.find((s) => s.file.name === f.name && s.file.size === f.size)) continue;
    const entry = { file: f, id: Date.now() + Math.random(), status: "pending", count: 0, ext };
    needsPurpose.push(entry);
  }
  renderBatchList();
  if (needsPurpose.length) {
    importPurposeStaging.push(...needsPurpose);
    openImportPurposeModal();
  }
  // Reset ngay sau khi đã "tiêu thụ" FileList vào state riêng (batchFiles/importPurposeStaging) —
  // nếu không, chọn lại ĐÚNG file vừa xong (VD chọn nhầm mục đích, muốn chọn lại) sẽ không đổi
  // .value của input, trình duyệt không bắn sự kiện "change", addBatchFiles() không chạy lại.
  document.getElementById("batchFileInput").value = "";
}

function openImportPurposeModal() {
  document.getElementById("importPurposeFileList").textContent = importPurposeStaging.map((s) => s.file.name).join(", ");
  openModal("importPurposeModal");
}

function cancelImportPurposeModal() {
  // Đóng mà không chọn mục đích → coi như hủy thêm các file này, không vào batchFiles lẫn hàng
  // đợi AI (tránh trạng thái lửng lơ "chưa rõ mục đích" mà startBatchImport() không biết xử lý).
  importPurposeStaging = [];
  closeModal("importPurposeModal");
}

function chooseImportPurposeLiteral() {
  batchFiles.push(...importPurposeStaging);
  importPurposeStaging = [];
  closeModal("importPurposeModal");
  renderBatchList();
}

/** Trích text từ file vừa chọn (không phải Material đã upload) để đưa qua đúng luồng C1 review-
 * trước-khi-lưu — cùng cơ chế mammoth.js/pdf.js đã dùng ở importWordBatch()/importPDFBatch(). */
async function extractFileTextForAi(file, ext) {
  if (ext === "docx") {
    const buffer = await file.arrayBuffer();
    const result = await mammoth.extractRawText({ arrayBuffer: buffer });
    return result.value;
  }
  if (ext === "pdf") {
    const buffer = await file.arrayBuffer();
    const pdf = await pdfjsLib.getDocument({ data: buffer }).promise;
    let fullText = "";
    for (let p = 1; p <= Math.min(pdf.numPages, 60); p++) {
      const page = await pdf.getPage(p);
      const tc = await page.getTextContent();
      fullText += tc.items.map((i) => i.str).join(" ") + "\n";
    }
    return fullText;
  }
  if (ext === "txt") return await file.text();
  return "";
}

// Nhiều file được chọn "Dùng AI" cùng lúc → xử lý tuần tự, 1 modal duyệt xong (Lưu) mới mở tiếp
// modal kế — advanceAiSourceQueue() được gọi lại sau khi saveSelectedExamQuestions()/
// saveAndExportSelectedExamQuestions() thành công. Đóng modal bằng nút X thay vì Lưu = dừng lại,
// các file còn lại trong hàng đợi bị bỏ qua (xem nút đóng examGenModal trong HTML).
let aiSourceQueue = [];

function advanceAiSourceQueue() {
  if (!aiSourceQueue.length) return;
  const next = aiSourceQueue.shift();
  if (aiSourceQueue.length) {
    showToast(`Còn ${aiSourceQueue.length} file nữa sẽ mở tiếp sau khi bạn xong file này`, "success");
  }
  openGenerateExamModalFromSource(next);
}

async function chooseImportPurposeAi() {
  const picked = importPurposeStaging;
  importPurposeStaging = [];
  closeModal("importPurposeModal");

  let skipped = 0;
  for (const b of picked) {
    let text = "";
    try {
      text = await extractFileTextForAi(b.file, b.ext);
    } catch (e) {
      showToast(`Lỗi đọc file ${b.file.name}: ${e.message}`, "error");
      skipped++;
      continue;
    }
    if (text.trim().length < 100) {
      skipped++;
      continue;
    }
    const label = b.file.name.replace(/\.(pdf|docx|txt)$/i, "").replace(/_/g, " ").trim();
    aiSourceQueue.push({ sourceText: text, chapter: label, label });
  }

  if (!aiSourceQueue.length) {
    showToast("Không trích được nội dung văn bản đủ dài từ file đã chọn", "error");
    return;
  }
  if (skipped) {
    showToast(`Bỏ qua ${skipped} file không trích được nội dung`, "error");
  }
  advanceAiSourceQueue();
}

function renderBatchList() {
  const el = document.getElementById("batchFileList");
  const startBtn = document.getElementById("batchStartBtn");
  const clearBtn = document.getElementById("batchClearBtn");
  const hint = document.getElementById("batchHint");

  if (!batchFiles.length) {
    el.innerHTML = "";
    startBtn.style.display = "none";
    clearBtn.style.display = "none";
    hint.textContent = "Chọn file để bắt đầu";
    return;
  }

  el.innerHTML = batchFiles
    .map((b, i) => {
      const icons = { pdf: "📄", docx: "📝", txt: "📃" };
      const colors = { pdf: "#145a3a", docx: "#1565c0", txt: "#555" };
      const statusMap = {
        pending: { cls: "bfs-pending", text: "⏳ Chờ" },
        loading: { cls: "bfs-loading", text: "🔄 Đang xử lý..." },
        done: {
          cls: b.count ? "bfs-done" : "bfs-warning",
          text: b.count ? `✅ +${b.count} câu` : "⚠️ 0 câu — kiểm tra định dạng",
        },
        error: { cls: "bfs-error", text: "❌ Lỗi" },
      };
      const s = statusMap[b.status] || statusMap.pending;
      const size = (b.file.size / 1024).toFixed(0) + " KB";
      return `<div class="batch-file-item" id="bfi_${i}">
      <div class="batch-file-icon" style="color:${colors[b.ext]}">${icons[b.ext]}</div>
      <div class="batch-file-info">
        <div class="batch-file-name">${b.file.name}</div>
        <div class="batch-file-meta">${b.ext.toUpperCase()} · ${size}</div>
      </div>
      <div class="batch-file-status ${s.cls}">${s.text}</div>
    </div>`;
    })
    .join("");

  startBtn.style.display = batchRunning ? "none" : "flex";
  clearBtn.style.display = "flex";
  hint.textContent = `${batchFiles.length} file đã chọn`;
}

function clearBatchFiles() {
  if (batchRunning) return;
  batchFiles = [];
  renderBatchList();
  document.getElementById("batchOverall").style.display = "none";
  document.getElementById("batchFileInput").value = "";
}

async function startBatchImport() {
  if (!batchFiles.length || batchRunning) return;
  batchRunning = true;
  document.getElementById("batchStartBtn").style.display = "none";
  document.getElementById("batchOverall").style.display = "block";

  let totalImported = 0;
  let totalErrors = 0;
  let hasZeroFromTextParse = false; // file dùng parseQText() (pdf/docx/txt) nhưng ra 0 câu

  for (let i = 0; i < batchFiles.length; i++) {
    const b = batchFiles[i];
    b.status = "loading";
    renderBatchList();
    updateBatchProgress(i, batchFiles.length, `Đang xử lý: ${b.file.name}`);

    try {
      let count = 0;
      if (b.ext === "pdf") {
        count = await importPDFBatch(b.file);
      } else if (b.ext === "docx") {
        count = await importWordBatch(b.file);
      } else if (b.ext === "txt") {
        count = await importTextBatch(b.file);
      }
      b.status = "done";
      b.count = count;
      totalImported += count;
      if (count === 0 && (b.ext === "pdf" || b.ext === "docx" || b.ext === "txt")) {
        hasZeroFromTextParse = true;
      }
    } catch (err) {
      b.status = "error";
      b.errorMsg = err.message;
      totalErrors++;
      console.error("Batch import error:", b.file.name, err);
    }
    renderBatchList();
  }

  updateBatchProgress(batchFiles.length, batchFiles.length, "Hoàn thành!");

  const single = batchFiles.length === 1 ? batchFiles[0] : null;
  const singleChapter =
    single && totalImported > 0 && (single.ext === "pdf" || single.ext === "docx" || single.ext === "txt")
      ? single.file.name.replace(/\.(pdf|docx|txt)$/i, "").replace(/_/g, " ").trim()
      : null;

  let summaryHtml =
    `✅ Đã import ${totalImported} câu hỏi từ ${batchFiles.length - totalErrors} file` +
    (totalErrors ? ` · ❌ ${totalErrors} file lỗi` : "") +
    (totalImported > 0 ? " · câu import cần xuất bản trước khi học viên thấy được" : "");
  if (hasZeroFromTextParse) {
    summaryHtml += `<br><span style="color:#b45309">⚠️ ${NO_QUESTIONS_FOUND_HINT}</span>`;
  }
  if (singleChapter) {
    summaryHtml += "<br>" + goToQuestionsButtonHtml(singleChapter);
  }
  document.getElementById("batchSummary").innerHTML = summaryHtml;

  batchRunning = false;
  document.getElementById("batchStartBtn").style.display = "flex";
  loadQuestions();
  showToast(`Import xong! +${totalImported} câu hỏi`, "success");
}

function updateBatchProgress(done, total, text) {
  const pct = total ? Math.round((done / total) * 100) : 0;
  document.getElementById("batchOverallFill").style.width = pct + "%";
  document.getElementById("batchOverallText").textContent = text + ` (${done}/${total})`;
}

// ── PDF IMPORT ──
async function importPDFBatch(file) {
  const buffer = await file.arrayBuffer();
  const pdf = await pdfjsLib.getDocument({ data: buffer }).promise;

  let fullText = "";
  for (let p = 1; p <= Math.min(pdf.numPages, 60); p++) {
    const page = await pdf.getPage(p);
    const tc = await page.getTextContent();
    fullText += tc.items.map((i) => i.str).join(" ") + "\n";
  }

  const chapterName = file.name.replace(/\.pdf$/i, "").replace(/_/g, " ").trim();

  // Thử tách câu hỏi trực tiếp từ text trước, nếu không đủ thì nhờ AI (ai-service) trích xuất
  let parsed = parseQText(fullText, chapterName);

  if (parsed.length < 3) {
    parsed = await aiExtractQuestions(fullText, chapterName);
  }

  if (!parsed.length) return 0;
  return await batchInsertQuestions(parsed);
}

/** Trích xuất câu hỏi từ nội dung bài giảng qua ai-service (Teacher/Admin only) —
 * không còn gọi Groq trực tiếp từ browser.
 *
 * QUAN TRỌNG: PHẢI gắn sourceType: "AiGenerated" ở đây — nếu thiếu, backend áp default
 * SourceType="Manual" (tham số mặc định trên CreateQuestionRequest), khiến câu hỏi AI sinh chưa
 * ai xem qua bị QuestionService.CreateAsync coi như đã "xuất bản" luôn (IsPublished =
 * SourceType=="Manual") — lọt thẳng qua cơ chế kiểm duyệt C3, học viên thấy ngay không cần giáo
 * viên xuất bản. Phát hiện qua audit thực tế 2026-08-15 (đã xác nhận bằng test qua API thật, xem
 * lịch sử commit) — materialId (nếu có, khi gọi từ handleMaterialUpload) được gắn kèm để nhất
 * quán với luồng Sinh đề bằng AI (C1). */
async function aiExtractQuestions(text, chapterName, materialId) {
  try {
    const result = await extractQuestionsFromDocument(chapterName, text.slice(0, 6000), 12);
    return (result.questions || []).map((q) => ({
      chapter: chapterName,
      questionText: q.questionText,
      optionA: q.optionA,
      optionB: q.optionB,
      optionC: q.optionC || "",
      optionD: q.optionD || "",
      correctAnswer: q.correctAnswer || 0,
      explanation: q.explanation || "",
      sourceType: "AiGenerated",
      sourceMaterialId: materialId || null,
    }));
  } catch (e) {
    console.error("AI extract failed:", e);
    return [];
  }
}

// ── WORD BATCH ──
async function importWordBatch(file) {
  const buffer = await file.arrayBuffer();
  const result = await mammoth.extractRawText({ arrayBuffer: buffer });
  const text = result.value;
  const chapterName = file.name.replace(/\.docx$/i, "").replace(/_/g, " ").trim();
  const parsed = parseQText(text, chapterName);
  if (!parsed.length) return 0;
  return await batchInsertQuestions(parsed);
}

// ── TEXT BATCH ──
async function importTextBatch(file) {
  const text = await file.text();
  const chapterName = file.name.replace(/\.txt$/i, "").replace(/_/g, " ").trim();
  const parsed = parseQText(text, chapterName);
  if (!parsed.length) return 0;
  return await batchInsertQuestions(parsed);
}

// ── BATCH INSERT helpers — quiz-service chưa có endpoint bulk-insert,
// nên gọi tuần tự từng câu qua createQuestion/createOralQuestion. ──
// Việc III (2026-08-20) — scopeIdPrefix tham số hóa (mặc định "batchImportScope") để mỗi khối
// import (file hàng loạt vs dán văn bản) đọc đúng picker Lớp RIÊNG của chính nó, không còn ngầm
// dùng chung lựa chọn của khối khác — xem remarks importPaste().
async function batchInsertQuestions(rows, onProgress, scopeIdPrefix = "batchImportScope") {
  // Việc 8: 1 lựa chọn Phạm vi hiển thị áp dụng cho CẢ đợt import (đọc 1 lần, không phải theo
  // từng dòng — khớp UI 1 điểm chọn duy nhất ở panel Import).
  const lopIds = getScopePickerLopIds(scopeIdPrefix);
  let ok = 0;
  for (let i = 0; i < rows.length; i++) {
    try {
      await createQuestion({ ...rows[i], lopIds });
      ok++;
    } catch (err) {
      console.warn("Bỏ qua câu hỏi lỗi:", rows[i].questionText, err.message);
    }
    if (onProgress) onProgress(i + 1, rows.length);
  }
  return ok;
}

async function batchInsertOral(rows) {
  // Việc 4.4 Phần A (2026-08-20) — cùng cách batchInsertQuestions: 1 lựa chọn phạm vi áp dụng cho
  // CẢ đợt import (đọc 1 lần), không phải theo từng dòng.
  const lopIds = getScopePickerLopIds("oralImportScope");
  let ok = 0;
  for (const row of rows) {
    try {
      await createOralQuestion({ ...row, lopIds });
      ok++;
    } catch (err) {
      console.warn("Bỏ qua câu tự luận lỗi:", row.questionText, err.message);
    }
  }
  return ok;
}

// ════════════════════════════════════════════
// MATERIALS — Upload & Manage lecture PDFs
// ════════════════════════════════════════════

// Set bởi handleMaterialUpload() ngay sau khi trích text thành công — banner "Tạo câu hỏi kiểm
// tra" bên dưới nút upload dùng lại, không phải tải/trích lại file (audit 2026-08-16 mục 2).
let lastUploadedMaterialForAi = null;

function openGenerateExamModalForLastUpload() {
  if (!lastUploadedMaterialForAi) return;
  openGenerateExamModalFromSource(lastUploadedMaterialForAi);
  document.getElementById("matUploadAiPrompt").style.display = "none";
}

async function handleMaterialUpload(e) {
  const file = e.target.files[0];
  if (!file) return;

  const title = document.getElementById("matTitle").value.trim();
  const chapter = document.getElementById("matChapter").value.trim();
  const desc = document.getElementById("matDesc").value.trim();
  const status = document.getElementById("matUploadStatus");
  const prog = document.getElementById("matProg");
  const progFill = document.getElementById("matProgFill");

  if (!title) {
    alert("Vui lòng nhập tên tài liệu!");
    return;
  }

  document.getElementById("matUploadAiPrompt").style.display = "none";
  lastUploadedMaterialForAi = null;

  status.style.color = "#1565c0";
  status.textContent = "📤 Đang upload lên máy chủ...";
  prog.style.display = "block";
  progFill.style.width = "20%";

  try {
    const uploaded = await uploadMaterialFile(file);
    const fileUrl = uploaded.fileUrl;
    progFill.style.width = "60%";
    status.textContent = "💾 Đang lưu thông tin...";

    const materialRecord = await createMaterial({
      title,
      chapter: chapter || "Chung",
      description: desc,
      fileName: uploaded.fileName,
      fileUrl,
      fileSize: uploaded.fileSize,
      cloudinaryPublicId: uploaded.publicId,
    });

    // Trích text sẵn (không tốn Groq) để banner "Tạo câu hỏi kiểm tra" bên dưới dùng ngay, không
    // phải tải lại file lần 2. KHÔNG tự gọi AI ở đây (audit 2026-08-16 mục 2): đường tự-sinh-âm-
    // thầm cũ đã bị bỏ — đây là lớp lỗi "câu AI-sinh lọt qua duyệt" thứ 3 phát hiện trong dự án
    // (auto-extract-khi-upload lần 1, 7 điểm Import lần 2), giờ mọi câu AI-sinh đều bắt buộc qua
    // examGenModal để giáo viên duyệt/sửa/chọn trước khi lưu, không có ngoại lệ.
    let extractedText = "";
    try {
      progFill.style.width = "80%";
      const pdfResp = await fetch(fileUrl);
      const pdfBuf = await pdfResp.arrayBuffer();
      const pdf = await pdfjsLib.getDocument({ data: pdfBuf }).promise;
      const maxPages = Math.min(pdf.numPages, 40);
      for (let i = 1; i <= maxPages; i++) {
        const page = await pdf.getPage(i);
        const tc = await page.getTextContent();
        extractedText += tc.items.map((t) => t.str).join(" ") + "\n";
      }
    } catch (extractErr) {
      console.warn("Không trích được text để chuẩn bị Sinh đề AI:", extractErr);
    }

    progFill.style.width = "100%";
    status.style.color = "#2e7d32";
    status.textContent = `✅ Upload thành công: "${title}"!`;
    showToast("Upload tài liệu thành công!", "success");

    const aiPrompt = document.getElementById("matUploadAiPrompt");
    if (extractedText.trim().length > 100) {
      lastUploadedMaterialForAi = {
        sourceText: extractedText,
        chapter: chapter || title,
        label: title,
        materialId: materialRecord.id,
      };
      aiPrompt.style.display = "flex";
    } else {
      aiPrompt.style.display = "none";
    }

    document.getElementById("matTitle").value = "";
    document.getElementById("matChapter").value = "";
    document.getElementById("matDesc").value = "";
    document.getElementById("matFileInput").value = "";
    loadMaterials();
  } catch (err) {
    progFill.style.width = "100%";
    progFill.style.background = "#ef5350";
    status.style.color = "#c62828";
    status.textContent = "❌ Lỗi: " + err.message;
  }
}

async function loadMaterials() {
  const el = document.getElementById("matList");
  if (!el) return;
  el.innerHTML = '<div style="text-align:center;padding:16px;color:var(--gray-400);"><i class="fas fa-spinner fa-spin" style="font-size:1.6rem;margin-bottom:6px;display:block;"></i>Đang tải...</div>';

  try {
    allMaterials = await listMaterials();
  } catch (err) {
    el.innerHTML = `<div style="text-align:center;padding:24px;color:#c62828;"><i class="fas fa-exclamation-triangle" style="font-size:2rem;margin-bottom:8px;display:block;"></i>Lỗi: ${err.message}</div>`;
    return;
  }

  if (!allMaterials.length) {
    el.innerHTML =
      '<div style="text-align:center;padding:24px;color:var(--gray-400);"><i class="fas fa-folder-open" style="font-size:2rem;margin-bottom:8px;display:block;"></i>Chưa có tài liệu nào. Upload tài liệu đầu tiên phía trên!</div>';
    return;
  }

  el.innerHTML = allMaterials
    .map((m) => {
      const size =
        m.fileSize > 1024 * 1024
          ? (m.fileSize / 1024 / 1024).toFixed(1) + " MB"
          : (m.fileSize / 1024).toFixed(0) + " KB";
      const date = new Date(m.createdAtUtc).toLocaleDateString("vi-VN");
      return `<div style="display:flex;align-items:center;gap:12px;padding:12px 0;border-bottom:1px solid var(--gray-100);">
      <div style="width:42px;height:42px;background:#fce4ec;border-radius:10px;display:flex;align-items:center;justify-content:center;font-size:1.2rem;flex-shrink:0;">📄</div>
      <div style="flex:1;min-width:0;">
        <div style="font-weight:700;font-size:0.82rem;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;">${m.title}</div>
        <div style="font-size:0.68rem;color:var(--gray-500);">${m.chapter} · ${size} · ${date} · 👁 ${m.viewCount} lượt xem</div>
      </div>
      <div style="display:flex;gap:6px;flex-shrink:0;">
        <a href="${m.fileUrl}" target="_blank" class="btn btn-outline btn-sm" title="Xem file" aria-label="Xem file ${m.title}">
          <i class="fas fa-eye"></i>
        </a>
        <button class="btn btn-sm" style="background:#ede7f6;color:#5e35b1;border:none;"
          onclick="openGenerateExamModal('${m.id}')" title="Sinh đề bằng AI" aria-label="Sinh đề bằng AI từ tài liệu ${m.title}">
          <i class="fas fa-wand-magic-sparkles"></i>
        </button>
        <button class="btn btn-sm" style="background:${m.isActive ? "#e8f5e9" : "#fff3e0"};color:${m.isActive ? "#2e7d32" : "#b45309"};border:none;"
          onclick="toggleMaterial('${m.id}')" title="${m.isActive ? "Ẩn" : "Hiện"} tài liệu" aria-label="${m.isActive ? "Ẩn" : "Hiện"} tài liệu ${m.title}">
          <i class="fas fa-${m.isActive ? "eye-slash" : "eye"}"></i>
        </button>
        <button class="btn btn-sm" style="background:#fce4ec;color:#c62828;border:none;"
          onclick="deleteMaterialRow('${m.id}')" title="Xóa" aria-label="Xóa tài liệu ${m.title}">
          <i class="fas fa-trash"></i>
        </button>
      </div>
    </div>`;
    })
    .join("");
}

async function toggleMaterial(id) {
  const m = allMaterials.find((x) => x.id === id);
  if (!m) return;
  try {
    await updateMaterial(id, {
      title: m.title,
      chapter: m.chapter,
      description: m.description,
      isActive: !m.isActive,
    });
  } catch (err) {
    showToast("Lỗi: " + err.message, "error");
    return;
  }
  loadMaterials();
}

async function deleteMaterialRow(id) {
  if (!confirm("Xóa tài liệu này? Học viên sẽ không thể xem nữa.")) return;
  try {
    // content-service deletes the Cloudinary file server-side as part of this call.
    await deleteMaterial(id);
  } catch (err) {
    showToast("Lỗi: " + err.message, "error");
    return;
  }
  showToast("Đã xóa tài liệu", "success");
  loadMaterials();
}

function escapeHtml(str) {
  return String(str ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);
}

// ════════════════════════════════════════════
// SINH ĐỀ BẰNG AI — nút gắn theo từng Material đã upload (audit C1). Trích text từ file PDF đã
// lưu trên Cloudinary (fetch qua URL công khai + pdf.js, cùng cơ chế extractPDF trong batch
// import), gọi ai-service POST /generate-exam-set MỘT lần để sinh cả câu trắc nghiệm lẫn tự luận.
// Chỉ trả về danh sách ứng viên — giáo viên SỬA ĐƯỢC từng câu (không chỉ tick chọn) ngay trong
// modal trước khi bấm Lưu; lúc lưu mới thật sự gọi createQuestion/createEssayQuestion với
// SourceType="AiGenerated" + SourceMaterialId (đúng thiết kế đã xác nhận).
// ════════════════════════════════════════════
let examGenMaterial = null;
let examGenCandidates = { mcq: [], essay: [] };
// Khi khác null: nguồn sinh đề KHÔNG phải 1 Material đã lưu (file vừa chọn ở panel Import, hoặc
// text đã trích lúc upload Material — xem openGenerateExamModalFromSource, audit 2026-08-16).
let examGenSourceOverride = null;

function resetExamGenModalUi(title, chapterDefault) {
  document.getElementById("examGenTitle").textContent = title;
  document.getElementById("examGenChapter").value = chapterDefault;
  document.getElementById("examGenMcqCount").value = 12;
  document.getElementById("examGenEssayCount").value = 1;
  document.getElementById("examGenStatus").textContent = "";
  document.getElementById("examGenCandidates").innerHTML = "";
  document.getElementById("examGenSaveBtn").style.display = "none";
  document.getElementById("examGenSaveExportBtn").style.display = "none";
  document.getElementById("examGenPublishNote").style.display = "none";
  document.getElementById("examGenScopeContainer").innerHTML = scopePickerHtml("examGenScope");
  initScopePicker("examGenScope");
  openModal("examGenModal");
}

function openGenerateExamModal(materialId) {
  const material = allMaterials.find((m) => m.id === materialId);
  if (!material) return;
  examGenMaterial = material;
  examGenSourceOverride = null;
  examGenCandidates = { mcq: [], essay: [] };
  resetExamGenModalUi(`Sinh đề bằng AI — ${material.title}`, material.chapter || material.title);
}

/** Điểm vào thay thế cho openGenerateExamModal() khi nguồn KHÔNG phải 1 Material đã lưu sẵn —
 * dùng cho: (1) file vừa chọn trong panel Import, chưa từng upload lên đâu (audit 2026-08-16 mục
 * 1); (2) banner "Tạo câu hỏi kiểm tra" ngay sau khi upload Material, dùng lại text đã trích lúc
 * upload thay vì tải lại file (mục 2). `materialId` chỉ có ở case (2) — case (1) truyền
 * undefined/null vì chưa có Material nào tồn tại, sourceMaterialId khi lưu sẽ là null (đã xác
 * nhận nullable end-to-end ở quiz-service, không cần Material thật mới lưu được câu AI-sinh). */
function openGenerateExamModalFromSource({ sourceText, chapter, label, materialId }) {
  examGenMaterial = null;
  examGenSourceOverride = { sourceText, materialId: materialId || null };
  examGenCandidates = { mcq: [], essay: [] };
  resetExamGenModalUi(`Sinh đề bằng AI — ${label}`, chapter || label);
}

/** Tải file PDF từ Cloudinary URL (fetch trực tiếp, Cloudinary delivery URL cho phép CORS công
 * khai) rồi trích text bằng pdf.js — cùng cơ chế importPDFBatch() dùng cho file mới chọn, chỉ
 * khác nguồn dữ liệu là URL thay vì File object đang cầm trên tay. */
async function extractMaterialText(material) {
  const res = await fetch(material.fileUrl);
  if (!res.ok) throw new Error(`Không tải được file tài liệu (HTTP ${res.status})`);
  const buffer = await res.arrayBuffer();
  const pdf = await pdfjsLib.getDocument({ data: buffer }).promise;

  let fullText = "";
  for (let p = 1; p <= Math.min(pdf.numPages, 60); p++) {
    const page = await pdf.getPage(p);
    const tc = await page.getTextContent();
    fullText += tc.items.map((i) => i.str).join(" ") + "\n";
  }
  return fullText;
}

async function runGenerateExamSet() {
  if (!examGenMaterial && !examGenSourceOverride) return;
  const chapter =
    document.getElementById("examGenChapter").value.trim() ||
    (examGenMaterial ? examGenMaterial.title : "Chương mới");
  // `|| 12`/`|| 1` sẽ biến 0 thành 12/1 vì 0 là falsy trong JS — Việc 3 (2026-08-16) cho phép nhập
  // 0 để bỏ hẳn 1 loại câu hỏi, nên chỉ fallback khi thật sự không parse được số (NaN), không phải
  // khi giá trị hợp lệ là 0.
  const mcqCountRaw = parseInt(document.getElementById("examGenMcqCount").value, 10);
  const mcqCount = Number.isNaN(mcqCountRaw) ? 12 : mcqCountRaw;
  const essayCountRaw = parseInt(document.getElementById("examGenEssayCount").value, 10);
  const essayCount = Number.isNaN(essayCountRaw) ? 1 : essayCountRaw;
  const statusEl = document.getElementById("examGenStatus");
  const btn = document.getElementById("examGenRunBtn");

  if (mcqCount === 0 && essayCount === 0) {
    statusEl.style.color = "#c62828";
    statusEl.textContent = "Cần ít nhất 1 loại câu hỏi (trắc nghiệm hoặc tự luận).";
    return;
  }

  btn.disabled = true;
  statusEl.style.color = "var(--gray-500)";
  statusEl.textContent = "Đang phân tích tài liệu...";
  document.getElementById("examGenCandidates").innerHTML = "";
  document.getElementById("examGenSaveBtn").style.display = "none";
  document.getElementById("examGenSaveExportBtn").style.display = "none";

  try {
    const text = examGenSourceOverride ? examGenSourceOverride.sourceText : await extractMaterialText(examGenMaterial);
    if (!text.trim()) {
      statusEl.style.color = "#c62828";
      statusEl.textContent = "Không trích được nội dung văn bản từ tài liệu này (có thể là file scan ảnh, không có lớp text để đọc).";
      return;
    }

    statusEl.textContent = "Đang sinh câu hỏi bằng AI (có thể mất vài chục giây)...";
    const result = await generateExamSet(chapter, text.slice(0, 8000), mcqCount, essayCount);

    examGenCandidates = {
      mcq: (result.mcqQuestions || []).map((q) => ({ ...q, chapter, selected: true })),
      essay: (result.essayQuestions || []).map((q) => ({ ...q, chapter, selected: true })),
    };

    if (!examGenCandidates.mcq.length && !examGenCandidates.essay.length) {
      statusEl.style.color = "#c62828";
      statusEl.textContent = "AI không sinh được câu hỏi nào từ nội dung này — thử tài liệu khác hoặc kiểm tra lại nội dung.";
      return;
    }

    renderExamCandidates();
    statusEl.style.color = "#2e7d32";
    const genParts = [];
    if (examGenCandidates.mcq.length) genParts.push(`${examGenCandidates.mcq.length} câu trắc nghiệm`);
    if (examGenCandidates.essay.length) genParts.push(`${examGenCandidates.essay.length} câu tự luận`);
    statusEl.textContent = `Đã sinh ${genParts.join(" + ")}. Xem lại, sửa nếu cần, rồi bấm "Lưu các câu đã chọn".`;
    document.getElementById("examGenSaveBtn").style.display = "block";
    document.getElementById("examGenSaveExportBtn").style.display = "block";
  } catch (err) {
    statusEl.style.color = "#c62828";
    statusEl.textContent = "Lỗi: " + err.message;
  } finally {
    btn.disabled = false;
  }
}

function renderExamCandidates() {
  const container = document.getElementById("examGenCandidates");
  document.getElementById("examGenPublishNote").style.display = "block";
  let html = "";

  if (examGenCandidates.mcq.length) {
    html += `<div style="font-weight:700;margin:12px 0 6px;">Trắc nghiệm (${examGenCandidates.mcq.length})</div>`;
    examGenCandidates.mcq.forEach((q, i) => {
      const options = ["A", "B", "C", "D"];
      html += `<div class="card" style="margin-bottom:10px;padding:10px;">
        <div style="display:flex;gap:8px;align-items:flex-start;">
          <input type="checkbox" ${q.selected ? "checked" : ""} onchange="examGenCandidates.mcq[${i}].selected=this.checked" style="margin-top:6px;flex-shrink:0;" aria-label="Chọn câu trắc nghiệm ${i + 1}">
          <div style="flex:1;min-width:0;">
            <textarea class="form-input" rows="2" style="margin-bottom:6px;" oninput="examGenCandidates.mcq[${i}].questionText=this.value">${escapeHtml(q.questionText)}</textarea>
            <div style="display:grid;grid-template-columns:1fr 1fr;gap:6px;margin-bottom:6px;">
              ${options
                .map(
                  (L, oi) => `<div style="display:flex;gap:4px;align-items:center;">
                  <input type="radio" name="mcqCorrect${i}" ${q.correctAnswer === oi ? "checked" : ""} onchange="examGenCandidates.mcq[${i}].correctAnswer=${oi}" aria-label="Đáp án đúng là ${L}">
                  <input class="form-input" style="font-size:0.78rem;padding:6px;" value="${escapeHtml(q["option" + L])}" oninput="examGenCandidates.mcq[${i}].option${L}=this.value">
                </div>`,
                )
                .join("")}
            </div>
            <input class="form-input" style="font-size:0.75rem;padding:6px;" placeholder="Giải thích (không bắt buộc)" value="${escapeHtml(q.explanation || "")}" oninput="examGenCandidates.mcq[${i}].explanation=this.value">
          </div>
        </div>
      </div>`;
    });
  }

  if (examGenCandidates.essay.length) {
    html += `<div style="font-weight:700;margin:12px 0 6px;">Tự luận (${examGenCandidates.essay.length})</div>`;
    examGenCandidates.essay.forEach((q, i) => {
      html += `<div class="card" style="margin-bottom:10px;padding:10px;">
        <div style="display:flex;gap:8px;align-items:flex-start;">
          <input type="checkbox" ${q.selected ? "checked" : ""} onchange="examGenCandidates.essay[${i}].selected=this.checked" style="margin-top:6px;flex-shrink:0;" aria-label="Chọn câu tự luận ${i + 1}">
          <div style="flex:1;min-width:0;">
            <textarea class="form-input" rows="2" style="margin-bottom:6px;" oninput="examGenCandidates.essay[${i}].questionText=this.value">${escapeHtml(q.questionText)}</textarea>
            <textarea class="form-input" rows="2" placeholder="Đáp án gợi ý (để giáo viên tham khảo khi chấm)" oninput="examGenCandidates.essay[${i}].suggestedAnswer=this.value">${escapeHtml(q.suggestedAnswer || "")}</textarea>
          </div>
        </div>
      </div>`;
    });
  }

  container.innerHTML = html;
}

/** Lưu các candidate đã tick chọn vào ngân hàng câu hỏi thật (quiz-service) — dùng chung cho cả
 * "Lưu các câu đã chọn" và "Lưu & Xuất Word" (C4), tránh lặp code 2 nơi. Trả về id thật của các
 * câu vừa tạo (để nút Xuất Word gọi export ngay mà không cần load lại danh sách). */
async function persistSelectedExamQuestions() {
  const mcqSelected = examGenCandidates.mcq.filter((q) => q.selected);
  const essaySelected = examGenCandidates.essay.filter((q) => q.selected);
  // examGenMaterial: Material thật đã lưu (nút 🪄 trên từng dòng tài liệu). examGenSourceOverride:
  // nguồn thay thế (file panel Import hoặc text vừa upload) — materialId có thể null (file chưa
  // từng lưu thành Material) hoặc là id thật (banner ngay-sau-upload, mục 2).
  const sourceMaterialId = examGenMaterial ? examGenMaterial.id : (examGenSourceOverride?.materialId ?? null);
  const lopIds = getScopePickerLopIds("examGenScope");

  const questionIds = [];
  const essayQuestionIds = [];
  let failedCount = 0;

  for (const q of mcqSelected) {
    try {
      const created = await createQuestion({
        chapter: q.chapter,
        questionText: q.questionText,
        optionA: q.optionA,
        optionB: q.optionB,
        optionC: q.optionC,
        optionD: q.optionD,
        correctAnswer: q.correctAnswer,
        explanation: q.explanation || "",
        sourceType: "AiGenerated",
        sourceMaterialId,
        lopIds,
      });
      questionIds.push(created.id);
    } catch {
      failedCount++;
    }
  }

  for (const q of essaySelected) {
    try {
      const created = await createEssayQuestion({
        chapter: q.chapter,
        questionText: q.questionText,
        suggestedAnswer: q.suggestedAnswer || "",
        sourceType: "AiGenerated",
        sourceMaterialId,
        lopIds,
      });
      essayQuestionIds.push(created.id);
    } catch {
      failedCount++;
    }
  }

  return { questionIds, essayQuestionIds, failedCount };
}

async function saveSelectedExamQuestions() {
  const mcqSelected = examGenCandidates.mcq.filter((q) => q.selected);
  const essaySelected = examGenCandidates.essay.filter((q) => q.selected);
  if (!mcqSelected.length && !essaySelected.length) {
    showToast("Chưa chọn câu hỏi nào để lưu", "error");
    return;
  }

  const btn = document.getElementById("examGenSaveBtn");
  btn.disabled = true;
  btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Đang lưu...';

  const { questionIds, essayQuestionIds, failedCount } = await persistSelectedExamQuestions();
  const savedCount = questionIds.length + essayQuestionIds.length;

  btn.disabled = false;
  btn.innerHTML = '<i class="fas fa-save"></i> Lưu các câu đã chọn';

  if (failedCount) {
    showToast(`Đã lưu ${savedCount} câu, ${failedCount} câu lỗi`, "error");
  } else {
    showToast(`Đã lưu ${savedCount} câu vào ngân hàng câu hỏi`, "success");
    closeModal("examGenModal");
    if (typeof loadQuestions === "function") loadQuestions();
    advanceAiSourceQueue();
  }
}

/** C4: lưu các candidate đã tick chọn RỒI xuất ngay ra .docx — vì export endpoint cần id thật
 * (đã lưu DB), không xuất trực tiếp từ candidate list chưa lưu được. */
async function saveAndExportSelectedExamQuestions() {
  const mcqSelected = examGenCandidates.mcq.filter((q) => q.selected);
  const essaySelected = examGenCandidates.essay.filter((q) => q.selected);
  if (!mcqSelected.length && !essaySelected.length) {
    showToast("Chưa chọn câu hỏi nào để lưu & xuất", "error");
    return;
  }

  const btn = document.getElementById("examGenSaveExportBtn");
  btn.disabled = true;
  btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Đang lưu & xuất...';

  try {
    const { questionIds, essayQuestionIds, failedCount } = await persistSelectedExamQuestions();
    if (questionIds.length || essayQuestionIds.length) {
      await exportQuestionsToWord(questionIds, essayQuestionIds, "de-thi-ai-sinh.docx");
    }
    const savedCount = questionIds.length + essayQuestionIds.length;
    if (failedCount) {
      showToast(`Đã lưu ${savedCount} câu, ${failedCount} câu lỗi, đã xuất Word`, "error");
    } else {
      showToast(`Đã lưu ${savedCount} câu và xuất file Word`, "success");
      closeModal("examGenModal");
      if (typeof loadQuestions === "function") loadQuestions();
      advanceAiSourceQueue();
    }
  } catch (err) {
    showToast("Lỗi: " + err.message, "error");
  } finally {
    btn.disabled = false;
    btn.innerHTML = '<i class="fas fa-file-word"></i> Lưu & Xuất Word';
  }
}

// ============================================================
// HỒ SƠ CÁ NHÂN — dùng chung Admin + Teacher thật (không phải Admin-only/Teacher-only, gọi từ
// showPanel() dispatch không cần guard typeof). Name sửa được qua PUT /me (updateProfile, đã có
// sẵn); Email/Lớp-Khóa/Chức vụ chỉ đọc (đúng quyết định Gap 2 — chỉ Admin/GV chủ nhiệm sửa được 2
// field sau qua endpoint riêng). Avatar upload qua Cloudinary (POST /me/avatar) — validate loại/
// dung lượng ở ĐÂY (client) trước khi gọi API, để báo lỗi ngay khi chọn file thay vì chờ upload
// xong mới biết; backend tự validate lại độc lập (đây không phải biên bảo mật, chỉ là UX).
// ============================================================
const AVATAR_MAX_BYTES = 5 * 1024 * 1024;
const AVATAR_ALLOWED_TYPES = ["image/jpeg", "image/png", "image/webp"];

async function loadProfilePanel() {
  let profile;
  try {
    profile = await getProfile();
  } catch (err) {
    showToast("Không tải được hồ sơ: " + err.message, "error");
    return;
  }
  await renderProfilePanel(profile);
}

async function renderProfilePanel(profile) {
  document.getElementById("profileNameInput").value = profile.name || "";
  document.getElementById("profileEmailValue").textContent = profile.email || "—";

  // Rà soát Lần XVI (2026-08-21) — "Vai trò" chỉ đọc, hiện cho mọi role.
  const roleLabel = { Admin: "Quản trị viên", Teacher: "Giảng viên", Student: "Học viên" };
  const roleValueEl = document.getElementById("profileRoleValue");
  if (roleValueEl) roleValueEl.textContent = roleLabel[profile.role] || profile.role || "—";

  // Rà soát Lần VI (2026-08-21) — "Lớp / Khóa" (profile.lopId) và "Chức vụ" (profile.chucVu, badge
  // Lớp trưởng/phó) trước hiện CHO CẢ Admin lẫn Teacher dù 2 field đó CHỈ có ý nghĩa cho STUDENT
  // (Admin/Teacher không có lopId, chucVu luôn là default "Học viên" vô nghĩa với họ) — bug hiển
  // thị sai đã tồn tại từ trước, không phải mới phát sinh. Sửa: Teacher giờ hiện "Lớp phụ trách"
  // (Lớp GV thật sự chủ nhiệm, qua authListMyLop) thay "Lớp / Khóa"; "Chức vụ" cho Teacher là ô
  // CHỌN chức danh học thuật (ChucVuGV, tự sửa), không phải badge Lớp trưởng/phó.
  // Rà soát Lần XVI (2026-08-21) — Admin trước đây không có gì thêm ngoài Email/Họ tên. Người dùng
  // yêu cầu Admin cũng cần SĐT/Cấp bậc/Chức vụ như GV — SĐT/CapBac/ChucVuGV giờ hiện cho CẢ Admin
  // lẫn Teacher (backend AuthServiceImpl.UpdateProfileAsync đã thêm nhánh Admin cùng 3 field này).
  // Bộ môn/Khoa + Môn học/Lớp phụ trách vẫn CHỈ Teacher (không áp dụng cho Admin theo đúng yêu cầu).
  const isTeacher = profile.role === "Teacher";
  const isAdmin = profile.role === "Admin";
  const showPersonalFields = isTeacher || isAdmin;
  document.getElementById("profilePhoneGroup").style.display = showPersonalFields ? "block" : "none";
  document.getElementById("profileBoMonKhoaGroup").style.display = isTeacher ? "block" : "none";
  document.getElementById("profileCapBacGroup").style.display = showPersonalFields ? "block" : "none";
  document.getElementById("profileChucVuGVGroup").style.display = showPersonalFields ? "block" : "none";
  document.getElementById("profileMonHocRow").style.display = isTeacher ? "flex" : "none";
  document.getElementById("profileLopPhuTrachRow").style.display = isTeacher ? "flex" : "none";
  if (showPersonalFields) {
    document.getElementById("profilePhoneInput").value = profile.soDienThoai || "";

    const capBacSel = document.getElementById("profileCapBacInput");
    capBacSel.innerHTML = CAP_BAC_OPTIONS.map((cb) => `<option value="${cb}">${cb}</option>`).join("");
    capBacSel.value = profile.capBac || CAP_BAC_OPTIONS[0];

    const chucVuGvSel = document.getElementById("profileChucVuGVInput");
    chucVuGvSel.innerHTML = CHUC_VU_GV_OPTIONS.map((cv) => `<option value="${cv}">${cv}</option>`).join("");
    chucVuGvSel.value = profile.chucVuGV || CHUC_VU_GV_OPTIONS[0];
  }
  if (isTeacher) {
    document.getElementById("profileBoMonKhoaInput").value = profile.boMonKhoa || "";
    document.getElementById("profileMonHocValue").textContent = profile.monHocPhuTrach || "Chưa được phân công";

    document.getElementById("profileLopPhuTrachValue").textContent = "Đang tải...";
    try {
      const myLop = await authListMyLop();
      document.getElementById("profileLopPhuTrachValue").textContent = myLop.length
        ? myLop.map((l) => l.ten).join(", ")
        : "Chưa được phân công";
    } catch {
      document.getElementById("profileLopPhuTrachValue").textContent = "Lỗi tải danh sách lớp";
    }
  }

  const img = document.getElementById("profileAvatarImg");
  const placeholder = document.getElementById("profileAvatarPlaceholder");
  if (profile.avatarUrl) {
    img.src = profile.avatarUrl;
    img.style.display = "block";
    placeholder.style.display = "none";
  } else {
    img.removeAttribute("src");
    img.style.display = "none";
    placeholder.style.display = "flex";
  }
}

async function saveProfileName() {
  const btn = document.getElementById("profileSaveNameBtn");
  const name = document.getElementById("profileNameInput").value.trim();
  if (!name) {
    showToast("Họ tên không được để trống", "error");
    return;
  }

  // Việc 3.1 — chỉ đọc/gửi field nếu panel đang HIỆN field đó (đã ẩn/hiện đúng role ở
  // renderProfilePanel) — Student không có các input này trong DOM nên không đọc.
  // Rà soát Lần XVI (2026-08-21) — SĐT/Cấp bậc/Chức vụ giờ hiện cho CẢ Admin lẫn Teacher (cùng
  // showPersonalFields ở renderProfilePanel), tách riêng cờ Bộ môn/Khoa (VẪN chỉ Teacher) — trước
  // đọc chung 1 cờ phoneGroupVisible cho cả 2 nhóm, gộp nhầm ý nghĩa 2 field khác phạm vi role.
  const personalFieldsVisible = document.getElementById("profilePhoneGroup").style.display !== "none";
  const boMonKhoaVisible = document.getElementById("profileBoMonKhoaGroup").style.display !== "none";
  const phone = personalFieldsVisible ? document.getElementById("profilePhoneInput").value.trim() : "";
  if (phone && !/^0\d{9}$/.test(phone)) {
    showToast("Số điện thoại phải gồm đúng 10 số, bắt đầu bằng 0", "error");
    return;
  }
  const boMonKhoa = boMonKhoaVisible ? document.getElementById("profileBoMonKhoaInput").value.trim() : "";
  const capBac = personalFieldsVisible ? document.getElementById("profileCapBacInput").value : "";
  const chucVuGV = personalFieldsVisible ? document.getElementById("profileChucVuGVInput").value : "";

  btn.disabled = true;
  btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Đang lưu...';
  try {
    await updateProfile({ name, soDienThoai: phone, boMonKhoa, capBac, chucVuGV });
    const msg = document.getElementById("profileSaveMsg");
    msg.textContent = "✓ Đã lưu thành công!";
    msg.style.color = "#2e7d32";
    msg.style.display = "block";
    setTimeout(() => {
      msg.style.display = "none";
    }, 2000);
  } catch (err) {
    showToast("Lỗi: " + err.message, "error");
  } finally {
    btn.disabled = false;
    btn.textContent = "Lưu thay đổi";
  }
}

// Rà soát Lần VI (2026-08-21) — Đổi mật khẩu, dùng chung cho cả Admin lẫn Teacher (modal
// #changePasswordModal khai báo 1 lần trong mỗi HTML, xem quan-ly-noi-dung.html/
// quan-tri-he-thong.html — nút mở modal đặt trong panel-profile).
function openChangePasswordModal() {
  document.getElementById("cpCurrentInput").value = "";
  document.getElementById("cpNewInput").value = "";
  document.getElementById("cpConfirmInput").value = "";
  document.getElementById("cpMsg").style.display = "none";
  openModal("changePasswordModal");
}

async function submitChangePassword() {
  const current = document.getElementById("cpCurrentInput").value;
  const next = document.getElementById("cpNewInput").value;
  const confirm = document.getElementById("cpConfirmInput").value;
  const msg = document.getElementById("cpMsg");
  msg.style.display = "none";

  if (!current || !next) {
    msg.textContent = "Vui lòng nhập đủ mật khẩu hiện tại và mật khẩu mới.";
    msg.style.display = "block";
    return;
  }
  if (next.length < 8) {
    msg.textContent = "Mật khẩu mới phải từ 8 ký tự trở lên.";
    msg.style.display = "block";
    return;
  }
  if (next !== confirm) {
    msg.textContent = "Xác nhận mật khẩu mới không khớp.";
    msg.style.display = "block";
    return;
  }

  const btn = document.getElementById("cpSubmitBtn");
  btn.disabled = true;
  btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Đang lưu...';
  try {
    await changeMyPassword(current, next);
    showToast("✓ Đã đổi mật khẩu!", "success");
    closeModal("changePasswordModal");
  } catch (err) {
    msg.style.color = "var(--danger)";
    msg.textContent = "Lỗi: " + err.message;
    msg.style.display = "block";
  } finally {
    btn.disabled = false;
    btn.innerHTML = "Đổi mật khẩu";
  }
}

async function onProfileAvatarSelected(file) {
  if (!file) return;
  const statusEl = document.getElementById("profileAvatarStatus");
  const inputEl = document.getElementById("profileAvatarInput");

  // Validate phía client TRƯỚC khi upload — báo lỗi ngay lúc chọn file, không để user chờ upload
  // xong mới biết sai định dạng/quá dung lượng.
  if (!AVATAR_ALLOWED_TYPES.includes(file.type)) {
    statusEl.textContent = "Chỉ chấp nhận ảnh JPG, PNG hoặc WebP.";
    statusEl.style.color = "#c62828";
    inputEl.value = "";
    return;
  }
  if (file.size > AVATAR_MAX_BYTES) {
    statusEl.textContent = "Ảnh vượt quá 5MB.";
    statusEl.style.color = "#c62828";
    inputEl.value = "";
    return;
  }

  statusEl.textContent = "Đang tải lên...";
  statusEl.style.color = "var(--gray-500)";
  try {
    const updated = await uploadAvatar(file);
    await renderProfilePanel(updated);
    statusEl.textContent = "✓ Đã cập nhật avatar!";
    statusEl.style.color = "#2e7d32";
    setTimeout(() => {
      statusEl.textContent = "";
    }, 2000);
  } catch (err) {
    statusEl.textContent = "Lỗi: " + err.message;
    statusEl.style.color = "#c62828";
  } finally {
    inputEl.value = "";
  }
}

// ============================================================
// PAGINATION + SORT (VIỆC 2 mục 2, chuyển sang dùng chung ở Việc 4.3 2026-08-20) — helper cho các
// bảng dữ liệu client-side đã tải hết (Admin: Quản lý tài khoản, Lớp trong Khóa; Teacher: Dashboard
// roster mở rộng, Thành viên gộp nhiều lớp). Trước ở riêng admin/quan-tri-he-thong.html — chuyển
// sang đây để Teacher dùng lại được, hành vi giữ NGUYÊN 100% (chỉ đổi vị trí file).
// ============================================================
function paginateAndSort(state, list) {
  let sorted = [...list];
  if (state.sortKey) {
    sorted.sort((a, b) => {
      const av = a[state.sortKey];
      const bv = b[state.sortKey];
      // null/undefined luôn xếp cuối, bất kể chiều sort — tránh "Chưa gán"/"—" nhảy lên đầu.
      if (av == null && bv == null) return 0;
      if (av == null) return 1;
      if (bv == null) return -1;
      const cmp =
        typeof av === "number" && typeof bv === "number"
          ? av - bv
          : String(av).localeCompare(String(bv), "vi");
      return state.sortDir === "desc" ? -cmp : cmp;
    });
  }
  const totalPages = Math.max(1, Math.ceil(sorted.length / state.pageSize));
  state.page = Math.min(state.page, totalPages);
  state.total = sorted.length;
  const start = (state.page - 1) * state.pageSize;
  return {
    pageItems: sorted.slice(start, start + state.pageSize),
    totalPages,
    total: sorted.length,
  };
}

function renderPaginationControls(containerId, state, totalPages, onPageChangeFn) {
  const el = document.getElementById(containerId);
  if (!el) return;
  if (totalPages <= 1) {
    el.innerHTML = "";
    return;
  }
  el.innerHTML = `
    <div class="pagination">
      <button class="btn btn-outline btn-sm" ${state.page === 1 ? "disabled" : ""} onclick="${onPageChangeFn}(${state.page - 1})">
        <i class="fas fa-chevron-left"></i>
      </button>
      <span class="pagination-info">Trang ${state.page}/${totalPages} (${state.total ?? ""})</span>
      <button class="btn btn-outline btn-sm" ${state.page === totalPages ? "disabled" : ""} onclick="${onPageChangeFn}(${state.page + 1})">
        <i class="fas fa-chevron-right"></i>
      </button>
    </div>`;
}

function updateSortIcons(prefix, state, keys) {
  keys.forEach((k) => {
    const el = document.getElementById(`sort-icon-${prefix}-${k}`);
    if (!el) return;
    if (state.sortKey !== k) {
      el.innerHTML = '<i class="fas fa-sort" style="opacity:.35"></i>';
    } else {
      el.innerHTML =
        state.sortDir === "asc"
          ? '<i class="fas fa-sort-up"></i>'
          : '<i class="fas fa-sort-down"></i>';
    }
  });
}

// Drag & drop — chạy ngay khi file này load, nên phải include ở CUỐI <body>
// (sau div#batchDrop trong HTML), giống đúng vị trí script gốc trong admin.html cũ.
const batchDrop = document.getElementById("batchDrop");
if (batchDrop) {
  batchDrop.addEventListener("dragover", (e) => {
    e.preventDefault();
    batchDrop.classList.add("drag-over");
  });
  batchDrop.addEventListener("dragleave", () => batchDrop.classList.remove("drag-over"));
  batchDrop.addEventListener("drop", (e) => {
    e.preventDefault();
    batchDrop.classList.remove("drag-over");
    addBatchFiles(e.dataTransfer.files);
  });
}
