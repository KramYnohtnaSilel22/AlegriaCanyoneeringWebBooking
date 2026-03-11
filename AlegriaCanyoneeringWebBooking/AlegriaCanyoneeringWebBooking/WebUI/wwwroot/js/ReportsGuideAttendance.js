$(document).ready(function () {

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
        pdf.save(`GuideAttendance_${new Date().toISOString().slice(0, 10)}.pdf`);
    });

    // ---- EXCEL EXPORT ----
    $("#btnDownloadExcel").on("click", async function () {
        if (typeof ExcelJS === "undefined") { alert("ExcelJS failed to load."); return; }

        const table = document.getElementById("guideAttendanceTable");
        if (!table) { alert("Table not found."); return; }

        const period = document.getElementById("displayPeriod")?.innerText?.trim() || "N/A";

        const wb = new ExcelJS.Workbook();
        const ws = wb.addWorksheet("Guide Attendance", {
            pageSetup: {
                orientation: "portrait",
                fitToPage: true, fitToWidth: 1, fitToHeight: 1,
                paperSize: 9,
                margins: { left: 0.25, right: 0.25, top: 0.25, bottom: 0.25, header: 0, footer: 0 },
                horizontalCentered: true
            }
        });

        // ---- Column widths (4 cols) ----
        ws.columns = [
            { width: 6 },   // Seq
            { width: 32 },   // Name
            { width: 22 },   // Date & Time
            { width: 26 }    // Route
        ];

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
        ws.mergeCells("A1:D1"); setCell("A1", "TOURIST TOUR GUIDE", { bold: true, size: 13 });
        ws.mergeCells("A2:D2"); setCell("A2", "Project", { size: 10 });
        ws.mergeCells("A3:D3"); setCell("A3", "MUNICIPALITY OF ALEGRIA", { bold: true, size: 11 });
        ws.mergeCells("A4:D4"); setCell("A4", `PERIOD: ${period}`, { bold: true, size: 10 });
        ws.getRow(5).height = 4;

        // ---- Table Header row 6 ----
        const thFill = "FFEEEEEE";
        setCell("A6", "", { bold: true, fill: thFill, border: true });
        setCell("B6", "NAME", { bold: true, fill: thFill, border: true });
        setCell("C6", "DATE & TIME", { bold: true, fill: thFill, border: true });
        setCell("D6", "ROUTE", { bold: true, fill: thFill, border: true });
        ws.getRow(6).height = 16;

        // ---- Body rows ----
        const tbody = table.querySelector("tbody");
        let rowIdx = 7;

        if (tbody) {
            Array.from(tbody.querySelectorAll("tr")).forEach(tr => {
                const tds = Array.from(tr.querySelectorAll("td")).map(td => td.innerText.trim());
                // Skip no-data colspan row
                if (tds.length < 4) return;

                const row = ws.getRow(rowIdx);
                row.height = 16;

                row.getCell(1).value = tds[0];
                row.getCell(2).value = tds[1];
                row.getCell(3).value = tds[2];
                row.getCell(4).value = tds[3];

                row.getCell(1).alignment = { horizontal: "center", vertical: "middle" };
                row.getCell(2).alignment = { horizontal: "left", vertical: "middle" };
                row.getCell(3).alignment = { horizontal: "center", vertical: "middle" };
                row.getCell(4).alignment = { horizontal: "left", vertical: "middle" };

                [1, 2, 3, 4].forEach(c => borderAll(row.getCell(c)));
                rowIdx++;
            });
        }

        // Spacer
        ws.getRow(rowIdx).height = 8; rowIdx++;

        // ---- Footer Certification ----
        const certRow = rowIdx;
        for (let r = certRow; r <= certRow + 4; r++) ws.getRow(r).height = 13;

        ws.mergeCells(`A${certRow}:A${certRow + 4}`);
        ws.getCell(`A${certRow}`).value = "CERTIFIED\nEach person whose name appears on this roll had rendered services for the time stated.";
        ws.getCell(`A${certRow}`).font = { size: 9 };
        ws.getCell(`A${certRow}`).alignment = { horizontal: "left", vertical: "top", wrapText: true };

        ws.mergeCells(`B${certRow}:C${certRow + 4}`);
        ws.getCell(`B${certRow}`).value = "Approved for Payment:";
        ws.getCell(`B${certRow}`).font = { size: 10 };
        ws.getCell(`B${certRow}`).alignment = { horizontal: "center", vertical: "middle" };

        ws.mergeCells(`D${certRow}:D${certRow + 4}`);
        ws.getCell(`D${certRow}`).value = "Each person whose name appears on the above roll has been paid the amount stated opposite his name after identifying them.";
        ws.getCell(`D${certRow}`).font = { size: 9 };
        ws.getCell(`D${certRow}`).alignment = { horizontal: "left", vertical: "top", wrapText: true };

        rowIdx = certRow + 5;

        // Sig lines
        setCell(`A${rowIdx}`, "PICHEY L. LENDIO", { bold: true, size: 10, border: true });
        setCell(`B${rowIdx}`, "HON. VERNA V. MAGALLON", { bold: true, size: 10, border: true });
        ws.mergeCells(`B${rowIdx}:C${rowIdx}`);
        setCell(`D${rowIdx}`, "JOSE WILSON C PATRIARCA", { bold: true, size: 10, border: true });

        rowIdx++;
        setCell(`A${rowIdx}`, "Name & Signature of Timekeeper", { size: 9 });
        ws.mergeCells(`B${rowIdx}:C${rowIdx}`);
        setCell(`B${rowIdx}`, "Name & Signature of Approving Officer", { size: 9 });
        setCell(`D${rowIdx}`, "Name & Signature of Disbursing", { size: 9 });

        // ---- System Generated row ----
        rowIdx += 2;
        ws.mergeCells(`A${rowIdx}:D${rowIdx}`);
        ws.getCell(`A${rowIdx}`).value = "System Generated Report — Guide Attendance Summary";
        ws.getCell(`A${rowIdx}`).font = { italic: true, size: 9, color: { argb: "FF888888" } };
        ws.getCell(`A${rowIdx}`).alignment = { horizontal: "right", vertical: "middle" };

        ws.pageSetup.printArea = `A1:D${rowIdx}`;
        ws.pageSetup.printTitlesRow = "6:6";

        // ---- Export ----
        const buffer = await wb.xlsx.writeBuffer();
        const blob = new Blob([buffer], {
            type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        });
        const a = document.createElement("a");
        a.href = URL.createObjectURL(blob);
        a.download = `GuideAttendance_${new Date().toISOString().slice(0, 10)}.xlsx`;
        a.click();
    });
});