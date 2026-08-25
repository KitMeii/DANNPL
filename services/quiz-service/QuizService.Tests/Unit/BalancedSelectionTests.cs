using QuizService.Api.Entities;
using QuizService.Api.Services;
using Xunit;

namespace QuizService.Tests.Unit;

public sealed class BalancedSelectionTests
{
    private static Question MakeQuestion(int? difficulty, string? topic = null) => new()
    {
        QuestionText = "Q",
        OptionA = "A",
        OptionB = "B",
        OptionC = "C",
        OptionD = "D",
        CorrectAnswer = 0,
        Difficulty = difficulty,
        Topic = topic,
    };

    /// <summary>150 câu: 40 dễ (1) / 70 trung bình (2) / 40 khó (3) — tỷ lệ 26.7% / 46.7% / 26.7%.</summary>
    private static List<Question> BuildPool150()
    {
        var pool = new List<Question>();
        for (var i = 0; i < 40; i++) pool.Add(MakeQuestion(1, $"topic-{i % 5}"));
        for (var i = 0; i < 70; i++) pool.Add(MakeQuestion(2, $"topic-{i % 5}"));
        for (var i = 0; i < 40; i++) pool.Add(MakeQuestion(3, $"topic-{i % 5}"));
        return pool;
    }

    [Fact]
    public void Selecting_50_from_150_keeps_difficulty_distribution_within_10_percent_of_the_pool()
    {
        var pool = BuildPool150();
        var selected = BalancedSelection.Select(pool, 50, new Random(1));

        Assert.Equal(50, selected.Count);

        var poolProportions = pool.GroupBy(q => q.Difficulty!.Value).ToDictionary(g => g.Key, g => (double)g.Count() / pool.Count);
        var selectedProportions = selected.GroupBy(q => q.Difficulty!.Value).ToDictionary(g => g.Key, g => (double)g.Count() / selected.Count);

        foreach (var (difficulty, poolProportion) in poolProportions)
        {
            var selectedProportion = selectedProportions.GetValueOrDefault(difficulty, 0);
            var deviation = Math.Abs(selectedProportion - poolProportion);
            Assert.True(deviation <= 0.10, $"Difficulty {difficulty}: pool={poolProportion:P1}, selected={selectedProportion:P1}, deviation={deviation:P1} > 10%");
        }
    }

    [Fact]
    public void Different_random_seeds_produce_different_subsets()
    {
        var pool = BuildPool150();
        var subsetA = BalancedSelection.Select(pool, 50, new Random(1)).Select(q => q.Id).ToHashSet();
        var subsetB = BalancedSelection.Select(pool, 50, new Random(2)).Select(q => q.Id).ToHashSet();

        Assert.False(subsetA.SetEquals(subsetB), "2 seed khác nhau tạo ra 2 tập con giống hệt nhau — thuật toán chọn không đủ ngẫu nhiên.");
    }

    [Fact]
    public void Questions_with_null_difficulty_are_treated_as_difficulty_2()
    {
        var pool = new List<Question>();
        for (var i = 0; i < 50; i++) pool.Add(MakeQuestion(null));
        for (var i = 0; i < 50; i++) pool.Add(MakeQuestion(2));

        var selected = BalancedSelection.Select(pool, 40, new Random(1));

        // Toàn bộ pool thực chất chỉ có 1 "nhóm độ khó hiệu lực" (2, vì null coi như 2) — chọn
        // đúng 40 câu, không lỗi/crash khi group theo Difficulty ?? 2.
        Assert.Equal(40, selected.Count);
    }

    [Fact]
    public void Requesting_the_entire_pool_returns_everything_unchanged()
    {
        var pool = BuildPool150();
        var selected = BalancedSelection.Select(pool, pool.Count, new Random(1));

        Assert.Equal(pool.Count, selected.Count);
        Assert.Equal(pool.Select(q => q.Id).ToHashSet(), selected.Select(q => q.Id).ToHashSet());
    }

    [Fact]
    public void Selection_never_exceeds_a_bucket_that_is_smaller_than_its_proportional_target()
    {
        // Pool lệch hẳn: chỉ 2 câu khó (3) trong 150 câu — target 50 câu, tỷ lệ lý tưởng của nhóm
        // khó là 2/150*50 ≈ 0.67 câu nhưng thuật toán không thể "vay" quá 2 câu có sẵn.
        var pool = new List<Question>();
        for (var i = 0; i < 148; i++) pool.Add(MakeQuestion(1));
        for (var i = 0; i < 2; i++) pool.Add(MakeQuestion(3));

        var selected = BalancedSelection.Select(pool, 50, new Random(1));

        Assert.Equal(50, selected.Count);
        Assert.True(selected.Count(q => q.Difficulty == 3) <= 2);
    }
}
