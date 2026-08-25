using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using QuizService.Api.Data;
using QuizService.Api.Dtos;
using QuizService.Api.Entities;

namespace QuizService.Api.Export;

/// <summary>Sinh file .docx đơn giản (câu hỏi + đáp án, không giải thích dài) cho MCQ (Question)
/// và tự luận (EssayQuestion) trộn lẫn — dùng DocumentFormat.OpenXml (SDK chính thức Microsoft,
/// chạy được server-side trên Linux container, không cần cài Word).</summary>
public sealed class WordExportService(QuizDbContext db) : IWordExportService
{
    private static readonly string[] OptionLabels = ["A", "B", "C", "D"];

    public async Task<byte[]> ExportAsync(ExportWordRequest request, CancellationToken ct)
    {
        var oralQuestionIds = request.OralQuestionIds ?? [];

        var questions = request.QuestionIds.Count > 0
            ? await db.Questions.Where(q => request.QuestionIds.Contains(q.Id)).ToListAsync(ct)
            : [];
        var essayQuestions = request.EssayQuestionIds.Count > 0
            ? await db.EssayQuestions.Where(q => request.EssayQuestionIds.Contains(q.Id)).ToListAsync(ct)
            : [];
        var oralQuestions = oralQuestionIds.Count > 0
            ? await db.OralQuestions.Where(q => oralQuestionIds.Contains(q.Id)).ToListAsync(ct)
            : [];

        // Giữ đúng thứ tự client gửi lên, không theo thứ tự trả về từ DB.
        var orderedQuestions = request.QuestionIds
            .Select(id => questions.FirstOrDefault(q => q.Id == id))
            .Where(q => q is not null)
            .Cast<Question>()
            .ToList();
        var orderedEssay = request.EssayQuestionIds
            .Select(id => essayQuestions.FirstOrDefault(q => q.Id == id))
            .Where(q => q is not null)
            .Cast<EssayQuestion>()
            .ToList();
        var orderedOral = oralQuestionIds
            .Select(id => oralQuestions.FirstOrDefault(q => q.Id == id))
            .Where(q => q is not null)
            .Cast<OralQuestion>()
            .ToList();

        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            var counter = 1;

            if (orderedQuestions.Count > 0)
            {
                body.AppendChild(Heading("I. TRẮC NGHIỆM"));
                foreach (var q in orderedQuestions)
                {
                    AppendMcq(body, counter++, q);
                }
            }

            if (orderedEssay.Count > 0)
            {
                body.AppendChild(Heading("II. TỰ LUẬN"));
                foreach (var q in orderedEssay)
                {
                    AppendEssay(body, counter++, q);
                }
            }

            if (orderedOral.Count > 0)
            {
                body.AppendChild(Heading("III. VẤN ĐÁP"));
                foreach (var q in orderedOral)
                {
                    AppendOral(body, counter++, q);
                }
            }
        }

        return stream.ToArray();
    }

    private static Paragraph Heading(string text) => new(
        new ParagraphProperties(new SpacingBetweenLines { Before = "240", After = "120" }),
        new Run(new RunProperties(new Bold()), new Text(text)));

    private static void AppendMcq(Body body, int index, Question q)
    {
        body.AppendChild(new Paragraph(
            new ParagraphProperties(new SpacingBetweenLines { Before = "120" }),
            new Run(new RunProperties(new Bold()), new Text($"Câu {index}. {q.QuestionText}"))));

        var options = new[] { q.OptionA, q.OptionB, q.OptionC, q.OptionD };
        for (var i = 0; i < options.Length; i++)
        {
            var isCorrect = i == q.CorrectAnswer;
            var run = isCorrect
                ? new Run(new RunProperties(new Bold(), new Underline { Val = UnderlineValues.Single }), new Text($"{OptionLabels[i]}. {options[i]}"))
                : new Run(new Text($"{OptionLabels[i]}. {options[i]}"));
            body.AppendChild(new Paragraph(new ParagraphProperties(new Indentation { Left = "360" }), run));
        }
    }

    private static void AppendEssay(Body body, int index, EssayQuestion q)
    {
        body.AppendChild(new Paragraph(
            new ParagraphProperties(new SpacingBetweenLines { Before = "120" }),
            new Run(new RunProperties(new Bold()), new Text($"Câu {index}. {q.QuestionText}"))));

        if (!string.IsNullOrWhiteSpace(q.SuggestedAnswer))
        {
            body.AppendChild(new Paragraph(
                new ParagraphProperties(new Indentation { Left = "360" }),
                new Run(new RunProperties(new Italic()), new Text($"Đáp án gợi ý: {q.SuggestedAnswer}"))));
        }
    }

    private static void AppendOral(Body body, int index, OralQuestion q)
    {
        body.AppendChild(new Paragraph(
            new ParagraphProperties(new SpacingBetweenLines { Before = "120" }),
            new Run(new RunProperties(new Bold()), new Text($"Câu {index}. {q.QuestionText}"))));

        if (!string.IsNullOrWhiteSpace(q.ExpectedAnswer))
        {
            body.AppendChild(new Paragraph(
                new ParagraphProperties(new Indentation { Left = "360" }),
                new Run(new RunProperties(new Italic()), new Text($"Đáp án chuẩn: {q.ExpectedAnswer}"))));
        }
    }
}
