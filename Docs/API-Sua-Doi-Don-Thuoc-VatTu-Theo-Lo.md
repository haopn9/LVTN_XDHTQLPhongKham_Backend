# ĐẶC TẢ — ĐỒNG BỘ CƠ CHẾ HOÀN/TRỪ KHO CHÍNH XÁC THEO LÔ
## Áp dụng cho Đơn Thuốc & Kê Vật Tư (PUT api/KhamBenh/{maPhieu})

Module: KhamBenh (Khám bệnh)
Controller: `KhamBenhController`
Ngày viết: 23-07-2026

====================================================================

## 1. Mục đích & bối cảnh

Hiện tại thuốc và vật tư đang chạy 2 cơ chế khác nhau:

| | Thuốc (hiện tại) | Vật tư (hiện tại) |
|---|---|---|
| Contract API | REPLACE (gửi toàn bộ danh sách) | Cộng dồn (chỉ gửi phần thêm mới) |
| Bug | Trừ kho 2 lần cho phần thuốc giữ nguyên (không hoàn trước khi trừ lại) | Không hỗ trợ sửa/xóa dòng đã kê |
| Hoàn kho khi sửa/xóa | Không có (chưa hỗ trợ) | Không áp dụng được |

Đặc tả này giải quyết đồng thời 2 vấn đề:
1. **Đồng bộ 1 cơ chế duy nhất** cho cả thuốc và vật tư (REPLACE semantics)
2. **Hoàn/trừ kho chính xác 100% theo đúng lô** đã bị tác động — không dùng phương án "hoàn gần đúng vào lô hiện tại"

**Thay đổi ảnh hưởng FE:** trường `VatTuList` đổi từ "chỉ gửi phần thêm mới" sang "gửi toàn bộ danh sách vật tư hiện tại của phiếu" — giống hệt cách `DonThuoc` đang hoạt động. Cần thông báo cho FE trước khi triển khai.

====================================================================

## 2. Thiết kế Database — bảng breakdown theo lô

Giữ nguyên 100% cấu trúc `ChiTietDonThuoc` và `ChiTietVatTuPhieuKham` hiện có (không đổi PK, không đổi response trả về FE). Thêm 2 bảng mới, chỉ dùng nội bộ backend:

```sql
-- Breakdown trừ kho theo lô cho từng dòng thuốc đã kê
CREATE TABLE ChiTietDonThuoc_Lo (
    MaDonThuoc   VARCHAR(20)  NOT NULL,
    MaThuoc      VARCHAR(20)  NOT NULL,
    MaLo         VARCHAR(50)  NOT NULL,   -- sửa 20 → 50 cho khớp LoThuoc.MaLo
    SoLuongTru   INT          NOT NULL,
    CONSTRAINT PK_ChiTietDonThuoc_Lo PRIMARY KEY (MaDonThuoc, MaThuoc, MaLo),
    CONSTRAINT FK_CTDTLo_ChiTietDonThuoc FOREIGN KEY (MaDonThuoc, MaThuoc)
        REFERENCES ChiTietDonThuoc(MaDonThuoc, MaThuoc),
    CONSTRAINT FK_CTDTLo_LoThuoc FOREIGN KEY (MaLo)
        REFERENCES LoThuoc(MaLo)
);

-- Breakdown trừ kho theo lô cho từng dòng vật tư đã kê
CREATE TABLE ChiTietVatTu_Lo (
    MaPhieu      VARCHAR(20)  NOT NULL,
    MaVatTu      VARCHAR(20)  NOT NULL,
    MaLo         VARCHAR(50)  NOT NULL,   -- sửa 20 → 50 cho khớp LoVatTu.MaLo
    SoLuongTru   INT          NOT NULL,
    CONSTRAINT PK_ChiTietVatTu_Lo PRIMARY KEY (MaPhieu, MaVatTu, MaLo),
    CONSTRAINT FK_CTVTLo_ChiTietVatTu FOREIGN KEY (MaPhieu, MaVatTu)
        REFERENCES ChiTietVatTuPhieuKham(MaPhieu, MaVatTu),
    CONSTRAINT FK_CTVTLo_LoVatTu FOREIGN KEY (MaLo)
        REFERENCES LoVatTu(MaLo)
);
```

> Sau khi thêm bảng, chạy `Scaffold-DbContext -Force` để cập nhật model Database-First.

> **Lưu ý dữ liệu cũ**: các dòng `ChiTietDonThuoc`/`ChiTietVatTuPhieuKham` đã tồn tại trước khi có 2 bảng này sẽ không có breakdown tương ứng → không thể hoàn chính xác nếu bị sửa/xóa. Nên xóa dữ liệu demo cũ và kê lại từ đầu sau khi triển khai, tránh gặp lỗi thiếu breakdown khi test.

====================================================================

## 3. Nguyên tắc xử lý chung (áp dụng như nhau cho thuốc và vật tư)

```
KÊ MỚI (dòng chưa từng tồn tại trong đơn/phiếu):
  1. Trừ kho FEFO như hiện tại (sắp HanSuDung ASC, trừ dần từng lô)
  2. Với mỗi lô bị trừ trong bước 1 → INSERT vào bảng breakdown:
     (Ma..., MaLo, SoLuongTru)
  3. INSERT dòng aggregate (ChiTietDonThuoc / ChiTietVatTuPhieuKham) như hiện tại

XÓA (dòng không còn xuất hiện trong request mới):
  1. Đọc toàn bộ breakdown rows của dòng đó
  2. Với mỗi (MaLo, SoLuongTru) → HOÀN CHÍNH XÁC: LoThuoc/LoVatTu.SoLuongTon += SoLuongTru
  3. XÓA breakdown rows
  4. XÓA dòng aggregate

SỬA SỐ LƯỢNG (dòng có mặt ở cả đơn cũ và request mới, SoLuong khác nhau):
  = THỰC HIỆN Y HỆT "XÓA" (bước 1-3 ở trên, hoàn hết theo breakdown cũ)
  + THỰC HIỆN Y HỆT "KÊ MỚI" (trừ FEFO lại từ đầu theo SoLuong MỚI, ghi breakdown mới)
  → Không tính delta tăng/giảm trực tiếp — luôn hoàn hết rồi trừ lại từ đầu theo
    tồn kho thực tế tại thời điểm sửa, tránh sai lệch khi tồn kho đã biến động
    giữa 2 lần lưu (nhập thêm lô mới, lô cũ hết hạn...).

GIỮ NGUYÊN (dòng có mặt ở cả 2, SoLuong không đổi):
  → Không làm gì cả (không hoàn, không trừ) — đây chính là điểm sửa bug so với
    code thuốc hiện tại (đang trừ kho lại dù SoLuong không đổi).
```

**Ràng buộc không đổi**: dòng thuốc đã `TrangThaiPhatThuoc = true` (đã phát) — **không được sửa/xóa** dưới bất kỳ hình thức nào, loại khỏi toàn bộ luồng trên (giữ nguyên rule hiện có).

====================================================================

## 4. Luồng xử lý chi tiết — Nhóm 4 (Đơn thuốc) trong PUT hiện có

Thay thế đoạn xử lý `request.DonThuoc` hiện tại (dòng 500–583 code cũ) bằng:

```
BƯỚC 1 — Validate đầu vào (giữ nguyên logic hiện có):
   - MaThuoc không rỗng, SoLuong > 0, CachDung không rỗng
   - Không trùng mã thuốc trong cùng request
   - Chặn nếu trùng thuốc đã phát (TrangThaiPhatThuoc = true)

BƯỚC 2 — Phân loại các dòng chưa phát hiện có so với request mới:
   - dongBiXoa    = có trong DB (chưa phát) nhưng KHÔNG có trong request.DonThuoc
   - dongGiuNguyen = có ở cả 2, SoLuong KHÔNG đổi
   - dongCanSua   = có ở cả 2, SoLuong CÓ đổi
   - dongMoi      = có trong request nhưng KHÔNG có trong DB

BƯỚC 3 — Hoàn kho cho (dongBiXoa + dongCanSua):
   Với mỗi dòng trong 2 nhóm này:
     - Đọc ChiTietDonThuoc_Lo theo (MaDonThuoc, MaThuoc)
     - Hoàn SoLuongTru vào đúng LoThuoc.MaLo tương ứng
     - Xóa các dòng ChiTietDonThuoc_Lo này
   Xóa dòng ChiTietDonThuoc của (dongBiXoa + dongCanSua) khỏi DB

BƯỚC 4 — Kiểm tra tồn kho khả dụng cho (dongCanSua + dongMoi):
   Giống logic hiện có (tính tonKhaDung, chặn 409 nếu không đủ) — nhưng
   giờ tính TRÊN TỒN KHO ĐÃ HOÀN Ở BƯỚC 3, nên không còn sai lệch.

BƯỚC 5 — Trừ FEFO + ghi breakdown cho (dongCanSua + dongMoi):
   Với mỗi dòng:
     - Trừ FEFO như logic hiện có (sắp HanSuDung ASC)
     - INSERT ChiTietDonThuoc_Lo cho từng lô bị trừ
     - INSERT ChiTietDonThuoc (dòng aggregate)

BƯỚC 6 — dongGiuNguyen: không làm gì, giữ nguyên dòng cũ và breakdown cũ.

BƯỚC 7 — SaveChangesAsync
```

====================================================================

## 5. Luồng xử lý chi tiết — Nhóm 4b (Kê vật tư) trong PUT hiện có

**Thay đổi mô tả field:**

```csharp
// Nhóm 4b: Kê vật tư (REPLACE toàn bộ danh sách — đổi từ cộng dồn)
// Chỉ áp dụng cho phiếu có CLS
public List<VatTuItem>? VatTuList { get; set; }
```

Luồng xử lý **giống hệt mục 4 ở trên**, chỉ đổi tên bảng/thực thể:

```
BƯỚC 1 — Validate đầu vào (giữ nguyên logic hiện có):
   - MaVatTu không rỗng, SoLuong > 0
   - Không trùng mã vật tư trong cùng request
   - Chặn nếu phiếu chưa có chỉ định CLS nào (giữ nguyên rule hiện có)

BƯỚC 2 — Phân loại (so ChiTietVatTuPhieuKham hiện có với request.VatTuList):
   - dongBiXoa / dongGiuNguyen / dongCanSua / dongMoi (như mục 3)

BƯỚC 3 — Hoàn kho cho (dongBiXoa + dongCanSua):
   - Đọc ChiTietVatTu_Lo theo (MaPhieu, MaVatTu)
   - Hoàn SoLuongTru vào đúng LoVatTu.MaLo tương ứng
   - Xóa breakdown rows + xóa dòng ChiTietVatTuPhieuKham tương ứng

BƯỚC 4 — Kiểm tra tồn kho khả dụng cho (dongCanSua + dongMoi) — như hiện có

BƯỚC 5 — Trừ FEFO + ghi breakdown cho (dongCanSua + dongMoi):
   - Trừ FEFO như logic hiện có
   - INSERT ChiTietVatTu_Lo cho từng lô bị trừ
   - INSERT/cập nhật ChiTietVatTuPhieuKham
     (DonGia = GiaBan của lô đầu tiên bị trừ trong LẦN TRỪ MỚI — không đổi
      cách tính giá hiện có, vẫn giữ nguyên quyết định trước đó)

BƯỚC 6 — dongGiuNguyen: không làm gì.

BƯỚC 7 — SaveChangesAsync
```

**Không có ràng buộc "vật tư đã phát" như thuốc** (vật tư không có khái niệm phát) — nên toàn bộ dòng vật tư của phiếu (miễn phiếu chưa Hoàn thành) đều thuộc diện được sửa/xóa qua PUT này.

====================================================================

## 6. Ví dụ minh họa (số liệu cụ thể)

**Trạng thái kho Paracetamol trước khi kê:**
Lô A (hạn gần nhất): tồn 8 · Lô B: tồn 20

**Lần lưu 1** — `DonThuoc = [{MaThuoc: "PARA01", SoLuong: 10}]`
→ Trừ: Lô A 8→0, Lô B 20→18
→ Breakdown: `(PARA01, LôA, 8)`, `(PARA01, LôB, 2)`

**Lần lưu 2** — bác sĩ sửa còn 6 viên: `DonThuoc = [{MaThuoc: "PARA01", SoLuong: 6}]`
→ Phân loại: `dongCanSua = [PARA01]` (SoLuong 10→6, có đổi)
→ Bước 3: hoàn theo breakdown cũ → Lô A 0→8, Lô B 18→20 (về đúng ban đầu)
→ Bước 5: trừ FEFO lại theo SoLuong=6 mới → Lô A 8→2, Lô B giữ 20
→ Breakdown mới: `(PARA01, LôA, 6)` (breakdown cũ đã bị xóa ở bước 3)

**Lần lưu 3** — bác sĩ xóa hẳn dòng Paracetamol: `DonThuoc = []`
→ Phân loại: `dongBiXoa = [PARA01]`
→ Hoàn theo breakdown: Lô A 2→8 ✅ — về đúng số ban đầu tuyệt đối, không sai số.

--------------------------------------------------------------------

### 6.1. Ví dụ số lượng lớn hơn — trừ rải trên 3 lô cùng lúc

Cơ chế **không giới hạn số lô bị chạm** trong 1 lần trừ — breakdown ghi lại đúng bấy nhiêu dòng tương ứng số lô đã bị trừ, dù là 2, 3 hay nhiều hơn.

**Trạng thái kho Panadol trước khi kê (3 lô, sắp theo hạn dùng gần → xa):**
Lô A: tồn 3 · Lô B: tồn 7 · Lô C: tồn 100

**Kê Panadol x10** → FEFO trừ tuần tự theo hạn dùng gần nhất trước:
- Lô A: 3 → **0** (trừ hết 3, còn thiếu 7)
- Lô B: 7 → **0** (trừ hết 7, còn thiếu 0 — vừa đủ)
- Lô C: không bị đụng tới (đã đủ 10 từ Lô A + Lô B)

**Breakdown ghi lại (2 dòng, vì chỉ 2 lô bị chạm dù kho có 3 lô):**
```
(Panadol, LôA, SoLuongTru=3)
(Panadol, LôB, SoLuongTru=7)
```

**Nếu bác sĩ xóa dòng này:**
→ Đọc breakdown, thấy 2 dòng → hoàn đúng 2 lô:
- Lô A: 0 → 3 ✅
- Lô B: 0 → 7 ✅
- Lô C: không đổi (vốn dĩ không bị đụng tới, không cần hoàn)

**Nếu bác sĩ sửa số lượng từ 10 xuống 12 (tăng thêm 2), giả sử không có lô mới nhập vào giữa 2 lần lưu:**
→ Bước A (hoàn hết theo breakdown cũ): Lô A 0→3, Lô B 0→7 (Lô C giữ nguyên 100)
→ Bước B (trừ FEFO lại theo số lượng mới = 12): Lô A 3→0, Lô B 7→0, Lô C 100→98 (trừ tiếp 2 vì A+B chỉ đủ 10/12)
→ Breakdown mới (3 dòng, vì lần này chạm luôn cả Lô C):
```
(Panadol, LôA, 3)
(Panadol, LôB, 7)
(Panadol, LôC, 2)
```

→ Kết luận: số dòng breakdown **thay đổi linh hoạt theo từng lần lưu**, tùy số lượng kê và tồn kho thực tế tại thời điểm đó — cơ chế hoàn/trừ vẫn đúng 100% trong mọi trường hợp vì luôn dựa vào breakdown ghi nhận đúng lần gần nhất, không phụ thuộc số lô cố định là 1 hay 2.

### 6.2. Bảng tổng hợp quy tắc (dùng để trình bày nhanh)

| Thao tác | Bước 1 | Bước 2 |
|---|---|---|
| Kê mới | — | Trừ FEFO theo số lượng mới + ghi breakdown |
| Xóa | Hoàn toàn bộ theo breakdown cũ (từng lô, đúng số) | Xóa breakdown + xóa dòng aggregate |
| Sửa số lượng | Hoàn toàn bộ theo breakdown cũ (từng lô, đúng số) | Trừ FEFO lại theo số lượng mới + ghi breakdown mới |
| Giữ nguyên | — | Không làm gì |

Quy tắc bất biến: **số dòng breakdown hoàn = đúng số dòng breakdown đã ghi lúc trừ**, không quan tâm là 1, 2 hay nhiều lô — không có trường hợp ngoại lệ nào cần xử lý riêng.

====================================================================

## 7. Response

Không đổi format response hiện có (`TaoResponsePhieuKham`) — bảng breakdown là nội bộ, FE không cần biết và không hiển thị.

Bổ sung 1 mã lỗi mới (nếu cần) khi hoàn kho phát hiện breakdown thiếu (dữ liệu cũ trước khi có bảng `_Lo`):

```json
{ "message": "Không thể xác định lô đã trừ cho dòng kê này. Vui lòng liên hệ quản trị viên để xử lý thủ công" }
```
→ HTTP 500, kèm log chi tiết `MaThuoc`/`MaVatTu` + `MaPhieu` để tra cứu thủ công.

====================================================================

## 8. Checklist triển khai

- [ ] Tạo 2 bảng `ChiTietDonThuoc_Lo`, `ChiTietVatTu_Lo` + FK
- [ ] Scaffold-DbContext -Force cập nhật model
- [ ] Xóa dữ liệu demo cũ (không có breakdown) trước khi test lại từ đầu
- [ ] Sửa Nhóm 4 (Đơn thuốc): thêm bước phân loại + hoàn theo breakdown + ghi breakdown mới
- [ ] Sửa Nhóm 4b (Vật tư): đổi từ cộng dồn sang REPLACE, áp dụng luồng giống thuốc
- [ ] Thông báo FE: `VatTuList` đổi contract — gửi toàn bộ danh sách mỗi lần lưu
- [ ] Thêm `UPDLOCK`/`HOLDLOCK` khi hoàn kho (tránh race condition giống lúc trừ kho)
- [ ] Viết test case:
  - Kê mới → breakdown đúng theo FEFO
  - Xóa dòng → tồn kho về đúng số ban đầu (test cả trường hợp trừ rải nhiều lô)
  - Sửa tăng số lượng → hoàn cũ + trừ mới đúng
  - Sửa giảm số lượng → hoàn cũ + trừ mới đúng
  - Giữ nguyên số lượng → tồn kho KHÔNG đổi (test bug double-deduct đã sửa)
  - Thuốc đã phát (`TrangThaiPhatThuoc = true`) → không bị đụng vào dù request không gửi lại
  - Vật tư: đủ tồn kho vs không đủ tồn kho khi sửa tăng số lượng
