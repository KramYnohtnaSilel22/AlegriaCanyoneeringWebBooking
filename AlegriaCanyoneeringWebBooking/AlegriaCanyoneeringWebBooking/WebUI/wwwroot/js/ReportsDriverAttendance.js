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
        const scale = Math.min((pageW - 10) / imgProps.width, (pageH - 10) / imgProps.height);
        const w = imgProps.width * scale;
        const h = imgProps.height * scale;
        const x = (pageW - w) / 2;
        const y = (pageH - h) / 2;

        pdf.addImage(imgData, "JPEG", x, y, w, h);
        pdf.save(`DriverAttendance_${new Date().toISOString().slice(0, 10)}.pdf`);
    });

    // ---- EXCEL EXPORT ----
    $("#btnDownloadExcel").on("click", async function () {
        if (typeof ExcelJS === "undefined") {
            alert("ExcelJS failed to load.");
            return;
        }

        const table = document.getElementById("driverAttendanceTable");
        if (!table) {
            alert("Table not found.");
            return;
        }

        // ---- Read dates ----
        let dateFromText = "N/A";
        let dateToText = "N/A";

        const displayFrom = document.getElementById("displayDateFrom");
        const displayTo = document.getElementById("displayDateTo");
        if (displayFrom) dateFromText = displayFrom.innerText.trim();
        if (displayTo) dateToText = displayTo.innerText.trim();

        const wb = new ExcelJS.Workbook();
        const ws = wb.addWorksheet("Driver Attendance", {
            pageSetup: {
                orientation: "portrait",
                fitToPage: true,
                fitToWidth: 1,
                fitToHeight: 1,
                paperSize: 9,
                margins: { left: 0.25, right: 0.25, top: 0.25, bottom: 0.25, header: 0, footer: 0 },
                horizontalCentered: true
            }
        });

        // ---- Column widths (5 columns) ----
        ws.columns = [
            { width: 8 },  // Seq
            { width: 38 },  // Driver Name
            { width: 18 },  // Ref ID
            { width: 28 },  // Date
            { width: 14 }   // Passengers
        ];

        // ---- Government header ----
        const headerStyle = (cell, value, bold, size) => {
            cell.value = value;
            cell.font = { bold: bold ?? false, size: size ?? 10 };
            cell.alignment = { horizontal: "center", vertical: "middle" };
        };

        ws.mergeCells("A1:E1");
        headerStyle(ws.getCell("A1"), "Republic of the Philippines", false, 11);
        ws.mergeCells("A2:E2");
        headerStyle(ws.getCell("A2"), "Province of Cebu", false, 10);
        ws.mergeCells("A3:E3");
        headerStyle(ws.getCell("A3"), "Municipality of Alegria", false, 10);

        ws.getRow(4).height = 5;

        ws.mergeCells("A5:E5");
        headerStyle(ws.getCell("A5"), "Driver Attendance Record", true, 13);

        ws.mergeCells("A6:E6");
        headerStyle(ws.getCell("A6"), `${dateFromText} - ${dateToText}`, true, 10);

        ws.getRow(7).height = 5;

        // ---- Table header (row 8) ----
        const thCells = [
            { col: "A", label: "Seq." },
            { col: "B", label: "Driver Name" },
            { col: "C", label: "Ref ID" },
            { col: "D", label: "Date" },
            { col: "E", label: "Passengers" }
        ];

        thCells.forEach(({ col, label }) => {
            const cell = ws.getCell(`${col}8`);
            cell.value = label;
            cell.font = { bold: true, size: 10 };
            cell.alignment = { horizontal: "center", vertical: "middle" };
            cell.fill = { type: "pattern", pattern: "solid", fgColor: { argb: "FFE0F7FA" } };
            cell.border = {
                top: { style: "thin" }, left: { style: "thin" },
                bottom: { style: "thin" }, right: { style: "thin" }
            };
        });

        ws.getRow(8).height = 18;

        // ---- Body rows ----
        const tbody = table.querySelector("tbody");
        if (tbody) {
            Array.from(tbody.querySelectorAll("tr")).forEach(tr => {
                const cells = Array.from(tr.querySelectorAll("td")).map(td => td.innerText.trim());
                if (cells.length >= 5 && !cells[1].includes("No attendance")) {
                    const row = ws.addRow([
                        Number(cells[0]) || 0,  // Seq
                        cells[1],               // Driver Name
                        cells[2],               // Ref ID
                        cells[3],               // Date
                        Number(cells[4]) || 0   // Passengers
                    ]);

                    row.getCell(1).alignment = { horizontal: "center", vertical: "middle" };
                    row.getCell(2).alignment = { horizontal: "left", vertical: "middle" };
                    row.getCell(3).alignment = { horizontal: "center", vertical: "middle" };
                    row.getCell(4).alignment = { horizontal: "center", vertical: "middle" };
                    row.getCell(5).alignment = { horizontal: "center", vertical: "middle" };
                    row.getCell(5).numFmt = "#,##0";
                    row.getCell(5).font = { bold: true };

                    for (let c = 1; c <= 5; c++) {
                        row.getCell(c).border = {
                            top: { style: "thin" }, left: { style: "thin" },
                            bottom: { style: "thin" }, right: { style: "thin" }
                        };
                    }
                }
            });
        }

        // ---- Totals row ----
        const tfoot = table.querySelector("tfoot tr");
        if (tfoot) {
            const tfCells = Array.from(tfoot.querySelectorAll("td")).map(td => td.innerText.trim());
            const totalRow = ws.addRow([
                "", "", "", "TOTAL:",
                Number(tfCells[tfCells.length - 1].replace(/[^0-9]/g, "")) || 0
            ]);

            totalRow.font = { bold: true, size: 10 };
            totalRow.getCell(4).alignment = { horizontal: "right", vertical: "middle" };
            totalRow.getCell(5).alignment = { horizontal: "center", vertical: "middle" };
            totalRow.getCell(5).numFmt = "#,##0";

            totalRow.eachCell(cell => {
                cell.fill = { type: "pattern", pattern: "solid", fgColor: { argb: "FFF5F5F5" } };
                cell.border = {
                    top: { style: "thin" }, left: { style: "thin" },
                    bottom: { style: "thin" }, right: { style: "thin" }
                };
            });
        }

        // ---- Footer note ----
        const noteRow = ws.addRow([]);
        const noteIdx = noteRow.number;
        ws.mergeCells(`A${noteIdx}:E${noteIdx}`);
        ws.getCell(`A${noteIdx}`).value = "System Generated Report - Driver Attendance Summary";
        ws.getCell(`A${noteIdx}`).font = { italic: true, size: 9 };
        ws.getCell(`A${noteIdx}`).alignment = { horizontal: "left", vertical: "middle" };

        ws.pageSetup.printArea = `A1:E${noteIdx}`;
        ws.pageSetup.printTitlesRow = "8:8";

        // ---- Export ----
        const buffer = await wb.xlsx.writeBuffer();
        const blob = new Blob([buffer], {
            type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        });
        const a = document.createElement("a");
        a.href = URL.createObjectURL(blob);
        a.download = `DriverAttendance_${new Date().toISOString().slice(0, 10)}.xlsx`;
        a.click();
    });
});