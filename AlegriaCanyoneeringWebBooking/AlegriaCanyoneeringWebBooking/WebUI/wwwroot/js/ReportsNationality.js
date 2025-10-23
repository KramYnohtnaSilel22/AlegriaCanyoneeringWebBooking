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
        pdf.save(`NationalityReport_${new Date().toISOString().slice(0, 10)}.pdf`);
    });

    // ---- EXCEL EXPORT ----
    $("#btnDownloadExcel").on("click", async function () {
        if (typeof ExcelJS === "undefined") {
            alert("ExcelJS failed to load.");
            return;
        }

        const table = document.getElementById("nationalityReportTable");
        if (!table) {
            alert("Table not found.");
            return;
        }

        const dateFrom = $("#dateFrom").val() || "";
        const dateTo = $("#dateTo").val() || "";
        console.log("Date From:", dateFrom, "Date To:", dateTo);

        // ---- Format individual dates ----
        function formatDate(dateStr) {
            const months = [
                "January", "February", "March", "April", "May", "June",
                "July", "August", "September", "October", "November", "December"
            ];

            if (!dateStr) return "N/A";

            const date = new Date(dateStr);
            if (isNaN(date.getTime())) return "N/A";

            const dayOfWeek = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"][date.getDay()];
            const month = months[date.getMonth()];
            const day = date.getDate();
            const year = date.getFullYear();

            return `${dayOfWeek}, ${month} ${day}, ${year}`;
        }

        const dateFromFormatted = formatDate(dateFrom);
        const dateToFormatted = formatDate(dateTo);

        const wb = new ExcelJS.Workbook();
        const ws = wb.addWorksheet("Nationality Report", {
            pageSetup: {
                orientation: "portrait",
                fitToPage: true,
                fitToWidth: 1,
                fitToHeight: 0,
                paperSize: 9,
                margins: { left: 0.25, right: 0.25, top: 0.25, bottom: 0.25, header: 0, footer: 0 },
                horizontalCentered: true,
                verticalCentered: false
            }
        });

        // ---- Columns ----
        ws.columns = [
            { width: 15 },  // Date From Label
            { width: 30 },  // Date From Value
            { width: 15 },  // Date To Label
            { width: 30 }   // Date To Value
        ];

        // ---- Header ----
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

        ws.getRow(4).height = 5;

        // ---- Date Row (Split into 4 columns) ----
        // ---- Date Row (Split into 4 columns) ----
        ws.getCell("A5").value = "Date:";
        ws.getCell("A5").font = { bold: true, size: 10 };
        ws.getCell("A5").alignment = { horizontal: "left", vertical: "middle" };

        ws.getCell("B5").value = dateFromFormatted;
        ws.getCell("B5").font = { size: 10 };
        ws.getCell("B5").alignment = { horizontal: "left", vertical: "middle" };

        ws.getCell("C5").value = "Date To:";
        ws.getCell("C5").font = { bold: true, size: 10 };
        ws.getCell("C5").alignment = { horizontal: "left", vertical: "middle" };

        ws.getCell("D5").value = dateToFormatted;
        ws.getCell("D5").font = { size: 10 };
        ws.getCell("D5").alignment = { horizontal: "left", vertical: "middle" };

        ws.getRow(6).height = 5;

        ws.mergeCells("A7:D7");
        ws.getCell("A7").value = "CANYONEERING - NUMBER OF GUESTS";
        ws.getCell("A7").font = { bold: true, size: 11 };
        ws.getCell("A7").alignment = { horizontal: "center", vertical: "middle" };

        ws.getRow(8).height = 5;

        // ---- Adjust columns for table (5 columns now) ----
        ws.getColumn(1).width = 6;   // Seq
        ws.getColumn(2).width = 30;  // Nationality
        ws.getColumn(3).width = 12;  // Male
        ws.getColumn(4).width = 12;  // Female
        ws.getColumn(5).width = 15;  // Ending Total

        // ---- Table Header ----
        ws.mergeCells("A9:A10");
        ws.getCell("A9").value = "SEQ.";
        ws.getCell("A9").font = { bold: true };
        ws.getCell("A9").alignment = { horizontal: "center", vertical: "middle" };

        ws.mergeCells("B9:B10");
        ws.getCell("B9").value = "NATIONALITY";
        ws.getCell("B9").font = { bold: true };
        ws.getCell("B9").alignment = { horizontal: "center", vertical: "middle" };

        ws.mergeCells("C9:D9");
        ws.getCell("C9").value = "NUMBER OF GUESTS";
        ws.getCell("C9").font = { bold: true };
        ws.getCell("C9").alignment = { horizontal: "center", vertical: "middle" };

        ws.getCell("C10").value = "MALE";
        ws.getCell("D10").value = "FEMALE";
        ws.getCell("C10").alignment = { horizontal: "center", vertical: "middle" };
        ws.getCell("D10").alignment = { horizontal: "center", vertical: "middle" };

        ws.mergeCells("E9:E10");
        ws.getCell("E9").value = "ENDING TOTAL";
        ws.getCell("E9").alignment = { horizontal: "center", vertical: "middle" };
        ws.getCell("E9").font = { bold: true };

        // ---- Body Rows ----
        let currentRow = 11;
        const tbody = table.querySelector("tbody");
        if (tbody) {
            Array.from(tbody.querySelectorAll("tr")).forEach(tr => {
                const cells = Array.from(tr.querySelectorAll("td")).map(td => td.innerText.trim());
                if (cells.length >= 5 && !cells[1].includes("No data")) {
                    const row = ws.addRow([
                        Number(cells[0]) || 0,  // Seq
                        cells[1],               // Nationality
                        Number(cells[2]) || 0,  // Male
                        Number(cells[3]) || 0,  // Female
                        Number(cells[4]) || 0   // Ending Total
                    ]);

                    row.eachCell((cell, colNumber) => {
                        cell.alignment = { horizontal: colNumber === 2 ? "left" : "center", vertical: "middle" };
                        cell.numFmt = (colNumber >= 3) ? "#,##0" : undefined;
                        if (colNumber === 5) cell.font = { bold: true };
                        cell.border = {
                            top: { style: "thin" },
                            left: { style: "thin" },
                            bottom: { style: "thin" },
                            right: { style: "thin" }
                        };
                    });
                    currentRow++;
                }
            });
        }

        // ---- Totals Row ----
        const tfoot = table.querySelector("tfoot tr");
        if (tfoot) {
            const tfootCells = Array.from(tfoot.querySelectorAll("td")).map(td => td.innerText.trim());
            const totalRow = ws.addRow([
                "",                    // Seq
                tfootCells[0] || "TOTAL:",
                Number(tfootCells[1].replace(/[^0-9]/g, "")) || 0,
                Number(tfootCells[2].replace(/[^0-9]/g, "")) || 0,
                Number(tfootCells[3].replace(/[^0-9]/g, "")) || 0
            ]);

            totalRow.font = { bold: true };
            totalRow.eachCell((cell, colNumber) => {
                cell.alignment = { horizontal: colNumber === 2 ? "left" : "center", vertical: "middle" };
                cell.numFmt = (colNumber >= 3) ? "#,##0" : undefined;
                cell.fill = {
                    type: "pattern",
                    pattern: "solid",
                    fgColor: { argb: "FFE8E8E8" }
                };
                cell.border = {
                    top: { style: "thin" },
                    left: { style: "thin" },
                    bottom: { style: "thin" },
                    right: { style: "thin" }
                };
            });
            currentRow++;
        }

        // ---- Footer Note ----
        const noteRow = ws.addRow([]);
        ws.mergeCells(`A${noteRow.number}:E${noteRow.number}`);
        ws.getCell(`A${noteRow.number}`).value = "System Generated Report";
        ws.getCell(`A${noteRow.number}`).font = { italic: true, size: 9 };
        ws.getCell(`A${noteRow.number}`).alignment = { horizontal: "left", vertical: "middle" };

        // ---- Print settings ----
        ws.pageSetup.printArea = `A1:E${noteRow.number}`;
        ws.pageSetup.printTitlesRow = "9:10";

        // ---- Export ----
        const buffer = await wb.xlsx.writeBuffer();
        const blob = new Blob([buffer], { type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" });
        const a = document.createElement("a");
        a.href = URL.createObjectURL(blob);
        a.download = `NationalityReport_${new Date().toISOString().slice(0, 10)}.xlsx`;
        a.click();
    }); 
});
