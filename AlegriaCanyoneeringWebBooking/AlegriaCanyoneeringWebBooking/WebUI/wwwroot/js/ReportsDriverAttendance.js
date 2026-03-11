$(document).ready(function () {

    // ---- AUTO TOTAL (live sum of GUEST column) ----
    function recalcTotal() {
        let total = 0;
        $("#driverAttendanceTable tbody tr").each(function () {
            const val = parseInt($(this).find("td.col-guest").data("passenger")) || 0;
            total += val;
        });
        $("#grandTotal").text(total);
    }
    recalcTotal();

    // ---- PDF EXPORT ----
    $("#btnDownloadPDF").on("click", async function () {
        if (!window.jspdf) { alert("jsPDF failed to load."); return; }

        const element = document.getElementById("printArea");
        if (!element) { alert("Report not found."); return; }

        const canvas = await html2canvas(element, { scale: 2, useCORS: true, backgroundColor: "#fff" });
        const imgData = canvas.toDataURL("image/jpeg", 0.95);

        const { jsPDF } = window.jspdf;
        const pdf = new jsPDF({ orientation: "portrait", unit: "mm", format: "a4" });

        const pageW = pdf.internal.pageSize.getWidth();
        const pageH = pdf.internal.pageSize.getHeight();
        const margin = 8;
        const imgProps = pdf.getImageProperties(imgData);
        const scale = Math.min(
            (pageW - margin * 2) / imgProps.width,
            (pageH - margin * 2) / imgProps.height
        );
        const w = imgProps.width * scale;
        const h = imgProps.height * scale;

        pdf.addImage(imgData, "JPEG", (pageW - w) / 2, margin, w, h);
        pdf.save(`DriverAttendance_${new Date().toISOString().slice(0, 10)}.pdf`);
    });

    // ---- EXCEL EXPORT ----
    $("#btnDownloadExcel").on("click", async function () {
        if (typeof ExcelJS === "undefined") { alert("ExcelJS failed to load."); return; }

        const table = document.getElementById("driverAttendanceTable");
        if (!table) { alert("Table not found."); return; }

        const period = document.getElementById("displayPeriod")?.innerText?.trim() || "N/A";

        const wb = new ExcelJS.Workbook();
        const ws = wb.addWorksheet("Driver Attendance", {
            pageSetup: {
                orientation: "portrait",
                fitToPage: true, fitToWidth: 1, fitToHeight: 1,
                paperSize: 9,
                margins: { left: 0.25, right: 0.25, top: 0.25, bottom: 0.25, header: 0, footer: 0 },
                horizontalCentered: true
            }
        });

        // ---- Column widths ----
        ws.columns = [
            { width: 6 },   // Seq
            { width: 32 },   // Name
            { width: 10 },   // Guest
            { width: 14 },   // Time
            { width: 28 }    // Route
        ];

        // Helper
        const setCell = (addr, value, opts = {}) => {
            const cell = ws.getCell(addr);
            cell.value = value;
            cell.font = { bold: opts.bold ?? false, size: opts.size ?? 10, ...(opts.font || {}) };
            cell.alignment = { horizontal: opts.align ?? "center", vertical: "middle", wrapText: opts.wrap ?? false };
            if (opts.fill) cell.fill = { type: "pattern", pattern: "solid", fgColor: { argb: opts.fill } };
            if (opts.border) cell.border = { top: { style: "thin" }, left: { style: "thin" }, bottom: { style: "thin" }, right: { style: "thin" } };
        };

        const borderAll = (cell) => {
            cell.border = { top: { style: "thin" }, left: { style: "thin" }, bottom: { style: "thin" }, right: { style: "thin" } };
        };

        // ---- Gov Header ----
        ws.mergeCells("A1:E1"); setCell("A1", "TOURIST MOTORCYCLE DRIVER", { bold: true, size: 13 });
        ws.mergeCells("A2:E2"); setCell("A2", "Project", { size: 10 });
        ws.mergeCells("A3:E3"); setCell("A3", "MUNICIPALITY OF ALEGRIA", { bold: true, size: 11 });
        ws.mergeCells("A4:E4"); setCell("A4", `PERIOD: ${period}`, { bold: true, size: 10 });
        ws.getRow(5).height = 4;

        // ---- Table Header row 6 ----
        const thFill = "FFEEEEEE";
        setCell("A6", "", { bold: true, fill: thFill, border: true });
        setCell("B6", "NAME", { bold: true, fill: thFill, border: true });
        setCell("C6", "GUEST", { bold: true, fill: thFill, border: true });
        setCell("D6", "TIME", { bold: true, fill: thFill, border: true });
        setCell("E6", "ROUTE", { bold: true, fill: thFill, border: true });
        ws.getRow(6).height = 16;

        // ---- Body rows (start at row 7) ----
        const tbody = table.querySelector("tbody");
        let grandTotal = 0;
        let rowIdx = 7;

        if (tbody) {
            Array.from(tbody.querySelectorAll("tr")).forEach(tr => {
                const tds = Array.from(tr.querySelectorAll("td")).map(td => td.innerText.trim());
                const seq = tds[0] || "";
                const name = tds[1] || "";
                const guestVal = parseInt(tds[2]) || 0;
                const time = tds[3] || "";
                const route = tds[4] || "";

                if (guestVal > 0) grandTotal += guestVal;

                const row = ws.getRow(rowIdx);
                row.height = 16;

                const seqCell = row.getCell(1);
                const nameCell = row.getCell(2);
                const guestCell = row.getCell(3);
                const timeCell = row.getCell(4);
                const routeCell = row.getCell(5);

                seqCell.value = seq;
                nameCell.value = name;
                guestCell.value = guestVal > 0 ? guestVal : "";
                timeCell.value = time;
                routeCell.value = route;

                seqCell.alignment = { horizontal: "center", vertical: "middle" };
                nameCell.alignment = { horizontal: "left", vertical: "middle" };
                guestCell.alignment = { horizontal: "center", vertical: "middle" };
                timeCell.alignment = { horizontal: "center", vertical: "middle" };
                routeCell.alignment = { horizontal: "left", vertical: "middle" };

                if (guestVal > 0) {
                    guestCell.numFmt = "#,##0";
                    guestCell.font = { bold: true };
                }

                [seqCell, nameCell, guestCell, timeCell, routeCell].forEach(borderAll);
                rowIdx++;
            });
        }

        // ---- Total Row ----
        const totalRow = ws.getRow(rowIdx);
        totalRow.height = 16;

        ws.mergeCells(`A${rowIdx}:B${rowIdx}`);
        const totalLabelCell = totalRow.getCell(1);
        totalLabelCell.value = "TOTAL:";
        totalLabelCell.font = { bold: true, size: 10 };
        totalLabelCell.alignment = { horizontal: "right", vertical: "middle" };
        borderAll(totalLabelCell);

        const totalValCell = totalRow.getCell(3);
        totalValCell.value = grandTotal;
        totalValCell.font = { bold: true, size: 10 };
        totalValCell.numFmt = "#,##0";
        totalValCell.alignment = { horizontal: "center", vertical: "middle" };
        totalValCell.fill = { type: "pattern", pattern: "solid", fgColor: { argb: "FFF5F5F5" } };
        borderAll(totalValCell);

        [totalRow.getCell(4), totalRow.getCell(5)].forEach(c => {
            c.value = "";
            borderAll(c);
        });

        rowIdx++;
        ws.getRow(rowIdx).height = 8;
        rowIdx++;

        // ---- Footer Certification ----
        const certRow = rowIdx;
        ws.mergeCells(`A${certRow}:A${certRow + 3}`);
        ws.getCell(`A${certRow}`).value = "CERTIFIED\nEach person whose name appears on this roll had rendered services for the time stated.";
        ws.getCell(`A${certRow}`).font = { size: 9 };
        ws.getCell(`A${certRow}`).alignment = { horizontal: "left", vertical: "top", wrapText: true };

        ws.mergeCells(`B${certRow}:C${certRow + 3}`);
        ws.getCell(`B${certRow}`).value = "Approved  for Payment:";
        ws.getCell(`B${certRow}`).font = { size: 10 };
        ws.getCell(`B${certRow}`).alignment = { horizontal: "center", vertical: "middle" };

        ws.mergeCells(`D${certRow}:E${certRow + 3}`);
        ws.getCell(`D${certRow}`).value = "Each person whose name appears on the above roll has been paid the amount stated opposite his name after identifying them.";
        ws.getCell(`D${certRow}`).font = { size: 9 };
        ws.getCell(`D${certRow}`).alignment = { horizontal: "left", vertical: "top", wrapText: true };

        for (let r = certRow; r <= certRow + 3; r++) {
            ws.getRow(r).height = 14;
        }

        rowIdx = certRow + 4;

        // Signature lines
        setCell(`A${rowIdx}`, "PICHEY L. LENDIO", { bold: true, size: 10, align: "center", border: true });
        setCell(`C${rowIdx}`, "HON. VERNA V. MAGALLON", { bold: true, size: 10, align: "center", border: true });
        setCell(`E${rowIdx}`, "JOSE WILSON C PATRIARCA", { bold: true, size: 10, align: "center", border: true });

        rowIdx++;
        setCell(`A${rowIdx}`, "Name & Signature of Timekeeper", { size: 9, align: "center" });
        setCell(`C${rowIdx}`, "Name & Signature of Approving Officer", { size: 9, align: "center" });
        setCell(`E${rowIdx}`, "Name & Signature of Disbursing", { size: 9, align: "center" });

        // ---- System Generated row ----
        rowIdx += 2;
        ws.mergeCells(`A${rowIdx}:E${rowIdx}`);
        ws.getCell(`A${rowIdx}`).value = "System Generated Report — Driver Attendance Summary";
        ws.getCell(`A${rowIdx}`).font = { italic: true, size: 9, color: { argb: "FF888888" } };
        ws.getCell(`A${rowIdx}`).alignment = { horizontal: "right", vertical: "middle" };

        ws.pageSetup.printArea = `A1:E${rowIdx}`;
        ws.pageSetup.printTitlesRow = "6:6";

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