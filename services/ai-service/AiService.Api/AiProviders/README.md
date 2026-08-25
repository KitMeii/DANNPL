# Thêm 1 nhà cung cấp AI mới

Kiến trúc này chỉ có 1 provider thật (Groq) tính đến thời điểm viết, nhưng được thiết kế để thêm
nhà thứ 2 trở đi mà không cần sửa `AiProviderRouter`, không cần sửa bất kỳ business Service nào
(`ChatService`, `LectureService`, `OralGradingService`, `QuestionExtractionService`).

## Các bước

1. **Viết class mới implement `IAiProvider`** trong `AiProviders/<TênNhà>/` (xem
   `AiProviders/Groq/GroqProvider.cs` làm mẫu):
   - `ChatAsync`/`CompleteTextAsync`: gửi request theo format của nhà đó, trả về text thô.
   - `CompleteJsonAsync`: PHẢI trả về chuỗi JSON hợp lệ, sẵn sàng
     `JsonSerializer.Deserialize` thẳng. Nếu nhà đó có flag JSON mode gốc (VD
     `response_format: {"type": "json_object"}`), dùng luôn — không cần bước dọn dẹp nào thêm.
     Nếu không, làm như Groq: dựa vào prompt (đã có sẵn ở tầng Service) rồi tự dọn dẹp fence
     markdown/rác thừa trước khi trả về.
   - Mọi lỗi ném ra PHẢI là `AiProviderTransientException` (đáng thử lại — 429, 5xx, mất mạng;
     có `retryAfterSeconds` nếu nhà đó tự cho biết) hoặc `AiProviderPermanentException` (thử lại
     provider này vô ích cho request này — 413, model không hợp lệ...). KHÔNG để lộ exception
     riêng của thư viện HTTP/nhà đó ra ngoài class provider.

2. **Đăng ký HttpClient** trong `Program.cs`:
   ```csharp
   builder.Services.AddHttpClient(nameof(TênNhàProvider));
   ```

3. **Thêm 1 case** trong `AiProviderFactory.Build(...)`:
   ```csharp
   "tên-trong-config" => new TênNhàProvider(httpClientFactory.CreateClient(nameof(TênNhàProvider)), config, apiKey),
   ```

4. **Thêm 1 entry** vào `appsettings.json`, mảng `Ai:Providers`:
   ```json
   { "Name": "tên-trong-config", "Enabled": true, "Priority": 2, "Model": "...", "BaseUrl": "...", "ApiKeyEnvVar": "TEN_NHA_API_KEY" }
   ```
   `Priority` thấp hơn = được thử trước. `ApiKeyEnvVar` chỉ ghi TÊN biến môi trường — key thật
   không bao giờ nằm trong file này, set qua `.env`/docker-compose như `GROQ_API_KEY` hiện tại.

Xong — không cần đổi gì ở `AiProviderRouter`, `AiEndpoints.cs`, hay các Service nghiệp vụ.

## Vì sao chat không tự động có failover

`AiProviderRouter.ChatAsync` gọi thẳng provider ưu tiên cao nhất, không retry/failover (quyết định
2026-08-18 — chat lỗi thì người dùng gửi lại dễ dàng, không mất gì như 1 bài giảng/đề thi vừa sinh
tốn công; failover giữa chừng cũng dễ rối nếu sau này chat có streaming). Provider mới nếu hỗ trợ
chat vẫn PHẢI implement `ChatAsync` như bình thường — chỉ là router chưa áp failover cho đường gọi
đó. Muốn bật failover cho chat sau này: đổi thân `AiProviderRouter.ChatAsync` sang gọi
`TryWithFailoverAsync(p => p.ChatAsync(...), ct)` giống `CompleteTextAsync` — không cần sửa
`IAiProvider` hay `IAiProviderRouter`, không cần sửa `ChatService`.

## Retry/failover nằm ở đâu

`AiProviderRouter.TryWithFailoverAsync` là NƠI DUY NHẤT biết cách retry/chuyển nhà — provider chỉ
việc phân loại lỗi của chính mình thành Transient/Permanent, không tự lặp hay tự chờ. Việc chia nhỏ
tài liệu dài thành nhiều đoạn (chunking) và nghỉ giữa các lượt gọi (pacing) KHÔNG nằm ở đây — đó là
nghiệp vụ riêng của `LectureService`/frontend (`giang-bai.html`), vì chỉ tính năng Giảng bài mới cần
biết "tôi sắp gọi N lượt liên tiếp cho 1 tài liệu". Thêm provider mới không cần đụng tới phần đó.
