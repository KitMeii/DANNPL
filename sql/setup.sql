-- ================================================
-- BƯỚC 1: XÓA SẠCH DỮ LIỆU CŨ (nếu có)
-- ================================================
DROP TRIGGER IF EXISTS on_auth_user_created ON auth.users;
DROP FUNCTION IF EXISTS handle_new_user();
DROP TABLE IF EXISTS study_logs CASCADE;
DROP TABLE IF EXISTS quiz_results CASCADE;
DROP TABLE IF EXISTS exam_results CASCADE;
DROP TABLE IF EXISTS profiles CASCADE;

-- ================================================
-- BƯỚC 2: TẠO CÁC BẢNG
-- ================================================

CREATE TABLE public.profiles (
  id UUID REFERENCES auth.users(id) ON DELETE CASCADE PRIMARY KEY,
  name TEXT NOT NULL DEFAULT 'Học viên',
  course TEXT DEFAULT '',
  unit TEXT DEFAULT '',
  class_name TEXT DEFAULT '',
  streak INTEGER DEFAULT 0,
  learned INTEGER DEFAULT 0,
  avg_score NUMERIC(4,2) DEFAULT 0,
  progress INTEGER DEFAULT 0,
  lesson_progress INTEGER DEFAULT 0,
  current_lesson TEXT DEFAULT 'Chương 1: Khái niệm cơ bản',
  total_study_minutes INTEGER DEFAULT 0,
  last_study_date DATE,
  created_at TIMESTAMPTZ DEFAULT NOW(),
  updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE public.exam_results (
  id UUID DEFAULT gen_random_uuid() PRIMARY KEY,
  user_id UUID REFERENCES public.profiles(id) ON DELETE CASCADE NOT NULL,
  score NUMERIC(4,2) NOT NULL,
  correct INTEGER NOT NULL,
  total INTEGER NOT NULL,
  time_spent_seconds INTEGER DEFAULT 0,
  created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE public.quiz_results (
  id UUID DEFAULT gen_random_uuid() PRIMARY KEY,
  user_id UUID REFERENCES public.profiles(id) ON DELETE CASCADE NOT NULL,
  chapter INTEGER NOT NULL DEFAULT 0,
  score NUMERIC(4,2) NOT NULL,
  correct INTEGER NOT NULL,
  total INTEGER NOT NULL,
  created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE public.study_logs (
  id UUID DEFAULT gen_random_uuid() PRIMARY KEY,
  user_id UUID REFERENCES public.profiles(id) ON DELETE CASCADE NOT NULL,
  study_date DATE NOT NULL DEFAULT CURRENT_DATE,
  minutes INTEGER DEFAULT 0,
  created_at TIMESTAMPTZ DEFAULT NOW(),
  UNIQUE(user_id, study_date)
);

-- ================================================
-- BƯỚC 3: BẬT ROW LEVEL SECURITY
-- ================================================
ALTER TABLE public.profiles ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.exam_results ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.quiz_results ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.study_logs ENABLE ROW LEVEL SECURITY;

-- ================================================
-- BƯỚC 4: TẠO POLICIES
-- ================================================

CREATE POLICY "profiles_select" ON public.profiles FOR SELECT USING (auth.uid() = id);
CREATE POLICY "profiles_insert" ON public.profiles FOR INSERT WITH CHECK (auth.uid() = id);
CREATE POLICY "profiles_update" ON public.profiles FOR UPDATE USING (auth.uid() = id);

CREATE POLICY "exam_select" ON public.exam_results FOR SELECT USING (auth.uid() = user_id);
CREATE POLICY "exam_insert" ON public.exam_results FOR INSERT WITH CHECK (auth.uid() = user_id);

CREATE POLICY "quiz_select" ON public.quiz_results FOR SELECT USING (auth.uid() = user_id);
CREATE POLICY "quiz_insert" ON public.quiz_results FOR INSERT WITH CHECK (auth.uid() = user_id);

CREATE POLICY "logs_select" ON public.study_logs FOR SELECT USING (auth.uid() = user_id);
CREATE POLICY "logs_insert" ON public.study_logs FOR INSERT WITH CHECK (auth.uid() = user_id);
CREATE POLICY "logs_update" ON public.study_logs FOR UPDATE USING (auth.uid() = user_id);

-- ================================================
-- BƯỚC 5: TẠO TRIGGER TỰ ĐỘNG TẠO PROFILE
-- ================================================
CREATE OR REPLACE FUNCTION public.handle_new_user()
RETURNS TRIGGER AS $$
BEGIN
  INSERT INTO public.profiles (id, name)
  VALUES (
    NEW.id,
    COALESCE(NEW.raw_user_meta_data->>'name', 'Học viên')
  )
  ON CONFLICT (id) DO NOTHING;
  RETURN NEW;
EXCEPTION
  WHEN OTHERS THEN
    RETURN NEW;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER SET search_path = public;

CREATE TRIGGER on_auth_user_created
  AFTER INSERT ON auth.users
  FOR EACH ROW EXECUTE FUNCTION public.handle_new_user();

-- ================================================
-- KIỂM TRA KẾT QUẢ
-- ================================================
SELECT table_name FROM information_schema.tables
WHERE table_schema = 'public'
ORDER BY table_name;