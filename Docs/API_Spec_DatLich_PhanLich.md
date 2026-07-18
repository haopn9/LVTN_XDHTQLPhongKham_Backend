# SPEC API — PHÂN HỆ ĐẶT LỊCH HẸN & PHÂN LỊCH KHÁM BÁC SĨ

**Phiên bản:** 1.0
**Ngày tạo:** 17/07/2026
**Căn cứ:** `NghiepVu_DatLich_PhanLich.md` (đã chốt cùng nhóm) + các quyết định bổ sung trong buổi review API spec.

---

## 0. THAY ĐỔI SCHEMA CƠ SỞ DỮ LIỆU

### 0.1. Bảng mới: `LichLamViec`

```sql
CREATE TABLE [dbo].[LichLamViec](
    [MaLich]        [int] IDENTITY(1,1) NOT NULL,
    [MaNV]          [varchar](20) NOT NULL,
    [NgayLamViec]   [date] NOT NULL,
    [CaLamViec]     [varchar](10) NOT NULL,   -- 'Sang' | 'Chieu'
    [PhongKham]     [nvarchar](100) NULL,
    [GhiChu]        [nvarchar](255) NULL,
    [NgayDangKy]    [datetime] NOT NULL DEFAULT GETDATE(),
    PRIMARY KEY CLUSTERED ([MaLich] ASC),
    CONSTRAINT FK_LichLamViec_NhanVien FOREIGN KEY (MaNV) REFERENCES NhanVien(MaNV),
    CONSTRAINT CK_LichLamViec_Ca CHECK (CaLamViec IN ('Sang','Chieu')),
    CONSTRAINT UQ_LichLamViec_BacSi_Ngay_Ca UNIQUE (MaNV, NgayLamViec, CaLamViec)
) ON [PRIMARY]
GO
```

> Ghi chú: không lưu `MaKhoa` trong bảng này (tránh trùng lặp dữ liệu) — khoa của bác sĩ được suy ra qua `JOIN NhanVien ON LichLamViec.MaNV = NhanVien.MaNV`.
> `PhongKham` giữ dạng free-text (chỉ mang tính hiển thị, không dùng làm điều kiện nghiệp vụ) theo thống nhất.

### 0.2. Cập nhật bảng `DatLichKham`

```sql
ALTER TABLE [dbo].[DatLichKham] ADD
    [MaNV]  [varchar](20) NULL,
    [CaHen] [varchar](10) NULL;   -- 'Sang' | 'Chieu'

ALTER TABLE [dbo].[DatLichKham] ADD CONSTRAINT
    FK_DatLichKham_NhanVien FOREIGN KEY (MaNV) REFERENCES NhanVien(MaNV);

ALTER TABLE [dbo].[DatLichKham] ADD CONSTRAINT
    CK_DatLichKham_Ca CHECK (CaHen IS NULL OR CaHen IN ('Sang','Chieu'));

ALTER TABLE [dbo].[DatLichKham] ADD CONSTRAINT
    CK_DatLichKham_TrangThai CHECK (TrangThai IN ('ChoXacNhan','DaXacNhan','DaTiepNhan','DaHuy'));
```

> **Không** bổ sung GioiTinh/NgaySinh/DiaChi/TienSuBenh vào bảng này (theo quyết định của nhóm) — các trường này sẽ được lễ tân nhập trực tiếp khi tạo `BenhNhan` mới ở bước Tiếp nhận nếu không tìm thấy hồ sơ trùng SDT.

### 0.3. Chuẩn hoá enum ca làm việc/ca hẹn

Toàn hệ thống dùng chung 2 giá trị chuỗi cố định, không dấu, để so khớp ổn định giữa `LichLamViec.CaLamViec` và `DatLichKham.CaHen`:

| Giá trị lưu DB | Hiển thị FE | Khung giờ |
|---|---|---|
| `Sang` | Ca Sáng | 07:30 – 11:30 |
| `Chieu` | Ca Chiều | 13:30 – 17:00 |

Backend expose qua enum C#:
```csharp
public enum CaKham { Sang, Chieu }
```

---

## 1. MODULE: PHÂN LỊCH LÀM VIỆC BÁC SĨ

### 1.1. `POST /api/LichLamViec` — Bác sĩ tự đăng ký ca trực

**Mô tả:** Bác sĩ đăng nhập tự đăng ký ca trực của chính mình.

**Phân quyền:** `[Authorize(Roles = "BacSi")]`

**Input:**
```json
{
  "ngayLamViec": "2026-07-25",
  "caLamViec": "Sang",
  "phongKham": "Phòng 102 - Răng Hàm Mặt",
  "ghiChu": "Khám định kỳ"
}
```
- `maNV` **không** truyền từ client — lấy từ claims của JWT (tránh bác sĩ đăng ký hộ người khác).
- `phongKham`, `ghiChu`: optional.

**Quy trình xử lý:**
1. Validate `ngayLamViec` không phải ngày trong quá khứ (`>= ngày hiện tại`).
2. Validate `caLamViec` thuộc `{Sang, Chieu}`.
3. Lấy `MaKhoa` của bác sĩ hiện tại từ `NhanVien`. Nếu `MaKhoa` là NULL → trả lỗi (bác sĩ chưa được gán khoa, không thể tự đăng ký).
4. Trong 1 transaction, dùng `WITH (UPDLOCK, HOLDLOCK)` khi truy vấn kiểm tra (tránh race condition khi 2 bác sĩ cùng khoa đăng ký đồng thời):
   - **Check trùng lịch của chính bác sĩ:** đã tồn tại bản ghi `(MaNV, NgayLamViec, CaLamViec)` → lỗi 409.
   - **Check giới hạn 3 ca/tuần:** đếm số bản ghi của bác sĩ này có `NgayLamViec` nằm trong tuần dương lịch (Thứ 2 → Chủ Nhật) chứa `ngayLamViec` đang đăng ký. Nếu đã có ≥ 3 → lỗi 409.
   - **Check cùng khoa trùng ca (1 khoa = tối đa 1 bác sĩ/ca):** join `LichLamViec` với `NhanVien` để tìm bản ghi khác có cùng `MaKhoa`, cùng `NgayLamViec`, cùng `CaLamViec` → nếu tồn tại → lỗi 409, kèm tên bác sĩ đã đăng ký trước để hiển thị cho người dùng.
5. Insert bản ghi mới vào `LichLamViec`.
6. Commit transaction.

**Output (201 Created):**
```json
{
  "maLich": 45,
  "maNV": "NV003",
  "ngayLamViec": "2026-07-25",
  "caLamViec": "Sang",
  "phongKham": "Phòng 102 - Răng Hàm Mặt",
  "ghiChu": "Khám định kỳ",
  "ngayDangKy": "2026-07-17T10:20:00"
}
```

**Ràng buộc:**
- Mỗi bác sĩ tối đa 3 ca/tuần (tuần Thứ 2 → CN).
- Mỗi khoa tối đa 1 bác sĩ trực/ca/ngày.
- Bác sĩ **chỉ được thêm mới**, không có API sửa/xoá cho vai trò BacSi (theo quyết định của nhóm).

**Mã lỗi:**
| HTTP | Trường hợp |
|---|---|
| 400 | Ngày trong quá khứ / ca không hợp lệ / thiếu MaKhoa |
| 401/403 | Không có quyền (không phải BacSi) |
| 409 | Trùng ca của chính bác sĩ / vượt giới hạn 3 ca/tuần / trùng khoa-ca |

---

### 1.2. `GET /api/LichLamViec` — Xem lịch trực (nội bộ)

**Mô tả:** Hiển thị công khai lịch trực cho các bác sĩ/nhân viên trong hệ thống tham khảo trước khi tự đăng ký, và cho Admin/quản lý giám sát.

**Phân quyền:** `[Authorize]` (mọi role đã đăng nhập)

**Input (query params):**
| Tên | Kiểu | Bắt buộc | Mô tả |
|---|---|---|---|
| `tuNgay` | date | Không | Mặc định = đầu tuần hiện tại |
| `denNgay` | date | Không | Mặc định = cuối tuần hiện tại |
| `maKhoa` | string | Không | Lọc theo khoa |
| `maNV` | string | Không | Lọc theo bác sĩ |

**Quy trình xử lý:**
1. Query `LichLamViec` JOIN `NhanVien` (lấy `HoTen`, `MaKhoa`, `ChuyenMon`) theo khoảng ngày và các filter.
2. Sắp xếp theo `NgayLamViec`, `CaLamViec`.
3. Áp dụng `AsNoTracking()`, có phân trang nếu khoảng ngày dài (mặc định trả toàn bộ nếu ≤ 31 ngày).

**Output (200 OK):**
```json
[
  {
    "maLich": 45,
    "maNV": "NV003",
    "tenBacSi": "BS. CK2. Trần Thị Bình",
    "maKhoa": "K02",
    "tenKhoa": "Tim mạch",
    "ngayLamViec": "2026-07-25",
    "caLamViec": "Sang",
    "phongKham": "Phòng 102 - Răng Hàm Mặt",
    "ghiChu": "Khám định kỳ"
  }
]
```

**Mã lỗi:**
| HTTP | Trường hợp |
|---|---|
| 400 | `tuNgay` > `denNgay` |
| 401 | Chưa đăng nhập |

---

### 1.3. `DELETE /api/LichLamViec/{maLich}` — Admin xoá/hiệu chỉnh ca trực *(bổ sung ngoài phạm vi nghiệp vụ gốc, đã được nhóm thống nhất bổ sung)*

> Endpoint này không nằm trong tài liệu nghiệp vụ gốc — bổ sung để xử lý trường hợp bác sĩ đăng ký nhầm hoặc nghỉ đột xuất, phù hợp với vai trò "Admin giám sát tiến trình" đã nêu trong nghiệp vụ.

**Phân quyền:** `[Authorize(Roles = "Admin")]`

**Quy trình xử lý:**
1. Kiểm tra `MaLich` tồn tại.
2. Kiểm tra `NgayLamViec` chưa qua (không cho xoá ca đã diễn ra, chỉ để giữ lịch sử).
3. Xoá cứng bản ghi (bảng này không liên kết dữ liệu khám bệnh nên không cần soft-delete).

**Output:** `204 No Content`

---

## 2. MODULE: ĐẶT LỊCH HẸN KHÁM (`DatLichKham`)

### 2.1. `GET /api/DanhSach/bac-si-lich-trong` — Tra cứu bác sĩ có ca trống theo ngày + khoa *(public, phục vụ Bước 1)*

**Mô tả:** Cổng đặt lịch dùng API này để khách chọn Ngày + Chuyên khoa → hiển thị danh sách bác sĩ có ca trực trống trong ngày đó (dữ liệu lấy từ `LichLamViec`). Bổ sung action mới vào `DanhSachController` đã có sẵn (`api/DanhSach/bac-si`), giữ đúng nguyên tắc "API danh sách bác sĩ dùng chung" đã thống nhất trước đây.

**Phân quyền:** `[AllowAnonymous]`

**Input (query params):**
| Tên | Kiểu | Bắt buộc |
|---|---|---|
| `ngayHen` | date | Có |
| `maKhoa` | string | Có |
| `caHen` | string (`Sang`/`Chieu`) | Không (nếu không truyền → trả cả 2 ca) |

**Quy trình xử lý:**
1. Validate `ngayHen >= ngày hiện tại`.
2. Query `LichLamViec` JOIN `NhanVien` theo `MaKhoa`, `NgayLamViec = ngayHen`, và `CaLamViec` (nếu có filter).
3. Trả về danh sách bác sĩ kèm ca trực, phòng khám.

**Output (200 OK):**
```json
[
  {
    "maNV": "NV003",
    "hoTen": "BS. CK2. Trần Thị Bình",
    "chuyenMon": "Tim mạch",
    "caLamViec": "Sang",
    "phongKham": "Phòng 102 - Răng Hàm Mặt"
  }
]
```

---

### 2.2. `POST /api/DatLichKham` — Khách tạo lịch hẹn khám (public)

**Mô tả:** Bước 2–3 của nghiệp vụ: khách gửi thông tin đặt lịch, hệ thống tạo bản ghi trạng thái `ChoXacNhan`.

**Phân quyền:** `[AllowAnonymous]`

**Input:**
```json
{
  "hoTenKhach": "Nguyễn Văn A",
  "sdt": "0901234567",
  "ngayHen": "2026-07-25",
  "caHen": "Sang",
  "yeuCauKham": "Đau họng, ho kéo dài",
  "maNV": "NV003"
}
```
- `maNV`: optional (khách có thể không chọn bác sĩ cụ thể → NULL).

**Quy trình xử lý:**
1. Validate bắt buộc: `hoTenKhach`, `sdt` (regex số điện thoại VN), `ngayHen` (>= hôm nay), `caHen` thuộc `{Sang, Chieu}`.
2. Nếu `maNV` được truyền:
   - Kiểm tra bác sĩ tồn tại và **có ca trực đúng `ngayHen` + `caHen`** trong `LichLamViec` → nếu không có, trả lỗi 409 ("Bác sĩ không có lịch trực vào thời gian này").
3. Insert bản ghi mới vào `DatLichKham` với `TrangThai = 'ChoXacNhan'`.
4. Trả về mã đặt lịch cho khách để tra cứu sau này.

**Output (201 Created):**
```json
{
  "maDatLich": 128,
  "trangThai": "ChoXacNhan",
  "ngayHen": "2026-07-25",
  "caHen": "Sang"
}
```

**Mã lỗi:**
| HTTP | Trường hợp |
|---|---|
| 400 | Thiếu trường bắt buộc / sai định dạng SDT / ngày quá khứ |
| 409 | Bác sĩ được chọn không có ca trực đúng ngày/ca yêu cầu |

---

### 2.3. `GET /api/DatLichKham` — Danh sách lịch hẹn (nội bộ, lễ tân)

**Phân quyền:** `[Authorize(Roles = "LeTan,Admin")]`

**Input (query params):** `trangThai`, `ngayHen`, `search` (theo tên/SDT), `page`, `pageSize` (bắt buộc phân trang, tránh load toàn bộ bảng).

**Quy trình xử lý:**
1. Query có `AsNoTracking()`, filter theo tham số.
2. Với `search`: dùng `LIKE` trên `HoTenKhach`/`SDT` — lưu ý tránh non-sargable search (không bọc hàm lên cột trong `WHERE`, cho phép index hoạt động).
3. Sắp xếp mặc định: `NgayHen ASC`, ưu tiên `TrangThai = 'ChoXacNhan'` lên đầu.

**Output (200 OK):** danh sách phân trang, mỗi item gồm đầy đủ field + `tenBacSi` (nếu có `MaNV`).

---

### 2.4. `PUT /api/DatLichKham/{maDatLich}/xac-nhan` — Lễ tân xác nhận lịch hẹn

**Mô tả:** Bước 4 — sau khi gọi điện xác minh thành công với khách.

**Phân quyền:** `[Authorize(Roles = "LeTan,Admin")]`

**Quy trình xử lý:**
1. Kiểm tra `MaDatLich` tồn tại và `TrangThai = 'ChoXacNhan'` (nếu khác → lỗi 409, không cho xác nhận lịch đã hủy/đã tiếp nhận).
2. Cập nhật `TrangThai = 'DaXacNhan'`.

**Output:** `200 OK` — bản ghi sau cập nhật.

**Mã lỗi:**
| HTTP | Trường hợp |
|---|---|
| 404 | Không tìm thấy MaDatLich |
| 409 | TrangThai hiện tại không phải `ChoXacNhan` |

---

### 2.5. `PUT /api/DatLichKham/{maDatLich}/huy` — Huỷ lịch hẹn

**Phân quyền:** `[Authorize(Roles = "LeTan,Admin")]`

**Quy trình xử lý:**
1. Kiểm tra `TrangThai` hiện tại phải là `ChoXacNhan` hoặc `DaXacNhan` (là trạng thái cuối, không thể hủy nếu đã `DaTiepNhan` hoặc đã `DaHuy` sẵn).
2. Cập nhật `TrangThai = 'DaHuy'`.

**Ràng buộc:** `DaHuy` là trạng thái **kết thúc, không thể đổi ngược**.

**Mã lỗi:**
| HTTP | Trường hợp |
|---|---|
| 409 | Lịch hẹn đã ở trạng thái `DaTiepNhan` hoặc `DaHuy` |

---

### 2.6. `POST /api/DatLichKham/{maDatLich}/tiep-nhan` — Chuyển đổi sang Tiếp nhận bệnh nhân

**Mô tả:** Bước 5 — khách đến đúng giờ hẹn, lễ tân bấm "Tiếp nhận". Hệ thống tự tạo `BenhNhan` (nếu chưa có) và tự sinh `PhieuKham`.

**Phân quyền:** `[Authorize(Roles = "LeTan,Admin")]`

**Input:**
```json
{
  "maBacSiChiDinh": "NV003",
  "benhNhan": {
    "maBN": null,
    "gioiTinh": "Nam",
    "ngaySinh": "1990-05-10",
    "diaChi": "123 Đường ABC, Q.1",
    "tienSuBenh": "Không"
  }
}
```
- `maBacSiChiDinh`: **bắt buộc** nếu `DatLichKham.MaNV` đang NULL (khách không chọn bác sĩ lúc đặt) — theo quy tắc bắt buộc gán bác sĩ lúc tiếp đón đã chốt trước đây. Nếu `DatLichKham.MaNV` đã có sẵn, trường này optional (mặc định dùng lại giá trị cũ, cho phép lễ tân đổi bác sĩ nếu cần).
- `benhNhan.maBN`: nếu lễ tân đã tự tra cứu và biết bệnh nhân đã tồn tại, truyền `maBN` để dùng lại (bỏ qua auto-match theo SDT). Nếu để `null`, hệ thống tự tìm theo SDT.
- Các trường `gioiTinh/ngaySinh/diaChi/tienSuBenh` chỉ bắt buộc khi hệ thống **không** tìm thấy `BenhNhan` trùng SDT (tức tạo mới).

**Quy trình xử lý (trong 1 transaction):**
1. Kiểm tra `MaDatLich` tồn tại, `TrangThai = 'DaXacNhan'` (chưa xác nhận thì chưa được tiếp nhận).
2. Xác định bác sĩ khám: dùng `maBacSiChiDinh` nếu có, ngược lại dùng `DatLichKham.MaNV`. Nếu cả 2 đều NULL → lỗi 400 ("Chưa chỉ định bác sĩ khám").
3. Xác định `BenhNhan`:
   - Nếu `benhNhan.maBN` được truyền → dùng trực tiếp (kiểm tra tồn tại).
   - Ngược lại, tìm `BenhNhan` có `SDT = DatLichKham.SDT`:
     - Tìm thấy đúng 1 → dùng `MaBN` đó.
     - Tìm thấy nhiều hơn 1 → trả lỗi 409, yêu cầu lễ tân tự chọn thủ công qua giao diện tra cứu (tránh tự động gán sai hồ sơ).
     - Không tìm thấy → tạo mới `BenhNhan` với `HoTen = DatLichKham.HoTenKhach`, `SDT = DatLichKham.SDT`, cùng `gioiTinh/ngaySinh/diaChi/tienSuBenh` từ input (bắt buộc phải có ở bước này).
4. Sinh `PhieuKham` mới: `MaBN`, `MaNV = <bác sĩ khám>`, `NgayKham = NOW()`, `LyDoKham = DatLichKham.YeuCauKham`, `TrangThaiKham = <trạng thái khởi tạo mặc định>`.
5. Cập nhật `DatLichKham.TrangThai = 'DaTiepNhan'`, `DatLichKham.MaNV = <bác sĩ khám>` (đồng bộ lại nếu lễ tân đổi bác sĩ).
6. Commit transaction.

**Output (201 Created):**
```json
{
  "maDatLich": 128,
  "maBN": "BN00045",
  "maPhieu": "PK00231",
  "maNV": "NV003",
  "trangThai": "DaTiepNhan"
}
```

**Ràng buộc:**
- Chỉ tiếp nhận được lịch hẹn ở trạng thái `DaXacNhan`.
- Bắt buộc phải xác định được bác sĩ khám trước khi tạo `PhieuKham`.
- Toàn bộ thao tác (tìm/tạo BenhNhan + tạo PhieuKham + cập nhật DatLichKham) nằm trong **1 transaction duy nhất** để đảm bảo tính toàn vẹn.

**Mã lỗi:**
| HTTP | Trường hợp |
|---|---|
| 400 | Không xác định được bác sĩ khám / thiếu thông tin BenhNhan mới khi cần tạo mới |
| 404 | Không tìm thấy MaDatLich |
| 409 | TrangThai khác `DaXacNhan` / tìm thấy nhiều hơn 1 BenhNhan trùng SDT |

---

## 3. TÓM TẮT DANH SÁCH ENDPOINT

| Method | Route | Vai trò | Ghi chú |
|---|---|---|---|
| POST | `/api/LichLamViec` | BacSi | Tự đăng ký ca trực |
| GET | `/api/LichLamViec` | Đã đăng nhập | Xem lịch trực |
| DELETE | `/api/LichLamViec/{maLich}` | Admin | Bổ sung ngoài nghiệp vụ gốc, đã chốt |
| GET | `/api/DanhSach/bac-si-lich-trong` | Public | Bước 1 đặt lịch |
| POST | `/api/DatLichKham` | Public | Bước 2–3 đặt lịch |
| GET | `/api/DatLichKham` | LeTan, Admin | Danh sách lịch hẹn |
| PUT | `/api/DatLichKham/{id}/xac-nhan` | LeTan, Admin | Bước 4 |
| PUT | `/api/DatLichKham/{id}/huy` | LeTan, Admin | Huỷ lịch |
| POST | `/api/DatLichKham/{id}/tiep-nhan` | LeTan, Admin | Bước 5 |

---

## 4. VIỆC CẦN LÀM Ở FRONTEND (để đồng bộ với spec này)

1. `CustomerPortal.jsx`: thêm bước chọn **Chuyên khoa** trước khi chọn bác sĩ, gọi `GET /api/DanhSach/bac-si-lich-trong` thay vì lấy toàn bộ danh sách bác sĩ qua `apiGetBacSiCongKhai()`.
2. Đổi tên field `caKham` → `caHen`, giá trị từ `'Sang'/'Chieu'` (đã đúng sẵn theo mock hiện tại, chỉ cần đảm bảo khớp enum chuẩn).
3. Bỏ các input `gioiTinh/ngaySinh/diaChi/tienSuBenh` khỏi form đặt lịch (theo quyết định không lưu ở `DatLichKham`) — các trường này sẽ được thu thập lại ở màn hình Tiếp nhận của lễ tân.
4. `LichPhongKham.jsx`: thay `localStorage` bằng gọi API thật (`POST/GET /api/LichLamViec`), bỏ logic check trùng theo `(maNV, ngày, ca)` đơn giản hiện tại, thay bằng xử lý lỗi 409 trả về từ backend (đã bao gồm cả check cùng-khoa và giới hạn 3 ca/tuần).
