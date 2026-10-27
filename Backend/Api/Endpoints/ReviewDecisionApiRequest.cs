using AiExamBank.Modules.Review.Contracts;

namespace AiExamBank.Api.Endpoints;

public sealed record ReviewDecisionApiRequest(
    ReviewDecision Decision,//duyệt hay từ chối.
    string? Reason,//lý do, bắt buộc khi từ chối.
    long ExpectedVersion)//phiên bản mà người duyệt đang xem, để sau này phát hiện dữ liệu đã đổi và trả 409.
{
    public string? GetValidationError()//kiểm tra dữ liệu người dùng gửi trước khi xử lý quyết định duyệt:
    {
        if (!Enum.IsDefined(Decision))//chặn giá trị khác Approve và Reject
            return "Decision must be Approve or Reject.";

        if (Decision == ReviewDecision.Reject && string.IsNullOrWhiteSpace(Reason))//rỗng hoặc chỉ có khoảng trắng, trả thông báo lỗi.
            return "Reason is required when rejecting.";

        return null;
    }
}

//Không có ReviewerId vì backend phải lấy người duyệt từ phiên đăng nhập; nếu nhận ID từ trình duyệt,
//người dùng có thể giả làm người khác.
