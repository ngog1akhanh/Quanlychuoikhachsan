// Hàm JS dùng chung (gọi qua IJSRuntime nếu cần).
window.hotel = {
    scrollTop: function () { window.scrollTo({ top: 0, behavior: 'smooth' }); },
    copyText: function (text) { return navigator.clipboard ? navigator.clipboard.writeText(text) : Promise.resolve(); }
};
