$(function () {
    // Export to Excel
    $("#exportExcel").click(function () {
        function formatDate(dateStr) {
            if (!dateStr) return "";
            var d = new Date(dateStr);
            return d.toLocaleDateString(undefined, { year: 'numeric', month: 'long', day: 'numeric' });
        }
        // Multi-row headers
        var excelHeaders = [
            ["Republic of the Philippines"],
            ["Province of Cebu"],
            ["Municipality of Alegria"],
            [],
            ["CANYONEERING"],
            [
                $("#dateFrom").val() && $("#dateTo").val()
                    ? formatDate($("#dateFrom").val()) + " - " + formatDate($("#dateTo").val())
                    : ""
            ],
            [],
            ["NATIONALITY", "MALE", "FEMALE", "ENDING TOTAL"]
        ];
        // Get table data rows
        var tableRows = [];
        $("#nationalityReportTable tbody tr").each(function () {
            var row = [];
            $(this).find("td").each(function () {
                row.push($(this).text().trim());
            });
            if (row.length) tableRows.push(row);
        });
        var exportRows = excelHeaders.concat(tableRows);

        // Sheet setup
        var ws = XLSX.utils.aoa_to_sheet(exportRows);

        // Merge header cells
        ws['!merges'] = [
            { s: { r: 0, c: 0 }, e: { r: 0, c: 3 } },
            { s: { r: 1, c: 0 }, e: { r: 1, c: 3 } },
            { s: { r: 2, c: 0 }, e: { r: 2, c: 3 } },
            { s: { r: 4, c: 0 }, e: { r: 4, c: 3 } },
            { s: { r: 5, c: 0 }, e: { r: 5, c: 3 } }
        ];
        ws['!cols'] = [
            { wch: 30 }, { wch: 12 }, { wch: 12 }, { wch: 16 }
        ];

        // Center align header cells
        ['A1', 'A2', 'A3', 'A5', 'A6'].forEach(function (cellRef) {
            if (ws[cellRef]) {
                ws[cellRef].s = ws[cellRef].s || {};
                ws[cellRef].s.alignment = { horizontal: "center" };
            }
        });


        var wb = XLSX.utils.book_new();
        XLSX.utils.book_append_sheet(wb, ws, "Nationality Report");
        XLSX.writeFile(wb, "NationalityReport.xlsx");
    });


    // Export to PDF
    $("#exportPdf").click(function () {
        const { jsPDF } = window.jspdf;
        var doc = new jsPDF('landscape', 'pt', 'a4');
        var pageWidth = doc.internal.pageSize.getWidth();

        // Multi-line headers just like the Excel (2nd image)
        var headerLines = [
            "Republic of the Philippines",
            "Province of Cebu",
            "Municipality of Alegria",
            "CANYONEERING",
            $("#dateFrom").val() && $("#dateTo").val()
                ? `${formatDate($("#dateFrom").val())} - ${formatDate($("#dateTo").val())}`
                : ""
        ];

        // Draw the headers
        doc.setFont("helvetica", "bold");
        doc.setFontSize(14);

        headerLines.forEach(function (line, idx) {
            if (line.trim().length === 0) return;
            // Dynamic y-position by line
            doc.text(line, pageWidth / 2, 30 + idx * 20, { align: "center" });
        });

        // Add space after headers
        let startY = 140;

        // AutoTable options
        doc.autoTable({
            html: "#nationalityReportTable",
            theme: 'grid',
            startY: startY,
            headStyles: {
                fillColor: [221, 221, 221],
                textColor: 40,
                fontSize: 12,
                fontStyle: 'bold'
            },
            styles: {
                font: "helvetica",
                fontSize: 11,
                halign: 'center',
                valign: 'middle'
            },
            didDrawPage: function (data) {
                // Optionally, can add a border or any extra styling here
            }
        });

        doc.save("NationalityReport.pdf");
    });

    // Helper to reformat date string
    function formatDate(isoDate) {
        if (!isoDate) return "";
        var date = new Date(isoDate);
        const options = { year: "numeric", month: "long", day: "numeric" };
        return date.toLocaleDateString(undefined, options);
    }


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
