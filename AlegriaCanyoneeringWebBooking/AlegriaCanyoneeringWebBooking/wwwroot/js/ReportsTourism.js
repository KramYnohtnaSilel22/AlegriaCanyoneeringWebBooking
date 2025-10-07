
        function setDateRange(filter) {
            const today = new Date();
            let start, end;

            switch (filter) {
                case "daily":
                    start = end = today;
                    break;
                case "weekly":
                    const day = today.getDay();
                    start = new Date(today);
                    start.setDate(today.getDate() - day);
                    end = new Date(start);
                    end.setDate(start.getDate() + 6);
                    break;
                case "monthly":
                    start = new Date(today.getFullYear(), today.getMonth(), 1);
                    end = new Date(today.getFullYear(), today.getMonth() + 1, 0);
                    break;
                case "quarterly":
                    const quarter = Math.floor(today.getMonth() / 3);
                    start = new Date(today.getFullYear(), quarter * 3, 1);
                    end = new Date(today.getFullYear(), quarter * 3 + 3, 0);
                    break;
                case "yearly":
                    start = new Date(today.getFullYear(), 0, 1);
                    end = new Date(today.getFullYear(), 11, 31);
                    break;
            }

            $("#dateFrom").val(start.toISOString().slice(0, 10));
            $("#dateTo").val(end.toISOString().slice(0, 10));
        }

        $("#filterSelect").on("change", function () {
            setDateRange(this.value);
        });

        $("#btnReset").on("click", function () {
            const currentFilter = $("#filterSelect").val();
            setDateRange(currentFilter);
            $("#filterForm").submit();
        });

        $(document).ready(function () {
            const from = $("#dateFrom").val();
            const to = $("#dateTo").val();
            if (!from || !to) {
                setDateRange($("#filterSelect").val());
            }
        });

        $("#btnDownloadPDF").on("click", function () {
            const filter = $("#filterSelect").val().toUpperCase();
            const element = document.querySelector('.report-container');

            const opt = {
                margin: 10,
                filename: `TourismReport_${filter}_${new Date().toISOString().slice(0, 10)}.pdf`,
                image: { type: 'jpeg', quality: 0.98 },
                html2canvas: { scale: 2, useCORS: true },
                jsPDF: { unit: 'mm', format: 'a4', orientation: 'landscape' }
            };

            html2pdf().set(opt).from(element).save();
        });

                   $("#btnDownloadExcel").on("click", function () {
            var table = document.getElementById("tourismReportTable");
            var wb = XLSX.utils.book_new();

            // Format dates
            function formatDate(dateStr) {
                const date = new Date(dateStr);
                const options = { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' };
                return date.toLocaleDateString('en-US', options);
            }

            var dateFrom = formatDate($("#dateFrom").val());
            var dateTo = formatDate($("#dateTo").val());
            var municipality = "ALEGRIA, CEBU";
            var attraction = "Canyoneering Adventure";

            // Header rows: each info in its own row
            var headerRows = [
                [{v: "Tourism Attraction Visitor Record", s: {alignment: {horizontal: "center"}}}], // Title
                [{v: "Date From", s: {alignment: {horizontal: "center"}}}, {v: dateFrom, s: {alignment: {horizontal: "center"}}}],
                [{v: "Date To", s: {alignment: {horizontal: "center"}}}, {v: dateTo, s: {alignment: {horizontal: "center"}}}],
                [{v: "Municipality", s: {alignment: {horizontal: "center"}}}, {v: municipality}],
                [{v: "Attraction Spot", s: {alignment: {horizontal: "center"}}}, {v: attraction}],
                [] // empty row before table
            ];

            // Convert header to worksheet
            var ws = XLSX.utils.aoa_to_sheet(headerRows);

            // Convert table to sheet
            var wsTable = XLSX.utils.table_to_sheet(table);

            // Append table below header
            var rowOffset = headerRows.length;
            var range = XLSX.utils.decode_range(wsTable['!ref']);
            for (var R = range.s.r; R <= range.e.r; ++R) {
                for (var C = range.s.c; C <= range.e.c; ++C) {
                    var cellAddress = {c: C, r: R + rowOffset};
                    var origCell = wsTable[XLSX.utils.encode_cell({c: C, r: R})];
                    if (origCell) ws[XLSX.utils.encode_cell(cellAddress)] = origCell;
                }
            }

            // Update sheet range
            var newRange = XLSX.utils.decode_range(wsTable['!ref']);
            newRange.e.r += rowOffset;
            ws['!ref'] = XLSX.utils.encode_range(newRange);

            // Center header cells
            for (let i = 0; i < headerRows.length; i++) {
                for (let j = 0; j < headerRows[i].length; j++) {
                    var cell = ws[XLSX.utils.encode_cell({c: j, r: i})];
                    if (cell) {
                        cell.s = cell.s || {};
                        cell.s.alignment = { horizontal: "center", vertical: "center" };
                    }
                }
            }

            // Append worksheet and download
            XLSX.utils.book_append_sheet(wb, ws, "Tourism Report");
            var filter = $("#filterSelect").val().toUpperCase();
            XLSX.writeFile(wb, `TourismReport_${filter}_${new Date().toISOString().slice(0,10)}.xlsx`);
        });
