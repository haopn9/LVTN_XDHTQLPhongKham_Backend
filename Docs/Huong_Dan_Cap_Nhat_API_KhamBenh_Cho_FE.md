# HƯỚNG DẪN CẬP NHẬT API KHÁM BỆNH (DÀNH CHO FRONTEND)

**Module:** Khám Bệnh (`KhamBenhController`)  
**Ngày cập nhật:** 23/07/2026  
**Mục đích:** Thông báo API mới (Xóa CLS) và thay đổi cơ chế kê Vật Tư / Toggle CLS để Frontend (FE) cập nhật giao diện & luồng gọi API.

---

## 📋 TỔNG QUAN THAY ĐỔI

1. **[MỚI] API Xóa chỉ định CLS:** Bổ sung endpoint `DELETE` để xóa chỉ định nhầm khi chưa thực hiện.
2. **[BREAKING CHANGE] Cơ chế kê Vật Tư (`VatTuList`):** Chuyển từ **CỘNG DỒN** sang **REPLACE (Thay thế toàn bộ)** — giống hệt luồng Đơn Thuốc.
3. **[CẬP NHẬT] Toggle trạng thái CLS:** Khi chuyển CLS từ "Đã làm" về "Chưa làm", hệ thống sẽ tự động xóa kết quả cũ (`ketQua = null`).

---

## 1. 🆕 API MỚI: XÓA CHỈ ĐỊNH CLS

Cho phép Bác sĩ / Admin xóa một dịch vụ CLS đã chỉ định nhầm (khi chưa thực hiện).

- **Endpoint:** `DELETE /api/KhamBenh/{maPhieu}/chi-dinh-cls/{maChiTiet}`
- **Headers:** `Authorization: Bearer <token>`
- **Phân quyền:** Bác sĩ phụ trách phiếu hoặc Admin.
- **Request Body:** Không có.

### 📌 Tham số Route:
| Tham số | Kiểu | Mô tả |
| :--- | :--- | :--- |
| `maPhieu` | string | Mã phiếu khám (VD: `PK260723001`) |
| `maChiTiet` | int | PK bản ghi chỉ định CLS (`maChiTiet` trong mảng `chiDinhCLS`) |

### ⚠️ Điều kiện xóa:
- `trangThaiCLS == 0` (Chưa thực hiện)
- `ketQua` là `null` hoặc rỗng (chưa từng ghi nhận kết quả)

### 📥 Response trả về:
- **200 OK:** Success — Trả về toàn bộ dữ liệu phiếu khám sau cập nhật (`data` giống GET/PUT).
```json
{
  "message": "Xóa chỉ định CLS thành công",
  "data": {
    "maPhieu": "PK260723001",
    "chiDinhCLS": [ /* Danh sách CLS đã loại bỏ maChiTiet vừa xóa */ ]
  }
}
```
- **409 Conflict:** Khi CLS đã làm hoặc đã có kết quả.
```json
{ "message": "Không thể xóa chỉ định CLS đã thực hiện hoặc đã có kết quả" }
```
- **403 Forbidden:** Phiếu đã hoàn thành (không phải Admin) hoặc không có quyền sửa phiếu.
- **404 Not Found:** Không tìm thấy phiếu hoặc chỉ định CLS không thuộc phiếu này.

---

## 2. ⚡ LƯU Ý BẮT BUỘC: KÊ VẬT TƯ (`VatTuList`) CHUYỂN SANG REPLACE

> 🚨 **BREAKING CHANGE DÀNH CHO FE:**  
> Trước đây: `VatTuList` gửi phần thêm mới (Backend tự cộng dồn).  
> **HIỆN TẠI:** `VatTuList` hoạt động theo cơ chế **REPLACE (Gửi toàn bộ danh sách vật tư hiện tại của phiếu)**.

### 🔄 Cách FE gửi Payload PUT `api/KhamBenh/{maPhieu}`:

```json
{
  "vatTuList": [
    { "maVatTu": "VT001", "soLuong": 2 },
    { "maVatTu": "VT002", "soLuong": 5 }
  ]
}
```

### 💡 Quy tắc thao tác từ FE:
1. **Giữ nguyên vật tư:** Gửi lại dòng vật tư đó trong danh sách `vatTuList`.
2. **Sửa số lượng vật tư:** Gửi lại dòng đó với `soLuong` mới.
3. **Thêm mới vật tư:** Push item mới vào mảng `vatTuList`.
4. **Xóa vật tư:** Loại bỏ dòng đó khỏi mảng `vatTuList` khi gửi lên.
5. **Xóa toàn bộ vật tư:** Gửi `vatTuList: []` (mảng rỗng).
6. **Không đụng tới vật tư:** Không truyền field `vatTuList` (hoặc gửi `null`).

---

## 3. 🔄 CẬP NHẬT LUỒNG TOGGLE TRẠNG THÁI CLS (`TrangThaiCLS`)

Khi Bác sĩ toggle trạng thái CLS từ **Đã làm (1)** về **Chưa làm (0)**:
- Backend sẽ **tự động xóa kết quả cũ** (`ketQua = null`).
- FE hiển thị lại ô nhập kết quả ở trạng thái trống/chưa thực hiện.

```json
{
  "trangThaiCLS": [
    {
      "maChiTiet": 45,
      "daLamCLS": false // Toggle về chưa làm -> ketQua tự động reset null
    }
  ]
}
```

---

## 🛠️ CHECKLIST DÀNH CHO FE-ER

- [ ] **Chức năng Xóa CLS:**
  - Nút "Xóa" chỉ xuất hiện/enable khi item CLS đang ở `trangThaiCLS == 0` và chưa có `ketQua`.
  - Gọi API `DELETE api/KhamBenh/{maPhieu}/chi-dinh-cls/{maChiTiet}`.
  - Cập nhật lại state phiếu khám từ response `data` trả về.
- [ ] **Chức năng Kê Vật Tư:**
  - Cập nhật hàm Submit Vật tư: Luôn gửi **toàn bộ danh sách vật tư** đang hiển thị trên UI thay vì chỉ gửi item mới.
  - Hỗ trợ nút Xóa dòng vật tư trên giao diện UI (bằng cách xoá khỏi state mảng và gọi PUT).
