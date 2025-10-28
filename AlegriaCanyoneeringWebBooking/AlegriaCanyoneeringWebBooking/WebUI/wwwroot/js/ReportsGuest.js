$(document).ready(function () {
    // ---- PDF EXPORT ----
    $("#btnDownloadPDF").on("click", async function () {
        if (!window.jspdf) {
            alert("jsPDF failed to load.");
            return;
        }

        const element = document.querySelector(".report-container");
        if (!element) {
            alert("Report not found.");
            return;
        }

        const canvas = await html2canvas(element, { scale: 2, useCORS: true });
        const imgData = canvas.toDataURL("image/jpeg", 0.95);

        const { jsPDF } = window.jspdf;
        const pdf = new jsPDF({ orientation: "landscape", unit: "mm", format: "a4" });

        const pageW = pdf.internal.pageSize.getWidth();
        const pageH = pdf.internal.pageSize.getHeight();

        const imgProps = pdf.getImageProperties(imgData);
        const scale = Math.min(
            (pageW - 10) / imgProps.width,
            (pageH - 10) / imgProps.height
        );

        const w = imgProps.width * scale;
        const h = imgProps.height * scale;
        const x = (pageW - w) / 2;
        const y = (pageH - h) / 2;

        pdf.addImage(imgData, "JPEG", x, y, w, h);
        pdf.save(`GuestReport_${new Date().toISOString().slice(0, 10)}.pdf`);
    });

    // ---- EXCEL EXPORT ----
    $("#btnDownloadExcel").on("click", async function () {
        if (typeof ExcelJS === "undefined") {
            alert("ExcelJS failed to load.");
            return;
        }

        const table = document.getElementById("tourismReportTable");
        if (!table) {
            alert("Table not found.");
            return;
        }

        // Read the DISPLAYED date text from the info-grid
        const infoGrid = document.querySelector(".info-grid");
        let dateFromText = "";
        let dateToText = "";

        // ✅ NEW CODE - correctly reads from the grid layout
        const infoCells = Array.from(document.querySelectorAll(".info-grid .row .col-6, .info-grid .row .col-md-3"));
        infoCells.forEach((cell, i) => {
            const text = cell.innerText.trim();
            if (text === "Date From:" && infoCells[i + 1]) {
                dateFromText = infoCells[i + 1].innerText.trim();
            }
            if (text === "Date To:" && infoCells[i + 1]) {
                dateToText = infoCells[i + 1].innerText.trim();
            }
        });


        // Read DYNAMIC header text
        const headerRows = table.querySelectorAll("thead tr");
        let dateColumnHeader = "Date\nWeek Day\n(Mon-Sun)";
        if (headerRows.length > 0) {
            const headerCells = headerRows[0].querySelectorAll("th");
            if (headerCells.length > 1) {
                dateColumnHeader = headerCells[1].innerText.trim();
            }
        }

        // Read DYNAMIC footer label
        const tfootRow = table.querySelector("tfoot tr");
        let footerLabel = "Monthly Total";
        if (tfootRow) {
            const firstCell = tfootRow.querySelector("td");
            if (firstCell) {
                footerLabel = firstCell.innerText.trim();
            }
        }

        // Read DYNAMIC note text
        const noteElement = document.querySelector(".note-text em");
        let noteText = "Note: Total number must be recorded. Residence entries are optional. Monthly totals must be recorded.";
        if (noteElement) {
            noteText = noteElement.innerText.trim();
        }

        const meta = {
            title: "Tourism Attraction Visitor Record",
            subtitle: "(This recording form can be used instead of just counting the visitors)",
            dateFrom: dateFromText || "N/A",
            dateTo: dateToText || "N/A",
            municipality: "ALEGRIA, CEBU",
            spotName: "Canyoneering Adventure"
        };

        const wb = new ExcelJS.Workbook();
        const ws = wb.addWorksheet("Guest Report", {
            pageSetup: {
                orientation: "landscape",
                fitToPage: true,
                fitToWidth: 1,
                fitToHeight: 1,
                paperSize: 9,
                margins: {
                    left: 0.25,
                    right: 0.25,
                    top: 0.25,
                    bottom: 0.25,
                    header: 0,
                    footer: 0
                },
                horizontalCentered: true,
                verticalCentered: false
            }
        });

        // 14 columns (A..N) - WIDER Date column
        ws.columns = [
            { key: "A", width: 6 },   // Seq.
            { key: "B", width: 35 },  // Date (WIDER)
            { key: "C", width: 8 },   // This Province Male
            { key: "D", width: 8 },   // This Province Female
            { key: "E", width: 8 },   // This Province Total
            { key: "F", width: 8 },   // Other Province Male
            { key: "G", width: 8 },   // Other Province Female
            { key: "H", width: 8 },   // Other Province Total
            { key: "I", width: 8 },   // Foreign Male
            { key: "J", width: 8 },   // Foreign Female
            { key: "K", width: 8 },   // Foreign Total
            { key: "L", width: 9 },   // Grand Total Male
            { key: "M", width: 9 },   // Grand Total Female
            { key: "N", width: 9 }    // Grand Total
        ];

        // ---- Top title and metadata ----
        ws.mergeCells("A1:N1");
        ws.getCell("A1").value = meta.title;
        ws.getCell("A1").font = { bold: true, size: 14 };
        ws.getCell("A1").alignment = { horizontal: "center", vertical: "middle" };

        ws.mergeCells("A2:N2");
        ws.getCell("A2").value = meta.subtitle;
        ws.getCell("A2").font = { size: 10 };
        ws.getCell("A2").alignment = { horizontal: "center", vertical: "middle" };

        // Date row - FIXED MERGING TO SHOW FULL DATES
        ws.getCell("A3").value = "Date:";
        ws.getCell("A3").font = { bold: true };
        ws.getCell("A3").alignment = { horizontal: "left", vertical: "middle" };

        ws.mergeCells("B3:E3"); // Wider merge for Date From
        ws.getCell("B3").value = meta.dateFrom;
        ws.getCell("B3").alignment = { horizontal: "left", vertical: "middle" };

        ws.mergeCells("F3:G3");
        ws.getCell("F3").value = "Date To:";
        ws.getCell("F3").font = { bold: true };
        ws.getCell("F3").alignment = { horizontal: "left", vertical: "middle" };

        ws.mergeCells("H3:N3"); // WIDER merge for Date To - extends to column N
        ws.getCell("H3").value = meta.dateTo;
        ws.getCell("H3").alignment = { horizontal: "left", vertical: "middle" };

        // Municipality / Attraction row
        ws.mergeCells("A4:B4");
        ws.getCell("A4").value = "Municipality:";
        ws.getCell("A4").font = { bold: true };
        ws.getCell("A4").alignment = { horizontal: "left", vertical: "middle" };

        ws.mergeCells("C4:F4");
        ws.getCell("C4").value = meta.municipality;
        ws.getCell("C4").alignment = { horizontal: "left", vertical: "middle" };

        ws.mergeCells("G4:H4");
        ws.getCell("G4").value = "Attraction:";
        ws.getCell("G4").font = { bold: true };
        ws.getCell("G4").alignment = { horizontal: "left", vertical: "middle" };

        ws.mergeCells("I4:N4");
        ws.getCell("I4").value = meta.spotName;
        ws.getCell("I4").alignment = { horizontal: "left", vertical: "middle" };

        // Blank row
        ws.getRow(5).height = 5;

        // ---- Multi-row table header (rows 6-9) ----
        ws.mergeCells("A6:A9");
        ws.getCell("A6").value = "Seq.";
        ws.mergeCells("B6:B9");
        ws.getCell("B6").value = dateColumnHeader;
        ws.mergeCells("C6:K6");
        ws.getCell("C6").value = "*** Place of Residences";
        ws.mergeCells("L6:N8");
        ws.getCell("L6").value = "Grand Total Number of Visitors";

        // Row 7
        ws.mergeCells("C7:H7");
        ws.getCell("C7").value = "Philippines";
        ws.mergeCells("I7:K8");
        ws.getCell("I7").value = "Foreign Country Residence";

        // Row 8
        ws.mergeCells("C8:E8");
        ws.getCell("C8").value = "This Province";
        ws.mergeCells("F8:H8");
        ws.getCell("F8").value = "Other Province";

        // Row 9 (leaf headers)
        const headers9 = ["Male", "Female", "Total", "Male", "Female", "Total", "Male", "Female", "Total", "Male", "Female", "Total"];
        headers9.forEach((label, i) => {
            ws.getCell(9, 3 + i).value = label;
        });

        // Style header rows
        for (let r = 6; r <= 9; r++) {
            const row = ws.getRow(r);
            row.height = 18;
            row.eachCell({ includeEmpty: true }, (cell) => {
                cell.font = { bold: true, size: 9 };
                cell.alignment = { horizontal: "center", vertical: "middle", wrapText: true };
                cell.fill = {
                    type: "pattern",
                    pattern: "solid",
                    fgColor: { argb: "FFFFFFFF" }
                };
                cell.border = {
                    top: { style: "thin" },
                    left: { style: "thin" },
                    bottom: { style: "thin" },
                    right: { style: "thin" }
                };
            });
        }

        // ---- Body rows from the HTML table ----
        const bodyStartRow = 10;
        const domRows = Array.from(table.querySelectorAll("tbody tr"));

        domRows.forEach(tr => {
            const cells = Array.from(tr.querySelectorAll("td")).map(td => td.innerText.trim());
            if (cells.length > 0 && !cells[0].includes("No data")) {
                ws.addRow(cells);
            }
        });

        // Style body rows
        const lastBodyRow = ws.lastRow.number;
        for (let r = bodyStartRow; r <= lastBodyRow; r++) {
            const row = ws.getRow(r);
            row.height = 20; // Increased height

            for (let c = 1; c <= 14; c++) {
                const cell = row.getCell(c);
                const isNumericCol = c >= 3;

                if (isNumericCol) {
                    const val = cell.value;
                    const num = Number(String(val).replace(/[^0-9.-]/g, ""));
                    if (!isNaN(num)) cell.value = num;
                    cell.numFmt = "#,##0";
                }

                // Special handling for date column (column B)
                if (c === 2) {
                    cell.alignment = {
                        horizontal: "center",
                        vertical: "middle",
                        wrapText: false,
                        shrinkToFit: false
                    };
                    cell.font = { size: 10 };
                } else {
                    cell.alignment = {
                        horizontal: "center",
                        vertical: "middle"
                    };
                }

                cell.border = {
                    top: { style: "thin" },
                    left: { style: "thin" },
                    bottom: { style: "thin" },
                    right: { style: "thin" }
                };
            }
        }

        // ---- Totals row from tfoot ----
        if (tfootRow) {
            const tfootCells = Array.from(tfootRow.querySelectorAll("td")).map(td => td.innerText.trim());
            const numericCells = tfootCells.slice(1);

            const totalRow = ws.addRow([]);
            const totalIdx = totalRow.number;

            ws.mergeCells(`A${totalIdx}:B${totalIdx}`);
            ws.getCell(`A${totalIdx}`).value = footerLabel;
            ws.getCell(`A${totalIdx}`).font = { bold: true, size: 10 };
            ws.getCell(`A${totalIdx}`).alignment = { horizontal: "center", vertical: "middle" };
            ws.getCell(`A${totalIdx}`).fill = {
                type: "pattern",
                pattern: "solid",
                fgColor: { argb: "FFFFFFFF" }
            };
            ws.getCell(`A${totalIdx}`).border = {
                top: { style: "thin" },
                left: { style: "thin" },
                bottom: { style: "thin" },
                right: { style: "thin" }
            };

            numericCells.forEach((value, index) => {
                const colIdx = index + 3;
                const cell = ws.getRow(totalIdx).getCell(colIdx);

                const num = Number(String(value).replace(/[^0-9.-]/g, ""));
                if (!isNaN(num)) cell.value = num;
                cell.numFmt = "#,##0";

                cell.font = { bold: true, size: 10 };
                cell.alignment = { horizontal: "center", vertical: "middle" };
                cell.fill = {
                    type: "pattern",
                    pattern: "solid",
                    fgColor: { argb: "FFFFFFFF" }
                };
                cell.border = {
                    top: { style: "thin" },
                    left: { style: "thin" },
                    bottom: { style: "thin" },
                    right: { style: "thin" }
                };
            });
        }

        // ---- Note row ----
        const noteRow = ws.addRow([]);
        const noteIdx = noteRow.number;
        ws.mergeCells(`A${noteIdx}:N${noteIdx}`);
        ws.getCell(`A${noteIdx}`).value = noteText;
        ws.getCell(`A${noteIdx}`).font = { italic: true, size: 9 };
        ws.getCell(`A${noteIdx}`).alignment = { horizontal: "left", vertical: "middle", wrapText: true };

        // ---- Print settings ----
        ws.pageSetup.printArea = `A1:N${noteIdx}`;
        ws.pageSetup.printTitlesRow = "6:9";

        // ---- Export ----
        const buffer = await wb.xlsx.writeBuffer();
        const blob = new Blob([buffer], {
            type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        });
        const a = document.createElement("a");
        a.href = URL.createObjectURL(blob);
        a.download = `GuestReport_${new Date().toISOString().slice(0, 10)}.xlsx`;
        a.click();
    });

});