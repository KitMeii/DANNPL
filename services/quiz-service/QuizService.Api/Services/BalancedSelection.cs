using QuizService.Api.Entities;

namespace QuizService.Api.Services;

/// <summary>Chọn 1 tập con cân đối độ khó từ 1 pool câu hỏi lớn hơn (C2 — "Import 150→50"). Câu
/// hỏi chưa phân loại (Difficulty = null) được coi là độ khó 2 (trung bình) khi cân đối, đúng
/// quyết định đã chốt cho dữ liệu cũ.</summary>
public static class BalancedSelection
{
    private const int DefaultDifficulty = 2;

    /// <summary>Phân bổ theo tỷ lệ độ khó của pool (largest-remainder method — sai lệch tối đa 1
    /// câu/nhóm so với tỷ lệ lý tưởng, luôn nằm trong ngưỡng ±10% yêu cầu), sau đó trong mỗi nhóm độ
    /// khó, xen kẽ theo Topic (round-robin) để tăng đa dạng chủ đề trước khi cắt đúng số lượng.</summary>
    public static List<Question> Select(List<Question> pool, int targetCount, Random rng)
    {
        if (targetCount >= pool.Count)
        {
            return [.. pool];
        }

        var buckets = pool.GroupBy(q => q.Difficulty ?? DefaultDifficulty).ToDictionary(g => g.Key, g => g.ToList());
        var difficulties = buckets.Keys.OrderBy(d => d).ToList();
        var totalPool = pool.Count;

        var rawTargets = difficulties.ToDictionary(d => d, d => (double)buckets[d].Count / totalPool * targetCount);
        var finalTargets = difficulties.ToDictionary(d => d, d => (int)Math.Floor(rawTargets[d]));

        var remainder = targetCount - finalTargets.Values.Sum();
        var byFractionDesc = difficulties.OrderByDescending(d => rawTargets[d] - finalTargets[d]).ToList();
        for (var i = 0; i < remainder; i++)
        {
            finalTargets[byFractionDesc[i % byFractionDesc.Count]]++;
        }

        // Nếu 1 nhóm không đủ câu để đáp ứng target (nhóm quá nhỏ), dồn phần thiếu sang các nhóm
        // còn dư sức chứa — vẫn giữ tổng đúng bằng targetCount.
        var deficit = 0;
        foreach (var d in difficulties)
        {
            if (finalTargets[d] > buckets[d].Count)
            {
                deficit += finalTargets[d] - buckets[d].Count;
                finalTargets[d] = buckets[d].Count;
            }
        }
        if (deficit > 0)
        {
            var spareOrder = difficulties.OrderByDescending(d => buckets[d].Count - finalTargets[d]).ToList();
            var idx = 0;
            while (deficit > 0 && idx < spareOrder.Count * targetCount)
            {
                var d = spareOrder[idx % spareOrder.Count];
                if (finalTargets[d] < buckets[d].Count)
                {
                    finalTargets[d]++;
                    deficit--;
                }
                idx++;
            }
        }

        var result = new List<Question>();
        foreach (var d in difficulties)
        {
            var topicInterleaved = InterleaveByTopic(buckets[d], rng);
            result.AddRange(topicInterleaved.Take(finalTargets[d]));
        }

        return result;
    }

    /// <summary>Xáo trong từng nhóm Topic rồi xen kẽ round-robin giữa các Topic — câu đầu tiên lấy
    /// ra ưu tiên trải đều Topic thay vì dồn hết 1 topic lên đầu danh sách trước khi Take() cắt.</summary>
    private static List<Question> InterleaveByTopic(List<Question> questions, Random rng)
    {
        var topicGroups = questions
            .GroupBy(q => q.Topic ?? "")
            .Select(g => g.OrderBy(_ => rng.Next()).ToList())
            .ToList();

        var result = new List<Question>();
        var maxLen = topicGroups.Count == 0 ? 0 : topicGroups.Max(g => g.Count);
        for (var i = 0; i < maxLen; i++)
        {
            foreach (var group in topicGroups)
            {
                if (i < group.Count)
                {
                    result.Add(group[i]);
                }
            }
        }

        return result;
    }
}
