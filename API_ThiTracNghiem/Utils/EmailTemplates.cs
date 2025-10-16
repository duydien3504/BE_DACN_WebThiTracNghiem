namespace API_ThiTracNghiem.Utils
{
    public static class EmailTemplates
    {
        public static string BuildOtpCard(string? greeting, string title, string otp, int expireMinutes)
        {
            var greet = string.IsNullOrWhiteSpace(greeting) ? string.Empty : $"<p style=\"margin:0 0 12px\">{greeting}</p>";
            return $@"<div style='font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;background:#f5f7fb;padding:24px;'>
  <div style='max-width:560px;margin:0 auto;background:#ffffff;border-radius:12px;box-shadow:0 6px 20px rgba(0,0,0,0.08);overflow:hidden'>
    <div style='background:#3b82f6;color:#fff;padding:16px 20px'>
      <h2 style='margin:0;font-weight:600'>Thi Trắc Nghiệm</h2>
    </div>
    <div style='padding:20px 24px;color:#111827'>
      {greet}
      <h3 style='margin:0 0 8px;font-weight:600;color:#111827'>{title}</h3>
      <div style='margin:16px 0;padding:14px 18px;border:1px dashed #3b82f6;border-radius:10px;background:#eff6ff;text-align:center'>
        <div style='font-size:28px;letter-spacing:4px;font-weight:700;color:#1f2937'>{otp}</div>
      </div>
      <p style='margin:0;color:#4b5563'>Mã sẽ hết hạn sau <b>{expireMinutes} phút</b>. Vui lòng không chia sẻ cho bất kỳ ai.</p>
    </div>
    <div style='padding:14px 20px;background:#f9fafb;color:#6b7280;font-size:12px'>
      <p style='margin:0'>Nếu bạn không yêu cầu, hãy bỏ qua email này.</p>
    </div>
  </div>
</div>";
        }
    }
}


