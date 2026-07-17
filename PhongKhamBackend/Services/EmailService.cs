using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace PhongKhamBackend.Services;

// Interface — cho phép mock/test và dùng chung cho nhiều loại email (OTP, thông báo, v.v.)
// Tham số "smtpProfile" cho phép chọn cấu hình SMTP nào sẽ dùng để gửi
// (ứng với các section "Smtp1", "Smtp2", ... trong appsettings.json)
public interface IEmailService
{
    Task SendOtpEmailAsync(string toEmail, string hoTen, string otpCode, int expiryMinutes, string smtpProfile = "Smtp1");
}

// Cấu hình SMTP — mỗi profile (Smtp1, Smtp2, ...) map với 1 section riêng trong appsettings.json
public class SmtpSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromName { get; set; } = "QLPhongKham";
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    // Đọc cấu hình SMTP theo tên profile (vd: "Smtp1", "Smtp2")
    private SmtpSettings LayCauHinhSmtp(string smtpProfile)
    {
        var settings = _configuration.GetSection($"Mail:{smtpProfile}").Get<SmtpSettings>();

        if (settings == null || string.IsNullOrWhiteSpace(settings.Username))
        {
            throw new InvalidOperationException(
                $"Không tìm thấy cấu hình SMTP hợp lệ cho profile '{smtpProfile}' trong appsettings.json");
        }

        return settings;
    }

    // Gửi email chứa mã OTP dùng cho chức năng Quên mật khẩu
    // smtpProfile: chọn tài khoản Gmail nào sẽ gửi — vd "Smtp1" (của bạn A) hoặc "Smtp2" (của bạn B)
    public async Task SendOtpEmailAsync(string toEmail, string hoTen, string otpCode, int expiryMinutes, string smtpProfile = "Smtp1")
    {
        var smtpSettings = LayCauHinhSmtp(smtpProfile);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(smtpSettings.FromName, smtpSettings.Username));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "[QLPhongKham] Mã OTP đặt lại mật khẩu";

        string tenHienThi = string.IsNullOrWhiteSpace(hoTen) ? "bạn" : hoTen;

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = $@"
                <div style='font-family: Arial, sans-serif; font-size: 14px; color: #333;'>
                    <p>Xin chào {tenHienThi},</p>
                    <p>Bạn (hoặc ai đó dùng tài khoản của bạn) vừa yêu cầu đặt lại mật khẩu
                    cho hệ thống <b>QLPhongKham</b>.</p>
                    <p>Mã OTP của bạn là:</p>
                    <p style='font-size: 28px; font-weight: bold; letter-spacing: 4px; color: #1a73e8;'>
                        {otpCode}
                    </p>
                    <p>Mã có hiệu lực trong <b>{expiryMinutes} phút</b>. Vui lòng không chia sẻ mã này cho bất kỳ ai.</p>
                    <p>Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email này hoặc
                    liên hệ quản trị viên.</p>
                    <br/>
                    <p style='color: #999; font-size: 12px;'>Đây là email tự động, vui lòng không trả lời.</p>
                </div>",
            TextBody = $"Xin chào {tenHienThi},\n\n" +
                       $"Mã OTP đặt lại mật khẩu của bạn là: {otpCode}\n" +
                       $"Mã có hiệu lực trong {expiryMinutes} phút.\n\n" +
                       $"Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email này."
        };

        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(smtpSettings.Host, smtpSettings.Port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(smtpSettings.Username, smtpSettings.Password);
            await client.SendAsync(message);
        }
        catch (Exception ex)
        {
            // Log lỗi để debug (vd: sai App Password, mất mạng, SMTP bị chặn...)
            // Controller sẽ bắt exception này và trả về HTTP 503 cho client
            _logger.LogError(ex, "Gửi email OTP thất bại đến {Email} qua profile {Profile}", toEmail, smtpProfile);
            throw;
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true);
            }
        }
    }
}

/*
=====================================================================
HƯỚNG DẪN CÀI ĐẶT (2 CẤU HÌNH SMTP SONG SONG)
=====================================================================

1) Cài NuGet package:
   Install-Package MailKit

2) Thêm 2 section riêng biệt vào appsettings.json (hoặc appsettings.Development.json
   để không commit App Password thật lên Git):

   {
     "Smtp1": {
       "Host": "smtp.gmail.com",
       "Port": 587,
       "Username": "ban1@gmail.com",
       "Password": "app-password-cua-ban1",
       "FromName": "QLPhongKham"
     },
     "Smtp2": {
       "Host": "smtp.gmail.com",
       "Port": 587,
       "Username": "ban2@gmail.com",
       "Password": "app-password-cua-ban2",
       "FromName": "QLPhongKham"
     }
   }

   Mỗi bạn trong nhóm tạo App Password riêng theo Gmail của mình
   (xem lại hướng dẫn tạo App Password ở phần trước).

3) Đăng ký service trong Program.cs (KHÔNG đổi gì so với trước —
   EmailService giờ tự đọc cấu hình theo profile khi được gọi,
   không cố định 1 cấu hình lúc khởi tạo):

   builder.Services.AddScoped<IEmailService, EmailService>();

4) Cách dùng trong XacThucController — chọn profile khi gọi hàm gửi:

   // Ví dụ: mặc định dùng Smtp1, nhưng cho phép chỉ định Smtp2 khi cần test
   await _emailService.SendOtpEmailAsync(
       toEmail: user.NhanVien!.Email,
       hoTen: user.NhanVien.HoTen,
       otpCode: otpCode,
       expiryMinutes: 5,
       smtpProfile: "Smtp2"); // hoặc "Smtp1", hoặc đọc từ appsettings/biến môi trường

   Gợi ý: có thể thêm 1 key cấu hình "ActiveSmtpProfile": "Smtp1" trong
   appsettings.json để chọn profile mặc định toàn hệ thống, thay vì
   hard-code "Smtp1"/"Smtp2" trong code — mỗi bạn chỉ cần đổi giá trị
   này trên máy mình khi test mà không cần sửa Controller.

=====================================================================
*/
