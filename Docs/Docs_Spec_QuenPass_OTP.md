================================================================
3b. QUÊN MẬT KHẨU (chưa đăng nhập) — XÁC THỰC QUA OTP EMAIL
================================================================

**Thay đổi so với bản cũ:** Tách 1 API `QuenMatKhau` thành 2 API
(`GuiOtpQuenMatKhau` + `XacNhanOtpVaDoiMatKhau`), bổ sung bước xác thực
OTP gửi qua email để tránh trường hợp người khác biết Email + SĐT của
nhân viên có thể tự ý đổi mật khẩu tài khoản đó.

Email nhận OTP dùng theo cơ chế **Gmail +alias**
(`nhomthesis2026+<manv>@gmail.com`) — dùng để demo, giải thích rõ trong
báo cáo là giả lập môi trường production thật (nơi mỗi nhân viên có
email cá nhân riêng).

----------------------------------------------------------------
3b.1 API 1 — GỬI OTP QUÊN MẬT KHẨU
----------------------------------------------------------------

**3b.1.1 Tên API**

POST api/xacthuc/GuiOtpQuenMatKhau

**3b.1.2 Mô tả**

Xác thực danh tính người dùng qua Email + Số điện thoại, sau đó sinh
mã OTP 6 số và gửi đến email đã đăng ký. OTP được lưu tạm trong
`IMemoryCache`, hiệu lực 5 phút, dùng cho bước xác nhận đổi mật khẩu
ở API 2.

Không yêu cầu đăng nhập trước — endpoint public (không có `[Authorize]`).

**3b.1.3 Phân quyền**

Không yêu cầu token — tất cả đều có thể gọi từ trang đăng nhập.

**3b.1.4 Quy trình**

- B1: Người dùng click "Quên mật khẩu" tại trang đăng nhập
- B2: Người dùng nhập Email, Số điện thoại
- B3: Người dùng submit form
- B4: Client gửi request lên server (không kèm token)
- B5: Server kiểm tra định dạng Email, dữ liệu không được để trống
- B6: Server kiểm tra tần suất gửi OTP (chống spam — xem 3b.1.7.6)
- B7: Server tìm tài khoản theo Email (cột Username trong bảng Users)
- B8: Server JOIN sang bảng NhanVien, kiểm tra Số điện thoại khớp (cột SDT)
- B9: Server kiểm tra tài khoản đang hoạt động (IsActive = 1)
- B10: Server sinh mã OTP ngẫu nhiên gồm 6 chữ số
- B11: Server lưu OTP vào `IMemoryCache` với key `otp_{email}`,
  hiệu lực 5 phút, kèm bộ đếm số lần nhập sai (`otpAttempts_{email}`)
- B12: Server gửi email chứa mã OTP đến địa chỉ email đã đăng ký
  của nhân viên (qua `EmailService`)
- B13: Server lưu thời điểm gửi OTP gần nhất vào cache
  (`otpLastSent_{email}`) để áp dụng giới hạn tần suất gửi lại
- B14: Trả về thành công, client chuyển sang màn hình nhập OTP + mật khẩu mới

**3b.1.5 Input (Body)**

- email `<string>` : Email đăng nhập (cột Username trong bảng Users)
- soDienThoai `<string>` : Số điện thoại đã đăng ký (cột SDT trong bảng NhanVien)

**3b.1.6 Output (khi thành công) — HTTP 200 OK**

- message `<string>` : "Mã OTP đã được gửi đến email của bạn. Vui lòng kiểm tra hộp thư"
- otpExpiryMinutes `<int>` : 5 (để client hiển thị đồng hồ đếm ngược)

**3b.1.7 Ràng buộc**

- 3b.1.7.1 Không để trống Email hoặc Số điện thoại
- 3b.1.7.2 Email phải đúng định dạng (vd: example@gmail.com)
- 3b.1.7.3 Email phải tồn tại trong hệ thống (bảng Users, cột Username)
- 3b.1.7.4 Số điện thoại phải khớp với SĐT của nhân viên có email đó
  (JOIN Users → NhanVien theo UserID, kiểm tra cột SDT)
- 3b.1.7.5 Tài khoản phải đang hoạt động (IsActive = 1)
- 3b.1.7.6 Giới hạn tần suất gửi lại OTP: tối thiểu 60 giây giữa 2 lần
  gửi cho cùng 1 email, tránh spam hộp thư và lạm dụng SMTP
- 3b.1.7.7 OTP có hiệu lực 5 phút kể từ lúc gửi, hết hạn phải yêu cầu gửi lại

**3b.1.8 Xử lý lỗi**

- 3b.1.8.1 Để trống một hoặc nhiều trường
  → HTTP 400 | "Người dùng bắt buộc nhập đủ các dữ liệu cần thiết"
- 3b.1.8.2 Email không đúng định dạng
  → HTTP 400 | "Email không đúng định dạng"
- 3b.1.8.3 Email không tồn tại hoặc Số điện thoại không khớp
  → HTTP 400 | "Email hoặc số điện thoại không chính xác"
  (Gộp chung 2 trường hợp để tránh lộ thông tin tài khoản)
- 3b.1.8.4 Tài khoản bị vô hiệu hóa (IsActive = 0)
  → HTTP 400 | "Tài khoản đã bị vô hiệu hóa, vui lòng liên hệ quản trị viên"
- 3b.1.8.5 Gửi lại OTP quá sớm (chưa đủ 60 giây từ lần gửi trước)
  → HTTP 429 | "Vui lòng đợi ít nhất 60 giây trước khi yêu cầu gửi lại OTP"
- 3b.1.8.6 Hệ thống không kết nối được API, Database hoặc SMTP
  → HTTP 503 | "Không thể gửi OTP lúc này. Xin hãy thử lại"

----------------------------------------------------------------
3b.2 API 2 — XÁC NHẬN OTP VÀ ĐỔI MẬT KHẨU
----------------------------------------------------------------

**3b.2.1 Tên API**

POST api/xacthuc/XacNhanOtpVaDoiMatKhau

**3b.2.2 Mô tả**

Xác nhận mã OTP đã gửi ở API 1 còn hiệu lực và chính xác, sau đó cho
phép đặt mật khẩu mới. Không yêu cầu đăng nhập trước.

**3b.2.3 Phân quyền**

Không yêu cầu token — endpoint public (không có `[Authorize]`).

**3b.2.4 Quy trình**

- B1: Người dùng nhập mã OTP nhận được từ email, mật khẩu mới, nhập lại mật khẩu mới
- B2: Hệ thống kiểm tra real-time các ràng buộc mật khẩu mới trong khi người dùng nhập
- B3: Người dùng submit form
- B4: Client gửi request lên server kèm Email (giữ từ bước trước) + OTP + mật khẩu mới
- B5: Server kiểm tra OTP có tồn tại trong cache theo key `otp_{email}` (chưa hết hạn)
- B6: Server so khớp mã OTP người dùng nhập với OTP lưu trong cache
- B7: Nếu sai → tăng bộ đếm `otpAttempts_{email}`; nếu đủ 5 lần sai → xóa
  OTP khỏi cache, bắt buộc người dùng quay lại B1 (API 1) để lấy OTP mới
- B8: Server kiểm tra tài khoản đang hoạt động (IsActive = 1)
- B9: Server kiểm tra mật khẩu mới không trùng mật khẩu hiện tại
- B10: Server hash mật khẩu mới và cập nhật vào bảng Users
- B11: Server xóa OTP và bộ đếm sai khỏi cache (dùng 1 lần)
- B12: Trả về thành công, client chuyển về trang đăng nhập

**3b.2.5 Input (Body)**

- email `<string>` : Email đã dùng ở bước gửi OTP (cột Username trong bảng Users)
- otp `<string>` : Mã OTP 6 số nhận được qua email
- matKhauMoi `<string>` : Mật khẩu mới muốn đặt
- nhapLaiMatKhauMoi `<string>` : Nhập lại mật khẩu mới để xác nhận

**3b.2.6 Output (khi thành công) — HTTP 200 OK**

- message `<string>` : "Đặt lại mật khẩu thành công. Vui lòng đăng nhập lại"

**3b.2.7 Ràng buộc**

- 3b.2.7.1 Không để trống bất kỳ trường nào
- 3b.2.7.2 Mã OTP phải còn tồn tại trong cache (chưa hết hạn 5 phút, chưa dùng lần nào)
- 3b.2.7.3 Mã OTP phải khớp chính xác với OTP đã gửi
- 3b.2.7.4 Số lần nhập sai OTP tối đa 5 lần, quá số lần → phải gửi lại OTP mới
- 3b.2.7.5 Tài khoản phải đang hoạt động (IsActive = 1)
- 3b.2.7.6 Mật khẩu mới phải tuân theo quy tắc (kiểm tra real-time):
  - Độ dài từ 8 đến 12 ký tự
  - Có ít nhất 1 chữ thường (a-z)
  - Có ít nhất 1 chữ hoa (A-Z)
  - Có ít nhất 1 chữ số (0-9)
  - Có ít nhất 1 ký tự đặc biệt (!, @, #, $, %, ...)
  - Không chứa khoảng trắng
- 3b.2.7.7 Mật khẩu mới không được trùng mật khẩu hiện tại
- 3b.2.7.8 Nhập lại mật khẩu mới phải trùng khớp với mật khẩu mới

**3b.2.8 Xử lý lỗi**

- 3b.2.8.1 Để trống một hoặc nhiều trường
  → HTTP 400 | "Người dùng bắt buộc nhập đủ các dữ liệu cần thiết"
- 3b.2.8.2 OTP hết hạn hoặc chưa từng gửi cho email này
  → HTTP 400 | "Mã OTP đã hết hạn hoặc không tồn tại. Vui lòng yêu cầu gửi lại OTP"
- 3b.2.8.3 OTP không khớp
  → HTTP 400 | "Mã OTP không chính xác"
- 3b.2.8.4 Nhập sai OTP quá 5 lần
  → HTTP 400 | "Bạn đã nhập sai quá số lần cho phép. Vui lòng yêu cầu gửi lại OTP"
- 3b.2.8.5 Tài khoản bị vô hiệu hóa (IsActive = 0)
  → HTTP 400 | "Tài khoản đã bị vô hiệu hóa, vui lòng liên hệ quản trị viên"
- 3b.2.8.6 Mật khẩu mới không đạt quy tắc (kiểm tra real-time từng điều kiện)
  → HTTP 400 | chiTiet: [danh sách điều kiện chưa đạt]
  → Frontend hiển thị trực tiếp các điều kiện chưa đạt bên dưới ô nhập
- 3b.2.8.7 Mật khẩu mới trùng mật khẩu hiện tại
  → HTTP 400 | "Mật khẩu mới không được trùng với mật khẩu hiện tại"
- 3b.2.8.8 Nhập lại mật khẩu không khớp
  → HTTP 400 | "Nhập lại mật khẩu không khớp"
- 3b.2.8.9 Hệ thống không kết nối được API hoặc Database
  → HTTP 503 | "Không thể cập nhật dữ liệu từ máy chủ. Xin hãy thử lại"

----------------------------------------------------------------
3b.3 Ghi chú kỹ thuật (dành cho báo cáo / bảo vệ luận văn)
----------------------------------------------------------------

- OTP và các bộ đếm liên quan lưu trong `IMemoryCache` — cùng cơ chế
  đang dùng để blacklist token đăng xuất và khóa đăng nhập sai nhiều
  lần, không cần thêm bảng DB mới.
- Email gửi OTP dùng SMTP Gmail (`smtp.gmail.com:587`, STARTTLS),
  xác thực bằng App Password 16 ký tự — không dùng mật khẩu Gmail
  thường vì Google chặn SMTP với mật khẩu thường.
- Trong môi trường demo/đồ án, mỗi nhân viên được gán 1 địa chỉ email
  dạng `<gmail-du-an>+<manv>@gmail.com` (Gmail +alias) để mô phỏng
  việc mỗi nhân viên có email riêng, trong khi thực tế toàn bộ mail
  đổ về 1 hộp thư Gmail chung để nhóm tiện theo dõi khi demo.
- Trong môi trường production thực tế, mỗi nhân viên cần dùng email
  cá nhân thật của họ; hệ thống không cần thay đổi logic, chỉ cần
  đổi dữ liệu email lưu trong bảng NhanVien.
