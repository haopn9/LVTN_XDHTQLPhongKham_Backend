# ĐẶC TẢ API — XÓA CHỈ ĐỊNH CLS

Module: KhamBenh (Khám bệnh)
Controller: `KhamBenhController`
Ngày viết: 23-07-2026

====================================================================

## 1. Mục đích

Cho phép bác sĩ xóa một chỉ định CLS đã lưu **khi chỉ định đó chưa được thực hiện**, nhằm sửa lỗi thao tác (chỉ định nhầm dịch vụ) mà không để lại dữ liệu rác trên phiếu khám.

Đây là phần bù cho lỗ hổng hiện tại: API `PUT /api/KhamBenh/{maPhieu}` (Nhóm 2 — `ChiDinhCLSMoi`) chỉ hỗ trợ **thêm**, không hỗ trợ xóa, khiến chỉ định cũ bị "chỉ định nhầm" tồn tại vĩnh viễn trên phiếu.

====================================================================

## 2. Endpoint

```
DELETE api/KhamBenh/{maPhieu}/chi-dinh-cls/{maChiTiet}
```

| Tham số      | Vị trí | Kiểu   | Bắt buộc | Ghi chú                                   |
|--------------|--------|--------|----------|--------------------------------------------|
| `maPhieu`    | route  | string | Có       | Mã phiếu khám                              |
| `maChiTiet`  | route  | int    | Có       | PK bảng `DichVuYte` (chỉ định CLS cần xóa) |

Không có request body.

====================================================================

## 3. Phân quyền

`[Authorize(Roles = "BacSi,Admin")]`

- **BacSi**: chỉ được xóa chỉ định CLS thuộc phiếu khám do chính mình phụ trách (dùng lại `CoQuyenTruyCapPhieu` hiện có).
- **Admin**: xóa được trên mọi phiếu.
- Phiếu khám đã ở trạng thái `TrangThaiKham = 3` (Hoàn thành): áp dụng cùng rule hiện có — chỉ Admin được sửa.

====================================================================

## 4. Điều kiện được phép xóa (Business Rules)

Chỉ định CLS **được xóa** khi thỏa **đồng thời cả 2 điều kiện**:

1. `TrangThaiDichVu == 0` (Chưa thực hiện)
2. `KetQua` là `null` hoặc rỗng (chưa từng ghi nhận kết quả)

> Lý do cần điều kiện 2 song song điều kiện 1: nếu bác sĩ từng toggle "Đã làm CLS" → "Chưa làm CLS" (revert), theo cơ chế hiện tại `KetQua` không tự động bị xóa khi toggle ngược — nên `TrangThaiDichVu == 0` một mình không đủ để đảm bảo an toàn. Xem mục 8 — thay đổi bắt buộc đi kèm ở API PUT.

Nếu không thỏa điều kiện trên → **chặn xóa**, trả lỗi 409 (xem mục 6).

====================================================================

## 5. Luồng xử lý

```
1. Xác thực đăng nhập (LayThongTinDangNhapAsync)
2. Kiểm tra phiếu khám tồn tại (maPhieu)
3. Kiểm tra quyền truy cập phiếu (CoQuyenTruyCapPhieu)
4. Nếu phiếu TrangThaiKham == 3 và không phải Admin → 403
5. Tìm bản ghi DichVuYte theo (MaChiTiet, MaPhieu)
   - Không tìm thấy → 404
6. Kiểm tra điều kiện xóa (mục 4)
   - Không thỏa → 409
7. Xóa bản ghi DichVuYte
8. SaveChangesAsync
9. Trả về 200 kèm phiếu khám sau cập nhật (dùng lại TaoResponsePhieuKham)
```

Không cần mở transaction — thao tác xóa 1 bản ghi, không đụng vào kho hay bảng liên quan khác (CLS không trừ kho).

====================================================================

## 6. Response

### 6.1. Thành công — 200 OK

```json
{
  "message": "Xóa chỉ định CLS thành công",
  "data": { /* toàn bộ phiếu khám sau cập nhật — giống response của GET/PUT hiện có */ }
}
```

### 6.2. Lỗi — 401 Unauthorized

```json
{ "message": "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại" }
```

### 6.3. Lỗi — 403 Forbidden

```json
{ "message": "Bạn không có quyền cập nhật phiếu khám này" }
```
hoặc (phiếu đã hoàn thành, không phải Admin):
```json
{ "message": "Phiếu khám đã hoàn thành, không thể chỉnh sửa" }
```

### 6.4. Lỗi — 404 Not Found

```json
{ "message": "Không tìm thấy phiếu khám cần cập nhật" }
```
hoặc (chỉ định CLS không tồn tại / không thuộc phiếu này):
```json
{ "message": "Chỉ định CLS không hợp lệ hoặc không thuộc phiếu khám này" }
```

### 6.5. Lỗi — 409 Conflict (chặn xóa)

```json
{ "message": "Không thể xóa chỉ định CLS đã thực hiện hoặc đã có kết quả" }
```

### 6.6. Lỗi — 500 Internal Server Error

```json
{ "message": "Không thể cập nhật dữ liệu từ máy chủ. Xin hãy thử lại" }
```

====================================================================

## 7. Ví dụ

**Request:**
```
DELETE api/KhamBenh/PK260723001/chi-dinh-cls/45
```

**Trường hợp thành công** (chỉ định #45 đang `TrangThaiDichVu = 0`, `KetQua = null`):
```json
{
  "message": "Xóa chỉ định CLS thành công",
  "data": { "maPhieu": "PK260723001", "chiDinhCLS": [ /* không còn #45 */ ], ... }
}
```

**Trường hợp bị chặn** (chỉ định #45 đang `TrangThaiDichVu = 1` hoặc có `KetQua`):
```json
{ "message": "Không thể xóa chỉ định CLS đã thực hiện hoặc đã có kết quả" }
```
→ HTTP 409

====================================================================

## 8. Thay đổi bắt buộc đi kèm ở API `PUT /api/KhamBenh/{maPhieu}` (Nhóm 3)

Để đảm bảo bất biến "`TrangThaiDichVu == 0` luôn đồng nghĩa với chưa có kết quả" (điều kiện xóa ở mục 4 dựa vào giả định này), cần sửa logic toggle hiện tại theo **Hướng A** đã thống nhất:

**Trước (hiện tại):**
```csharp
dichVuYte.TrangThaiDichVu = clsItem.DaLamCLS ? 1 : 0;
```

**Sau (cần sửa):**
```csharp
dichVuYte.TrangThaiDichVu = clsItem.DaLamCLS ? 1 : 0;
if (!clsItem.DaLamCLS)
{
    // Toggle "Đã làm" → "Chưa làm": coi như hủy kết quả, làm lại từ đầu
    dichVuYte.KetQua = null;
}
```

> Không sửa đoạn này thì API xóa ở đặc tả trên vẫn đúng logic, nhưng sẽ có trường hợp "chỉ định CLS bị toggle về 0 mà vẫn còn KetQua cũ" → bị chặn xóa dù `TrangThaiDichVu == 0` (đây là hành vi **đúng và an toàn** theo điều kiện mục 4, nhưng cần đảm bảo dev hiểu rõ vì sao — tránh nhầm là bug).

====================================================================

## 9. Việc cần làm (Checklist triển khai)

- [ ] Thêm action `XoaChiDinhCLS` vào `KhamBenhController`
- [ ] Sửa đoạn toggle Nhóm 3 trong `CapNhatThongTinKhamBenh` theo mục 8
- [ ] Viết test case:
  - Xóa thành công khi `TrangThaiDichVu = 0`, `KetQua = null`
  - Chặn xóa khi `TrangThaiDichVu = 1`
  - Chặn xóa khi `TrangThaiDichVu = 0` nhưng `KetQua` còn dữ liệu (revert case)
  - 404 khi `maChiTiet` không thuộc `maPhieu`
  - 403 khi BacSi xóa phiếu không phải của mình
  - 403 khi phiếu đã hoàn thành và không phải Admin
