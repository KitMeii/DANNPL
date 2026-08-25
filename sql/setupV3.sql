-- ================================================
-- SETUP V3 — Phân quyền role (teacher / student)
-- Chạy trong SQL Editor SAU setup.sql và setup-v2.sql
-- ================================================

-- ================================================
-- BƯỚC 1: Thêm cột role và email vào profiles
-- ================================================
ALTER TABLE public.profiles 
  ADD COLUMN IF NOT EXISTS role TEXT DEFAULT 'student' 
  CHECK (role IN ('student','teacher','admin'));

ALTER TABLE public.profiles
  ADD COLUMN IF NOT EXISTS email TEXT DEFAULT '';

-- ================================================
-- BƯỚC 2: Hàm helper lấy role — SECURITY DEFINER
-- Bắt buộc dùng hàm này thay vì query trực tiếp profiles
-- để tránh infinite recursion trong RLS policies
-- ================================================
CREATE OR REPLACE FUNCTION public.get_my_role()
RETURNS TEXT AS $$
  SELECT role FROM public.profiles WHERE id = auth.uid();
$$ LANGUAGE SQL SECURITY DEFINER STABLE SET search_path = public;

-- ================================================
-- BƯỚC 3: Cập nhật policies bảng profiles
-- ================================================
DROP POLICY IF EXISTS "profiles_select"      ON public.profiles;
DROP POLICY IF EXISTS "profiles_select_v2"   ON public.profiles;
DROP POLICY IF EXISTS "profiles_update"      ON public.profiles;
DROP POLICY IF EXISTS "profiles_update_role" ON public.profiles;
DROP POLICY IF EXISTS "profiles_insert"      ON public.profiles;
DROP POLICY IF EXISTS "profiles_select_v3"   ON public.profiles;
DROP POLICY IF EXISTS "profiles_update_v3"   ON public.profiles;
DROP POLICY IF EXISTS "profiles_insert_v3"   ON public.profiles;

CREATE POLICY "profiles_select_v3" ON public.profiles
  FOR SELECT USING (
    auth.uid() = id OR public.get_my_role() IN ('teacher','admin')
  );

CREATE POLICY "profiles_update_v3" ON public.profiles
  FOR UPDATE USING (
    auth.uid() = id OR public.get_my_role() IN ('teacher','admin')
  );

CREATE POLICY "profiles_insert_v3" ON public.profiles
  FOR INSERT WITH CHECK (auth.uid() = id);

-- ================================================
-- BƯỚC 4: Cập nhật policies bảng questions
-- ================================================
DROP POLICY IF EXISTS "q_select_all"       ON public.questions;
DROP POLICY IF EXISTS "q_insert"           ON public.questions;
DROP POLICY IF EXISTS "q_delete"           ON public.questions;
DROP POLICY IF EXISTS "q_read"             ON public.questions;
DROP POLICY IF EXISTS "q_insert_teacher"   ON public.questions;
DROP POLICY IF EXISTS "q_update_teacher"   ON public.questions;
DROP POLICY IF EXISTS "q_delete_teacher"   ON public.questions;
DROP POLICY IF EXISTS "q_read_v3"          ON public.questions;
DROP POLICY IF EXISTS "q_insert_v3"        ON public.questions;
DROP POLICY IF EXISTS "q_update_v3"        ON public.questions;
DROP POLICY IF EXISTS "q_delete_v3"        ON public.questions;

CREATE POLICY "q_read_v3"   ON public.questions FOR SELECT USING (auth.uid() IS NOT NULL);
CREATE POLICY "q_insert_v3" ON public.questions FOR INSERT WITH CHECK (public.get_my_role() IN ('teacher','admin'));
CREATE POLICY "q_update_v3" ON public.questions FOR UPDATE USING (public.get_my_role() IN ('teacher','admin'));
CREATE POLICY "q_delete_v3" ON public.questions FOR DELETE USING (public.get_my_role() IN ('teacher','admin'));

-- ================================================
-- BƯỚC 5: Cập nhật policies bảng oral_questions
-- ================================================
DROP POLICY IF EXISTS "oq_select_all"       ON public.oral_questions;
DROP POLICY IF EXISTS "oq_insert"           ON public.oral_questions;
DROP POLICY IF EXISTS "oq_read"             ON public.oral_questions;
DROP POLICY IF EXISTS "oq_insert_teacher"   ON public.oral_questions;
DROP POLICY IF EXISTS "oq_delete_teacher"   ON public.oral_questions;
DROP POLICY IF EXISTS "oq_read_v3"          ON public.oral_questions;
DROP POLICY IF EXISTS "oq_insert_v3"        ON public.oral_questions;
DROP POLICY IF EXISTS "oq_delete_v3"        ON public.oral_questions;

CREATE POLICY "oq_read_v3"   ON public.oral_questions FOR SELECT USING (auth.uid() IS NOT NULL);
CREATE POLICY "oq_insert_v3" ON public.oral_questions FOR INSERT WITH CHECK (public.get_my_role() IN ('teacher','admin'));
CREATE POLICY "oq_delete_v3" ON public.oral_questions FOR DELETE USING (public.get_my_role() IN ('teacher','admin'));

-- ================================================
-- BƯỚC 6: Teacher xem kết quả tất cả học viên
-- ================================================
DROP POLICY IF EXISTS "exam_select"      ON public.exam_results;
DROP POLICY IF EXISTS "exam_select_own"  ON public.exam_results;
DROP POLICY IF EXISTS "exam_select_v3"   ON public.exam_results;

CREATE POLICY "exam_select_v3" ON public.exam_results
  FOR SELECT USING (
    auth.uid() = user_id OR public.get_my_role() IN ('teacher','admin')
  );

DROP POLICY IF EXISTS "quiz_select"      ON public.quiz_results;
DROP POLICY IF EXISTS "quiz_select_own"  ON public.quiz_results;
DROP POLICY IF EXISTS "quiz_select_v3"   ON public.quiz_results;

CREATE POLICY "quiz_select_v3" ON public.quiz_results
  FOR SELECT USING (
    auth.uid() = user_id OR public.get_my_role() IN ('teacher','admin')
  );

-- ================================================
-- BƯỚC 7: View tổng hợp cho admin dashboard
-- ================================================
DROP VIEW IF EXISTS public.admin_student_summary;

CREATE VIEW public.admin_student_summary AS
SELECT
  p.id, p.name, p.email, p.course, p.class_name, p.role,
  p.streak, p.learned, p.avg_score, p.progress,
  p.total_study_minutes, p.last_study_date, p.created_at,
  COUNT(DISTINCT e.id) AS exam_count,
  MAX(e.score)         AS best_exam_score,
  COUNT(DISTINCT q.id) AS quiz_count
FROM public.profiles p
LEFT JOIN public.exam_results e ON e.user_id = p.id
LEFT JOIN public.quiz_results q ON q.user_id = p.id
GROUP BY p.id, p.name, p.email, p.course, p.class_name, p.role,
         p.streak, p.learned, p.avg_score, p.progress,
         p.total_study_minutes, p.last_study_date, p.created_at;

-- ================================================
-- KIỂM TRA — Kết quả mong đợi: 3 rows đều có giá trị
-- ================================================
SELECT 'profiles.role column' AS check_item,
  COUNT(*)::TEXT AS result
FROM information_schema.columns
WHERE table_schema='public' AND table_name='profiles' AND column_name='role'
UNION ALL
SELECT 'get_my_role function', COUNT(*)::TEXT
FROM pg_proc WHERE proname='get_my_role'
UNION ALL
SELECT 'admin_student_summary view', COUNT(*)::TEXT
FROM information_schema.views
WHERE table_schema='public' AND table_name='admin_student_summary';