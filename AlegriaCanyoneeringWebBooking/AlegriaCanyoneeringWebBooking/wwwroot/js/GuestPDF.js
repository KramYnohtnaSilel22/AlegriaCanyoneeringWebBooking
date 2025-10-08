
        async function downloadGuestDetailsPdf() {
                    const btn = document.querySelector('.btn-danger');
                btn.disabled = true;
                btn.innerHTML = `<span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>Generating...`;

                const {jsPDF} = window.jspdf;
                const doc = new jsPDF("p", "mm", "a4");

                const date = new Date().toLocaleDateString();
                doc.setFontSize(18);
                doc.setTextColor("#333");
                doc.text("Daily Guest Report", 14, 20);
                doc.setFontSize(11);
                doc.text(`Date: ${date}`, 14, 28);

                let currentY = 35;

                // Get all batch cards
                const batchCards = document.querySelectorAll("#guestDetailsContent .card");

                if (!batchCards.length) {
                    alert("No data available to export.");
                btn.disabled = false;
                btn.innerHTML = `<i class="fas fa-file-pdf"></i> Download PDF`;
                return;
                    }

                for (const card of batchCards) {
                        const header = card.querySelector(".card-header");
                const primaryTable = card.querySelector("table");

                // --- Add Batch Title ---
                const batchTitle = header?.innerText?.trim() || "Batch Info";
                doc.setFontSize(13);
                doc.setTextColor(0);
                doc.text(batchTitle, 14, currentY);
                currentY += 7;

                // --- Get Headers ---
                const headers = [];
                        primaryTable.querySelectorAll("thead tr th").forEach(th => {
                    headers.push(th.innerText.trim());
                        });

                // --- Get Rows ---
                const rows = [];
                        primaryTable.querySelectorAll("tbody tr").forEach(tr => {
                            const row = [];
                            tr.querySelectorAll("td").forEach(td => {
                    row.push(td.innerText.trim());
                            });
                rows.push(row);
                        });

                // --- Add Primary Guest Table ---
                doc.autoTable({
                    startY: currentY,
                head: [headers],
                body: rows,
                theme: 'grid',
                styles: {fontSize: 10, cellPadding: 3 },
                headStyles: {
                    fillColor: [41, 128, 185],
                textColor: 255,
                halign: 'center',
                            },
                alternateRowStyles: {fillColor: [245, 245, 245] },
                didDrawPage: function (data) {
                                const pageCount = doc.internal.getNumberOfPages();
                doc.setFontSize(9);
                doc.text(`Page ${data.pageNumber} of ${pageCount}`, doc.internal.pageSize.getWidth() - 30, doc.internal.pageSize.getHeight() - 10);
                            },
                        });

                currentY = doc.lastAutoTable.finalY + 10;

                // --- Check for Companion Table ---
                const companionTable = card.querySelectorAll("table")[1];
                if (companionTable) {
                            const companionHeaders = [];
                const companionBody = [];

                            companionTable.querySelectorAll("thead tr th").forEach(th => {
                    companionHeaders.push(th.innerText.trim());
                            });

                            companionTable.querySelectorAll("tbody tr").forEach(tr => {
                                const row = [];
                                tr.querySelectorAll("td").forEach(td => {
                    row.push(td.innerText.trim());
                                });
                companionBody.push(row);
                            });

                doc.setFontSize(12);
                doc.setTextColor(80);
                doc.text("Companion Guests", 14, currentY);
                currentY += 5;

                doc.autoTable({
                    startY: currentY,
                head: [companionHeaders],
                body: companionBody,
                theme: 'grid',
                styles: {fontSize: 10, cellPadding: 3 },
                headStyles: {
                    fillColor: [108, 117, 125],
                textColor: 255,
                halign: 'center',
                                },
                alternateRowStyles: {fillColor: [248, 248, 248] },
                didDrawPage: function (data) {
                                    const pageCount = doc.internal.getNumberOfPages();
                doc.setFontSize(9);
                doc.text(`Page ${data.pageNumber} of ${pageCount}`, doc.internal.pageSize.getWidth() - 30, doc.internal.pageSize.getHeight() - 10);
                                },
                            });

                currentY = doc.lastAutoTable.finalY + 10;
                        }

                        // Add space before next batch
                        if (currentY > 250) {
                    doc.addPage();
                currentY = 20;
                        }
                    }

                doc.save("DailyGuestReport.pdf");
                btn.disabled = false;
                btn.innerHTML = `<i class="fas fa-file-pdf"></i> Download PDF`;
                }


