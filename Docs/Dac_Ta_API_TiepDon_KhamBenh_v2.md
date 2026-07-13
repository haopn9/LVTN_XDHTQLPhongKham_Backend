**ĐẶC TẢ API**

**MODULE TIẾP ĐÓN & KHÁM BỆNH**

_Hệ thống Quản lý Phòng khám - QLPhongKham_

_Bản cập nhật theo nghiệp vụ mới - Luồng khám TRỰC TIẾP tại phòng khám_

Phạm vi: TiepDonController · KhamBenhController · HoSoBenhAnController · DanhSachController

_Đặt lịch khám Online: KHÔNG thuộc phạm vi bản đặc tả này - sẽ làm ở giai đoạn sau_

# 0\. TÓM TẮT THAY ĐỔI SO VỚI BẢN TRƯỚC

Bảng dưới liệt kê toàn bộ các điểm đã thống nhất giữa nhóm và GVHD, áp dụng cho bản đặc tả này.

| **#** | **Nội dung thay đổi**                                                                                        | **Mức độ** | **Áp dụng tại mục** |
| ----- | ------------------------------------------------------------------------------------------------------------ | ---------- | ------------------- |
| 1     | Bỏ trường danhSachICD khỏi bước Tiếp đón - ICD chỉ nhập ở bước Khám cơ bản (bác sĩ)                          | Cao        | 9                   |
| 2     | PhieuKham.MaNV chính thức = bác sĩ được chỉ định (RoleID=2); không lưu vết lễ tân tiếp đón                   | Cao        | 9, 10, 11           |
| 3     | maNVBacSi chuyển từ Tuỳ chọn sang BẮT BUỘC khi tạo phiếu khám                                                | Cao        | 9                   |
| 4     | Chặn tạo phiếu khám mới nếu bệnh nhân còn phiếu đang ở trạng thái Chờ khám/Đang khám/Chờ CLS                 | Cao        | 9                   |
| 5     | Danh sách tiếp đón (mục 10): lọc theo vai trò - BacSi chỉ thấy phiếu của chính mình, không thể xem chéo      | Cao        | 10                  |
| 6     | API lấy danh sách bác sĩ tách sang controller riêng: DanhSachController (api/DanhSach/bac-si)                | Trung bình | 12 (mới)            |
| 7     | ICD chuyển từ nhóm "Kết luận khám" sang nhóm "Khám cơ bản"; Kết luận khám chỉ hiển thị lại (read-only)       | Cao        | 42, 43              |
| 8     | Trạng thái CLS rút gọn còn 2 mức (Chưa thực hiện / Đã làm CLS), bác sĩ tự toggle qua PUT, field trangThaiCLS | Cao        | 42, 43              |
| 9     | Chặn kê đơn thuốc nếu phiếu còn dịch vụ CLS ở trạng thái Chưa thực hiện                                      | Cao        | 43                  |
| 10    | Kê đơn thuốc: kiểm tra & trừ tồn kho theo lô (LoThuoc), nguyên tắc FEFO, chặn cứng nếu không đủ tồn          | Cao        | 43                  |
| 11    | Đặt lịch khám Online: đưa ra khỏi phạm vi đợt này, làm sau                                                   | -          | -                   |
| 12    | Gợi ý bác sĩ theo triệu chứng/ca trực: đưa ra khỏi phạm vi đợt này, làm sau                                  | -          | -                   |
| 13    | "Hồ sơ bệnh án" dùng chung bảng BenhNhan hiện có (MaBN = mã hồ sơ), không tạo bảng mới                       | Thấp       | Ghi chú schema      |

**_Ghi chú:_** _Các API danh mục ICD (mục 12-15), danh mục Dịch vụ CLS (mục 16-19), danh mục Thuốc (mục 25-28) đã có sẵn ở các bản đặc tả khác - module này KHÔNG viết lại, chỉ gọi lại (GET)._

# 1\. GHI CHÚ SCHEMA DB (áp dụng cho toàn bộ đặc tả)

**1.1 Bảng PhieuKham**

PhieuKham: MaPhieu, MaBN, MaNV, NgayKham, Mach, NhietDo, HuyetAp,

CanNang, ChieuCao, KetLuan, LyDoKham, TrangThaiKham

- MaNV trong PhieuKham CHÍNH THỨC là mã nhân viên có RoleID = 2 (BacSi) - được gán ngay lúc lễ tân tạo phiếu (không đổi qua các giai đoạn sau). Không còn khái niệm "MaNV lễ tân lúc tạo, bác sĩ nhận ca sau" như bản đặc tả cũ.
- Hệ thống KHÔNG lưu vết lễ tân nào đã tiếp đón phiếu khám này - nhóm đã xác nhận chấp nhận việc này.
- Không có cột MaBacSiChiDinh riêng như đề xuất ở bản nháp trước - dùng thẳng cột MaNV có sẵn.
- Không có cột MaICD trực tiếp - dùng bảng junction ChiTietPhieuKhamICD.
- Không có cột Spo2, NhipTho (frontend tự mở rộng UI, không lưu DB).

**1.2 Bảng BenhNhan (= Hồ sơ bệnh án)**

BenhNhan: MaBN, HoTen, NgaySinh (DATE), GioiTinh, SDT, DiaChi, TienSuBenh

- "Hồ sơ bệnh án" trong nghiệp vụ = chính bảng BenhNhan hiện có. MaBN đóng vai trò vừa là mã bệnh nhân vừa là mã hồ sơ (1 bệnh nhân - 1 hồ sơ - nhiều PhieuKham). KHÔNG tạo bảng HoSoBenhAn riêng.
- Tra cứu hồ sơ bằng "mã hồ sơ" hay "mã bệnh nhân" đều là tra cứu theo MaBN - xem mục 44.

**1.3 Bảng ChiTietPhieuKhamICD (junction MaPhieu ↔ MaICD)**

ChiTietPhieuKhamICD: MaPhieu, MaICD - quan hệ nhiều-nhiều (1 phiếu có thể có nhiều ICD)

- ICD được nhập/chỉnh sửa DUY NHẤT ở bước "Khám cơ bản" (bác sĩ). Lễ tân không còn được nhập ICD ở bước Tiếp đón (khác bản đặc tả cũ).
- Ở bước "Kết luận khám", danh sách ICD chỉ được hiển thị lại (read-only), không cho chỉnh sửa thêm.

**1.4 Bảng DichVuYTe (chỉ định CLS theo phiếu khám) & ChiTietDichVuYTe (danh mục CLS)**

DichVuYTe (chỉ định theo phiếu): MaChiTiet IDENTITY, MaPhieu, MaDV, KetQua, TrangThaiDichVu

ChiTietDichVuYTe (danh mục): MaDV, MaLoaiDV, TenDV, GiaTien, TrangThai

- TrangThaiDichVu chỉ còn 2 mức: 0 = Chưa thực hiện | 1 = Đã làm CLS. Bỏ mức "2 = BS đã đọc" từng đề xuất ở bản nháp trước.
- Bác sĩ là người trực tiếp toggle qua lại giữa 2 trạng thái này (không có vai trò KTV nhập kết quả riêng ở đợt này).

**1.5 Bảng DonThuoc / ChiTietDonThuoc / DanhMucThuoc / LoThuoc**

DonThuoc: MaDonThuoc, MaPhieu, NgayKeDon, LoiDan

ChiTietDonThuoc: MaDonThuoc, MaThuoc, SoLuong, CachDung, TrangThaiPhatThuoc

DanhMucThuoc: MaThuoc, TenThuoc, HoatChat, DonViTinh, IsActive

LoThuoc: MaLo, MaThuoc, MaNCC, SoLuongNhap, SoLuongTon,

GiaNhap, GiaBan, NgaySanXuat, HanSuDung

- 1 MaThuoc có thể có NHIỀU lô (LoThuoc), mỗi lô có SoLuongTon và HanSuDung riêng.
- Khi kê đơn: tồn khả dụng = SUM(SoLuongTon) của các lô CHƯA hết hạn (HanSuDung >= GETDATE()) của MaThuoc đó.
- Nếu tồn khả dụng < số lượng yêu cầu → CHẶN CỨNG, không cho kê (không có chế độ ghi nhận thiếu hụt ở đợt này).
- Nếu đủ → trừ theo nguyên tắc FEFO (First-Expired-First-Out: lô hết hạn gần nhất bị trừ trước); có thể phải trừ qua nhiều lô nếu 1 lô không đủ số lượng.
- Toàn bộ thao tác kiểm tra + trừ kho + INSERT ChiTietDonThuoc chạy trong CÙNG 1 transaction.

**1.6 Bảng NhanVien / Users / Roles**

NhanVien: MaNV, UserID, HoTen, ChuyenMon, MaKhoa, SDT, Email

Users: UserID, Username, PasswordHash, RoleID, IsActive

Roles: RoleID, RoleName (2 = BacSi, 3 = LeTan, ...)

# 2\. CONTROLLER: TiepDonController.cs

**Base Route:** api/TiepDon

**Phân quyền chung:** Lễ tân (LeTan) & Admin (một số action mở rộng cho BacSi - ghi rõ theo từng mục)

## 8\. TRA CỨU BỆNH NHÂN CŨ THEO SĐT

_Không thay đổi so với bản trước._

**8.1 Tên API**

GET api/tiep-don/tra-cuu?sdt={soDienThoai}

**8.2 Mô tả**

- Lễ tân nhập SĐT để kiểm tra bệnh nhân đã có hồ sơ (BenhNhan) trong hệ thống chưa.
- Tìm thấy → trả thông tin để điền nhanh vào form. Không tìm thấy → bệnh nhân mới, lễ tân nhập thủ công.
- Cả hai trường hợp đều trả HTTP 200; frontend phân nhánh qua field "found".

**8.3 Phân quyền**

- Yêu cầu token hợp lệ trong Header Authorization
- Role được phép: LeTan, Admin

**8.4 Input (Query Parameter + Header)**

Header:

\- Authorization &lt;string&gt; : "Bearer {token}"

Query:

\- sdt &lt;string&gt; : Số điện thoại cần tra cứu - BẮT BUỘC

Ví dụ: GET api/tiep-don/tra-cuu?sdt=0901234567

**8.5 Quy trình xử lý tại server**

B1: Xác thực token và quyền LeTan / Admin

B2: Kiểm tra tham số sdt không rỗng

B3: Validate định dạng sdt (10 chữ số, bắt đầu bằng 0)

B4: SELECT từ bảng BenhNhan WHERE SDT = @sdt

B5a: Tìm thấy → HTTP 200, found = true kèm dữ liệu BN

B5b: Không tìm thấy → HTTP 200, found = false (luồng bình thường, không phải lỗi)

**8.6 Output - khi tìm thấy (HTTP 200)**

{

"found": true,

"data": {

"maBN": &lt;string&gt;, "hoTen": &lt;string&gt;, "ngaySinh": &lt;string&gt;, // DD-MM-YYYY

"gioiTinh": &lt;string&gt;, "sdt": &lt;string&gt;,

"diaChi": &lt;string&gt;, // có thể null

"tienSuBenh": &lt;string&gt; // có thể null

}

}

**8.7 Output - khi không tìm thấy (HTTP 200)**

{

"found": false, "data": null,

"message": "Không tìm thấy hồ sơ bệnh nhân với số điện thoại này. Đây là bệnh nhân mới."

}

**8.8 Xử lý lỗi**

- Thiếu sdt → HTTP 400 | "Vui lòng nhập số điện thoại để tra cứu!"
- Sai định dạng sdt → HTTP 400 | "Số điện thoại không đúng định dạng (phải gồm đúng 10 chữ số và bắt đầu bằng số 0)!"
- Token hết hạn → HTTP 401 | "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại"
- Không có quyền → HTTP 403 | "Bạn không có quyền truy cập chức năng này"
- Lỗi hệ thống → HTTP 500 | "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại"

## 9\. TIẾP NHẬN BỆNH NHÂN (TẠO PHIẾU KHÁM)

**9.1 Tên API**

POST api/TiepDon

**9.2 Mô tả**

Lễ tân hoàn tất form tiếp đón và lưu để tạo phiếu khám mới. API xử lý 2 tình huống:

- Tình huống A - Bệnh nhân CŨ (client truyền maBN có giá trị): chỉ tạo bản ghi mới trong PhieuKham, liên kết maBN cũ. Nếu diaChi/tienSuBenh khác hiện tại → cập nhật lại BenhNhan.
- Tình huống B - Bệnh nhân MỚI (maBN rỗng/null): server sinh maBN mới → INSERT BenhNhan (tạo hồ sơ) → INSERT PhieuKham liên kết maBN vừa tạo (lưu phiếu khám vào hồ sơ).

**\[CẬP NHẬT\]** Trường danhSachICD ĐÃ BỊ LOẠI BỎ khỏi API này. Lễ tân không nhập ICD - ICD do bác sĩ nhập ở bước Khám cơ bản (xem mục 43).

**\[CẬP NHẬT\]** Trường maNVBacSi chuyển từ Tuỳ chọn → BẮT BUỘC. Không có nhánh "bỏ trống, bác sĩ phân công sau" nữa.

**\[CẬP NHẬT\]** Cột PhieuKham.MaNV được gán = maNVBacSi ngay khi tạo phiếu (không phải MaNV của lễ tân).

**\[CẬP NHẬT\]** Thêm ràng buộc mới: bệnh nhân không được có phiếu khám khác đang ở trạng thái Chờ khám(0)/Đang khám(1)/Chờ CLS(2) - nếu có, từ chối tạo phiếu mới.

**9.3 Phân quyền**

- Yêu cầu token hợp lệ trong Header Authorization
- Role được phép: LeTan, Admin

**9.4 Input (Request Body - JSON)**

Header:

\- Authorization &lt;string&gt; : "Bearer {token}"

\-- Thông tin bệnh nhân (bảng BenhNhan) --

\- maBN &lt;string&gt; : Mã BN cũ - truyền nếu là BN cũ, bỏ trống/null nếu là BN mới

\- hoTen &lt;string&gt; : Họ và tên bệnh nhân (CHỮ HOA) - BẮT BUỘC

\- ngaySinh &lt;string&gt; : DD - MM - YYYY- BẮT BUỘC

\- gioiTinh &lt;string&gt; : "Nam" | "Nữ" | "Khác" - BẮT BUỘC

\- sdt &lt;string&gt; : Số điện thoại 10 chữ số - BẮT BUỘC

\- diaChi &lt;string&gt; : Địa chỉ thường trú - Tuỳ chọn

\- tienSuBenh &lt;string&gt; : Tiền sử bệnh lý - Tuỳ chọn

\-- Thông tin phiếu khám (bảng PhieuKham) --

\- maNVBacSi &lt;string&gt; : Mã nhân viên RoleID=2 (BacSi) được chỉ định khám - BẮT BUỘC \[CẬP NHẬT\]

\- lyDoKham &lt;string&gt; : Lý do đến khám / triệu chứng - BẮT BUỘC

\* KHÔNG còn trường danhSachICD ở API này. \[CẬP NHẬT\]

Ví dụ Request Body (BN mới):

{

"maBN": "",

"hoTen": "NGUYỄN THỊ LAN",

"ngaySinh": "15-06-1995",

"gioiTinh": "Nữ",

"sdt": "0912345678",

"diaChi": "456 Lê Lợi, Quận 1, TP. Hồ Chí Minh",

"tienSuBenh": "Dị ứng Penicillin",

"maNVBacSi": "BS002",

"lyDoKham": "Đau đầu, sốt nhẹ 2 ngày"

}

Ví dụ Request Body (BN cũ):

{

"maBN": "BN260001", "hoTen": "NGUYỄN VĂN AN",

"ngaySinh": "1980-03-15", "gioiTinh": "Nam", "sdt": "0901234567",

"diaChi": "123 Nguyễn Trãi, Quận 5, TP. Hồ Chí Minh",

"tienSuBenh": "Tăng huyết áp, Đái tháo đường typ 2",

"maNVBacSi": "BS001", "lyDoKham": "Tái khám định kỳ, đo huyết áp"

}

**9.5 Quy trình xử lý tại server**

B1: Xác thực token

B2: Validate các trường bắt buộc (hoTen, ngaySinh, gioiTinh, sdt, lyDoKham, maNVBacSi)

B3: Validate định dạng sdt (10 chữ số, bắt đầu bằng 0)

B4: Validate ngaySinh (DD - MM - YYYY, không là ngày tương lai, năm >= 1900)

B5: Validate gioiTinh ("Nam" | "Nữ" | "Khác")

B6: Validate maNVBacSi tồn tại trong NhanVien và có RoleID = 2 (BacSi) \[CẬP NHẬT - nay bắt buộc\]

B7 (BN mới - maBN rỗng): sinh maBN mới theo GHI CHÚ 4 → INSERT BenhNhan

(BN cũ - maBN có giá trị): kiểm tra maBN tồn tại (404 nếu không) →

nếu diaChi/tienSuBenh thay đổi → UPDATE BenhNhan

B8: \[MỚI\] Kiểm tra bệnh nhân (theo maBN ở B7) KHÔNG có phiếu khám nào khác

đang TrangThaiKham IN (0,1,2) → nếu có, từ chối tạo phiếu mới (409)

B9: Sinh maPhieu mới theo GHI CHÚ 4

B10: INSERT vào PhieuKham:

MaPhieu, MaBN, MaNV = @maNVBacSi, NgayKham (GETDATE()),

LyDoKham, TrangThaiKham = 0 \[CẬP NHẬT - MaNV = bác sĩ\]

B11: Trả về HTTP 201 kèm thông tin phiếu khám vừa tạo

\* Toàn bộ B7 → B10 chạy trong một transaction.

**9.6 Output (khi thành công) - HTTP 201 Created**

{

"message": "Tiếp nhận bệnh nhân thành công",

"data": {

"maPhieu": &lt;string&gt;, "maBN": &lt;string&gt;, "hoTen": &lt;string&gt;,

"ngayKham": &lt;string&gt;, // ISO 8601

"lyDoKham": &lt;string&gt;,

"maBacSi": &lt;string&gt;, // = MaNV, mã bác sĩ được chỉ định \[CẬP NHẬT tên field\]

"tenBacSi": &lt;string&gt;, // Họ tên bác sĩ được chỉ định \[CẬP NHẬT tên field\]

"trangThaiKham": 0,

"isNewPatient": &lt;bool&gt;

}

}

**9.7 Ràng buộc**

- Bắt buộc: hoTen, ngaySinh, gioiTinh, sdt, lyDoKham, maNVBacSi \[CẬP NHẬT\]
- Họ tên: không trống, chỉ chữ cái (kể cả tiếng Việt) và khoảng trắng
- SĐT: đúng 10 chữ số, bắt đầu bằng 0, không khoảng trắng. Không unique - 1 SĐT có thể khám nhiều lần; unique chỉ áp dụng khi tạo BenhNhan MỚI
- Ngày sinh: DD - MM - YYYY, không là ngày tương lai, năm >= 1900
- Giới tính: chỉ "Nam", "Nữ", "Khác"
- Bác sĩ chỉ định (maNVBacSi): BẮT BUỘC - phải tồn tại trong NhanVien và có RoleID = 2. Được lưu trực tiếp vào PhieuKham.MaNV. \[CẬP NHẬT\]
- \[MỚI\] Không cho tạo phiếu khám mới nếu bệnh nhân (maBN) đang có phiếu khám khác ở trạng thái Chờ khám(0)/Đang khám(1)/Chờ CLS(2)
- Tạo BN mới: không cho phép trùng SĐT - kiểm tra trước khi INSERT, nếu trùng → 409

**9.8 Xử lý lỗi**

- Không nhập họ tên → HTTP 400 | "Vui lòng nhập Họ và tên bệnh nhân!"
- Không nhập SĐT → HTTP 400 | "Vui lòng nhập Số điện thoại!"
- SĐT sai định dạng → HTTP 400 | "Số điện thoại không đúng định dạng (phải gồm đúng 10 chữ số và bắt đầu bằng số 0, không chứa khoảng trắng)!"
- Không nhập ngày sinh → HTTP 400 | "Vui lòng nhập Ngày tháng năm sinh!"
- Ngày sinh không hợp lệ → HTTP 400 | "Ngày sinh không hợp lệ. Vui lòng kiểm tra lại!"
- Ngày sinh là ngày tương lai → HTTP 400 | "Ngày sinh không thể là ngày trong tương lai!"
- Không nhập lý do khám → HTTP 400 | "Vui lòng nhập Lý do đến khám!"
- Giới tính không hợp lệ → HTTP 400 | "Giới tính không hợp lệ. Chỉ chấp nhận: Nam, Nữ, Khác!"
- \[MỚI\] Không nhập maNVBacSi → HTTP 400 | "Vui lòng chỉ định bác sĩ khám trước khi lưu tiếp đón!"
- maNVBacSi không tồn tại hoặc không phải bác sĩ → HTTP 400 | "Bác sĩ chỉ định không hợp lệ hoặc không tồn tại trong hệ thống!"
- maBN truyền lên nhưng không tồn tại → HTTP 404 | "Không tìm thấy hồ sơ bệnh nhân với mã BN đã cung cấp!"
- \[MỚI\] Bệnh nhân còn phiếu khám khác chưa xử lý xong → HTTP 409 | "Bệnh nhân này đang có 1 phiếu khám chưa hoàn tất (Mã phiếu: {maPhieu}). Vui lòng xử lý xong phiếu cũ trước khi tạo phiếu mới!"
- Tạo BN mới nhưng SĐT đã tồn tại → HTTP 409 | "Số điện thoại này đã có hồ sơ bệnh nhân trong hệ thống. Vui lòng tra cứu theo SĐT và dùng luồng bệnh nhân cũ!"
- Token hết hạn → HTTP 401 | "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại"
- Không có quyền → HTTP 403 | "Bạn không có quyền truy cập chức năng này"
- Lỗi hệ thống → HTTP 500 | "Không thể cập nhật dữ liệu từ máy chủ. Xin hãy thử lại"

## 10\. LẤY DANH SÁCH BỆNH NHÂN ĐÃ TIẾP ĐÓN

**10.1 Tên API**

GET api/TiepDon/danh-sach

**10.2 Mô tả**

Trả về danh sách các phiếu khám kèm thông tin tóm tắt bệnh nhân. Hỗ trợ tìm kiếm theo họ tên/SĐT/mã BN; lọc theo trạng thái khám, ngày khám, phân trang.

**\[CẬP NHẬT\]** Phạm vi dữ liệu trả về nay PHỤ THUỘC VÀO ROLE của người gọi (trước đây maBacSi chỉ là filter tuỳ chọn chung cho mọi role):

- Role = Admin, LeTan: mặc định xem TẤT CẢ phiếu khám trong ngày hiện tại; có thể dùng bộ lọc để xem ngày khác hoặc lọc theo 1 bác sĩ cụ thể (query maBacSi, tuỳ chọn).
- Role = BacSi: CHỈ xem được phiếu khám do CHÍNH MÌNH được chỉ định (PhieuKham.MaNV = MaNV lấy từ token) trong ngày hiện tại; có thể dùng bộ lọc để xem ngày khác nhưng vẫn giới hạn theo chính bác sĩ đó - KHÔNG được xem danh sách của bác sĩ khác.

**10.3 Phân quyền**

- Yêu cầu token hợp lệ trong Header Authorization
- Role được phép: LeTan, BacSi, Admin

**10.4 Input (Query Parameters)**

Header:

\- Authorization &lt;string&gt; : "Bearer {token}"

Query:

\- search &lt;string&gt; : Tìm theo họ tên, SĐT hoặc mã BN (gần đúng, không phân biệt hoa/thường) - Tuỳ chọn

\- maBacSi &lt;string&gt; : Lọc theo bác sĩ chỉ định (PhieuKham.MaNV).

CHỈ có hiệu lực với Role = Admin, LeTan. \[CẬP NHẬT\]

Role = BacSi: tham số này bị BỎ QUA - hệ thống luôn tự giới hạn

theo MaNV của bác sĩ đang đăng nhập, không cho xem chéo.

\- trangThai &lt;int&gt; : 0=Chờ khám | 1=Đang khám | 2=Chờ CLS | 3=Hoàn thành - Tuỳ chọn

\- ngayKham &lt;string&gt; : DD - MM - YYYY - Mặc định: ngày hiện tại (áp dụng cho mọi role)

\- page &lt;int&gt; : Trang hiện tại \[Mặc định: 1\]

\- limit &lt;int&gt; : Số bản ghi/trang \[Mặc định: 100, Tối đa: 200\]

**10.5 Quy trình xử lý tại server**

B1: Xác thực token, lấy Role + MaNV (nếu Role = BacSi) từ token

B2: \[CẬP NHẬT\] Xác định phạm vi xem theo role:

\- Role = Admin, LeTan:

→ Không giới hạn theo bác sĩ trừ khi có query maBacSi (áp dụng thêm điều kiện lọc)

→ ngayKham mặc định hôm nay, có thể đổi qua query

\- Role = BacSi:

→ BẮT BUỘC AND PhieuKham.MaNV = {MaNV từ token}

→ ngayKham mặc định hôm nay, có thể đổi qua query nhưng vẫn giữ điều kiện MaNV = chính mình

→ Query maBacSi (nếu có truyền) KHÔNG có tác dụng, bị bỏ qua

B3: Validate trangThai, ngayKham, page, limit

B4: Build query theo điều kiện đã xác định ở B2:

SELECT pk.MaPhieu, pk.MaBN, bn.HoTen, bn.NgaySinh, bn.GioiTinh, bn.SDT, bn.DiaChi,

pk.LyDoKham, pk.NgayKham, pk.TrangThaiKham,

pk.MaNV AS MaBacSi, nv.HoTen AS TenBacSi \[CẬP NHẬT tên cột output\]

FROM PhieuKham pk

JOIN BenhNhan bn ON pk.MaBN = bn.MaBN

JOIN NhanVien nv ON pk.MaNV = nv.MaNV

WHERE CAST(pk.NgayKham AS DATE) = @ngayKham

AND (@trangThai IS NULL OR pk.TrangThaiKham = @trangThai)

AND (@search IS NULL OR bn.HoTen LIKE N'%@search%' OR bn.SDT LIKE '%@search%' OR pk.MaBN LIKE '%@search%')

AND (--\[chỉ Admin/LeTan\] @maBacSi IS NULL OR pk.MaNV = @maBacSi)

AND (--\[chỉ BacSi\] pk.MaNV = @maNVTuToken)

ORDER BY pk.NgayKham ASC

B5: Đếm tổng bản ghi (total) trước khi phân trang

B6: Áp dụng OFFSET / FETCH NEXT

B7: Trả về HTTP 200 kèm data + pagination + filter

**10.6 Output (khi thành công) - HTTP 200 OK**

{

"data": \[

{

"maPhieu": &lt;string&gt;, "maBN": &lt;string&gt;, "hoTen": &lt;string&gt;,

"ngaySinh": &lt;string&gt;, "gioiTinh": &lt;string&gt;, "sdt": &lt;string&gt;, "diaChi": &lt;string&gt;,

"lyDoKham": &lt;string&gt;, "ngayKham": &lt;string&gt;, "trangThaiKham": &lt;int&gt;,

"maBacSi": &lt;string&gt;, // PhieuKham.MaNV \[CẬP NHẬT tên field\]

"tenBacSi": &lt;string&gt; // Họ tên bác sĩ chỉ định \[CẬP NHẬT tên field\]

}

\],

"pagination": { "page": &lt;int&gt;, "limit": &lt;int&gt;, "total": &lt;int&gt;, "totalPages": &lt;int&gt; },

"filter": { "ngayKham": &lt;string&gt;, "trangThai": &lt;int&gt;, "maBacSi": &lt;string&gt;, "search": &lt;string&gt; }

}

**10.7 Xử lý lỗi**

- Giá trị trangThai không hợp lệ → HTTP 400 | "Giá trị trạng thái không hợp lệ. Chỉ chấp nhận: 0 | 1 | 2 | 3"
- Định dạng ngayKham không hợp lệ → HTTP 400 | "Định dạng ngày lọc không hợp lệ. Vui lòng nhập theo định dạng DD - MM - YYYY!"
- page/limit không hợp lệ → HTTP 400 | "Giá trị phân trang không hợp lệ"
- Token hết hạn → HTTP 401 | "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại"
- Không có quyền → HTTP 403 | "Bạn không có quyền truy cập chức năng này"
- Lỗi hệ thống → HTTP 500 | "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại"

**_Ghi chú:_** _Bác sĩ truyền query maBacSi trỏ tới người khác KHÔNG bị coi là lỗi 403 - server chỉ âm thầm bỏ qua tham số này và luôn áp điều kiện MaNV = chính bác sĩ đó._

## 11\. XEM CHI TIẾT HỒ SƠ BỆNH NHÂN (THEO PHIẾU KHÁM)

**11.1 Tên API**

GET api/TiepDon/{maPhieu}

**11.2 Mô tả**

Trả về toàn bộ thông tin của 1 lượt khám theo maPhieu: thông tin cá nhân BN, thông tin phiếu khám (sinh hiệu, kết luận, lý do khám), danh sách ICD, danh sách chỉ định CLS, đơn thuốc (nếu có).

- Các trường sinh hiệu/chẩn đoán/CLS/đơn thuốc có thể null nếu bác sĩ chưa cập nhật - frontend hiển thị "Chưa cập nhật".

**11.3 Phân quyền**

- Yêu cầu token hợp lệ trong Header Authorization
- Role được phép: LeTan, BacSi, Admin

**11.4 Input**

Header: - Authorization &lt;string&gt; : "Bearer {token}"

URL Parameter:

\- maPhieu &lt;string&gt; : Mã phiếu khám cần xem - BẮT BUỘC

Ví dụ: GET api/tiep-don/PK_260609_001

**11.5 Quy trình xử lý tại server**

B1: Xác thực token và quyền truy cập

B2: Kiểm tra maPhieu tồn tại trong PhieuKham (404 nếu không)

B3: JOIN và lấy dữ liệu từ:

\- PhieuKham → thông tin phiếu khám & sinh hiệu

\- BenhNhan → thông tin cá nhân BN

\- NhanVien (theo PhieuKham.MaNV) → tên BÁC SĨ chỉ định \[CẬP NHẬT - trước là lễ tân\]

\- ChiTietPhieuKhamICD + DanhMucICD → danh sách ICD

\- DichVuYTe + ChiTietDichVuYTe → danh sách chỉ định CLS

\- DonThuoc + ChiTietDonThuoc + DanhMucThuoc → đơn thuốc (nếu có)

B4: Tổng hợp và trả về HTTP 200

**11.6 Output (khi thành công) - HTTP 200 OK**

{

"maPhieu": &lt;string&gt;, "ngayKham": &lt;string&gt;,

"trangThaiKham": &lt;int&gt;, // 0=Chờ khám | 1=Đang khám | 2=Chờ CLS | 3=Hoàn thành

"lyDoKham": &lt;string&gt;,

// --- Bệnh nhân ---

"maBN": &lt;string&gt;, "hoTen": &lt;string&gt;, "ngaySinh": &lt;string&gt;, "gioiTinh": &lt;string&gt;,

"sdt": &lt;string&gt;, "diaChi": &lt;string&gt;, "tienSuBenh": &lt;string&gt;,

// --- Bác sĩ được chỉ định (từ NhanVien JOIN PhieuKham.MaNV) ---

"maBacSi": &lt;string&gt;, // MaNV \[CẬP NHẬT tên field\]

"tenBacSi": &lt;string&gt;, // HoTen bác sĩ \[CẬP NHẬT tên field\]

// --- Sinh hiệu (bác sĩ cập nhật ở bước Khám cơ bản, có thể null) ---

"mach": &lt;int&gt;, "nhietDo": &lt;float&gt;, "huyetAp": &lt;string&gt;, "canNang": &lt;float&gt;, "chieuCao": &lt;float&gt;,

// --- Chẩn đoán ---

"ketLuan": &lt;string&gt;, // có thể null

"danhSachICD": \[ { "maICD": &lt;string&gt;, "tenBenh": &lt;string&gt; } \], // \[\] nếu chưa có

// --- Chỉ định CLS ---

"dichVuYTe": \[

{ "maChiTiet": &lt;int&gt;, "maDV": &lt;string&gt;, "tenDV": &lt;string&gt;, "ketQua": &lt;string&gt;,

"trangThaiDichVu": &lt;int&gt; } // 0=Chưa thực hiện | 1=Đã làm CLS \[CẬP NHẬT - còn 2 mức\]

\],

// --- Đơn thuốc ---

"donThuoc": {

"maDonThuoc": &lt;string&gt;, "ngayKeDon": &lt;string&gt;, "loiDanDonThuoc": &lt;string&gt;,

"chiTiet": \[ { "maThuoc": &lt;string&gt;, "tenThuoc": &lt;string&gt;, "soLuong": &lt;int&gt;,

"cachDung": &lt;string&gt;, "donViTinh": &lt;string&gt; } \]

}

}

**11.7 Xử lý lỗi**

- maPhieu không tồn tại → HTTP 404 | "Không tìm thấy hồ sơ bệnh án. Phiếu khám có thể đã bị xóa hoặc không tồn tại!"
- Token hết hạn → HTTP 401 | "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại"
- Không có quyền → HTTP 403 | "Bạn không có quyền truy cập chức năng này"
- Lỗi hệ thống → HTTP 500 | "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại"

## Ghi chú chung - TiepDonController

**Phân quyền theo action**

\[Route("api/TiepDon")\] \[ApiController\]

Action 8 - Tra cứu BN theo SĐT : \[Authorize(Roles = "LeTan,Admin")\]

Action 9 - Tiếp nhận BN : \[Authorize(Roles = "LeTan,Admin")\]

Action 10 - Danh sách tiếp đón : \[Authorize(Roles = "LeTan,BacSi,Admin")\]

Action 11 - Xem chi tiết hồ sơ : \[Authorize(Roles = "LeTan,BacSi,Admin")\]

**Quy tắc sinh mã tự động (server-side)**

maBN : "BN" + DDMMYY + stt 3 chữ số theo ngày (VD: BN260609001)

→ Chạy trong transaction / dùng UPDLOCK,HOLDLOCK để tránh race condition

maPhieu : "PK\_" + DDMMYY + "\_" + stt 3 chữ số theo ngày (VD: PK_260609_001)

**MaNV trong PhieuKham**

- MaNV LUÔN LUÔN là mã bác sĩ (RoleID = 2), gán ngay lúc tạo phiếu ở bước Tiếp đón, không đổi qua các giai đoạn khám sau. \[CẬP NHẬT\]
- Không lưu vết nhân viên lễ tân đã tiếp đón phiếu này.
- Output dùng tên field "maBacSi" / "tenBacSi" (không dùng "maNV"/"tenNhanVien" như bản cũ) để phản ánh đúng ý nghĩa.

**HTTP Status Code quy chuẩn**

200 OK → Thành công (GET)

201 Created → Tạo mới thành công (POST)

400 Bad Request → Dữ liệu đầu vào không hợp lệ

401 Unauthorized → Token không hợp lệ / hết hạn

403 Forbidden → Không có quyền truy cập

404 Not Found → Không tìm thấy tài nguyên

409 Conflict → Xung đột dữ liệu (SĐT trùng, phiếu khám đang active, ...)

500 Server Error → Lỗi server hoặc database

# 3\. CONTROLLER MỚI: DanhSachController.cs

**Base Route:** api/DanhSach

**_Ghi chú:_** _Controller mới, tách riêng khỏi TiepDonController. Lý do: API lấy danh sách bác sĩ ban đầu được viết trong TiepDonController để vá lỗi "Admin tạo phiếu khám load được danh sách bác sĩ nhưng LeTan thì không" (do api/nhan-su chỉ dành cho Admin) - nhưng module Khám bệnh (mục 41) cũng cần danh sách này để lọc theo bác sĩ. Tách thành controller riêng để 2 module dùng chung, tránh viết trùng logic và dễ tối ưu (cache) sau này mà không phải sửa 2 nơi._

## 12\. LẤY DANH SÁCH BÁC SĨ (phục vụ chọn/lọc bác sĩ)

**12.1 Tên API**

GET api/DanhSach/bac-si

**12.2 Mô tả**

Trả về danh sách nhân viên có vai trò Bác sĩ (RoleID = 2), dùng để:

- Lễ tân chọn "Bác sĩ chỉ định" khi tạo phiếu tiếp đón (trường maNVBacSi bắt buộc - mục 9.4).
- Hiển thị tên bác sĩ chỉ định trên danh sách/chi tiết phiếu khám (mục 10, 11).
- Admin lọc theo bác sĩ trong danh sách chờ khám (mục 41).
- Chỉ trả thông tin tối thiểu (mã nhân viên + tên nhân viên + mã khoa + chuyên môn), không trả các trường quản trị khác của nhân viên - khác với api/nhan-su vốn chỉ dành cho Admin.

**12.3 Phân quyền**

- Yêu cầu token hợp lệ trong Header Authorization
- Role được phép: LeTan, BacSi, Admin \[CẬP NHẬT - mở rộng thêm BacSi so với bản nháp trước\]

**12.4 Input**

Header:

\- Authorization &lt;string&gt; : "Bearer {token}"

(Không có query parameter)

Ví dụ: GET api/DanhSach/bac-si

**12.5 Quy trình xử lý tại server**

B1: Xác thực token và quyền LeTan / BacSi / Admin

B2: SELECT từ NhanVien JOIN Users

WHERE Users.RoleID = 2 (BacSi) AND Users.IsActive = 1

(dùng AsNoTracking() vì đây là query chỉ đọc)

B3: Trả về HTTP 200 kèm danh sách (mã NV + họ tên + mã Khoa + Chuyên môn), sắp xếp theo HoTen tăng dần

**12.6 Output (khi thành công) - HTTP 200 OK**

{

"data": \[

{ "maNV": "BS001", "hoTen": "NGUYỄN VĂN MINH", "maKhoa": "KHOA01", "chuyenMon": "Nội tổng quát" },

{ "maNV": "BS002", "hoTen": "TRẦN THỊ HƯƠNG"", "maKhoa": "KHOA02", "chuyenMon": "Tim Mạch" }

\]

}

**12.7 Ràng buộc**

- Chỉ trả nhân viên có RoleID = 2 (BacSi)
- Chỉ trả bác sĩ đang hoạt động (Users.IsActive = 1) - không hiển thị bác sĩ đã nghỉ việc/bị khoá
- Không trả các trường nhạy cảm khác của NhanVien (SĐT cá nhân, lương, v.v.) - chỉ maNV và hoTen, maKhoa và chuyên môn 
- Danh sách có thể rỗng \[\] nếu chưa có bác sĩ nào - không coi là lỗi

**12.8 Xử lý lỗi**

- Token hết hạn → HTTP 401 | "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại"
- Không có quyền → HTTP 403 | "Bạn không có quyền truy cập chức năng này"
- Lỗi hệ thống → HTTP 500 | "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại"

**12.9 Ghi chú kỹ thuật (không thuộc hợp đồng API, chỉ là gợi ý triển khai)**

- Danh sách bác sĩ gần như không đổi trong ngày (chỉ đổi khi Admin thêm/khoá nhân sự) - có thể cache bằng IMemoryCache, TTL ngắn (5-10 phút), invalidate khi NhanSuController có thao tác CRUD liên quan đến bác sĩ.
- Không mở rộng thêm tính năng "gợi ý bác sĩ theo triệu chứng" hoặc "lọc theo ca trực" ở đợt này - để giai đoạn sau.

# 4\. CONTROLLER: KhamBenhController.cs

**Base Route:** api/KhamBenh (module Khám bệnh) - api/HoSoBenhAn (module Hồ sơ bệnh án, chỉ đọc - xem mục 5)

**Phân quyền chung:** Admin, BacSi

## Ghi chú tích hợp đầu chương

- Ghi chú 1 - Sinh hiệu: chỉ xử lý 5 chỉ số có cột tương ứng trong PhieuKham: Mach, NhietDo, HuyetAp, CanNang, ChieuCao. Không có Spo2, NhipTho.
- Ghi chú 2 - Lời dặn/Hướng điều trị: PhieuKham không có cột lưu "Lời dặn". Trường loiDan ở bước Kết luận khám được lưu vào cột DonThuoc.LoiDan. Nếu bác sĩ nhập loiDan nhưng không kê thuốc nào, hệ thống vẫn tạo 1 bản ghi DonThuoc (không có ChiTietDonThuoc) chỉ để lưu LoiDan.

**\[CẬP NHẬT\]** Ghi chú 3 - Mã bệnh ICD: ICD được nhập và chỉnh sửa DUY NHẤT ở bước "Khám cơ bản" (cùng lúc với sinh hiệu). Ở bước "Kết luận khám", danh sách ICD CHỈ ĐƯỢC HIỂN THỊ LẠI (read-only) - không nhận input chỉnh sửa nữa. Đây là thay đổi so với bản đặc tả trước (trước đây ICD được chỉnh ở bước Kết luận khám).

**\[CẬP NHẬT\]** Ghi chú 4 - Trạng thái CLS: chỉ còn 2 mức - 0 = Chưa thực hiện | 1 = Đã làm CLS. Chính bác sĩ là người trực tiếp toggle qua lại 2 chiều (không có vai trò KTV nhập kết quả riêng ở đợt này). Việc đổi trạng thái CLS gộp chung vào API PUT api/KhamBenh/{maPhieu}, không tách API PATCH riêng.

- Ghi chú 5 - Cần chỉnh sửa Frontend (KhamBenh.jsx) trước khi tích hợp API: (a) Chỉ định CLS đổi thành ô chọn từ danh mục Dịch Vụ Y Tế CLS để lấy đúng maDV; (b) Đơn thuốc đổi thành chọn maThuoc từ danh mục Thuốc, tách rõ soLuong (int) và cachDung (string).
- Ghi chú 6 - Tái sử dụng API: danh mục ICD, danh mục Dịch vụ CLS, danh mục Thuốc đã có sẵn - module Khám bệnh KHÔNG viết lại, chỉ gọi lại (GET) từ Frontend.
- Ghi chú 7 - Trang xem chi tiết 1 lượt khám từ Hồ sơ bệnh án: dùng chung API GET api/KhamBenh/{maPhieu} (mục 42), route Frontend /ho-so-chi-tiet/:maPhieu chỉ gọi lại API này ở chế độ chỉ xem (ẩn nút Lưu).

## 41\. LẤY DANH SÁCH BỆNH NHÂN CHỜ KHÁM

**41.1 Tên API**

GET api/KhamBenh/danh-sach

**41.2 Mô tả**

Bác sĩ xem danh sách các phiếu khám đang chờ xử lý để chọn bệnh nhân bắt đầu/tiếp tục khám. Mặc định chỉ trả về phiếu ở trạng thái Chờ khám(0)/Đang khám(1)/Chờ CLS(2) - không hiển thị phiếu Hoàn thành(3). Hỗ trợ tìm kiếm theo tên/mã BN/mã phiếu, lọc theo ngày và phân trang.

- Role = BacSi → CHỈ trả về phiếu có PhieuKham.MaNV = mã nhân viên của chính bác sĩ đó - không xem được phiếu của bác sĩ khác.
- Role = Admin → trả về tất cả các phiếu (có thể lọc theo maBacSi qua query, dùng danh sách từ mục 12).

**\[CẬP NHẬT\]** Do PhieuKham.MaNV nay LUÔN LUÔN là bác sĩ (không còn lẫn lễ tân như trước), câu JOIN không cần phân biệt vai trò qua RoleID nữa - query gọn hơn hẳn so với bản trước.

**41.3 Phân quyền**

- Yêu cầu token hợp lệ trong Header Authorization
- Role được phép: BacSi, Admin

**41.4 Input (Query Parameters)**

Header:

\- Authorization &lt;string&gt; : "Bearer {token}" - BẮT BUỘC

Query:

\- search &lt;string&gt; : Tìm theo họ tên, mã BN hoặc mã phiếu - Tuỳ chọn

\- trangThai &lt;int&gt; : 0=Chờ khám | 1=Đang khám | 2=Chờ CLS - Tuỳ chọn (bỏ trống → mặc định lấy cả 0,1,2)

\- maBacSi &lt;string&gt; : Chỉ Admin dùng được - lọc theo bác sĩ (lấy từ mục 12 - api/DanhSach/bac-si)

\- ngayKham &lt;string&gt; : DD-MM-YYYY - Mặc định: ngày hiện tại

\- page &lt;int&gt; : Trang hiện tại \[Mặc định: 1\]

\- limit &lt;int&gt; : Số bản ghi/trang \[Mặc định: 20\]

Ví dụ: GET api/KhamBenh/danh-sach → DS phiếu hôm nay của bác sĩ đăng nhập, trạng thái 0/1/2

GET api/KhamBenh/danh-sach?search=an&trangThai=1 → Tìm BN tên "An" đang khám dở

**41.5 Quy trình xử lý tại server**

B1: Xác thực token, lấy Role và MaNV (nếu Role = BacSi) từ token

B2: Validate trangThai (nếu có) chỉ nhận 0, 1 hoặc 2

B3: Validate định dạng ngayKham (nếu có)

B4: Xây câu truy vấn PhieuKham JOIN BenhNhan JOIN NhanVien (MaNV = bác sĩ) \[CẬP NHẬT - bớt 1 JOIN phân vai\]

\- WHERE TrangThaiKham IN (0,1,2) hoặc = trangThai (nếu lọc)

\- AND NgayKham thuộc ngày ngayKham (mặc định hôm nay)

\- Nếu Role = BacSi → AND MaNV = {maNV lấy từ token}

\- Nếu Role = Admin và có maBacSi → AND MaNV = {maBacSi}

\- Nếu có search → AND (HoTen LIKE %search% OR MaBN LIKE %search% OR MaPhieu LIKE %search%)

B5: Sắp xếp theo NgayKham DESC, phân trang theo page/limit

B6: Trả về HTTP 200 kèm danh sách và thông tin phân trang

**41.6 Output (khi thành công) - HTTP 200 OK**

{

"data": \[

{

"maPhieu": &lt;string&gt;, "maBN": &lt;string&gt;, "hoTen": &lt;string&gt;, "gioiTinh": &lt;string&gt;,

"sdt": &lt;string&gt;, "lyDoKham": &lt;string&gt;,

"maBacSi": &lt;string&gt;, "tenBacSi": &lt;string&gt;,

"ngayKham": &lt;string&gt;, "trangThaiKham": &lt;int&gt; // 0 | 1 | 2

}

\],

"pagination": { "page": &lt;int&gt;, "limit": &lt;int&gt;, "total": &lt;int&gt;, "totalPages": &lt;int&gt; }

}

**41.7 Ràng buộc**

- Chỉ trả về phiếu có TrangThaiKham thuộc {0,1,2}; phiếu Hoàn thành (3) xem tại module Hồ sơ bệnh án.
- Bác sĩ chỉ xem được phiếu khám được chỉ định cho chính mình (MaNV trùng token); Admin xem được tất cả.
- Tham số trangThai (nếu truyền) chỉ nhận 0, 1 hoặc 2.

**41.8 Xử lý lỗi**

- Giá trị trangThai không hợp lệ → HTTP 400 | "Giá trị trạng thái không hợp lệ. Chỉ chấp nhận: 0 | 1 | 2"
- Định dạng ngayKham không hợp lệ → HTTP 400 | "Định dạng ngày lọc không hợp lệ. Vui lòng nhập theo định dạng DD - MM - YYYY!"
- Token hết hạn → HTTP 401 | "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại"
- Không có quyền → HTTP 403 | "Bạn không có quyền truy cập chức năng này"
- Lỗi hệ thống → HTTP 500 | "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại"

## 42\. XEM CHI TIẾT PHIẾU KHÁM (MÀN HÌNH KHÁM BỆNH)

**42.1 Tên API**

GET api/KhamBenh/{maPhieu}

**42.2 Mô tả**

Bác sĩ chọn 1 bệnh nhân từ danh sách chờ khám (mục 41) để xem toàn bộ thông tin phục vụ khám: thông tin hành chính BN, sinh hiệu hiện có, danh sách ICD đã ghi nhận, danh sách chỉ định CLS (kèm trạng thái/kết quả), đơn thuốc đã kê, kết luận khám hiện tại. API này cũng được module Hồ sơ bệnh án tái sử dụng để xem lại (kể cả phiếu đã Hoàn thành) ở chế độ chỉ xem.

**42.3 Phân quyền**

- Yêu cầu token hợp lệ trong Header Authorization
- Role được phép: BacSi, Admin
- BacSi chỉ được xem phiếu khám do chính mình phụ trách (MaNV trùng token); Admin xem được tất cả.

**42.4 Input (URL Parameter + Header)**

URL Parameter: - maPhieu &lt;string&gt; : Mã phiếu khám cần xem - BẮT BUỘC

Header: - Authorization &lt;string&gt; : "Bearer {token}" - BẮT BUỘC

Ví dụ: GET api/KhamBenh/PK_260609_001

**42.5 Quy trình xử lý tại server**

B1: Xác thực token và quyền truy cập

B2: Kiểm tra maPhieu tồn tại trong PhieuKham (404 nếu không)

B3: Nếu Role = BacSi → kiểm tra PhieuKham.MaNV = maNV của bác sĩ đăng nhập (403 nếu không khớp)

B4: JOIN BenhNhan lấy thông tin hành chính bệnh nhân theo MaBN

B5: JOIN NhanVien lấy tên bác sĩ theo MaNV

B6: Lấy danh sách ChiTietPhieuKhamICD JOIN DanhMucICD theo MaPhieu

B7: Lấy danh sách DichVuYTe JOIN ChiTietDichVuYTe theo MaPhieu (chỉ định CLS, kèm KetQua và TrangThaiDichVu - 2 mức)

B8: Lấy DonThuoc (nếu có, 1 đơn/phiếu theo quy ước) JOIN ChiTietDonThuoc JOIN DanhMucThuoc theo MaPhieu

B9: Trả về HTTP 200 kèm toàn bộ dữ liệu tổng hợp

**42.6 Output (khi thành công) - HTTP 200 OK**

{

"data": {

"maPhieu": &lt;string&gt;, "trangThaiKham": &lt;int&gt;, "ngayKham": &lt;string&gt;, "lyDoKham": &lt;string&gt;,

"ketLuan": &lt;string&gt;, // Kết luận/chẩn đoán lâm sàng (có thể null)

"maBacSi": &lt;string&gt;, "tenBacSi": &lt;string&gt;,

"benhNhan": {

"maBN": &lt;string&gt;, "hoTen": &lt;string&gt;, "ngaySinh": &lt;string&gt;, "gioiTinh": &lt;string&gt;,

"sdt": &lt;string&gt;, "diaChi": &lt;string&gt;, "tienSuBenh": &lt;string&gt;

},

"sinhHieu": {

"mach": &lt;int&gt;, "nhietDo": &lt;float&gt;, "huyetAp": &lt;string&gt;, "canNang": &lt;float&gt;, "chieuCao": &lt;float&gt;

},

// \[CẬP NHẬT\] icdList thuộc nhóm "Khám cơ bản" - nhập cùng lúc với sinh hiệu, KHÔNG còn nằm ở nhóm Kết luận khám

"icdList": \[ { "maICD": &lt;string&gt;, "tenBenh": &lt;string&gt; } \],

// \[CẬP NHẬT\] trạng thái CLS rút gọn còn 2 mức

"chiDinhCLS": \[

{ "maChiTiet": &lt;int&gt;, "maDV": &lt;string&gt;, "tenDV": &lt;string&gt;, "giaTien": &lt;decimal(18,2)&gt;,

"ketQua": &lt;string&gt;,

"trangThaiCLS": &lt;int&gt; // 0 = Chưa thực hiện | 1 = Đã làm CLS

}

\],

"donThuoc": {

"maDonThuoc": &lt;string&gt;, "loiDan": &lt;string&gt;,

"chiTiet": \[

{ "maThuoc": &lt;string&gt;, "tenThuoc": &lt;string&gt;, "soLuong": &lt;int&gt;, "cachDung": &lt;string&gt;,

"trangThaiPhatThuoc": &lt;bool&gt; }

\]

}

}

}

**42.7 Ràng buộc**

- maPhieu trên URL phải tồn tại trong PhieuKham.
- BacSi chỉ xem được phiếu khám do chính mình phụ trách; nếu không khớp → HTTP 403.

**\[CẬP NHẬT\]** icdList lấy trực tiếp từ ChiTietPhieuKhamICD - đây là dữ liệu do bác sĩ nhập/chỉnh sửa Ở BƯỚC KHÁM CƠ BẢN (khác bản đặc tả cũ - trước đây gắn với bước Kết luận khám).

- Mỗi phiếu khám chỉ có tối đa 1 bản ghi DonThuoc theo quy ước nghiệp vụ.

**42.8 Xử lý lỗi**

- maPhieu không tồn tại → HTTP 404 | "Không tìm thấy phiếu khám cần xem"
- Bác sĩ xem phiếu không do mình phụ trách → HTTP 403 | "Bạn không có quyền xem phiếu khám này"
- Token hết hạn → HTTP 401 | "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại"
- Không có quyền → HTTP 403 | "Bạn không có quyền truy cập chức năng này"
- Lỗi hệ thống → HTTP 500 | "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại"

## 43\. CẬP NHẬT THÔNG TIN KHÁM BỆNH (SINH HIỆU + ICD / CHỈ ĐỊNH CLS / TRẠNG THÁI CLS / ĐƠN THUỐC / KẾT LUẬN)

**43.1 Tên API**

PUT api/KhamBenh/{maPhieu}

**43.2 Mô tả**

API DUY NHẤT dùng chung cho các hành động: "Lưu khám cơ bản" (sinh hiệu + ICD, chuyển trangThaiKham = 1 ), "Chỉ định/cập nhật trạng thái CLS", "Kê đơn thuốc", "Kết luận & Hoàn thành khám" (chuyển trangThaiKham = 3) - chỉ khác nhau ở nhóm field gửi lên. Client gửi nhóm nào thì API cập nhật nhóm đó.

**Bảng tổng hợp thay đổi cấu trúc Request Body so với bản trước**

| **Nhóm field** | **Bản trước**                                        | **Bản này \[CẬP NHẬT\]**                                                                          |
| -------------- | ---------------------------------------------------- | ------------------------------------------------------------------------------------------------- |
| ICD            | Thuộc nhóm "Kết luận khám" (icdList, ghi đè toàn bộ) | Thuộc nhóm "Khám cơ bản" - nhập cùng sinh hiệu; bắt buộc có ít nhất 1 ICD mới cho lưu khám cơ bản |
| Trạng thái CLS | Không có - chỉ có chiDinhCLSMoi (thêm chỉ định)      | Thêm field trangThaiCLS (toggle 2 chiều Chưa thực hiện ⇄ Đã làm CLS)                              |
| Kê đơn thuốc   | Không kiểm tra tồn kho                               | Bắt buộc kiểm tra & trừ LoThuoc theo FEFO; chặn nếu còn CLS chưa làm hoặc không đủ tồn kho        |
| Kết luận khám  | Nhận icdList (ghi đè ICD)                            | KHÔNG nhận icdList nữa - chỉ nhận ketLuan (ICD chỉ hiển thị lại, đọc từ nhóm Khám cơ bản)         |

**43.3 Phân quyền**

- Yêu cầu token hợp lệ trong Header Authorization
- Role được phép: BacSi, Admin
- BacSi chỉ được sửa phiếu khám do chính mình phụ trách (MaNV trùng token); Admin sửa được tất cả.
- Phiếu khám đã Hoàn thành (3) chỉ Admin mới được sửa lại; BacSi bị từ chối.

**43.4 Input (URL Parameter + Header + Request Body - JSON)**

URL Parameter: - maPhieu &lt;string&gt; : Mã phiếu khám cần cập nhật - BẮT BUỘC

Header: - Authorization &lt;string&gt; : "Bearer {token}" - BẮT BUỘC

Body (tất cả nhóm bên dưới đều TUỲ CHỌN, gửi nhóm nào cập nhật nhóm đó):

\-- Nhóm 1: Khám cơ bản (Sinh hiệu + ICD) -- \[CẬP NHẬT cấu trúc nhóm\]

\- mach &lt;int&gt; : Mạch (lần/phút) - Tuỳ chọn

\- nhietDo &lt;float&gt; : Nhiệt độ (°C) - Tuỳ chọn

\- huyetAp &lt;string&gt; : "SBP/DBP" - Tuỳ chọn

\- canNang &lt;float&gt; : Cân nặng (kg) - Tuỳ chọn

\- chieuCao &lt;float&gt; : Chiều cao (cm) - Tuỳ chọn

\- icdList &lt;array<string&gt;>: Danh sách maICD chẩn đoán - ghi đè toàn bộ ChiTietPhieuKhamICD

BẮT BUỘC không rỗng nếu muốn chuyển trangThaiKham 0 → 1 \[MỚI\]

\-- Nhóm 2: Chỉ định CLS mới (chỉ thêm, không xoá chỉ định cũ) --

\- chiDinhCLSMoi &lt;array<string&gt;> : Danh sách maDV cần chỉ định thêm - Tuỳ chọn

\-- Nhóm 3: Trạng thái CLS (nếu có) -- \[MỚI\]

\- trangThaiCLS &lt;array&gt; : Tuỳ chọn - chỉ gửi nếu phiếu có chỉ định CLS

\- maChiTiet &lt;int&gt; : PK bảng DichVuYTe (chỉ định CLS cụ thể của phiếu)

\- daLamCLS &lt;bool&gt; : true = chuyển TrangThaiDichVu = 1 (Đã làm CLS)

false = chuyển lại 0 (Chưa thực hiện) - cho phép toggle 2 chiều

\-- Nhóm 4: Đơn thuốc (REPLACE toàn bộ danh sách thuốc CHƯA PHÁT của đơn hiện tại) --

\- loiDan &lt;string&gt; : Lời dặn/hướng điều trị - Tuỳ chọn

\- donThuoc &lt;array&gt; : Danh sách thuốc kê toa - Tuỳ chọn

\- maThuoc &lt;string&gt; : Mã thuốc (FK DanhMucThuoc) - BẮT BUỘC nếu có

\- soLuong &lt;int&gt; : Số lượng - BẮT BUỘC nếu có

\- cachDung &lt;string&gt; : Cách dùng - BẮT BUỘC nếu có

\-- Nhóm 5: Kết luận khám -- \[CẬP NHẬT - bỏ icdList\]

\- ketLuan &lt;string&gt; : Chẩn đoán/kết luận lâm sàng - Tuỳ chọn

\* KHÔNG còn nhận icdList ở nhóm này - ICD đọc lại từ Nhóm 1 (read-only tại bước này)

\-- Chuyển trạng thái phiếu khám --

\- trangThaiKham &lt;int&gt; : 1 = Đang khám | 2 = Chờ CLS | 3 = Hoàn thành - Tuỳ chọn

Ví dụ Request (Lưu khám cơ bản - sinh hiệu + ICD):

PUT api/KhamBenh/PK_260609_001

{

"mach": 78, "nhietDo": 37.0, "huyetAp": "120/80", "canNang": 65, "chieuCao": 168,

"icdList": \["J06"\],

"trangThaiKham": 1

}

Ví dụ Request (Cập nhật trạng thái CLS):

PUT api/KhamBenh/PK_260609_001

{ "trangThaiCLS": \[ { "maChiTiet": 15, "daLamCLS": true } \] }

Ví dụ Request (Kê đơn thuốc):

PUT api/KhamBenh/PK_260609_001

{

"loiDan": "Uống nhiều nước, tái khám nếu sốt trên 3 ngày",

"donThuoc": \[ { "maThuoc": "TH001", "soLuong": 10, "cachDung": "Uống 1 viên x 2 lần/ngày x 5 ngày" } \]

}

Ví dụ Request (Hoàn thành khám):

PUT api/KhamBenh/PK_260609_001

{ "ketLuan": "Viêm họng cấp", "trangThaiKham": 3 }

**43.5 Quy trình xử lý tại server**

B1: Xác thực token và quyền truy cập

B2: Kiểm tra maPhieu tồn tại trong PhieuKham (404 nếu không)

B3: Nếu Role = BacSi → kiểm tra PhieuKham.MaNV = maNV bác sĩ đăng nhập (403 nếu không khớp)

B4: Nếu TrangThaiKham hiện tại = 3 (Hoàn thành) và Role != Admin → từ chối (403)

B5: \[Nhóm 1 - Khám cơ bản\] Nếu body có sinh hiệu và/hoặc icdList:

\- Validate và UPDATE các cột sinh hiệu tương ứng trong PhieuKham

\- Nếu có icdList → DELETE toàn bộ ChiTietPhieuKhamICD của maPhieu rồi INSERT lại

theo danh sách maICD mới (validate từng maICD tồn tại trong DanhMucICD)

\- \[MỚI\] Nếu request có trangThaiKham chuyển 0 → 1: bắt buộc icdList (đã lưu hoặc

đang gửi kèm) không được rỗng → nếu rỗng, từ chối (400)

B6: \[Nhóm 2 - Chỉ định CLS mới\] Nếu body có chiDinhCLSMoi → với mỗi maDV:

\- Validate maDV tồn tại trong ChiTietDichVuYTe và đang hoạt động (TrangThai = 1)

\- Kiểm tra chưa tồn tại chỉ định trùng maDV cho phiếu này

\- INSERT vào DichVuYTe (MaPhieu, MaDV, TrangThaiDichVu = 0)

B7: \[Nhóm 3 - Trạng thái CLS, MỚI\] Nếu body có trangThaiCLS → với mỗi phần tử:

\- Validate maChiTiet tồn tại trong DichVuYTe và thuộc đúng maPhieu này

\- UPDATE DichVuYTe.TrangThaiDichVu = (daLamCLS ? 1 : 0)

\- Cho phép toggle 2 chiều tự do (không có ràng buộc "đã làm rồi không được sửa lại")

B8: \[Nhóm 4 - Đơn thuốc, MỚI: kiểm tra + trừ kho FEFO\] Nếu body có donThuoc hoặc loiDan:

\- \[MỚI\] Chặn cứng nếu phiếu còn ít nhất 1 dòng DichVuYTe có TrangThaiDichVu = 0

(còn CLS chưa thực hiện xong) → từ chối (409)

\- Nếu phiếu chưa có DonThuoc → INSERT bản ghi DonThuoc mới (MaDonThuoc tự sinh,

NgayKeDon = GETDATE(), LoiDan); nếu đã có → UPDATE LoiDan

\- Với danh sách donThuoc gửi lên, với TỪNG maThuoc:

(a) Validate maThuoc tồn tại và IsActive = 1 trong DanhMucThuoc

(b) Tính tồn khả dụng = SUM(LoThuoc.SoLuongTon) của các lô CHƯA hết hạn

(HanSuDung >= GETDATE()) của maThuoc đó

(c) Nếu tồn khả dụng < soLuong yêu cầu → CHẶN CỨNG, từ chối cả request (409)

(d) Nếu đủ → trừ theo FEFO: sắp lô theo HanSuDung tăng dần, trừ dần từng lô

cho đến khi đủ soLuong (có thể trừ qua nhiều lô)

\- Xoá các dòng ChiTietDonThuoc cũ có TrangThaiPhatThuoc = 0 (chưa phát) rồi INSERT

lại theo danh sách mới; các dòng đã TrangThaiPhatThuoc = 1 được giữ nguyên

\- \[MỚI\] Toàn bộ (b)-(d) + INSERT ChiTietDonThuoc chạy trong CÙNG 1 transaction -

nếu bất kỳ maThuoc nào không đủ tồn, ROLLBACK toàn bộ, không kê một phần

B9: \[Nhóm 5 - Kết luận khám\] Nếu body có ketLuan → UPDATE cột KetLuan trong PhieuKham

\* KHÔNG xử lý icdList ở bước này nữa (đã chuyển sang B5)

B10: Nếu body có trangThaiKham → validate thuộc {1, 2, 3}

\- Nếu = 3 (Hoàn thành) → bắt buộc KetLuan không rỗng

\- UPDATE cột TrangThaiKham

B11: Trả về HTTP 200 kèm toàn bộ dữ liệu phiếu khám sau cập nhật (cấu trúc như Output 42.6)

**43.6 Output (khi thành công) - HTTP 200 OK**

{

"message": "Cập nhật thông tin khám bệnh thành công",

"data": { ... } // Cấu trúc giống hệt Output mục 42.6

}

**43.7 Ràng buộc**

- maPhieu trên URL phải tồn tại trong PhieuKham.
- BacSi chỉ được sửa phiếu khám do chính mình phụ trách; Admin sửa được tất cả.
- Phiếu đã Hoàn thành (3) chỉ Admin mới được sửa lại.

**\[CẬP NHẬT\]** icdList thuộc nhóm Khám cơ bản; bắt buộc không rỗng để chuyển trangThaiKham 0→1. Ở bước Kết luận khám không nhận icdList nữa.

**\[CẬP NHẬT\]** Trạng thái CLS (trangThaiCLS) chỉ có 2 mức, bác sĩ toggle tự do 2 chiều, không giới hạn số lần đổi.

**\[CẬP NHẬT\]** Chặn kê đơn thuốc nếu phiếu còn dịch vụ CLS ở trạng thái Chưa thực hiện (0) - phải hoàn tất toàn bộ CLS trước khi qua bước kê thuốc.

**\[CẬP NHẬT\]** Kê thuốc: BẮT BUỘC đủ tồn kho (SUM SoLuongTon các lô chưa hết hạn) mới cho kê; không có chế độ ghi nhận thiếu hụt. Trừ kho theo FEFO, cùng transaction với insert đơn thuốc.

- Mã dịch vụ CLS (maDV) chỉ định thêm phải tồn tại trong ChiTietDichVuYTe và TrangThai = 1; không chỉ định trùng maDV đã có cho cùng phiếu.
- Mã thuốc (maThuoc) phải tồn tại trong DanhMucThuoc và IsActive = 1.
- Chỉ được xoá/ghi đè các dòng thuốc chưa phát (TrangThaiPhatThuoc = 0); dòng đã phát (1) không bị xoá qua API này.
- Giá trị trangThaiKham chỉ nhận 1, 2 hoặc 3 - không set về 0 qua API này.
- Muốn chuyển trangThaiKham = 3 thì KetLuan không được rỗng.

**43.8 Xử lý lỗi**

- maPhieu không tồn tại → HTTP 404 | "Không tìm thấy phiếu khám cần cập nhật"
- Bác sĩ sửa phiếu không do mình phụ trách → HTTP 403 | "Bạn không có quyền cập nhật phiếu khám này"
- Sửa phiếu đã Hoàn thành (không phải Admin) → HTTP 403 | "Phiếu khám đã hoàn thành, không thể chỉnh sửa"
- Giá trị sinh hiệu không hợp lệ → HTTP 400 | "Giá trị sinh hiệu không hợp lệ. Vui lòng nhập lại"
- Huyết áp sai định dạng → HTTP 400 | "Huyết áp không đúng định dạng (VD: 120/80)"
- Mã bệnh ICD không tồn tại → HTTP 400 | "Mã bệnh ICD không tồn tại trong danh mục. Vui lòng kiểm tra lại"
- \[MỚI\] Chuyển trangThaiKham=1 nhưng chưa có ICD nào → HTTP 400 | "Vui lòng nhập ít nhất 1 chẩn đoán ICD trước khi lưu khám cơ bản!"
- Mã dịch vụ CLS không tồn tại/ngưng hoạt động → HTTP 400 | "Dịch vụ CLS không tồn tại hoặc đã ngừng cung cấp. Vui lòng kiểm tra lại"
- Chỉ định CLS trùng → HTTP 409 | "Dịch vụ CLS này đã được chỉ định cho phiếu khám"
- \[MỚI\] maChiTiet trong trangThaiCLS không tồn tại hoặc không thuộc phiếu này → HTTP 400 | "Chỉ định CLS không hợp lệ hoặc không thuộc phiếu khám này"
- \[MỚI\] Kê đơn thuốc khi còn CLS chưa thực hiện → HTTP 409 | "Còn dịch vụ cận lâm sàng chưa thực hiện xong. Vui lòng hoàn tất CLS trước khi kê thuốc!"
- Mã thuốc không tồn tại/ngưng sử dụng → HTTP 400 | "Thuốc không tồn tại hoặc đã ngừng sử dụng. Vui lòng kiểm tra lại"
- Số lượng thuốc không hợp lệ → HTTP 400 | "Số lượng thuốc phải là số nguyên dương. Vui lòng nhập lại"
- \[MỚI\] Không đủ tồn kho cho 1 hoặc nhiều thuốc → HTTP 409 | "Thuốc '{tenThuoc}' không đủ tồn kho (còn {soLuongTon}, yêu cầu {soLuong}). Vui lòng kiểm tra lại hoặc liên hệ kho thuốc!"
- Chuyển trangThaiKham=3 nhưng chưa có Kết luận khám → HTTP 400 | "Vui lòng nhập Kết luận khám trước khi hoàn thành"
- Giá trị trangThaiKham không hợp lệ → HTTP 400 | "Giá trị trạng thái không hợp lệ. Chỉ chấp nhận: 1 | 2 | 3"
- Token hết hạn → HTTP 401 | "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại"
- Không có quyền → HTTP 403 | "Bạn không có quyền truy cập chức năng này"
- Lỗi hệ thống → HTTP 500 | "Không thể cập nhật dữ liệu từ máy chủ. Xin hãy thử lại"

# 5\. CONTROLLER: HoSoBenhAnController.cs (chỉ đọc)

**Base Route:** api/HoSoBenhAn

**_Ghi chú:_** _"Hồ sơ bệnh án" = bảng BenhNhan hiện có. MaBN đóng vai trò vừa là mã bệnh nhân vừa là mã hồ sơ - tra cứu theo "mã hồ sơ" hay "mã bệnh nhân" đều là cùng 1 thao tác tra cứu theo MaBN, không có 2 mã khác nhau. Không có thay đổi logic ở 3 mục dưới so với bản trước, chỉ cập nhật lại thuật ngữ cho khớp nghiệp vụ._

## 44\. TRA CỨU HỒ SƠ BỆNH ÁN

**44.1 Tên API**

GET api/HoSoBenhAn/tra-cuu?query={tuKhoa}

**44.2 Mô tả**

Bác sĩ/Admin nhập mã bệnh nhân (= mã hồ sơ), mã phiếu khám, SĐT hoặc họ tên để tra cứu thông tin hành chính của 1 bệnh nhân. Ưu tiên khớp CHÍNH XÁC theo maBN hoặc maPhieu trước; nếu không có kết quả, tìm khớp GẦN ĐÚNG theo maBN, maPhieu, hoTen hoặc sdt và trả kết quả đầu tiên tìm thấy.

**44.3 Phân quyền**

- Yêu cầu token hợp lệ trong Header Authorization
- Role được phép: BacSi, Admin

**44.4 Input**

Header: - Authorization &lt;string&gt; : "Bearer {token}" - BẮT BUỘC

Query: - query &lt;string&gt; : Từ khoá tra cứu (mã BN/mã hồ sơ / mã phiếu / SĐT / họ tên) - BẮT BUỘC

Ví dụ: GET api/HoSoBenhAn/tra-cuu?query=BN260001

**44.5 Quy trình xử lý tại server**

B1: Xác thực token và quyền truy cập

B2: Kiểm tra query không rỗng

B3: Tìm PhieuKham JOIN BenhNhan có MaPhieu = query hoặc MaBN = query (khớp chính xác)

B4: Nếu không tìm thấy ở B3 → tìm gần đúng (LIKE) theo MaPhieu, MaBN, HoTen, SDT,

lấy bản ghi PhieuKham có NgayKham mới nhất

B5: Không tìm thấy → HTTP 404

B6: Trả về HTTP 200 kèm thông tin hành chính bệnh nhân

**44.6 Output (khi tìm thấy) - HTTP 200 OK**

{

"found": true,

"data": { "maBN": &lt;string&gt;, "hoTen": &lt;string&gt;, "ngaySinh": &lt;string&gt;, "gioiTinh": &lt;string&gt;,

"sdt": &lt;string&gt;, "diaChi": &lt;string&gt;, "tienSuBenh": &lt;string&gt; }

}

**44.7 Output (khi không tìm thấy) - HTTP 404 Not Found**

{ "found": false, "message": "Không tìm thấy thông tin bệnh nhân nào trùng khớp!" }

**44.8 Xử lý lỗi**

- Không nhập từ khoá → HTTP 400 | "Vui lòng nhập mã bệnh nhân hoặc mã hồ sơ khám bệnh để tra cứu!"
- Token hết hạn → HTTP 401 | "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại"
- Không có quyền → HTTP 403 | "Bạn không có quyền truy cập chức năng này"
- Lỗi hệ thống → HTTP 500 | "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại"

## 45\. LẤY DANH SÁCH BỆNH NHÂN VỪA KHÁM GẦN ĐÂY

**45.1 Tên API**

GET api/HoSoBenhAn/gan-day

**45.2 Mô tả**

Trả về tối đa 5 bệnh nhân có lượt khám/tiếp đón gần nhất (duy nhất theo MaBN), hiển thị dạng gợi ý chọn nhanh trên màn hình Hồ sơ bệnh án.

**45.3 Phân quyền**

- Yêu cầu token hợp lệ trong Header Authorization
- Role được phép: BacSi, Admin

**45.4 Input**

Header: - Authorization &lt;string&gt; : "Bearer {token}" - BẮT BUỘC

Ví dụ: GET api/HoSoBenhAn/gan-day

**45.5 Quy trình xử lý tại server**

B1: Xác thực token và quyền truy cập

B2: Truy vấn PhieuKham JOIN BenhNhan, sắp xếp NgayKham DESC

B3: Lọc duy nhất theo MaBN (lấy lượt khám mới nhất của mỗi bệnh nhân)

B4: Giới hạn tối đa 5 bản ghi

B5: Trả về HTTP 200 kèm danh sách

**45.6 Output (khi thành công) - HTTP 200 OK**

{

"data": \[

{ "maBN": &lt;string&gt;, "hoTen": &lt;string&gt;, "ngaySinh": &lt;string&gt;, "gioiTinh": &lt;string&gt;,

"sdt": &lt;string&gt;, "diaChi": &lt;string&gt;, "tienSuBenh": &lt;string&gt;, "lastVisit": &lt;string&gt; }

\]

}

**45.7 Xử lý lỗi**

- Token hết hạn → HTTP 401 | "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại"
- Không có quyền → HTTP 403 | "Bạn không có quyền truy cập chức năng này"
- Lỗi hệ thống → HTTP 500 | "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại"

## 46\. LẤY LỊCH SỬ KHÁM BỆNH CỦA 1 BỆNH NHÂN

**46.1 Tên API**

GET api/HoSoBenhAn/{maBN}/lich-su

**46.2 Mô tả**

Sau khi tra cứu ra 1 bệnh nhân (mục 44) hoặc chọn từ danh sách gần đây (mục 45), lấy toàn bộ các lượt khám (mọi trạng thái) của bệnh nhân đó, mới nhất lên đầu; bấm vào xem chi tiết dùng chung API mục 42.

**46.3 Phân quyền**

- Yêu cầu token hợp lệ trong Header Authorization
- Role được phép: BacSi, Admin

**46.4 Input**

URL Parameter: - maBN &lt;string&gt; : Mã bệnh nhân (= mã hồ sơ) cần xem lịch sử - BẮT BUỘC

Header: - Authorization &lt;string&gt; : "Bearer {token}" - BẮT BUỘC

Ví dụ: GET api/HoSoBenhAn/BN260001/lich-su

**46.5 Quy trình xử lý tại server**

B1: Xác thực token và quyền truy cập

B2: Kiểm tra maBN tồn tại trong BenhNhan (404 nếu không)

B3: Truy vấn PhieuKham JOIN NhanVien (bác sĩ) theo MaBN

B4: Với mỗi phiếu, lấy kèm ChiTietPhieuKhamICD JOIN DanhMucICD

B5: Sắp xếp theo NgayKham DESC

B6: Trả về HTTP 200 kèm danh sách lịch sử khám

**46.6 Output (khi thành công) - HTTP 200 OK**

{

"data": \[

{ "maPhieu": &lt;string&gt;, "ngayKham": &lt;string&gt;, "trangThaiKham": &lt;int&gt;,

"tenBacSi": &lt;string&gt;, "ketLuan": &lt;string&gt;,

"icdList": \[ { "maICD": &lt;string&gt;, "tenBenh": &lt;string&gt; } \] }

\]

}

**46.7 Xử lý lỗi**

- maBN không tồn tại → HTTP 404 | "Không tìm thấy hồ sơ bệnh nhân"
- Token hết hạn → HTTP 401 | "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại"
- Không có quyền → HTTP 403 | "Bạn không có quyền truy cập chức năng này"
- Lỗi hệ thống → HTTP 500 | "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại"

# 6\. GHI CHÚ CHUNG TOÀN BỘ ĐẶC TẢ

**6.1 HTTP Status Code quy chuẩn**

200 OK → Thành công (GET)

201 Created → Tạo mới thành công (POST)

400 Bad Request → Dữ liệu đầu vào không hợp lệ

401 Unauthorized → Token không hợp lệ / hết hạn

403 Forbidden → Không có quyền truy cập

404 Not Found → Không tìm thấy tài nguyên

409 Conflict → Xung đột dữ liệu (SĐT trùng, phiếu đang active, CLS chưa xong, thiếu tồn kho, ...)

500 Server Error → Lỗi server hoặc database

**6.2 Quy ước route & JSON**

- camelCase cho toàn bộ field JSON

**6.3 Phạm vi KHÔNG thuộc đặc tả này (để giai đoạn sau)**

- Đặt lịch khám online (đặt lịch, chọn slot giờ, xử lý no-show, liên kết DatLichKham → PhieuKham).
- Gợi ý bác sĩ theo triệu chứng/chuyên khoa và theo ca trực.
- Vai trò KTV (Kỹ thuật viên) nhập kết quả CLS riêng biệt với bác sĩ.
- Chế độ ghi nhận thiếu hụt tồn kho khi kê thuốc (đợt này chỉ chặn cứng).

_- Hết đặc tả -_