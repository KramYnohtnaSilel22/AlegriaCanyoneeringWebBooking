
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
        title,              // <- First row: Title
        [],                 // <- Spacer row
        headers             // <- Header row for the table
    ];

            // Select all batch cards
            const batchCards = document.querySelectorAll("#guestDetailsContent .card");

            if (!batchCards.length) {
                alert("No data available to export.");
                return;
            }

            batchCards.forEach(card => {
                // Extract batch name and operator from card header
                const cardHeader = card.querySelector(".card-header");
                const batchMatch = cardHeader?.innerText.match(/Batch:\s*(\S+)/i);
                const batch = batchMatch ? batchMatch[1] : "N/A";

                const operatorMatch = cardHeader?.innerText.match(/\(Operator:\s*(.+?)\)/i);
                const operator = operatorMatch ? operatorMatch[1] : "N/A";

                // Extract primary guest row
                const primaryTable = card.querySelector("table");
                if (primaryTable) {
                    primaryTable.querySelectorAll("tbody tr").forEach(tr => {
                        const cols = tr.querySelectorAll("td");
                        const row = [
                            batch,
                            operator,
                            cols[0]?.innerText.trim() || "",
                            cols[1]?.innerText.trim() || "",
                            cols[2]?.innerText.trim() || "",
                            cols[3]?.innerText.trim() || "",
                            cols[4]?.innerText.trim() || "",
                            cols[5]?.innerText.trim() || "",
                            cols[6]?.innerText.trim() || "",
                        ];
                        rows.push(row);
                    });
                }

                // Extract companion guests if any (second table)
                const companionsTable = card.querySelectorAll("table")[1];
                if (companionsTable) {
                    companionsTable.querySelectorAll("tbody tr").forEach(tr => {
                        const cols = tr.querySelectorAll("td");
                        // Companions don't have Area and ArrivalDate columns, so keep blank
                        const row = [
                            batch,
                            operator,
                            cols[0]?.innerText.trim() || "",
                            cols[1]?.innerText.trim() || "",
                            cols[2]?.innerText.trim() || "",
                            cols[3]?.innerText.trim() || "",
                            cols[4]?.innerText.trim() || "",
                            "", // Area blank
                            "", // ArrivalDate blank
                        ];
                        rows.push(row);
                    });
                }
            });

            // Create worksheet from combined rows
            const ws = XLSX.utils.aoa_to_sheet(rows);

            // Optional: set column widths for better readability
            ws['!cols'] = [
                { wch: 10 }, // Batch
                { wch: 20 }, // Operator
                { wch: 25 }, // Full Name
                { wch: 5 },  // Age
                { wch: 15 }, // Contact
                { wch: 8 },  // Gender
                { wch: 15 }, // Nationality
                { wch: 15 }, // Area
                { wch: 15 }  // Arrival Date
            ];

            // Add worksheet to workbook
            XLSX.utils.book_append_sheet(wb, ws, "Guests of the Day");

            // Save the Excel file
            XLSX.writeFile(wb, "DailyGuestReport.xlsx");
        }


        // Batch Search Filter
        document.getElementById('batchSearchInput')?.addEventListener('input', function (e) {
            const query = e.target.value.trim().toLowerCase();
            const cards = document.querySelectorAll('#guestDetailsContent .card');
            cards.forEach(card => {
                const headerText = card.querySelector('.card-header')?.textContent.toLowerCase() || '';
                if (headerText.includes(query)) {
                    card.style.display = '';
                } else {
                    card.style.display = 'none';
                }
            });
        });
