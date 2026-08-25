-- ================================================
-- SETUP V2 — Thêm bảng câu hỏi & vấn đáp
-- Chạy thêm file này trong SQL Editor
-- ================================================

-- Bảng câu hỏi trắc nghiệm (import từ Word/PDF)
CREATE TABLE IF NOT EXISTS public.questions (
  id UUID DEFAULT gen_random_uuid() PRIMARY KEY,
  user_id UUID REFERENCES public.profiles(id) ON DELETE CASCADE,
  chapter TEXT NOT NULL DEFAULT 'Chương 1',
  question TEXT NOT NULL,
  option_a TEXT NOT NULL DEFAULT '',
  option_b TEXT NOT NULL DEFAULT '',
  option_c TEXT DEFAULT '',
  option_d TEXT DEFAULT '',
  correct_answer INTEGER NOT NULL DEFAULT 0,
  explanation TEXT DEFAULT '',
  created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Bảng câu hỏi vấn đáp tự luận
CREATE TABLE IF NOT EXISTS public.oral_questions (
  id UUID DEFAULT gen_random_uuid() PRIMARY KEY,
  user_id UUID REFERENCES public.profiles(id) ON DELETE CASCADE,
  chapter TEXT DEFAULT 'Chung',
  question TEXT NOT NULL,
  expected_answer TEXT DEFAULT '',
  difficulty INTEGER DEFAULT 1, -- 1=dễ, 2=TB, 3=khó
  created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Bảng lưu bài thi vấn đáp chi tiết
CREATE TABLE IF NOT EXISTS public.oral_results (
  id UUID DEFAULT gen_random_uuid() PRIMARY KEY,
  user_id UUID REFERENCES public.profiles(id) ON DELETE CASCADE NOT NULL,
  question_id UUID REFERENCES public.oral_questions(id),
  main_answer TEXT DEFAULT '',
  followup_answers JSONB DEFAULT '[]',
  ai_score NUMERIC(4,2) DEFAULT 0,
  ai_comment TEXT DEFAULT '',
  created_at TIMESTAMPTZ DEFAULT NOW()
);

-- RLS
ALTER TABLE public.questions ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.oral_questions ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.oral_results ENABLE ROW LEVEL SECURITY;

-- Policies cho questions (mọi user đều đọc được, chỉ owner sửa)
CREATE POLICY "q_select_all" ON public.questions FOR SELECT USING (true);
CREATE POLICY "q_insert" ON public.questions FOR INSERT WITH CHECK (auth.uid() = user_id);
CREATE POLICY "q_delete" ON public.questions FOR DELETE USING (auth.uid() = user_id);

-- Policies oral_questions
CREATE POLICY "oq_select_all" ON public.oral_questions FOR SELECT USING (true);
CREATE POLICY "oq_insert" ON public.oral_questions FOR INSERT WITH CHECK (auth.uid() = user_id);

-- Policies oral_results
CREATE POLICY "or_select" ON public.oral_results FOR SELECT USING (auth.uid() = user_id);
CREATE POLICY "or_insert" ON public.oral_results FOR INSERT WITH CHECK (auth.uid() = user_id);

-- Kiểm tra
SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name;