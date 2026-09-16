$(document).ready(function () {

    // ---- AUTO COMPUTE TOTALS ------------------------------------------------
    function recalcTotals() {
        let totalJumps = 0, totalGross = 0, totalNet = 0;

        $("#outsideGuideDtrTable tbody tr").each(function () {
            const jumps = parseFloat($(this).find("td.col-pax").data("pax")) || 0;
            const gross = parseFloat($(this).find("td.col-gross").data("gross")) || 0;
            const net = parseFloat($(this).find("td.col-net").data("net")) || 0;
            totalJumps += jumps;
            totalGross += gross;
            totalNet += net;
        });

        $("#grandTotalJumps").text(totalJumps);
        $("#grandTotalGross").text(totalGross > 0
            ? totalGross.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })
            : "-");
        $("#grandTotalNet").text(totalNet > 0
            ? totalNet.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })
            : "-");
    }
    recalcTotals();

    // ---- PDF EXPORT ---------------------------------------------------------
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
        const scale = Math.min((pageW - margin * 2) / imgProps.width, (pageH - margin * 2) / imgProps.height);
        const w = imgProps.width * scale, h = imgProps.height * scale;
        pdf.addImage(imgData, "JPEG", (pageW - w) / 2, margin, w, h);
        pdf.save(`OutsideGuideDTR_${new Date().toISOString().slice(0, 10)}.pdf`);
    });

    // ---- EXCEL EXPORT -------------------------------------------------------
    // 14 Column Layout (A–N) with Area column:
    //  A=#   B=Name   C=Address   D=Designation   E=Area
    //  F=Jumps   G=Rate   H=Gross   I=Net
    //  J=#2   K=Signature   L=CTC No.   M=CTC Date   N=Place
    //
    // HTML Table Row TD Indices (14 cells):
    //  [0]=#  [1]=Name  [2]=Address  [3]=Desig  [4]=Area
    //  [5]=Jumps  [6]=Rate  [7]=Gross  [8]=Net
    //  [9]=#2  [10]=Sig  [11]=CTC#  [12]=CTCDate  [13]=Place

    $("#btnDownloadExcel").on("click", async function () {
        if (typeof ExcelJS === "undefined") { alert("ExcelJS failed to load."); return; }

        const table = document.getElementById("outsideGuideDtrTable");
        if (!table) { alert("Table not found."); return; }

        const period = document.getElementById("displayPeriod")?.innerText?.trim() || "N/A";
        const area = document.getElementById("displayArea")?.innerText?.trim() || "ALL AREAS";

        const wb = new ExcelJS.Workbook();
        const ws = wb.addWorksheet("Outside Guide DTR", {
            pageSetup: {
                orientation: "landscape",
                fitToPage: true, fitToWidth: 1, fitToHeight: 1,
                paperSize: 9,
                margins: { left: 0.2, right: 0.2, top: 0.2, bottom: 0.2, header: 0, footer: 0 },
                horizontalCentered: true
            }
        });

        ws.columns = [
            { width: 5 },    // A  #
            { width: 24 },   // B  Name
            { width: 14 },   // C  Address
            { width: 12 },   // D  Designation
            { width: 14 },   // E  Area ← new column
            { width: 10 },   // F  No. of Jumps
            { width: 10 },   // G  Rate/Jump
            { width: 12 },   // H  Gross Salary
            { width: 12 },   // I  Net Pay
            { width: 5 },    // J  #2
            { width: 14 },   // K  Signature/Thumbmark
            { width: 10 },   // L  CTC Number
            { width: 10 },   // M  CTC Date
            { width: 14 }    // N  Place of Issue
        ];

        const sc = (addr, value, opts = {}) => {
            const cell = ws.getCell(addr);
            cell.value = value;
            cell.font = { bold: opts.bold ?? false, size: opts.size ?? 10, ...(opts.font || {}) };
            cell.alignment = {
                horizontal: opts.align ?? "center",
                vertical: "middle",
                wrapText: opts.wrap ?? false
            };
            if (opts.fill) cell.fill = { type: "pattern", pattern: "solid", fgColor: { argb: opts.fill } };
            if (opts.border !== false) cell.border = {
                top: { style: "thin" }, left: { style: "thin" },
                bottom: { style: "thin" }, right: { style: "thin" }
            };
        };

        const border = (cell) => {
            cell.border = {
                top: { style: "thin" }, left: { style: "thin" },
                bottom: { style: "thin" }, right: { style: "thin" }
            };
        };

        // ── Government header (rows 1–3) ──────────────────────────────────
        ws.mergeCells("A1:N1");
        sc("A1", "Project", { bold: false, size: 11, border: false });

        ws.mergeCells("A2:G2");
        sc("A2", "LGU :  MUNICIPALITY OF ALEGRIA", { bold: true, size: 11, align: "left", border: false });

        ws.mergeCells("H2:N2");
        sc("H2", `Period: ${period}`, { bold: true, size: 10, align: "right", border: false });

        ws.mergeCells("A3:G3");
        sc("A3", area, { bold: true, size: 12, align: "left", border: false });

        ws.mergeCells("H3:N3");
        sc("H3", "Rate: Wonder Falls=₱500/jump | Kawasan Exit=₱600/jump | Kanlaob=₱500/jump",
            { bold: false, size: 9, align: "left", border: false });

        ws.getRow(4).height = 5; // spacer

        // ── Table header (rows 5–6) ───────────────────────────────────────
        const thFill = "FFD1E7DD";
        const thOpts = { bold: true, size: 9, fill: thFill, wrap: true };

        ws.mergeCells("A5:A6"); sc("A5", "", { ...thOpts });
        ws.mergeCells("B5:B6"); sc("B5", "Name", { ...thOpts });
        ws.mergeCells("C5:C6"); sc("C5", "Address", { ...thOpts });
        ws.mergeCells("D5:D6"); sc("D5", "Designation", { ...thOpts });
        ws.mergeCells("E5:E6"); sc("E5", "Area", { ...thOpts }); // ✅ NEW
        ws.mergeCells("F5:F6"); sc("F5", "Number of\nJumps", { ...thOpts });
        ws.mergeCells("G5:G6"); sc("G5", "Rate per\nJump", { ...thOpts });
        ws.mergeCells("H5:H6"); sc("H5", "Gross\nSalary", { ...thOpts });
        ws.mergeCells("I5:I6"); sc("I5", "NET PAY", { ...thOpts });
        ws.mergeCells("J5:J6"); sc("J5", "", { ...thOpts });
        ws.mergeCells("K5:K6"); sc("K5", "Signature or\nThumbmark", { ...thOpts });
        ws.mergeCells("L5:N5"); sc("L5", "COMMUNITY TAX", { ...thOpts });
        sc("L6", "Number", { ...thOpts });
        sc("M6", "Date", { ...thOpts });
        sc("N6", "Place of\nIssue", { ...thOpts });

        ws.getRow(5).height = 22;
        ws.getRow(6).height = 18;

        // ── Data rows ────────────────────────────────────────────────────
        let totalJumps = 0, totalGross = 0, totalNet = 0;
        let rowIdx = 7;

        const tbody = table.querySelector("tbody");
        if (tbody) {
            Array.from(tbody.querySelectorAll("tr")).forEach(tr => {
                const tds = Array.from(tr.querySelectorAll("td")).map(td => td.innerText.trim());
                if (tds.length < 14 || tds[1].includes("No")) return;

                const jumps = parseInt(tds[5]) || 0;           // ✅ [5] = col-pax
                const rate = parseFloat(tds[6].replace(/[^0-9.]/g, "")) || 0;  // ✅ [6] = col-rate
                const gross = parseFloat(tds[7].replace(/,/g, "")) || 0;        // ✅ [7] = col-gross
                const net = parseFloat(tds[8].replace(/,/g, "")) || 0;          // ✅ [8] = col-net

                totalJumps += jumps;
                totalGross += gross;
                totalNet += net;

                const row = ws.getRow(rowIdx);
                row.height = 16;

                // ✅ Map HTML td indices → Excel columns (A=1...N=14)
                const vals = [
                    tds[0],                      // A  #
                    tds[1],                      // B  Name
                    tds[2],                      // C  Address
                    tds[3],                      // D  Designation
                    tds[4],                      // E  Area ✅
                    jumps,                       // F  Jumps
                    rate || "",                  // G  Rate
                    gross || "",                 // H  Gross
                    net || "",                   // I  Net
                    tds[9],                      // J  #2
                    "",                          // K  Signature
                    tds[11],                     // L  CTC No
                    tds[12],                     // M  CTC Date
                    tds[13] || "Alegria, Cebu"   // N  Place
                ];

                vals.forEach((v, i) => {
                    row.getCell(i + 1).value = v;
                    border(row.getCell(i + 1));
                });

                // Set alignment
                row.getCell(1).alignment = { horizontal: "center", vertical: "middle" };
                row.getCell(2).alignment = { horizontal: "left", vertical: "middle" };
                row.getCell(3).alignment = { horizontal: "left", vertical: "middle" };
                row.getCell(4).alignment = { horizontal: "center", vertical: "middle" };
                row.getCell(5).alignment = { horizontal: "center", vertical: "middle" };
                row.getCell(6).alignment = { horizontal: "center", vertical: "middle" };
                row.getCell(7).alignment = { horizontal: "center", vertical: "middle" };
                row.getCell(8).alignment = { horizontal: "right", vertical: "middle" };
                if (gross) row.getCell(8).numFmt = "#,##0.00";
                row.getCell(9).alignment = { horizontal: "right", vertical: "middle" };
                if (net) row.getCell(9).numFmt = "#,##0.00";
                row.getCell(10).alignment = { horizontal: "center", vertical: "middle" };
                row.getCell(14).alignment = { horizontal: "center", vertical: "middle" };

                rowIdx++;
            });
        }

        // ── Totals row ────────────────────────────────────────────────────
        const tRow = ws.getRow(rowIdx);
        tRow.height = 16;
        ws.mergeCells(`A${rowIdx}:E${rowIdx}`); // ✅ A-E (5 cols)

        [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14].forEach(c => {
            border(tRow.getCell(c));
            tRow.getCell(c).fill = { type: "pattern", pattern: "solid", fgColor: { argb: "FFF0F0F0" } };
            tRow.getCell(c).font = { bold: true, size: 10 };
        });

        tRow.getCell(6).value = totalJumps;
        tRow.getCell(6).alignment = { horizontal: "center", vertical: "middle" };

        if (totalGross > 0) {
            tRow.getCell(8).value = totalGross;
            tRow.getCell(8).numFmt = "#,##0.00";
            tRow.getCell(8).alignment = { horizontal: "right", vertical: "middle" };
            tRow.getCell(9).value = totalNet;
            tRow.getCell(9).numFmt = "#,##0.00";
            tRow.getCell(9).alignment = { horizontal: "right", vertical: "middle" };
        }
        rowIdx++;

        ws.getRow(rowIdx).height = 6; rowIdx++; // spacer

        // ── Footer certification ──────────────────────────────────────────
        const certStart = rowIdx;
        for (let r = certStart; r <= certStart + 4; r++) ws.getRow(r).height = 13;

        ws.mergeCells(`A${certStart}:E${certStart + 4}`);
        ws.getCell(`A${certStart}`).value =
            "CERTIFIED:\nEach person whose name appears on this roll had rendered services for the time stated.";
        ws.getCell(`A${certStart}`).font = { size: 9 };
        ws.getCell(`A${certStart}`).alignment = { horizontal: "left", vertical: "top", wrapText: true };

        ws.mergeCells(`F${certStart}:J${certStart + 4}`);
        ws.getCell(`F${certStart}`).value = "Approved for Payment:";
        ws.getCell(`F${certStart}`).font = { size: 10 };
        ws.getCell(`F${certStart}`).alignment = { horizontal: "center", vertical: "middle" };

        ws.mergeCells(`K${certStart}:N${certStart + 4}`);
        ws.getCell(`K${certStart}`).value =
            "CERTIFIED:\nEach person whose name appears on the above roll has been paid the amount stated opposite his name after identifying them.";
        ws.getCell(`K${certStart}`).font = { size: 9 };
        ws.getCell(`K${certStart}`).alignment = { horizontal: "left", vertical: "top", wrapText: true };

        rowIdx = certStart + 5;

        ws.mergeCells(`A${rowIdx}:E${rowIdx}`);
        sc(`A${rowIdx}`, "DESMOND KENNITH A. PLATERO", { bold: true, size: 10 });
        ws.mergeCells(`F${rowIdx}:J${rowIdx}`);
        sc(`F${rowIdx}`, "GILBERTO F. MAGALLON, M.D.", { bold: true, size: 10 });
        ws.mergeCells(`K${rowIdx}:N${rowIdx}`);
        sc(`K${rowIdx}`, "C/O REGISTER PAYROLL", { bold: true, size: 10 });

        rowIdx++;
        ws.mergeCells(`A${rowIdx}:E${rowIdx}`);
        sc(`A${rowIdx}`, "Municipal Tourism Officer - Designate", { size: 9, border: false });
        ws.mergeCells(`F${rowIdx}:J${rowIdx}`);
        sc(`F${rowIdx}`, "Municipal Mayor", { size: 9, border: false });
        ws.mergeCells(`K${rowIdx}:N${rowIdx}`);
        sc(`K${rowIdx}`, "Disbursing officer", { size: 9, border: false });

        rowIdx += 2;
        ws.mergeCells(`A${rowIdx}:N${rowIdx}`);
        ws.getCell(`A${rowIdx}`).value = "System Generated Report — Outside Guide DTR Summary";
        ws.getCell(`A${rowIdx}`).font = { italic: true, size: 9, color: { argb: "FF888888" } };
        ws.getCell(`A${rowIdx}`).alignment = { horizontal: "right", vertical: "middle" };

        ws.pageSetup.printArea = `A1:N${rowIdx}`;
        ws.pageSetup.printTitlesRow = "5:6";

        const buffer = await wb.xlsx.writeBuffer();
        const blob = new Blob([buffer], {
            type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        });
        const a = document.createElement("a");
        a.href = URL.createObjectURL(blob);
        a.download = `OutsideGuideDTR_${new Date().toISOString().slice(0, 10)}.xlsx`;
        a.click();
    });
});