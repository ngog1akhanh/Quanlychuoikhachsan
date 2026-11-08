namespace QuanLyKhachSan.Web.Services;

public sealed record ServicePresentation(string Image, string Intro, string Experience, string Preparation)
{
    public static ServicePresentation For(string name)
    {
        var key = name.ToLowerInvariant();
        if (key.Contains("tại phòng") || key.Contains("tận phòng")) return new("roomservice",
            "Một bữa ăn riêng tư, ngay trong không gian nghỉ ngơi của bạn.",
            "Phục vụ ăn tại phòng phù hợp khi bạn muốn dùng bữa thoải mái sau chuyến đi, tiếp tục công việc hoặc dành thời gian bên gia đình. Bạn có thể trao đổi với nhân viên về món ăn và thời điểm phục vụ phù hợp với lịch trình của mình.",
            "Liên hệ lễ tân để xem thực đơn, hỏi giá món và thời gian chờ dự kiến. Vui lòng báo số phòng, số người dùng bữa và các yêu cầu về dị ứng thực phẩm. Kiểm tra riêng phí phục vụ và tiền món trước khi xác nhận.");
        if (key.Contains("sáng") || key.Contains("ăn") || key.Contains("ẩm thực")) return new("dining",
            "Khởi đầu ngày mới bằng một bữa sáng thư thái tại khách sạn.",
            "Dành một khoảng thời gian buổi sáng để thưởng thức bữa ăn và chuẩn bị cho lịch trình trong ngày. Buffet giúp bạn chủ động lựa chọn món theo khẩu vị; thực đơn và món phục vụ có thể thay đổi theo ngày.",
            "Kiểm tra địa điểm và giờ phục vụ trong thông tin bên dưới. Khi cần hỗ trợ về chế độ ăn, món chay hoặc dị ứng thực phẩm, hãy trao đổi với nhân viên nhà hàng trước khi lựa chọn món. Liên hệ lễ tân để kiểm tra bữa sáng đã nằm trong gói phòng hay chưa.");
        if (key.Contains("giặt")) return new("laundry",
            "Giữ trang phục sạch sẽ để bạn nhẹ hành lý và thoải mái tận hưởng chuyến đi.",
            "Dịch vụ giặt ủi hỗ trợ nhu cầu chăm sóc quần áo trong thời gian lưu trú, đặc biệt với những chuyến công tác hoặc kỳ nghỉ dài ngày. Nhân viên sẽ trao đổi về loại trang phục và yêu cầu xử lý khi tiếp nhận.",
            "Vui lòng kiểm tra túi áo, túi quần và báo rõ chất liệu cần chăm sóc đặc biệt. Xác nhận số lượng hoặc khối lượng, chi phí và thời gian nhận lại trước khi giao đồ. Với đồ dễ phai màu hoặc có hướng dẫn giặt riêng, hãy thông báo cho nhân viên.");
        if (key.Contains("xe") || key.Contains("đón")) return new("transport",
            "Chủ động hành trình giữa sân bay và khách sạn.",
            "Dịch vụ đưa đón giúp bạn lên kế hoạch di chuyển cùng người thân và hành lý thuận tiện hơn. Trao đổi trước lịch trình với lễ tân để lựa chọn chuyến xe phù hợp với số khách và nhu cầu di chuyển.",
            "Cung cấp mã chuyến bay, ngày giờ đến hoặc đi, số người và số kiện hành lý. Xác nhận điểm hẹn, loại xe, giá chuyến và các chi phí phát sinh nếu có. Khi chuyến bay thay đổi, vui lòng thông báo sớm để nhân viên hỗ trợ điều chỉnh.");
        if (key.Contains("massage") || key.Contains("spa")) return new("wellness",
            "Dành một khoảng lặng để nghỉ ngơi sau ngày dài di chuyển.",
            "Trải nghiệm massage mang đến thời gian thư giãn riêng trong kỳ nghỉ. Bạn có thể trao đổi với nhân viên về mức lực và những vùng muốn tập trung hoặc tránh tác động để buổi trải nghiệm phù hợp với sự thoải mái của mình.",
            "Liên hệ lễ tân để chọn khung giờ và xác nhận thời lượng của dịch vụ. Hãy thông báo tình trạng sức khỏe, vùng đau hoặc nhạy cảm và yêu cầu riêng trước khi bắt đầu. Thời gian đặt trước được ghi trong thông tin dịch vụ bên dưới.");
        return new("wellness", "Thêm tiện ích cho kỳ nghỉ của bạn.",
            "Khám phá dịch vụ và lựa chọn trải nghiệm phù hợp với nhu cầu lưu trú. Nhân viên khách sạn sẵn sàng giải đáp thông tin trước khi bạn sử dụng.",
            "Liên hệ lễ tân để xác nhận thời gian, phạm vi phục vụ và chi phí áp dụng.");
    }
}
