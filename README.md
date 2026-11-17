# Move to Connect v3.1 — Plant 3D 2027

C# / .NET 10 / Windows x64. Lệnh mới: **PC_MTC3**. Alias: PC_MTC.

## Nạp bản mới

1. Trong Plant 3D 2027, gõ NETLOAD.
2. Nạp `artifacts\v3.1\PlanCad_MoveToConnect_v3.dll`.
3. Gõ **PC_MTC3**. Dòng đầu phải hiện `Move to Connect v3.1 - Chon dich dung yen truoc, cum di chuyen sau.`
4. Click lần 1 gần đầu TRỐNG của pipe/fitting ĐỨNG YÊN (đích).
5. Click lần 2 gần đầu TRỐNG của pipe/fitting thuộc cụm CẦN DI CHUYỂN.
6. Tool tự chọn port trống gần điểm click, hiển thị tên port và tọa độ; không còn bước nhập số port.
7. Sau click thứ hai, tool tự di chuyển cả cụm và nối ngay; không hỏi chế độ hoặc yêu cầu Enter. Chỉ tịnh tiến, không xoay. Nếu hai port không đối hướng, dừng trước khi thay đổi mô hình.
8. Tool tạo joint bằng cấu hình Plant và kiểm tra lại kết nối. Gõ U để hoàn tác thao tác thành công.

Nếu đã nạp v3 trong phiên hiện tại, hãy lưu công việc và khởi động lại Plant 3D trước khi NETLOAD DLL trong artifacts/v3.1. NETLOAD không thay thế assembly v3 đã nạp. Tên lệnh vẫn là PC_MTC3; kiểm tra banner v3.1 để nhận biết đúng bản.

Log cũ dừng ở `Chon so port ... *Cancel*` là hủy lựa chọn, không phải thông báo lỗi API. V2 bỏ câu hỏi đó.

## Cụm được di chuyển

- Tự duyệt toàn bộ các kết nối từ đối tượng nguồn: pipe, InlineAsset (elbow/tee/fitting/valve), Connector chứa joint/weld/fastener.
- Bao gồm tất cả nhánh của tee và các vòng kín; mỗi đối tượng chỉ được biến đổi một lần.
- Giữ nguyên chiều dài, góc tương đối, ID đối tượng và joint hiện có. Các subpart thuộc connector đi cùng connector.
- Không gọi Disconnect hay tạo lại joint bên trong cụm. Ghi lại các cặp port trước khi move và xác minh chúng sau khi move và sau khi tạo joint mới.
- Chỉ thêm joint ở port nguồn đã chọn. Nếu mất kết nối cũ, hình học không đúng phép biến đổi cứng, hoặc xuất hiện kết nối ngoài ý muốn, lệnh báo lỗi và yêu cầu UNDO nhóm thao tác.
- Từ chối đích nằm trong cụm nguồn, nguồn không có port trống, phần tử bị khóa layer/PlacementLock/anchored, kết nối ngoài bản vẽ hoặc chưa phân giải.
- Nếu cụm chạm equipment/nozzle/support hoặc đối tượng chưa hỗ trợ, dừng toàn bộ trước khi sửa; không âm thầm chỉ di chuyển phần pipe.
- Toàn bộ cụm liên thông được lấy tự động, không chỉ các đối tượng đang chọn. Giới hạn 10.000 phần tử; vượt giới hạn thì dừng, không cắt ngắn cụm.
- Chưa hỗ trợ kéo giãn ống, routing thêm elbow/reducer, chọn một phần cụm rồi tự ngắt tại biên, equipment/nozzle và support. Không phải bản sao đầy đủ Microdesk.

## Tạo joint và hoàn tác

Sau khi căn hai port, lệnh dùng ConnectorWrapper.CanConnect, Connect và Append theo Joint Setup của project. Cách này giữ hai pipe riêng biệt; PLANTCONNECTJOINT đã được xác nhận gộp hai pipe thẳng hàng trong ca thử Plant 2027. Nếu không có joint phù hợp hoặc có nhiều lựa chọn, lệnh báo lỗi và hoàn tác. Compound joint cần fitting trung gian chưa được kiểm thử.

Move và joint nằm trong một Undo group. Khi có lỗi sau khi move đã commit, plugin gọi UNDO để khôi phục nhóm; nếu UNDO lỗi, dòng lệnh báo rõ để kiểm tra thủ công. Yêu cầu UNDO Control = All, không có nhóm Undo khác đang mở và đang ở Model tab.

Lỗi hiển thị kèm giai đoạn, ví dụ `PC_MTC3 [tao joint]: ...`. Có thể sao chép nguyên log để xác định đúng bước lỗi.

## Build và kiểm tra

```powershell
dotnet build PlanCad_MoveToConnect_ver1.csproj -c Release -o artifacts/v3.1
```

Đường dẫn mặc định: `C:\Program Files\Autodesk\AutoCAD 2027`, DLL Plant trong `PLNT3D`. Có thể truyền `-p:AutoCADDir=...` và `-p:Plant3DDir=...`.

Đã build v3.1 bằng DLL cài trên máy: **0 lỗi, 0 cảnh báo**. Thay đổi thứ tự chọn và bỏ Rotate đã build thành công; chưa chạy lại bản v3.1 trong Plant.

Đã chạy 5 kiểm tra thuật toán duyệt cụm: nhánh/vòng kín/connector, không lặp phần tử, pipe rời, giới hạn số phần tử và dừng tại biên không hỗ trợ. Chạy lại không cần NuGet bằng PowerShell 7:

```powershell
Add-Type -Path @((Join-Path (Get-Location) 'GraphWalk.cs'), (Join-Path (Get-Location) 'tests\Program.cs'))
[Program]::Main()
```

**Đã kiểm thử trực tiếp PC_MTC3 / Move trong Plant 3D 2027 ngày 18/11/2026:** cụm 2 pipe + 1 elbow + 2 connector di chuyển và nối thành công vào pipe đích. Đối chiếu JSON xác nhận 12 port gốc đúng hình học, cả 4 liên kết cũ còn nguyên, pipe đích đứng yên và xuất hiện đúng 1 connector mới. Xem [biên bản kiểm thử](TEST-RESULTS.md). Tee/vòng kín trong mô hình thật, UNDO sau thành công và lưu/mở lại vẫn chưa được kiểm thử. Danh sách ca kiểm tra tiếp:

| Ca | Kết quả mong đợi |
| --- | --- |
| Pipe–elbow–pipe nối sẵn, có đầu trống | Cả cụm di chuyển; joint cũ còn nguyên; joint mới xuất hiện |
| Tee với nhiều nhánh, hoặc vòng có nhánh hở | Tất cả nhánh đi cùng; không có phần tử bị biến đổi hai lần |
| Hai port không đối hướng | Dừng trước khi sửa; không xoay cụm |
| Hai đầu của cùng một cụm | Từ chối trước khi sửa |
| Cụm có phần tử khóa hoặc chạm equipment/xref/support | Báo vị trí bị chặn, không di chuyển một phần |
| Joint mới không tương thích hoặc người dùng hủy | Hoàn tác move; kết nối cũ giữ nguyên |
| UCS xoay/dịch, view 3D | Tên port được chọn đúng theo điểm click; căn đúng trong WCS |
| U sau thành công | Khôi phục cả cụm và xóa joint mới |
| Lưu rồi mở lại bản vẽ | Hình học và dữ liệu kết nối Plant vẫn hợp lệ |

## Mã nguồn

- MoveToConnectCommands.cs: chọn port, căn chỉnh, gọi joint, Undo.
- ConnectedAssembly.cs: đọc cụm, ghi nhận và xác minh port/kết nối/hình học.
- GraphWalk.cs: duyệt graph không lặp.
- tests/Program.cs: 5 kiểm tra thuật toán graph.

Tham khảo Autodesk: [kết nối trong cơ sở dữ liệu Plant](https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/How-are-the-connected-AutoCAD-Plant-3D-components-linked-together-in-the-project-database.html), [PLANTCONNECTJOINT](https://help.autodesk.com/cloudhelp/2023/ENU/Plant3D-UserGuide/files/GUID-2A156BBF-ECAC-491A-B87D-5BC83C500ABD.htm).


