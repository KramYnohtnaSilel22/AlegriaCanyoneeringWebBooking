document.addEventListener("DOMContentLoaded", function () {
    var toastEl = document.getElementById("toastMessage");
    if (toastEl) {
        var toast = new bootstrap.Toast(toastEl, {
            delay: 3000
        });
        toast.show();
    }
});




document.addEventListener('DOMContentLoaded', function () {
    const driverSelect = document.getElementById('driverSelect');
    const guideSelect = document.getElementById('guideSelect');
    const selectedDriversDiv = document.getElementById('selectedDrivers');
    const selectedGuidesDiv = document.getElementById('selectedGuides');

    function updateSelectedItems(selectElement, displayDiv) {
        if (!selectElement || !displayDiv) return;
        const selectedOptions = Array.from(selectElement.selectedOptions || []);
        displayDiv.innerHTML = '';

        if (selectedOptions.length === 0) {
            displayDiv.innerHTML = '<em class="text-muted">None selected</em>';
            return;
        }

        selectedOptions.forEach(option => {
            const item = document.createElement('span');
            item.className = 'selected-item';
            item.innerHTML = `${option.text} <span class="remove-item" data-value="${option.value}">&times;</span>`;
            displayDiv.appendChild(item);

            const removeBtn = item.querySelector('.remove-item');
            if (removeBtn) {
                removeBtn.addEventListener('click', function () {
                    option.selected = false;
                    updateSelectedItems(selectElement, displayDiv);
                });
            }
        });
    }

    function attachHandlers(selectElement, displayDiv) {
        if (!selectElement || !displayDiv) return;

        selectElement.addEventListener('change', function () {
            updateSelectedItems(selectElement, displayDiv);
        });

        selectElement.addEventListener('dblclick', function (e) {
            if (e.target && e.target.tagName === 'OPTION') {
                e.target.selected = !e.target.selected;
                updateSelectedItems(selectElement, displayDiv);
            }
        });

        updateSelectedItems(selectElement, displayDiv);
    }

    if (driverSelect && selectedDriversDiv) {
        attachHandlers(driverSelect, selectedDriversDiv);
    }
    if (guideSelect && selectedGuidesDiv) {
        attachHandlers(guideSelect, selectedGuidesDiv);
    }
});