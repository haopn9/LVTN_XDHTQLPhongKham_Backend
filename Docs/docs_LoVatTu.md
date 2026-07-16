================================================================
Controller : KhoVatTuController.cs
Base Route : api/KhoVatTu
Phân quyền : Admin, QuanLyKhoThuoc (Quản lý kho thuốc)
================================================================

41. Lấy danh sách lô vật tư nhập kho
================================================================

41.1 Tên API
GET api/KhoVatTu

41.2 Mô tả
Lấy danh sách lô vật tư nhập kho từ bảng LoVatTu, có hỗ trợ
lọc theo mã lô / tên vật tư / tên nhà cung cấp / trạng thái hạn sử dụng và phân trang.

41.3 Phân quyền
- Yêu cầu token hợp lệ trong Header Authorization
- Tài khoản có Role = Admin, QuanLyKhoThuoc mới được gọi API này

41.4 Input (Query Parameters)
Header:
- Authorization <string> : "Bearer {token}" - BẮT BUỘC

Query Parameters (tùy chọn):
- maLo <string> : Lọc theo mã lô vật tư (tìm kiếm gần đúng, không phân biệt hoa thường)
- tenVatTu <string> : Lọc theo tên vật tư (tìm kiếm gần đúng, không phân biệt hoa thường)
- tenNCC <string> : Lọc theo tên nhà cung cấp (tìm kiếm gần đúng, không phân biệt hoa thường)
- hanSuDung <string> : Lọc theo hạn sử dụng của lô vật tư ({Tất cả},{An toàn},{Hạn ngắn (<6 th)},{Đã hết hạn})
- page <int> : Số trang hiện tại (mặc định: 1)
- pageSize <int> : Số bản ghi mỗi trang (mặc định: 10)

Ví dụ Request:
GET api/KhoVatTu → Danh sách lô vật tư theo quyền, trang 1
GET api/KhoVatTu?maLo=LV26001&page=1&pageSize=10
GET api/KhoVatTu?tenVatTu=Khẩu trang y tế 4 lớp
GET api/KhoVatTu?tenNCC=Công ty cổ phần dược phẩm OPC
GET api/KhoVatTu?hanSuDung=An toàn

41.5 Quy trình xử lý tại server
B1: Xác thực token và quyền (Admin, QuanLyKhoThuoc)
B2: Kiểm tra Role của tài khoản đang gọi API:
- Nếu Role = Admin hoặc QuanLyKhoThuoc
→ Lấy tất cả bản ghi trong LoVatTu
B3: Áp dụng bộ lọc maLo, tenVatTu, tenNCC, hanSuDung
B4: Đếm tổng số bản ghi sau lọc (total)
B5: Phân trang và trả về kết quả

41.6 Output (khi thành công) - HTTP 200 OK
{
"data": [
{
"stt": <int>, // Số thứ tự (tính theo trang hiện tại)
"maLo": <string>, // Mã lô vật tư (từ bảng LoVatTu)
"maVatTu": <string>, // Mã vật tư (từ bảng LoVatTu)
"tenVatTu": <string>, // Tên vật tư (từ bảng DanhMucVatTu)
"maNCC": <int>, // Mã nhà cung cấp (từ bảng LoVatTu)
"tenNCC": <string>, // Tên nhà cung cấp (từ bảng NhaCungCap)
"soLuongNhap": <int>, // Số lượng nhập về của lô vật tư (từ bảng LoVatTu)
"soLuongTon": <int>, // Số lượng tồn của lô vật tư (từ bảng LoVatTu)
"giaBan": <decimal(18,2)>, // Giá bán của lô vật tư (từ bảng LoVatTu)
"trangThaiHSD": <string> // Trạng thái hạn sử dụng của lô vật tư
}
],
"total": <int>, // Tổng số bản ghi sau khi lọc
"page": <int>, // Trang hiện tại
"pageSize": <int>, // Số bản ghi mỗi trang
"totalPages": <int> // Tổng số trang
}

41.7 Ràng buộc
41.7.1 Trạng thái của hạn sử dụng bao gồm: {An toàn},{Hạn ngắn (<6 th)},{Đã hết hạn}
TrangThaiHSD được tính dựa trên thời gian còn lại tính từ ngày hiện tại đến hạn sử dụng:
ThoiGianConLai = hanSuDung - TODAY()
- Nếu ThoiGianConLai >= 180 ngày → TrangThaiHSD: "An toàn"
- Nếu 0 < ThoiGianConLai < 180 ngày → TrangThaiHSD: "Hạn ngắn (<6 th)"
- Nếu ThoiGianConLai <= 0 → TrangThaiHSD: "Đã hết hạn"

41.8 Xử lý lỗi
41.8.1 Giá trị page hoặc pageSize không hợp lệ (số âm, bằng 0, không phải số)
→ HTTP 400 Bad Request
→ Thông báo: "Giá trị phân trang không hợp lệ"

41.8.2 Token không hợp lệ hoặc hết hạn
→ HTTP 401 Unauthorized
→ Thông báo: "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại"

41.8.3 Tài khoản không có quyền truy cập
→ HTTP 403 Forbidden
→ Thông báo: "Bạn không có quyền truy cập chức năng này"

41.8.4 Hệ thống không kết nối được API hoặc Database
→ HTTP 500 Internal Server Error
→ Thông báo: "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại"

================================================================

42. Thêm lô vật tư nhập kho
================================================================

42.1 Tên API
POST api/KhoVatTu

42.2 Mô tả
Admin & quản lý kho thuốc thêm mới một lô vật tư nhập kho vào bảng LoVatTu.

42.3 Phân quyền
- Yêu cầu token hợp lệ trong Header Authorization
- Chỉ tài khoản có Role = Admin, QuanLyKhoThuoc (Quản lý kho thuốc) mới được gọi API này

42.4 Input (Header + Request Body - JSON)
Header:
- Authorization <string> : "Bearer {token}" - BẮT BUỘC

Body:
- maLo <string> : Mã lô vật tư - BẮT BUỘC (VD: LV26004, LV26005)
- maVatTu <string> : Mã vật tư (FK → DanhMucVatTu) - BẮT BUỘC (VD: VT001)
- maNCC <int> : Mã nhà cung cấp (FK → NhaCungCap) - BẮT BUỘC (VD: 1)
- soLuongNhap <int> : Số lượng nhập lô vật tư - BẮT BUỘC (VD: 100)
- soLuongTon <int> : Số lượng tồn lô vật tư - BẮT BUỘC (VD: 100)
- giaNhap <decimal(18,2)> : Giá nhập của lô vật tư - BẮT BUỘC (VD: 500)
- giaBan <decimal(18,2)> : Giá bán niêm yết của lô vật tư - BẮT BUỘC (VD: 1000)
- ngaySanXuat <date> : Ngày sản xuất của lô vật tư (VD: 01/01/2026)
- hanSuDung <date> : Hạn sử dụng của lô vật tư - BẮT BUỘC (VD: 31/12/2026)

Ví dụ Request Body:
{
"maLo": "LV26005",
"maVatTu": "VT001",
"maNCC": 1,
"soLuongNhap": 100,
"soLuongTon": 90,
"giaNhap": 500,
"giaBan": 1000,
"ngaySanXuat": "01/01/2026",
"hanSuDung": "31/12/2026"
}

42.5 Quy trình xử lý tại server
B1: Xác thực token và quyền Admin & QuanLyKhoThuoc
B2: Validate các trường bắt buộc (maLo, maVatTu, maNCC, soLuongNhap, soLuongTon, giaNhap, giaBan, hanSuDung)
B3: Trim và tự động chuyển maLo về chữ HOA
B4: Kiểm tra maLo trùng trong bảng LoVatTu
→ Trùng: HTTP 409
B5: Kiểm tra maVatTu tồn tại trong bảng DanhMucVatTu
→ Không tồn tại: HTTP 404
B6: Kiểm tra maNCC tồn tại trong bảng NhaCungCap
→ Không tồn tại: HTTP 404
B7: INSERT vào bảng LoVatTu
B8: Trả về HTTP 201 kèm bản ghi vừa tạo

42.6 Output (khi thành công) - HTTP 201 Created
{
"message": "Thêm mới lô vật tư nhập kho thành công",
"data": {
"maLo": <string>,
"maVatTu": <string>,
"tenVatTu": <string>,
"maNCC": <int>,
"tenNCC": <string>,
"soLuongNhap": <int>,
"soLuongTon": <int>,
"giaNhap": <decimal(18,2)>,
"giaBan": <decimal(18,2)>,
"ngaySanXuat": <date>,
"hanSuDung": <date>
}
}

42.7 Ràng buộc
42.7.1 Các trường bắt buộc: maLo, maVatTu, maNCC, soLuongNhap, soLuongTon, giaNhap, giaBan, hanSuDung

42.7.2 Mã lô vật tư (maLo)
- Không được để trống
- Không được chứa khoảng trắng
- Không được trùng với mã lô vật tư đã tồn tại trong bảng LoVatTu
- Độ dài tối đa 10 ký tự
- Không chứa ký tự đặc biệt (@, !, #, $, %, ...)
* Server tự động trim và chuyển về chữ HOA trước khi kiểm tra trùng và lưu

42.7.3 Mã vật tư (maVatTu)
- Phải tồn tại trong bảng DanhMucVatTu (bao gồm cả vật tư đang và ngưng sử dụng)

42.7.4 Mã nhà cung cấp (maNCC)
- Phải tồn tại trong bảng NhaCungCap

42.7.5 Số lượng nhập kho & số lượng tồn kho
- Không được để trống
- Phải là số nguyên dương >= 0
- Số lượng nhập kho >= số lượng tồn kho

42.7.6 Giá nhập đơn vị & giá bán niêm yết
- Không được để trống
- Phải là số dương >= 0
- Kiểu decimal(18,2): tối đa 16 chữ số phần nguyên, 2 chữ số thập phân

42.7.7 Ngày sản xuất & Hạn sử dụng
- Kiểu "dd/mm/yyyy"
- Ngày sản xuất <= hạn sử dụng

42.8 Xử lý lỗi
42.8.1 Không nhập mã lô vật tư
→ HTTP 400 | Thông báo: "Vui lòng nhập mã lô vật tư"

42.8.2 Không nhập số lượng nhập lô vật tư
→ HTTP 400 | Thông báo: "Vui lòng nhập số lượng nhập của lô vật tư"

42.8.3 Không nhập số lượng tồn kho của lô vật tư
→ HTTP 400 | Thông báo: "Vui lòng nhập số lượng tồn kho của lô vật tư"

42.8.4 Không nhập giá nhập của lô vật tư
→ HTTP 400 | Thông báo: "Vui lòng nhập giá nhập của lô vật tư"

42.8.5 Không nhập giá bán niêm yết của lô vật tư
→ HTTP 400 | Thông báo: "Vui lòng nhập giá bán niêm yết của lô vật tư"

42.8.6 Không nhập hạn sử dụng của lô vật tư
→ HTTP 400 | Thông báo: "Vui lòng nhập hạn sử dụng của lô vật tư"

42.8.7 Mã lô vật tư chứa khoảng trắng
→ HTTP 400 | Thông báo: "Mã lô vật tư không được chứa khoảng trắng. Vui lòng nhập lại"

42.8.8 Mã lô vật tư vượt quá 10 ký tự
→ HTTP 400 | Thông báo: "Mã lô vật tư không được vượt quá 10 ký tự. Vui lòng nhập lại"

42.8.9 Mã lô vật tư chứa ký tự đặc biệt
→ HTTP 400 | Thông báo: "Mã lô vật tư không được chứa ký tự đặc biệt. Vui lòng nhập lại"

42.8.10 Mã lô vật tư đã tồn tại trong hệ thống
→ HTTP 409 Conflict
→ Thông báo: "Mã lô vật tư đã tồn tại trong hệ thống. Vui lòng kiểm tra & nhập lại mã khác"

42.8.11 Mã vật tư không tồn tại trong hệ thống
→ HTTP 404 Not Found
→ Thông báo: "Không tìm thấy danh mục vật tư tương ứng"

42.8.12 Mã nhà cung cấp không tồn tại trong hệ thống
→ HTTP 404 Not Found
→ Thông báo: "Không tìm thấy nhà cung cấp tương ứng"

42.8.13 Số lượng nhập là 1 số âm
→ HTTP 400 | Thông báo: "Số lượng nhập của lô vật tư phải là 1 số nguyên dương. Vui lòng nhập lại"

42.8.14 Số lượng tồn kho là 1 số âm
→ HTTP 400 | Thông báo: "Số lượng tồn kho của lô vật tư phải là 1 số nguyên dương. Vui lòng nhập lại"

42.8.15 Số lượng tồn kho lớn hơn số lượng nhập
→ HTTP 400 | Thông báo: "Số lượng tồn kho không được lớn hơn số lượng nhập về của lô vật tư. Vui lòng nhập lại"

42.8.16 Gía nhập về của lô vật tư là 1 số âm
→ HTTP 400 | Thông báo: "Gía nhập về của lô vật tư phải là 1 số dương. Vui lòng nhập lại"

42.8.17 Gía bán niêm yết của lô vật tư là 1 số âm
→ HTTP 400 | Thông báo: "Gía bán niêm yết của lô vật tư phải là 1 số dương. Vui lòng nhập lại"

42.8.18 Giá nhập về của lô vật tư vượt quá giới hạn decimal(18,2)
→ HTTP 400 | Thông báo: "Giá nhập về của lô vật tư không được vượt quá 16 chữ số phần nguyên. Vui lòng nhập lại"

42.8.19 Giá bán niêm yết của lô vật tư vượt quá giới hạn decimal(18,2)
→ HTTP 400 | Thông báo: "Giá bán niêm yết của lô vật tư không được vượt quá 16 chữ số phần nguyên. Vui lòng nhập lại"

42.8.20 Hạn sử dụng của lô vật tư < ngày sản xuất của lô vật tư
→ HTTP 400 | Thông báo: "Hạn sử dụng phải lớn hơn hoặc bằng ngày sản xuất của lô vật tư. Vui lòng nhập lại"

42.8.21 Token không hợp lệ hoặc hết hạn
→ HTTP 401 | Thông báo: "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại"

42.8.22 Tài khoản không có quyền truy cập
→ HTTP 403 | Thông báo: "Bạn không có quyền truy cập chức năng này"

42.8.23 Hệ thống không kết nối được API hoặc Database
→ HTTP 500 | Thông báo: "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại"

================================================================

43. Sửa thông tin lô vật tư nhập kho
================================================================

43.1 Tên API
PUT api/KhoVatTu/{maLo}

43.2 Mô tả
Admin & quản lý kho thuốc cập nhật thông tin lô vật tư: mã vật tư, mã nhà cung cấp, số lượng nhập, số lượng tồn kho, giá nhập, giá bán, ngày sản xuất, hạn sử dụng của một lô vật tư nhập kho đã tồn tại trong
bảng LoVatTu. Chỉ cho phép sửa maVatTu, maNCC, soLuongNhap, soLuongTon, giaNhap, giaBan, ngaySanXuat, hanSuDung - không cho sửa
maLo vì đây là PK.

43.3 Phân quyền
- Yêu cầu token hợp lệ trong Header Authorization
- Chỉ tài khoản có Role = Admin & QuanLyKhoThuoc (Quản lý kho thuốc) mới được gọi API này

43.4 Input (URL Parameter + Header + Request Body - JSON)
URL Parameter:
- maLo <string> : Mã lô vật tư cần cập nhật - BẮT BUỘC

Header:
- Authorization <string> : "Bearer {token}" - BẮT BUỘC

Body (JSON):
- maVatTu <string> : Mã vật tư (FK → DanhMucVatTu) - BẮT BUỘC (VD: VT001)
- maNCC <int> : Mã nhà cung cấp (FK → NhaCungCap) - BẮT BUỘC (VD: 1)
- soLuongNhap <int> : Số lượng nhập lô vật tư - BẮT BUỘC (VD: 100)
- soLuongTon <int> : Số lượng tồn lô vật tư - BẮT BUỘC (VD: 100)
- giaNhap <decimal(18,2)> : Giá nhập của lô vật tư - BẮT BUỘC (VD: 500)
- giaBan <decimal(18,2)> : Giá bán niêm yết của lô vật tư - BẮT BUỘC (VD: 1000)
- ngaySanXuat <date> : Ngày sản xuất của lô vật tư (VD: 01/01/2026)
- hanSuDung <date> : Hạn sử dụng của lô vật tư - BẮT BUỘC (VD: 31/12/2026)

Ví dụ Request:
PUT api/KhoVatTu/LV26001
{
"maVatTu": "VT001",
"maNCC": 1,
"soLuongNhap": 100,
"soLuongTon": 90,
"giaNhap": 500,
"giaBan": 1000,
"ngaySanXuat": "01/01/2026",
"hanSuDung": "31/12/2026"
}

43.5 Quy trình xử lý tại server
B1: Xác thực token và quyền Admin & QuanLyKhoThuoc
B2: Kiểm tra maLo tồn tại trong bảng LoVatTu
→ Không tồn tại: HTTP 404
B3: Validate maVatTu (có tồn tại trong bảng DanhMucVatTu)
B4: Validate maNCC (có tồn tại trong bảng NhaCungCap)
B5: Validate soLuongNhap
B6: Validate soLuongTon
B7: Validate giaNhap
B8: Validate giaBan
B9: Validate ngaySanXuat
B10: Validate hanSuDung
B11: UPDATE LoVatTu
B12: Trả về HTTP 200 kèm bản ghi đã cập nhật

43.6 Output (khi thành công) - HTTP 200 OK
{
"message": "Cập nhật thông tin lô vật tư thành công",
"data": {
"maLo": <string>,
"maVatTu": <string>,
"tenVatTu": <string>,
"maNCC": <int>,
"tenNCC": <string>,
"soLuongNhap": <int>,
"soLuongTon": <int>,
"giaNhap": <decimal(18,2)>,
"giaBan": <decimal(18,2)>,
"ngaySanXuat": <date>,
"hanSuDung": <date>
}
}

43.7 Ràng buộc
43.7.1 Trường được phép cập nhật: Chỉ cho phép sửa maVatTu, maNCC, soLuongNhap, soLuongTon, giaNhap, giaBan, ngaySanXuat, hanSuDung
(maLo là PK, không được phép thay đổi)

43.7.2 Lô vật tư cần cập nhật phải tồn tại trong bảng LoVatTu

43.7.3 Mã vật tư (maVatTu)
- Phải tồn tại trong bảng DanhMucVatTu (bao gồm cả vật tư đang và ngưng sử dụng)

43.7.4 Mã nhà cung cấp (maNCC)
- Phải tồn tại trong bảng NhaCungCap

43.7.5 Số lượng nhập kho & số lượng tồn kho
- Không được để trống
- Phải là số nguyên dương >= 0
- Số lượng nhập kho >= số lượng tồn kho

43.7.6 Giá nhập đơn vị & giá bán niêm yết
- Không được để trống
- Phải là số dương >= 0
- Kiểu decimal(18,2): tối đa 16 chữ số phần nguyên, 2 chữ số thập phân

43.7.7 Ngày sản xuất & Hạn sử dụng
- Kiểu "dd/mm/yyyy"
- Ngày sản xuất <= hạn sử dụng

43.8 Xử lý lỗi
43.8.1 maLo trên URL không tồn tại trong hệ thống
→ HTTP 404 Not Found
→ Thông báo: "Không tìm thấy thông tin lô vật tư cần cập nhật"

43.8.2 Không nhập số lượng nhập lô vật tư
→ HTTP 400 | Thông báo: "Vui lòng nhập số lượng nhập của lô vật tư"

43.8.3 Không nhập số lượng tồn kho của lô vật tư
→ HTTP 400 | Thông báo: "Vui lòng nhập số lượng tồn kho của lô vật tư"

43.8.4 Không nhập giá nhập của lô vật tư
→ HTTP 400 | Thông báo: "Vui lòng nhập giá nhập của lô vật tư"

43.8.5 Không nhập giá bán niêm yết của lô vật tư
→ HTTP 400 | Thông báo: "Vui lòng nhập giá bán niêm yết của lô vật tư"

43.8.6 Không nhập hạn sử dụng của lô vật tư
→ HTTP 400 | Thông báo: "Vui lòng nhập hạn sử dụng của lô vật tư"

43.8.7 Mã vật tư không tồn tại trong hệ thống
→ HTTP 404 Not Found
→ Thông báo: "Không tìm thấy danh mục vật tư tương ứng"

43.8.8 Mã nhà cung cấp không tồn tại trong hệ thống
→ HTTP 404 Not Found
→ Thông báo: "Không tìm thấy nhà cung cấp tương ứng"

43.8.9 Số lượng nhập là 1 số âm
→ HTTP 400 | Thông báo: "Số lượng nhập của lô vật tư phải là 1 số nguyên dương. Vui lòng nhập lại"

43.8.10 Số lượng tồn kho là 1 số âm
→ HTTP 400 | Thông báo: "Số lượng tồn kho của lô vật tư phải là 1 số nguyên dương. Vui lòng nhập lại"

43.8.11 Số lượng tồn kho lớn hơn số lượng nhập
→ HTTP 400 | Thông báo: "Số lượng tồn kho không được lớn hơn số lượng nhập về của lô vật tư. Vui lòng nhập lại"

43.8.12 Gía nhập về của lô vật tư là 1 số âm
→ HTTP 400 | Thông báo: "Gía nhập về của lô vật tư phải là 1 số dương. Vui lòng nhập lại"

43.8.13 Gía bán niêm yết của lô vật tư là 1 số âm
→ HTTP 400 | Thông báo: "Gía bán niêm yết của lô vật tư phải là 1 số dương. Vui lòng nhập lại"

43.8.14 Giá nhập về của lô vật tư vượt quá giới hạn decimal(18,2)
→ HTTP 400 | Thông báo: "Giá nhập về của lô vật tư không được vượt quá 16 chữ số phần nguyên. Vui lòng nhập lại"

43.8.15 Giá bán niêm yết của lô vật tư vượt quá giới hạn decimal(18,2)
→ HTTP 400 | Thông báo: "Giá bán niêm yết của lô vật tư không được vượt quá 16 chữ số phần nguyên. Vui lòng nhập lại"

43.8.16 Hạn sử dụng của lô vật tư < ngày sản xuất của lô vật tư
→ HTTP 400 | Thông báo: "Hạn sử dụng lớn hơn hoặc bằng ngày sản xuất của lô vật tư. Vui lòng nhập lại"

43.8.17 Token không hợp lệ hoặc hết hạn
→ HTTP 401 | Thông báo: "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại"

43.8.18 Tài khoản không có quyền truy cập
→ HTTP 403 | Thông báo: "Bạn không có quyền truy cập chức năng này"

43.8.19 Hệ thống không kết nối được API hoặc Database
→ HTTP 500 | Thông báo: "Không thể kết nối dữ liệu từ máy chủ. Xin hãy thử lại"

================================================================

44. Xóa lô vật tư nhập kho
================================================================

44.1 Tên API
DELETE api/KhoVatTu/{maLo}

44.2 Mô tả
Admin & quản lý kho thuốc xóa một lô vật tư trong bảng LoVatTu.

44.3 Phân quyền
- Yêu cầu token hợp lệ trong Header Authorization
- Chỉ tài khoản có Role = Admin & QuanLyKhoThuoc (Quản lý kho thuốc) mới được gọi API này

44.4 Input (URL Parameter + Header)
URL Parameter:
- maLo <string> : Mã lô vật tư cần xóa - BẮT BUỘC

Header:
- Authorization <string> : "Bearer {token}" - BẮT BUỘC

Ví dụ Request:
DELETE api/KhoVatTu/LV26001

44.5 Quy trình xử lý tại server
B1: Xác thực token và quyền Admin & QuanLyKhoThuoc
B2: Kiểm tra maLo tồn tại trong bảng LoVatTu
→ Không tồn tại: HTTP 404
B3: Kiểm tra lô vật tư đã từng bị trừ kho hay chưa (so sánh SoLuongTon với SoLuongNhap)
→ Nếu SoLuongTon < SoLuongNhap (lô đã từng được dùng để kê vật tư cho bệnh nhân qua cơ chế FEFO tại KhamBenhController): HTTP 409
B4: DELETE FROM LoVatTu WHERE MaLo = {maLo}
B5: Trả về HTTP 200 kèm thông tin bản ghi đã xóa

44.6 Output (khi xóa thành công - Hard Delete) - HTTP 200 OK
{
"message": "Xóa lô vật tư thành công",
"data": {
"maLo": <string>,
"maVatTu": <string>,
"tenVatTu": <string>,
"maNCC": <int>,
"tenNCC": <string>,
"soLuongNhap": <int>,
"soLuongTon": <int>,
"giaNhap": <decimal(18,2)>,
"giaBan": <decimal(18,2)>,
"ngaySanXuat": <date>,
"hanSuDung": <date>
}
}

44.7 Ràng buộc
44.7.1 Mã lô vật tư cần xóa phải tồn tại trong bảng LoVatTu
44.7.2 Lô vật tư chỉ được phép Hard Delete khi chưa từng bị trừ kho (SoLuongTon = SoLuongNhap), tức chưa từng được dùng để kê vật tư cho bệnh nhân.
44.7.3 Nếu SoLuongTon < SoLuongNhap → chặn xóa, để đảm bảo tính toàn vẹn dữ liệu lịch sử kê vật tư trong ChiTietVatTuPhieuKham.
Lưu ý: Ràng buộc này không dựa trên FK trực tiếp (vì ChiTietVatTuPhieuKham chỉ FK tới DanhMucVatTu, không lưu MaLo — giống hệt cách ChiTietDonThuoc chỉ FK tới DanhMucThuoc), mà dựa trên nghiệp vụ: so sánh SoLuongTon với SoLuongNhap để suy luận lô đã qua sử dụng hay chưa.

44.8 Xử lý lỗi
44.8.1 maLo trên URL không tồn tại trong hệ thống
→ HTTP 404 Not Found | Thông báo: "Không tìm thấy lô vật tư cần xóa"
44.8.2 Lô vật tư đã được sử dụng để kê vật tư cho bệnh nhân (SoLuongTon < SoLuongNhap)
→ HTTP 409 Conflict | Thông báo: "Lô vật tư này đã được sử dụng để kê cho bệnh nhân, không thể xóa"
44.8.3 Token không hợp lệ hoặc hết hạn
→ HTTP 401 | Thông báo: "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại"
44.8.4 Tài khoản không có quyền truy cập
→ HTTP 403 | Thông báo: "Bạn không có quyền truy cập chức năng này"
44.8.5 Hệ thống không kết nối được API hoặc Database
→ HTTP 500 | Thông báo: "Không thể thực hiện thao tác. Xin hãy thử lại"

================================================================
