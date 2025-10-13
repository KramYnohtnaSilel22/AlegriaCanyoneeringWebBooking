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

$("#btnDownloadPDF").on("click", function () {
    const filter = $("#filterSelect").val().toUpperCase();
    const element = document.querySelector('.report-container');

    if (!element) {
        console.error("Report container not found");
        return;
    }

    // Clone the element to avoid modifying the original
    const clonedElement = element.cloneNode(true);
    const container = document.createElement('div');
    container.style.position = 'absolute';
    container.style.left = '-10000px';
    container.appendChild(clonedElement);
    document.body.appendChild(container);

    const opt = {
        margin: [10, 10, 10, 10],
        filename: `NationalityReport_${filter}_${new Date().toISOString().slice(0, 10)}.pdf`,
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
        .from(clonedElement)
        .save()
        .finally(() => {
            document.body.removeChild(container);
        });
});

$("#btnDownloadExcel").on("click", function () {
    var table = document.getElementById("tourismReportTable");

    if (!table) {
        console.error("Table not found");
        return;
    }

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

    // Create header information
    var headerRows = [
        [{ v: "Nationality Visitor Record", s: { alignment: { horizontal: "center", vertical: "center" }, font: { bold: true, size: 14 } } }],
        [{ v: "(Summary of visitors by nationality)", s: { alignment: { horizontal: "center", vertical: "center" } } }],
        [],
        [{ v: "Date From:", s: { alignment: { horizontal: "right", vertical: "center" }, font: { bold: true } } }, { v: dateFrom, s: { alignment: { horizontal: "left", vertical: "center" } } }],
        [{ v: "Date To:", s: { alignment: { horizontal: "right", vertical: "center" }, font: { bold: true } } }, { v: dateTo, s: { alignment: { horizontal: "left", vertical: "center" } } }],
        [{ v: "Municipality:", s: { alignment: { horizontal: "right", vertical: "center" }, font: { bold: true } } }, { v: municipality, s: { alignment: { horizontal: "left", vertical: "center" } } }],
        [{ v: "Attraction:", s: { alignment: { horizontal: "right", vertical: "center" }, font: { bold: true } } }, { v: attraction, s: { alignment: { horizontal: "left", vertical: "center" } } }],
        []
    ];

    var ws = XLSX.utils.aoa_to_sheet(headerRows);
    var wsTable = XLSX.utils.table_to_sheet(table);

    var rowOffset = headerRows.length;
    var range = XLSX.utils.decode_range(wsTable['!ref']);

    // Copy table data to main sheet
    for (var R = range.s.r; R <= range.e.r; ++R) {
        for (var C = range.s.c; C <= range.e.c; ++C) {
            var cellAddress = { c: C, r: R + rowOffset };
            var origCell = wsTable[XLSX.utils.encode_cell({ c: C, r: R })];
            if (origCell) {
                ws[XLSX.utils.encode_cell(cellAddress)] = origCell;
            }
        }
    }

    // Update the range reference
    var newRange = XLSX.utils.decode_range(wsTable['!ref']);
    newRange.e.r += rowOffset;
    ws['!ref'] = XLSX.utils.encode_range(newRange);

    // Set column widths
    ws['!cols'] = [
        { wch: 8 },    // Seq
        { wch: 30 },   // Nationality
        { wch: 12 },   // Male
        { wch: 12 },   // Female
        { wch: 15 }    // Ending Total
    ];

    // Format header rows
    for (let i = 0; i < headerRows.length; i++) {
        for (let j = 0; j < headerRows[i].length; j++) {
            var cell = ws[XLSX.utils.encode_cell({ c: j, r: i })];
            if (cell) {
                cell.s = cell.s || {};
                if (i === 0) {
                    cell.s.font = { bold: true, size: 14 };
                    cell.s.alignment = { horizontal: "center", vertical: "center" };
                }
            }
        }
    }

    // Merge cells for the title
    ws['!merges'] = [
        { s: { r: 0, c: 0 }, e: { r: 0, c: 4 } },
        { s: { r: 1, c: 0 }, e: { r: 1, c: 4 } }
    ];

    // Set row heights
    ws['!rows'] = [];
    ws['!rows'][0] = { hpt: 20 };
    ws['!rows'][1] = { hpt: 16 };

    XLSX.utils.book_append_sheet(wb, ws, "Nationality Report");
    var filter = $("#filterSelect").val().toUpperCase();
    XLSX.writeFile(wb, `NationalityReport_${filter}_${new Date().toISOString().slice(0, 10)}.xlsx`);
});