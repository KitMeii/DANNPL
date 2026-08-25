-- ================================================
-- SETUP V4 — Groq key + Ôn câu sai + Rubric vấn đáp
-- Chạy trong SQL Editor SAU setup.sql, v2, v3
-- ================================================

-- ================================================
-- BƯỚC 1: Thêm cột Groq API key vào profiles
-- ================================================
ALTER TABLE public.profiles
  ADD COLUMN IF NOT EXISTS groq_api_key TEXT DEFAULT '';

-- ================================================
-- BƯỚC 2: Bảng câu trả lời sai (ôn tập)
-- ================================================
CREATE TABLE IF NOT EXISTS public.wrong_answers (
  user_id     UUID REFERENCES public.profiles(id) ON DELETE CASCADE,
  question_id UUID REFERENCES public.questions(id) ON DELETE CASCADE,
  wrong_count INTEGER DEFAULT 1,
  last_wrong_at TIMESTAMPTZ DEFAULT NOW(),
  PRIMARY KEY (user_id, question_id)
);

ALTER TABLE public.wrong_answers ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "wa_select" ON public.wrong_answers;
DROP POLICY IF EXISTS "wa_insert" ON public.wrong_answers;
DROP POLICY IF EXISTS "wa_update" ON public.wrong_answers;
DROP POLICY IF EXISTS "wa_delete" ON public.wrong_answers;

CREATE POLICY "wa_select" ON public.wrong_answers FOR SELECT USING (auth.uid() = user_id);
CREATE POLICY "wa_insert" ON public.wrong_answers FOR INSERT WITH CHECK (auth.uid() = user_id);
CREATE POLICY "wa_update" ON public.wrong_answers FOR UPDATE USING (auth.uid() = user_id);
CREATE POLICY "wa_delete" ON public.wrong_answers FOR DELETE USING (auth.uid() = user_id);

-- ================================================
-- BƯỚC 3: Thêm cột rubric_scores vào oral_results
-- (Chỉ chạy nếu bảng oral_results đã tồn tại từ v2)
-- ================================================
DO $$
BEGIN
  IF EXISTS (
    SELECT 1 FROM information_schema.tables
    WHERE table_schema = 'public' AND table_name = 'oral_results'
  ) THEN
    ALTER TABLE public.oral_results
      ADD COLUMN IF NOT EXISTS rubric_scores JSONB DEFAULT '{}';
  END IF;
END $$;

-- ================================================
-- BƯỚC 4: Sync email từ auth vào profiles
-- ================================================
UPDATE public.profiles p
SET email = u.email
FROM auth.users u
WHERE p.id = u.id
  AND (p.email IS NULL OR p.email = '');

-- ================================================
-- KIỂM TRA — Kết quả mong đợi: đều có giá trị > 0
-- ================================================
SELECT 'profiles.groq_api_key' AS check_item, COUNT(*)::TEXT AS result
FROM information_schema.columns
WHERE table_schema='public' AND table_name='profiles' AND column_name='groq_api_key'
UNION ALL
SELECT 'wrong_answers table', COUNT(*)::TEXT
FROM information_schema.tables
WHERE table_schema='public' AND table_name='wrong_answers'
UNION ALL
SELECT 'wrong_answers policies', COUNT(*)::TEXT
FROM pg_policies WHERE tablename='wrong_answers' AND schemaname='public';