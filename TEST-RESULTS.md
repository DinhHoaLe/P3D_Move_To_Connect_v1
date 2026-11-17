# Kiểm thử trực tiếp Plant 3D 2027 — v3

Ngày 18/11/2026. Bản vẽ Drawing1.dwg, Default Project, imperial, CS300, size 4 inch. Chạy PC_MTC3 qua giao diện Plant 3D; chọn Move.

## Kết quả: PASS

Nguồn: 2DF (pipe), 30F (elbow), 314 (connector), 317 (pipe), 31C (connector). Đích: pipe 302.
Chọn nguồn 2DF:S2 và đích 302:S1. Dịch chuyển WCS (12, -24.779528425562833, 0).

- 5 phần tử nguồn giữ nguyên handle/type, dịch chuyển cùng vector; hướng port giữ nguyên.
- Đối chiếu cả 12 port gốc: sai số tọa độ lớn nhất 0 trong dữ liệu JSON.
- 4 liên kết cũ giữ nguyên ở cả hai chiều: 2DF:S1–314:S2; 314:S1–30F:S1; 30F:S2–31C:S1; 31C:S2–317:S1.
- Hai port của pipe đích giữ nguyên tọa độ/hướng; pipe đích không bị gộp/xóa.
- Thêm đúng connector 325: 2DF:S2–325:S1 và 325:S2–302:S1. Tất cả liên kết có chiều ngược hợp lệ.
- Tổng số Part từ 6 thành 7. Các đầu trống còn lại vẫn trống.
- Command line báo: Da move va connect cum 5 doi tuong; da kiem tra cac ket noi cu.

Dữ liệu bằng PC_MTC_AUDIT, trong bin/Release/net10.0-windows/audit:
- Trước: audit-20261118-004137-842.json
- Sau: audit-20261118-004825-009.json

Để nguyên mô hình sau khi nối thành công cho người dùng xem. Không chủ động SAVE bản vẽ; AutoCAD có chạy autosave.

## Phạm vi chưa xác minh

Chưa kiểm thử trực tiếp Rotate, tee/vòng kín, compound/flanged joint, persistence sau lưu/mở lại, hoặc U sau thành công của v3. 5 kiểm tra graph độc lập đã chạy ở lượt trước; không thay thế các ca mô hình thực này.

## Phát hiện dẫn tới v3

PLANTCONNECTJOINT gộp hai pipe thẳng hàng, làm thay đổi chiều dài và xóa pipe đích. V2 phát hiện sai hình học rồi hoàn tác. Thí nghiệm native riêng cũng xác nhận hành vi gộp; hoàn tác phục hồi dữ liệu audit giống baseline. V3 chuyển sang ConnectorWrapper.Connect + Append; ca thử trên xác nhận hai pipe được giữ riêng và nối bằng connector mới.

## Cập nhật v3.1
Đảo thứ tự: đích đứng yên trước, cụm di chuyển sau. Bỏ Rotate và prompt Move/Rotate; thực hiện ngay sau lựa chọn thứ hai. Build artifacts/v3.1 thành công, 0 lỗi/0 cảnh báo. Chưa kiểm thử trực tiếp bản sửa flow này; bằng chứng mô hình ở trên thuộc bản v3 trước sửa flow.
