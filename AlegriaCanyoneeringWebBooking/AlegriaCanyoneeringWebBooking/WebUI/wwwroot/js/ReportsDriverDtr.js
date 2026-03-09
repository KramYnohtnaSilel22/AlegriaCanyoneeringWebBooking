$(document).ready(function () {

    const RATE = 100;

    // ---- AUTO COMPUTE TOTALS ----
    function recalcTotals() {
        let totalPax = 0;
        let totalGross = 0;
        let totalNet = 0;

        $("#driverDtrTable tbody tr").each(function () {
            const pax = parseFloat($(this).find("td.col-pax").data("pax")) || 0;
            const gross = parseFloat($(this).find("td.col-gross").data("gross")) || 0;
            const net = parseFloat($(this).find("td.col-net").data("net")) || 0;
            totalPax += pax;
            totalGross += gross;
            totalNet += net;
        });

        $("#grandTotalPax").text(totalPax);
        $("#grandTotalGross").text(totalGross.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 }));
        $("#grandTotalNet").text(totalNet.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 }));
    }
    recalcTotals();

    // ---- PDF EXPORT ----
    $("#btnDownloadPDF").on("click", async function () {
        if (!window.jspdf) { alert("jsPDF failed to load."); return; }

        const element = document.getElementById("printArea");
        if (!element) { alert("Report not found."); return; }

        const canvas = await html2canvas(element, { scale: 2, useCORS: true, backgroundColor: "#fff" });
        const imgData = canvas.toDataURL("image/jpeg", 0.95);

        const { jsPDF } = window.jspdf;
        const pdf = new jsPDF({ orientation: "landscape", unit: "mm", format: "a4" });

        const pageW = pdf.internal.pageSize.getWidth();
        const pageH = pdf.internal.pageSize.getHeight();
        const margin = 6;
        const imgProps = pdf.getImageProperties(imgData);
        const scale = Math.min(
            (pageW - margin * 2) / imgProps.width,
            (pageH - margin * 2) / imgProps.height
        );
        const w = imgProps.width * scale;
        const h = imgProps.height * scale;

        pdf.addImage(imgData, "JPEG", (pageW - w) / 2, margin, w, h);
        pdf.save(`DriverDTR_${new Date().toISOString().slice(0, 10)}.pdf`);
    });

    // ---- EXCEL EXPORT ----
    $("#btnDownloadExcel").on("click", async function () {
        if (typeof ExcelJS === "undefined") { alert("ExcelJS failed to load."); return; }

        const table = document.getElementById("driverDtrTable");
        if (!table) { alert("Table not found."); return; }

        const period = document.getElementById("displayPeriod")?.innerText?.trim() || "N/A";
        const area = document.getElementById("displayArea")?.innerText?.trim() || "ALL AREAS";

        const wb = new ExcelJS.Workbook();
        const ws = wb.addWorksheet("Driver DTR", {
            pageSetup: {
                orientation: "landscape",
                fitToPage: true, fitToWidth: 1, fitToHeight: 1,
                paperSize: 9,
                margins: { left: 0.2, right: 0.2, top: 0.2, bottom: 0.2, header: 0, footer: 0 },
                horizontalCentered: true
            }
        });

        // ---- Column widths (13 cols) ----
        ws.columns = [
            { width: 5 },   // Seq
            { width: 24 },   // Name
            { width: 14 },   // Address
            { width: 12 },   // Designation
            { width: 10 },   // Num Passenger
            { width: 8 },   // Rate/Day
            { width: 12 },   // Gross Salary
            { width: 12 },   // Net Pay
            { width: 5 },   // Seq2
            { width: 14 },   // Signature
            { width: 10 },   // CTax Number
            { width: 10 },   // CTax Date
            { width: 14 }    // Place of Issue
        ];

        // Helper
        const sc = (addr, value, opts = {}) => {
            const cell = ws.getCell(addr);
            cell.value = value;
            cell.font = { bold: opts.bold ?? false, size: opts.size ?? 10, ...(opts.font || {}) };
            cell.alignment = { horizontal: opts.align ?? "center", vertical: "middle", wrapText: opts.wrap ?? false };
            if (opts.fill) cell.fill = { type: "pattern", pattern: "solid", fgColor: { argb: opts.fill } };
            if (opts.border !== false) {
                cell.border = { top: { style: "thin" }, left: { style: "thin" }, bottom: { style: "thin" }, right: { style: "thin" } };
            }
        };
        const border = (cell) => {
            cell.border = { top: { style: "thin" }, left: { style: "thin" }, bottom: { style: "thin" }, right: { style: "thin" } };
        };

        // ---- Gov Header rows 1–4 ----
        ws.mergeCells("A1:M1"); sc("A1", "Project", { bold: false, size: 11, border: false });
        ws.mergeCells("A2:F2"); sc("A2", "LGU :  MUNICIPALITY OF ALEGRIA", { bold: true, size: 11, align: "left", border: false });
        ws.mergeCells("H2:M2"); sc("H2", `Period: ${period}`, { bold: true, size: 10, align: "right", border: false });
        ws.mergeCells("A3:F3"); sc("A3", area, { bold: true, size: 12, align: "left", border: false });
        ws.getRow(4).height = 5;

        // ---- Table Header rows 5–6 (merged cells for rowspan=2) ----
        const thFill = "FFEEEEEE";
        const thOpts = { bold: true, size: 9, fill: thFill, wrap: true };

        // Row 5 — spans rows 5&6 for most cols, colspan 3 for Community Tax
        ws.mergeCells("A5:A6"); sc("A5", "", { ...thOpts });
        ws.mergeCells("B5:B6"); sc("B5", "Name", { ...thOpts });
        ws.mergeCells("C5:C6"); sc("C5", "Address", { ...thOpts });
        ws.mergeCells("D5:D6"); sc("D5", "Designation", { ...thOpts });
        ws.mergeCells("E5:E6"); sc("E5", "Number of\nPassenger", { ...thOpts });
        ws.mergeCells("F5:F6"); sc("F5", "Rate per\nDay", { ...thOpts });
        ws.mergeCells("G5:G6"); sc("G5", "Gross\nSalary", { ...thOpts });
        ws.mergeCells("H5:H6"); sc("H5", "NET PAY", { ...thOpts });
        ws.mergeCells("I5:I6"); sc("I5", "", { ...thOpts });
        ws.mergeCells("J5:J6"); sc("J5", "Signature or\nThumbmark", { ...thOpts });
        ws.mergeCells("K5:M5"); sc("K5", "COMMUNITY TAX", { ...thOpts });

        // Row 6 — Community Tax sub-headers
        sc("K6", "Number", { ...thOpts });
        sc("L6", "Date", { ...thOpts });
        sc("M6", "Place of\nIssue", { ...thOpts });

        ws.getRow(5).height = 22;
        ws.getRow(6).height = 18;

        // ---- Body rows ----
        let totalPax = 0;
        let totalGross = 0;
        let totalNet = 0;
        let rowIdx = 7;

        const tbody = table.querySelector("tbody");
        if (tbody) {
            Array.from(tbody.querySelectorAll("tr")).forEach(tr => {
                const tds = Array.from(tr.querySelectorAll("td")).map(td => td.innerText.trim());
                if (tds.length < 12 || tds[1].includes("No DTR")) return;

                const pax = parseInt(tds[4]) || 0;
                const rate = parseInt(tds[5]) || RATE;
                const gross = parseFloat(tds[6].replace(/,/g, "")) || (pax * rate);
                const net = parseFloat(tds[7].replace(/,/g, "")) || gross;

                totalPax += pax;
                totalGross += gross;
                totalNet += net;

                const row = ws.getRow(rowIdx);
                row.height = 16;

                const vals = [
                    tds[0],         // Seq
                    tds[1],         // Name
                    tds[2],         // Address
                    tds[3],         // Designation
                    pax,            // Num Passenger
                    rate,           // Rate
                    gross,          // Gross
                    net,            // Net
                    tds[8],         // Seq2
                    "",             // Signature
                    "",             // CTax Num
                    "",             // CTax Date
                    tds[12] || "Alegria, Cebu"  // Place
                ];

                vals.forEach((v, i) => {
                    const cell = row.getCell(i + 1);
                    cell.value = v;
                    border(cell);
                });

                // Alignment
                row.getCell(1).alignment = { horizontal: "center", vertical: "middle" };
                row.getCell(2).alignment = { horizontal: "left", vertical: "middle" };
                row.getCell(3).alignment = { horizontal: "left", vertical: "middle" };
                row.getCell(4).alignment = { horizontal: "center", vertical: "middle" };
                row.getCell(5).alignment = { horizontal: "center", vertical: "middle" };
                row.getCell(6).alignment = { horizontal: "center", vertical: "middle" };
                row.getCell(7).alignment = { horizontal: "right", vertical: "middle" };
                row.getCell(7).numFmt = "#,##0.00";
                row.getCell(8).alignment = { horizontal: "right", vertical: "middle" };
                row.getCell(8).numFmt = "#,##0.00";
                row.getCell(9).alignment = { horizontal: "center", vertical: "middle" };
                row.getCell(13).alignment = { horizontal: "center", vertical: "middle" };

                rowIdx++;
            });
        }

        // ---- Total Row ----
        const tRow = ws.getRow(rowIdx);
        tRow.height = 16;
        ws.mergeCells(`A${rowIdx}:D${rowIdx}`);
        tRow.getCell(1).value = "";
        [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13].forEach(c => {
            border(tRow.getCell(c));
            tRow.getCell(c).fill = { type: "pattern", pattern: "solid", fgColor: { argb: "FFF0F0F0" } };
            tRow.getCell(c).font = { bold: true, size: 10 };
        });
        tRow.getCell(5).value = totalPax;
        tRow.getCell(5).alignment = { horizontal: "center", vertical: "middle" };
        tRow.getCell(7).value = totalGross;
        tRow.getCell(7).numFmt = "#,##0.00";
        tRow.getCell(7).alignment = { horizontal: "right", vertical: "middle" };
        tRow.getCell(8).value = totalNet;
        tRow.getCell(8).numFmt = "#,##0.00";
        tRow.getCell(8).alignment = { horizontal: "right", vertical: "middle" };
        rowIdx++;

        // Spacer
        ws.getRow(rowIdx).height = 6;
        rowIdx++;

        // ---- Footer Certification ----
        const certStart = rowIdx;
        for (let r = certStart; r <= certStart + 4; r++) ws.getRow(r).height = 13;

        ws.mergeCells(`A${certStart}:D${certStart + 4}`);
        ws.getCell(`A${certStart}`).value = "CERTIFIED:\nEach person whose name appears on this roll had rendered services for the time stated.";
        ws.getCell(`A${certStart}`).font = { size: 9 };
        ws.getCell(`A${certStart}`).alignment = { horizontal: "left", vertical: "top", wrapText: true };

        ws.mergeCells(`E${certStart}:I${certStart + 4}`);
        ws.getCell(`E${certStart}`).value = "Approved for Payment:";
        ws.getCell(`E${certStart}`).font = { size: 10 };
        ws.getCell(`E${certStart}`).alignment = { horizontal: "center", vertical: "middle" };

        ws.mergeCells(`J${certStart}:M${certStart + 4}`);
        ws.getCell(`J${certStart}`).value = "CERTIFIED:\nEach person whose name appears on the above roll has been paid the amount stated opposite his name after identifying them.";
        ws.getCell(`J${certStart}`).font = { size: 9 };
        ws.getCell(`J${certStart}`).alignment = { horizontal: "left", vertical: "top", wrapText: true };

        rowIdx = certStart + 5;

        // Sig lines
        ws.mergeCells(`A${rowIdx}:D${rowIdx}`);
        sc(`A${rowIdx}`, "DESMOND KENNITH A. PLATERO", { bold: true, size: 10, align: "center" });
        ws.mergeCells(`E${rowIdx}:I${rowIdx}`);
        sc(`E${rowIdx}`, "GILBERTO F. MAGALLON, M.D.", { bold: true, size: 10, align: "center" });
        ws.mergeCells(`J${rowIdx}:M${rowIdx}`);
        sc(`J${rowIdx}`, "C/O REGISTER PAYROLL", { bold: true, size: 10, align: "center" });

        rowIdx++;
        ws.mergeCells(`A${rowIdx}:D${rowIdx}`);
        sc(`A${rowIdx}`, "Municipal Tourism Officer - Designate", { size: 9, align: "center", border: false });
        ws.mergeCells(`E${rowIdx}:I${rowIdx}`);
        sc(`E${rowIdx}`, "Municipal Mayor", { size: 9, align: "center", border: false });
        ws.mergeCells(`J${rowIdx}:M${rowIdx}`);
        sc(`J${rowIdx}`, "Disbursing officer", { size: 9, align: "center", border: false });

        ws.pageSetup.printArea = `A1:M${rowIdx}`;
        ws.pageSetup.printTitlesRow = "5:6";

        // ---- Export ----
        const buffer = await wb.xlsx.writeBuffer();
        const blob = new Blob([buffer], {
            type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        });
        const a = document.createElement("a");
        a.href = URL.createObjectURL(blob);
        a.download = `DriverDTR_${new Date().toISOString().slice(0, 10)}.xlsx`;
        a.click();
    });
});