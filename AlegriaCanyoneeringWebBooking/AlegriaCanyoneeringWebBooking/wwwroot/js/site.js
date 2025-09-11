document.addEventListener("DOMContentLoaded", function () {
    var toastEl = document.getElementById("toastMessage");
    if (toastEl) {
        var toast = new bootstrap.Toast(toastEl, {
            delay: 3000
        });
        toast.show();
    }
});

