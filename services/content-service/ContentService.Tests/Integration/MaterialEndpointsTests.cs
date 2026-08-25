using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ContentService.Api.Dtos;
using Shared.Infrastructure.Common;
using Xunit;

namespace ContentService.Tests.Integration;

public sealed class MaterialEndpointsTests : IClassFixture<ContentApiFactory>
{
    private readonly HttpClient _client;
    private readonly ContentApiFactory _factory;

    public MaterialEndpointsTests(ContentApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static HttpRequestMessage WithAuth(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    [Fact]
    public async Task Student_cannot_create_material()
    {
        var request = WithAuth(HttpMethod.Post, "/api/v1/content/materials", TestTokens.Student());
        request.Content = JsonContent.Create(new CreateMaterialRequest("Bài 1", "1", "desc", "bai1.pdf", "https://example.com/bai1.pdf", 1024));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Teacher_can_create_and_student_can_read_active_material()
    {
        var createRequest = WithAuth(HttpMethod.Post, "/api/v1/content/materials", TestTokens.Teacher());
        createRequest.Content = JsonContent.Create(new CreateMaterialRequest("Bài 2", "2", "desc", "bai2.pdf", "https://example.com/bai2.pdf", 2048));
        var createResponse = await _client.SendAsync(createRequest);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<MaterialResponse>>();
        Assert.NotNull(created);

        var getRequest = WithAuth(HttpMethod.Get, $"/api/v1/content/materials/{created!.Data!.Id}", TestTokens.Student());
        var getResponse = await _client.SendAsync(getRequest);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<ApiResponse<MaterialResponse>>();
        Assert.Equal("Bài 2", fetched!.Data!.Title);
    }

    [Fact]
    public async Task Student_cannot_see_inactive_material()
    {
        var createRequest = WithAuth(HttpMethod.Post, "/api/v1/content/materials", TestTokens.Admin());
        createRequest.Content = JsonContent.Create(new CreateMaterialRequest("Bài ẩn", "3", null, "hidden.pdf", "https://example.com/hidden.pdf", 512));
        var createResponse = await _client.SendAsync(createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<MaterialResponse>>();

        var deactivateRequest = WithAuth(HttpMethod.Put, $"/api/v1/content/materials/{created!.Data!.Id}", TestTokens.Admin());
        deactivateRequest.Content = JsonContent.Create(new UpdateMaterialRequest("Bài ẩn", "3", null, IsActive: false));
        await _client.SendAsync(deactivateRequest);

        var studentGet = WithAuth(HttpMethod.Get, $"/api/v1/content/materials/{created.Data.Id}", TestTokens.Student());
        var studentResponse = await _client.SendAsync(studentGet);
        Assert.Equal(HttpStatusCode.NotFound, studentResponse.StatusCode);

        var teacherGet = WithAuth(HttpMethod.Get, $"/api/v1/content/materials/{created.Data.Id}", TestTokens.Teacher());
        var teacherResponse = await _client.SendAsync(teacherGet);
        Assert.Equal(HttpStatusCode.OK, teacherResponse.StatusCode);
    }

    [Fact]
    public async Task View_endpoint_increments_view_count()
    {
        var createRequest = WithAuth(HttpMethod.Post, "/api/v1/content/materials", TestTokens.Teacher());
        createRequest.Content = JsonContent.Create(new CreateMaterialRequest("Bài view", "4", null, "view.pdf", "https://example.com/view.pdf", 100));
        var createResponse = await _client.SendAsync(createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<MaterialResponse>>();

        var viewRequest1 = WithAuth(HttpMethod.Post, $"/api/v1/content/materials/{created!.Data!.Id}/view", TestTokens.Student());
        var viewResponse1 = await _client.SendAsync(viewRequest1);
        var count1 = await viewResponse1.Content.ReadFromJsonAsync<ApiResponse<int>>();
        Assert.Equal(1, count1!.Data);

        var viewRequest2 = WithAuth(HttpMethod.Post, $"/api/v1/content/materials/{created.Data.Id}/view", TestTokens.Student());
        var viewResponse2 = await _client.SendAsync(viewRequest2);
        var count2 = await viewResponse2.Content.ReadFromJsonAsync<ApiResponse<int>>();
        Assert.Equal(2, count2!.Data);
    }

    [Fact]
    public async Task Unauthenticated_request_returns_401()
    {
        var response = await _client.GetAsync("/api/v1/content/materials");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_with_missing_title_returns_validation_error()
    {
        var request = WithAuth(HttpMethod.Post, "/api/v1/content/materials", TestTokens.Teacher());
        request.Content = JsonContent.Create(new CreateMaterialRequest("", "1", null, "f.pdf", "https://example.com/f.pdf", 10));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static MultipartFormDataContent BuildPdfUpload(byte[] bytes, string fileName)
    {
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        var form = new MultipartFormDataContent { { fileContent, "file", fileName } };
        return form;
    }

    [Fact]
    public async Task Student_cannot_upload_file()
    {
        var request = WithAuth(HttpMethod.Post, "/api/v1/content/materials/upload", TestTokens.Student());
        request.Content = BuildPdfUpload([1, 2, 3], "test.pdf");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Teacher_can_upload_pdf_and_it_goes_through_file_storage_not_the_browser()
    {
        var uploadCountBefore = _factory.FileStorage.UploadCallCount;

        var request = WithAuth(HttpMethod.Post, "/api/v1/content/materials/upload", TestTokens.Teacher());
        request.Content = BuildPdfUpload([1, 2, 3, 4, 5], "lecture.pdf");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<UploadedFileResponse>>();
        Assert.NotNull(body!.Data);
        Assert.Contains("res.cloudinary.com", body.Data!.FileUrl);
        Assert.Equal(5, body.Data.FileSize);
        Assert.Equal(uploadCountBefore + 1, _factory.FileStorage.UploadCallCount);
    }

    [Fact]
    public async Task Upload_rejects_non_pdf_content_type()
    {
        var request = WithAuth(HttpMethod.Post, "/api/v1/content/materials/upload", TestTokens.Teacher());
        var fileContent = new ByteArrayContent([1, 2, 3]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        request.Content = new MultipartFormDataContent { { fileContent, "file", "not-a-pdf.png" } };

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_material_with_cloudinary_public_id_deletes_from_storage()
    {
        // Rà soát Lần VIII (2026-08-21) — CÙNG 1 giáo viên tạo lẫn xóa (Material giờ lọc/kiểm tra
        // theo người tải lên, 2 lần gọi TestTokens.Teacher() riêng sẽ sinh 2 userId khác nhau).
        var teacherToken = TestTokens.Teacher();
        var createRequest = WithAuth(HttpMethod.Post, "/api/v1/content/materials", teacherToken);
        createRequest.Content = JsonContent.Create(new CreateMaterialRequest(
            "Bài xoá", "5", null, "delete-me.pdf", "https://res.cloudinary.com/fake/delete-me.pdf", 100, "tthcm/materials/delete-me-public-id"));
        var createResponse = await _client.SendAsync(createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<MaterialResponse>>();

        var deleteRequest = WithAuth(HttpMethod.Delete, $"/api/v1/content/materials/{created!.Data!.Id}", teacherToken);
        var deleteResponse = await _client.SendAsync(deleteRequest);

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.Equal("tthcm/materials/delete-me-public-id", _factory.FileStorage.LastDeletedPublicId);
    }
}
