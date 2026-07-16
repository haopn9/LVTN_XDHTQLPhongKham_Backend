**ĐẶC TẢ API**

**MODULE THANH TOÁN & HÓA ĐƠN VIỆN PHÍ**

_(ThanhToanController)_

Hệ thống Quản lý Phòng khám - QLPhongKham

Base route: api/ThanhToan

Vai trò sử dụng: Admin, Thu Ngân

Phiên bản 1.0 - Ngày 16/07/2026

# **1\. TỔNG QUAN MODULE**

Module Thanh toán & Hóa đơn phục vụ vai trò Thu Ngân (và Admin để giám sát/hỗ trợ) thực hiện lập hóa đơn viện phí cho các phiếu khám đã hoàn thành khám lâm sàng. Toàn bộ endpoint được đặt dưới base route sau:

api/ThanhToan

Dữ liệu đầu vào của module lấy từ 3 nhóm chi phí: dịch vụ cận lâm sàng (CLS) đã chỉ định, đơn thuốc đã kê, và vật tư y tế đã sử dụng/phát sinh. Bảng tổng hợp endpoint:

| **#** | **Endpoint**                             | **Method** | **Chức năng chính**                                               |
| ----- | ---------------------------------------- | ---------- | ----------------------------------------------------------------- |
| 1     | api/ThanhToan/danh-sach                  | GET        | Danh sách phiếu chờ/đã thanh toán, filter + tìm kiếm + phân trang |
| 2     | api/ThanhToan/{maPhieu}/chi-tiet         | GET        | Chi tiết chi phí của 1 phiếu khám (CLS, thuốc, vật tư, tổng tiền) |
| 3     | api/ThanhToan/{maPhieu}/vat-tu           | POST       | Kê thêm vật tư phát sinh vào phiếu (chưa thanh toán)              |
| 4     | api/ThanhToan/{maPhieu}/vat-tu/{maVatTu} | DELETE     | Xóa vật tư phát sinh khỏi phiếu (chưa thanh toán)                 |
| 5     | api/ThanhToan/xac-nhan                   | POST       | Xác nhận thanh toán, tạo hóa đơn (F12)                            |
| 6     | api/ThanhToan/{maHoaDon}/pdf             | GET        | Xuất file PDF hóa đơn đã lập (F8 / tải về)                        |

**MỚI - Toàn bộ ThanhToanController là controller mới, chưa tồn tại trong hệ thống trước đó.**

# **2\. THAY ĐỔI SCHEMA CẦN THỰC HIỆN**

Rà soát schema hiện có (QLPhongKham.sql) phát hiện bảng HoaDon chưa có cột lưu phương thức thanh toán, trong khi FE (ThanhToanHoaDon.jsx) đã có UI chọn Tiền mặt / Chuyển khoản. Cần bổ sung migration sau trước khi triển khai controller:

**CẦN MIGRATION**

ALTER TABLE \[dbo\].\[HoaDon\] ADD \[PhuongThucTT\] \[nvarchar\](20) NULL;

_Giá trị hợp lệ cho PhuongThucTT: N'Tiền mặt' hoặc N'Chuyển khoản' (kiểm tra ở tầng backend, không đặt CHECK constraint để tránh vướng khi cần mở rộng thêm phương thức sau này)._

Không cần thêm bảng hoặc cột nào khác. Giá thuốc và giá vật tư phát sinh được tính động (simulate) tại thời điểm truy vấn/thanh toán, không snapshot ngược vào ChiTietDonThuoc - xem chi tiết tại mục 3 và mục 6.

# **3\. SERVICE DÙNG CHUNG (SHARED BUSINESS LOGIC)**

Hai hàm dưới đây được dùng lại ở nhiều endpoint (4.2 và 4.5), nên tách thành service riêng (ví dụ IGiaThuocVatTuService), không viết lặp trong từng action của controller.

## **3.1. GetGiaThuocFEFO(maThuoc, soLuongCan)**

Mục đích: mô phỏng đơn giá một loại thuốc tại thời điểm hiện tại theo nguyên tắc xuất kho FEFO (First-Expired-First-Out), dùng để tính tiền thuốc trên hóa đơn.

### **Quy trình xử lý:**

- Truy vấn LoThuoc WHERE MaThuoc = maThuoc AND SoLuongTon > 0 AND HanSuDung >= GETDATE(), ORDER BY HanSuDung ASC.
- Duyệt tuần tự từng lô, cộng dồn SoLuongTon cho đến khi đủ soLuongCan; với mỗi lô lấy được, ghi nhận (soLuongLayTuLo, GiaBan).
- Đơn giá trả về = bình quân gia quyền = Σ(soLuongLayTuLo × GiaBan lô) / soLuongCan.
- Nếu tổng tồn kho hợp lệ không đủ soLuongCan (dữ liệu thực tế lệch so với đơn thuốc đã kê): phần thiếu tính theo GiaBan của lô có NgaySanXuat gần nhất (mới nhập nhất) làm giá fallback.
- Trả về DTO: { donGia, canhBao: bool } - canhBao = true khi phải dùng fallback, để FE hiển thị icon cảnh báo nhỏ cạnh dòng thuốc (không chặn thanh toán).
- Toàn bộ truy vấn dùng AsNoTracking() - đây là hàm chỉ đọc, không trừ kho, không ảnh hưởng SoLuongTon.

## **3.2. GetGiaVatTuFEFO(maVatTu)**

Mục đích: lấy đơn giá tham khảo cho vật tư được thu ngân kê thêm trực tiếp tại màn hình thanh toán (không qua kho theo lô như thuốc, vì nghiệp vụ này không trừ tồn kho).

### **Quy trình xử lý:**

- Truy vấn LoVatTu WHERE MaVatTu = maVatTu AND SoLuongTon > 0 AND HanSuDung >= GETDATE(), ORDER BY HanSuDung ASC, lấy GiaBan của lô đầu tiên (không chia bình quân vì không trừ số lượng theo lô).
- Nếu không có lô hợp lệ nào: lấy GiaBan của lô có NgaySanXuat gần nhất bất kể hạn sử dụng, kèm canhBao = true.
- Đây là hàm chỉ đọc (AsNoTracking()); LoVatTu.SoLuongTon không bị thay đổi bởi module Thanh toán.

**LƯU Ý - Vật tư phát sinh thêm ở màn hình thanh toán KHÔNG trừ tồn kho LoVatTu, chỉ ghi nhận chi phí trên hóa đơn (theo xác nhận của nhóm). Việc trừ kho vật tư (nếu có) thuộc trách nhiệm của module khám bệnh khi vật tư được chỉ định trong quá trình khám.**

# **4\. CHI TIẾT ĐẶC TẢ API**

## **4.1. GET api/ThanhToan/danh-sach**

Lấy danh sách các phiếu khám đã hoàn thành khám lâm sàng, phục vụ màn hình danh sách bên trái của thu ngân, có tìm kiếm và lọc theo trạng thái thanh toán.

### **Phân quyền**

Roles: Admin, ThuNgan. Cả 2 role đều xem được toàn bộ danh sách (không lọc theo bác sĩ phụ trách).

### **Input (query params)**

| **Tham số** | **Kiểu** | **Bắt buộc** | **Mô tả**                                                                  |
| ----------- | -------- | ------------ | -------------------------------------------------------------------------- |
| search      | string   | Không        | Tìm theo tên bệnh nhân / MaBN / MaPhieu (chứa, không phân biệt hoa thường) |
| trangThai   | string   | Không        | All \| Unpaid \| Paid - mặc định All                                       |
| page        | int      | Không        | Trang hiện tại, mặc định 1                                                 |
| pageSize    | int      | Không        | Số dòng/trang, mặc định 20, tối đa 100                                     |

### **Quy trình xử lý**

- Điều kiện gốc bắt buộc: PhieuKham.TrangThaiKham = 3 (đã hoàn thành khám).
- LEFT JOIN HoaDon theo MaPhieu, điều kiện HoaDon.TrangThaiThanhToan = 1 để xác định phiếu đã có hóa đơn hợp lệ.
- trangThai = Unpaid → lọc các phiếu KHÔNG có HoaDon khớp điều kiện trên; trangThai = Paid → lọc các phiếu CÓ; All → không lọc thêm.
- Áp bộ lọc search bằng LIKE trên HoTen (BenhNhan), MaBN, MaPhieu - sargable, tránh hàm bao quanh cột trong WHERE.
- Include thông tin BenhNhan (HoTen, MaBN) và NhanVien bác sĩ (MaNV, HoTen) qua PhieuKham.MaNV.
- Sắp xếp mặc định theo NgayKham giảm dần; áp Skip/Take theo page, pageSize; dùng AsNoTracking().

### **Output**

{ "data": \[ { "maPhieu": "PK_260716_001", "maBN": "BN260001", "hoTen": "NGUYỄN VĂN AN", "ngaySinh": "1980-03-15", "gioiTinh": "Nam", "tenBacSi": "BS. Nguyễn Văn An", "ngayKham": "2026-07-16T09:00:00", "daThanhToan": false } \], "totalCount": 42, "page": 1, "pageSize": 20 }

### **Ràng buộc & Xử lý lỗi**

| **Mã lỗi** | **Trường hợp**                        |
| ---------- | ------------------------------------- |
| 200        | Thành công (kể cả khi danh sách rỗng) |
| 400        | pageSize > 100 hoặc page < 1          |
| 401/403    | Không có token hoặc role không hợp lệ |

## **4.2. GET api/ThanhToan/{maPhieu}/chi-tiet**

Trả về toàn bộ chi tiết chi phí của một phiếu khám để hiển thị bảng tổng hợp bên phải màn hình thanh toán.

### **Phân quyền**

Roles: Admin, ThuNgan.

### **Input**

| **Tham số** | **Vị trí** | **Kiểu** | **Mô tả**                      |
| ----------- | ---------- | -------- | ------------------------------ |
| maPhieu     | route      | string   | Mã phiếu khám cần lấy chi tiết |

### **Quy trình xử lý**

- Kiểm tra PhieuKham tồn tại và TrangThaiKham = 3, nếu không → 404 / 409 tương ứng (xem bảng lỗi).
- Lấy thông tin BenhNhan, bác sĩ (NhanVien qua PhieuKham.MaNV).
- CLS: JOIN DichVuYTe với ChiTietDichVuYTe theo MaDV, lấy TenDV và GiaTien; SoLuong mặc định = 1, ThanhTien = GiaTien.
- Thuốc: với mỗi dòng ChiTietDonThuoc (join DanhMucThuoc lấy TenThuoc), tính SoLuongQuyDoi = Số lượng mỗi lần uống × số lần/ngày × số ngày (parse từ CachDung/SoNgay theo đúng công thức mô tả trong yêu cầu), sau đó gọi GetGiaThuocFEFO(maThuoc, soLuongQuyDoi) để lấy DonGia + cờ canhBao; ThanhTien = SoLuongQuyDoi × DonGia.
- Vật tư: đọc trực tiếp SoLuong và DonGia đã lưu sẵn trong ChiTietVatTuPhieuKham (không cần tính lại), ThanhTien = SoLuong × DonGia.
- Tính 3 tổng phụ (TongTienDichVu, TongTienThuoc, TongTienVatTu) và TongTienThanhToan = tổng 3 khoản.
- Nếu phiếu đã có HoaDon (TrangThaiThanhToan = 1): trả kèm MaHoaDon, PhuongThucTT, NgayThanhToan, tên thu ngân đã thực hiện (join NhanVien qua HoaDon.MaNV).
- Toàn bộ truy vấn dùng AsNoTracking(), Include theo đúng 1 câu LINQ (không N+1 theo từng dòng thuốc/CLS).

### **Output**

{ "maPhieu": "PK_260716_001", "benhNhan": { "maBN": "BN260001", "hoTen": "NGUYỄN VĂN AN", "ngaySinh": "1980-03-15" }, "bacSi": { "maNV": "NV002", "hoTen": "BS. Nguyễn Văn An" }, "cls": \[ { "maDV": "DV001", "tenDV": "Siêu âm ổ bụng tổng quát", "soLuong": 1, "donGia": 150000, "thanhTien": 150000 } \], "thuoc": \[ { "maThuoc": "T001", "tenThuoc": "Paracetamol 500mg", "soLuongQuyDoi": 21, "donGia": 2000, "thanhTien": 42000, "canhBao": false } \], "vatTu": \[ { "maVatTu": "VT001", "tenVatTu": "Găng tay y tế", "soLuong": 2, "donGia": 3000, "thanhTien": 6000 } \], "tongTienDichVu": 150000, "tongTienThuoc": 42000, "tongTienVatTu": 6000, "tongTienThanhToan": 198000, "daThanhToan": false }

### **Ràng buộc & Xử lý lỗi**

| **Mã lỗi** | **Trường hợp**                                               |
| ---------- | ------------------------------------------------------------ |
| 200        | Thành công                                                   |
| 404        | maPhieu không tồn tại                                        |
| 409        | PhieuKham.TrangThaiKham != 3 (chưa đủ điều kiện lập hóa đơn) |

## **4.3. POST api/ThanhToan/{maPhieu}/vat-tu**

Cho phép thu ngân kê thêm vật tư phát sinh trực tiếp vào phiếu tại màn hình thanh toán (tương ứng popup "Kê vật tư tiêu hao phát sinh" trong FE).

### **Phân quyền**

Roles: Admin, ThuNgan.

### **Input**

| **Trường** | **Vị trí** | **Kiểu** | **Bắt buộc** | **Mô tả**                                     |
| ---------- | ---------- | -------- | ------------ | --------------------------------------------- |
| maPhieu    | route      | string   | Có           | Phiếu cần thêm vật tư                         |
| maVatTu    | body       | string   | Có           | FK tới DanhMucVatTu (không truyền tên string) |
| soLuong    | body       | int      | Có           | Số lượng, phải > 0                            |

### **Quy trình xử lý**

- Kiểm tra PhieuKham tồn tại, TrangThaiKham = 3, và CHƯA có HoaDon với TrangThaiThanhToan = 1 → nếu đã thanh toán, chặn với 409.
- Kiểm tra maVatTu tồn tại trong DanhMucVatTu và IsActive = 1 → nếu không, 404/400.
- Kiểm tra soLuong > 0 → nếu không, 400.
- Gọi GetGiaVatTuFEFO(maVatTu) lấy DonGia tham khảo hiện tại (snapshot ngay lúc thêm, không đổi về sau dù giá lô thay đổi).
- Insert dòng mới vào ChiTietVatTuPhieuKham (MaPhieu, MaVatTu, SoLuong, DonGia). Không trừ LoVatTu.SoLuongTon.

### **Output**

{ "maPhieu": "PK_260716_001", "maVatTu": "VT001", "tenVatTu": "Găng tay y tế", "soLuong": 2, "donGia": 3000, "thanhTien": 6000 }

### **Ràng buộc & Xử lý lỗi**

| **Mã lỗi** | **Trường hợp**                          |
| ---------- | --------------------------------------- |
| 201        | Thêm thành công                         |
| 400        | soLuong <= 0, hoặc maVatTu không active |
| 404        | maPhieu hoặc maVatTu không tồn tại      |
| 409        | Phiếu đã thanh toán, không cho sửa      |

## **4.4. DELETE api/ThanhToan/{maPhieu}/vat-tu/{maVatTu}**

Xóa một dòng vật tư đã kê (kể cả vật tư gốc từ khám bệnh lẫn vật tư phát sinh thêm ở màn thanh toán) khỏi phiếu, khi phiếu chưa thanh toán.

### **Phân quyền**

Roles: Admin, ThuNgan.

### **Input**

| **Trường** | **Vị trí** | **Kiểu** | **Mô tả**                 |
| ---------- | ---------- | -------- | ------------------------- |
| maPhieu    | route      | string   | Phiếu cần xóa vật tư      |
| maVatTu    | route      | string   | Vật tư cần xóa khỏi phiếu |

### **Quy trình xử lý**

- Kiểm tra PhieuKham CHƯA có HoaDon với TrangThaiThanhToan = 1 → nếu đã thanh toán, chặn với 409.
- Kiểm tra dòng ChiTietVatTuPhieuKham (maPhieu, maVatTu) tồn tại → nếu không, 404.
- Xóa dòng khỏi ChiTietVatTuPhieuKham (hard delete - đây là dữ liệu chi tiết hóa đơn nháp, chưa phát sinh giao dịch tài chính nên không áp dụng soft delete).

### **Output**

204 No Content khi xóa thành công.

### **Ràng buộc & Xử lý lỗi**

| **Mã lỗi** | **Trường hợp**                                  |
| ---------- | ----------------------------------------------- |
| 204        | Xóa thành công                                  |
| 404        | Không tìm thấy dòng vật tư tương ứng trên phiếu |
| 409        | Phiếu đã thanh toán, không cho sửa              |

## **4.5. POST api/ThanhToan/xac-nhan**

API quan trọng nhất module: xác nhận thanh toán và lập hóa đơn chính thức khi thu ngân bấm nút "Thu tiền & đóng phiếu" (phím tắt F12).

### **Phân quyền**

Roles: Admin, ThuNgan. MaNV của thu ngân thực hiện lấy từ claim trong JWT, không nhận từ body (tránh giả mạo người thực hiện).

### **Input (body)**

| **Trường**   | **Kiểu** | **Bắt buộc** | **Mô tả**                      |
| ------------ | -------- | ------------ | ------------------------------ |
| maPhieu      | string   | Có           | Phiếu khám cần lập hóa đơn     |
| phuongThucTT | string   | Có           | 'Tiền mặt' hoặc 'Chuyển khoản' |

### **Quy trình xử lý (trong 1 DbTransaction)**

- Bước 1 - Khóa & kiểm tra: đọc PhieuKham theo maPhieu với khóa cập nhật (WITH (UPDLOCK, HOLDLOCK) hoặc tương đương transaction isolation trong EF Core) để tránh race condition khi bấm F12 nhiều lần liên tiếp / nhiều tab.
- Kiểm tra PhieuKham tồn tại và TrangThaiKham = 3 → sai thì 404/409.
- Kiểm tra CHƯA tồn tại HoaDon nào của phiếu này với TrangThaiThanhToan = 1 → nếu đã có, trả 409 Conflict (chặn tạo trùng hóa đơn do double-submit).
- Kiểm tra phuongThucTT thuộc {Tiền mặt, Chuyển khoản} → sai thì 400.
- Bước 2 - Tính lại tổng tiền ở backend: KHÔNG tin số liệu FE gửi lên; tái sử dụng đúng logic tại mục 4.2 để tính TongTienDichVu, TongTienThuoc (qua GetGiaThuocFEFO), TongTienVatTu (Σ DonGia × SoLuong hiện có trong ChiTietVatTuPhieuKham tại thời điểm xác nhận).
- Bước 3 - Sinh MaHoaDon theo quy tắc: "HD" + yyMMdd (ngày hiện tại) + 3 chữ số ngẫu nhiên. Nếu trùng khóa chính khi insert (DbUpdateException do đụng UNIQUE/PK) → retry sinh lại mã tối đa 5 lần (không dùng cách đếm-rồi-cộng-1 vì không an toàn khi có nhiều giao dịch đồng thời).
- Bước 4 - Insert bảng HoaDon: MaHoaDon, MaPhieu, MaNV (từ JWT), NgayThanhToan = GETDATE(), TongTienDichVu, TongTienThuoc, TongTienVatTu, ThanhTien = tổng 3 khoản, TrangThaiThanhToan = 1, PhuongThucTT.
- Bước 5 - Commit transaction. Nếu bất kỳ bước nào lỗi → rollback toàn bộ, không tạo hóa đơn một phần.

### **Output**

{ "maHoaDon": "HD260716782", "maPhieu": "PK_260716_001", "maNV": "NV010", "tenThuNgan": "Mai Xuân Phát", "ngayThanhToan": "2026-07-16T14:32:00", "phuongThucTT": "Tiền mặt", "tongTienDichVu": 150000, "tongTienThuoc": 42000, "tongTienVatTu": 6000, "thanhTien": 198000 }

### **Ràng buộc & Xử lý lỗi**

| **Mã lỗi** | **Trường hợp**                                                                              |
| ---------- | ------------------------------------------------------------------------------------------- |
| 201        | Lập hóa đơn thành công                                                                      |
| 400        | phuongThucTT không hợp lệ                                                                   |
| 404        | maPhieu không tồn tại                                                                       |
| 409        | TrangThaiKham != 3, hoặc phiếu đã có hóa đơn với TrangThaiThanhToan = 1 (đã thanh toán rồi) |
| 500        | Sinh MaHoaDon trùng quá 5 lần retry (cực hiếm) - trả lỗi để thu ngân thử lại thao tác       |

## **4.6. GET api/ThanhToan/{maHoaDon}/pdf**

Sinh file PDF hóa đơn thật từ backend (dùng thư viện QuestPDF hoặc tương đương), phục vụ nút "In phiếu thu \[F8\]" và nhu cầu lưu file hóa đơn xuống máy tính.

### **Phân quyền**

Roles: Admin, ThuNgan.

### **Input**

| **Tham số** | **Vị trí** | **Kiểu** | **Mô tả**               |
| ----------- | ---------- | -------- | ----------------------- |
| maHoaDon    | route      | string   | Mã hóa đơn cần xuất PDF |

### **Quy trình xử lý**

- Truy vấn HoaDon theo maHoaDon, join PhieuKham → BenhNhan, NhanVien (bác sĩ + thu ngân) → nếu không tồn tại, 404.
- Lấy lại chi tiết CLS/thuốc/vật tư tại thời điểm hiện tại theo đúng logic mục 4.2 để hiển thị bảng kê trong PDF (lưu ý: vì giá thuốc/vật tư không snapshot, số liệu hiển thị lại có thể lệch rất nhỏ nếu tồn kho biến động sau khi thanh toán - chấp nhận được vì xác suất thấp và không ảnh hưởng số tiền đã thu ghi trong HoaDon.ThanhTien).
- Build layout PDF: header phòng khám, thông tin bệnh nhân, bảng chi tiết 3 nhóm chi phí, tổng tiền, phương thức thanh toán, chữ ký thu ngân.
- Trả về response với Content-Type: application/pdf, header Content-Disposition: attachment; filename={maHoaDon}.pdf.

### **Output**

Binary stream (application/pdf).

### **Ràng buộc & Xử lý lỗi**

| **Mã lỗi** | **Trường hợp**          |
| ---------- | ----------------------- |
| 200        | Trả file PDF thành công |
| 404        | maHoaDon không tồn tại  |

# **5\. GIẢ ĐỊNH & RỦI RO ĐÃ BIẾT (ASSUMPTIONS)**

**CẢNH BÁO - Đọc kỹ trước khi bảo vệ đồ án**

- Giá thuốc không được snapshot tại thời điểm kê đơn (ChiTietDonThuoc và DanhMucThuoc không có cột giá). Số tiền thuốc trên hóa đơn được TÍNH LẠI tại thời điểm thanh toán bằng cách mô phỏng FEFO trên LoThuoc hiện tại. Nếu tồn kho biến động giữa lúc khám và lúc thanh toán (ví dụ lô bị đơn khác xuất trước), số tiền có thể lệch nhẹ so với giá trị thực tế lẽ ra đã xuất khi kê đơn.
- Vật tư phát sinh thêm tại màn hình thanh toán KHÔNG trừ tồn kho LoVatTu - chỉ ghi nhận chi phí trên hóa đơn. Đây là quyết định nghiệp vụ đã được nhóm xác nhận, cần nêu rõ trong phần giới hạn hệ thống khi bảo vệ.
- PDF hóa đơn build lại chi tiết CLS/thuốc/vật tư tại thời điểm xuất (không lưu snapshot chi tiết từng dòng vào HoaDon), nên nếu in lại PDF sau một thời gian dài, phần chi tiết dòng có thể không tuyệt đối khớp 100% với thời điểm thanh toán ban đầu - tuy nhiên tổng tiền đã thu (HoaDon.ThanhTien) là số liệu cố định, không đổi.
- Đề xuất mở rộng (ngoài phạm vi hiện tại, có thể làm ở bản sau): thêm bảng log chi tiết xuất kho theo lô (kèm giá) khi kê đơn thuốc, để loại bỏ hoàn toàn rủi ro lệch giá nêu trên.

# **6\. BẢNG THEO DÕI THAY ĐỔI (CHANGELOG)**

| **Phiên bản** | **Ngày**   | **Nội dung**                                                                                                                             | **Người thực hiện** |
| ------------- | ---------- | ---------------------------------------------------------------------------------------------------------------------------------------- | ------------------- |
| 1.0           | 16/07/2026 | Khởi tạo đặc tả module Thanh toán & Hóa đơn (6 API), bổ sung migration PhuongThucTT, định nghĩa service GetGiaThuocFEFO/GetGiaVatTuFEFO. | abc                 |