$(function () {
    // Export to Excel
    $("#exportExcel").click(function () {
        var table = document.getElementById('nationalityReportTable');
        var wb = XLSX.utils.table_to_book(table, { sheet: "Nationality Report" });
        XLSX.writeFile(wb, "NationalityReport.xlsx");
    });

    // Export to PDF
    $("#exportPdf").click(function () {
        const { jsPDF } = window.jspdf;
        var doc = new jsPDF('landscape');
        doc.text("Nationality Report", 40, 40);
        doc.autoTable({ html: "#nationalityReportTable", startY: 60 });
        doc.save("NationalityReport.pdf");
    });

    // Auto set date range based on filter
    function setDateRange(filter) {
        const today = new Date();
        let start, end;
        switch (filter) {
            case "daily":
                start = end = today;
                break;
            case "weekly":
                const day = today.getDay();
                start = new Date(today);
                start.setDate(today.getDate() - day + 1);
                end = new Date(start);
                end.setDate(start.getDate() + 6);
                break;
            case "monthly":
                start = new Date(today.getFullYear(), today.getMonth(), 1);
                end = new Date(today.getFullYear(), today.getMonth() + 1, 0);
                break;
            case "quarterly":
                const quarter = Math.floor(today.getMonth() / 3);
                start = new Date(today.getFullYear(), quarter * 3, 1);
                end = new Date(today.getFullYear(), quarter * 3 + 3, 0);
                break;
            case "yearly":
                start = new Date(today.getFullYear(), 0, 1);
                end = new Date(today.getFullYear(), 11, 31);
                break;
        }

        $("#dateFrom").val(start.toISOString().slice(0, 10));
        $("#dateTo").val(end.toISOString().slice(0, 10));
    }

    // On initial load
    const from = $("#dateFrom").val();
    const to = $("#dateTo").val();
    if (!from || !to) {
        setDateRange($("#filter").val());
    }

    // On filter change
    $("#filter").on("change", function () {
        setDateRange(this.value);
    });
});
