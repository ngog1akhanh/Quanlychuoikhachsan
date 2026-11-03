// Hàm JS dùng chung (gọi qua IJSRuntime nếu cần).
window.hotel = {
    scrollTop: function () { window.scrollTo({ top: 0, behavior: 'smooth' }); },
    copyText: function (text) { return navigator.clipboard ? navigator.clipboard.writeText(text) : Promise.resolve(); },
    // Vẽ mã QR từ chuỗi VietQR (dùng khi API không trả ảnh, vd. chế độ demo). Cần js/qrcode.js.
    qrDataUrl: function (text) {
        if (!text || typeof qrcode !== 'function') return '';
        var qr = qrcode(0, 'M');
        qr.addData(text);
        qr.make();
        return qr.createDataURL(6, 8);
    }
};
