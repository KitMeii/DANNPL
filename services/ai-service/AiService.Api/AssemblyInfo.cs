using System.Runtime.CompilerServices;

// Lets AiService.Tests' FakeAiProvider reuse the exact same MarkdownJson cleanup GroqProvider uses,
// instead of duplicating that logic in test code where it could silently drift out of sync.
[assembly: InternalsVisibleTo("AiService.Tests")]
