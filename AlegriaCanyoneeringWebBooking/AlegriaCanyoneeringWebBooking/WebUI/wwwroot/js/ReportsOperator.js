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
        const pdf = new jsPDF({ orientation: "portrait", unit: "mm", format: "a4" });

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
        pdf.save(`OperatorReport_${new Date().toISOString().slice(0, 10)}.pdf`);
    });

    // ---- EXCEL EXPORT ----
    $("#btnDownloadExcel").on("click", async function () {
        if (typeof ExcelJS === "undefined") {
            alert("ExcelJS failed to load.");
            return;
        }

        const table = document.getElementById("operatorReportTable");
        if (!table) {
            alert("Table not found.");
            return;
        }

        // Read the DISPLAYED date text from the info-grid
        const infoGrid = document.querySelector(".info-grid");
        let dateFromText = "";
        let dateToText = "";

        if (infoGrid) {
            const infoCells = Array.from(infoGrid.querySelectorAll(".info-cell"));
            infoCells.forEach((cell, index) => {
                if (cell.innerText.trim() === "Date From:" && infoCells[index + 1]) {
                    dateFromText = infoCells[index + 1].innerText.trim();
                }
                if (cell.innerText.trim() === "Date To:" && infoCells[index + 1]) {
                    dateToText = infoCells[index + 1].innerText.trim();
                }
            });
        }

        const wb = new ExcelJS.Workbook();
        const ws = wb.addWorksheet("Operator Report", {
            pageSetup: {
                orientation: "portrait",
                fitToPage: true,
                fitToWidth: 1,
                fitToHeight: 1,
                paperSize: 9,
                margins: { left: 0.25, right: 0.25, top: 0.25, bottom: 0.25, header: 0, footer: 0 },
                horizontalCentered: true,
                verticalCentered: false
            }
        });

        // ---- Column widths (5 columns - WITH SEQ) ----
        ws.columns = [
            { width: 8 },   // Seq
            { width: 40 },  // Operator/Business Name (WIDER)
            { width: 12 },  // Male
            { width: 12 },  // Female
            { width: 15 }   // Ending Total
        ];

        // ---- Header Section ----
        ws.mergeCells("A1:E1");
        ws.getCell("A1").value = "Republic of the Philippines";
        ws.getCell("A1").font = { size: 11 };
        ws.getCell("A1").alignment = { horizontal: "center", vertical: "middle" };

        ws.mergeCells("A2:E2");
        ws.getCell("A2").value = "Province of Cebu";
        ws.getCell("A2").font = { size: 10 };
        ws.getCell("A2").alignment = { horizontal: "center", vertical: "middle" };

        ws.mergeCells("A3:E3");
        ws.getCell("A3").value = "Municipality of Alegria";
        ws.getCell("A3").font = { size: 10 };
        ws.getCell("A3").alignment = { horizontal: "center", vertical: "middle" };

        // Blank row
        ws.getRow(4).height = 5;

        // ---- Title Section ----
        ws.mergeCells("A5:E5");
        ws.getCell("A5").value = "CANYONEERING";
        ws.getCell("A5").font = { bold: true, size: 11 };
        ws.getCell("A5").alignment = { horizontal: "center", vertical: "middle" };

        ws.mergeCells("A6:E6");
        ws.getCell("A6").value = `${dateFromText} to ${dateToText}`;
        ws.getCell("A6").font = { bold: true, size: 10 };
        ws.getCell("A6").alignment = { horizontal: "center", vertical: "middle" };

        // Blank row
        ws.getRow(7).height = 5;

        // ---- Table Header (2 rows) - ROWS 8-9 ----
        // Row 8 - First header row
        ws.mergeCells("A8:A9");
        ws.getCell("A8").value = "Seq.";
        ws.getCell("A8").font = { bold: true, size: 10 };
        ws.getCell("A8").alignment = { horizontal: "center", vertical: "middle" };
        ws.getCell("A8").fill = {
            type: "pattern",
            pattern: "solid",
            fgColor: { argb: "FFFFFFFF" }
        };
        ws.getCell("A8").border = {
            top: { style: "thin" },
            left: { style: "thin" },
            bottom: { style: "thin" },
            right: { style: "thin" }
        };
        ws.getCell("A9").border = {
            top: { style: "thin" },
            left: { style: "thin" },
            bottom: { style: "thin" },
            right: { style: "thin" }
        };

        ws.mergeCells("B8:B9");
        ws.getCell("B8").value = "OPERATOR  ";
        ws.getCell("B8").font = { bold: true, size: 10 };
        ws.getCell("B8").alignment = { horizontal: "center", vertical: "middle" };
        ws.getCell("B8").fill = {
            type: "pattern",
            pattern: "solid",
            fgColor: { argb: "FFFFFFFF" }
        };
        ws.getCell("B8").border = {
            top: { style: "thin" },
            left: { style: "thin" },
            bottom: { style: "thin" },
            right: { style: "thin" }
        };
        ws.getCell("B9").border = {
            top: { style: "thin" },
            left: { style: "thin" },
            bottom: { style: "thin" },
            right: { style: "thin" }
        };

        ws.mergeCells("C8:D8");
        ws.getCell("C8").value = "NUMBER OF GUESTS";
        ws.getCell("C8").font = { bold: true, size: 10 };
        ws.getCell("C8").alignment = { horizontal: "center", vertical: "middle" };
        ws.getCell("C8").fill = {
            type: "pattern",
            pattern: "solid",
            fgColor: { argb: "FFFFFFFF" }
        };
        ws.getCell("C8").border = {
            top: { style: "thin" },
            left: { style: "thin" },
            bottom: { style: "thin" },
            right: { style: "thin" }
        };

        ws.mergeCells("E8:E9");
        ws.getCell("E8").value = "ENDING TOTAL";
        ws.getCell("E8").font = { bold: true, size: 10 };
        ws.getCell("E8").alignment = { horizontal: "center", vertical: "middle" };
        ws.getCell("E8").fill = {
            type: "pattern",
            pattern: "solid",
            fgColor: { argb: "FFFFFFFF" }
        };
        ws.getCell("E8").border = {
            top: { style: "thin" },
            left: { style: "thin" },
            bottom: { style: "thin" },
            right: { style: "thin" }
        };
        ws.getCell("E9").border = {
            top: { style: "thin" },
            left: { style: "thin" },
            bottom: { style: "thin" },
            right: { style: "thin" }
        };

        // Row 9 - Second header row (Male/Female)
        ws.getCell("C9").value = "MALE";
        ws.getCell("C9").font = { bold: true, size: 10 };
        ws.getCell("C9").alignment = { horizontal: "center", vertical: "middle" };
        ws.getCell("C9").fill = {
            type: "pattern",
            pattern: "solid",
            fgColor: { argb: "FFFFFFFF" }
        };
        ws.getCell("C9").border = {
            top: { style: "thin" },
            left: { style: "thin" },
            bottom: { style: "thin" },
            right: { style: "thin" }
        };

        ws.getCell("D9").value = "FEMALE";
        ws.getCell("D9").font = { bold: true, size: 10 };
        ws.getCell("D9").alignment = { horizontal: "center", vertical: "middle" };
        ws.getCell("D9").fill = {
            type: "pattern",
            pattern: "solid",
            fgColor: { argb: "FFFFFFFF" }
        };
        ws.getCell("D9").border = {
            top: { style: "thin" },
            left: { style: "thin" },
            bottom: { style: "thin" },
            right: { style: "thin" }
        };

        // ---- Body Rows (start at row 10) ----
        let currentRow = 10;
        const tbody = table.querySelector("tbody");
        if (tbody) {
            Array.from(tbody.querySelectorAll("tr")).forEach(tr => {
                const cells = Array.from(tr.querySelectorAll("td")).map(td => td.innerText.trim());
                if (cells.length >= 5 && !cells[1].includes("No data")) {
                    const row = ws.addRow([
                        Number(cells[0]) || 0,  // Seq
                        cells[1],               // Operator
                        Number(cells[2]) || 0,  // Male
                        Number(cells[3]) || 0,  // Female
                        Number(cells[4]) || 0   // Ending Total
                    ]);

                    // Style body cells
                    row.getCell(1).alignment = { horizontal: "center", vertical: "middle" };
                    row.getCell(2).alignment = { horizontal: "left", vertical: "middle" };
                    row.getCell(3).alignment = { horizontal: "center", vertical: "middle" };
                    row.getCell(3).numFmt = "#,##0";
                    row.getCell(4).alignment = { horizontal: "center", vertical: "middle" };
                    row.getCell(4).numFmt = "#,##0";
                    row.getCell(5).alignment = { horizontal: "center", vertical: "middle" };
                    row.getCell(5).numFmt = "#,##0";
                    row.getCell(5).font = { bold: true };

                    // Borders
                    for (let c = 1; c <= 5; c++) {
                        row.getCell(c).border = {
                            top: { style: "thin" },
                            left: { style: "thin" },
                            bottom: { style: "thin" },
                            right: { style: "thin" }
                        };
                    }
                    currentRow++;
                }
            });
        }

        // ---- Totals Row ----
        const tfoot = table.querySelector("tfoot tr");
        if (tfoot) {
            const tfootCells = Array.from(tfoot.querySelectorAll("td")).map(td => td.innerText.trim());
            const totalRow = ws.addRow([
                "",  // Empty Seq column
                tfootCells[0] || "TOTAL:",
                Number(tfootCells[1].replace(/[^0-9]/g, "")) || 0,
                Number(tfootCells[2].replace(/[^0-9]/g, "")) || 0,
                Number(tfootCells[3].replace(/[^0-9]/g, "")) || 0
            ]);

            totalRow.font = { bold: true, size: 10 };
            totalRow.getCell(1).alignment = { horizontal: "center", vertical: "middle" };
            totalRow.getCell(2).alignment = { horizontal: "left", vertical: "middle" };
            totalRow.getCell(3).alignment = { horizontal: "center", vertical: "middle" };
            totalRow.getCell(3).numFmt = "#,##0";
            totalRow.getCell(4).alignment = { horizontal: "center", vertical: "middle" };
            totalRow.getCell(4).numFmt = "#,##0";
            totalRow.getCell(5).alignment = { horizontal: "center", vertical: "middle" };
            totalRow.getCell(5).numFmt = "#,##0";

            totalRow.eachCell((cell) => {
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

        // ---- Footer Note ----
        const noteRow = ws.addRow([]);
        const noteIdx = noteRow.number;
        ws.mergeCells(`A${noteIdx}:E${noteIdx}`);
        ws.getCell(`A${noteIdx}`).value = "System Generated Report - Operator Summary";
        ws.getCell(`A${noteIdx}`).font = { italic: true, size: 9 };
        ws.getCell(`A${noteIdx}`).alignment = { horizontal: "left", vertical: "middle" };

        // ---- Print settings ----
        ws.pageSetup.printArea = `A1:E${noteIdx}`;
        ws.pageSetup.printTitlesRow = "8:9";

        // ---- Export ----
        const buffer = await wb.xlsx.writeBuffer();
        const blob = new Blob([buffer], { type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" });
        const a = document.createElement("a");
        a.href = URL.createObjectURL(blob);
        a.download = `OperatorReport_${new Date().toISOString().slice(0, 10)}.xlsx`;
        a.click();
    });
});