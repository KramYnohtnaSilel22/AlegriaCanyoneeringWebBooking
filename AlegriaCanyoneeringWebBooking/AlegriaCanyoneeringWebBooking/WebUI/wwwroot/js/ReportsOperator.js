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
            start.setDate(today.getDate() - ((day + 6) % 7));
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

$("#filterSelect").on("change", function () {
    setDateRange(this.value);
});

$("#btnReset").on("click", function () {
    const currentFilter = $("#filterSelect").val();
    setDateRange(currentFilter);
    $("#filterForm").submit();
});

$(document).ready(function () {
    const from = $("#dateFrom").val();
    const to = $("#dateTo").val();
    if (!from || !to) {
        setDateRange($("#filterSelect").val());
        $("#filterForm").submit();
    }
});

// FIXED: PDF download - preserves outer border
$("#btnDownloadPDF").on("click", function () {
    const filter = $("#filterSelect").val().toUpperCase();
    const element = document.querySelector('.report-container');

    const opt = {
        margin: [5, 5, 5, 5],
        filename: `OperatorReport_${filter}_${new Date().toISOString().slice(0, 10)}.pdf`,
        image: { type: 'jpeg', quality: 0.98 },
        html2canvas: {
            scale: 2,
            useCORS: true,
            allowTaint: true,
            logging: false,
            backgroundColor: '#ffffff'
        },
        jsPDF: { unit: 'mm', format: 'a4', orientation: 'landscape' },
        pagebreak: { mode: 'avoid-all' },
        compress: true
    };

    html2pdf()
        .set(opt)
        .from(element)
        .save();
});

$("#btnDownloadExcel").on("click", function () {
    var table = document.getElementById("tourismReportTable");
    var wb = XLSX.utils.book_new();

    function formatDate(dateStr) {
        const date = new Date(dateStr + "T00:00:00");
        const options = { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' };
        return date.toLocaleDateString('en-US', options);
    }

    var dateFrom = formatDate($("#dateFrom").val());
    var dateTo = formatDate($("#dateTo").val());
    var municipality = "ALEGRIA, CEBU";
    var attraction = "Canyoneering Adventure";

    // Simplified header rows
    var headerRows = [
        ["Operator Visitor Record"],
        ["Date From", dateFrom],
        ["Date To", dateTo],
        ["Municipality", municipality],
        ["Attraction Spot", attraction],
        []
    ];

    var ws = XLSX.utils.aoa_to_sheet(headerRows);
    var wsTable = XLSX.utils.table_to_sheet(table);

    var rowOffset = headerRows.length;
    var range = XLSX.utils.decode_range(wsTable['!ref']);
    for (var R = range.s.r; R <= range.e.r; ++R) {
        for (var C = range.s.c; C <= range.e.c; ++C) {
            var cellAddress = { c: C, r: R + rowOffset };
            var origCell = wsTable[XLSX.utils.encode_cell({ c: C, r: R })];
            if (origCell) ws[XLSX.utils.encode_cell(cellAddress)] = origCell;
        }
    }

    var newRange = XLSX.utils.decode_range(wsTable['!ref']);
    newRange.e.r += rowOffset;
    ws['!ref'] = XLSX.utils.encode_range(newRange);

    ws['!cols'] = [
        { wch: 8 },
        { wch: 35 },
        { wch: 10 },
        { wch: 10 },
        { wch: 10 }
    ];

    // Merge title row across all columns (5 columns for operator report)
    ws['!merges'] = [
        { s: { r: 0, c: 0 }, e: { r: 0, c: 4 } }
    ];

    // Style header cells
    for (let i = 0; i < headerRows.length; i++) {
        for (let j = 0; j < headerRows[i].length; j++) {
            var cellRef = XLSX.utils.encode_cell({ c: j, r: i });
            if (!ws[cellRef]) {
                ws[cellRef] = { t: 's', v: '' };
            }
            ws[cellRef].s = ws[cellRef].s || {};
            ws[cellRef].s.alignment = { horizontal: "center", vertical: "center" };

            if (i === 0) {
                ws[cellRef].s.font = { bold: true, size: 14 };
            } else if (j === 0 && i < 5) {
                ws[cellRef].s.font = { bold: true };
            }
        }
    }

    XLSX.utils.book_append_sheet(wb, ws, "Operator Report");
    var filter = $("#filterSelect").val().toUpperCase();
    XLSX.writeFile(wb, `OperatorReport_${filter}_${new Date().toISOString().slice(0, 10)}.xlsx`);
});
