async function downloadGuestDetailsPdf() {
    const btn = document.querySelector('.btn-danger');
    btn.disabled = true;
    btn.innerHTML = `<span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>Generating...`;

    const { jsPDF } = window.jspdf;
    const doc = new jsPDF("p", "mm", "a4");

    const date = new Date().toLocaleDateString();
    doc.setFontSize(18);
    doc.setTextColor("#333");
    doc.text("Daily Guest Report", 14, 20);
    doc.setFontSize(11);
    doc.text(`Date: ${date}`, 14, 28);

    let currentY = 35;

    const container = document.getElementById("guestDetailsContent");
    if (!container) {
        alert("No guest details found to export.");
        btn.disabled = false;
        btn.innerHTML = `<i class="fas fa-file-pdf"></i> Download PDF`;
        return;
    }

    // Find the primary guest table (the first table)
    const primaryTable = container.querySelector("table");
    if (!primaryTable) {
        alert("No primary guest data found to export.");
        btn.disabled = false;
        btn.innerHTML = `<i class="fas fa-file-pdf"></i> Download PDF`;
        return;
    }

    // Add title for primary guest
    doc.setFontSize(13);
    doc.setTextColor(0);
    doc.text("Primary Guest", 14, currentY);
    currentY += 7;

    // Extract headers and rows for primary table
    const headers = Array.from(primaryTable.querySelectorAll("thead tr th")).map(th => th.innerText.trim());
    const rows = Array.from(primaryTable.querySelectorAll("tbody tr")).map(tr =>
        Array.from(tr.querySelectorAll("td")).map(td => td.innerText.trim())
    );

    doc.autoTable({
        startY: currentY,
        head: [headers],
        body: rows,
        theme: 'grid',
        styles: { fontSize: 10, cellPadding: 3 },
        headStyles: {
            fillColor: [41, 128, 185],
            textColor: 255,
            halign: 'center',
        },
        alternateRowStyles: { fillColor: [245, 245, 245] },
        didDrawPage: function (data) {
            const pageCount = doc.internal.getNumberOfPages();
            doc.setFontSize(9);
            doc.text(`Page ${data.pageNumber} of ${pageCount}`, doc.internal.pageSize.getWidth() - 30, doc.internal.pageSize.getHeight() - 10);
        },
    });

    currentY = doc.lastAutoTable.finalY + 10;

    // Check for companion guest table (second table inside container)
    const companionTable = container.querySelectorAll("table")[1];
    if (companionTable) {
        doc.setFontSize(12);
        doc.setTextColor(80);
        doc.text("Companion Guests", 14, currentY);
        currentY += 5;

        const companionHeaders = Array.from(companionTable.querySelectorAll("thead tr th")).map(th => th.innerText.trim());
        const companionRows = Array.from(companionTable.querySelectorAll("tbody tr")).map(tr =>
            Array.from(tr.querySelectorAll("td")).map(td => td.innerText.trim())
        );

        doc.autoTable({
            startY: currentY,
            head: [companionHeaders],
            body: companionRows,
            theme: 'grid',
            styles: { fontSize: 10, cellPadding: 3 },
            headStyles: {
                fillColor: [108, 117, 125],
                textColor: 255,
                halign: 'center',
            },
            alternateRowStyles: { fillColor: [248, 248, 248] },
            didDrawPage: function (data) {
                const pageCount = doc.internal.getNumberOfPages();
                doc.setFontSize(9);
                doc.text(`Page ${data.pageNumber} of ${pageCount}`, doc.internal.pageSize.getWidth() - 30, doc.internal.pageSize.getHeight() - 10);
            },
        });

        currentY = doc.lastAutoTable.finalY + 10;
    }

    doc.save("DailyGuestReport.pdf");

    btn.disabled = false;
    btn.innerHTML = `<i class="fas fa-file-pdf"></i> Download PDF`;
}


function downloadGuestDetailsExcel() {
    const wb = XLSX.utils.book_new();

    const date = new Date().toLocaleDateString();
    const title = [`Guests of the Day - ${date}`];

    // Headers for combined table
    const headers = [
        "Batch", "Operator", "Full Name", "Age", "Contact", "Gender", "Nationality", "Area", "Arrival Date"
    ];

    // Initialize rows with title and headers
    const rows = [
        title,              // Title row
        [],                 // Spacer row
        headers             // Header row
    ];

    const container = document.getElementById("guestDetailsContent");
    if (!container) {
        alert("No guest details found to export.");
        return;
    }

    // Extract batch info from modal title
    const modalTitle = document.getElementById("guestDetailsModalLabel")?.innerText || "";
    // Example title: "Booking Details - Batch: 12345"
    const batchMatch = modalTitle.match(/Batch:\s*(\S+)/i);
    const batch = batchMatch ? batchMatch[1] : "N/A";

    // Extract Operator and Primary guest info from primary table (first table inside container)
    const tables = container.querySelectorAll("table");
    if (!tables.length) {
        alert("No guest tables found to export.");
        return;
    }

    const primaryTable = tables[0];
    // Operator is in your model row, grab from first td in the first row of primary table? No, your first column is Operator for primary guest
    // But your primary table has columns: Operator, Full Name, Age, Contact, Gender, Nationality, Area, Arrival Date
    // So Operator is col 0 in primary guest table tbody row
    primaryTable.querySelectorAll("tbody tr").forEach(tr => {
        const cols = tr.querySelectorAll("td");
        if (cols.length < 8) return; // sanity check
        const row = [
            batch,
            cols[0]?.innerText.trim() || "",  // Operator
            cols[1]?.innerText.trim() || "",  // Full Name
            cols[2]?.innerText.trim() || "",  // Age
            cols[3]?.innerText.trim() || "",  // Contact
            cols[4]?.innerText.trim() || "",  // Gender
            cols[5]?.innerText.trim() || "",  // Nationality
            cols[6]?.innerText.trim() || "",  // Area
            cols[7]?.innerText.trim() || ""   // Arrival Date
        ];
        rows.push(row);
    });

    // Companion guests table (if any)
    if (tables.length > 1) {
        const companionTable = tables[1];
        companionTable.querySelectorAll("tbody tr").forEach(tr => {
            const cols = tr.querySelectorAll("td");
            if (cols.length < 5) return; // sanity check
            const row = [
                "",
                "", // Operator blank for companions
                cols[0]?.innerText.trim() || "",  // Full Name
                cols[1]?.innerText.trim() || "",  // Age
                cols[2]?.innerText.trim() || "",  // Contact
                cols[3]?.innerText.trim() || "",  // Gender
                cols[4]?.innerText.trim() || "",  // Nationality
                "", // Area blank (no column for companions)
                ""  // Arrival Date blank
            ];
            rows.push(row);
        });
    }

    // Create worksheet from combined rows
    const ws = XLSX.utils.aoa_to_sheet(rows);

    // Set column widths
    ws['!cols'] = [
        { wch: 10 }, // Batch
        { wch: 20 }, // Operator
        { wch: 25 }, // Full Name
        { wch: 5 },  // Age
        { wch: 15 }, // Contact
        { wch: 8 },  // Gender
        { wch: 15 }, // Nationality
        { wch: 15 }, // Area
        { wch: 20 }  // Arrival Date
    ];

    XLSX.utils.book_append_sheet(wb, ws, "Guests of the Day");

    XLSX.writeFile(wb, `DailyGuestReport_${new Date().toISOString().slice(0, 10)}.xlsx`);
}
