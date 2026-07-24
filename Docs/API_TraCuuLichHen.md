# API 3 — TRA CỨU LỊCH HẸN KHÁM ĐÃ ĐẶT
### (Bổ sung vào `CongKhaiController` — sau API #2 `tra-cuu-ho-so`)

====================================================================

## 1. Tổng quan

| | |
|---|---|
| **Mục đích** | Khách vãng lai tự tra cứu (các) lịch hẹn đã đặt qua form đặt lịch online, để xem Ngày hẹn / Ca hẹn / Trạng thái xử lý — không cần đăng nhập. |
| **Route** | `GET api/CongKhai/tra-cuu-lich-hen` |
| **Phân quyền** | `[AllowAnonymous]` — không token |
| **Bảng liên quan** | `DatLichKham` (chính), `NhanVien`, `DanhMucKhoa` (join lấy tên hiển thị) |
| **Điều kiện khớp** | Bắt buộc khớp **CẢ HAI**: Họ tên khách (`HoTenKhach`) VÀ Số điện thoại (`SDT`) — đúng theo yêu cầu bảo mật giống API `tra-cuu-ho-so` (maBN + sdt) |

====================================================================

## 2. Request

```
GET /api/CongKhai/tra-cuu-lich-hen?hoTen={hoTen}&sdt={sdt}
```

| Query param | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `hoTen` | string | ✅ | Họ tên khách đã nhập lúc đặt lịch (`HoTenKhach`) |
| `sdt` | string | ✅ | Số điện thoại đã đăng ký lúc đặt lịch |

====================================================================

## 3. Validate

**B1.** Thiếu `hoTen` hoặc `sdt` → `400 BadRequest`
```json
{ "message": "Vui lòng nhập đầy đủ Họ tên và Số điện thoại" }
```

**B2.** Validate định dạng `sdt` — **chốt theo yêu cầu team: chỉ chấp nhận `0xxxxxxxxx`** (10 chữ số, bắt đầu bằng `0`), giống hệt quy tắc đang dùng ở API `tra-cuu-ho-so`:

```csharp
if (!Regex.IsMatch(sdt.Trim(), @"^0\d{9}$"))
    return BadRequest(new { message = "Số điện thoại không đúng định dạng" });
```

> ⚠️ **Lưu ý rủi ro có sẵn (không phải lỗi của API này)**: `DatLichKhamController.DatLich` (API đặt lịch) vẫn đang chấp nhận cả `0xxxxxxxxx` lẫn `+84xxxxxxxxx` và lưu y nguyên chuỗi khách nhập vào cột `SDT`. Nếu khách lỡ đặt lịch bằng số dạng `+84941646475`, thì khi tra cứu ở đây (chỉ nhận `0xxxxxxxxx`) sẽ **không tìm thấy**. Rủi ro này nằm ở khâu đặt lịch — có thể xử lý sau bằng cách chuẩn hoá SĐT ngay lúc lưu ở `DatLichKhamController.DatLich`, không thuộc phạm vi API tra cứu này.

Sai định dạng → `400 BadRequest`
```json
{ "message": "Số điện thoại không đúng định dạng" }
```

====================================================================

## 4. Logic xử lý

**B1.** Chốt theo yêu cầu team: **so khớp chính xác cả hai trường** — `HoTenKhach` giữ nguyên dấu (Trim), `SDT` đúng định dạng `0xxxxxxxxx` (Trim). Không cần bỏ dấu / chuẩn hoá gì thêm, nên có thể truy vấn thẳng trong SQL, không cần load hết rồi lọc lại trong bộ nhớ:

```csharp
var ketQua = await _context.DatLichKhams
    .AsNoTracking()
    .Include(d => d.MaNvNavigation)
        .ThenInclude(nv => nv!.MaKhoaNavigation)
    .Where(d => d.Sdt == sdt.Trim() && d.HoTenKhach == hoTen.Trim())
    .OrderByDescending(d => d.NgayHen)
    .ThenByDescending(d => d.MaDatLich)
    .Take(20) // giới hạn, tránh trả về danh sách quá dài cho khách vãng lai
    .ToListAsync();
```

> Ghi chú: collation mặc định của SQL Server cho `nvarchar` thường **không phân biệt hoa/thường nhưng có phân biệt dấu** (VD: `CI_AS`). Nghĩa là `"nguyễn văn a"` vẫn khớp `"Nguyễn Văn A"`, nhưng `"Nguyen Van A"` (không dấu) thì **không khớp** — đúng như yêu cầu team đã chốt (khách phải gõ tên có dấu).

**B2.** Không có kết quả nào khớp → `404 NotFound`, thông báo **chung chung** (không tiết lộ là sai tên hay sai SĐT, đúng nguyên tắc chống dò thông tin đã áp dụng ở `tra-cuu-ho-so`):
```json
{ "message": "Không tìm thấy lịch hẹn khớp với thông tin đã nhập" }
```

**B3.** Có kết quả → `200 OK`, trả danh sách (xem mục 5).

> Không trả: `MaNV` (mã nội bộ), `MaKhoa` (mã nội bộ) — chỉ trả tên hiển thị (`tenBacSi`, `tenKhoa`), theo đúng nguyên tắc "không trả mã nội bộ" đã áp dụng ở API #2.

====================================================================

## 5. Response — Thành công (`200 OK`)

```json
{
  "data": [
    {
      "maDatLich": 128,
      "hoTenKhach": "Nguyễn Văn A",
      "sdt": "0941646475",
      "ngayHen": "2026-07-28",
      "caHen": "Sang",
      "yeuCauKham": "Đau bụng, buồn nôn",
      "trangThai": "DaXacNhan",
      "tenBacSi": "BS. Trần Thị B",
      "tenKhoa": "Nội tổng quát"
    }
  ]
}
```

| Field | Kiểu | Ghi chú |
|---|---|---|
| `maDatLich` | int | Mã lịch hẹn |
| `hoTenKhach` | string | |
| `sdt` | string | |
| `ngayHen` | string (`yyyy-MM-dd`) | |
| `caHen` | string | `Sang` \| `Chieu` |
| `yeuCauKham` | string? | |
| `trangThai` | string | `ChoXacNhan` \| `DaXacNhan` \| `DaTiepNhan` \| `DaHuy` — frontend tự map badge màu (đã có sẵn hàm `getStatusBadge` trong `CustomerPortal.jsx`) |
| `tenBacSi` | string? | null nếu khách đặt không chọn bác sĩ cụ thể |
| `tenKhoa` | string? | |

====================================================================

## 6. Các quyết định

| # | Vấn đề | Đã chốt |
|---|---|---|
| 1 | So khớp Họ tên | ✅ **Có dấu, so khớp chính xác** (Trim, không bỏ dấu) |
| 2 | Định dạng SĐT chấp nhận | ✅ **Chỉ `0xxxxxxxxx`** (10 số, bắt đầu bằng 0) |
| 3 | Giới hạn số lịch hẹn trả về | Top 20 mới nhất *(đề xuất, có thể đổi nếu cần)* |

====================================================================

## 7. Lưu ý cho Frontend

Ảnh UI hiện tại (`CustomerPortal.jsx` — tab "Tra cứu lịch hẹn") có ô nhập mẫu dạng **không dấu, viết hoa** (`NGUYEN VAN A`). Vì API đã chốt so khớp **có dấu, chính xác**, cần cập nhật placeholder/hướng dẫn trên form để khách nhập đúng **họ tên có dấu** như lúc đặt lịch (VD: `Nguyễn Văn A`), tránh nhầm lẫn dẫn đến báo "không tìm thấy" dù thông tin đúng.

====================================================================

## 8. Ví dụ gọi API

**Request:**
```
GET /api/CongKhai/tra-cuu-lich-hen?hoTen=Nguyễn Văn A&sdt=0941646475
```

**Response khi không tìm thấy:**
```json
{ "message": "Không tìm thấy lịch hẹn khớp với thông tin đã nhập" }
```
*(HTTP 404 — khớp với UI hiện có ở màn "Chưa tìm thấy lịch hẹn khám")*
